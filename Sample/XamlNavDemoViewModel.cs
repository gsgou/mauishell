using Shiny;

namespace Sample;

[ShellMap<XamlNavDemoPage>]
public partial class XamlNavDemoViewModel : ObservableObject
{
    [ObservableProperty] string navArg = "From XAML";
}
