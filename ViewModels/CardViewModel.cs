using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty] private BitmapImage? _cardImage;
    [ObservableProperty] private bool _isLoadingImage;

    private int _quantity;
    private bool _isOwned;

    public int Quantity
    {
        get => _quantity;
        set
        {
            int clamped = Math.Max(0, value);
            if (!SetProperty(ref _quantity, clamped)) return;
            var shouldBeOwned = _quantity > 0;
            if (_isOwned != shouldBeOwned)
                SetProperty(ref _isOwned, shouldBeOwned, nameof(IsOwned));
        }
    }

    public bool IsOwned
    {
        get => _isOwned;
        set
        {
            if (!SetProperty(ref _isOwned, value)) return;
            if (value && _quantity == 0)
                SetProperty(ref _quantity, 1, nameof(Quantity));
            else if (!value && _quantity > 0)
                SetProperty(ref _quantity, 0, nameof(Quantity));
        }
    }

    public CardViewModel(
        string cardId, string name, string number, string setId,
        string imageUrl, string? rarity, int quantity,
        ImageCacheService imageCache)
    {
        CardId = cardId;
        Name = name;
        Number = number;
        SetId = setId;
        ImageUrl = imageUrl;
        Rarity = rarity;
        _quantity = quantity;
        _isOwned = quantity > 0;
        _imageCache = imageCache;
    }

    [RelayCommand]
    private void Increment() => Quantity++;

    [RelayCommand]
    private void Decrement() => Quantity--;

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
