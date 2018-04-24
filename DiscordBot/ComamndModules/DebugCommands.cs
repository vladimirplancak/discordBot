using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.ComamndModules
{
    [Group("debug")]
    public class InfoModule : ModuleBase
    {

        [Command("ping"), Summary("Echos a message.")]
        public async Task Say([Remainder, Summary("The text to echo")] string echo)
        {
            // ReplyAsync is a method on ModuleBase
            await ReplyAsync(echo);
        }

        [Command("test")]
        public async Task Test()
        {
        }

    }
}
