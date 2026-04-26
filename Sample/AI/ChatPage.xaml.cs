using System.Globalization;

namespace Sample.AI;

public partial class ChatPage : ContentPage
{
    public ChatPage()
    {
        Resources.Add("IsNotNullConverter", new IsNotNullConverter());
        Resources.Add("InvertedBoolConverter", new InvertedBoolConverter());

        this.InitializeComponent();
    }
}

public class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
