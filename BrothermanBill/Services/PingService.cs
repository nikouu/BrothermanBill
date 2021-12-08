using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrothermanBill.Services
{
    public class PingService
    {
        private readonly DiscordSocketClient _client;
        public PingService(DiscordSocketClient client)
        {
            _client = client;
        }

        public async Task<bool> HandlePingAsync(SocketMessage messageParam, Guid instanceId)
        {
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
                    Console.WriteLine($"Brotherman Bill instance {newGuid} already running. Shutting down.");
                    await _client.StopAsync();
                    Console.WriteLine("Press any key to close...");
                    Console.ReadKey();
                    Environment.Exit(0);
                }
                else
                {
                    Console.WriteLine("Unknown message.");
                }
            }

            return false;
        }
    }
}
