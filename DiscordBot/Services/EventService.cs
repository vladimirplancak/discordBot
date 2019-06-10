using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class EventService
    {
        public readonly DiscordSocketClient _client;
        public Dictionary<ulong, EventModel> Events = new Dictionary<ulong, EventModel>();
        public ulong EventsChannelId = 438588991219040266;

        public EventService(DiscordSocketClient client)
        {
            _client = client;

            _client.Ready += () =>
            {
                _client.ReactionAdded += Client_ReactionAdded;
                return Task.CompletedTask;
            };
        }

        private Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> cacheable, ISocketMessageChannel socketMessage, SocketReaction socketReaction)
        {
            throw new NotImplementedException();
        }

        //Name|Description|time 
        public static EventCreationModel ParseEventData(string eventCommandText, out List<string> errors)
        {
            errors = new List<string>();
            //List<string> errors = new List<string>();
            string[] parts = eventCommandText.Split('|');
            string eventName = parts[0];
            string eventDescription = parts[1];
            string[] eventTimeParts = parts[2].Split('-');

            DateTime eventTime;

            if (int.TryParse(eventTimeParts[0], out int month) && int.TryParse(eventTimeParts[1], out int day) && int.TryParse(eventTimeParts[2], out int year))
            {
                eventTime = new DateTime(year, month, day);
            }
            else
            {
                errors.Add("Failed to pars date, please provide date in the format of mm--dd--yyyy");
                return null;
            }

            return new EventCreationModel()
            {
                Title = eventName,
                Description = eventDescription,
                Time = eventTime
            };
        }

        public async Task<IGroupChannel> GetEventChannelAsync()
        {
            return await _client.GetGroupChannelAsync(EventsChannelId);
        }



        public void AssigneEventWithId(SocketMessage message)
        {

        }

        public bool IsEventMessage(SocketMessage message)
        {
            throw new NotImplementedException();
        }

        public bool AddEvent(EventCreationModel eventCreation, IUserMessage userMessage, IUser user)
        {
            EventModel eventModel = new EventModel(eventCreation, userMessage, user);
            Events.Add(userMessage.Id, eventModel);

            return true;
        }
    }

    public class EventModel
    {
        public EventModel(EventCreationModel eventCreation, IUserMessage userMessage, IUser user)
        {
            Id = userMessage.Id;
            Title = eventCreation.Title;
            Description = eventCreation.Description;
            Time = eventCreation.Time;
            CreatedByUserId = user.Id;

            RandomIdentifier = Guid.NewGuid();
        }

        public ulong Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Time { get; set; }
        public ulong CreatedByUserId { get; set; }

        public List<int> Going { get; set; } = new List<int>();
        public List<int> Maybe { get; set; } = new List<int>();
        public List<int> NotGoing { get; set; } = new List<int>();

        public Guid RandomIdentifier { get; set; }
    }

    public class EventCreationModel
    {
        public EventCreationModel() { }

        public EventCreationModel(EventModel eventModel)
        {
            Title = eventModel.Title;
            Description = eventModel.Description;
            Time = eventModel.Time;
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Time { get; set; }
    }
}
