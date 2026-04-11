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

        var configurableSegments = new Queue<Segment>(
            this.segments.Where(s => s.ConfigureAction != null)
        );

        var tcs = configurableSegments.Count > 0
            ? new TaskCompletionSource()
            : null;

        EventHandler<Page>? handler = null;
        if (configurableSegments.Count > 0)
        {
            handler = (_, page) =>
            {
                if (configurableSegments.Count == 0)
                    return;

                var next = configurableSegments.Peek();
                if (next.ViewModelType != null &&
                    next.ViewModelType.IsInstanceOfType(page.BindingContext))
                {
                    configurableSegments.Dequeue();
                    logger.LogDebug("Pre-Configuring ViewModel '{type}' via NavigationBuilder", next.ViewModelType);
                    next.ConfigureAction!.DynamicInvoke(page.BindingContext);

                    if (configurableSegments.Count == 0)
                        tcs!.TrySetResult();
                }
            };
            ShinyRouteFactory.PageResolved += handler;
        }

        try
        {
            navigator.PrepareForProgrammaticNavigation(uri, navType, parameters);
            await mainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(uri, true, parameters));

            if (tcs != null)
                await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            if (handler != null)
                ShinyRouteFactory.PageResolved -= handler;
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
