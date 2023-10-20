using AniWorldReminder_TelegramBot.Enums;

namespace AniWorldReminder_TelegramBot.Misc
{
    public static class ChatCommandHelper
    {
        private static readonly Dictionary<string, ChatCommand> ChatCommands = new()
        {
            {
                "remind",
                ChatCommand.Remind
            },
             {
                "remremind",
                ChatCommand.RemRemind
            },
             {
                "reminders",
                ChatCommand.Reminders
            }
        };

        public static ChatCommand GetChatCommandByName(string commandName)
        {
            return ChatCommands.SingleOrDefault(_ => _.Key == commandName.ToLower()).Value;
        }

        public static List<string> GetCommandNames()
        {
            return System.Enum.GetNames(typeof(ChatCommand)).ToList();
        }

        public static ChatCommand ToChatCommand(this string textcommand)
        {
            return ChatCommands.SingleOrDefault(_ => _.Key == textcommand.ToLower()).Value;
        }
    }
}
