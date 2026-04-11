using Microsoft.Extensions.Logging;
using Shiny;

namespace Sample;

[ShellMap<AnotherPage>("another")]
public partial class AnotherViewModel(
    ILogger<AnotherViewModel> logger,
    INavigator navigator,
    IDialogs dialogs
) : ObservableObject, IQueryAttributable, IPageLifecycleAware, INavigationConfirmation, INavigationAware, IDisposable
{
    [ObservableProperty] string backArg;
    [ObservableProperty] string arg;
    [ObservableProperty] bool isNavFromViewModel;
    
    [RelayCommand] Task GoBack() => navigator.GoBack(("ToTheBack", this.BackArg));
    [RelayCommand] Task ResetToRoot() => navigator.NavigateTo<MainViewModel>(x => x.BackArg = "RESET TO ROOT", relativeNavigation: false);
    [RelayCommand] Task PushAnother() => navigator.NavigateTo("another", args: [("Arg", "Pushing Another")]);
    [RelayCommand] Task PopToRoot() => navigator.PopToRoot(("ToTheBack", "POPPED TO ROOT"));

    [ObservableProperty] string dialogResult;
    
    [RelayCommand]
    async Task ShowConfirm()
    {
        var result = await dialogs.Confirm("Confirm", "Do you want to proceed?", "Yes", "No");
        this.DialogResult = $"Confirm: {result}";
    }
    
    [RelayCommand]
    async Task ShowPrompt()
    {
        var result = await dialogs.Prompt(
            "Prompt",
            "Enter your name",
            placeholder: "Your name here",
            maxLength: 50
        );
        this.DialogResult = result != null ? $"Prompt: {result}" : "Prompt: (cancelled)";
    }
    
    [RelayCommand]
    async Task ShowActionSheet()
    {
        var result = await dialogs.ActionSheet("Choose an Option", "Cancel", "Delete", "Edit", "Share", "Copy");
        this.DialogResult = $"ActionSheet: {result}";
    }
    
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        logger.LogInformation("AnotherViewModel ApplyQueryAttributes");
        if (query.TryGetValue("Arg", out var value))
            this.Arg = (string)value;
    }

    public void Dispose() => logger.LogInformation("AnotherViewModel Dispose");
    public void OnAppearing() => logger.LogInformation("AnotherViewModel OnAppearing");
    public void OnDisappearing() => logger.LogInformation("AnotherViewModel OnDisappearing");

    public Task<bool> CanNavigate() => dialogs.Confirm(
        "Confirm", 
        "Are you sure you want to navigate?", 
        "Yes", 
        "No"
    );
    
    public void OnNavigatingFrom(IDictionary<string, object> parameters)
    {
        if (!parameters.ContainsKey("ToTheBack"))
        {
            parameters["ToTheBack"] = "SET BY OnNavigatingFrom"; // mutate/change parameters before actually leaving
        }
        logger.LogInformation("OnNavigatingFrom AnotherViewModel");
    }
}