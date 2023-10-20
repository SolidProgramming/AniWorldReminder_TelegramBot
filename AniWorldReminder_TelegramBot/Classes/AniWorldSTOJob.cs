using AniWorldReminder_TelegramBot.Enums;
using AniWorldReminder_TelegramBot.Misc;
using AniWorldReminder_TelegramBot.Models;
using AniWorldReminder_TelegramBot.Models.AniWorld;
using AniWorldReminder_TelegramBot.Models.DB;
using MethodTimer;
using Quartz;
using System.Reflection;
using System.Text;

namespace AniWorldReminder_TelegramBot.Classes
{
    [DisallowConcurrentExecution]
    public class AniWorldSTOJob : IJob
    {
        private readonly ILogger<AniWorldSTOJob> Logger;
        private readonly IStreamingPortalService AniWorldService;
        private readonly IStreamingPortalService STOService;
        private readonly IDBService DBService;
        private readonly ITelegramBotService TelegramBotService;

        public AniWorldSTOJob(ILogger<AniWorldSTOJob> logger,
            IStreamingPortalServiceFactory streamingPortalServiceFactory,
            IDBService dbService,
            ITelegramBotService telegramBotService)
        {
            Logger = logger;
            AniWorldService = streamingPortalServiceFactory.GetService(StreamingPortal.AniWorld);
            STOService = streamingPortalServiceFactory.GetService(StreamingPortal.STO);
            DBService = dbService;
            TelegramBotService = telegramBotService;
        }

        [Time]
        public async Task Execute(IJobExecutionContext context)
        {
            MethodBase? methodBase = typeof(AniWorldSTOJob).GetMethod("Execute");
            MethodTimeLogger.LogExecution(methodBase);

            await CheckForNewEpisodes();
        }

        private async Task CheckForNewEpisodes()
        {
            List<SeriesReminderModel>? userReminderSeries = await DBService.GetUsersReminderSeriesAsync();

            if (userReminderSeries is null)
                return;

            IEnumerable<IGrouping<int, SeriesReminderModel>>? userReminderSeriesGroups = userReminderSeries.GroupBy(_ => _.Id);

            foreach (IGrouping<int, SeriesReminderModel> group in userReminderSeriesGroups)
            {
                SeriesReminderModel series = group.First();

                (bool updateAvailable, SeriesInfoModel? seriesInfo) = await UpdateNeeded(series);

                if (!updateAvailable || seriesInfo is null)
                    continue;

                List<EpisodeModel>? newEpisodes = await GetNewEpisodes(series.Id, seriesInfo);

                if (newEpisodes is null || newEpisodes.Count == 0)
                    continue;

                await DBService.UpdateSeriesInfoAsync(series.Id, seriesInfo);

                await DBService.InsertEpisodesAsync(series.Id, newEpisodes);

                await SendNotifications(seriesInfo, group, newEpisodes);
            }
        }

        private async Task SendNotifications(SeriesInfoModel seriesInfo, IGrouping<int, SeriesReminderModel> seriesGroup, List<EpisodeModel> newEpisodes)
        {
            int maxCount = 5;

            string? seriesName = seriesGroup.First().Name;

            if (string.IsNullOrEmpty(seriesName))
                return;

            StringBuilder sb = new();

            sb.AppendLine($"{Emoji.Confetti} Neue Folge(n) für <b>{seriesName}</b> sind erschienen! {Emoji.Confetti}\n\n");
            sb.AppendLine($"{Emoji.Wave} Staffel: <b>{seriesInfo.SeasonCount}</b> Episode: <b>{seriesInfo.Seasons.Last().EpisodeCount}</b> {Emoji.Wave}\n");

            foreach (EpisodeModel newEpisde in newEpisodes.TakeLast(maxCount))
            {

                sb.AppendLine($"{Emoji.SmallBlackSquare} S<b>{newEpisde.Season:D2}</b> E<b>{newEpisde.Episode:D2}</b> {Emoji.HeavyMinus} {newEpisde.Name} [{newEpisde.Languages.ToLanguageText()}]");
            }

            if (newEpisodes.Count > maxCount)
            {
                sb.AppendLine($"{Emoji.SmallBlackSquare} <b>...</b>");
            }

            string messageText = sb.ToString();

            List<string?>? chatsToInform = seriesGroup
                .Select(_ => _.TelegramChatId)
                    .ToList();

            foreach (string? telegramChatId in chatsToInform)
            {
                Logger.LogInformation($"{DateTime.Now} | Sent 'New Episodes' notification to chat: {telegramChatId}");

                if (string.IsNullOrEmpty(seriesInfo.CoverArtUrl))
                {
                    await TelegramBotService.SendMessageAsync(Convert.ToInt64(telegramChatId), messageText);
                }
                else
                {
                    await TelegramBotService.SendPhotoAsync(Convert.ToInt64(telegramChatId), seriesInfo.CoverArtUrl, messageText);
                }
            }
        }

        private async Task<(bool updateAvailable, SeriesInfoModel? seriesInfo)> UpdateNeeded(SeriesModel series)
        {
            if (series is null || string.IsNullOrEmpty(series.Name) || string.IsNullOrEmpty(series.StreamingPortal))
                return (false, null);

            (int seasonCount, int episodeCount) = await DBService.GetSeriesSeasonEpisodeCountAsync(series.Id);

            IStreamingPortalService streamingPortalService;
            StreamingPortal streamingPortal = StreamingPortalHelper.GetStreamingPortalByName(series.StreamingPortal);

            switch (streamingPortal)
            {
                case StreamingPortal.Undefined:
                    return (false, null);
                case StreamingPortal.AniWorld:
                    streamingPortalService = AniWorldService;
                    break;
                case StreamingPortal.STO:
                    streamingPortalService = STOService;
                    break;
                default:
                    return (false, null);
            }

            SeriesInfoModel? seriesInfo = await streamingPortalService.GetSeriesInfoAsync(series.Name, streamingPortal);

            if (seriesInfo is null)
                return (false, null);

            if (seriesInfo.Seasons is null || seriesInfo.Seasons.Count == 0)
            {
                return (false, null);
            }

            if (seriesInfo.Seasons.Last().EpisodeCount > 0 && ( seriesInfo.SeasonCount > seasonCount || seriesInfo.Seasons.Last().EpisodeCount > episodeCount ))
            {
                return (true, seriesInfo);
            }

            return (false, null);
        }

        private async Task<List<EpisodeModel>?> GetNewEpisodes(int seriesId, SeriesInfoModel seriesInfo)
        {
            List<EpisodeModel>? dbEpisodes = await DBService.GetSeriesEpisodesAsync(seriesId);
            List<EpisodeModel>? newEpisodes = new();

            if (dbEpisodes is null)
            {
                return seriesInfo.Seasons.SelectMany(_ => _.Episodes)
                    .ToList();
            }

            foreach (SeasonModel season in seriesInfo.Seasons)
            {
                List<EpisodeModel>? tempNewEpisodes = season.Episodes.Where(seasonEp =>
                    !dbEpisodes.Any(dbEp => dbEp.Episode == seasonEp.Episode && dbEp.Season == seasonEp.Season))
                        .ToList();

                if (tempNewEpisodes is null)
                    continue;

                newEpisodes.AddRange(tempNewEpisodes);
            }

            return newEpisodes;
        }
    }
}
