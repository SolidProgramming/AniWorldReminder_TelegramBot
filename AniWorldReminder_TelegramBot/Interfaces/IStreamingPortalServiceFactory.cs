namespace AniWorldReminder_TelegramBot.Interfaces
{
    public interface IStreamingPortalServiceFactory
    {
        void AddService(StreamingPortal streamingPortal, IServiceProvider sp);
        IStreamingPortalService GetService(StreamingPortal streamingPortal);
    }
}
