using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace Cardex.Services;

public class ImageCacheService
{
    private readonly HttpClient _http = new();
    private readonly string _cacheDir;

    public ImageCacheService()
    {
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cardex", "ImageCache");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<BitmapImage?> GetImageAsync(string url, string cardId)
    {
        var filePath = await EnsureCachedFileAsync(url, cardId);
        return filePath is null ? null : LoadBitmap(filePath, 200);
    }

    public async Task<BitmapImage?> GetFullResImageAsync(string url, string cardId)
    {
        var filePath = await EnsureCachedFileAsync(url, cardId);
        return filePath is null ? null : LoadBitmap(filePath, null);
    }

    private async Task<string?> EnsureCachedFileAsync(string url, string cardId)
    {
        if (string.IsNullOrEmpty(url)) return null;

        var ext = Path.GetExtension(url).Split('?')[0];
        if (string.IsNullOrEmpty(ext)) ext = ".png";
        // Le SVG n'est pas décodable par BitmapImage : on le rasterise en PNG avant mise en cache.
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
        var reader = new FileSvgReader(new WpfDrawingSettings(), false);
        using var svgStream = new MemoryStream(svgBytes);
        var drawing = reader.Read(svgStream) ?? throw new InvalidOperationException("SVG invalide");

        var bounds = drawing.Bounds;
        var scale = pixelWidth / bounds.Width;
        var pixelHeight = Math.Max(1, (int)Math.Round(bounds.Height * scale));

        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.PushTransform(new ScaleTransform(scale, scale));
            ctx.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
            ctx.DrawDrawing(drawing);
        }

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var outStream = new MemoryStream();
        encoder.Save(outStream);
        return outStream.ToArray();
    }

    private static BitmapImage? LoadBitmap(string filePath, int? decodePixelWidth)
    {
        return Task.Run(() =>
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(filePath, UriKind.Absolute);
            if (decodePixelWidth.HasValue)
                bmp.DecodePixelWidth = decodePixelWidth.Value;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }).GetAwaiter().GetResult();
    }
}
