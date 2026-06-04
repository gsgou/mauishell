namespace Shiny.Infrastructure;


public class ShinyRouteFactory(Type pageType, Type viewModelType) : RouteFactory
{
    public override Element GetOrCreate() => throw new NotImplementedException();

    public override Element GetOrCreate(IServiceProvider services)
    {
        var page = (Page)services.GetRequiredService(pageType);

        // Prefer the pinned (pre-resolved + pre-configured) viewmodel from a
        // pending NavigateTo<TVm> / INavigationBuilder.Navigate call. Falls
        // back to DI when no pinned instance exists (e.g. Shell navigated to
        // this route directly via Shell.Current.GoToAsync without going
        // through INavigator).
        var configurator = services.GetService<ShellNavigationConfigurator>();
        var vm = configurator?.TryConsume(viewModelType)
            ?? services.GetRequiredService(viewModelType);

        page.BindingContext = vm;
        return page;
    }
}
