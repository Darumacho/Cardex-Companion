using System.Globalization;
using Avalonia.Data.Converters;

namespace Cardex.Converters;

// values[0] : nombre de cartes, values[1] : largeur disponible (Bounds.Width côté Avalonia,
// ActualWidth côté WPF) → largeur proportionnelle sur 60 cartes.
public class RatioWidthConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2
            || values[0] is not int count
            || values[1] is not double actualWidth
            || actualWidth <= 0)
            return 0.0;

        return Math.Clamp(actualWidth * count / 60.0, 0, actualWidth);
    }
}
