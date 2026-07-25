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
        Loaded += async (_, _) => await LoadImageAsync();
    }

    private async Task LoadImageAsync()
    {
        var bmp = await _imageCache.GetFullResImageAsync(_imageUrl, _cardId);
        if (bmp is not null)
        {
            CardImage.Source = bmp;
            LoadingText.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Close();
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}
