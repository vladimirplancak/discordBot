using DiscordBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Storage
{
    interface IMusicStorage
    {
        ICollection<SongInQueue> GetAll();
        bool Add(SongInQueue song);
        bool Delete(SongInQueue song);
    }
}
