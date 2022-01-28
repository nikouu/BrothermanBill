using CliWrap;
using Discord;
using Discord.Commands;

namespace BrothermanBill.Modules
{
    public class GeneralModule : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        public Task PingAsync()
            => ReplyAsync($"Current Ping {Context.Client.Latency}ms");

        [Command("userinfo")]
        public async Task UserInfoAsync(IUser user = null)
        {
            user ??= Context.User;
            await ReplyAsync(user.ToString());
        }

        [Command("setgame")]
        public async Task GameAsync([Remainder] string setgame)  // [Remainder] takes all arguments as one
        {
            await Context.Client.SetGameAsync(setgame);
            await ReplyAsync("Set game succeeded");
        }

        [Command("kkona")]
        [Summary("Echoes a message.")]
        public async Task KkonaAsync() => await ReplyAsync("KKona brother <:Kkona:917645359633813545>");

        [Command("cum")]
        public async Task CumAsync() => await ReplyAsync("Cum");

        [Command("Pick")]
        public async Task Pick([Remainder] string list)
        {
            var games = list.Split(" ");
            var random = new Random();
            var game = games[random.Next(games.Length)];
            await ReplyAsync(game);
        }
    }
}
