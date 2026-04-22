using Shiny;

namespace Sample;

[ShellMap<BuilderDemoPage>]
public partial class BuilderDemoViewModel(INavigator navigator) : ObservableObject
{
    [RelayCommand]
    Task ChainThree() => navigator
        .CreateBuilder()
        .Add<DetailViewModel>(x => x.Text = "Chain Step 1")
        .Add<DetailViewModel>(x => x.Text = "Chain Step 2")
        .Add<DetailViewModel>(x => x.Text = "Chain Step 3 (final)")
        .Navigate();

    [RelayCommand]
    Task ChainWithModal() => navigator
        .CreateBuilder()
        .Add<DetailViewModel>(x => x.Text = "Before Modal")
        .Add<ModalDemoViewModel>(x => x.Title = "Chained Modal")
        .Navigate();

    [RelayCommand]
    Task PopAndPush() => navigator
        .CreateBuilder()
        .PopBack()
        .Add<DetailViewModel>(x => x.Text = "Replaced via Pop+Push")
        .Navigate();
}
