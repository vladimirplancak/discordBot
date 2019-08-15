using DiscordBot.OpenDotaApiConnector;
using NUnit.Framework;
using System.IO;
using System.Linq;
using Xabe.FFmpeg;

namespace Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            var playerManager = new PlayerManager();

            var result = playerManager.GetAccountAsync(113864991).Result;
        }

        [Test]
        public void Test2()
        {
            //FFmpeg.ExecutablesPath = Path.Combine(@"D:\Projects\DiscordBotNewest\DiscordBot.Test\bin\Debug\netcoreapp2.2\");
            //FFmpeg.GetLatestVersion().Wait();
            //string filePath = Path.Combine(@"D:\youtubeMusicPlayList\Black Coffee - Salle Wagram for Cercle.mp3");
            //IMediaInfo mediaInfo = MediaInfo.Get(filePath).Result;
            //mediaInfo.Streams.ToList()[0].
        }
    }
}