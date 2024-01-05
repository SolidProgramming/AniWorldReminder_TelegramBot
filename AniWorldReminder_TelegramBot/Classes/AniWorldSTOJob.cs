using AniWorldReminder_TelegramBot.Enums;
using AniWorldReminder_TelegramBot.Misc;
using AniWorldReminder_TelegramBot.Models;
using AniWorldReminder_TelegramBot.Models.AniWorld;
using AniWorldReminder_TelegramBot.Models.DB;
using Quartz;
using System.Reflection;
using System.Text;
using System.Linq;

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
            MethodBase? methodBase = typeof(AniWorldSTOJob).GetMethod(nameof(Execute));
            MethodTimeLogger.LogExecution(methodBase);

            await CheckForNewEpisodes();
        }

        private async Task CheckForNewEpisodes()
        {
            List<EpisodeModel>? matchingEpisodes = new();

            List<SeriesReminderModel>? userReminderSeries = await DBService.GetUsersReminderSeriesAsync();

            if (userReminderSeries is null)
                return;

            TelegramBotSettingsModel? telegramBotSettings = SettingsHelper.ReadSettings<TelegramBotSettingsModel>();

            IEnumerable<IGrouping<int, SeriesReminderModel>>? userReminderSeriesGroups = userReminderSeries.GroupBy(_ => _.Series!.Id);

            foreach (IGrouping<int, SeriesReminderModel> group in userReminderSeriesGroups)
            {
                SeriesReminderModel seriesReminder = group.First();

                if (seriesReminder.Series is null)
                    continue;

                Logger.LogInformation($"{DateTime.Now} | Scanning for changes: {seriesReminder.Series.Name}");

                (bool updateAvailable, SeriesInfoModel? seriesInfo, List<EpisodeModel>? languageUpdateEpisodes, List<EpisodeModel>? newEpisodes, List<EpisodeModel>? namesUpdatedEpisodes) = await UpdateNeeded(seriesReminder);

                if (!updateAvailable || seriesInfo is null)
                    continue;

                matchingEpisodes.Clear();

                if (namesUpdatedEpisodes.HasItems())
                {
                    await DBService.UpdateEpisodesAsync(seriesReminder.Series.Id, namesUpdatedEpisodes);

                    string messageText = $"Es wurden folgende Episoden für <b>{seriesReminder.Series.Name}</b> mit <b>Namens-Updates</b> gefunden und geupdated!";
                    await SendAdminNotification(namesUpdatedEpisodes, messageText);
                }

                if (languageUpdateEpisodes.HasItems())
                {
                    await DBService.UpdateEpisodesAsync(seriesReminder.Series.Id, languageUpdateEpisodes);

                    if (languageUpdateEpisodes.Any(_ => _.LanguageFlag.HasFlag(seriesReminder.Language)))
                    {
                        matchingEpisodes.AddRange(languageUpdateEpisodes.Where(_ => _.LanguageFlag.HasFlag(seriesReminder.Language)));
                    }

                    string messageText = $"Es wurden folgende Episoden für <b>{seriesReminder.Series.Name}</b> mit <b>Sprach-Updates</b> gefunden und geupdated!";
                    await SendAdminNotification(languageUpdateEpisodes, messageText);
                }

                if (newEpisodes.HasItems())
                {
                    await DBService.InsertEpisodesAsync(seriesReminder.Series.Id, newEpisodes);
                    await DBService.UpdateSeriesInfoAsync(seriesReminder.Series.Id, seriesInfo);

                    if (newEpisodes.Any(_ => _.LanguageFlag.HasFlag(seriesReminder.Language)))
                    {
                        matchingEpisodes.AddRange(newEpisodes.Where(_ => _.LanguageFlag.HasFlag(seriesReminder.Language)));
                    }

                    string messageText = $"Es wurden neue Episoden für <b>{seriesReminder.Series.Name}</b> gefunden und hinzugefügt!";
                    await SendAdminNotification(newEpisodes, messageText);
                }

                if (matchingEpisodes.HasItems())
                {
                    Logger.LogInformation($"{DateTime.Now} | Changes found for: {seriesReminder.Series.Name} | {matchingEpisodes.Count}x");
                    await SendNotifications(seriesInfo, group, matchingEpisodes);

                    if (telegramBotSettings is not null && !string.IsNullOrEmpty(telegramBotSettings.AdminChat))
                    {
                        if (group.Any(_ => _.User?.TelegramChatId == telegramBotSettings.AdminChat))
                        {
                            await DBService.InsertDownloadAsync(seriesReminder.Series.Id, matchingEpisodes);
                            await TelegramBotService.SendMessageAsync(long.Parse(telegramBotSettings.AdminChat), "Die Folgen wurden in die Download-Datenbank eingetragen.");
                        }

                        await SendAdminDownloadNotification(group, matchingEpisodes);
                    }
                }
            }
        }

        private async Task SendNotifications(SeriesInfoModel seriesInfo, IGrouping<int, SeriesReminderModel> seriesGroup, List<EpisodeModel> newEpisodes)
        {
            int maxCount = 5;

            string? seriesName = seriesGroup.First().Series!.Name;

            if (string.IsNullOrEmpty(seriesName))
                return;

            StringBuilder sb = new();

            sb.AppendLine($"{Emoji.Confetti} Neue Folge(n) für <b>{seriesName}</b> sind erschienen! {Emoji.Confetti}\n\n");
            sb.AppendLine($"{Emoji.Wave} Staffel: <b>{seriesInfo.SeasonCount}</b> Episode: <b>{seriesInfo.Seasons.Last().EpisodeCount}</b> {Emoji.Wave}\n");

            foreach (EpisodeModel newEpisode in newEpisodes.TakeLast(maxCount))
            {

                sb.AppendLine($"{Emoji.SmallBlackSquare} S<b>{newEpisode.Season:D2}</b> E<b>{newEpisode.Episode:D2}</b> {Emoji.HeavyMinus} {newEpisode.Name} [{newEpisode.LanguageFlag.ToLanguageText()}]");
            }

            if (newEpisodes.Count > maxCount)
            {
                sb.AppendLine($"{Emoji.SmallBlackSquare} <b>...</b>");
            }

            string messageText = "";
            string languageText = "";

            foreach (SeriesReminderModel? seriesReminder in seriesGroup)
            {
                languageText = $"\n\nFür Benachrichtigung eingestellte Sprache(n): {seriesReminder.Language.ToLanguageText()}";

                if (!string.IsNullOrEmpty(seriesReminder.User?.Username))
                {
                    messageText = $"Hallo {seriesReminder.User.Username}!\n\n" + sb.ToString() + languageText;
                }
                else
                {
                    messageText = sb.ToString() + languageText;
                }

                if (string.IsNullOrEmpty(seriesInfo.CoverArtUrl))
                {
                    await TelegramBotService.SendMessageAsync(Convert.ToInt64(seriesReminder.User.TelegramChatId), messageText);
                }
                else
                {
                    await TelegramBotService.SendPhotoAsync(Convert.ToInt64(seriesReminder.User.TelegramChatId), seriesInfo.CoverArtUrl, messageText);
                }

                string usernameText = string.IsNullOrEmpty(seriesReminder.User!.Username) ? "N/A" : seriesReminder.User.Username;
                Logger.LogInformation($"{DateTime.Now} | Sent 'New Episodes' notification to chat: {usernameText}|{seriesReminder.User.TelegramChatId}");
            }
        }

        private async Task SendAdminDownloadNotification(IGrouping<int, SeriesReminderModel> seriesGroup, List<EpisodeModel> newEpisodes)
        {
            TelegramBotSettingsModel? botSettings = SettingsHelper.ReadSettings<TelegramBotSettingsModel>();

            if (botSettings is null || string.IsNullOrEmpty(botSettings.AdminChat))
                return;

            string? seriesName = seriesGroup.First().Series!.Name;

            if (string.IsNullOrEmpty(seriesName))
                return;

            StringBuilder sb = new();

            sb.AppendLine($"{Emoji.Confetti} Neue Folge(n) zu den Downloads hinzugefügt! (<b>{seriesName}</b>) {Emoji.Confetti}\n\n");

            foreach (EpisodeModel newEpisde in newEpisodes)
            {
                sb.AppendLine($"{Emoji.SmallBlackSquare} S<b>{newEpisde.Season:D2}</b> E<b>{newEpisde.Episode:D2}</b> {Emoji.HeavyMinus} {newEpisde.Name} [{newEpisde.LanguageFlag.ToLanguageText()}]");
            }

            string messageText = sb.ToString();

            await TelegramBotService.SendMessageAsync(Convert.ToInt64(botSettings.AdminChat), messageText);

            Logger.LogInformation($"{DateTime.Now} | Sent 'New Episodes Admin' notification to chat: {botSettings.AdminChat}");
        }

        private async Task SendAdminNotification(List<EpisodeModel> episodes, string messageText)
        {
            TelegramBotSettingsModel? botSettings = SettingsHelper.ReadSettings<TelegramBotSettingsModel>();

            if (botSettings is null || string.IsNullOrEmpty(botSettings.AdminChat) || string.IsNullOrEmpty(messageText))
                return;
                 
            StringBuilder sb = new();

            sb.AppendLine($"Neue Admin Meldung:\n\n");
            sb.AppendLine($"{messageText}\n\n");


            foreach (EpisodeModel newEpisde in episodes)
            {
                sb.AppendLine($"{Emoji.SmallBlackSquare} S<b>{newEpisde.Season:D2}</b> E<b>{newEpisde.Episode:D2}</b> {Emoji.HeavyMinus} {newEpisde.Name} [{newEpisde.LanguageFlag.ToLanguageText()}]");
            }

            string fullMessageText = sb.ToString();

            await TelegramBotService.SendMessageAsync(Convert.ToInt64(botSettings.AdminChat), fullMessageText);

            Logger.LogInformation($"{DateTime.Now} | Sent Admin notification to chat: {botSettings.AdminChat}");
        }

        private async Task<(bool updateAvailable, SeriesInfoModel? seriesInfo, List<EpisodeModel>? updateEpisodes, List<EpisodeModel>? newEpisodes, List<EpisodeModel>? namesUpdatedEpisodes)> UpdateNeeded(SeriesReminderModel seriesReminder)
        {
            if (seriesReminder is null || seriesReminder.Series is null || string.IsNullOrEmpty(seriesReminder.Series.Name) || seriesReminder.Series.StreamingPortal is null || string.IsNullOrEmpty(seriesReminder.Series.StreamingPortal.Name))
                return (false, null, null, null, null);

            (int seasonCount, int episodeCount) = await DBService.GetSeriesSeasonEpisodeCountAsync(seriesReminder.Series.Id);

            IStreamingPortalService streamingPortalService;
            StreamingPortal streamingPortal = StreamingPortalHelper.GetStreamingPortalByName(seriesReminder.Series.StreamingPortal.Name!);

            switch (streamingPortal)
            {
                case StreamingPortal.Undefined:
                    return (false, null, null, null, null);
                case StreamingPortal.AniWorld:
                    streamingPortalService = AniWorldService;
                    break;
                case StreamingPortal.STO:
                    streamingPortalService = STOService;
                    break;
                default:
                    return (false, null, null, null, null);
            }

            SeriesInfoModel? seriesInfo = await streamingPortalService.GetSeriesInfoAsync(seriesReminder.Series.Name, streamingPortal);

            if (seriesInfo is null)
                return (false, null, null, null, null);


            if (seriesInfo.Seasons is null || seriesInfo.Seasons.Count == 0)
            {
                return (false, null, null, null, null);
            }

            List<EpisodeModel>? languageUpdateEpisodes = await GetLanguageUpdateEpisodes(seriesReminder.Series.Id, seriesInfo);
            List<EpisodeModel>? newEpisodes = await GetNewEpisodes(seriesReminder.Series.Id, seriesInfo);
            List<EpisodeModel>? namesUpdatedEpisodes = await GetEpisodeNamesUpdates(seriesReminder.Series.Id, seriesInfo);

            if (languageUpdateEpisodes.HasItems() || newEpisodes.HasItems() || namesUpdatedEpisodes.HasItems())
            {
                return (true, seriesInfo, languageUpdateEpisodes, newEpisodes, namesUpdatedEpisodes);
            }

            return (false, null, null, null, null);
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

        private async Task<List<EpisodeModel>?> GetLanguageUpdateEpisodes(int seriesId, SeriesInfoModel seriesInfo)
        {
            List<EpisodeModel>? dbEpisodes = await DBService.GetSeriesEpisodesAsync(seriesId);
            List<EpisodeModel>? updateEpisodes = new();

            if (dbEpisodes is null)
                return null;

            foreach (EpisodeModel episode in seriesInfo.Seasons.SelectMany(_ => _.Episodes))
            {
                EpisodeModel? epNeedUpdate = dbEpisodes.SingleOrDefault(_ => ( _.Season == episode.Season && _.Episode == episode.Episode ) && _.LanguageFlag != episode.LanguageFlag);

                if (epNeedUpdate is null)
                    continue;

                epNeedUpdate.LanguageFlag = episode.LanguageFlag;

                updateEpisodes.Add(epNeedUpdate);
            }

            return updateEpisodes;
        }

        private async Task<List<EpisodeModel>?> GetEpisodeNamesUpdates(int seriesId, SeriesInfoModel seriesInfo)
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
                    dbEpisodes.Any(dbEp => dbEp.Episode == seasonEp.Episode && dbEp.Season == seasonEp.Season && dbEp.Name != seasonEp.Name))
                        .ToList();

                if (!tempNewEpisodes.HasItems())
                    continue;

                foreach (EpisodeModel episode in tempNewEpisodes)
                {
                    EpisodeModel dbEp = dbEpisodes.First(dbEp => dbEp.Episode == episode.Episode && dbEp.Season == episode.Season);
                    episode.Id = dbEp.Id;
                    episode.SeriesId = dbEp.SeriesId;
                }

                newEpisodes.AddRange(tempNewEpisodes);
            }

            return newEpisodes;
        }
    }
}
