namespace Shiny;

/// <summary>
/// This attribute is used to map a page to a route for Shell navigation. It can be applied to any class, but is typically used on ViewModel classes to associate them with their corresponding pages. The route can be specified explicitly or will default to the name of the page type. If the page is already registered in the AppShell xaml, set registerRoute to false to prevent conflicts.
/// </summary>
/// <param name="route">An optional route name (must be named like a C# class) or the page type name is used which can cause conflicted names</param>
/// <param name="registerRoute">Set this to false if you have the page specified in your AppShell xaml to prevent issues</param>
/// <param name="description">The source generator uses this to create AI compatible methods</param>
/// <typeparam name="TPage"></typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ShellMapAttribute<TPage>(
    string? route = null,
    bool registerRoute = true,
    string? description = null
) : Attribute
{
    public string Route => route ?? typeof(TPage).Name;
    public bool RegisterRoute => registerRoute;
}