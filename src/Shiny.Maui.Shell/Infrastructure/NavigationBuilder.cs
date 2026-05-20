using Microsoft.Extensions.Logging;

namespace Shiny.Infrastructure;

public class NavigationBuilder(
    ShinyShellNavigator navigator,
    ShinyAppBuilder navBuilder,
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

        navigator.PrepareForProgrammaticNavigation(uri, navType, parameters);
        await mainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(uri, true, parameters));

        // Apply configure callbacks by walking the resulting navigation stack.
        // The last N entries of NavigationStack correspond to the N pushed
        // segments in chronological order. Avoids the static PageResolved event
        // which was vulnerable to cross-navigation handler crosstalk when
        // multiple Navigate() calls or NavigateTo<T> calls overlapped.
        if (this.segments.Any(s => s.ConfigureAction != null))
        {
            var stack = Shell.Current.Navigation.NavigationStack;
            for (var i = 0; i < this.segments.Count; i++)
            {
                var seg = this.segments[i];
                if (seg.ConfigureAction == null || seg.ViewModelType == null)
                    continue;

                var stackIndex = stack.Count - this.segments.Count + i;
                if (stackIndex < 0 || stackIndex >= stack.Count)
                {
                    logger.LogWarning(
                        "NavigationBuilder segment '{route}' is out of NavigationStack bounds (index {index}, stack count {count})",
                        seg.Route, stackIndex, stack.Count
                    );
                    continue;
                }

                var page = stack[stackIndex];
                if (!seg.ViewModelType.IsInstanceOfType(page.BindingContext))
                {
                    logger.LogWarning(
                        "NavigationBuilder segment '{route}' expected BindingContext '{expected}' but found '{actual}'",
                        seg.Route, seg.ViewModelType, page.BindingContext?.GetType()
                    );
                    continue;
                }

                logger.LogDebug("Configuring ViewModel '{type}' via NavigationBuilder", seg.ViewModelType);
                seg.ConfigureAction.DynamicInvoke(page.BindingContext);
            }
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
