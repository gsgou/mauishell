using Shiny;

namespace Sample;

[ShellMap<OnePage>]
public partial class OneViewModel(INavigator navigator) : ObservableObject
{
    [ShellProperty(true)]
    public string Text { get; set; }

    [RelayCommand]
    Task PushChain() => navigator
        .CreateBuilder()
        .Add<OneViewModel>(x => x.Text = "Chained One")
        .Add<AnotherViewModel>(x => x.Arg = "Mid-Chain")
        .Add<TwoViewModel>(x => x.Text = "Chained Two")
        .Navigate();

    [RelayCommand]
    Task RootChain() => navigator.PopToRoot();

    [RelayCommand]
    Task GoBack() => navigator.GoBack();
}
