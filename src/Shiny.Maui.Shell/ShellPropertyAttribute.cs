namespace Shiny;

/// <summary>
/// Creates a parameter in the source generated method
/// </summary>
/// <param name="description">Source generation uses this to create AI compatible parameters for the ShellMap method</param>
/// <param name="required">If this parameter is required for successful navigation</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ShellPropertyAttribute(
    string? description = null,
    bool required = true
) : Attribute
{
    public string? Description => description;
    public bool IsRequired => required;
}