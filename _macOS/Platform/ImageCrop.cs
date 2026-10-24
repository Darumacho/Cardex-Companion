using System.IO;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Cardex.Platform;

// Recadrage sur la zone non transparente d'une image (remplace FormatConvertedBitmap +
// CroppedBitmap du WPF). Utilisé pour le logo « Cardex » du bandeau (StartupService) et pour le
// sprite PokeAPI des stats (MainViewModel.CropTransparentPadding).
public static class ImageCrop
{
    // Pixels dont l'alpha est ≤ 10 considérés comme transparents (même seuil que le WPF).
    private const byte AlphaThreshold = 10;

    public static Bitmap? CropTransparentPadding(Stream encodedImage)
    {
        using var source = SKBitmap.Decode(encodedImage);
        return source is null ? null : CropTransparentPadding(source);
    }

    public static Bitmap CropTransparentPadding(Bitmap image)
    {
        using var png = new MemoryStream();
        image.Save(png, new PngBitmapEncoderOptions());
        png.Position = 0;
        return CropTransparentPadding(png) ?? image;
    }

    private static Bitmap? CropTransparentPadding(SKBitmap source)
    {
        int w = source.Width, h = source.Height;
        int left = w, right = -1, top = h, bottom = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (source.GetPixel(x, y).Alpha > AlphaThreshold)
                {
                    if (x < left)   left   = x;
                    if (x > right)  right  = x;
                    if (y < top)    top    = y;
                    if (y > bottom) bottom = y;
                }

        // Entièrement transparente : on garde l'image d'origine.
        var area = right < left || bottom < top
            ? new SKRectI(0, 0, w, h)
            : new SKRectI(left, top, right + 1, bottom + 1);

        using var cropped = new SKBitmap();
        if (!source.ExtractSubset(cropped, area)) return null;

        using var image = SKImage.FromBitmap(cropped);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream(encoded.ToArray());
        return new Bitmap(ms);
    }
}
