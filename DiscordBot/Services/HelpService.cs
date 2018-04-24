using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class HelpService
    {
        private DiscordSocketClient _client;

        public HelpService(DiscordSocketClient client)
        {
            _client = client;
        }
    }
}
