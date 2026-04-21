using Microsoft.Extensions.Logging;

namespace Shiny.Infrastructure;

public sealed class ShellTabBadgeManager(
    ILogger<ShellTabBadgeManager> logger,
    IMainThread mainThread
)
{
    readonly Dictionary<string, int> badgeValues = new(StringComparer.Ordinal);

    public Task Set(string route, int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Tab badge value must be 0 or greater");

        return mainThread.InvokeOnMainThreadAsync(() =>
        {
            var normalizedRoute = NormalizeRoute(route);
            var target = this.ResolveTarget(normalizedRoute);

            ShellTabBadgePlatform.Set(target.Index, value);
            this.badgeValues[normalizedRoute] = value;
        });
    }


    public Task Clear(string route) => mainThread.InvokeOnMainThreadAsync(() =>
    {
        var normalizedRoute = NormalizeRoute(route);
        var target = this.ResolveTarget(normalizedRoute);

        ShellTabBadgePlatform.Clear(target.Index);
        this.badgeValues.Remove(normalizedRoute);
    });


    public void ReapplyAll()
    {
        if (this.badgeValues.Count == 0)
            return;

        foreach (var badge in this.badgeValues)
        {
            if (!this.TryResolveTarget(badge.Key, out var target))
                continue;

            try
            {
                ShellTabBadgePlatform.Set(target.Index, badge.Value);
            }
            catch (PlatformNotSupportedException ex)
            {
                logger.LogWarning(ex, "Tab badges are not supported on this platform");
                return;
            }
            catch (InvalidOperationException ex)
            {
                logger.LogDebug(ex, "Unable to reapply tab badge for route '{route}'", badge.Key);
            }
        }
    }


    static string NormalizeRoute(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);

        var segments = route
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
            throw new ArgumentException("Route must contain at least one segment", nameof(route));

        return segments[^1];
    }


    TabTarget ResolveTarget(string route)
    {
        if (!this.TryResolveTarget(route, out var target))
            throw new InvalidOperationException($"Could not resolve tab route '{route}' in the active Shell");

        return target;
    }


    bool TryResolveTarget(string route, out TabTarget target)
    {
        target = default;

        var shell = Shell.Current;
        var shellItem = shell?.CurrentItem;
        if (shellItem == null)
            return false;

        for (var index = 0; index < shellItem.Items.Count; index++)
        {
            var section = shellItem.Items[index];
            if (RouteMatches(section, route))
            {
                target = new TabTarget(index, section);
                return true;
            }

            foreach (var content in section.Items)
            {
                if (RouteMatches(content, route))
                {
                    target = new TabTarget(index, section);
                    return true;
                }
            }
        }
        return false;
    }


    static bool RouteMatches(BindableObject element, string route)
        => String.Equals(Routing.GetRoute(element), route, StringComparison.Ordinal);


    readonly record struct TabTarget(int Index, ShellSection Section);
}
