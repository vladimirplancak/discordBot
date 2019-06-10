using Discord;
using Discord.Commands;
using Discord.WebSocket;
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
        private DiscordSocketClient _client;

        public EventCommands(EventService eventService, DiscordSocketClient client)
        {
            _eventService = eventService;
            _client = client;
        }


        private Embed CreateResponseMesasge(EventCreationModel creationModel)
        {
            DateTimeOffset eventTimeOffset = new DateTimeOffset(creationModel.Time);

            EmbedBuilder builder = new EmbedBuilder();

            builder.WithTitle(creationModel.Title);
            builder.WithDescription(creationModel.Description);
            builder.WithTimestamp(eventTimeOffset);

            return builder.Build();
        }

        private Embed CreateResponseMessage(EventModel eventModel)
        {
            return CreateResponseMesasge(new EventCreationModel(eventModel));
        }


        [Command("createEvent", RunMode = RunMode.Async), Summary("Create event in following format: Name|Description|Date(mm-dd-yyyy)")]
        public async Task CreateEvent([Summary("Cr.")] string eventCommand = null)
        {

            if (eventCommand == null || !eventCommand.Contains('|'))
            {
                await ReplyAsync("Event format should be: Name|Description|Date");
                return;
            }

            EventCreationModel eventCreationModel = EventService.ParseEventData(eventCommand, out List<string> errors);

            Embed eventEmbed = CreateResponseMesasge(eventCreationModel);

            if (errors.Count > 0)
            {
                //TODO: Send errors message.
                await ReplyAsync("Bad request.");
                return;
            }

            SocketGuild guild = _client.GetGuild(Context.Guild.Id);
            SocketTextChannel channel = guild.GetTextChannel(_eventService.EventsChannelId);

            //using to extract message Id, so later on we can update message if necessary.
            IUserMessage eventMessage = await channel.SendMessageAsync("", embed: eventEmbed);
            _eventService.AddEvent(eventCreationModel, eventMessage, Context.User);
        }

        [Command("getEvents", RunMode = RunMode.Async), Summary("Create event in following format: Name|Description|Date(mm-dd-yyyy)")]
        public async Task GetEvents([Summary("Cr.")] string eventCommand = null)
        {
            _eventService.Events.ToList().ForEach(async it => {
                await ReplyAsync("", embed: CreateResponseMessage(it.Value));
            });
        }
    }
}


