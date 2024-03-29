﻿using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Models
{
    public class SongInQueue
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public bool IsPlaying { get; set; }
        public IUser QueueBy;
        public bool IsPlayList { get; set; } = false;

        public override string ToString()
        {
            return $@"song name: { Name }, path: { FilePath }";
        }
    }
}
