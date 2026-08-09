using System.Globalization;
using System.Windows.Data;

namespace Cardex.Converters;

public class RatioWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not int count
            || values[1] is not double actualWidth
            || actualWidth <= 0)
            return 0.0;

        return Math.Clamp(actualWidth * count / 60.0, 0, actualWidth);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
