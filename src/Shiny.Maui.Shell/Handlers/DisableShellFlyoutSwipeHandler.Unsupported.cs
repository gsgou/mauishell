#if !ANDROID && !IOS && !MACCATALYST && !WINDOWS
namespace Shiny.Handlers;

public static partial class DisableShellFlyoutSwipeHandler
{
    public static partial void Register() { }
}
#endif
