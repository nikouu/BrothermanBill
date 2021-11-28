using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrothermanBill
{
    internal class DiscordWrapper
    {
        private DiscordSocketClient SocketClient;
        private string Token;

        public DiscordWrapper(string token)
        {
            Token = token;
            SocketClient = new DiscordSocketClient();
            SocketClient.Log += Log;
        }

        public async Task Run()
        {
            await SocketClient.LoginAsync(TokenType.Bot, Token);
            await SocketClient.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

    }
}