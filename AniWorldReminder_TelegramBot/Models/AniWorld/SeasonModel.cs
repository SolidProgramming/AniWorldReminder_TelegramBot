namespace AniWorldReminder_TelegramBot.Models.AniWorld
{
    public class SeasonModel
    {
        public int Id { get; set; }
        public int EpisodeCount { get; set; }
        public List<EpisodeModel> Episodes { get; set; } = [];
    }
}
