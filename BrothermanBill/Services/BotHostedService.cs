using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Lavalink4NET;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.NetworkInformation;

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
        private readonly ILogger _lavalinkLogger;
        private Process? _lavalinkProcess;

        public BotHostedService(
            DiscordSocketClient client,
            InteractionService interactions,
            InteractionHandlerService interactionHandler,
            IConfiguration config,
            ILogger<BotHostedService> logger,
            ILoggerFactory loggerFactory,
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
            _lavalinkLogger = loggerFactory.CreateLogger("Lavalink"); // Lavalink's own stdout/stderr
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                StartLavalink();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Lavalink. Audio features will be unavailable.");
            }

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
                var botUser = _client.CurrentUser;
                if (botUser is null) return;

                // Ignore the bot's own voice state changes
                if (user.Id == botUser.Id) return;

                // Someone left or moved out of this channel
                var leftChannel = before.VoiceChannel;
                if (leftChannel is null) return;

                // Ask Lavalink (the layer that actually owns the connection) whether the
                // bot is connected in this guild, and to which channel. Discord.Net's
                // cached voice states can't be trusted here because Lavalink4NET makes
                // the voice connection directly.
                var player = await _lavalinkAudioService.Players.GetPlayerAsync(leftChannel.Guild.Id);
                if (player is null || player.VoiceChannelId != leftChannel.Id) return;

                // ConnectedUsers, not Users. SocketVoiceChannel inherits Users from
                // SocketTextChannel, where it means "everyone who can view the channel"
                var humansRemaining = leftChannel.ConnectedUsers.Any(u => u.Id != botUser.Id);
                if (humansRemaining) return;

                _logger.LogInformation("{Channel} is empty - leaving in 10s if still empty.", leftChannel.Name);

                // Defer so we don't block the gateway, then re-check after the grace period.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));

                        var currentPlayer = await _lavalinkAudioService.Players.GetPlayerAsync(leftChannel.Guild.Id);
                        var botStillConnected = currentPlayer is not null && currentPlayer.VoiceChannelId == leftChannel.Id;
                        var stillEmpty = !leftChannel.ConnectedUsers.Any(u => u.Id != botUser.Id);

                        if (botStillConnected && stillEmpty)
                        {
                            _logger.LogInformation("Leaving {Channel} - still empty after 10s.", leftChannel.Name);
                            await currentPlayer!.DisconnectAsync();
                            await _statusService.SetReady();
                        }
                        else
                        {
                            _logger.LogInformation("Staying in {Channel} - someone rejoined within 10s or already disconnected.", leftChannel.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during delayed leave of {Channel}.", leftChannel.Name);
                    }
                });
            };

            _client.Ready += async () =>
            {
                await _statusService.SetStatus("Waiting for Lavalink...");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _lavalinkAudioService.WaitForReadyAsync(CancellationToken.None).ConfigureAwait(false);
                        await _statusService.SetReady();
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
                try
                {
                    _lavalinkProcess.Kill(entireProcessTree: true);
                    _logger.LogInformation("Lavalink process terminated.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to terminate Lavalink process.");
                }
            }

            _lavalinkProcess?.Dispose();
            _lavalinkProcess = null;
        }

        private const int LavalinkPort = 2333;

        private void StartLavalink()
        {
            // In a container, Lavalink runs as its own Compose service, so the bot
            // must not self-spawn it. The .NET base image sets this env var to "true".
            if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
            {
                _logger.LogInformation("Running in a container; using external Lavalink, not self-spawning.");
                return;
            }

            // Only skip if something is actually listening on the Lavalink port — not
            // merely because *some* java process exists. The old "any java" check
            // silently skipped launching whenever an unrelated (or orphaned) JVM was
            // running, leaving the client talking to a stale/missing node.
            if (IsPortInUse(LavalinkPort))
            {
                _logger.LogInformation("Port {Port} already in use; assuming Lavalink is already running, skipping launch.", LavalinkPort);
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
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            Process? process;
            try
            {
                process = Process.Start(processInfo);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogError(ex, "Could not start 'java'. Is Java 17+ installed and on PATH? Lavalink (and audio) will be unavailable.");
                return;
            }

            if (process is null)
            {
                _logger.LogError("Failed to start Lavalink process.");
                return;
            }

            _lavalinkProcess = process;

            // Surface Lavalink's own output through our logger instead of swallowing it.
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) _lavalinkLogger.LogInformation("{Line}", e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) _lavalinkLogger.LogError("{Line}", e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Tie Lavalink's lifetime to ours so a hard kill (e.g. stopping the VS
            // debugger, which skips StopAsync) can't leave an orphan holding the port.
            if (OperatingSystem.IsWindows() && !ChildProcessTracker.AddProcess(process))
            {
                _logger.LogWarning("Could not register Lavalink with the kill-on-exit job object; it may linger if the bot is force-killed.");
            }

            _logger.LogInformation("Started Lavalink process (PID {Pid}).", process.Id);
        }

        private static bool IsPortInUse(int port)
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(endpoint => endpoint.Port == port);
        }
    }
}
