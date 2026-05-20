namespace Shiny.Infrastructure;


public class ShinyRouteFactory(Type pageType, Type viewModelType) : RouteFactory
{
    public override Element GetOrCreate() => throw new NotImplementedException();

    public override Element GetOrCreate(IServiceProvider services)
    {
        var page = (Page)services.GetRequiredService(pageType);
        page.BindingContext = services.GetRequiredService(viewModelType);
        return page;
    }
}