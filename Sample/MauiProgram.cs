using Microsoft.Extensions.Logging;
using Sample.AI;
using Shiny;

namespace Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp
            .CreateBuilder()
            .UseMauiApp<App>()
            .UseShinyShell(x => x
                .UseUxDiversDialogs()
                .AddGeneratedMaps()
                .AddAiTools()
            )
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<GitHubCopilotAuthService>();
        builder.Services.AddSingleton<IMauiInitializeService, NavigationLogger>();

#if DEBUG
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
