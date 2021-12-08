using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrothermanBill.Modules
{
    public class MiscModule : ModuleBase<SocketCommandContext>
    {
        [Command("cum")]
        public Task CumAsync() => ReplyAsync("Cum");
    }
}