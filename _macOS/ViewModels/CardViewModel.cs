using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cardex.Services;
using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;

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

    [ObservableProperty] private Bitmap? _cardImage;
    [ObservableProperty] private bool _isLoadingImage;
    [ObservableProperty] private bool _isWanted;
    [ObservableProperty] private bool _isExcluded;
    [ObservableProperty] private bool _isHolo;

    // Some fallback sets (see TCGdex) don't have an image URL at all for certain cards.
    public bool HasNoImage => string.IsNullOrEmpty(ImageUrl);
    [ObservableProperty] private int _deckQuantity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagBrush))]
    private TagViewModel? _selectedTag;

    public Avalonia.Media.SolidColorBrush? TagBrush =>
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
    public string? ShortCode { get; set; }

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
            OnPropertyChanged(nameof(TriggerLabel));
            OnPropertyChanged(nameof(TotalLabel));
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
            if (IsMultiLanguageMode)
            {
                if (value)
                {
                    if (Quantity == 0)
                    {
                        var en = LanguageRows.FirstOrDefault(r => r.Code == "EN") ?? LanguageRows.FirstOrDefault();
                        if (en is not null) en.Quantity = 1;
                    }
                }
                else
                {
                    foreach (var row in LanguageRows) row.Quantity = 0;
                }
            }
            else
            {
                if (value && _quantity == 0)
                    SetProperty(ref _quantity, 1, nameof(Quantity));
                else if (!value && _quantity > 0)
                    SetProperty(ref _quantity, 0, nameof(Quantity));
            }
        }
    }

    // Vrai quand plus d'une langue est suivie (Settings → Languages) : le compteur simple +/- est
    // remplacé par le select par langue (LanguageRows) dans la vue. Figé à la construction de la
    // carte — un changement de réglage ne prend effet qu'au prochain chargement du set
    // (MainViewModel force un rechargement du set ouvert quand les langues suivies changent).
    public bool IsMultiLanguageMode { get; }
    public ObservableCollection<LanguageRowViewModel> LanguageRows { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronGlyph))]
    private bool _selectOpen;

    // Petit chevron sur le bouton du select, pour indiquer qu'il est cliquable/dépliable.
    public string ChevronGlyph => SelectOpen ? "▲" : "▼";

    // Appelé (par MainViewModel) chaque fois qu'une ligne de langue change, pour persister le
    // détail par langue — indépendant de la persistance de l'agrégat Quantity (déjà gérée via le
    // PropertyChanged existant sur Quantity, inchangée).
    public Func<CardViewModel, Task>? OnLanguageBreakdownChanged { get; set; }

    public string TriggerLabel
    {
        get
        {
            if (Quantity == 0) return "Not owned";
            var checkedRows = LanguageRows.Where(r => r.Checked).ToList();
            if (checkedRows.Count <= 3)
                return string.Join(", ", checkedRows.Select(r => $"{r.Code} ×{r.Quantity}"));
            return $"{checkedRows.Count} languages · {Quantity} total";
        }
    }

    public string TotalLabel => $"Total: {Quantity} {(Quantity == 1 ? "copy" : "copies")}";

    [RelayCommand] private void ToggleSelect() => SelectOpen = !SelectOpen;
    [RelayCommand] private void CloseSelect() => SelectOpen = false;

    private void OnLanguageRowChanged()
    {
        Quantity = LanguageRows.Sum(r => r.Quantity);
        if (OnLanguageBreakdownChanged is not null)
            _ = OnLanguageBreakdownChanged(this);
    }

    public CardViewModel(
        string cardId, string name, string number, string setId,
        string imageUrl, string? imageLargeUrl, string? rarity, int quantity, bool isWanted, bool isExcluded,
        ImageCacheService imageCache, string? supertype = null, string? subtypes = null,
        IReadOnlyList<string>? trackedLanguages = null, Dictionary<string, int>? languageQuantities = null,
        bool isHolo = false)
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
        _isHolo = isHolo;
        _imageCache = imageCache;

        IsMultiLanguageMode = trackedLanguages is { Count: > 1 };
        if (IsMultiLanguageMode)
        {
            foreach (var code in trackedLanguages!)
            {
                var qty = languageQuantities?.GetValueOrDefault(code) ?? 0;
                LanguageRows.Add(new LanguageRowViewModel(code, LanguageCatalog.Label(code), qty, OnLanguageRowChanged));
            }
        }
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
    // Exception : les sets "Trainer Kit" demi-deck partagent un code marketplace avec un suffixe
    // a/b sur le numéro plutôt que sur le set (voir SetShortCodes.MarketplaceOverrides) — sans ça,
    // "Bagon TK1A 1" ne donne aucun résultat sur Cardmarket alors que "Bagon TK1 1A" si.
    private string CmSearchQuery => Uri.EscapeDataString(
        SetShortCodes.CardmarketCustomOverrides.TryGetValue(SetId, out var cmCustom)
            ? (cmCustom.Code is null
                ? $"{Name} {StripNumberPrefix(Number, cmCustom.StripNumberPrefix)}"
                : $"{Name} {cmCustom.Code} {StripNumberPrefix(Number, cmCustom.StripNumberPrefix)}")
            : SetShortCodes.MarketplaceOverrides.TryGetValue(SetId, out var cmOverride)
                ? $"{Name} {cmOverride.Code} {Number}{cmOverride.Suffix}"
                : SetShortCodes.TrainerKitLetterOverrides.TryGetValue(SetId, out var cmLetterOverride)
                    ? $"{Name} {cmLetterOverride.Code} {SetShortCodes.FormatTrainerKitNumber(Number, cmLetterOverride.PokemonLetter, cmLetterOverride.Position)}"
                    : SetShortCodes.CardmarketUsesShortCodeFrom.Contains(SetId)
                        ? $"{Name} {ShortCode ?? SetId.ToUpperInvariant()} {Number}"
                        : $"{Name} {SetId.ToUpperInvariant()} {Number}");

    private static string StripNumberPrefix(string number, string? prefix) =>
        prefix is not null && number.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? number[prefix.Length..]
            : number;

    // ShortCode + nom + numéro sur 3 chiffres (ex: "SVP Lunatone 004") — pas de ShortCode pour
    // certains sets de secours (TCGdex), on retombe alors sur le SetId en majuscules.
    private static string PadCardNumber(string number) =>
        int.TryParse(number, out var n) ? n.ToString("D3") : number;

    private string TcgSearchQuery => Uri.EscapeDataString(
        SetShortCodes.MarketplaceOverrides.TryGetValue(SetId, out var tcgOverride)
            ? $"{tcgOverride.Code} {Name} {Number}{tcgOverride.Suffix}"
            : SetShortCodes.TrainerKitLetterOverrides.TryGetValue(SetId, out var tcgLetterOverride)
                ? $"{tcgLetterOverride.Code} {Name} {SetShortCodes.FormatTrainerKitNumber(Number, tcgLetterOverride.PokemonLetter, tcgLetterOverride.Position)}"
                : $"{ShortCode ?? SetId.ToUpperInvariant()} {Name} {PadCardNumber(Number)}");

    [RelayCommand]
    private void OpenCmLink()
    {
        var url = !string.IsNullOrEmpty(CmUrl)
            ? CmUrl
            : $"https://www.cardmarket.com/en/Pokemon/Products/Search?searchString={CmSearchQuery}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenTcgLink()
    {
        var url = !string.IsNullOrEmpty(TcgUrl)
            ? TcgUrl
            : $"https://www.tcgplayer.com/search/pokemon/product?productLineName=pokemon&view=grid&q={TcgSearchQuery}";
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
