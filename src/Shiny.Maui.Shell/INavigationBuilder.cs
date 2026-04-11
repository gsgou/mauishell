namespace Shiny;

// https://docs.prismlibrary.com/docs/current/platforms/maui/navigation/navigation-builder
// //rootpage/page1/page2
// relative to current   page1/page2
// relative to current   ../../page1/page2 - pops 2 and adds two more
public interface INavigationBuilder
{
    /// <summary>
    /// Adds one or more ".." pop-back segments to the navigation URI. Must be called before any Add calls.
    /// </summary>
    /// <param name="count">The number of pages to pop back. Defaults to 1.</param>
    INavigationBuilder PopBack(int count = 1);

    /// <summary>
    /// Adds a navigation segment for the specified view model type.
    /// </summary>
    /// <typeparam name="TViewModel">The view model type. Must be registered in the navigation system.</typeparam>
    INavigationBuilder Add<TViewModel>() where TViewModel : class;

    /// <summary>
    /// Adds a navigation segment for the specified view model type with a configure callback
    /// that is invoked on the view model when the page is created during navigation.
    /// </summary>
    /// <typeparam name="TViewModel">The view model type. Must be registered in the navigation system.</typeparam>
    /// <param name="configure">An action to configure the view model before the page appears.</param>
    INavigationBuilder Add<TViewModel>(Action<TViewModel> configure) where TViewModel : class;

    /// <summary>
    /// Adds a navigation segment using a raw route string.
    /// </summary>
    /// <param name="routeName">The route name to navigate to.</param>
    INavigationBuilder Add(string routeName);

    /// <summary>
    /// Builds the final URI from all accumulated segments and executes the navigation.
    /// </summary>
    Task Navigate();
}
