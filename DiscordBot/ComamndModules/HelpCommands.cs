using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.ComamndModules
{
    public class HelpCommands : ModuleBase
    {

        private CommandService _commandService;
        public HelpCommands(CommandService commandService)
        {
            _commandService = commandService;
        }

        [Command("help"), Summary("Show all commands with their description")]
        public async Task Help()
        {
            var builder = new EmbedBuilder();
            builder.Color = Color.Green;
            builder.Footer = new EmbedFooterBuilder()
            {
                Text = "To execute any command use this syntax: '!<command_name> <additional_parameters>'"
            };

            _commandService.Commands.ToList().ForEach(it =>
            {
                if(it.Module.Name == "debug")
                {
                    return;
                }

                var summary = it.Summary;
                if(summary == null)
                {
                    summary = "No description!";
                }

                builder.AddField(it.Name, summary);
            });
            builder.Build().ToString();

            await ReplyAsync("", false, builder.Build());
        }

    }
}
