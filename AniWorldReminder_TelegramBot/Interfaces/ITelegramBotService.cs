using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;

namespace AniWorldReminder_TelegramBot.Interfaces
{
    public interface ITelegramBotService
    {
        Task<bool> Init();
        Task<Message> SendMessageAsync(long chatId, string text, int? replyId = null, bool showLinkPreview = true, ParseMode parseMode = ParseMode.Html, ReplyKeyboardMarkup? rkm = null);
        Task SendChatAction(long chatId, ChatAction chatAction);
        Task<Message> SendPhotoAsync(long chatId, string photoUrl, string? text = null, ParseMode parseMode = ParseMode.Html);
    }
}
