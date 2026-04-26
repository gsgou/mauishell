#if WINDOWS
namespace Shiny.Handlers;

public static partial class DisableShellFlyoutSwipeHandler
{
    // Windows Shell does not support flyout swipe gestures
    public static partial void Register() { }
}
#endif
