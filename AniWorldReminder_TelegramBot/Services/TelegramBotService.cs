using AniWorldReminder_TelegramBot.Misc;
using AniWorldReminder_TelegramBot.Models.AniWorld;
using AniWorldReminder_TelegramBot.Models.DB;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace AniWorldReminder_TelegramBot.Services
{
    public class TelegramBotService : ITelegramBotService
    {
        private readonly ILogger<TelegramBotService> Logger;
        private TelegramBotClient BotClient = default!;
        private readonly IDBService DBService;
        private readonly IStreamingPortalService AniWorldService;
        private readonly IStreamingPortalService STOService;

        public TelegramBotService(ILogger<TelegramBotService> logger, IDBService dbService, IStreamingPortalServiceFactory streamingPortalServiceFactory)
        {
            Logger = logger;
            DBService = dbService;
            AniWorldService = streamingPortalServiceFactory.GetService(StreamingPortal.AniWorld);
            STOService = streamingPortalServiceFactory.GetService(StreamingPortal.STO);
        }

        public async Task<bool> Init()
        {
            TelegramBotSettingsModel? settings = SettingsHelper.ReadSettings<TelegramBotSettingsModel>();

            if (settings is null)
            {
                Logger.LogError($"{DateTime.Now} | {ErrorMessage.ReadSettings}");
                return false;
            }

            BotClient = new(settings.Token);

            User? bot_me = await BotClient.GetMeAsync();

            if (bot_me is null)
            {
                Logger.LogError($"{DateTime.Now} | {ErrorMessage.RetrieveBotInfo}");
                return false;
            }

            ReceiverOptions? receiverOptions = new()
            {
                AllowedUpdates = new UpdateType[] { UpdateType.Message } //only receive message updates
            };

            BotClient.StartReceiving(
                updateHandler: HandleUpdate,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions
            );

            Logger.LogInformation($"{DateTime.Now} | Telegram Bot Service initialized");

            return true;
        }

        private async Task HandleUpdate(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            #region activate this if you allowed more update types than 'UpdateType.Message' and only want to process message updates
            //// Only process Message updates
            //if (update.Message is not { } message)
            //    return Task.CompletedTask;
            #endregion

            // Only process text messages
            if (update.Message?.Text is not { })
                return;

            const int maxSeconds = 30;

            if (( DateTime.UtcNow - update.Message.Date ).TotalSeconds > maxSeconds)
            {
                Logger.LogInformation($"{DateTime.Now} | Received a message older than {maxSeconds} seconds. Message is ignored.");
                return;
            }

            await HandleCommand(update.Message);
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            string? ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Logger.LogError($"{DateTime.Now} | {ErrorMessage}");

            return Task.CompletedTask;
        }

        private async Task HandleCommand(Message? message)
        {
            if (message is null || string.IsNullOrEmpty(message.Text))
                return;

            if (message.Text.IsCommand(out ChatCommand? chatCommand, out string? parameter) && chatCommand is not null)
            {
                Logger.LogInformation($"{DateTime.Now} | Received a '{chatCommand}' command from ChatId: {message.Chat.Id}.");

                switch (chatCommand)
                {
                    case ChatCommand.Remind:
                        await HandleRemind(message, parameter);
                        break;
                    case ChatCommand.RemRemind:
                        await HandleRemindRemove(message, parameter);
                        break;
                    case ChatCommand.Reminders:
                        await HandleReminders(message);
                        break;
                    case ChatCommand.Verify:
                        await HandleVerify(message);
                        break;
                    default:
                        break;
                }

                return;
            }

            UserState userState = await DBService.GetUserStateAsync(message.Chat.Id.ToString());

            if (userState == UserState.Undefined)
            {
                await HandleCommandNotRecognised(message);
                return;
            }


            await HandleRemind(message, message.Text, userState);
        }

        private async Task HandleCommandNotRecognised(Message message)
        {
            List<string>? commandNames = ChatCommandHelper.GetCommandNames();

            if (commandNames is null || commandNames.Count == 0)
                return;

            StringBuilder sb = new();

            sb.AppendLine("Eingegebenes Command wurde nicht erkannt.\n");
            sb.AppendLine($"{Emoji.Exclamationmark} <b>/[COMMAND]{UnicodeChars.OpenBox.As<char>()}[PARAMETER]</b> {Emoji.Exclamationmark}\n");
            sb.AppendLine("Folgende Commands sind verfügbar:\n");

            foreach (string commandName in commandNames)
            {
                sb.AppendLine($"{Emoji.HeavyCheckmark} /{commandName}\n");
            }

            string messageText = sb.ToString();

            await SendMessageAsync(message.Chat.Id, messageText);
        }

        [Time]
        private async Task HandleRemind(Message message, string? seriesName, UserState userState = UserState.Undefined)
        {
            if (string.IsNullOrEmpty(seriesName))
                return;

            await SendChatAction(message.Chat.Id, ChatAction.Typing);

            string telegramChatId = message.Chat.Id.ToString();

            UsersModel? user = await DBService.GetUserAsync(telegramChatId);

            if (user is null)
                await DBService.InsertUserAsync(telegramChatId);

            bool useStrictSearch = false;

            if (userState == UserState.KeyboardAnswer)
                useStrictSearch = true;

            (bool successAniWorld, List<SearchResultModel>? searchResultsAniWorld) = await AniWorldService.GetSeriesAsync(seriesName, useStrictSearch);
            (bool successSTO, List<SearchResultModel>? searchResultsSTO) = await STOService.GetSeriesAsync(seriesName, useStrictSearch);

            if (( !successAniWorld && !successSTO ) || ( !searchResultsAniWorld.HasItems() && !searchResultsSTO.HasItems() ))
            {
                await SendMessageAsync(message.Chat.Id, "Es wurden keine passenden Treffer gefunden.");
                return;
            }

            StreamingPortal streamingPortal;

            if (successAniWorld)
            {
                streamingPortal = StreamingPortal.AniWorld;
            }
            else if (successSTO)
            {
                streamingPortal = StreamingPortal.STO;
            }
            else { return; }

            List<SearchResultModel> allSearchResults = new();

            if (searchResultsAniWorld.HasItems())
                allSearchResults.AddRange(searchResultsAniWorld);

            if (searchResultsSTO.HasItems())
                allSearchResults.AddRange(searchResultsSTO);

            allSearchResults = allSearchResults.DistinctBy(_ => _.Title).ToList();

            if (allSearchResults.Count > 1)
            {
                await DBService.UpdateUserStateAsync(telegramChatId, UserState.KeyboardAnswer);
                await SendSearchResult(message, allSearchResults);
                return;
            }

            seriesName = allSearchResults[0].Title.StripHtmlTags().HtmlDecode();

            SeriesModel series = await DBService.GetSeriesAsync(seriesName);

            if (series is null)
                await InsertSeries(seriesName, streamingPortal);

            UsersSeriesModel? usersSeries = await DBService.GetUsersSeriesAsync(telegramChatId, seriesName);

            string messageText;

            if (usersSeries is null)
            {
                series = await DBService.GetSeriesAsync(seriesName);

                usersSeries = new()
                {
                    Users = user,
                    Series = series
                };

                await DBService.InsertUsersSeriesAsync(usersSeries);

                messageText = $"{Emoji.Checkmark} Dein Reminder für <b>{seriesName}</b> wurde hinzugefügt.";

                if (string.IsNullOrEmpty(series.CoverArtUrl))
                {
                    await SendMessageAsync(Convert.ToInt64(telegramChatId), messageText);
                }
                else
                {
                    await SendPhotoAsync(Convert.ToInt64(telegramChatId), series.CoverArtUrl, messageText);
                }
            }
            else
            {
                messageText = $"{Emoji.ExclamationmarkRed} Es existiert bereits ein Reminder für <b>{seriesName}</b> {Emoji.ExclamationmarkRed}";

                await SendMessageAsync(message.Chat.Id, messageText);
            }

            await DBService.UpdateUserStateAsync(telegramChatId, UserState.Undefined);
        }

        private async Task InsertSeries(string seriesName, StreamingPortal streamingPortal)
        {
            IStreamingPortalService streamingPortalService;

            switch (streamingPortal)
            {
                case StreamingPortal.STO:
                    streamingPortalService = STOService;
                    break;
                case StreamingPortal.AniWorld:
                    streamingPortalService = AniWorldService;
                    break;
                default:
                    return;
            }

            SeriesInfoModel? seriesInfo = await streamingPortalService.GetSeriesInfoAsync(seriesName, streamingPortal);

            if (seriesInfo is null)
                return;

            int seriesId = await DBService.InsertSeriesAsync(seriesInfo, streamingPortal);

            if (seriesId == -1)
                return;

            foreach (SeasonModel season in seriesInfo.Seasons)
            {
                await DBService.InsertEpisodesAsync(seriesId, season.Episodes);
            }
        }

        private async Task HandleRemindRemove(Message message, string? seriesName)
        {
            string telegramChatId = message.Chat.Id.ToString();

            UsersSeriesModel? usersSeries = await DBService.GetUsersSeriesAsync(telegramChatId, seriesName);

            string messageText;

            if (usersSeries is null)
            {
                messageText = $"{Emoji.ExclamationmarkRed} Du hast keinen Reminder für diesen Anime {Emoji.ExclamationmarkRed}";
                await SendMessageAsync(message.Chat.Id, messageText);
                return;
            }

            await DBService.DeleteUsersSeriesAsync(usersSeries);

            messageText = $"{Emoji.Checkmark} Reminder für <b>{seriesName}</b> wurde gelöscht.";
            await SendMessageAsync(message.Chat.Id, messageText);
        }

        private async Task HandleReminders(Message message)
        {
            string telegramChatId = message.Chat.Id.ToString();

            List<UsersSeriesModel>? usersSeries = await DBService.GetUsersSeriesAsync(telegramChatId);

            if (usersSeries is null || usersSeries.Count == 0)
            {
                string messageText = $"{Emoji.Crossmark} <b>Du hast aktuell keine Reminder</b> {Emoji.Crossmark}";
                await SendMessageAsync(message.Chat.Id, messageText);
                return;
            }

            StringBuilder sb = new();

            sb.AppendLine($"{Emoji.Exclamationmark} <b>Deine aktuellen Reminder:</b> {Emoji.Exclamationmark}\n");

            foreach (UsersSeriesModel userSeries in usersSeries)
            {
                if (userSeries.Series is null)
                    continue;

                sb.AppendLine($"{Emoji.HeavyCheckmark} {userSeries.Series.Name}");
            }

            await SendMessageAsync(message.Chat.Id, sb.ToString());
        }

        private async Task HandleVerify(Message message)
        {
            string telegramChatId = message.Chat.Id.ToString();

            UsersModel? user = await DBService.GetUserAsync(telegramChatId);

            user ??= await DBService.InsertUserAsync(telegramChatId);

            string messageText;

            if (user.Verified == VerificationStatus.Verified)
            {
                messageText = $"{Emoji.ExclamationmarkRed} <b>Du bist schon verifiziert!</b> {Emoji.ExclamationmarkRed}\nBenutzername: <b>{user.Username}</b>";
                await SendMessageAsync(message.Chat.Id, messageText);
                return;
            }

            string? token = Helper.GenerateToken(user);
            TokenValidationModel tokenValidation = Helper.ValidateToken(user, token);

            if (string.IsNullOrEmpty(token) || !tokenValidation.Validated)
            {
                messageText = $"{Emoji.ExclamationmarkRed} <b>Verifikations Code konnte nicht erstellt werden!</b> {Emoji.ExclamationmarkRed}";
                await SendMessageAsync(message.Chat.Id, messageText);
                return;
            }

            await DBService.UpdateVerifyTokenAsync(telegramChatId, token);  

            StringBuilder sb = new();

            sb.AppendLine($"{Emoji.Confetti} <b>Deine Daten zum Verifikationsprozesses:</b> {Emoji.Confetti}\n");
            sb.AppendLine($"{Emoji.Checkmark} Token: <b>{token}</b>\n");
            sb.AppendLine($"{Emoji.AlarmClock} Token ist gültig bis: <b>{tokenValidation.ExpireDate}</b>\n\n");
            sb.AppendLine($"{Emoji.ExclamationmarkRed} <b>Bitte gebe diese Daten auf der Webseite ein um den Verifikationsprozess abzuschließen</b> {Emoji.ExclamationmarkRed}\n");

            await SendMessageAsync(message.Chat.Id, sb.ToString());
        }

        private async Task SendSearchResult(Message message, List<SearchResultModel> searchResults)
        {
            string answerText = $"{Emoji.ExclamationmarkRed} Da die Suche mehr als einen Treffer enthielt, wähle bitte einen der angezeigten Animes aus.\n" +
                $"Falls der gesuchte Anime <b>nicht</b> aufgelistet ist, bitte den <b>Suchparameter erweitern</b> {Emoji.ExclamationmarkRed}";

            List<string>? top5SeriesNames = searchResults.Select(_ => _.Title)
                                                        .Take(5)
                                                            .ToList();

            ReplyKeyboardMarkup? rkm = GetKeyboard(top5SeriesNames);

            await SendMessageAsync(message.Chat.Id, answerText, rkm: rkm);
        }

        private static ReplyKeyboardMarkup GetKeyboard(List<string> text)
        {
            ReplyKeyboardMarkup? rkm = new("")
            {
                OneTimeKeyboard = true
            };
            List<KeyboardButton[]>? rows = new();
            List<KeyboardButton>? cols = new();

            for (int i = 0; i < text.Count; i++)
            {
                string seriesName = text[i].StripHtmlTags();

                cols.Add(new KeyboardButton(seriesName));

                if (i % 2 != 0)
                    continue;

                rows.Add(cols.ToArray());
                cols = new List<KeyboardButton>();
            }

            if (cols.Count > 0)
            {
                rows.Add(cols.ToArray());
            }

            rkm.Keyboard = rows.ToArray();
            return rkm;
        }

        public async Task SendChatAction(long chatId, ChatAction chatAction)
        {
            await BotClient.SendChatActionAsync(chatId, chatAction);
        }

        public async Task<Message?> SendMessageAsync(long chatId, string text, int? replyId = null, bool showLinkPreview = true, ParseMode parseMode = ParseMode.Html, ReplyKeyboardMarkup? rkm = null)
        {
            try
            {
                return await BotClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: text,
                    replyToMessageId: replyId,
                    parseMode: parseMode,
                    disableWebPagePreview: !showLinkPreview,
                    replyMarkup: rkm);
            }
            catch (Exception)
            {
                return null;
            }            
        }

        public async Task<Message?> SendPhotoAsync(long chatId, string photoUrl, string? text = null, ParseMode parseMode = ParseMode.Html)
        {
            try
            {
                return await BotClient.SendPhotoAsync(
                               chatId,
                         new InputFileUrl(photoUrl),
                               caption: text,
                               parseMode: parseMode);
            }
            catch (Exception)
            {
                return await SendMessageAsync(chatId, text, parseMode: parseMode);
            }
           
        }                
    }
}
