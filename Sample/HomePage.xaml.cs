namespace Sample;

public partial class HomePage : ContentPage
{
    public HomePage() => this.InitializeComponent();

    void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is DemoItem item)
        {
            item.Action?.Invoke();
            ((CollectionView)sender!).SelectedItem = null;
        }
    }
}
