using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BrothermanBill.Services
{
    public class BotHostedService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly CommandHandlerService _commandHandler;
        private readonly IConfiguration _config;
        private readonly ILogger<BotHostedService> _logger;
        private readonly StatusService _statusService;
        private readonly AudioService _audioService;
        private Process? _lavalinkProcess;

        public BotHostedService(
            DiscordSocketClient client,
            CommandService commands,
            CommandHandlerService commandHandler,
            IConfiguration config,
            ILogger<BotHostedService> logger,
            StatusService statusService,
            AudioService audioService)
        {
            _client = client;
            _commands = commands;
            _commandHandler = commandHandler;
            _config = config;
            _logger = logger;
            _statusService = statusService;
            _audioService = audioService; // triggers DI construction & event subscription
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            StartLavalink();

            _client.Log += msg =>
            {
                _logger.LogInformation("{Message}", msg.ToString());
                return Task.CompletedTask;
            };

            _commands.Log += msg =>
            {
                _logger.LogInformation("{Message}", msg.ToString());
                return Task.CompletedTask;
            };

            _client.UserVoiceStateUpdated += async (user, before, after) =>
            {
                var currentUser = _client.CurrentUser?.Username;
                if (currentUser is null) return;

                if (after.VoiceChannel is null && before.VoiceChannel?.Users.Any(x => x.Username == currentUser) == true)
                {
                    var hasOtherUsers = before.VoiceChannel.Users.Any(x => x.Username != currentUser);
                    if (!hasOtherUsers)
                    {
                        _logger.LogInformation("Leaving {Channel} as the last user, {User}, has left.", before.VoiceChannel.Name, user.Username);
                        await before.VoiceChannel.DisconnectAsync();
                    }
                }
            };

            _client.Ready += async () =>
            {
                await _statusService.SetStatus("Ready");
            };

            await _commandHandler.InstallCommandsAsync();
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
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Minimized
            };

            _lavalinkProcess = Process.Start(processInfo);
            _logger.LogInformation("Started Lavalink process.");
        }
    }
}
