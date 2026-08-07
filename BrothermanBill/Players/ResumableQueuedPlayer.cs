using BrothermanBill.Models;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Microsoft.Extensions.Logging;

namespace BrothermanBill.Players
{
    public class ResumableQueuedPlayer : QueuedLavalinkPlayer
    {
        private readonly ILogger<ResumableQueuedPlayer> _logger;

        public ResumableQueuedPlayer(IPlayerProperties<ResumableQueuedPlayer, QueuedLavalinkPlayerOptions> properties)
            : base(properties)
        {
            _logger = properties.Logger as ILogger<ResumableQueuedPlayer>
                ?? throw new InvalidOperationException("Logger not available.");
        }

        protected override async ValueTask NotifyTrackStartedAsync(ITrackQueueItem queueItem, CancellationToken cancellationToken = default)
        {
            await base.NotifyTrackStartedAsync(queueItem, cancellationToken).ConfigureAwait(false);

            if (queueItem is ResumableTrackQueueItem resumable && resumable.StartPosition > TimeSpan.Zero)
            {
                var currentTrack = CurrentTrack;
                var resumableTrack = resumable.Reference.Track;

                // Only seek if the track that actually started is the one we saved the position for
                if (currentTrack is not null && resumableTrack is not null
                    && currentTrack.Identifier == resumableTrack.Identifier)
                {
                    _logger.LogInformation("Resuming {Title} at {Position}.", currentTrack.Title, resumable.StartPosition);
                    await SeekAsync(resumable.StartPosition, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogWarning(
                        "ResumableTrackQueueItem mismatch: expected {ExpectedTrack} but got {ActualTrack}. Skipping seek.",
                        resumableTrack?.Title ?? "null",
                        currentTrack?.Title ?? "null");
                }
            }
        }
    }
}
