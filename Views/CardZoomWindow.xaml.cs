using Cardex.Services;
using System.Windows;
using System.Windows.Input;

namespace Cardex.Views;

public partial class CardZoomWindow : Window
{
    private readonly string _imageUrl;
    private readonly ImageCacheService _imageCache;
    private readonly string _cardId;

    public CardZoomWindow(string name, string number, string? rarity, string imageUrl, string cardId, ImageCacheService imageCache)
    {
        InitializeComponent();
        _imageUrl = imageUrl;
        _imageCache = imageCache;
        _cardId = $"zoom_{cardId}";

        CardName.Text = name;
        CardDetails.Text = $"N° {number}" + (rarity is not null ? $"  ·  {rarity}" : "");

        Owner = Application.Current.MainWindow;
        MaxHeight = SystemParameters.WorkArea.Height - 20;
        Loaded += async (_, _) => await LoadImageAsync();
    }

    private async Task LoadImageAsync()
    {
        var bmp = await _imageCache.GetFullResImageAsync(_imageUrl, _cardId);
        if (bmp is not null)
        {
            CardImage.Source = bmp;
            LoadingText.Visibility = Visibility.Collapsed;
            _ = Dispatcher.BeginInvoke(EnsureOnScreen, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void EnsureOnScreen()
    {
        var workArea = SystemParameters.WorkArea;
        if (Top + ActualHeight > workArea.Bottom)
            Top = Math.Max(workArea.Top + 8, workArea.Bottom - ActualHeight - 8);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Close();
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); return; }
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.FocusSearch();
                e.Handled = true;
            }
        }
    }
}
