using DiscordBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Extensions
{
    public static class SongInQueueExtensions
    {
        [Obsolete]
        public static List<SongInQueue> AddToTheEnd(this List<SongInQueue> currentList, SongInQueue song)
        {
            currentList.Add(song);
            return currentList;
        }
    }
}
