namespace AniWorldReminder_TelegramBot.Models.AniWorld
{
    public class SeriesInfoModel
    {
        public string? Name { get; set; }
        public int SeasonCount { get; set; }
        public string? CoverArtUrl { get; set; }
        public List<SeasonModel> Seasons { get; set; } = new List<SeasonModel>();
    }
}
