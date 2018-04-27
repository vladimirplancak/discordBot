using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Models
{
    public class QueueItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FilePath { get; set; }
        public bool IsPlaying { get; set; }
    }
}
