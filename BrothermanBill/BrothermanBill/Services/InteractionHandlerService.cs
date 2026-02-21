using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace BrothermanBill.Services
{
    public class InteractionHandlerService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactions;
        private readonly IServiceProvider _services;
        private readonly ILogger _logger;

        private bool _initialized;

        public InteractionHandlerService(DiscordSocketClient client, InteractionService interactions, IServiceProvider services, ILogger<InteractionHandlerService> logger)
        {
            _client = client;
            _interactions = interactions;
            _services = services;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return;
            _initialized = true;

            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            _client.InteractionCreated += HandleInteractionAsync;

            _client.Ready += async () =>
            {
                await _interactions.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Slash commands registered globally.");
            };
        }

        private async Task HandleInteractionAsync(SocketInteraction interaction)
        {
            try
            {
                var context = new SocketInteractionContext(_client, interaction);
                var result = await _interactions.ExecuteCommandAsync(context, _services);

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Interaction failed: {Error} | Reason: {Reason}", result.Error, result.ErrorReason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred executing an interaction.");
            }
        }
    }
}
