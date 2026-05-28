using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MyHobbyWarehouse.Models;

namespace MyHobbyWarehouse.Converters;

// Colors aligned with Styles.xaml palette
file static class Pal
{
    public static readonly Color Ok     = Color.FromRgb(0x4E, 0xC9, 0x94);
    public static readonly Color Warn   = Color.FromRgb(0xFF, 0xB3, 0x47);
    public static readonly Color Danger = Color.FromRgb(0xF4, 0x71, 0x74);
    public static readonly Color Sub    = Color.FromRgb(0x85, 0x85, 0x85);
}

public class StockStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is StockStatus s) return s switch
        {
            StockStatus.Ok   => new SolidColorBrush(Pal.Ok),
            StockStatus.Low  => new SolidColorBrush(Pal.Warn),
            StockStatus.Out  => new SolidColorBrush(Pal.Danger),
            _                => new SolidColorBrush(Pal.Sub),
        };
        return Brushes.Transparent;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
}

public class StockToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is double d && d <= 0
            ? new SolidColorBrush(Pal.Danger)
            : Brushes.Transparent;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
}

public class PositiveQtyToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is double d
            ? d > 0 ? new SolidColorBrush(Pal.Ok) : new SolidColorBrush(Pal.Danger)
            : (object)Brushes.Gray;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v == null ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
}

public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v == null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
}
