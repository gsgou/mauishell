namespace Shiny.Infrastructure;

/// <summary>
/// This is used by source generation
/// </summary>
/// <param name="Route">The route from ShellMapAttribute</param>
/// <param name="Description">The description of the route from ShellMapAttribute</param>
/// <param name="Parameters">Parameters that are marked with ShellPropertyAttribute</param>
public record GeneratedRouteInfo(
    string Route, 
    string Description,
    GeneratedRouteParameter[] Parameters
);

/// <summary>
/// This is route parameter info
/// </summary>
/// <param name="ParameterName">Property name</param>
/// <param name="Description">The description/documentation of the property</param>
public record GeneratedRouteParameter(string ParameterName, string Description);