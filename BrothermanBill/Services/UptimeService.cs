namespace BrothermanBill.Services
{
    // Turning this into a record class breaks Victoria, though I'm guessing it breaks something else which then bubbles up via Victoria
    public class UptimeService
    {
        public DateTime StartTimeUtc { get; private set; }

        public UptimeService()
        {
            StartTimeUtc = DateTime.UtcNow;
        }

        public TimeSpan UpTime => DateTime.UtcNow - StartTimeUtc;
    }
}