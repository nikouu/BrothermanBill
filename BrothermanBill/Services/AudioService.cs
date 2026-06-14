using Lavalink4NET;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;

namespace BrothermanBill.Services
{
    public sealed class AudioService
    {
        private readonly IAudioService _audioService;
        private readonly ILogger _logger;
        private readonly StatusService _statusService;

        public AudioService(IAudioService audioService, ILogger<AudioService> logger, StatusService statusService)
        {
            _audioService = audioService;
            _logger = logger;
            _statusService = statusService;

            _audioService.TrackStarted += OnTrackStarted;
            _audioService.TrackEnded += OnTrackEnded;
            _audioService.TrackException += OnTrackException;
            _audioService.TrackStuck += OnTrackStuck;
            _audioService.WebSocketClosed += OnWebSocketClosed;
        }

        public async Task UpdateStatusWithTrackName(string? name = null)
        {
            _logger.LogInformation("Updated currently playing status to: {Name}", name);
            await _statusService.SetStatus(name);
        }

        private async Task OnTrackStarted(object sender, TrackStartedEventArgs args)
        {
            _logger.LogInformation("Now playing: {Title}", args.Track.Title);
            await UpdateStatusWithTrackName(args.Track.Title);
        }

        private async Task OnTrackEnded(object sender, TrackEndedEventArgs args)
        {
            _logger.LogInformation("Track ended: {Title}, Reason: {Reason}", args.Track.Title, args.Reason);

            if (args.Reason != TrackEndReason.Finished)
            {
                return;
            }

            // QueuedLavalinkPlayer handles auto-advance internally.
            // We just update status when the queue is empty.
            if (args.Player is IQueuedLavalinkPlayer queuedPlayer && queuedPlayer.Queue.Count == 0)
            {
                _logger.LogInformation("Queue completed.");
                await _statusService.SetReady();
            }
        }

        private Task OnTrackException(object sender, TrackExceptionEventArgs args)
        {
            _logger.LogError("Track {Title} threw an exception. Please check Lavalink console/logs.", args.Track.Title);
            return Task.CompletedTask;
        }

        private Task OnTrackStuck(object sender, TrackStuckEventArgs args)
        {
            _logger.LogError("Track {Title} got stuck for {Threshold}. Please check Lavalink console/logs.", args.Track.Title, args.Threshold);
            return Task.CompletedTask;
        }

        private async Task OnWebSocketClosed(object sender, WebSocketClosedEventArgs args)
        {
            _logger.LogCritical("Discord WebSocket connection closed with following reason: {Reason}", args.Reason);
            // Voice connection dropped — fall back to idle.
            await _statusService.SetReady();
        }
    }
}
