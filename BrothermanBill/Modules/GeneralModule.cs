using BrothermanBill.Services;
using Discord.Interactions;

namespace BrothermanBill.Modules
{
    public class GeneralModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly StatusService _statusService;
        private readonly UptimeService _uptimeService;

        public GeneralModule(StatusService statusService, UptimeService uptimeService)
        {
            _statusService = statusService;
            _uptimeService = uptimeService;
        }

        [SlashCommand("ping", "Gets latency between Brotherman Bill and Discord servers.")]
        public async Task PingAsync()
            => await RespondAsync($"Current Ping {Context.Client.Latency}ms");

        [SlashCommand("setgame", "Manually set the status of Brotherman Bill.")]
        public async Task GameAsync([Summary(description: "The status to set")] string status)
        {
            await _statusService.SetStatus(status);
            await RespondAsync("Set game succeeded");
        }

        [SlashCommand("kkona", "Kkona brother")]
        public async Task KkonaAsync() => await RespondAsync("KKona brother <:Kkona:917645359633813545>");

        [SlashCommand("cum", "Checks if Brotherman Bill is running.")]
        public async Task CumAsync() => await RespondAsync("Cum");

        [SlashCommand("pick", "Randomly selects a word from a list of space separated words.")]
        public async Task Pick([Summary(description: "Space separated list of words")] string list)
        {
            var games = list.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (games.Length == 0)
            {
                await RespondAsync("Give me a space separated list of words to pick from.");
                return;
            }

            await RespondAsync(games[Random.Shared.Next(games.Length)]);
        }

        [SlashCommand("uptime", "Current Brotherman Bill uptime.")]
        public async Task UpTime()
            => await RespondAsync($"Uptime: {_uptimeService.UpTime:dd\\.hh\\:mm\\:ss}");
    }
}
