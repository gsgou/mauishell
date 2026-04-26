#if ANDROID
using AndroidX.DrawerLayout.Widget;
using Microsoft.Maui.Controls.Handlers.Compatibility;

namespace Shiny.Handlers;

public static partial class DisableShellFlyoutSwipeHandler
{
    public static partial void Register()
    {
        ShellRenderer.Mapper.AppendToMapping(
            nameof(DisableShellFlyoutSwipeHandler),
            (handler, _) =>
            {
                if (((IElementHandler)handler).PlatformView is DrawerLayout drawerLayout)
                    drawerLayout.SetDrawerLockMode(DrawerLayout.LockModeLockedClosed);
            }
        );
    }
}
#endif
