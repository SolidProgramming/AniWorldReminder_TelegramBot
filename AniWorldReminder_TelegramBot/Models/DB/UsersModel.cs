namespace AniWorldReminder_TelegramBot.Models.DB
{
    public class UsersModel
    {
        public int Id { get; set; }

        public string? TelegramChatId { get; set; }
        public VerificationStatus Verified { get; set; }
    }
}
