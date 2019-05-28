using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.ComamndModules
{
    public class EventCommands : ModuleBase
    {
        [Command("createEvent", RunMode = RunMode.Async), Summary("Create event in following format: name|date(mm-dd)")]
        public async Task CreateEvent([Summary("Cr.")] string eventCommand = null)
        {


            EmbedBuilder builder = new EmbedBuilder();
            builder.WithAuthor("Vladimir Plancak");
            builder.Color = Color.DarkMagenta;

            var titleBuilder = new EmbedFieldBuilder();

            //create title
            titleBuilder.WithName("Dota fridays: ").WithValue("Come if you would like to play dota!").Build();

            var title2Builder = new EmbedFieldBuilder();
            title2Builder.WithName("Going: ").WithValue("Mirox, Pishta, Limun").Build();

            builder.AddField(titleBuilder);
            builder.AddField(title2Builder);
            builder.WithFooter(it => {
                it.Text = DateTime.Now.ToString();
            });

            await ReplyAsync("", embed: builder.Build());
        }
    }
}

