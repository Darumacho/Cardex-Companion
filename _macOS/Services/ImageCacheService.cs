using System.IO;
using System.Net.Http;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Svg.Skia;

namespace Cardex.Services;

// Version Avalonia de Services/ImageCacheService.cs (WPF) : même cache disque, même nommage des
// fichiers ; Bitmap Avalonia au lieu de BitmapImage, et Svg.Skia au lieu de SharpVectors pour
// rasteriser les SVG.
public class ImageCacheService
{
    private readonly HttpClient _http = new();
    private readonly string _cacheDir;

    public ImageCacheService()
    {
        _cacheDir = Path.Combine(AppPaths.DataFolder, "ImageCache");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<Bitmap?> GetImageAsync(string url, string cardId)
    {
        var filePath = await EnsureCachedFileAsync(url, cardId);
        return filePath is null ? null : await LoadBitmapAsync(filePath, 200);
    }

    public async Task<Bitmap?> GetFullResImageAsync(string url, string cardId)
    {
        var filePath = await EnsureCachedFileAsync(url, cardId);
        return filePath is null ? null : await LoadBitmapAsync(filePath, null);
    }

    private async Task<string?> EnsureCachedFileAsync(string url, string cardId)
    {
        if (string.IsNullOrEmpty(url)) return null;

        var ext = Path.GetExtension(url).Split('?')[0];
        if (string.IsNullOrEmpty(ext)) ext = ".png";
        // Le SVG n'est pas décodable par Bitmap : on le rasterise en PNG avant mise en cache.
        var isSvg = ext.Equals(".svg", StringComparison.OrdinalIgnoreCase);
        if (isSvg) ext = ".png";

        var safeName = string.Concat(cardId.Split(Path.GetInvalidFileNameChars())) + ext;
        var filePath = Path.Combine(_cacheDir, safeName);

        if (!File.Exists(filePath))
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                if (isSvg) bytes = RasterizeSvgToPng(bytes, 256);
                await File.WriteAllBytesAsync(filePath, bytes);
            }
            catch
            {
                return null;
            }
        }

        return filePath;
    }

    private static byte[] RasterizeSvgToPng(byte[] svgBytes, int pixelWidth)
    {
        using var svg = new SKSvg();
        using var svgStream = new MemoryStream(svgBytes);
        var picture = svg.Load(svgStream) ?? throw new InvalidOperationException("SVG invalide");

        var bounds = picture.CullRect;
        var scale = pixelWidth / bounds.Width;
        var pixelHeight = Math.Max(1, (int)Math.Round(bounds.Height * scale));

        using var bitmap = new SKBitmap(pixelWidth, pixelHeight);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(picture);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static Task<Bitmap?> LoadBitmapAsync(string filePath, int? decodePixelWidth)
    {
        return Task.Run(() =>
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                return decodePixelWidth.HasValue
                    ? Bitmap.DecodeToWidth(stream, decodePixelWidth.Value)
                    : new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        });
    }
}
