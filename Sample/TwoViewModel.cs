using Shiny;

namespace Sample;

[ShellMap<TwoPage>]
public partial class TwoViewModel(INavigator navigator) : ObservableObject
{
    [ShellProperty(true)]
    public string Text { get; set; }

    [RelayCommand]
    Task PopAndPush() => navigator
        .CreateBuilder()
        .PopBack(2)
        .Add<OneViewModel>(x => x.Text = "Popped Back 2 + Pushed")
        .Navigate();

    [RelayCommand]
    Task GoBack() => navigator.GoBack();
}
