using AniWorldModels = AniWorldReminder_TelegramBot.Models.AniWorld;
using DBModels = AniWorldReminder_TelegramBot.Models.DB;

namespace AniWorldReminder_TelegramBot.Interfaces
{
    public interface IDBService
    {
        Task<bool> Init();
        Task<DBModels.UsersModel?> GetUserAsync(string telegramChatId);
        Task<DBModels.UsersModel> InsertUserAsync(string telegramChatId);
        Task<DBModels.SeriesModel> GetSeriesAsync(string seriesName);
        Task<int> InsertSeriesAsync(AniWorldModels.SeriesInfoModel series, StreamingPortal streamingPortal);
        Task<DBModels.UsersSeriesModel?> GetUsersSeriesAsync(string telegramChatId, string seriesName);
        Task<List<DBModels.UsersSeriesModel>?> GetUsersSeriesAsync(string telegramChatId);
        Task<List<DBModels.UsersSeriesModel>?> GetUsersSeriesAsync();
        Task<List<DBModels.SeriesReminderModel>?> GetUsersReminderSeriesAsync();
        Task<(int seasonCount, int episodeCount)> GetSeriesSeasonEpisodeCountAsync(int seriesId);
        Task InsertUsersSeriesAsync(DBModels.UsersSeriesModel usersSeries);
        Task DeleteUsersSeriesAsync(DBModels.UsersSeriesModel usersSeries);
        Task UpdateSeriesInfoAsync(int seriesId, AniWorldModels.SeriesInfoModel seriesInfo);
        Task<List<EpisodeModel>?> GetSeriesSeasonEpisodesAsync(int seriesId, int season);
        Task InsertEpisodesAsync(int seriesId, List<EpisodeModel> episodes);
        Task<List<EpisodeModel>?> GetSeriesEpisodesAsync(int seriesId);
        Task<UserState> GetUserStateAsync(string telegramChatId);
        Task UpdateUserStateAsync(string telegramChatId, UserState userState);
        Task UpdateVerifyTokenAsync(string telegramChatId, string token);
        Task InsertDownloadAsync(int seriesId, List<EpisodeModel> episodes);
        Task UpdateEpisodesAsync(int seriesId, List<EpisodeModel> episodes);
    }
}
