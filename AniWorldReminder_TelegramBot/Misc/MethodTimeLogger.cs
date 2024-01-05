using System.Reflection;

namespace AniWorldReminder_TelegramBot.Misc
{
    public static class MethodTimeLogger
    {
        public static ILogger Logger;

        public static void Log(MethodBase methodBase, TimeSpan timeSpan, string message)
        {
            string additionalInfo = $" Additional Info: {message}";
            string duration = $" Runtime: {timeSpan:mm}m {timeSpan:ss}s.";

            Type type = methodBase.DeclaringType;
            Type? @interface = type!.GetInterfaces()
                .FirstOrDefault(i => type.GetInterfaceMap(i).TargetMethods.Any(m => m.DeclaringType == type));

            string info = "Executed ";

            if (@interface is not null && @interface.FullName == "Quartz.IJob" && methodBase.Name == "Execute")
            {
                info += $"CronJob: ";
            }

            Logger.LogInformation($"{DateTime.Now} | " + info + "{Class}.{Method}.{Duration}{Message}",
                methodBase.DeclaringType!.Name,
                methodBase.Name,
                ( timeSpan.Seconds > 0 ? duration : "" ),
                ( string.IsNullOrEmpty(message) ? "" : additionalInfo ));
        }

        public static void LogExecution(MethodBase methodBase)
        {
            Type type = methodBase.DeclaringType;
            Type? @interface = type!.GetInterfaces()
                .FirstOrDefault(i => type.GetInterfaceMap(i).TargetMethods.Any(m => m.DeclaringType == type));

            string info = "Started ";

            if (@interface is not null && @interface.FullName == "Quartz.IJob" && methodBase.Name == "Execute")
            {
                info += $"CronJob: ";
            }

            Logger.LogInformation($"{DateTime.Now} | " + info + "{Class}.{Method}.",
                methodBase.DeclaringType!.Name,
                methodBase.Name);
        }
    }
}

