using DiscordBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.YoutubeDownlaoder
{
    public interface IYoutubeDownloaderClient
    {
        SongInQueue DownloadSong(string link);
    }
}
