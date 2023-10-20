using System.Text.Json.Serialization;


namespace AniWorldReminder_TelegramBot.Models.AniWorld
{
    public class SearchResultModel
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("link")]
        public string Link { get; set; }
    }
}
