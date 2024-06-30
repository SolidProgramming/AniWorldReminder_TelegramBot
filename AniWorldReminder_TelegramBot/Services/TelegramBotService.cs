using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace AniWorldReminder_TelegramBot.Services
{
    public class TelegramBotService(ILogger<TelegramBotService> logger, IDBService dbService, IStreamingPortalServiceFactory streamingPortalServiceFactory) : ITelegramBotService
    {
        private TelegramBotClient BotClient = default!;
        private readonly IStreamingPortalService AniWorldService = streamingPortalServiceFactory.GetService(StreamingPortal.AniWorld);
        private readonly IStreamingPortalService STOService = streamingPortalServiceFactory.GetService(StreamingPortal.STO);

        public async Task<bool> Init()
        {
            TelegramBotSettingsModel? settings = SettingsHelper.ReadSettings<TelegramBotSettingsModel>();

            if (settings is null)
            {
                logger.LogError($"{DateTime.Now} | {ErrorMessage.ReadSettings}");
                return false;
            }

            BotClient = new(settings.Token);

            User? bot_me = await BotClient.GetMeAsync();

            if (bot_me is null)
            {
                logger.LogError($"{DateTime.Now} | {ErrorMessage.RetrieveBotInfo}");
                return false;
            }

            ReceiverOptions? receiverOptions = new()
            {
                AllowedUpdates = [UpdateType.Message] //only receive message updates
            };

            BotClient.StartReceiving(
                updateHandler: HandleUpdate,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions
            );

            logger.LogInformation($"{DateTime.Now} | Telegram Bot Service initialized");

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
                logger.LogInformation($"{DateTime.Now} | Received a message older than {maxSeconds} seconds. Message is ignored.");
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

            logger.LogError($"{DateTime.Now} | {ErrorMessage}");

            return Task.CompletedTask;
        }

        private async Task HandleCommand(Message? message)
        {
            if (message is null || string.IsNullOrEmpty(message.Text))
                return;

            if (message.Text.IsCommand(out ChatCommand? chatCommand, out string? parameter) && chatCommand is not null)
            {
                logger.LogInformation($"{DateTime.Now} | Received a '{chatCommand}' command from ChatId: {message.Chat.Id}.");

                switch (chatCommand)
                {
                    case ChatCommand.Verify:
                        await HandleVerify(message);
                        break;
                    default:
                        break;
                }

                return;
            }

            UserState userState = await dbService.GetUserStateAsync(message.Chat.Id.ToString());

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
            sb.AppendLine($"{Emoji.Exclamationmark} <b>/[COMMAND]\n");
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

            await SendMessageAsync(message.Chat.Id, "Dieses Command wird nicht mehr unterstützt. Bitte verwende die Webseite zum hinzufügen von Remindern.");
        }

        private async Task HandleRemindRemove(Message message, string? seriesName)
        {
            string telegramChatId = message.Chat.Id.ToString();

            UsersSeriesModel? usersSeries = await dbService.GetUsersSeriesAsync(telegramChatId, seriesName);

            string messageText;

            if (usersSeries is null)
            {
                messageText = $"{Emoji.ExclamationmarkRed} Du hast keinen Reminder für diese Serie {Emoji.ExclamationmarkRed}";
                await SendMessageAsync(message.Chat.Id, messageText);
                return;
            }

            await dbService.DeleteUsersSeriesAsync(usersSeries);

            messageText = $"{Emoji.Checkmark} Reminder für <b>{seriesName}</b> wurde gelöscht.";
            await SendMessageAsync(message.Chat.Id, messageText);
        }

        private async Task HandleReminders(Message message)
        {
            string telegramChatId = message.Chat.Id.ToString();

            List<UsersSeriesModel>? usersSeries = await dbService.GetUsersSeriesAsync(telegramChatId);

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

            UsersModel? user = await dbService.GetUserAsync(telegramChatId);

            user ??= await dbService.InsertUserAsync(telegramChatId);

            string messageText;

            if (user.Verified == VerificationStatus.Verified)
            {
                messageText = $"{Emoji.ExclamationmarkRed} <b>Du bist schon verifiziert!</b> {Emoji.ExclamationmarkRed}\nBenutzername: <b>{user.Username}</b>";
                await SendMessageAsync(message.Chat.Id, messageText);
                return;
            }

            string? token = Helper.GenerateToken(user);

            if (string.IsNullOrEmpty(token))
            {
                messageText = $"{Emoji.ExclamationmarkRed} <b>Verifikations Code konnte nicht erstellt werden!</b> {Emoji.ExclamationmarkRed}";
                await SendMessageAsync(message.Chat.Id, messageText);
                return;
            }

            TokenValidationModel tokenValidation = Helper.ValidateToken(user, token);

            if (!tokenValidation.Validated)
            {
                messageText = $"{Emoji.ExclamationmarkRed} <b>Verifikations Code wurde erstellt aber konnte nicht validiert werden!</b> {Emoji.ExclamationmarkRed}";
                await SendMessageAsync(message.Chat.Id, messageText);
                return;
            }

            await dbService.UpdateVerifyTokenAsync(telegramChatId, token);  

            StringBuilder sb = new();

            const string website = "https://proxymov.xyz/verify";

            sb.AppendLine($"{Emoji.Confetti} <b>Deine Daten zum Verifikationsprozesses:</b> {Emoji.Confetti}\n");
            sb.AppendLine($"{Emoji.Checkmark} Token: <b>{token}</b>\n");
            sb.AppendLine($"{Emoji.AlarmClock} Token ist gültig bis: <b>{tokenValidation.ExpireDate}</b>\n\n");
            sb.AppendLine($"{Emoji.ExclamationmarkRed} <b>Bitte gebe diese Daten auf der folgenden Webseite ein um den Verifikationsprozess abzuschließen und dich einzuloggen</b> {Emoji.ExclamationmarkRed}\n\n");
            sb.AppendLine($"{website}\n");

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
            List<KeyboardButton[]>? rows = [];
            List<KeyboardButton>? cols = [];

            for (int i = 0; i < text.Count; i++)
            {
                string seriesName = text[i].StripHtmlTags();

                cols.Add(new KeyboardButton(seriesName));

                if (i % 2 != 0)
                    continue;

                rows.Add([.. cols]);
                cols = [];
            }

            if (cols.Count > 0)
            {
                rows.Add([.. cols]);
            }

            rkm.Keyboard = rows;
            return rkm;
        }

        public async Task SendChatAction(long chatId, ChatAction chatAction)
        {
            await BotClient.SendChatActionAsync(chatId, chatAction);
        }

        public async Task<Message?> SendMessageAsync(long chatId, string text, int? replyId = null, bool showLinkPreview = true, ParseMode parseMode = ParseMode.Html, bool silentMessage = false, ReplyKeyboardMarkup? rkm = null)
        {
            try
            {
                return await BotClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: text,
                    replyToMessageId: replyId,
                    parseMode: parseMode,
                    disableWebPagePreview: !showLinkPreview,
                    replyMarkup: rkm,
                    disableNotification: silentMessage);
            }
            catch (Exception)
            {
                return null;
            }            
        }

        public async Task<Message?> SendPhotoAsync(long chatId, string photoUrl, string? text = null, ParseMode parseMode = ParseMode.Html, bool silentMessage = false)
        {
            try
            {
                return await BotClient.SendPhotoAsync(
                               chatId,
                         new InputFileUrl(photoUrl),
                               caption: text,
                               parseMode: parseMode,
                               disableNotification: silentMessage);
            }
            catch (Exception)
            {
                return await SendMessageAsync(chatId, text, parseMode: parseMode);
            }
           
        }                
    }
}
