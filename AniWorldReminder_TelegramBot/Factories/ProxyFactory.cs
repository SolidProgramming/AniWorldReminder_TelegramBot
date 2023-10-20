using System.Net;

namespace AniWorldReminder_TelegramBot.Factories
{
    public static class ProxyFactory
    {
        public static WebProxy? CreateProxy(ProxyAccountModel proxyAccount)
        {
            if (string.IsNullOrEmpty(proxyAccount.Uri))
                return null;

            return new WebProxy()
            {
                Address = new Uri(proxyAccount.Uri),
                BypassProxyOnLocal = false,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(proxyAccount.Username, proxyAccount.Password)
            };
        }
    }
}
