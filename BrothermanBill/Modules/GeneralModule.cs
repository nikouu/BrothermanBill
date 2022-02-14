using BrothermanBill.Services;
using CliWrap;
using Discord.Commands;

namespace BrothermanBill.Modules
{
    [Name("General Module")]
    [Summary("Miscellaneous commands.")]
    public class GeneralModule : ModuleBase<SocketCommandContext>
    {
        private readonly StatusService _statusService;
        private readonly UptimeService _uptimeService;

        public GeneralModule(StatusService statusService, UptimeService uptimeService)
        {
            _statusService = statusService;
            _uptimeService = uptimeService;
        }

        [Command("Ping")]
        [Summary("Gets latency between Brotherman Bill and Discord servers.")]
        public Task PingAsync()
            => ReplyAsync($"Current Ping {Context.Client.Latency}ms");

        [Command("Setgame")]
        [Summary("Manually set the status of Brotherman Bill")]
        public async Task GameAsync([Remainder] string setgame)  // [Remainder] takes all arguments as one
        {
            await _statusService.SetStatus(setgame);
            await ReplyAsync("Set game succeeded");
        }

        [Command("Kkona")]
        [Summary("Kkona brother")]
        public async Task KkonaAsync() => await ReplyAsync("KKona brother <:Kkona:917645359633813545>");

        [Command("Cum")]
        [Summary("Checks if Brotherman Bill is running.")]
        public async Task CumAsync() => await ReplyAsync("Cum");

        [Command("Pick")]
        [Summary("Randomly selects a word from a list of space separated words.")]
        public async Task Pick([Remainder] string list)
        {
            var games = list.Split(" ");
            var random = new Random();
            var game = games[random.Next(games.Length)];
            await ReplyAsync(game);
        }

        [Command("UpTime")]
        [Summary("Current Brotherman Bill uptime.")]
        public async Task UpTime()
            => await ReplyAsync($"Uptime: {_uptimeService.UpTime:dd\\.hh\\:mm\\:ss}");
    }
}
