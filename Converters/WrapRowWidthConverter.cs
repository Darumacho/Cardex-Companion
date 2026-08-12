using System.Globalization;
using System.Windows.Data;

namespace Cardex.Converters;

// Rounds a container width down to the nearest multiple of the item slot width
// (passed as ConverterParameter), so a centered WrapPanel always shows full rows
// with equal left/right margins instead of a ragged edge on the last column.
public class WrapRowWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double containerWidth || containerWidth <= 0)
            return double.NaN;
        if (parameter is not string ps
            || !double.TryParse(ps, NumberStyles.Any, CultureInfo.InvariantCulture, out var itemWidth)
            || itemWidth <= 0)
            return double.NaN;

        int columns = Math.Max(1, (int)(containerWidth / itemWidth));
        return columns * itemWidth;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
