using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NickeltownFinance.Converters;

public class ConfidenceToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var confidence = value switch
        {
            byte b => b,
            int i => (byte)Math.Clamp(i, 0, 100),
            _ => (byte?)null
        };

        if (confidence is null)
            return Brushes.Gray;

        if (confidence >= 95)
            return new SolidColorBrush(Color.FromRgb(34, 197, 94));
        if (confidence >= 80)
            return new SolidColorBrush(Color.FromRgb(234, 179, 8));
        return new SolidColorBrush(Color.FromRgb(239, 68, 68));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class ConfidenceDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            byte b => $"{b}%",
            int i => $"{i}%",
            _ => "—"
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class MatchQualityBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is byte confidence && confidence >= 97)
            return new SolidColorBrush(Color.FromRgb(34, 197, 94));
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
