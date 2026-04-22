using Microsoft.Extensions.Logging;
using Shiny;

namespace Sample;

[ShellMap<ModalDemoPage>("modal")]
public partial class ModalDemoViewModel(
    ILogger<ModalDemoViewModel> logger,
    INavigator navigator
) : ObservableObject, IPageLifecycleAware, INavigationAware, IDisposable
{
    [ShellProperty(true)]
    public string Title { get; set; } = "Modal Page";

    [ShellProperty(false)]
    public string OptionalNote { get; set; } = "(none)";

    [RelayCommand]
    Task PushWithinModal() => navigator.NavigateTo<DetailViewModel>(
        x => x.Text = "Inside Modal"
    );

    [RelayCommand]
    Task Close() => navigator.GoBack();

    public void OnNavigatingFrom(IDictionary<string, object> parameters)
        => logger.LogDebug("ModalDemoViewModel.OnNavigatingFrom");

    public void OnAppearing() => logger.LogDebug("ModalDemoViewModel.OnAppearing");
    public void OnDisappearing() => logger.LogDebug("ModalDemoViewModel.OnDisappearing");
    public void Dispose() => logger.LogDebug("ModalDemoViewModel.Dispose");
}
