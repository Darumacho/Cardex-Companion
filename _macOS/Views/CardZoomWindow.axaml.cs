using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cardex.Platform;
using Cardex.Services;

namespace Cardex.Views;

public partial class CardZoomWindow : Window
{
    private readonly string _imageUrl = "";
    private readonly ImageCacheService? _imageCache;
    private readonly string _cardId = "";

    // Requis par le chargeur XAML Avalonia ; l'app passe par le constructeur ci-dessous.
    public CardZoomWindow() => InitializeComponent();

    public CardZoomWindow(string name, string number, string? rarity, string imageUrl, string cardId, ImageCacheService imageCache)
    {
        InitializeComponent();
        _imageUrl = imageUrl;
        _imageCache = imageCache;
        _cardId = $"zoom_{cardId}";

        CardName.Text = name;
        CardDetails.Text = $"N° {number}" + (rarity is not null ? $"  ·  {rarity}" : "");

        Opened += async (_, _) =>
        {
            if (Screens.ScreenFromWindow(this) is { } screen)
                MaxHeight = screen.WorkingArea.Height / screen.Scaling - 20;
            await LoadImageAsync();
        };
        PointerPressed += (_, _) => Close();
        AddHandler(KeyDownEvent, Window_PreviewKeyDown, RoutingStrategies.Tunnel);
    }

    // Les ViewModels appellent Show() comme côté WPF, où l'Owner était fixé dans le constructeur ;
    // en Avalonia le propriétaire se passe à Show(owner).
    public new void Show()
    {
        if (Dialogs.MainWindow is { } owner) base.Show(owner);
        else base.Show();
    }

    private async Task LoadImageAsync()
    {
        if (_imageCache is null) return;
        var bmp = await _imageCache.GetFullResImageAsync(_imageUrl, _cardId);
        if (bmp is not null)
        {
            CardImage.Source = bmp;
            LoadingText.IsVisible = false;
            Dispatcher.UIThread.Post(EnsureOnScreen, DispatcherPriority.Loaded);
        }
    }

    // Remonte la fenêtre si l'image chargée la fait déborder en bas de l'écran.
    private void EnsureOnScreen()
    {
        if (Screens.ScreenFromWindow(this) is not { } screen) return;
        var area = screen.WorkingArea;
        var heightPx = (int)(Bounds.Height * screen.Scaling);
        if (Position.Y + heightPx > area.Bottom)
            Position = new PixelPoint(Position.X, Math.Max(area.Y + 8, area.Bottom - heightPx - 8));
    }

    private void Window_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); return; }
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Meta && Dialogs.MainWindow is MainWindow mw)
        {
            mw.FocusSearch();
            e.Handled = true;
        }
    }
}
