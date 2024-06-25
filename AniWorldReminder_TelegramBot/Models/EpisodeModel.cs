using AniWorldReminder_TelegramBot.Enums;

namespace AniWorldReminder_TelegramBot.Models
{
    public class EpisodeModel
    {
        public int Id { get; set; }
        public int SeriesId { get; set; }
        public int Season { get; set; }
        public int Episode { get; set; }
        public string? Name { get; set; }
        public Language LanguageFlag { get; set; }
        public IEnumerable<Language> UpdatedLanguageFlags { get; set; }
    }
}
