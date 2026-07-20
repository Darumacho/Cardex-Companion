using CommunityToolkit.Mvvm.ComponentModel;
using Cardex.Services;
using System.Windows.Media.Imaging;

namespace Cardex.ViewModels;

public partial class CardViewModel : ObservableObject
{
    private readonly ImageCacheService _imageCache;

    public string CardId { get; }
    public string Name { get; }
    public string Number { get; }
    public string SetId { get; }
    public string ImageUrl { get; }
    public string? Rarity { get; }

    [ObservableProperty] private bool _isOwned;
    [ObservableProperty] private BitmapImage? _cardImage;
    [ObservableProperty] private bool _isLoadingImage;

    public CardViewModel(
        string cardId, string name, string number, string setId,
        string imageUrl, string? rarity, bool isOwned,
        ImageCacheService imageCache)
    {
        CardId = cardId;
        Name = name;
        Number = number;
        SetId = setId;
        ImageUrl = imageUrl;
        Rarity = rarity;
        _isOwned = isOwned;
        _imageCache = imageCache;
    }

    public async Task LoadImageAsync()
    {
        if (CardImage is not null || IsLoadingImage) return;
        IsLoadingImage = true;
        try
        {
            CardImage = await _imageCache.GetImageAsync(ImageUrl, CardId);
        }
        finally
        {
            IsLoadingImage = false;
        }
    }
}
