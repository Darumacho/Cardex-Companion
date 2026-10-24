using System.IO;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Cardex.Platform;

// Remplace Converters/ImageUrlToBitmapConverter.cs (WPF) : un converter Avalonia ne peut pas
// télécharger une image sans bloquer l'UI. Utilisation :
//   <Image p:RemoteImage.DecodeWidth="300" p:RemoteImage.Url="{Binding ImageUrl}"/>
// (WPF : Source="{Binding ImageUrl, Converter={StaticResource ImageUrlToBitmap}, ConverterParameter=300}")
// Comme côté WPF, pas de cache : chaque changement d'URL redemande l'image. Si l'URL change
// pendant le téléchargement (conteneur recyclé), le résultat obsolète est ignoré.
public static class RemoteImage
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static readonly AttachedProperty<string?> UrlProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("Url", typeof(RemoteImage));

    // Largeur de décodage en pixels (0 = taille d'origine).
    public static readonly AttachedProperty<int> DecodeWidthProperty =
        AvaloniaProperty.RegisterAttached<Image, int>("DecodeWidth", typeof(RemoteImage));

    public static string? GetUrl(Image image) => image.GetValue(UrlProperty);
    public static void SetUrl(Image image, string? value) => image.SetValue(UrlProperty, value);
    public static int GetDecodeWidth(Image image) => image.GetValue(DecodeWidthProperty);
    public static void SetDecodeWidth(Image image, int value) => image.SetValue(DecodeWidthProperty, value);

    static RemoteImage()
    {
        UrlProperty.Changed.AddClassHandler<Image>(OnUrlChanged);
    }

    private static async void OnUrlChanged(Image image, AvaloniaPropertyChangedEventArgs e)
    {
        image.Source = null;
        if (e.NewValue is not string url || string.IsNullOrWhiteSpace(url)) return;

        // Laisse le XAML finir d'affecter les autres attributs (DecodeWidth) avant de lire.
        await Task.Yield();
        var bitmap = await LoadAsync(url, GetDecodeWidth(image));
        if (GetUrl(image) == url)
            image.Source = bitmap;
    }

    private static async Task<Bitmap?> LoadAsync(string url, int decodeWidth)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(url);
            return await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes);
                return decodeWidth > 0 ? Bitmap.DecodeToWidth(stream, decodeWidth) : new Bitmap(stream);
            });
        }
        catch
        {
            return null;
        }
    }
}
