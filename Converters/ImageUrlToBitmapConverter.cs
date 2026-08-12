using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Cardex.Converters;

// Builds a fresh BitmapImage from a URL string on every resolve.
// Binding Image.Source directly to a nested <BitmapImage UriSource="{Binding ...}"/> element
// is unreliable in virtualized/recycled containers (ListView/ItemsControl recycling) : the
// BitmapImage instance is reused across recycled containers and its UriSource property update
// doesn't always trigger a redecode, so the old image can stick around on the new data item.
// Returning a brand-new object here forces WPF to swap the image whenever the source changes.
//
// IMPORTANT: keep loading asynchronous (default CacheOption, no Freeze()). Using
// BitmapCacheOption.OnLoad forces a *synchronous* download on the calling (UI) thread during
// EndInit(), and any network failure then throws synchronously out of the converter, crashing
// the app — this hit hardest exactly where many images load at once (search results, deck load).
public class ImageUrlToBitmapConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(url, UriKind.RelativeOrAbsolute);
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            if (parameter is string ps && int.TryParse(ps, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width))
                bitmap.DecodePixelWidth = width;
            bitmap.EndInit();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
