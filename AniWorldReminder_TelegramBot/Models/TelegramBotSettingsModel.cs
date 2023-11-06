using System.Text.Json.Serialization;

namespace AniWorldReminder_TelegramBot.Models
{
    public class TelegramBotSettingsModel
    {
        [JsonPropertyName("Token")]
        public string Token { get; set; } = default!;

        [JsonPropertyName("AdminChat")]
        public string AdminChat { get; set; } = default!;
    }
}
