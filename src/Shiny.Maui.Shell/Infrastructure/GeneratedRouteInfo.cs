namespace Shiny.Infrastructure;

/// <summary>
/// This is used by source generation
/// </summary>
/// <param name="Route">The route from ShellMapAttribute</param>
/// <param name="Parameters">Parameters that are marked with ShellPropertyAttribute</param>
public record GeneratedRouteInfo(string Route, GeneratedRouteParameter[] Parameters);

/// <summary>
/// This is route parameter info
/// </summary>
/// <param name="ParameterName">Property name</param>
/// <param name="Description">The description/documentation of the property</param>
public record GeneratedRouteParameter(string ParameterName, string Description);


static class Test
{
    // WHAT I WANT GENERATED
    public static GeneratedRouteInfo[] GetGeneratedRouteInfo(this INavigator navigator) =>
    [
        new("DetailPage", [new("Text", "Show this text"), new("Text2", "Show this text2")])
    ];
}