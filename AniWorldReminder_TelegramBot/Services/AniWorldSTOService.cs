using AniWorldReminder_TelegramBot.Models.AniWorld;
using System.Text;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Net;
using AniWorldReminder_TelegramBot.Enums;
using Org.BouncyCastle.Tls;

namespace AniWorldReminder_TelegramBot.Services
{
    public class AniWorldSTOService(ILogger<AniWorldSTOService> logger, Interfaces.IHttpClientFactory httpClientFactory, string baseUrl, string name) : IStreamingPortalService
    {
        private HttpClient? HttpClient;

        public string BaseUrl { get; init; } = baseUrl;
        public string Name { get; init; } = name;

        public async Task<bool> Init(WebProxy? proxy = null)
        {
            if (proxy is null)
            {
                HttpClient = httpClientFactory.CreateHttpClient<AniWorldSTOService>();
            }
            else
            {
                HttpClient = httpClientFactory.CreateHttpClient<AniWorldSTOService>(proxy);
            }

            (bool success, string? ipv4) = await HttpClient.GetIPv4();

            if (!success)
            {
                logger.LogError($"{DateTime.Now} | {Name} Service unable to retrieve WAN IP");
            }
            else
            {
                logger.LogInformation($"{DateTime.Now} | {Name} Service initialized with WAN IP {ipv4}");
            }

            return success;
        }

        public async Task<(bool success, List<SearchResultModel>? searchResults)> GetSeriesAsync(string seriesName, bool strictSearch = false)
        {
            (bool reachable, string? html) = await StreamingPortalHelper.GetHosterReachableAsync(this);

            if (!reachable)
                return (false, null);

            if (seriesName.Contains('\''))
            {
                seriesName = seriesName.Split('\'')[0];
            }

            using StringContent postData = new($"keyword={seriesName.SearchSanitize()}", Encoding.UTF8, "application/x-www-form-urlencoded");

            HttpResponseMessage? resp = await HttpClient.PostAsync(new Uri($"{BaseUrl}/ajax/search"), postData);

            if (!resp.IsSuccessStatusCode)
                return (false, null);

            string content = await resp.Content.ReadAsStringAsync();

            try
            {
                List<SearchResultModel>? searchResults = System.Text.Json.JsonSerializer.Deserialize<List<SearchResultModel>>(content.StripHtmlTags());

                if (searchResults is null)
                    return (false, null);

                if (!searchResults.Any(_ => _.Link.Contains("/stream")))
                    return (false, null);

                List<SearchResultModel>? filteredSearchResults = searchResults.Where(_ =>
                    _.Link.Contains("/stream") &&
                    _.Title.ToLower().Contains(seriesName.ToLower()) &&
                    !_.Link.Contains("staffel") &&
                    !_.Link.Contains("episode"))
                        .ToList();

                if (strictSearch)
                {
                    filteredSearchResults = filteredSearchResults.Where(_ => _.Title == seriesName).ToList();
                }

                if (filteredSearchResults.Count == 0)
                    return (false, null);

                return (true, filteredSearchResults);
            }
            catch (Exception)
            {
                return (false, null);
            }
        }

        public async Task<SeriesInfoModel?> GetSeriesInfoAsync(string seriesPath, StreamingPortal streamingPortal)
        {
            string seriesUrl;

            switch (streamingPortal)
            {
                case StreamingPortal.STO:
                    seriesUrl = $"{BaseUrl}/serie/stream/{seriesPath}";
                    break;
                case StreamingPortal.AniWorld:
                    seriesUrl = $"{BaseUrl}/anime/stream/{seriesPath}";
                    break;
                default:
                    return null;
            }

            HttpResponseMessage? resp = await HttpClient.GetAsync(new Uri(seriesUrl));

            if (!resp.IsSuccessStatusCode)
                return null;

            string content = await resp.Content.ReadAsStringAsync();

            HtmlDocument doc = new();
            doc.LoadHtml(content);

            List<HtmlNode>? seriesInfoNode = new HtmlNodeQueryBuilder()
                .Query(doc)
                    .GetNodesByQuery("//div[@class='hosterSiteDirectNav']/ul/li[last()]");

            if (seriesInfoNode is null || seriesInfoNode.Count == 0)
                return null;

            if (!int.TryParse(seriesInfoNode[0].InnerText, out int seasonCount))
                return null;

            HtmlNode? titleNode = new HtmlNodeQueryBuilder()
                .Query(doc)
                    .GetNodesByQuery("//div[@class='series-title']/h1/span")
                        .FirstOrDefault();

            SeriesInfoModel seriesInfo = new()
            {
                Name = titleNode?.InnerHtml,
                SeasonCount = seasonCount,
                CoverArtUrl = GetCoverArtUrl(doc),
                Seasons = await GetSeasonsAsync(seriesPath, seasonCount, streamingPortal),
                Path = seriesPath
            };

            foreach (SeasonModel season in seriesInfo.Seasons)
            {
                List<EpisodeModel>? episodes = await GetSeasonEpisodesAsync(seriesPath, season.Id, streamingPortal);

                if (episodes is null || episodes.Count == 0)
                    continue;

                season.Episodes = episodes;
            }

            return seriesInfo;
        }

        private async Task<List<SeasonModel>> GetSeasonsAsync(string seriesPath, int seasonCount, StreamingPortal streamingPortal)
        {
            List<SeasonModel> seasons = [];

            for (int i = 0; i < seasonCount; i++)
            {
                string seasonUrl;

                switch (streamingPortal)
                {
                    case StreamingPortal.STO:
                        seasonUrl = $"{BaseUrl}/serie/stream/{seriesPath}/staffel-{i + 1}";
                        break;
                    case StreamingPortal.AniWorld:
                        seasonUrl = $"{BaseUrl}/anime/stream/{seriesPath}/staffel-{i + 1}";
                        break;
                    default:
                        return null;
                }

                HttpResponseMessage? resp = await HttpClient.GetAsync(new Uri(seasonUrl));

                if (!resp.IsSuccessStatusCode)
                    continue;

                string html = await resp.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(html))
                    continue;

                HtmlDocument doc = new();
                doc.LoadHtml(html);

                List<HtmlNode>? episodeNodes = new HtmlNodeQueryBuilder()
                    .Query(doc)
                        .GetNodesByQuery($"//div[@class='hosterSiteDirectNav']/ul/li/a[@data-season-id=\"{i + 1}\"]");

                SeasonModel season = new()
                {
                    Id = i + 1,
                    EpisodeCount = episodeNodes.Count,
                };

                if (episodeNodes is null || episodeNodes.Count == 0)
                {
                    season.EpisodeCount = 0;
                    continue;
                }

                seasons.Add(season);
            }

            return seasons;
        }

        private async Task<List<EpisodeModel>?> GetSeasonEpisodesAsync(string seriesPath, int season, StreamingPortal streamingPortal)
        {
            string seasonUrl;

            switch (streamingPortal)
            {
                case StreamingPortal.STO:
                    seasonUrl = $"{BaseUrl}/serie/stream/{seriesPath}/staffel-{season}";
                    break;
                case StreamingPortal.AniWorld:
                    seasonUrl = $"{BaseUrl}/anime/stream/{seriesPath}/staffel-{season}";
                    break;
                default:
                    return null;
            }

            HttpResponseMessage? resp = await HttpClient.GetAsync(new Uri(seasonUrl));

            if (!resp.IsSuccessStatusCode)
                return null;

            string html = await resp.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(html))
                return null;

            HtmlDocument doc = new();
            doc.LoadHtml(html);

            List<HtmlNode>? episodeNodes = new HtmlNodeQueryBuilder()
                .Query(doc)
                    .GetNodesByQuery("//tbody/tr/td[@class=\"seasonEpisodeTitle\"]/a");

            if (episodeNodes is null || episodeNodes.Count == 0)
                return null;

            List<EpisodeModel> episodes = [];

            int i = 1;

            foreach (HtmlNode episodeNode in episodeNodes)
            {
                string episodeName = new Regex("<(strong|span)>(?'Name'.*?)</(strong|span)>")
                    .Matches(episodeNode.InnerHtml)
                        .First(_ => !string.IsNullOrEmpty(_.Groups["Name"].Value))
                            .Groups["Name"]
                                .Value;

                if (string.IsNullOrEmpty(episodeName))
                    continue;

                episodes.Add(new EpisodeModel()
                {
                    Name = episodeName,
                    Episode = i,
                    Season = season,
                    LanguageFlag = GetEpisodeLanguages(i, html)
                });

                i++;
            }

            return episodes;
        }

        private static Language GetEpisodeLanguages(int episode, string html)
        {
            HtmlDocument doc = new();
            doc.LoadHtml(html);

            List<HtmlNode>? languages = new HtmlNodeQueryBuilder()
                .Query(doc)
                    .GetNodesByQuery($"//tr[@data-episode-season-id=\"{episode}\"]/td/a/img");

            Language availableLanguages = Language.None;

            foreach (HtmlNode node in languages)
            {
                string language = node.Attributes["title"].Value;

                if (string.IsNullOrEmpty(language))
                    continue;

                switch (language)
                {
                    case "Deutsch/German":
                        availableLanguages |= Language.GerDub;
                        break;
                    case "Englisch":
                        availableLanguages |= Language.EngDub;
                        break;
                    case "Mit deutschem Untertitel":
                        availableLanguages |= Language.GerSub;
                        break;
                    default:
                        break;
                }
            }

            return availableLanguages;
        }

        private string? GetCoverArtUrl(HtmlDocument document)
        {
            HtmlNode? node = new HtmlNodeQueryBuilder()
                 .Query(document)
                     .GetNodesByQuery("//div[@class='seriesCoverBox']/img")
                        .FirstOrDefault();

            if (node is null)
                return null;

            return BaseUrl + node.Attributes["data-src"].Value;
        }

        public HttpClient GetHttpClient()
        {
            return HttpClient;
        }
    }
}
