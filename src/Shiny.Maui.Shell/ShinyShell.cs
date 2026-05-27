using Shiny.Infrastructure;

namespace Shiny;

public class ShinyShell : Shell
{
    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);

        var page = this.CurrentPage;
        if (page == null)
            return;

        var services = this.Handler?.MauiContext?.Services
            ?? IPlatformApplication.Current?.Services;
        if (services == null)
            return;

        var navBuilder = services.GetService<ShinyAppBuilder>();
        var viewModelType = navBuilder?.GetViewModelTypeForPage(page);
        if (viewModelType == null)
            return;

        var configurator = services.GetService<ShellNavigationConfigurator>();

        // BindingContext inherits down the visual tree, so it may be the Shell
        // instance rather than null — check if it's already the correct ViewModel
        if (page.BindingContext != null && viewModelType.IsInstanceOfType(page.BindingContext))
        {
            // Page already bound (e.g., the route factory resolved it earlier).
            // Apply any still-pending configure callback so re-navigations to the
            // same VM instance also get property updates before OnAppearing.
            configurator?.TryApply(page.BindingContext);
            return;
        }

        var vm = services.GetService(viewModelType);
        if (vm != null)
        {
            // Apply pending configure BEFORE BindingContext is set so OnAppearing
            // observes a fully configured viewmodel.
            configurator?.TryApply(vm);
        }
        page.BindingContext = vm;
    }
}
