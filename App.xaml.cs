using Cardex.Data;
using Cardex.Services;
using Cardex.ViewModels;
using Cardex.Views;
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

        var tcgService = new PokemonTcgService();
        var imageCache = new ImageCacheService();

        MainVm = new MainViewModel(tcgService, imageCache, db);

        var window = new MainWindow { DataContext = MainVm };
        window.Show();

        await MainVm.LoadSetsAsync();
    }
}

