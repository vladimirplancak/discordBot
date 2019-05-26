using DiscordBot.OpenDotaApiConnector.Player;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.OpenDotaApiConnector
{
    public class PlayerManager
    {
        public readonly HttpClient httpClient;
        

        public PlayerManager()
        {
            httpClient = new HttpClient();
        }

        private async Task<string> GetAsync(string url)
        {
            using (httpClient)
            {
                HttpResponseMessage response = await httpClient.GetAsync(url);
                return await response.Content.ReadAsStringAsync();
            }
        }

        public async Task<string> GetAccountAsync(long accountId)
        {
            string url = $"https://api.opendota.com/api/players/{ accountId }";

            return await GetAsync(url);
        }
    }
}
