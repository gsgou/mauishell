using Microsoft.Extensions.Logging;
using Shiny;

namespace Sample;

[ShellMap<MainPage>(registerRoute: false)]
public partial class MainViewModel(
    ILogger<MainViewModel> logger,
    INavigator navigator
) : ObservableObject, IQueryAttributable, IPageLifecycleAware, INavigationAware
{
    [ObservableProperty] string navArg;

    [NotifyPropertyChangedFor(nameof(ShowBackArg))]
    [ObservableProperty]
    string? backArg;
    public bool ShowBackArg => !String.IsNullOrWhiteSpace(BackArg);

    public string[] ShellTypes { get; } = Enum.GetNames<ShellType>();

    string selectedShellType = (Preferences.Default.ContainsKey("ShellType")
        ? Preferences.Default.Get("ShellType", nameof(ShellType.Standard))
        : nameof(ShellType.Standard));

    public string SelectedShellType
    {
        get => selectedShellType;
        set
        {
            if (SetProperty(ref selectedShellType, value) && value != null)
            {
                var shellType = Enum.Parse<ShellType>(value);
                App.SetShellPreference(shellType);
                navigator.SwitchShell(App.CreateShell(shellType));
            }
        }
    }
    
    [RelayCommand] Task NavByUri() => navigator.NavigateTo("another", args: [("Arg", this.NavArg)]);
    [RelayCommand] Task NavToModal(string uri) => navigator.NavigateTo("modal");
    [RelayCommand]
    Task NavByViewModel() => navigator.NavigateTo<AnotherViewModel>(
        x => x.IsNavFromViewModel = true,
        args: [("Arg", this.NavArg)]
    );

    [RelayCommand] Task NavToOne() => navigator.NavigateTo<OneViewModel>(x => x.Text = "From Main");

    [RelayCommand]
    Task NavBuilderChain() => navigator
        .CreateBuilder()
        .Add<OneViewModel>(x => x.Text = "From Builder")
        .Add<AnotherViewModel>(x => x.Arg = "Mid-Chain")
        .Add<TwoViewModel>(x => x.Text = "End of Chain")
        .Navigate();
    
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.Count > 0)
        {
            var pair = query.First();
            this.BackArg = $"{pair.Key}={pair.Value}";
        }
    }

    public void OnNavigatingFrom(IDictionary<string, object> parameters)
    {
        logger.LogInformation("OnNavigatingFrom MainViewModel");
    }

    
    public void OnAppearing()
    {
        Console.WriteLine("MainViewModel Appearing");
    }

    
    public void OnDisappearing()
    {
        Console.WriteLine("MainViewModel Disappearing");
    }
}