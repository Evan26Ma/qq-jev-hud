using System.Globalization;
using System.Windows.Data;

namespace QQJevHud.Views;

/// <summary>Maps a 0..100 probability to the pixel width of a distribution bar.</summary>
public sealed class ProbabilityToWidthConverter : IValueConverter
{
    public double FullWidth { get; set; } = 56;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var probability = value switch
        {
            int integer => integer,
            double number => (int)Math.Round(number),
            _ => 0
        };
        return Math.Clamp(probability, 0, 100) / 100.0 * FullWidth;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
