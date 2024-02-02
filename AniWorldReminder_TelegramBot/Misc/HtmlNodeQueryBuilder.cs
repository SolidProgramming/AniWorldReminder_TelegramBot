using HtmlAgilityPack;
using System.Text;

namespace AniWorldReminder_TelegramBot.Misc
{
    public class HtmlNodeQueryBuilder
    {
        private List<HtmlNode> _nodes = [];

        /// <summary>
        ///     Return all resulting nodes from the list
        /// </summary>
        public List<HtmlNode> Results => _nodes;

        /// <summary>
        ///     Returns only the first node result from the list
        /// </summary>
        public HtmlNode Result
        {
            get
            {
                if (_nodes.Count > 0)
                {
                    return _nodes[0];
                }

                return null;
            }
        }

        public HtmlNodeQueryBuilder Query(HtmlDocument doc)
        {
            _nodes.Add(doc.DocumentNode);

            return this;
        }
        public HtmlNodeQueryBuilder Query(HtmlNode node)
        {
            _nodes.Add(node);

            return this;
        }
        public HtmlNodeQueryBuilder Query(List<HtmlNode> nodes)
        {
            _nodes.AddRange(nodes);

            return this;
        }

        public HtmlNodeQueryBuilder ById(string id)
        {
            string query = $".//*[@id='{id}']";

            _nodes = GetNodesByQuery(query);

            return this;
        }
        public HtmlNodeQueryBuilder ByElement(string elementName)
        {
            string query = ".//" + elementName;

            _nodes = GetNodesByQuery(query);

            return this;
        }
        public HtmlNodeQueryBuilder ByClass(params string[] classNames)
        {
            StringBuilder _builder = new();

            foreach (string className in classNames)
            {
                _builder.Append(className);
                _builder.Append(' ');
            }

            _builder.Remove(_builder.Length - 1, 1);

            string query = $".//*[@class='{_builder}']";

            _nodes = GetNodesByQuery(query);

            return this;
        }
        public HtmlNodeQueryBuilder ByAttribute(params string[] attributeNames)
        {
            StringBuilder _builder = new();

            foreach (string className in attributeNames)
            {
                _builder.Append(className);
                _builder.Append(' ');
            }

            _builder.Remove(_builder.Length - 1, 1);

            string query = $".//*[@{_builder}]";

            _nodes = GetNodesByQuery(query);

            return this;
        }
        public HtmlNodeQueryBuilder ByAttributeValue(string attributeName, string attributeValue)
        {
            string query = $".//*[@{attributeName}='{attributeValue}']";

            _nodes = GetNodesByQuery(query);

            return this;
        }
        public HtmlNodeQueryBuilder ByAttributeValues(string attributeName, List<string> attributeValues)
        {
            List<HtmlNode> filteredNodes = [];
            foreach (string attValue in attributeValues)
            {
                string query = $".//*[@{attributeName}='{attValue}']";

                filteredNodes.AddRange(GetNodesByQuery(query));
            }

            _nodes = filteredNodes;

            return this;
        }
        public List<HtmlNode> GetNodesByQuery(string query)
        {
            List<HtmlNode> nodes = [];

            for (int i = 0; i < _nodes?.Count; i++)
            {
                List<HtmlNode> newNodes = _nodes[i].SelectNodes(query)?.ToList();

                if (newNodes is null) continue;

                nodes.AddRange(newNodes);
            }

            return nodes;
        }
    }
}
