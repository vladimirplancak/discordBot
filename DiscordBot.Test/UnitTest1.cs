using DiscordBot.OpenDotaApiConnector;
using NUnit.Framework;

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
    }
}