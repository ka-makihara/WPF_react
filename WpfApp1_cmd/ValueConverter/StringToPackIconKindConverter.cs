
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

public class StringToPackIconKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && Enum.TryParse(typeof(PackIconKind), s, out var kind))
            return kind;
        return PackIconKind.HelpCircleOutline;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString();
}
