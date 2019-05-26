using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Crawler
{
    public class DotaPickerProcessor
    {
        private readonly CrawlerClient _crawlerClient;
        private HtmlDocument _htmlDoc;

        private string CreateCounterPicksURL(List<string> initials)
        {
            const string prefix = "E_";
            StringBuilder sb = new StringBuilder();

            foreach (var item in initials)
            {
                sb.Append($"/{ prefix }{ item }");
            }

            return sb.ToString();
        }

        private HtmlNode GetCounterPicksNodeData(List<string> initials)
        {
            string counterPickerUrl = CreateCounterPicksURL(initials);
            string rawData = _crawlerClient.Download(counterPickerUrl);

            _htmlDoc.LoadHtml(rawData);

            return _htmlDoc.DocumentNode;
        }

        public DotaPickerProcessor(CrawlerClient crawlerClient)
        {
            _crawlerClient = crawlerClient;
            _htmlDoc = new HtmlDocument();
        }

        public IList<object> GetCounterPicks(List<string> initials)
        {
            const string resultSectionClassSelector = "heroSuggestionContainer";

            HtmlNode htmlNode = GetCounterPicksNodeData(initials);
            if (htmlNode.HasChildNodes)
            {
                HtmlNode resultsSection = htmlNode.ChildNodes.FirstOrDefault(it => it.HasClass(resultSectionClassSelector));
            }


            return new List<object>();
        }

    }
}
