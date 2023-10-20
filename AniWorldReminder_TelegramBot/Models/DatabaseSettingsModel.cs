using System.Text.Json.Serialization;

namespace AniWorldReminder_TelegramBot.Models
{
    public class DatabaseSettingsModel
    {
        [JsonPropertyName("IP")]
        public string Ip { get; set; } = default!;

        [JsonPropertyName("Database")]
        public string Database { get; set; } = default!;

        [JsonPropertyName("Username")]
        public string Username { get; set; } = default!;

        [JsonPropertyName("Password")]
        public string Password { get; set; } = default!;
    }
}
