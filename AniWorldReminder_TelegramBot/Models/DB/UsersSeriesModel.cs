namespace AniWorldReminder_TelegramBot.Models.DB
{
    public class UsersSeriesModel
    {
        public int Id { get; set; }
        public UsersModel? Users { get; set; }
        public SeriesModel? Series { get; set; }
        public Language LanguageFlag { get; set; }
    }
}
