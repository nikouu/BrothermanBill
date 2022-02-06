using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BrothermanBill.Services
{
    public class StatusService
    {
        private readonly DiscordSocketClient _socketClient;
        private readonly ILogger _logger;

        public StatusService(DiscordSocketClient socketClient, ILogger<StatusService> logger)
        {
            _socketClient = socketClient;
            _logger = logger;
        }

        public async Task SetStatus(string status)
        {
            _logger.LogInformation($"Setting status to {status}");
            await _socketClient.SetActivityAsync(new Game(status));
        }
    }
}