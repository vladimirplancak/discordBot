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
        private readonly DiscordSocketClient _client;
        private Dictionary<ulong, EventModel> Events = new Dictionary<ulong, EventModel>();

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



        public void AssigneEventWithId(SocketMessage message)
        {

        }

        public bool IsEventMessage(SocketMessage message)
        {
            throw new NotImplementedException();
        }
    }

    public class EventModel
    {
        public EventModel(SocketMessage message)
        {
            RandomIdentifier = Guid.NewGuid();
        }

        public ulong Id { get; set; }
        public string Name { get; set; }
        public List<int> Going { get; set; }
        public List<int> Maybe { get; set; }
        public List<int> NotGoing { get; set; }
        public Guid RandomIdentifier { get; set; }
    }
}
