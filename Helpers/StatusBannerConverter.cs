using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NexusOptimizer.Helpers;

/// <summary>Returns the banner background or border color for a status message.</summary>
public class StatusBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hex = StatusBanner.Kind(value as string) switch
        {
            "Success" => "#14291F",
            "Warning" => "#2E2612",
            "Error" => "#2E1517",
            _ => "#171321"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StatusBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hex = StatusBanner.Kind(value as string) switch
        {
            "Success" => "#30A46C",
            "Warning" => "#E0A93B",
            "Error" => "#E5484D",
            _ => "#2A2438"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}