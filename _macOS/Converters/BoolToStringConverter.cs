using System.Globalization;
using Avalonia.Data.Converters;

namespace Cardex.Converters;

// Remplace les DataTrigger WPF qui changent un texte selon un booléen.
// ConverterParameter = "texte si vrai|texte si faux", ex. "▼|►".
// Une valeur non booléenne est considérée vraie si elle est non nulle / non zéro.
public class BoolToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string p) return null;
        var parts = p.Split('|');
        var isTrue = value switch
        {
            bool b => b,
            int i  => i != 0,
            null   => false,
            _      => true,
        };
        return isTrue ? parts[0] : parts.Length > 1 ? parts[1] : "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
