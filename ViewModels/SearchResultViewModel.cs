using CommunityToolkit.Mvvm.ComponentModel;
using Cardex.Services;
using System.Windows.Media.Imaging;

namespace Cardex.ViewModels;

public partial class SearchResultViewModel : ObservableObject
{
    private readonly ImageCacheService _imageCache;

    public string CardId { get; }
    public string Name { get; }
    public string Number { get; }
    public string SetId { get; }
    public string SetName { get; }
    public string ImageUrl { get; }
    public string? Rarity { get; }
    public int OwnedQuantity { get; }

    [ObservableProperty] private BitmapImage? _cardImage;
    [ObservableProperty] private bool _isLoadingImage;

    public SearchResultViewModel(string cardId, string name, string number, string setId, string setName,
        string imageUrl, string? rarity, int ownedQuantity, ImageCacheService imageCache)
    {
        CardId = cardId;
        Name = name;
        Number = number;
        SetId = setId;
        SetName = setName;
        ImageUrl = imageUrl;
        Rarity = rarity;
        OwnedQuantity = ownedQuantity;
        _imageCache = imageCache;
    }

    public async Task LoadImageAsync()
    {
        if (CardImage is not null || IsLoadingImage) return;
        IsLoadingImage = true;
        try
        {
            CardImage = await _imageCache.GetImageAsync(ImageUrl, $"sr_{CardId}");
        }
        finally
        {
            IsLoadingImage = false;
        }
    }
}
