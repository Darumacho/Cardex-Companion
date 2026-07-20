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
        // Migration manuelle pour les DB existantes (ajout de la colonne Quantity)
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE OwnedCards ADD COLUMN Quantity INTEGER NOT NULL DEFAULT 1"); }
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

