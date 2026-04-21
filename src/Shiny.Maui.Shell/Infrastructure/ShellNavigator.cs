using Microsoft.Extensions.Logging;

namespace Shiny.Infrastructure;


// TODO: //PageName and ../../PageName or ../Page1/Page2
// TODO: replace route, backCount = 2, relative
public class ShinyShellNavigator(
    ILogger<ShinyShellNavigator> logger,
    IApplication application,
    IMainThread mainThread,
    IServiceProvider services,
    ShinyAppBuilder navBuilder,
    ShellTabBadgeManager tabBadgeManager
) : INavigator, IMauiInitializeService, IDisposable
{
    public event EventHandler<NavigationEventArgs>? Navigating;
    public event EventHandler<NavigatedEventArgs>? Navigated;

    record PendingNavigation(string ToUri, NavigationType NavigationType, IReadOnlyDictionary<string, object> Parameters);
    PendingNavigation? pendingNavigation;
    bool isProgrammaticNavigation;

    public void Initialize(IServiceProvider _)
    {
        if (application is not Application app)
            throw new InvalidOperationException($"Invalid MAUI Application - {application.GetType()}");

        app.DescendantAdded += this.AppOnDescendantAdded;
        app.DescendantRemoved += this.AppOnDescendantRemoved;
        app.PageAppearing += this.AppOnPageAppearing;
        app.PageDisappearing += this.AppOnPageDisappearing;

        // The initial page may have already appeared before event handlers were registered
        var currentPage = Shell.Current?.CurrentPage;
        if (currentPage != null)
            this.AppOnPageAppearing(this, currentPage);
    }
    
    
    public void Dispose()
    {
        if (application is Application app)
        {
            app.DescendantAdded -= this.AppOnDescendantAdded;
            app.DescendantRemoved -= this.AppOnDescendantRemoved;
            app.PageAppearing -= this.AppOnPageAppearing;
            app.PageDisappearing -= this.AppOnPageDisappearing;
        }
    }

    
    void RaiseNavigating(Shell shell, string toUri, NavigationType navigationType, IDictionary<string, object> parameters)
    {
        var readOnlyParams = new Dictionary<string, object>(parameters);
        this.pendingNavigation = new PendingNavigation(toUri, navigationType, readOnlyParams);

        try
        {
            this.Navigating?.Invoke(this, new NavigationEventArgs(
                shell.CurrentState?.Location?.ToString(),
                shell.CurrentPage?.BindingContext,
                toUri,
                navigationType,
                readOnlyParams
            ));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Navigating event handler");
        }
    }


    void RaiseNavigated(object? toViewModel)
    {
        var pending = this.pendingNavigation;
        this.pendingNavigation = null;
        if (pending == null)
            return;

        try
        {
            this.Navigated?.Invoke(this, new NavigatedEventArgs(
                pending.ToUri,
                toViewModel,
                pending.NavigationType,
                pending.Parameters
            ));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Navigated event handler");
        }
    }


    public INavigationBuilder CreateBuilder(bool fromRoot = false)
        => new NavigationBuilder(this, navBuilder, mainThread, logger, fromRoot);


    internal void PrepareForProgrammaticNavigation(string uri, NavigationType navType, Dictionary<string, object> parameters)
    {
        if (Shell.Current.CurrentPage?.BindingContext is INavigationAware navAware)
            navAware.OnNavigatingFrom(parameters);

        this.RaiseNavigating(Shell.Current, uri, navType, parameters);
        this.isProgrammaticNavigation = true;
    }


    public Task NavigateTo(string route, bool relativeNavigation = true, params IEnumerable<(string Key, object Value)> args) =>
        mainThread.InvokeOnMainThreadAsync(() =>
        {
            var shell = Shell.Current;
            var parameters = args.ToDictionary(x => x.Key, x => x.Value);

            if (shell.CurrentPage?.BindingContext is INavigationAware navAware)
                navAware.OnNavigatingFrom(parameters);

            var uri = relativeNavigation ? route : $"//{route}";
            var navType = relativeNavigation ? NavigationType.Push : NavigationType.SetRoot;
            this.RaiseNavigating(shell, uri, navType, parameters);
            this.isProgrammaticNavigation = true;

            if (OperatingSystem.IsLinux())
            {
                // Shell.GoToAsync is unreliable on Platform.Maui.Linux.Gtk4 — resolve
                // the page from the registered route map and push directly.
                var pageType = navBuilder.GetPageTypeForRoute(route);
                if (pageType != null && services.GetService(pageType) is Page page)
                    return shell.Navigation.PushAsync(page, true);
            }

            return shell.GoToAsync(uri, true, parameters);
        });


    public async Task NavigateTo<TViewModel>(
        Action<TViewModel>? configure = null,
        bool relativeNavigation = true,
        params IEnumerable<(string Key, object Value)> args
    )
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel));
        if (route == null)
            throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        if (!relativeNavigation)
            route = $"//{route}";

        var tcs = new TaskCompletionSource();
        var handler = new EventHandler<Page>((_, page) =>
        {
            if (page.BindingContext is TViewModel vm)
            {
                logger.LogDebug("Pre-Configuring ViewModel");
                configure?.Invoke(vm);
                tcs.TrySetResult();
            }
            else
                tcs.TrySetException(new InvalidOperationException($"Page BindingContext is not of type '{typeof(TViewModel)}'"));
        });

        try
        {
            var parameters = args.ToDictionary(x => x.Key, x => x.Value);
            if (Shell.Current.CurrentPage?.BindingContext is INavigationAware navAware)
                navAware.OnNavigatingFrom(parameters);

            var navType = relativeNavigation ? NavigationType.Push : NavigationType.SetRoot;
            this.RaiseNavigating(Shell.Current, route, navType, parameters);

            ShinyRouteFactory.PageResolved += handler;
            this.isProgrammaticNavigation = true;
            await mainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(route, true, parameters));
            await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            ShinyRouteFactory.PageResolved -= handler;
        }
    }

    
    public Task PopToRoot(params IEnumerable<(string Key, object Value)> args)
    {
        // we already have 1 page covered and we don't want to pop the last page
        var count = Shell.Current.Navigation.NavigationStack.Count - 1;
        if (count < 1)
            count = 1;

        return this.DoGoBack(count, NavigationType.PopToRoot, args);
    }


    public Task GoBack(params IEnumerable<(string Key, object Value)> args) => this.DoGoBack(1, NavigationType.GoBack, args);


    public Task GoBack(int backCount = 1, params IEnumerable<(string Key, object Value)> args) => this.DoGoBack(backCount, NavigationType.GoBack, args);


    public async Task SwitchShell(Shell shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        if (application is not Application app)
            throw new InvalidOperationException($"Invalid MAUI Application - {application.GetType()}");

        var currentShell = Shell.Current;
        var parameters = new Dictionary<string, object>();

        if (currentShell?.CurrentPage?.BindingContext is INavigationAware navAware)
            navAware.OnNavigatingFrom(parameters);

        if (currentShell != null)
        {
            this.RaiseNavigating(
                currentShell,
                shell.GetType().Name,
                NavigationType.SwitchShell,
                parameters
            );
        }

        if (app.Windows.Count == 0)
            throw new InvalidOperationException("No active window to switch Shell on");

        // Two-phase swap: first replace the current Shell with a temporary blank page.
        // This forces the platform to tear down the old Shell handlers and puts the
        // native window (UIWindow on iOS) into a clean state — avoiding the crash in
        // ShellFlyoutRenderer.ViewDidLoad that occurs when a new Shell handler is
        // created while the old Shell's native view hierarchy is still active.
        await mainThread.InvokeOnMainThreadAsync(() =>
        {
            var window = app.Windows[0];
            if (window.Page?.Handler is IElementHandler oldHandler)
            {
                logger.LogDebug("Disconnecting old handler '{type}'", oldHandler.GetType().Name);
                oldHandler.DisconnectHandler();
            }
            window.Page = new ContentPage();
        });

        // Yield to let the platform fully process the interim page and clean up native state
        await Task.Delay(50).ConfigureAwait(false);

        // Now set the actual Shell in a clean window state
        await mainThread.InvokeOnMainThreadAsync(() =>
        {
            var window = app.Windows[0];
            window.Page = shell;
            logger.LogDebug("Switched Shell to '{type}'", shell.GetType().Name);
        });
    }


    public Task SwitchShell<TShell>() where TShell : Shell
    {
        var shell = services.GetRequiredService<TShell>();
        return this.SwitchShell(shell);
    }


    public Task SetTabBadge(string route, int value) => tabBadgeManager.Set(route, value);


    public Task SetTabBadge<TViewModel>(int value)
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel));
        if (route == null)
            throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        return tabBadgeManager.Set(route, value);
    }


    public Task ClearTabBadge(string route) => tabBadgeManager.Clear(route);


    public Task ClearTabBadge<TViewModel>()
    {
        var route = navBuilder.GetRouteForViewModel(typeof(TViewModel));
        if (route == null)
            throw new InvalidOperationException($"Could not find a route for viewmodel '{typeof(TViewModel)}'");

        return tabBadgeManager.Clear(route);
    }


    Task DoGoBack(int backCount, NavigationType navType, IEnumerable<(string Key, object Value)> args) => mainThread.InvokeOnMainThreadAsync(() =>
    {
        if (backCount < 1)
            throw new ArgumentException("Back count must be 1 or more");

        var uri = String.Empty;
        for (var i = 0; i < backCount; i++)
        {
            if (i > 0)
                uri += "/";

            uri += "..";
        }

        var shell = Shell.Current;
        var parameters = args.ToDictionary(x => x.Key, x => x.Value);
        if (shell.CurrentPage?.BindingContext is INavigationAware navAware)
            navAware.OnNavigatingFrom(parameters);

        this.RaiseNavigating(shell, uri, navType, parameters);
        this.isProgrammaticNavigation = true;
        return shell.GoToAsync(uri, true, parameters);
    });
    
    
    void AppOnDescendantAdded(object? sender, ElementEventArgs args)
    {
        if (args.Element is Shell shell)
        {
            shell.Navigating += async (_, shellArgs) =>
            {
                if (this.isProgrammaticNavigation)
                {
                    this.isProgrammaticNavigation = false;
                    return;
                }
                
                var vm = shell.CurrentPage?.BindingContext;

                if (vm is INavigationConfirmation confirm)
                {
                    var deferral = shellArgs.GetDeferral();
                    var canNav = await confirm.CanNavigate();
                    if (!canNav)
                        shellArgs.Cancel();

                    deferral.Complete();
                }
            };
        }
    }
    
    
    void AppOnDescendantRemoved(object? sender, ElementEventArgs args)
    {
        if (args.Element is Page { BindingContext: IDisposable disposable })
        {
            logger.LogDebug("[Dispose] ViewModel '{type}'", disposable.GetType());
            disposable.Dispose();
        }
    }

    
    void AppOnPageAppearing(object? sender, Page page)
    {
        // BindingContext may be inherited from Shell rather than explicitly set —
        // check whether it's already the correct ViewModel type
        var viewModelType = navBuilder.GetViewModelTypeForPage(page);
        if (viewModelType != null && (page.BindingContext == null || !viewModelType.IsInstanceOfType(page.BindingContext)))
        {
            var vm = services.GetService(viewModelType);
            page.BindingContext = vm;
            logger.LogDebug("[Binding] ViewModel {type} set on page", viewModelType);
        }

        if (page.BindingContext is IPageLifecycleAware lc)
        {
            logger.LogDebug("[OnAppearing] ViewModel '{type}' ", lc.GetType());
            lc.OnAppearing();
        }

        tabBadgeManager.ReapplyAll();
        this.RaiseNavigated(page.BindingContext);
    }


    void AppOnPageDisappearing(object? sender, Page page)
    {
        if (page.BindingContext is IPageLifecycleAware lc)
        {
            logger.LogDebug("[OnAppearing] ViewModel '{type}' ", lc.GetType());
            lc.OnDisappearing();
        }
    }
}
