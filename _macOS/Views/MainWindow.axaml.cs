using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Transformation;
using Avalonia.Platform;
using Avalonia.Threading;
using Cardex.Platform;
using Cardex.Services;
using Cardex.ViewModels;

namespace Cardex.Views;

public partial class MainWindow : Window
{
    private DispatcherTimer? _toastTimer;

    private static readonly Key[] _konamiSequence =
        [Key.Up, Key.Up, Key.Down, Key.Down, Key.Left, Key.Right, Key.Left, Key.Right];
    private int _konamiIndex;

    private static readonly Key[] _briggsSequence =
        [Key.B, Key.R, Key.I, Key.G, Key.G, Key.S];
    private int _briggsIndex;

    public MainWindow()
    {
        InitializeComponent();
        AchievementService.Unlocked += def =>
            Dispatcher.UIThread.Post(() => ShowAchievementToast(def));
        // Équivalent du PreviewKeyDown WPF : phase tunnel, avant les contrôles enfants.
        AddHandler(KeyDownEvent, Window_PreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void ShowAchievementToast(AchievementDef def)
    {
        ToastName.Text  = def.Name;
        ToastDesc.Text  = def.Description;

        if (def.Icon is not null)
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri($"avares://Cardex/Assets/AchievementIcons/{def.Icon}"));
                ToastIcon.Source      = Bitmap.DecodeToWidth(stream, 32);
                ToastIcon.IsVisible   = true;
                ToastEmoji.IsVisible  = false;
            }
            catch { ToastIcon.IsVisible = false; ToastEmoji.IsVisible = true; }
        }
        else
        {
            ToastEmoji.Text       = def.Emoji;
            ToastIcon.IsVisible   = false;
            ToastEmoji.IsVisible  = true;
        }

        string? soundKey = def.CustomSound;
        if (soundKey is null && DataContext is MainViewModel vm && vm.AchievementSound is "xbox" or "ps4")
            soundKey = vm.AchievementSound;
        if (soundKey is not null)
            SoundPlayer.Play(soundKey);

        SlideToast(visible: true);

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer!.Stop();
            SlideToast(visible: false);
        };
        _toastTimer.Start();
    }

    // Glissement du toast : entrée 350 ms ease-out, sortie 300 ms ease-in (comme le WPF).
    private void SlideToast(bool visible)
    {
        AchievementToast.Transitions =
        [
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(visible ? 350 : 300),
                Easing   = visible ? new CubicEaseOut() : new CubicEaseIn(),
            }
        ];
        AchievementToast.RenderTransform = TransformOperations.Parse(visible ? "translateX(0px)" : "translateX(320px)");
    }

    // Charge les images au fur et à mesure que les cartes deviennent visibles
    private async void CardsScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        => await LoadVisibleCardImagesAsync(20);

    private async void CardsItemsControl_Loaded(object? sender, RoutedEventArgs e)
        => await LoadVisibleCardImagesAsync(20);

    private async void BinderScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        => await LoadVisibleCardImagesAsync(60);

    private async void BinderItemsControl_Loaded(object? sender, RoutedEventArgs e)
        => await LoadVisibleCardImagesAsync(60);

    public void FocusSearch()
    {
        Activate();
        if (DataContext is MainViewModel vm && vm.SelectedSet is not null)
        {
            SetSearchBox.Focus();
            SetSearchBox.SelectAll();
        }
        else
        {
            GlobalSearchBox.Focus();
            GlobalSearchBox.SelectAll();
        }
    }

    private void Window_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var noModifiers = e.KeyModifiers == KeyModifiers.None;

        if (e.Key == _konamiSequence[_konamiIndex] && noModifiers)
        {
            _konamiIndex++;
            if (_konamiIndex == _konamiSequence.Length)
            {
                _konamiIndex = 0;
                SoundPlayer.Play("Outstanding.mp3");
                if (DataContext is MainViewModel konamiVm)
                    _ = konamiVm.UnlockKonamiCodeCommand.ExecuteAsync(null);
            }
        }
        else
        {
            _konamiIndex = e.Key == _konamiSequence[0] && noModifiers ? 1 : 0;
        }

        if (e.Key == _briggsSequence[_briggsIndex] && noModifiers)
        {
            _briggsIndex++;
            if (_briggsIndex == _briggsSequence.Length)
            {
                _briggsIndex = 0;
                SoundPlayer.Play("Briggs.mp3");
                if (DataContext is MainViewModel briggsVm)
                    _ = briggsVm.UnlockBriggsCommand.ExecuteAsync(null);
            }
        }
        else
        {
            _briggsIndex = e.Key == _briggsSequence[0] && noModifiers ? 1 : 0;
        }

        if (DataContext is not MainViewModel vm) return;

        // ⌘F sur macOS (Ctrl+F côté Windows)
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Meta)
        {
            FocusSearch();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(vm.GlobalSearch))
                vm.GlobalSearch = "";
            else if (vm.SelectedSet is not null)
                vm.GoHomeCommand.Execute(null);
            e.Handled = true;
        }
    }

    // Vue classeur : clic = possédée, ⇧+clic = voulue, ⌘+clic = exclue (Ctrl+clic côté Windows ;
    // sur Mac, Ctrl+clic est le clic droit).
    private void BinderCard_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: CardViewModel card }) return;

        if (e.KeyModifiers == KeyModifiers.Shift)
            card.ToggleWantedCommand.Execute(null);
        else if (e.KeyModifiers == KeyModifiers.Meta)
            card.ToggleExcludedCommand.Execute(null);
        else if (e.KeyModifiers == KeyModifiers.None)
            card.ToggleOwnedCommand.Execute(null);
    }

    private void RarityFilter_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is string rarity
            && DataContext is MainViewModel vm && vm.SelectedSet is not null)
            vm.SelectedSet.SelectedRarity = rarity;
    }

    // ── Barre de menus macOS (NativeMenu dans MainWindow.axaml) ─────────────

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void MenuBackup_Click(object? sender, EventArgs e)          => Vm?.BackupCollectionCommand.Execute(null);
    private void MenuRestore_Click(object? sender, EventArgs e)         => Vm?.RestoreCollectionCommand.Execute(null);
    private void MenuImport_Click(object? sender, EventArgs e)          => Vm?.ImportCollectionCommand.Execute(null);
    private void MenuImportTemplate_Click(object? sender, EventArgs e)  => Vm?.GenerateImportTemplateCommand.Execute(null);
    private void MenuExportCsv_Click(object? sender, EventArgs e)       => Vm?.ExportCsvCommand.Execute(null);
    private void MenuSync_Click(object? sender, EventArgs e)            => Vm?.SyncNowCommand.Execute(null);
    private void MenuRefreshDatabase_Click(object? sender, EventArgs e) => Vm?.RefreshSetsCommand.Execute(null);

    // Les actions de deck basculent d'abord sur l'onglet Deck Builder pour que le résultat soit visible.
    private DeckBuilderViewModel? ShowDeckBuilder()
    {
        if (Vm is not { } vm) return null;
        if (!vm.IsDeckBuilderTabActive) vm.SwitchToDeckBuilderCommand.Execute(null);
        return vm.DeckBuilder;
    }

    private void MenuNewDeck_Click(object? sender, EventArgs e)        => ShowDeckBuilder()?.NewDeckCommand.Execute(null);
    private void MenuSaveDeck_Click(object? sender, EventArgs e)       => ShowDeckBuilder()?.SaveDeckCommand.Execute(null);
    private void MenuImportDeckFile_Click(object? sender, EventArgs e) => ShowDeckBuilder()?.ImportPtcgoDeckCommand.Execute(null);
    private void MenuPasteDeck_Click(object? sender, EventArgs e)      => ShowDeckBuilder()?.ImportPtcgoTextCommand.Execute(null);
    private void MenuExportDeck_Click(object? sender, EventArgs e)     => ShowDeckBuilder()?.ExportDeckAsPtcgoCommand.Execute(null);

    private void MenuHome_Click(object? sender, EventArgs e)          => Vm?.GoHomeCommand.Execute(null);
    private void MenuDeckBuilder_Click(object? sender, EventArgs e)   => ShowDeckBuilder();
    private void MenuToggleBinder_Click(object? sender, EventArgs e)  { if (Vm is { } vm) vm.IsBinderView = !vm.IsBinderView; }
    private void MenuFind_Click(object? sender, EventArgs e)          => FocusSearch();

    private void MenuShortcuts_Click(object? sender, EventArgs e)
    {
        if (Vm is not { } vm) return;
        var settings = OwnedWindows.OfType<SettingsWindow>().FirstOrDefault();
        if (settings is null)
        {
            vm.ToggleSettingsCommand.Execute(null);
            settings = OwnedWindows.OfType<SettingsWindow>().FirstOrDefault();
        }
        settings?.ShowShortcutsTab();
        settings?.Activate();
    }

    private void MenuApiWebsite_Click(object? sender, EventArgs e)     => Vm?.OpenApiWebsiteCommand.Execute(null);
    private void MenuMobileReleases_Click(object? sender, EventArgs e) => Vm?.OpenMobileAppReleasesCommand.Execute(null);

    private async Task LoadVisibleCardImagesAsync(int batch)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedSet is null) return;
        var tasks = vm.SelectedSet.Cards
            .Where(c => c.CardImage is null && !c.IsLoadingImage)
            .Take(batch)
            .Select(c => c.LoadImageAsync());
        await Task.WhenAll(tasks);
    }
}
