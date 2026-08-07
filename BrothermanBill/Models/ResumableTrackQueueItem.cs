using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;

namespace BrothermanBill.Models
{
    public record class ResumableTrackQueueItem(TrackReference Reference, TimeSpan StartPosition) : ITrackQueueItem
    {
        public ResumableTrackQueueItem(LavalinkTrack track, TimeSpan startPosition)
            : this(new TrackReference(track), startPosition)
        {
        }
    }
}
