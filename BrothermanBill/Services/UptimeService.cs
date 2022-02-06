namespace BrothermanBill.Services
{
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
