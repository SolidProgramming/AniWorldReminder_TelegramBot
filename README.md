# AniWorldReminder_TelegramBot
ASP.NET Backend to host a Telegram Bot that reminds you when a new Episode of a TV Series/Anime is uploaded to AniWorld.to/S.to. You can extend the hosters by adding a new hoster class and interface.


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

