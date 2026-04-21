#if ANDROID
using Android.Views;
using Google.Android.Material.BottomNavigation;

namespace Shiny.Infrastructure;

internal static partial class ShellTabBadgePlatform
{
    static partial void PlatformSet(int tabIndex, int value)
    {
        var bottomNav = FindBottomNavigationView();
        var menuItem = GetMenuItem(bottomNav, tabIndex);
        var badge = bottomNav.GetOrCreateBadge(menuItem.ItemId);
        badge.Number = value;
    }


    static partial void PlatformClear(int tabIndex)
    {
        var bottomNav = FindBottomNavigationView();
        var menuItem = GetMenuItem(bottomNav, tabIndex);
        bottomNav.RemoveBadge(menuItem.ItemId);
    }


    static BottomNavigationView FindBottomNavigationView()
    {
        if (Shell.Current?.Handler?.PlatformView is not ViewGroup root)
            throw new InvalidOperationException("Could not locate the native Android Shell view");

        return FindBottomNavigationView(root)
            ?? throw new InvalidOperationException("Could not locate the native Android tab bar");
    }


    static BottomNavigationView? FindBottomNavigationView(ViewGroup root)
    {
        if (root is BottomNavigationView bottomNavigationView)
            return bottomNavigationView;

        for (var i = 0; i < root.ChildCount; i++)
        {
            if (root.GetChildAt(i) is not ViewGroup child)
                continue;

            var result = FindBottomNavigationView(child);
            if (result != null)
                return result;
        }
        return null;
    }


    static IMenuItem GetMenuItem(BottomNavigationView bottomNav, int tabIndex)
    {
        if (tabIndex >= bottomNav.Menu.Size())
            throw new InvalidOperationException($"Tab index '{tabIndex}' does not exist in the Android tab bar");

        return bottomNav.Menu.GetItem(tabIndex)
            ?? throw new InvalidOperationException($"Could not locate Android tab menu item at index '{tabIndex}'");
    }
}
#endif
