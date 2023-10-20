using System.Text.Json.Serialization;

namespace AniWorldReminder_TelegramBot.Models
{
    public class SettingsModel
    {
        [JsonPropertyName("TelegramBot")]
        public TelegramBotSettingsModel TelegramBotSettings { get; set; } = default!;

        [JsonPropertyName("Database")]
        public DatabaseSettingsModel DatabaseSettings { get; set; } = default!;

        [JsonPropertyName("Proxy")]
        public ProxyAccountModel ProxySettings { get; set; } = default!;
    }
}
