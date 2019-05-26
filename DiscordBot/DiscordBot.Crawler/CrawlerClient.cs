using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Crawler
{
    public class CrawlerClient
    {
        private readonly string _baseUrl;
        private WebClient _webClient;

        public CrawlerClient(string baseUrl)
        {
            _baseUrl = baseUrl;
            _webClient = new WebClient();
        }

        public string Download(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                throw new ArgumentNullException(nameof(uri));
            }

            _webClient.BaseAddress = _baseUrl;

            return _webClient.DownloadString(uri);
        }
    }
}
