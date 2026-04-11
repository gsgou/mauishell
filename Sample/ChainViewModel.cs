using Shiny;

namespace Sample;

[ShellMap<ChainPage>]
public partial class ChainViewModel(INavigator navigator) : ObservableObject
{
    static int counter;

    [ShellProperty(true)]
    public string Text { get; set; }

    [ObservableProperty]
    string title = "Chain";

    [RelayCommand]
    Task PushChain() => navigator
        .CreateBuilder()
        .AddChain("Page1")
        .Add<AnotherViewModel>(x => x.Arg = "Mid-Chain")
        .AddChain("Page3")
        .Navigate();

    [RelayCommand]
    Task PopAndPush() => navigator
        .CreateBuilder()
        .PopBack(2)
        .AddChain("Popped Back 2 + Pushed")
        .Navigate();

    [RelayCommand]
    Task RootChain() => navigator
        .CreateBuilder(fromRoot: true)
        .AddChain("Root Reset")
        .Navigate();

    [RelayCommand]
    Task GoBack() => navigator.GoBack();
}
