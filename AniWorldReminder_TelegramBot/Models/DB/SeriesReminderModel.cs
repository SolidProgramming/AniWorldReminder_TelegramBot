namespace AniWorldReminder_TelegramBot.Models.DB
{
    public class SeriesReminderModel
    {
        public SeriesModel? Series { get; set; }
        public UsersModel? User { get; set; }
        public Language Language { get; set; }
    }
}
