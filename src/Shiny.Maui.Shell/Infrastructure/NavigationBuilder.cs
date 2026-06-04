using Microsoft.Extensions.Logging;

namespace Shiny.Infrastructure;

public class NavigationBuilder(
    ShinyShellNavigator navigator,
    ShinyAppBuilder navBuilder,
    ShellNavigationConfigurator configurator,
    IMainThread mainThread,
    ILogger logger,
    bool fromRoot
) : INavigationBuilder
{
    record Segment(string Route, Type? ViewModelType, Delegate? ConfigureAction);

    readonly List<Segment> segments = new();
    int popCount;

    public INavigationBuilder PopBack(int count)
    {
        if (count < 1)
            throw new ArgumentException("Count must be 1 or more", nameof(count));

        if (fromRoot)
            throw new InvalidOperationException("PopBack is not supported when navigating from root");

        if (this.segments.Count > 0)
            throw new InvalidOperationException("PopBack must be called before any Add calls");

        this.popCount += count;
        return this;
    }

    public INavigationBuilder Add<TViewModel>() where TViewModel : class
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel))
            ?? throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        this.segments.Add(new Segment(route, typeof(TViewModel), null));
        return this;
    }

    public INavigationBuilder Add<TViewModel>(Action<TViewModel> configure) where TViewModel : class
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel))
            ?? throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        this.segments.Add(new Segment(route, typeof(TViewModel), configure));
        return this;
    }

    public INavigationBuilder Add(string routeName)
    {
        this.segments.Add(new Segment(routeName, null, null));
        return this;
    }

    public async Task Navigate()
    {
        if (this.popCount == 0 && this.segments.Count == 0)
            throw new InvalidOperationException("No navigation segments have been added");

        var uri = this.BuildUri();
        var parameters = new Dictionary<string, object>();
        var navType = fromRoot ? NavigationType.SetRoot : NavigationType.Push;

        // Pre-resolve each typed segment's viewmodel, apply its configure callback
        // synchronously, and pin the instance on the configurator. The apply sites
        // (ShinyRouteFactory.GetOrCreate, ShinyShell.OnNavigated, AppOnPageAppearing)
        // consume the pinned instances in FIFO + type order matching the order Shell
        // realises each segment's page. No post-await stack walk is required because
        // the configure callbacks have already run before any page is constructed.
        var subscriptions = new List<IDisposable>(this.segments.Count);
        foreach (var seg in this.segments)
        {
            if (seg.ViewModelType == null)
                continue;

            var vm = navigator.Services.GetRequiredService(seg.ViewModelType);
            seg.ConfigureAction?.DynamicInvoke(vm);
            subscriptions.Add(configurator.EnqueueResolved(seg.ViewModelType, vm));
        }

        navigator.PrepareForProgrammaticNavigation(uri, navType, parameters);

        try
        {
            await mainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(uri, true, parameters));
        }
        catch
        {
            // Only roll back pinned entries when navigation actually failed.
            // On success we leave them pinned because the apply sites typically
            // fire on the next dispatcher tick (notably on Android), and
            // disposing here would cause them to fall back to a fresh DI resolve.
            foreach (var sub in subscriptions)
                sub.Dispose();
            throw;
        }
    }

    string BuildUri()
    {
        var prefix = fromRoot ? "//" : "";
        var popPart = string.Join("/", Enumerable.Repeat("..", this.popCount));
        var routePart = string.Join("/", this.segments.Select(s => s.Route));

        var separator = this.popCount > 0 && this.segments.Count > 0 ? "/" : "";
        return prefix + popPart + separator + routePart;
    }
}
