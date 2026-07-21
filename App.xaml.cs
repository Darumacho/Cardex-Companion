using Cardex.Data;
using Cardex.Services;
using Cardex.ViewModels;
using Cardex.Views;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Cardex;

public partial class App : Application
{
    public static MainViewModel MainVm { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var db = new AppDbContext();
        await db.Database.EnsureCreatedAsync();
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE OwnedCards ADD COLUMN Quantity INTEGER NOT NULL DEFAULT 1"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS CachedSets (
                SetId TEXT PRIMARY KEY NOT NULL, Name TEXT NOT NULL DEFAULT '',
                Series TEXT NOT NULL DEFAULT '', Total INTEGER NOT NULL DEFAULT 0,
                ReleaseDate TEXT NOT NULL DEFAULT '', LogoUrl TEXT NOT NULL DEFAULT '',
                SymbolUrl TEXT NOT NULL DEFAULT '', CachedAt TEXT NOT NULL DEFAULT '')"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS CachedCards (
                CardId TEXT PRIMARY KEY NOT NULL, SetId TEXT NOT NULL DEFAULT '',
                Name TEXT NOT NULL DEFAULT '', Number TEXT NOT NULL DEFAULT '',
                ImageSmall TEXT NOT NULL DEFAULT '', Rarity TEXT,
                SortOrder INTEGER NOT NULL DEFAULT 0)"); }
        catch { }

        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS WantedCards (
                CardId TEXT PRIMARY KEY NOT NULL, SetId TEXT NOT NULL DEFAULT '')"); }
        catch { }
        try { await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS FavoriteSets (
                SetId TEXT PRIMARY KEY NOT NULL)"); }
        catch { }

        var tcgService = new PokemonTcgService();
        var imageCache = new ImageCacheService();

        MainVm = new MainViewModel(tcgService, imageCache, db);

        var asm = Assembly.GetExecutingAssembly();

        using (var stream = asm.GetManifestResourceStream("Cardex.Logo.png"))
            if (stream != null)
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = stream;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                MainVm.AppLogo = bmp;
            }

        using (var stream = asm.GetManifestResourceStream("Cardex.Name.png"))
            if (stream != null)
                MainVm.AppName = LoadAndCrop(stream);

        var window = new MainWindow { DataContext = MainVm };

        var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo.ico");
        if (System.IO.File.Exists(iconPath))
            window.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri(iconPath, UriKind.Absolute),
                System.Windows.Media.Imaging.BitmapCreateOptions.None,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);

        window.Show();

        await MainVm.LoadSetsAsync();
    }

    private static ImageSource LoadAndCrop(Stream stream)
    {
        var source = new BitmapImage();
        source.BeginInit();
        source.StreamSource = stream;
        source.CacheOption = BitmapCacheOption.OnLoad;
        source.EndInit();
        source.Freeze();

        var fmt = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = fmt.PixelWidth, h = fmt.PixelHeight, stride = w * 4;
        var pixels = new byte[h * stride];
        fmt.CopyPixels(pixels, stride, 0);

        int left = w, right = 0, top = h, bottom = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (pixels[y * stride + x * 4 + 3] > 10)
                {
                    if (x < left)   left   = x;
                    if (x > right)  right  = x;
                    if (y < top)    top    = y;
                    if (y > bottom) bottom = y;
                }

        if (left >= right || top >= bottom) return source;

        var cropped = new CroppedBitmap(source,
            new System.Windows.Int32Rect(left, top, right - left + 1, bottom - top + 1));
        cropped.Freeze();
        return cropped;
    }
}

