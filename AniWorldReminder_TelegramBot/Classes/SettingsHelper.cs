using System.Reflection;
using System.Text.Json;

namespace AniWorldReminder_TelegramBot.Classes
{
    public static class SettingsHelper
    {
        public static T? ReadSettings<T>()
        {
            string path;
            if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
            {
                path = @"/app/appdata/settings.json";

                if (!Directory.Exists(Path.GetDirectoryName(path)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                };
            }
            else
            {
                path = "settings.json";
            }

            using StreamReader r = new(path);
            string json = r.ReadToEnd();

            SettingsModel? settings = JsonSerializer.Deserialize<SettingsModel>(json);

            if (settings is null) return default;

            if (typeof(T) == typeof(SettingsModel))
            {
                return (T)Convert.ChangeType(settings, typeof(T));
            }

            return settings.GetSetting<T>();
        }

        private static T? GetSetting<T>(this SettingsModel settings)
        {
            return (T?)settings?.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .First(_ => _.PropertyType == typeof(T))
                .GetValue(settings, null);
        }
    }
}
