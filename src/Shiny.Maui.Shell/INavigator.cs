namespace Shiny;

public interface INavigator
{
    event EventHandler<NavigationEventArgs>? Navigating;
    event EventHandler<NavigatedEventArgs>? Navigated;

    /// <summary>
    /// Creates a fluent navigation builder for constructing multi-segment navigation URIs
    /// </summary>
    /// <param name="fromRoot">If true, builds an absolute URI starting with "//". If false (default), builds a relative URI.</param>
    INavigationBuilder CreateBuilder(bool fromRoot = false);


    /// <summary>
    /// Navigates to the specified route and passes the provided arguments to the target page or view model.
    /// </summary>
    /// <remarks>To receive the arguments passed via <paramref name="args"/>, the target page or view model
    /// must implement the <see cref="IQueryAttributable"/> interface.</remarks>
    /// <param name="route">The route to navigate to. This should be a valid route string recognized by the navigation system.</param>
    /// <param name="relativeNavigation">Assumes relative navigation (page1/page2), if set to false, assumes root navigation "//" </param>
    /// <param name="args">A collection of key-value pairs representing the arguments to pass to the target page or view model. Each key
    /// must be unique.</param>
    /// <returns>A task that represents the asynchronous navigation operation.</returns>
    Task NavigateTo(string route, bool relativeNavigation = true, params IEnumerable<(string Key, object Value)> args);


    /// <summary>
    /// Navigates to a view associated with the specified view model type.
    /// </summary>
    /// <remarks>This method allows for flexible navigation by enabling both view model configuration and the
    /// passing of additional arguments. Ensure that the specified view model type is properly registered and that any
    /// required arguments are provided.</remarks>
    /// <typeparam name="TViewModel">The type of the view model to navigate to. The view model must be registered in the navigation system.</typeparam>
    /// <param name="configure">An optional action to configure the view model before navigation. This can be used to set up properties or
    /// perform initialization.</param>
    /// <param name="relativeNavigation">Assumes relative navigation (page1/page2), if set to false, assumes root navigation "//" </param>
    /// <param name="args">A collection of key-value pairs representing arguments to pass to the view during navigation. Each key must be
    /// unique.</param>
    /// <returns>A task that represents the asynchronous navigation operation.</returns>
    Task NavigateTo<TViewModel>(
        Action<TViewModel>? configure = null, 
        bool relativeNavigation = true,
        params IEnumerable<(string Key, object Value)> args
    );

    
    /// <summary>
    /// Returns to the root page regardless of how far up the stack you are
    /// </summary>
    /// <param name="args">A collection of key-value pairs representing parameters to pass to the target view or state.  Each key must be a
    /// unique identifier, and the value represents the associated data.</param>
    /// <returns></returns>
    Task PopToRoot(params IEnumerable<(string Key, object Value)> args);
    
    
    /// <summary>
    /// Navigates back to the previous view or state in the application, optionally passing parameters to the target.
    /// </summary>
    /// <remarks>The behavior of the navigation may depend on the application's navigation stack or state
    /// management. Ensure that the keys and values provided in <paramref name="args"/> are compatible with the target
    /// view or state.</remarks>
    /// <param name="args">A collection of key-value pairs representing parameters to pass to the target view or state.  Each key must be a
    /// unique identifier, and the value represents the associated data.</param>
    /// <returns>A task that represents the asynchronous operation of navigating back.</returns>
    Task GoBack(params IEnumerable<(string Key, object Value)> args);
    
    
    /// <summary>
    /// Navigates back to the previous view or state in the application, optionally passing parameters to the target.
    /// </summary>
    /// <remarks>The behavior of the navigation may depend on the application's navigation stack or state
    /// management. Ensure that the keys and values provided in <paramref name="args"/> are compatible with the target
    /// view or state.</remarks>
    /// <param name="backCount">Allows you to go back 1 or more pages in the navigation stack. Defaults to 1.</param>
    /// <param name="args">A collection of key-value pairs representing parameters to pass to the target view or state.  Each key must be a
    /// unique identifier, and the value represents the associated data.</param>
    /// <returns>A task that represents the asynchronous operation of navigating back.</returns>
    Task GoBack(int backCount = 1, params IEnumerable<(string Key, object Value)> args);


    /// <summary>
    /// Switches the application's main page to the specified Shell instance, replacing the current Shell entirely.
    /// </summary>
    /// <remarks>This replaces the current <see cref="Application.MainPage"/> with the provided Shell instance,
    /// effectively resetting the navigation stack. Use this for scenarios like switching between a login shell and a main app shell.</remarks>
    /// <param name="shell">The Shell instance to switch to.</param>
    /// <returns>A task that represents the asynchronous shell switch operation.</returns>
    Task SwitchShell(Shell shell);


    /// <summary>
    /// Switches the application's main page to a Shell instance resolved from the dependency injection container.
    /// </summary>
    /// <remarks>This resolves the specified Shell type from the service provider and replaces the current
    /// <see cref="Application.MainPage"/>, effectively resetting the navigation stack.</remarks>
    /// <typeparam name="TShell">The type of Shell to resolve and switch to. Must be registered in the DI container.</typeparam>
    /// <returns>A task that represents the asynchronous shell switch operation.</returns>
    Task SwitchShell<TShell>() where TShell : Shell;
}