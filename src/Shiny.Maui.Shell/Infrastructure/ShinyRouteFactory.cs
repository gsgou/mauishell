namespace Shiny.Infrastructure;


public class ShinyRouteFactory(Type pageType, Type viewModelType) : RouteFactory
{
    public override Element GetOrCreate() => throw new NotImplementedException();

    public override Element GetOrCreate(IServiceProvider services)
    {
        var page = (Page)services.GetRequiredService(pageType);
        var vm = services.GetRequiredService(viewModelType);

        // Apply any pending NavigateTo<T>(configure) callback BEFORE the page's
        // BindingContext is set so the viewmodel is fully configured before
        // IPageLifecycleAware.OnAppearing fires.
        services.GetService<ShellNavigationConfigurator>()?.TryApply(vm);

        page.BindingContext = vm;
        return page;
    }
}
