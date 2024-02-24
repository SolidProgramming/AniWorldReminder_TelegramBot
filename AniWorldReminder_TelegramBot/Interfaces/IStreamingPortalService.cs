using AniWorldReminder_TelegramBot.Models.AniWorld;
using System.Net;

namespace AniWorldReminder_TelegramBot.Interfaces
{
    public interface IStreamingPortalService
    {
        string BaseUrl { get; init; }
        string Name { get; init; }
        Task<bool> Init(WebProxy? proxy = null);
        Task<(bool success, List<SearchResultModel>? searchResults)> GetSeriesAsync(string seriesName, bool strictSearch = false);
        Task<SeriesInfoModel?> GetSeriesInfoAsync(string seriesPath, StreamingPortal streamingPortal);
        HttpClient GetHttpClient();
    }
}
