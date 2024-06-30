using AniWorldReminder_TelegramBot.Enums;

namespace AniWorldReminder_TelegramBot.Misc
{
    public static class ChatCommandHelper
    {
        private static readonly Dictionary<string, ChatCommand> ChatCommands = new()
        {
             {
                "verify",
                ChatCommand.Verify
            }
        };

        public static ChatCommand GetChatCommandByName(string commandName)
        {
            if (commandName is null)
                return ChatCommand.Undefined;

            return ChatCommands.SingleOrDefault(_ => _.Key == commandName.ToLower()).Value;
        }

        public static List<string> GetCommandNames()
        {
            return System.Enum.GetNames(typeof(ChatCommand)).Where(_ => !_.Equals(ChatCommand.Undefined.ToString())).ToList();
        }

        public static ChatCommand ToChatCommand(this string textcommand)
        {
            return ChatCommands.SingleOrDefault(_ => _.Key == textcommand.ToLower()).Value;
        }
    }
}
