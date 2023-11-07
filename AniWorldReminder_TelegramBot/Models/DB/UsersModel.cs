namespace AniWorldReminder_TelegramBot.Models.DB
{
    public class UsersModel
    {
        public int Id { get; set; }
        public string? TelegramChatId { get; set; }
        public UserState StateId { get; set; }
        public string? Username { get; set; }
        public VerificationStatus Verified { get; set; }
    }
}
