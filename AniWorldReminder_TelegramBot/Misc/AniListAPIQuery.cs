namespace AniWorldReminder_TelegramBot.Misc
{
    public static class AniListAPIQuery
    {
        public const string Uri = "https://graphql.anilist.co";

        private static readonly Dictionary<AniListAPIQueryType, string> QueryTypes = new()
        {
            { AniListAPIQueryType.SearchMedia, SearchMediaQuery }
        };

        private const string SearchMediaQuery = @"{
                ""query"": ""query($page:Int = 1 $id:Int $type:MediaType $search:String){Page(page:$page,perPage:20){pageInfo{total perPage currentPage lastPage hasNextPage}media(id:$id type:$type search:$search){id title{english,userPreferred}coverImage{large, medium color}startDate{year month day}endDate{year month day}season seasonYear description type format status(version:2)episodes duration genres isAdult averageScore nextAiringEpisode{airingAt timeUntilAiring episode}}}}"",
                ""variables"": {
                    ""page"": 1,
                    ""type"": ""ANIME"",
                    ""sort"": ""SEARCH_MATCH"",
                    ""search"": ""{searchString}""
                    }
                }";

        public static string? GetQuery(AniListAPIQueryType queryType, string queryData)
        {
            return QueryTypes.SingleOrDefault(_ => _.Key == queryType).Value.Replace("{searchString}", queryData);
        }
    }
}
