# AniWorldReminder_TelegramBot
ASP.NET Backend to host a Telegram Bot that reminds you when a new Episode of a TV Series/Anime is uploaded to AniWorld.to/S.to. You can extend the hosters by adding a new hoster class and interface.


# Getting started


## Add a new Hoster

### Add a Hoster to Enums.StreamingPortal
```C#
 public enum StreamingPortal
 {
     Undefined = 0,
     AniWorld = 1,
     STO = 2,
     MyNewHoster = 3 //Here
 }
```

### Add new Service that implements IStreamingPortalService
```C#
public class MyNewHosterService : IStreamingPortalService
{
    private readonly ILogger<MyNewHosterService> Logger;
    private readonly Interfaces.IHttpClientFactory HttpClientFactory;
    private HttpClient? HttpClient;

    public string BaseUrl { get; init; }
    public string Name { get; init; }

    public AniWorldSTOService(ILogger<MyNewHosterService> logger, Interfaces.IHttpClientFactory httpClientFactory, string baseUrl, string name)
    {
        BaseUrl = baseUrl;
        Name = name;

        HttpClientFactory = httpClientFactory;

        Logger = logger;
    }
}
//... implement the missing Methods required by IStreamingPortalService
```

### Add Service instance of Hoster to the StreamingPortalServiceFactory in StreamingPortalServiceFactory.cs
```C#
private static IStreamingPortalService CreateService(StreamingPortal streamingPortal, IServiceProvider sp)
{
    Interfaces.IHttpClientFactory httpClientFactory = sp.GetRequiredService<Interfaces.IHttpClientFactory>();

    switch (streamingPortal)
    {
        case StreamingPortal.STO:
            ILogger<AniWorldSTOService> loggerSTO = sp.GetRequiredService<ILogger<AniWorldSTOService>>();
            return new AniWorldSTOService(loggerSTO, httpClientFactory, "https://s.to", "S.TO");
        case StreamingPortal.AniWorld:
            ILogger<AniWorldSTOService> loggerAniWorld = sp.GetRequiredService<ILogger<AniWorldSTOService>>();
            return new AniWorldSTOService(loggerAniWorld, httpClientFactory, "https://aniworld.to", "AniWorld");
        case StreamingPortal.MyNewHoster:
           ILogger<AniWorldSTOService> loggerMyNewHoster = sp.GetRequiredService<ILogger<MyNewHosterService>>(); //Add Logger
           return new MyNewHosterService(loggerAniWorld, httpClientFactory, "https://mynewhoster.to", "MyNewHoster"); //Here
        default:
            throw new NotImplementedException();
    }
}
```

### Add the Hoster Service to the StreamingPortalServiceFactory for the Dependency Injection(DI) in Program.cs.
```C#
builder.Services.AddSingleton<IStreamingPortalServiceFactory>(_ =>
{
    StreamingPortalServiceFactory streamingPortalServiceFactory = new();
    streamingPortalServiceFactory.AddService(StreamingPortal.AniWorld, _);
    streamingPortalServiceFactory.AddService(StreamingPortal.STO, _);
    streamingPortalServiceFactory.AddService(StreamingPortal.MyNewHoster, _); //Here

    return streamingPortalServiceFactory;
});
```

## Add a Quartz Job
### Add a Job class in Classes and add the required Services via Dependency Injection(DI).
```C#
using MethodTimer;
using Quartz;
using System.Reflection;
using System.Text;

namespace AniWorldReminder_TelegramBot.Classes
{
    [DisallowConcurrentExecution]
    public class MyNewHosterJob : IJob
    {
        private readonly ILogger<MyNewHosterJob> Logger;
        private readonly IStreamingPortalService MyNewHosterService;
        private readonly IDBService DBService;
        private readonly ITelegramBotService TelegramBotService;

        public MyNewHosterJob(ILogger<MyNewHosterJob> logger,
            IStreamingPortalServiceFactory streamingPortalServiceFactory,
            IDBService dbService,
            ITelegramBotService telegramBotService)
        {
            Logger = logger;
            MyNewHosterService = streamingPortalServiceFactory.GetService(StreamingPortal.MyNewHoster);
            DBService = dbService;
            TelegramBotService = telegramBotService;
        }

        [Time]
        public async Task Execute(IJobExecutionContext context)
        {
            MethodBase? methodBase = typeof(MyNewHosterJob).GetMethod("Execute");
            MethodTimeLogger.LogExecution(methodBase);

            // ... Check for new Episodes
        }

  }
}
```

### Add Quartz Job and Trigger to Program.cs. The Number represents the interval for the Job to be executed.
```C#
builder.Services.AddQuartz(_ =>
{
    _.AddJobAndTrigger<AniWorldSTOJob>(60);
    _.AddJobAndTrigger<MyNewHosterJob>(60); //Here.
});
```
