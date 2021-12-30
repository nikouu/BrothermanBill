using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace BrothermanBill.Services
{
    public sealed class AudioService
    {
        private readonly LavaNode _lavaNode;
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTokens;
        private readonly DiscordSocketClient _socketClient;
        private readonly ILogger _logger;

        public AudioService(LavaNode lavaNode, DiscordSocketClient socketClient, ILogger<AudioService> logger)
        {
            _lavaNode = lavaNode;
            _socketClient = socketClient;
            _logger = logger;
            _disconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();

            _lavaNode.OnLog += arg =>
            {
                // todo: tidy
                if (arg.Message.Contains("playerUpdate"))
                {
                    return Task.CompletedTask;
                }
                _logger.LogInformation(arg.Message);
                return Task.CompletedTask;
            };

            _lavaNode.OnPlayerUpdated += OnPlayerUpdated;
            _lavaNode.OnStatsReceived += OnStatsReceived;
            _lavaNode.OnTrackEnded += OnTrackEnded;
            _lavaNode.OnTrackStarted += OnTrackStarted;
            _lavaNode.OnTrackException += OnTrackException;
            _lavaNode.OnTrackStuck += OnTrackStuck;
            _lavaNode.OnWebSocketClosed += OnWebSocketClosed;
        }

        public async Task UpdateStatusWithTrackName(string? name = null)
        {
            _logger.LogInformation($"Updated currently playing status to: {name}");
            await _socketClient.SetActivityAsync(new Game(name));
        }

        private Task OnPlayerUpdated(PlayerUpdateEventArgs arg)
        {
            //_logger.LogInformation($"Track update received for {arg.Track?.Title}: {arg.Position}");
            return Task.CompletedTask;
        }

        private Task OnStatsReceived(StatsEventArgs arg)
        {
            _logger.LogInformation($"Lavalink has been up for {arg.Uptime}.");
            return Task.CompletedTask;
        }

        private async Task OnTrackStarted(TrackStartEventArgs arg)
        {
            //await arg.Player.TextChannel.SendMessageAsync($"Now playing: {arg.Track.Title}");
            _logger.LogInformation($"Now playing: {arg.Track.Title}");
            await UpdateStatusWithTrackName(arg.Track.Title);
            if (!_disconnectTokens.TryGetValue(arg.Player.VoiceChannel.Id, out var value))
            {
                return;
            }

            if (value.IsCancellationRequested)
            {
                return;
            }

            value.Cancel(true);
            await arg.Player.TextChannel.SendMessageAsync("Auto disconnect has been cancelled!");
        }

        private async Task OnTrackEnded(TrackEndedEventArgs args)
        {
            if (args.Reason != TrackEndReason.Finished)
            {
                return;
            }

            // if paused then resume

            var player = args.Player;
            if (!player.Queue.TryDequeue(out var lavaTrack))
            {

                //await player.TextChannel.SendMessageAsync("Queue completed.");
                _logger.LogInformation("Queue completed.");
                //_ = InitiateDisconnectAsync(args.Player, TimeSpan.FromMinutes(10));
                return;
            }

            if (lavaTrack is null)
            {
                await player.TextChannel.SendMessageAsync("Next item in queue is not a track.");
                return;
            }

            await args.Player.PlayAsync(x =>
            {
                x.Track = lavaTrack;
                x.StartTime = lavaTrack.Position;
            });
            //await args.Player.TextChannel.SendMessageAsync($"{args.Reason}: {args.Track.Title}\nNow playing: {lavaTrack.Title}");
        }

        private async Task InitiateDisconnectAsync(LavaPlayer player, TimeSpan timeSpan)
        {
            if (!_disconnectTokens.TryGetValue(player.VoiceChannel.Id, out var value))
            {
                value = new CancellationTokenSource();
                _disconnectTokens.TryAdd(player.VoiceChannel.Id, value);
            }
            else if (value.IsCancellationRequested)
            {
                _disconnectTokens.TryUpdate(player.VoiceChannel.Id, new CancellationTokenSource(), value);
                value = _disconnectTokens[player.VoiceChannel.Id];
            }

            // await player.TextChannel.SendMessageAsync($"Auto disconnect initiated! Disconnecting in {timeSpan}...");
            var isCancelled = SpinWait.SpinUntil(() => value.IsCancellationRequested, timeSpan);
            if (isCancelled)
            {
                return;
            }

            await UpdateStatusWithTrackName(null);
            await _lavaNode.LeaveAsync(player.VoiceChannel);
        }

        private async Task OnTrackException(TrackExceptionEventArgs arg)
        {
            _logger.LogError($"Track {arg.Track.Title} threw an exception. Please check Lavalink console/logs.");
            //arg.Player.Queue.Enqueue(arg.Track);
            await arg.Player.TextChannel.SendMessageAsync($"Track {arg.Track.Title} could not play.");
        }

        private async Task OnTrackStuck(TrackStuckEventArgs arg)
        {
            _logger.LogError(
                $"Track {arg.Track.Title} got stuck for {arg.Threshold}ms. Please check Lavalink console/logs.");
            //arg.Player.Queue.Enqueue(arg.Track);
            await arg.Player.TextChannel.SendMessageAsync($"Track {arg.Track.Title} could not play. Reason: Stuck");
        }

        private Task OnWebSocketClosed(WebSocketClosedEventArgs arg)
        {
            _logger.LogCritical($"Discord WebSocket connection closed with following reason: {arg.Reason}");
            return Task.CompletedTask;
        }
    }
}