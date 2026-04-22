using Microsoft.Extensions.Logging;
using Shiny;

namespace Sample;

public class NavigationLogger(
    ILogger<NavigationLogger> logger,
    INavigator navigator
) : IMauiInitializeService
{
    public void Initialize(IServiceProvider services)
    {
        navigator.Navigating += (_, args) =>
        {
            logger.LogInformation(
                "Navigating: {FromUri} -> {ToUri} [{Type}] Params: {Params}",
                args.FromUri,
                args.ToUri,
                args.NavigationType,
                string.Join(", ", args.Parameters.Select(p => $"{p.Key}={p.Value}"))
            );
        };

        navigator.Navigated += (_, args) =>
        {
            logger.LogInformation(
                "Navigated: {ToUri} [{Type}] VM: {ViewModel}",
                args.ToUri,
                args.NavigationType,
                args.ToViewModel?.GetType().Name
            );
        };
    }
}
