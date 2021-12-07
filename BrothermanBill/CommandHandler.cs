using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BrothermanBill
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        public readonly Guid InstanceId;

        // Retrieve client and CommandService instance via ctor
        public CommandHandler(DiscordSocketClient client, CommandService commands, IServiceProvider services, IOptions<InstanceId> options)
        {
            _commands = commands;
            _client = client;
            _services = services;
            InstanceId = options.Value.Id; 
        }

        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived event into our command handler
            _client.MessageReceived += HandleCommandAsync;

            // Here we discover all of the command modules in the entry 
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the 
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                            services: _services);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;

            var isSelf = message.Author.Id == _client.CurrentUser.Id;

            Console.WriteLine($"Recieved Message: {message}");
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            // if the ping message was sent by this bot message was sent by this bot
            if (isSelf && message.ToString() == $"ping {InstanceId}")
            {
                return;
            }

            // was own message
            if (isSelf && message.ToString() == $"pong {InstanceId}")
            {
                return;
            }

            // show that this bot is active
            if (isSelf && message.ToString().Contains("ping")) {
                var channel = _client.GetChannel(message.Channel.Id) as IMessageChannel;
                channel.SendMessageAsync($"pong {InstanceId}");
                return;
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

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasCharPrefix('!', ref argPos) ||
                message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
                return;

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_client, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            await _commands.ExecuteAsync(
                context: context,
                argPos: argPos,
                services: _services);
        }
    }
}
