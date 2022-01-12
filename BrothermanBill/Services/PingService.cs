using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BrothermanBill.Services
{
    public class PingService
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger _logger;
        public PingService(DiscordSocketClient client, ILogger<PingService> logger)
        {
            _client = client;
            _logger = logger;
        }

        // A cheap way to ensure only one instance is running at a time. 
        // A if a pong reply to a ping message occurs, another bot is running
        // and as such, exit this instance. Discord itself is the free "state storage".
        public async Task<bool> HandlePingAsync(SocketMessage messageParam, Guid instanceId)
        {
            _logger.LogInformation($"Handling ping: {instanceId}");
            var message = messageParam as SocketUserMessage;
            var messageString = message.ToString();

            var isSelf = message.Author.Id == _client.CurrentUser.Id;

            if (isSelf && messageString == $"ping {instanceId}")
            {
                return true;
            }

            // was own message
            if (isSelf && messageString == $"pong {instanceId}")
            {
                return true;
            }

            // show that this bot is active
            if (isSelf && messageString.Contains("ping"))
            {
                var channel = _client.GetChannel(message.Channel.Id) as IMessageChannel;
                await channel.SendMessageAsync($"pong {instanceId}");
                return true;
            }

            // if there is another bot active already
            if (isSelf && message.ToString().Contains("pong"))
            {
                var messageArray = message.ToString().Split(" ");

                if (Guid.TryParse(messageArray[1], out var newGuid))
                {
                    _logger.LogInformation($"Brotherman Bill instance {newGuid} already running. Shutting down.");
                    await _client.StopAsync();
                    Console.WriteLine("Press any key to close...");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
                else
                {
                    _logger.LogWarning("Unknown message");
                }
            }

            return false;
        }
    }
}
