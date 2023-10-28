using RegexSchemaLib.Classes;
using RegexSchemaLib.Models;
using RegexSchemaLib.Structs;

namespace AniWorldReminder_TelegramBot.Misc
{
    public static class RegexBuilder
    {
        public static string? BuildChatCommandPattern()
        {
            SchemaModel schema = new()
            {
                 Pattern = "^(?i:/[COMMAND] [PARAM])$|^(?i:/[COMMAND])$"
            };

            List<string> chatCommands = new();

            foreach (ChatCommand chatCommand in Enum.GetValues<ChatCommand>())
            {
                if (chatCommand == ChatCommand.Undefined)
                    continue;

                chatCommands.Add(chatCommand.ToString());
            }

            PlaceholderModel phChatCommands = new(useDefaultGroupOptions: true)
            {
                Name = "COMMAND",
                ReplaceValue = chatCommands.Concat('|')
            };

            PlaceholderModel phParameter = new(useDefaultGroupOptions: true)
            {
                Name = "PARAM",
                ReplaceValue = ".+?"
            };

            schema.Placeholders.Add(phChatCommands);
            schema.Placeholders.Add(phParameter);

            (string? result, ErrorModel? _) = new RegexSchema(schema).CreateRegexPattern();

            return result;
        }
    }
}
