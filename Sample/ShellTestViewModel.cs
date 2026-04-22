using Shiny;

namespace Sample;

[ShellMap<ShellTestPage>]
public partial class ShellTestViewModel(INavigator navigator) : ObservableObject
{
    [ObservableProperty] string currentShellType = Shell.Current?.GetType().Name ?? "Unknown";

    [RelayCommand]
    async Task Switch(string type)
    {
        Shell shell = type switch
        {
            "Tabbed" => new TabbedShell(),
            "Flyout" => new FlyoutShell(),
            _ => new AppShell()
        };
        await navigator.SwitchShell(shell);
        this.CurrentShellType = shell.GetType().Name;
    }
}
