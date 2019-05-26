using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Crawler
{
    class Program
    {
        static void Main(string[] args)
        {
            DotaPickerProcessor processor = new DotaPickerProcessor(new CrawlerClient("http://dotapicker.com/counterpick#!"));
            processor.GetCounterPicks(new List<string>() { "Abaddon" });
        }
    }
}
