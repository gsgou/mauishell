using Shiny;

namespace Sample;

[ShellMap<BadgeDemoPage>]
public partial class BadgeDemoViewModel(INavigator navigator, IDialogs dialogs) : ObservableObject
{
    [RelayCommand]
    async Task SetBadge(string value)
    {
        if (!await this.EnsureTabbedShell())
            return;

        await navigator.SetTabBadge<BadgeDemoViewModel>(int.Parse(value));
    }

    [RelayCommand]
    async Task ClearBadge()
    {
        if (!await this.EnsureTabbedShell())
            return;

        await navigator.ClearTabBadge<BadgeDemoViewModel>();
    }

    async Task<bool> EnsureTabbedShell()
    {
        if (Shell.Current is TabbedShell)
            return true;

        if (!await dialogs.Confirm("Switch Shell?", "Tab badges require the Tabbed Shell. Switch now?"))
            return false;

        await navigator.SwitchShell(new TabbedShell());
        return true;
    }
}
