using System.Collections.ObjectModel;
using Shiny;

namespace Sample;

[ShellMap<LifecycleDemoPage>]
public partial class LifecycleDemoViewModel(INavigator navigator, IDialogs dialogs)
    : ObservableObject, IPageLifecycleAware, INavigationAware, INavigationConfirmation, IDisposable
{
    public ObservableCollection<string> Events { get; } = [];

    void Log(string message) => Events.Add($"[{DateTime.Now:HH:mm:ss}] {message}");

    public void OnAppearing() => this.Log("OnAppearing");
    public void OnDisappearing() => this.Log("OnDisappearing");

    public void OnNavigatingFrom(IDictionary<string, object> parameters)
    {
        this.Log($"OnNavigatingFrom (params: {parameters.Count})");
        parameters["FromLifecycle"] = "true";
    }

    public async Task<bool> CanNavigate()
    {
        this.Log("CanNavigate called");
        return await dialogs.Confirm("Leave?", "Allow navigation away from Lifecycle page?");
    }

    public void Dispose() => this.Log("Dispose");

    [RelayCommand]
    Task PushDetail() => navigator.NavigateTo<DetailViewModel>(
        x => x.Text = "From Lifecycle"
    );

    [RelayCommand]
    void Clear() => Events.Clear();
}
