using Discord;
using Discord.Commands;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.ComamndModules
{
    public class EventCommands : ModuleBase
    {
        private EventService _eventService;

        public EventCommands(EventService eventService)
        {
            _eventService = eventService;
        }


        //Name|Description|time 
        private Embed CreateEventMessage(string eventCommandText, ref List<string> errors)
        {
            string[] parts = eventCommandText.Split('|');
            string eventName = parts[0];
            string eventDescription = parts[1];
            string[] eventTimeParts = parts[2].Split('-');

            DateTime? eventTime = null;

            if (int.TryParse(eventTimeParts[0], out int month) && int.TryParse(eventTimeParts[1], out int day) && int.TryParse(eventTimeParts[2], out int year))
            {
                eventTime = new DateTime(year, month, day);
            }
            else
            {
                errors.Add("Failed to pars date, please provide date in the format of mm--dd--yyyy");
                return null;
            }
            

            DateTimeOffset eventTimeOffset = new DateTimeOffset(eventTime.Value);

            EmbedBuilder builder = new EmbedBuilder();

            builder.WithTitle(eventName);
            builder.WithDescription(eventDescription);
            builder.WithTimestamp(eventTimeOffset);
            
            return builder.Build();
        }

        [Command("createEvent", RunMode = RunMode.Async), Summary("Create event in following format: Name|Description|Date(mm-dd-yyyy)")]
        public async Task CreateEvent([Summary("Cr.")] string eventCommand = null)
        {

            if (eventCommand == null || !eventCommand.Contains('|'))
            {
                await ReplyAsync("Event format should be: Name|Description|Date");
                return;
            }

            List<string> errors = new List<string>();

            Embed eventEmbed = CreateEventMessage(eventCommand, ref errors);

            if(errors.Count > 0)
            {
                //TODO: Send errors message.
                await ReplyAsync("Bad request.");
                return;
            }

            //using to extract message Id, so later on we can update message if necessary.
            IUserMessage eventMessage = await ReplyAsync("", embed: eventEmbed);
        }
    }
}

