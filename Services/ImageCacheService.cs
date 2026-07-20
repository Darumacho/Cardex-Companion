using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

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
        if (string.IsNullOrEmpty(url)) return null;

        var ext = Path.GetExtension(url).Split('?')[0];
        if (string.IsNullOrEmpty(ext)) ext = ".png";
        var safeName = string.Concat(cardId.Split(Path.GetInvalidFileNameChars())) + ext;
        var filePath = Path.Combine(_cacheDir, safeName);

        if (!File.Exists(filePath))
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(filePath, bytes);
            }
            catch
            {
                return null;
            }
        }

        return await Task.Run(() =>
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(filePath, UriKind.Absolute);
            bmp.DecodePixelWidth = 200;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        });
    }
}
