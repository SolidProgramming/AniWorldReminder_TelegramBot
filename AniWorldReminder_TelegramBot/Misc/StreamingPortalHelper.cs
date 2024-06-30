using AniWorldReminder_TelegramBot.Enums;

namespace AniWorldReminder_TelegramBot.Misc
{
    public static class StreamingPortalHelper
    {
        private static readonly Dictionary<string, StreamingPortal> StreamingPortals = new()
        {
            { "AniWorld", StreamingPortal.AniWorld },
            { "STO", StreamingPortal.STO }
        };

        public static async Task<(bool reachable, string? html)> GetHosterReachableAsync(IStreamingPortalService streamingPortalService)
        {
            try
            {
                HttpClient httpClient = streamingPortalService.GetHttpClient();
                HttpResponseMessage responseMessage = await httpClient.GetAsync(new Uri(streamingPortalService.BaseUrl));

                if (!responseMessage.IsSuccessStatusCode)
                    return (false, null);

                string html = await responseMessage.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(html) || CaptchaRequired(html))
                    return (false, null);

                return (true, html);
            }
            catch (Exception)
            {
                return (false, null);
            }
        }
        private static bool CaptchaRequired(string html)
        {
            return html.Contains("Browser Check");
        }

        public static StreamingPortal GetStreamingPortalByName(string streamingPortalName)
        {
            if (StreamingPortals.Any(_ => _.Key == streamingPortalName))
            {
                return StreamingPortals[streamingPortalName];
            }

            return StreamingPortal.Undefined;
        }
        public static string? GetStreamingPortalName(StreamingPortal streamingPortal)
        {
            if (StreamingPortals.Any(_ => _.Value == streamingPortal))
            {
                return StreamingPortals.First(_ => _.Value == streamingPortal).Key;
            }

            return null;
        }
    }
}
