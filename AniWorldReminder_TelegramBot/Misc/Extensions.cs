using AniWorldReminder_TelegramBot.Enums;
using MySqlX.XDevAPI.Common;
using Quartz;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace AniWorldReminder_TelegramBot.Misc
{
    public static class Extensions
    {
        public static bool IsCommand(this string text, out ChatCommand? command, out string? parameter)
        {
            command = null;
            parameter = null;

            string? regexPattern = RegexBuilder.BuildChatCommandPattern();

            if (string.IsNullOrEmpty(regexPattern))
                return false;

            Regex regex = new(regexPattern);

            Match match = regex.Match(text);

            bool isMatch = match.Success;

            if (!isMatch)
                return false;

            List<Group>? groupList = match.Groups.ToList(removeEmptyGroups: true);

            if(!groupList.HasItems())
                return false;

            command = match.Groups["COMMAND"].Value.ToChatCommand();

            parameter = match.Groups["PARAM"].Value;

            return true;
        }
        public static T As<T>(this UnicodeChars unicode)
        {
            return (T)Convert.ChangeType(unicode, typeof(T));
        }
        public static string StripHtmlTags(this string text)
        {
            return Regex.Replace(text, "<.*?>", string.Empty); //|&.*?;
        }
        public static string HtmlDecode(this string text)
        {
            return HttpUtility.HtmlDecode(text);
        }
        public static string SearchSanitize(this string text)
        {
            return text
               .Replace("+", "%2B")
               .Replace(' ', '+')
               .Replace("'", "");
        }
        public static string UrlSanitize(this string text)
        {
            return text.Replace(' ', '-')
                .Replace(":", "")
                .Replace("~", "")
                .Replace("'", "")
                .Replace(",", "")
                .Replace("’", "")
                .Replace("+", "")
                .Replace(".", "")
                .Replace("!", "")
                .Replace("--", "-");
        }
        public static string? ToLanguageText(this Language languages)
        {
            string? languageText = null;

            if (languages.HasFlag(Language.GerDub))
            {
                languageText += "<b>GerDub</b>|";
            }

            if (languages.HasFlag(Language.GerSub))
            {
                languageText += "<b>GerSub</b>|";
            }

            if (languages.HasFlag(Language.EngDub))
            {
                languageText += "<b>EngDub</b>|";
            }

            if (languages.HasFlag(Language.EngSub))
            {
                languageText += "<b>EngSub</b>|";
            }

            if (!string.IsNullOrEmpty(languageText))
                languageText = languageText.Remove(languageText.Length - 1, 1);

            return languageText;
        }
        public static void AddJobAndTrigger<T>(this IServiceCollectionQuartzConfigurator quartz, int intervalInMinutes) where T : IJob
        {
            // Use the name of the IJob as the appsettings.json key
            string jobName = typeof(T).Name;

            // Try and load the schedule from configuration
            var configKey = $"Quartz:{jobName}";

            // register the job as before
            JobKey? jobKey = new(jobName);
            quartz.AddJob<T>(opts => opts.WithIdentity(jobKey));

            quartz.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity(jobName + "-trigger")
                .WithSimpleSchedule(_ =>
                    _.WithIntervalInMinutes(intervalInMinutes)
                    .RepeatForever())
                .StartNow());
        }
        public static bool HasItems<T>(this IEnumerable<T> source) => source != null && source.Any();
        public static async Task<(bool success, string? ipv4)> GetIPv4(this HttpClient httpClient)
        {
            string result = await httpClient.GetStringAsync("https://api.ipify.org/");

            return (!string.IsNullOrEmpty(result), result);
        }
        public static string Concat(this IEnumerable<string> list, char delimeter, bool removeLastChar = true)
        {
            return string.Join(delimeter, list);
        }
        public static List<Group>? ToList(this GroupCollection groupCollection, bool removeEmptyGroups = true)
        {
            List<Group> result = new();

            if (!groupCollection.HasItems<Group>())
                return null;

            result = groupCollection.Cast<Group>().ToList();

            if (removeEmptyGroups)
                result = result.Where(_ => !string.IsNullOrEmpty(_.Value)).ToList();


            return result;
        }
    }
}
