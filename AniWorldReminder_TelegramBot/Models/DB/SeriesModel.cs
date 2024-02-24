namespace AniWorldReminder_TelegramBot.Models.DB
{
    public class SeriesModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? CoverArtUrl { get; set; }
        public string? CoverArtBase64 { get; set; }
        public string? Path { get; set; }
        public StreamingPortalModel? StreamingPortal { get; set; }
    }
}
