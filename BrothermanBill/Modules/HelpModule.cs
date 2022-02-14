using BrothermanBill.Models;
using Discord.Commands;

namespace BrothermanBill.Modules
{
    [Name("Help Module")]
    [Summary("Provides information about commands.")]
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commandService;
        private readonly EmbedHandler _embedHandler;

        public HelpModule(CommandService commandService, EmbedHandler embedHandler)
        {
            _commandService = commandService;
            _embedHandler = embedHandler;
        }

        [Command("Help")]
        [Alias("Commands")]
        [Summary("Lists all commands.")]
        public async Task Help()
        {
            var commandModules = new List<CommandModuleDto>();

            foreach (var module in _commandService.Modules)
            {
                var commandModuleDto = new CommandModuleDto
                {
                    Name = module.Name,
                    Summary = module.Summary,
                    Modules = new List<CommandDto>()
                };

                foreach (var command in module.Commands)
                {
                    var commandDto = new CommandDto
                    {
                        Name = command.Name,
                        Summary = command.Summary,
                        Aliases = command.Aliases.ToList()
                    };

                    commandModuleDto.Modules.Add(commandDto);
                }

                commandModules.Add(commandModuleDto);
            }

            foreach (var module in commandModules)
            {
                var embed = await _embedHandler.CreateCommandModuleEmbed(module);

                if (commandModules.ElementAt(0) == module)
                {
                    await ReplyAsync("Commands:", embed: embed);
                }
                else
                {
                    await ReplyAsync(embed: embed);
                }
            }
        }
    }
}
