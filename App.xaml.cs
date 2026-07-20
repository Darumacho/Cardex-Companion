using Cardex.Data;
using Cardex.Services;
using Cardex.ViewModels;
using Cardex.Views;
using Microsoft.EntityFrameworkCore;
using System.Windows;

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

        var tcgService = new PokemonTcgService();
        var imageCache = new ImageCacheService();

        MainVm = new MainViewModel(tcgService, imageCache, db);

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
}

