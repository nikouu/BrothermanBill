namespace BrothermanBill.Services
{
    public record class UptimeService(DateTime StartTimeUtc)
    {
        public TimeSpan UpTime => DateTime.UtcNow - StartTimeUtc;
    }
}