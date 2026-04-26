#if IOS || MACCATALYST
using Microsoft.Maui.Controls.Handlers.Compatibility;
using UIKit;

namespace Shiny.Handlers;

public static partial class DisableShellFlyoutSwipeHandler
{
    public static partial void Register()
    {
        ShellRenderer.Mapper.AppendToMapping(
            nameof(DisableShellFlyoutSwipeHandler),
            (handler, _) =>
            {
                if (((IElementHandler)handler).PlatformView is not UIViewController controller)
                    return;

                DisablePanGestures(controller.View);
            }
        );
    }


    static void DisablePanGestures(UIView? view)
    {
        if (view == null)
            return;

        var gestures = view.GestureRecognizers;
        if (gestures != null)
        {
            foreach (var gesture in gestures)
            {
                if (gesture is UIPanGestureRecognizer panGesture)
                    panGesture.Enabled = false;
            }
        }

        foreach (var subview in view.Subviews)
            DisablePanGestures(subview);
    }
}
#endif
