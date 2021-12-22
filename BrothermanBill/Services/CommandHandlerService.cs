using BrothermanBill.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BrothermanBill
{
    public class CommandHandlerService
    {
        public readonly Guid InstanceId;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        private readonly PingService _pingService;
        private readonly ILogger _logger;
        
        

        // Retrieve client and CommandService instance via ctor
        public CommandHandlerService(DiscordSocketClient client, CommandService commands, IServiceProvider services, IOptions<InstanceId> options, PingService pingService, ILogger<CommandHandlerService> logger)
        {
            InstanceId = options.Value.Id;
            _commands = commands;
            _client = client;
            _services = services;
            _pingService = pingService;
            _logger = logger;
            
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

            _logger.LogInformation($"Recieved Message: {message}");
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            var shouldReturn = await _pingService.HandlePingAsync(messageParam, InstanceId);
            
            if (shouldReturn) return;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            // making sure the bot can call its own commands might be a mistake

            // if the message does not start with a command prefix
            if(!message.HasCharPrefix('!', ref argPos)) {
                return;
            }

            //// if the message does not mention the bot
            //if (!message.HasMentionPrefix(_client.CurrentUser, ref argPos))
            //{
            //    return;
            //}

            // if the message is a bot that isnt itself
            if (message.Author.IsBot && message.Author != _client.CurrentUser)
            {
                return;
            }

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
