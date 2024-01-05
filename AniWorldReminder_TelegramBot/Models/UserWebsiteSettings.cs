namespace AniWorldReminder_TelegramBot.Models
{
    public class UserWebsiteSettings
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int TelegramDisableNotifications { get; set; }
        public int TelegramNoCoverArtNotifications { get; set; }
    }
}
