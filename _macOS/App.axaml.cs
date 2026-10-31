using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Cardex.Data;
using Cardex.Platform;
using Cardex.Services;
using Cardex.ViewModels;
using Cardex.Views;

namespace Cardex;

public partial class App : Application
{
    public static MainViewModel MainVm { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Resources["MagnifierCursor"] = MagnifierCursor.Create();

            var window = new MainWindow();
            desktop.MainWindow = window;
            Dispatcher.UIThread.Post(async () => await StartAsync(window));
        }

        base.OnFrameworkInitializationCompleted();
    }

    // ── Menu de l'application (App.axaml) ────────────────────────────────────

    private async void About_Click(object? sender, EventArgs e)
    {
        var version = typeof(App).Assembly.GetName().Version;
        await Dialogs.InfoAsync(
            $"Cardex {version?.ToString(2)}\nPokémon TCG Collection Companion",
            "About Cardex");
    }

    // ⌘, : ouvre les réglages, ou les ramène au premier plan s'ils sont déjà ouverts
    // (le bouton ⚙ de l'en-tête, lui, ouvre/ferme comme côté WPF).
    private void Settings_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.Windows.OfType<SettingsWindow>().FirstOrDefault() is { } open)
            open.Activate();
        else if (MainVm is not null)
            MainVm.ToggleSettingsCommand.Execute(null);
    }

    // Même séquence que App.OnStartup côté WPF : schéma → ViewModel → fenêtre → catalogue → synchro.
    private static async Task StartAsync(MainWindow window)
    {
        var db = new AppDbContext();
        await StartupService.MigrateAsync(db);

        var settings = AppSettings.Load();
        var tcgService = new PokemonTcgService(settings.ApiKey);
        var tcgdexService = new TCGdexService();
        var imageCache = new ImageCacheService();
        var cardexApiService = new CardexApiService(StartupService.LoadCardexApiKey());

        MainVm = new MainViewModel(tcgService, tcgdexService, imageCache, db, cardexApiService)
        {
            AppLogo = StartupService.LoadResource("Cardex.Logo.png"),
            AppName = StartupService.LoadCroppedResource("Cardex.Name.png"),
        };
        window.DataContext = MainVm;

        await StartupService.SeedAndSyncAsync(db, settings, tcgdexService, cardexApiService);
        await MainVm.LoadSetsAsync();
        await MainVm.CheckPendingSyncAsync();
        _ = MainVm.CheckForUpdateAsync();
    }
}
