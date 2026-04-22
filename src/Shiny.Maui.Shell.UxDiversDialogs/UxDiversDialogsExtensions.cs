using UXDivers.Popups.Maui;

namespace Shiny;

public static class UxDiversDialogsExtensions
{
    public static ShinyAppBuilder UseUxDiversDialogs(this ShinyAppBuilder builder)
    {
        builder.UseDialogs<UxDiversDialogs>();
        builder.MauiBuilder.UseUXDiversPopups();
        return builder;
    }
}
