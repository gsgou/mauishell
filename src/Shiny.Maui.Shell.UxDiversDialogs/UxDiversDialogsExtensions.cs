using UXDivers.Popups.Maui;

namespace Shiny;

public static class UxDiversDialogsExtensions
{
    public static ShinyAppBuilder UseUxDiversDialogs(this ShinyAppBuilder builder)
    {
        builder.UseDialogs<UxDiversDialogs>();
        return builder;
    }

    public static MauiAppBuilder UseUxDiversDialogs(this MauiAppBuilder builder)
    {
        builder.UseUXDiversPopups();
        return builder;
    }
}
