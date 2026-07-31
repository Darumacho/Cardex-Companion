using Cardex.Services;
using Cardex.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Cardex.Views;

public partial class MainWindow : Window
{
    private DispatcherTimer? _toastTimer;
    private static readonly Dictionary<string, string> _soundPaths = new();

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
            Dispatcher.BeginInvoke(() => ShowAchievementToast(def));
    }

    private static string? GetSoundPath(string soundKey)
    {
        if (_soundPaths.TryGetValue(soundKey, out var cached)) return cached;

        var fileName = soundKey switch
        {
            "xbox" => "AchievementXbox.mp3",
            "ps4"  => "AchievementPS4.mp3",
            _      => soundKey
        };
        var packUri = new Uri($"pack://application:,,,/Assets/{fileName}");
        try
        {
            var info = Application.GetResourceStream(packUri);
            if (info is null) return null;
            var path = Path.Combine(Path.GetTempPath(), $"Cardex_{fileName}");
            using var fs = File.Create(path);
            info.Stream.CopyTo(fs);
            _soundPaths[soundKey] = path;
            return path;
        }
        catch { return null; }
    }

    private void ShowAchievementToast(AchievementDef def)
    {
        ToastName.Text  = def.Name;
        ToastDesc.Text  = def.Description;

        if (def.Icon is not null)
        {
            try
            {
                var info = Application.GetResourceStream(
                    new Uri($"pack://application:,,,/Assets/AchievementIcons/{def.Icon}"));
                if (info is not null)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = info.Stream;
                    bmp.CacheOption  = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    ToastIcon.Source     = bmp;
                    ToastIcon.Visibility = Visibility.Visible;
                    ToastEmoji.Visibility = Visibility.Collapsed;
                }
                else { ToastIcon.Visibility = Visibility.Collapsed; ToastEmoji.Visibility = Visibility.Visible; }
            }
            catch { ToastIcon.Visibility = Visibility.Collapsed; ToastEmoji.Visibility = Visibility.Visible; }
        }
        else
        {
            ToastEmoji.Text       = def.Emoji;
            ToastIcon.Visibility  = Visibility.Collapsed;
            ToastEmoji.Visibility = Visibility.Visible;
        }

        string? soundKey = def.CustomSound;
        if (soundKey is null && DataContext is MainViewModel vm && vm.AchievementSound is "xbox" or "ps4")
            soundKey = vm.AchievementSound;

        if (soundKey is not null)
        {
            var path = GetSoundPath(soundKey);
            if (path is not null)
            {
                ToastSound.Stop();
                ToastSound.Source = null;
                ToastSound.Source = new Uri(path);
            }
        }

        var slideIn = new DoubleAnimation(320, 0, new Duration(TimeSpan.FromMilliseconds(350)))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        ToastTranslate.BeginAnimation(TranslateTransform.XProperty, slideIn);

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer!.Stop();
            var slideOut = new DoubleAnimation(0, 320, new Duration(TimeSpan.FromMilliseconds(300)))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            ToastTranslate.BeginAnimation(TranslateTransform.XProperty, slideOut);
        };
        _toastTimer.Start();
    }

    // Charge les images au fur et à mesure que les cartes deviennent visibles
    private void ToastSound_MediaOpened(object sender, RoutedEventArgs e)
        => ToastSound.Play();

    private async void CardsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        => await LoadVisibleCardImagesAsync(20);

    private async void CardsItemsControl_Loaded(object sender, RoutedEventArgs e)
        => await LoadVisibleCardImagesAsync(20);

    private async void BinderScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        => await LoadVisibleCardImagesAsync(60);

    private async void BinderItemsControl_Loaded(object sender, RoutedEventArgs e)
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

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == _konamiSequence[_konamiIndex] && Keyboard.Modifiers == ModifierKeys.None)
        {
            _konamiIndex++;
            if (_konamiIndex == _konamiSequence.Length)
            {
                _konamiIndex = 0;
                var outstandingPath = GetSoundPath("Outstanding.mp3");
                if (outstandingPath is not null)
                {
                    ToastSound.Stop();
                    ToastSound.Source = null;
                    ToastSound.Source = new Uri(outstandingPath);
                }
                if (DataContext is MainViewModel konamiVm)
                    _ = konamiVm.UnlockKonamiCodeCommand.ExecuteAsync(null);
            }
        }
        else
        {
            _konamiIndex = e.Key == _konamiSequence[0] && Keyboard.Modifiers == ModifierKeys.None ? 1 : 0;
        }

        if (e.Key == _briggsSequence[_briggsIndex] && Keyboard.Modifiers == ModifierKeys.None)
        {
            _briggsIndex++;
            if (_briggsIndex == _briggsSequence.Length)
            {
                _briggsIndex = 0;
                var briggsPath = GetSoundPath("Briggs.mp3");
                if (briggsPath is not null)
                {
                    ToastSound.Stop();
                    ToastSound.Source = null;
                    ToastSound.Source = new Uri(briggsPath);
                }
                if (DataContext is MainViewModel briggsVm)
                    _ = briggsVm.UnlockBriggsCommand.ExecuteAsync(null);
            }
        }
        else
        {
            _briggsIndex = e.Key == _briggsSequence[0] && Keyboard.Modifiers == ModifierKeys.None ? 1 : 0;
        }

        if (DataContext is not MainViewModel vm) return;

        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
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

    private void RarityFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is string rarity
            && DataContext is MainViewModel vm && vm.SelectedSet is not null)
            vm.SelectedSet.SelectedRarity = rarity;
    }

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
