using Newtonsoft.Json;

namespace AniWorldReminder_TelegramBot.Models
{
    public class CoverImage
    {
        [JsonProperty("large")]
        public string Large { get; set; }

        [JsonProperty("medium")]
        public string Medium { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }
    }

    public class Data
    {
        [JsonProperty("Page")]
        public Page Page { get; set; }
    }

    public class EndDate
    {
        [JsonProperty("year")]
        public int? Year { get; set; }

        [JsonProperty("month")]
        public int? Month { get; set; }

        [JsonProperty("day")]
        public int? Day { get; set; }
    }

    public class Medium
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("title")]
        public Title Title { get; set; }

        [JsonProperty("coverImage")]
        public CoverImage CoverImage { get; set; }

        [JsonProperty("startDate")]
        public StartDate StartDate { get; set; }

        [JsonProperty("endDate")]
        public EndDate EndDate { get; set; }

        [JsonProperty("season")]
        public string Season { get; set; }

        [JsonProperty("seasonYear")]
        public int? SeasonYear { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("episodes")]
        public int? Episodes { get; set; }

        [JsonProperty("duration")]
        public int? Duration { get; set; }

        [JsonProperty("genres")]
        public List<string> Genres { get; set; }

        [JsonProperty("isAdult")]
        public bool? IsAdult { get; set; }

        [JsonProperty("averageScore")]
        public int? AverageScore { get; set; }

        [JsonProperty("nextAiringEpisode")]
        public object NextAiringEpisode { get; set; }
    }

    public class Page
    {
        [JsonProperty("pageInfo")]
        public PageInfo PageInfo { get; set; }

        [JsonProperty("media")]
        public List<Medium> Media { get; set; }
    }

    public class PageInfo
    {
        [JsonProperty("total")]
        public int? Total { get; set; }

        [JsonProperty("perPage")]
        public int? PerPage { get; set; }

        [JsonProperty("currentPage")]
        public int? CurrentPage { get; set; }

        [JsonProperty("lastPage")]
        public int? LastPage { get; set; }

        [JsonProperty("hasNextPage")]
        public bool? HasNextPage { get; set; }
    }

    public class AniListSearchMediaModel
    {
        [JsonProperty("data")]
        public Data Data { get; set; }
    }

    public class StartDate
    {
        [JsonProperty("year")]
        public int? Year { get; set; }

        [JsonProperty("month")]
        public int? Month { get; set; }

        [JsonProperty("day")]
        public int? Day { get; set; }
    }

    public class Title
    {
        [JsonProperty("english")]
        public string English { get; set; }

        [JsonProperty("userPreferred")]
        public string UserPreferred { get; set; }
    }
}
