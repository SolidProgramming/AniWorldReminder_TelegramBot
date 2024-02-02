global using AniWorldReminder_TelegramBot.Interfaces;
global using AniWorldReminder_TelegramBot.Classes;
global using AniWorldReminder_TelegramBot.Models;
global using AniWorldReminder_TelegramBot.Misc;
global using AniWorldReminder_TelegramBot.Enums;
global using AniWorldReminder_TelegramBot.Models.AniWorld;
global using AniWorldReminder_TelegramBot.Models.DB;
global using Emoji = AniWorldReminder_TelegramBot.Misc.Emoji;
global using MethodTimer;
using AniWorldReminder_TelegramBot.Services;
using Quartz;
using AniWorldReminder_TelegramBot.Factories;
using System.Net;

namespace AniWorldReminder_TelegramBot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton<IDBService, DBService>();
            builder.Services.AddSingleton<ITelegramBotService, TelegramBotService>();

            builder.Services.AddSingleton<Interfaces.IHttpClientFactory, HttpClientFactory>();

            builder.Services.AddSingleton<IStreamingPortalServiceFactory>(_ =>
            {
                StreamingPortalServiceFactory streamingPortalServiceFactory = new();
                streamingPortalServiceFactory.AddService(StreamingPortal.AniWorld, _);
                streamingPortalServiceFactory.AddService(StreamingPortal.STO, _);

                return streamingPortalServiceFactory;
            });

            builder.Services.AddQuartz(_ =>
            {
                _.AddJobAndTrigger<AniWorldSTOJob>(60);
            });

            builder.Services.AddQuartzHostedService(_ => _.WaitForJobsToComplete = true);

            WebApplication? app = builder.Build();
            MethodTimeLogger.Logger = app.Logger;

            IDBService DBService = app.Services.GetRequiredService<IDBService>();
            if (!await DBService.Init())
                return;

            ITelegramBotService? telegramBotService = app.Services.GetRequiredService<ITelegramBotService>();
            if (!await telegramBotService.Init())
                return;

            Interfaces.IHttpClientFactory? httpClientFactory = app.Services.GetRequiredService<Interfaces.IHttpClientFactory>();
            HttpClient? noProxyClient = httpClientFactory.CreateHttpClient<Program>();

            (bool successNoProxy, string? ipv4NoProxy) = await noProxyClient.GetIPv4();
            if (!successNoProxy)
            {
                app.Logger.LogError($"{DateTime.Now} | HttpClient could not retrieve WAN IP Address. Shutting down...");
                return;
            }

            app.Logger.LogInformation($"{DateTime.Now} | Your WAN IP: {ipv4NoProxy}");

            SettingsModel? settings = SettingsHelper.ReadSettings<SettingsModel>();

            if (settings is null)
                return;

            WebProxy? proxy = null;

            if (settings.UseProxy)
            {
                ProxyAccountModel? proxyAccount = settings.ProxySettings;

                if (proxyAccount is not null)
                    proxy = ProxyFactory.CreateProxy(proxyAccount);
            }

            IStreamingPortalServiceFactory streamingPortalServiceFactory = app.Services.GetRequiredService<IStreamingPortalServiceFactory>();

            IStreamingPortalService aniWordService = streamingPortalServiceFactory.GetService(StreamingPortal.AniWorld);
            IStreamingPortalService sTOService = streamingPortalServiceFactory.GetService(StreamingPortal.STO);

            if (!await aniWordService.Init(proxy))
                return;

            if (!await sTOService.Init(proxy))
                return;

            app.Run();
        }
    }
}