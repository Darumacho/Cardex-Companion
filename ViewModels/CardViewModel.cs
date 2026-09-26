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
    public string? ImageLargeUrl { get; }
    public string? Rarity { get; }
    public string? Supertype { get; }
    public string? Subtypes { get; }

    [ObservableProperty] private BitmapImage? _cardImage;
    [ObservableProperty] private bool _isLoadingImage;
    [ObservableProperty] private bool _isWanted;
    [ObservableProperty] private bool _isExcluded;

    // Some fallback sets (see TCGdex) don't have an image URL at all for certain cards.
    public bool HasNoImage => string.IsNullOrEmpty(ImageUrl);
    [ObservableProperty] private int _deckQuantity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagBrush))]
    private TagViewModel? _selectedTag;

    public System.Windows.Media.SolidColorBrush? TagBrush =>
        SelectedTag?.Id > 0 ? SelectedTag.Brush : null;

    public Func<CardViewModel, TagViewModel?, Task>? OnTagChanged { get; set; }

    partial void OnSelectedTagChanged(TagViewModel? value)
    {
        if (OnTagChanged is not null)
            _ = OnTagChanged(this, value?.Id > 0 ? value : null);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CmLowText))]
    [NotifyPropertyChangedFor(nameof(CmTooltip))]
    private decimal? _cmLow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TcgLowText))]
    [NotifyPropertyChangedFor(nameof(TcgTooltip))]
    private decimal? _tcgLow;

    public DateTime? PricesUpdatedAt { get; set; }
    public string? CmUrl { get; set; }
    public string? TcgUrl { get; set; }

    public string CmLowText   => CmLow.HasValue  ? $"€{CmLow.Value:F2}"  : "—";
    public string TcgLowText  => TcgLow.HasValue ? $"${TcgLow.Value:F2}" : "—";
    public string CmTooltip   => $"Cardmarket : {CmLowText}\nClick to visit the card's page";
    public string TcgTooltip  => $"TCGPlayer : {TcgLowText}\nClick to visit the card's page";
    public bool HasMultiple   => _quantity > 1;

    private int _quantity;
    private bool _isOwned;

    public int Quantity
    {
        get => _quantity;
        set
        {
            int clamped = Math.Max(0, value);
            if (!SetProperty(ref _quantity, clamped)) return;
            OnPropertyChanged(nameof(HasMultiple));
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
        string imageUrl, string? imageLargeUrl, string? rarity, int quantity, bool isWanted, bool isExcluded,
        ImageCacheService imageCache, string? supertype = null, string? subtypes = null)
    {
        CardId = cardId;
        Name = name;
        Number = number;
        SetId = setId;
        ImageUrl = imageUrl;
        ImageLargeUrl = imageLargeUrl;
        Rarity = rarity;
        Supertype = supertype;
        Subtypes = subtypes;
        _quantity = quantity;
        _isOwned = quantity > 0;
        _isWanted = isWanted;
        _isExcluded = isExcluded;
        _imageCache = imageCache;
    }

    [RelayCommand]
    private void ShowZoom()
    {
        var url = ImageLargeUrl
            ?? (ImageUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? ImageUrl[..^4] + "_hires.png"
                : ImageUrl);
        var win = new Views.CardZoomWindow(Name, Number, Rarity, url, CardId, _imageCache);
        win.Show();
    }

    [RelayCommand]
    private void ToggleOwned() => IsOwned = !IsOwned;

    [RelayCommand]
    private void ToggleWanted() => IsWanted = !IsWanted;

    [RelayCommand]
    private void ToggleExcluded() => IsExcluded = !IsExcluded;

    [RelayCommand]
    private void Increment() => Quantity++;

    [RelayCommand]
    private void Decrement() => Quantity--;

    // Les cartes venant de TCGdex/Cardex API n'ont pas d'URL directe (elles viennent du service de
    // redirection de pokemontcg.io, qui ne connaît pas ces cartes) : on retombe sur une recherche
    // par nom plutôt que de ne rien faire au clic.
    // Nom + identifiant du set + numéro (ex: "Lunatone MEP 4") pour cibler la bonne impression.
    private string SearchQuery => Uri.EscapeDataString($"{Name} {SetId.ToUpperInvariant()} {Number}");

    [RelayCommand]
    private void OpenCmLink()
    {
        var url = !string.IsNullOrEmpty(CmUrl)
            ? CmUrl
            : $"https://www.cardmarket.com/en/Pokemon/Products/Search?searchString={SearchQuery}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenTcgLink()
    {
        var url = !string.IsNullOrEmpty(TcgUrl)
            ? TcgUrl
            : $"https://www.tcgplayer.com/search/pokemon/product?productLineName=pokemon&view=grid&q={SearchQuery}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
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
