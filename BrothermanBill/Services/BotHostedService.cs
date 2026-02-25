using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BrothermanBill.Services
{
    public class BotHostedService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly InteractionHandlerService _interactionHandler;
        private readonly IConfiguration _config;
        private readonly ILogger<BotHostedService> _logger;
        private readonly StatusService _statusService;
        private readonly AudioService _audioService;
        private readonly IAudioService _lavalinkAudioService;
        private Process? _lavalinkProcess;

        public BotHostedService(
            DiscordSocketClient client,
            InteractionService interactions,
            InteractionHandlerService interactionHandler,
            IConfiguration config,
            ILogger<BotHostedService> logger,
            StatusService statusService,
            AudioService audioService,
            IAudioService lavalinkAudioService)
        {
            _client = client;
            _interactions = interactions;
            _interactionHandler = interactionHandler;
            _config = config;
            _logger = logger;
            _statusService = statusService;
            _audioService = audioService; // triggers DI construction & event subscription
            _lavalinkAudioService = lavalinkAudioService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            StartLavalink();

            _client.Log += msg =>
            {
                _logger.LogInformation("{Message}", msg.ToString());
                return Task.CompletedTask;
            };

            _interactions.Log += msg =>
            {
                _logger.LogInformation("{Message}", msg.ToString());
                return Task.CompletedTask;
            };

            _client.UserVoiceStateUpdated += async (user, before, after) =>
            {
                // Ignore the bot's own voice state changes
                if (user.Id == _client.CurrentUser?.Id) return;

                var botUser = _client.CurrentUser;
                if (botUser is null) return;

                // Check if someone left or moved from the channel the bot is in
                var leftChannel = before.VoiceChannel;
                if (leftChannel is null) return;

                // Is the bot in the channel the user just left?
                var botInChannel = leftChannel.Users.Any(u => u.Id == botUser.Id);
                if (!botInChannel) return;

                // Is the bot the only one remaining?
                var otherUsers = leftChannel.Users.Where(u => u.Id != botUser.Id);
                if (!otherUsers.Any())
                {
                    _logger.LogInformation("Leaving {Channel} — no other users remaining.", leftChannel.Name);
                    await leftChannel.DisconnectAsync();
                }
            };

            _client.Ready += async () =>
            {
                await _statusService.SetStatus("Waiting for Lavalink...");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _lavalinkAudioService.WaitForReadyAsync(CancellationToken.None).ConfigureAwait(false);
                        await _statusService.SetStatus("Ready");
                        _logger.LogInformation("Lavalink is ready.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed waiting for Lavalink.");
                    }
                });
            };

            await _interactionHandler.InitializeAsync();
            await _statusService.SetStatus("Starting up");

            await _client.LoginAsync(TokenType.Bot, _config["DiscordBotToken"]);
            await _client.StartAsync();

            _logger.LogInformation("Discord bot started.");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Shutting down...");

            await _client.StopAsync();
            await _client.LogoutAsync();

            if (_lavalinkProcess is { HasExited: false })
            {
                _lavalinkProcess.Kill();
                _lavalinkProcess.Dispose();
                _lavalinkProcess = null;
                _logger.LogInformation("Lavalink process terminated.");
            }
        }

        private void StartLavalink()
        {
            var processList = Process.GetProcessesByName("java");
            if (processList.Length > 0)
            {
                _logger.LogInformation("Java process already running, skipping Lavalink launch.");
                return;
            }

            var lavalinkFile = Path.Combine(AppContext.BaseDirectory, "Lavalink", "Lavalink.jar");
            if (!File.Exists(lavalinkFile))
            {
                _logger.LogWarning("Lavalink.jar not found at {Path}.", lavalinkFile);
                return;
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = $"-jar \"{lavalinkFile}\"",
                WorkingDirectory = Path.Combine(AppContext.BaseDirectory, "Lavalink"),
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            _lavalinkProcess = Process.Start(processInfo);
            _logger.LogInformation("Started Lavalink process.");
        }
    }
}
