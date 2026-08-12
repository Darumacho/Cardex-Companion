using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cardex.Data;
using Cardex.Models;
using Cardex.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Cardex.ViewModels;

// ─────────────────────────────────────────────────────────────────────────────
// Child ViewModels
// ─────────────────────────────────────────────────────────────────────────────

public partial class DeckBrowserCardVm : ObservableObject
{
    private readonly ImageCacheService _imageCache;

    public string CardId  { get; }
    public string Name    { get; }
    public string SetId   { get; }
    public string SetName { get; }
    public string Number  { get; }
    public string ImageUrl { get; }
    public string? ImageLargeUrl { get; }
    public string? Supertype { get; }
    public string? Types     { get; }
    public string? Subtypes  { get; }
    public string? Rarity    { get; }
    public decimal? CmLow    { get; }
    public decimal? TcgLow   { get; }
    public bool IsOwned      { get; }

    [ObservableProperty] private int _deckQuantity;
    [ObservableProperty] private ImageSource? _cardImage;
    [ObservableProperty] private bool _isLoadingImage;

    public bool InDeck => DeckQuantity > 0;
    partial void OnDeckQuantityChanged(int value) => OnPropertyChanged(nameof(InDeck));

    public string TypeBadge => string.Join(" · ", new[] { Supertype, Subtypes, Types }
        .Where(s => !string.IsNullOrWhiteSpace(s)));

    public string PriceText
    {
        get
        {
            var parts = new List<string>();
            if (CmLow.HasValue)  parts.Add($"{CmLow:F2}€");
            if (TcgLow.HasValue) parts.Add($"${TcgLow:F2}");
            return parts.Count > 0 ? string.Join("  /  ", parts) : "";
        }
    }

    public DeckBrowserCardVm(string cardId, string name, string setId, string setName,
        string number, string imageUrl, string? imageLargeUrl, string? supertype, string? types,
        string? subtypes, string? rarity, decimal? cmLow, decimal? tcgLow, int deckQty,
        bool isOwned, ImageCacheService imageCache)
    {
        CardId   = cardId; Name     = name;   SetId   = setId;
        SetName  = setName; Number  = number; ImageUrl = imageUrl;
        ImageLargeUrl = imageLargeUrl;
        Supertype = supertype; Types = types; Subtypes = subtypes;
        Rarity   = rarity; CmLow   = cmLow;  TcgLow  = tcgLow;
        _deckQuantity = deckQty; IsOwned = isOwned;
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

    public async Task LoadImageAsync()
    {
        if (CardImage is not null || IsLoadingImage || string.IsNullOrEmpty(ImageUrl)) return;
        IsLoadingImage = true;
        try   { CardImage = await _imageCache.GetImageAsync(ImageUrl, $"db_{CardId}"); }
        catch { }
        finally { IsLoadingImage = false; }
    }
}

public partial class DeckEntryVm : ObservableObject
{
    private readonly ImageCacheService _imageCache;

    public string CardId   { get; }
    public string Name     { get; }
    public string SetId    { get; }
    public string SetName  { get; }
    public string Number   { get; }
    public string? Supertype { get; }
    public string? Subtypes  { get; }
    public string? ImageUrl  { get; }
    public string? ImageLargeUrl { get; }
    public string? Rarity    { get; }
    public decimal? CmLow    { get; }
    public decimal? TcgLow   { get; }
    public bool SetStandardLegal { get; }
    public bool SetExpandedLegal { get; }

    [ObservableProperty] private int _quantity;

    public bool IsBasicEnergy =>
        Supertype == "Energy" && (Subtypes is null || Subtypes.Contains("Basic Energy"));

    public int MaxQuantity => IsBasicEnergy ? 60 : 4;

    // Basic Energy is legal in any format regardless of which set it was printed in.
    public bool IsStandardLegal => IsBasicEnergy || SetStandardLegal;
    public bool IsExpandedLegal => IsBasicEnergy || SetExpandedLegal;

    public decimal? LineCmPrice  => CmLow  is null ? null : CmLow  * Quantity;
    public decimal? LineTcgPrice => TcgLow is null ? null : TcgLow * Quantity;

    public DeckEntryVm(string cardId, string name, string setId, string setName,
        string number, string? supertype, string? subtypes, string? imageUrl,
        string? imageLargeUrl, string? rarity, decimal? cmLow, decimal? tcgLow,
        bool setStandardLegal, bool setExpandedLegal,
        int quantity, ImageCacheService imageCache)
    {
        CardId = cardId; Name = name; SetId = setId; SetName = setName;
        Number = number; Supertype = supertype; Subtypes = subtypes;
        ImageUrl = imageUrl; ImageLargeUrl = imageLargeUrl; Rarity = rarity;
        CmLow = cmLow; TcgLow = tcgLow;
        SetStandardLegal = setStandardLegal; SetExpandedLegal = setExpandedLegal;
        _quantity = quantity;
        _imageCache = imageCache;
    }

    [RelayCommand]
    private void ShowZoom()
    {
        if (string.IsNullOrEmpty(ImageUrl)) return;
        var url = ImageLargeUrl
            ?? (ImageUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? ImageUrl[..^4] + "_hires.png"
                : ImageUrl);
        var win = new Views.CardZoomWindow(Name, Number, Rarity, url, CardId, _imageCache);
        win.Show();
    }

    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(LineCmPrice));
        OnPropertyChanged(nameof(LineTcgPrice));
    }
}

public partial class DeckSummaryVm : ObservableObject
{
    public int Id { get; }
    public string Name { get; private set; }
    public DateTime UpdatedAt { get; }
    public int PokemonCount { get; }
    public int TrainerCount { get; }
    public int EnergyCount { get; }
    public int TotalCards { get; }

    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editingName = "";

    public bool IsNotEditing => !IsEditing;

    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(IsNotEditing));

    public DeckSummaryVm(int id, string name, DateTime updatedAt,
        int pokemonCount, int trainerCount, int energyCount)
    {
        Id = id; Name = name; UpdatedAt = updatedAt;
        PokemonCount = pokemonCount; TrainerCount = trainerCount; EnergyCount = energyCount;
        TotalCards = pokemonCount + trainerCount + energyCount;
    }

    public void UpdateName(string name) => Name = name;
}

public record SetFilterItem(string Display, string Value, bool IsHeader = false);
public record TagFilterItem(string Display, int Value);

// ─────────────────────────────────────────────────────────────────────────────
// Main ViewModel
// ─────────────────────────────────────────────────────────────────────────────

public partial class DeckBuilderViewModel : ObservableObject
{
    private readonly AppDbContext _db;
    private readonly ImageCacheService _imageCache;
    private Dictionary<string, string> _setNames = [];
    private Dictionary<string, (bool Standard, bool Expanded)> _setLegality = [];
    private HashSet<string> _ownedIds = [];

    // ── Browser ──────────────────────────────────────────────────────────────
    public ObservableCollection<DeckBrowserCardVm> BrowserCards { get; } = [];
    public ObservableCollection<SetFilterItem> SetOptions { get; } = [];
    public ObservableCollection<TagFilterItem> TagOptions { get; } = [];

    [ObservableProperty] private string _cardSearch = "";
    [ObservableProperty] private string _supertypeFilter = "All";
    [ObservableProperty] private string _typeFilter = "All";
    [ObservableProperty] private string _subtypeFilter = "All";
    [ObservableProperty] private string _setFilter = "All";
    [ObservableProperty] private int    _tagFilter = -1;
    [ObservableProperty] private bool   _onlyOwned;
    [ObservableProperty] private bool   _isSearching;
    [ObservableProperty] private string _browserStatus = "Search for a card to get started";

    public static readonly string[] SupertypeOptions =
        ["All", "Pokémon", "Trainer", "Energy"];

    public static readonly string[] TypeOptions =
        ["All", "Colorless", "Darkness", "Dragon", "Fairy", "Fighting",
         "Fire", "Grass", "Lightning", "Metal", "Psychic", "Water"];

    public static readonly string[] SubtypeOptions =
        ["All", "Basic", "Stage 1", "Stage 2", "V", "VMAX", "VSTAR",
         "ex", "EX", "GX", "Item", "Supporter", "Stadium",
         "Basic Energy", "Special Energy"];

    // ── Deck actuel ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _deckName = "New Deck";
    [ObservableProperty] private int?   _currentDeckId;
    [ObservableProperty] private bool   _isSaving;

    // ── Toast (avertissements de règles) ────────────────────────────────────
    [ObservableProperty] private string? _toastMessage;
    public bool HasToast => !string.IsNullOrEmpty(ToastMessage);
    partial void OnToastMessageChanged(string? value) => OnPropertyChanged(nameof(HasToast));

    private readonly System.Windows.Threading.DispatcherTimer _toastTimer =
        new() { Interval = TimeSpan.FromSeconds(2.5) };

    private void ShowToast(string message)
    {
        ToastMessage = message;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    public ObservableCollection<DeckEntryVm> DeckEntries { get; } = [];
    public ObservableCollection<DeckSummaryVm> SavedDecks { get; } = [];

    // ── Stats calculées ──────────────────────────────────────────────────────
    public int  TotalCards    => DeckEntries.Sum(e => e.Quantity);
    public int  PokemonCount  => DeckEntries.Where(e => e.Supertype == "Pokémon").Sum(e => e.Quantity);
    public int  TrainerCount  => DeckEntries.Where(e => e.Supertype == "Trainer").Sum(e => e.Quantity);
    public int  EnergyCount   => DeckEntries.Where(e => e.Supertype == "Energy") .Sum(e => e.Quantity);
    public int  UnknownCount  => DeckEntries.Where(e => e.Supertype is null).Sum(e => e.Quantity);
    public bool HasUnknown    => UnknownCount > 0;
    public bool IsDeckFull    => TotalCards >= 60;
    public bool IsValidDeck   => TotalCards == 60;
    public string DeckStatus  => $"{TotalCards}/60";

    // ── Légalité de format ───────────────────────────────────────────────────
    public bool IsStandardLegal => DeckEntries.Count > 0 && DeckEntries.All(e => e.IsStandardLegal);
    public bool IsExpandedLegal => DeckEntries.Count > 0 && DeckEntries.All(e => e.IsExpandedLegal);

    public string StandardLegalTooltip => BuildLegalityTooltip(e => e.IsStandardLegal, "Standard");
    public string ExpandedLegalTooltip => BuildLegalityTooltip(e => e.IsExpandedLegal, "Expanded");

    private string BuildLegalityTooltip(Func<DeckEntryVm, bool> isLegal, string format)
    {
        var illegal = DeckEntries.Where(e => !isLegal(e)).Select(e => e.Name).Distinct().ToList();
        if (illegal.Count == 0) return $"All cards are {format} legal";
        return $"Not {format} legal: " + string.Join(", ", illegal.Take(8))
            + (illegal.Count > 8 ? $" (+{illegal.Count - 8} more)" : "");
    }

    public decimal? TotalCmPrice => DeckEntries.Any(e => e.CmLow.HasValue)
        ? DeckEntries.Where(e => e.CmLow.HasValue).Sum(e => e.CmLow!.Value * e.Quantity)
        : null;
    public decimal? TotalTcgPrice => DeckEntries.Any(e => e.TcgLow.HasValue)
        ? DeckEntries.Where(e => e.TcgLow.HasValue).Sum(e => e.TcgLow!.Value * e.Quantity)
        : null;

    public string TotalCmPriceText  => TotalCmPrice.HasValue  ? $"~{TotalCmPrice:F2}€"  : "—";
    public string TotalTcgPriceText => TotalTcgPrice.HasValue ? $"~${TotalTcgPrice:F2}" : "—";

    public string PriceSummary
    {
        get
        {
            var parts = new List<string>();
            if (TotalCmPrice.HasValue)  parts.Add($"~{TotalCmPrice:F2} €");
            if (TotalTcgPrice.HasValue) parts.Add($"~${TotalTcgPrice:F2}");
            return parts.Count > 0 ? string.Join("   /   ", parts) : "Prices not available";
        }
    }

    public event Action? DeckChanged;

    // ─────────────────────────────────────────────────────────────────────────
    public DeckBuilderViewModel(AppDbContext db, ImageCacheService imageCache)
    {
        _db = db;
        _imageCache = imageCache;
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastMessage = null; };
    }

    public async Task InitializeAsync()
    {
        _setNames = await _db.CachedSets.ToDictionaryAsync(s => s.SetId, s => s.Name);
        _setLegality = await _db.CachedSets.ToDictionaryAsync(
            s => s.SetId, s => (s.StandardLegal, s.ExpandedLegal));
        _ownedIds = (await _db.OwnedCards.Select(o => o.CardId).ToListAsync()).ToHashSet();

        var sets = await _db.CachedSets
            .OrderByDescending(s => s.ReleaseDate)
            .ToListAsync();

        SetOptions.Clear();
        SetOptions.Add(new SetFilterItem("All sets", "All"));
        string? currentSeries = null;
        foreach (var s in sets)
        {
            if (s.Series != currentSeries)
            {
                SetOptions.Add(new SetFilterItem($"── {s.Series} ──", string.Empty, IsHeader: true));
                currentSeries = s.Series;
            }
            SetOptions.Add(new SetFilterItem(s.Name, s.SetId));
        }

        var tags = await _db.Tags.OrderBy(t => t.Name).ToListAsync();
        TagOptions.Clear();
        TagOptions.Add(new TagFilterItem("All tags", -1));
        TagOptions.Add(new TagFilterItem("No tag", 0));
        foreach (var t in tags)
            TagOptions.Add(new TagFilterItem(t.Name, t.Id));

        await LoadSavedDecksAsync();
    }

    // ── Recherche ────────────────────────────────────────────────────────────
    partial void OnSupertypeFilterChanged(string value) => _ = SearchCardsAsync();
    partial void OnTypeFilterChanged(string value)      => _ = SearchCardsAsync();
    partial void OnSubtypeFilterChanged(string value)   => _ = SearchCardsAsync();
    partial void OnSetFilterChanged(string value)       => _ = SearchCardsAsync();
    partial void OnTagFilterChanged(int value)          => _ = SearchCardsAsync();
    partial void OnOnlyOwnedChanged(bool value)         => _ = SearchCardsAsync();

    [RelayCommand]
    private async Task SearchCardsAsync()
    {
        IsSearching = true;
        BrowserCards.Clear();
        try
        {
            var q = _db.CachedCards.AsQueryable();

            if (!string.IsNullOrWhiteSpace(CardSearch))
                q = q.Where(c => EF.Functions.Like(c.Name, $"%{CardSearch}%"));

            if (SupertypeFilter != "All")
            {
                if (SupertypeFilter == "Energy")
                    q = q.Where(c => c.Supertype == "Energy"
                        || (c.Supertype == null && (
                            EF.Functions.Like(c.Subtypes ?? "", "%Energy%")
                            || EF.Functions.Like(c.Name, "%Energy"))));
                else
                    q = q.Where(c => c.Supertype == SupertypeFilter);
            }

            if (TypeFilter != "All")
                q = q.Where(c => EF.Functions.Like(c.Types ?? "", $"%{TypeFilter}%"));

            if (SubtypeFilter != "All")
                q = q.Where(c => EF.Functions.Like(c.Subtypes ?? "", $"%{SubtypeFilter}%"));

            if (SetFilter != "All" && !string.IsNullOrEmpty(SetFilter))
                q = q.Where(c => c.SetId == SetFilter);

            if (TagFilter != -1)
            {
                if (TagFilter == 0)
                {
                    var taggedIds = _db.CardTags.Select(ct => ct.CardId);
                    q = q.Where(c => !taggedIds.Contains(c.CardId));
                }
                else
                {
                    var taggedIds = _db.CardTags.Where(ct => ct.TagId == TagFilter).Select(ct => ct.CardId);
                    q = q.Where(c => taggedIds.Contains(c.CardId));
                }
            }

            if (OnlyOwned)
            {
                var owned = _ownedIds.ToList();
                q = q.Where(c => owned.Contains(c.CardId));
            }

            var cards = await q.OrderBy(c => c.Name).ThenBy(c => c.SetId).Take(150).ToListAsync();

            foreach (var c in cards)
            {
                var inDeck = DeckEntries.FirstOrDefault(e => e.CardId == c.CardId)?.Quantity ?? 0;
                BrowserCards.Add(new DeckBrowserCardVm(
                    c.CardId, c.Name, c.SetId,
                    _setNames.GetValueOrDefault(c.SetId, c.SetId),
                    c.Number, c.ImageSmall, c.ImageLarge,
                    c.Supertype, c.Types, c.Subtypes,
                    c.Rarity, c.CmLow, c.TcgLow,
                    inDeck, _ownedIds.Contains(c.CardId),
                    _imageCache));
            }

            BrowserStatus = cards.Count == 0
                ? "No cards found — browse some sets first to populate the database"
                : cards.Count == 150
                    ? "150 results shown — refine your search"
                    : $"{cards.Count} card(s) found";

            // Chargement des images en parallèle
            using var sem = new SemaphoreSlim(8, 8);
            _ = Task.WhenAll(BrowserCards.ToList().Select(async vm =>
            {
                await sem.WaitAsync();
                try { await vm.LoadImageAsync(); }
                catch { }
                finally { sem.Release(); }
            }));
        }
        finally { IsSearching = false; }
    }

    // ── Gestion du deck ──────────────────────────────────────────────────────

    // Un Pokémon (toutes cartes/sets confondus) ne peut pas dépasser 4 exemplaires dans le deck.
    private int TotalPokemonByName(string name) =>
        DeckEntries.Where(e => e.Supertype == "Pokémon" && e.Name == name).Sum(e => e.Quantity);

    private bool WouldExceedPokemonLimit(string? supertype, string? subtypes, string name) =>
        InferSupertype(supertype, subtypes, name) == "Pokémon" && TotalPokemonByName(name) >= 4;

    [RelayCommand]
    private void AddCardToDeck(DeckBrowserCardVm card)
    {
        if (WouldExceedPokemonLimit(card.Supertype, card.Subtypes, card.Name))
        {
            ShowToast($"Maximum 4 {card.Name} in the deck");
            return;
        }

        var existing = DeckEntries.FirstOrDefault(e => e.CardId == card.CardId);
        if (existing is not null)
        {
            if (TotalCards >= 60) { ShowToast("Deck full (60/60)"); return; }
            if (existing.Quantity >= existing.MaxQuantity)
            {
                ShowToast($"Maximum {existing.MaxQuantity} copies of this card");
                return;
            }
            existing.Quantity++;
            card.DeckQuantity = existing.Quantity;
        }
        else
        {
            if (TotalCards >= 60) { ShowToast("Deck full (60/60)"); return; }
            var entry = CreateEntry(card.CardId, card.Name, card.SetId,
                _setNames.GetValueOrDefault(card.SetId, card.SetId),
                card.Number, card.Supertype, card.Subtypes,
                card.ImageUrl, card.ImageLargeUrl, card.Rarity, card.CmLow, card.TcgLow, 1);
            InsertEntry(entry);
            card.DeckQuantity = 1;
        }
        NotifyDeckChanged();
    }

    public void AddCardFromCollection(string cardId, string name, string setId,
        string setName, string number, string? supertype, string? subtypes,
        string? imageUrl, string? imageLargeUrl, string? rarity, decimal? cmLow, decimal? tcgLow)
    {
        if (WouldExceedPokemonLimit(supertype, subtypes, name))
        {
            ShowToast($"Maximum 4 {name} in the deck");
            return;
        }

        var existing = DeckEntries.FirstOrDefault(e => e.CardId == cardId);
        if (existing is not null)
        {
            if (TotalCards >= 60) { ShowToast("Deck full (60/60)"); return; }
            if (existing.Quantity >= existing.MaxQuantity)
            {
                ShowToast($"Maximum {existing.MaxQuantity} copies of this card");
                return;
            }
            existing.Quantity++;
        }
        else
        {
            if (TotalCards >= 60) { ShowToast("Deck full (60/60)"); return; }
            var entry = CreateEntry(cardId, name, setId, setName,
                number, supertype, subtypes, imageUrl, imageLargeUrl, rarity, cmLow, tcgLow, 1);
            InsertEntry(entry);
        }
        NotifyDeckChanged();
    }

    [RelayCommand]
    private void IncreaseDeckEntry(DeckEntryVm entry)
    {
        if (entry.Supertype == "Pokémon" && TotalPokemonByName(entry.Name) >= 4)
        {
            ShowToast($"Maximum 4 {entry.Name} in the deck");
            return;
        }
        if (TotalCards >= 60) { ShowToast("Deck full (60/60)"); return; }
        if (entry.Quantity >= entry.MaxQuantity)
        {
            ShowToast($"Maximum {entry.MaxQuantity} copies of this card");
            return;
        }
        entry.Quantity++;
        UpdateBrowserQty(entry.CardId, entry.Quantity);
        NotifyDeckChanged();
    }

    [RelayCommand]
    private void DecreaseDeckEntry(DeckEntryVm entry)
    {
        if (entry.Quantity > 1)
        {
            entry.Quantity--;
            UpdateBrowserQty(entry.CardId, entry.Quantity);
        }
        else
        {
            DeckEntries.Remove(entry);
            UpdateBrowserQty(entry.CardId, 0);
        }
        NotifyDeckChanged();
    }

    [RelayCommand]
    private void RemoveFromDeck(DeckEntryVm entry)
    {
        DeckEntries.Remove(entry);
        UpdateBrowserQty(entry.CardId, 0);
        NotifyDeckChanged();
    }

    // ── Save / Load / New ────────────────────────────────────────────────────
    [RelayCommand]
    private void NewDeck()
    {
        DeckEntries.Clear();
        BrowserCards.Clear();
        DeckName = "New Deck";
        CurrentDeckId = null;
        NotifyDeckChanged();
        BrowserStatus = "Search for a card to get started";
    }

    [RelayCommand]
    private async Task SaveDeckAsync()
    {
        if (DeckEntries.Count == 0 || IsSaving) return;
        IsSaving = true;
        try
        {
            if (CurrentDeckId is null)
            {
                var deck = new Deck { Name = DeckName, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                _db.Decks.Add(deck);
                await _db.SaveChangesAsync();
                CurrentDeckId = deck.Id;
            }
            else
            {
                var deck = await _db.Decks.FindAsync(CurrentDeckId.Value);
                if (deck is not null) { deck.Name = DeckName; deck.UpdatedAt = DateTime.UtcNow; }
            }

            await _db.DeckCards.Where(dc => dc.DeckId == CurrentDeckId!.Value).ExecuteDeleteAsync();
            // Clear stale tracked DeckCard entities to avoid PK conflict on re-add
            _db.ChangeTracker.Clear();
            _db.DeckCards.AddRange(DeckEntries.Select(e => new DeckCard
                { DeckId = CurrentDeckId!.Value, CardId = e.CardId, Quantity = e.Quantity }));
            await _db.SaveChangesAsync();
            await LoadSavedDecksAsync();

            if (TotalCards == 60)
            {
                await AchievementService.CheckAsync("full_deck", _db);

                var deckTotals = await _db.DeckCards
                    .GroupBy(dc => dc.DeckId)
                    .Select(g => g.Sum(dc => dc.Quantity))
                    .ToListAsync();
                if (deckTotals.Count(total => total == 60) >= 5)
                    await AchievementService.CheckAsync("five_full_decks", _db);
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task LoadDeckAsync(DeckSummaryVm summary)
    {
        var deckCards = await _db.DeckCards.Where(dc => dc.DeckId == summary.Id).ToListAsync();
        // Fusionne les éventuelles lignes dupliquées pour une même carte (données historiques)
        // afin qu'un CardId ne produise jamais deux DeckEntryVm distincts.
        var mergedCards = deckCards
            .GroupBy(dc => dc.CardId)
            .Select(g => (CardId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();
        var cardIds = mergedCards.Select(m => m.CardId).ToList();
        var cards   = await _db.CachedCards.Where(c => cardIds.Contains(c.CardId))
                                           .ToDictionaryAsync(c => c.CardId);

        DeckEntries.Clear();
        foreach (var mc in mergedCards)
        {
            if (!cards.TryGetValue(mc.CardId, out var c)) continue;
            var entry = CreateEntry(c.CardId, c.Name, c.SetId,
                _setNames.GetValueOrDefault(c.SetId, c.SetId),
                c.Number, c.Supertype, c.Subtypes,
                c.ImageSmall, c.ImageLarge, c.Rarity, c.CmLow, c.TcgLow, mc.Quantity);
            InsertEntry(entry);
        }

        CurrentDeckId = summary.Id;
        DeckName = summary.Name;
        BrowserCards.Clear();
        BrowserStatus = "Search for a card to get started";
        NotifyDeckChanged();
    }

    [RelayCommand]
    private async Task DeleteDeckAsync(DeckSummaryVm summary)
    {
        var result = MessageBox.Show($"Delete deck \"{summary.Name}\"?",
            "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        await _db.DeckCards.Where(dc => dc.DeckId == summary.Id).ExecuteDeleteAsync();
        await _db.Decks.Where(d => d.Id == summary.Id).ExecuteDeleteAsync();
        if (CurrentDeckId == summary.Id) NewDeck();
        await LoadSavedDecksAsync();
    }

    // ── Admin commands ───────────────────────────────────────────────────────
    [RelayCommand]
    private void BeginRename(DeckSummaryVm d)
    {
        d.EditingName = d.Name;
        d.IsEditing = true;
    }

    [RelayCommand]
    private async Task CommitRenameAsync(DeckSummaryVm d)
    {
        if (string.IsNullOrWhiteSpace(d.EditingName)) { d.IsEditing = false; return; }
        var deck = await _db.Decks.FindAsync(d.Id);
        if (deck is not null)
        {
            deck.Name = d.EditingName;
            deck.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            d.UpdateName(d.EditingName);
            if (CurrentDeckId == d.Id) DeckName = d.EditingName;
        }
        d.IsEditing = false;
    }

    [RelayCommand]
    private void CancelRename(DeckSummaryVm d) => d.IsEditing = false;

    [RelayCommand]
    private async Task DuplicateDeckAsync(DeckSummaryVm d)
    {
        var source = await _db.DeckCards.Where(dc => dc.DeckId == d.Id).ToListAsync();
        var copy = new Deck { Name = $"{d.Name} (copy)", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.Decks.Add(copy);
        await _db.SaveChangesAsync();
        _db.DeckCards.AddRange(source.Select(dc =>
            new DeckCard { DeckId = copy.Id, CardId = dc.CardId, Quantity = dc.Quantity }));
        await _db.SaveChangesAsync();
        await LoadSavedDecksAsync();
    }

    [RelayCommand]
    private async Task ImportPtcgoDeckAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import PTCGO deck",
            Filter = "Text file|*.txt"
        };
        if (dlg.ShowDialog() != true) return;

        var lines = await File.ReadAllLinesAsync(dlg.FileName, Encoding.UTF8);
        var deckName = Path.GetFileNameWithoutExtension(dlg.FileName);
        await ImportPtcgoLinesAsync(deckName, lines);
    }

    [RelayCommand]
    private async Task ImportPtcgoTextAsync()
    {
        var dlg = new Views.PasteDeckDialog { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true) return;
        if (string.IsNullOrWhiteSpace(dlg.DeckText))
        {
            ShowToast("No decklist text pasted");
            return;
        }

        var lines = dlg.DeckText.Split('\n');
        await ImportPtcgoLinesAsync(dlg.DeckName, lines);
    }

    private async Task ImportPtcgoLinesAsync(string deckName, IEnumerable<string> lines)
    {
        var allSets = await _db.CachedSets.ToListAsync();
        var setByPtcgo = allSets.Where(s => s.PtcgoCode != null)
            .ToDictionary(s => s.PtcgoCode!.ToUpperInvariant());
        var setByShort = allSets.Where(s => s.ShortCode != null)
            .ToDictionary(s => s.ShortCode!.ToUpperInvariant());
        var setById = allSets.ToDictionary(s => s.SetId.ToUpperInvariant());

        var newDeck = new Deck { Name = deckName, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.Decks.Add(newDeck);
        await _db.SaveChangesAsync();

        var notFound = new List<string>();
        var deckCards = new List<DeckCard>();
        var sectionHeader = new System.Text.RegularExpressions.Regex(
            @"^(Pok[eé]mon|Trainer|Energy|Total)\s*:", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("//") || sectionHeader.IsMatch(line))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || !int.TryParse(parts[0], out var qty))
            {
                notFound.Add(line);
                continue;
            }

            var number  = parts[^1];
            var setCode = parts[^2].ToUpperInvariant();

            if (!setByPtcgo.TryGetValue(setCode, out var set)
                && !setByShort.TryGetValue(setCode, out set)
                && !setById.TryGetValue(setCode, out set))
            {
                notFound.Add(line);
                continue;
            }

            var card = await _db.CachedCards
                .FirstOrDefaultAsync(c => c.SetId == set.SetId && c.Number == number);

            if (card is null) { notFound.Add(line); continue; }

            deckCards.Add(new DeckCard { DeckId = newDeck.Id, CardId = card.CardId, Quantity = qty });
        }

        if (deckCards.Count > 0)
        {
            _db.DeckCards.AddRange(deckCards);
            await _db.SaveChangesAsync();
        }

        await LoadSavedDecksAsync();

        if (notFound.Count > 0)
        {
            var msg = $"Imported {deckCards.Count} card(s).\n\n{notFound.Count} line(s) not found:\n"
                + string.Join("\n", notFound.Take(10))
                + (notFound.Count > 10 ? $"\n… and {notFound.Count - 10} more" : "");
            MessageBox.Show(msg, "Import complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ── Export PTCGO ─────────────────────────────────────────────────────────
    [RelayCommand]
    private void ExportDeckAsPtcgo()
    {
        if (DeckEntries.Count == 0) return;
        var sb = new StringBuilder();
        sb.AppendLine($"// {DeckName}");
        sb.AppendLine();

        void WriteSection(string header, IEnumerable<DeckEntryVm> entries, int count)
        {
            var list = entries.ToList();
            if (list.Count == 0) return;
            sb.AppendLine($"{header}: {count}");
            foreach (var e in list)
                sb.AppendLine($"{e.Quantity} {e.Name} {e.SetId} {e.Number}");
            sb.AppendLine();
        }

        WriteSection("Pokémon", DeckEntries.Where(e => e.Supertype == "Pokémon"), PokemonCount);
        WriteSection("Trainer", DeckEntries.Where(e => e.Supertype == "Trainer"), TrainerCount);
        WriteSection("Energy",  DeckEntries.Where(e => e.Supertype == "Energy"),  EnergyCount);

        var rest = DeckEntries.Where(e => e.Supertype is null).ToList();
        if (rest.Count > 0)
        {
            sb.AppendLine("// (unknown type)");
            foreach (var e in rest)
                sb.AppendLine($"{e.Quantity} {e.Name} {e.SetId} {e.Number}");
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export deck",
            Filter = "Text file|*.txt",
            FileName = $"{DeckName}.txt"
        };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // When Supertype is null (set not yet browsed via API), infer it from available data.
    // Subtypes is reliable when present; name heuristic covers basic energy cards.
    private static string? InferSupertype(string? supertype, string? subtypes, string name)
    {
        if (supertype is not null) return supertype;
        if (subtypes is not null)
        {
            if (subtypes.Contains("Energy",    StringComparison.OrdinalIgnoreCase)) return "Energy";
            if (subtypes.Contains("Item",      StringComparison.OrdinalIgnoreCase)
             || subtypes.Contains("Supporter", StringComparison.OrdinalIgnoreCase)
             || subtypes.Contains("Stadium",   StringComparison.OrdinalIgnoreCase)
             || subtypes.Contains("Tool",      StringComparison.OrdinalIgnoreCase)) return "Trainer";
            return "Pokémon"; // Basic, Stage 1, Stage 2, V, VMAX, VSTAR, ex, EX, GX…
        }
        if (name.EndsWith("Energy", StringComparison.OrdinalIgnoreCase)) return "Energy";
        return null;
    }

    private DeckEntryVm CreateEntry(string cardId, string name, string setId, string setName,
        string number, string? supertype, string? subtypes, string? imageUrl,
        string? imageLargeUrl, string? rarity, decimal? cmLow, decimal? tcgLow, int qty)
    {
        var resolved = InferSupertype(supertype, subtypes, name);
        var legality = _setLegality.GetValueOrDefault(setId);
        var entry = new DeckEntryVm(cardId, name, setId, setName,
            number, resolved, subtypes, imageUrl, imageLargeUrl, rarity, cmLow, tcgLow,
            legality.Standard, legality.Expanded, qty, _imageCache);
        entry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeckEntryVm.Quantity)) NotifyDeckChanged();
        };
        return entry;
    }

    private void InsertEntry(DeckEntryVm entry)
    {
        int order = entry.Supertype switch { "Pokémon" => 0, "Trainer" => 1, "Energy" => 2, _ => 3 };
        int idx = DeckEntries.Count;
        for (int i = 0; i < DeckEntries.Count; i++)
        {
            int io = DeckEntries[i].Supertype switch { "Pokémon" => 0, "Trainer" => 1, "Energy" => 2, _ => 3 };
            if (io > order) { idx = i; break; }
        }
        DeckEntries.Insert(idx, entry);
    }

    private void UpdateBrowserQty(string cardId, int qty)
    {
        var bc = BrowserCards.FirstOrDefault(c => c.CardId == cardId);
        if (bc is not null) bc.DeckQuantity = qty;
    }

    private void NotifyDeckChanged()
    {
        OnPropertyChanged(nameof(TotalCards));
        OnPropertyChanged(nameof(PokemonCount));
        OnPropertyChanged(nameof(TrainerCount));
        OnPropertyChanged(nameof(EnergyCount));
        OnPropertyChanged(nameof(UnknownCount));
        OnPropertyChanged(nameof(HasUnknown));
        OnPropertyChanged(nameof(IsDeckFull));
        OnPropertyChanged(nameof(IsValidDeck));
        OnPropertyChanged(nameof(DeckStatus));
        OnPropertyChanged(nameof(IsStandardLegal));
        OnPropertyChanged(nameof(IsExpandedLegal));
        OnPropertyChanged(nameof(StandardLegalTooltip));
        OnPropertyChanged(nameof(ExpandedLegalTooltip));
        OnPropertyChanged(nameof(TotalCmPrice));
        OnPropertyChanged(nameof(TotalTcgPrice));
        OnPropertyChanged(nameof(TotalCmPriceText));
        OnPropertyChanged(nameof(TotalTcgPriceText));
        OnPropertyChanged(nameof(PriceSummary));
        DeckChanged?.Invoke();
    }

    private async Task LoadSavedDecksAsync()
    {
        var decks = await _db.Decks.OrderByDescending(d => d.UpdatedAt).ToListAsync();
        var deckIds = decks.Select(d => d.Id).ToList();

        var counts = await _db.DeckCards
            .Where(dc => deckIds.Contains(dc.DeckId))
            .Join(_db.CachedCards, dc => dc.CardId, c => c.CardId,
                (dc, c) => new { dc.DeckId, dc.Quantity, c.Supertype })
            .GroupBy(x => new { x.DeckId, x.Supertype })
            .Select(g => new { g.Key.DeckId, g.Key.Supertype, Count = g.Sum(x => x.Quantity) })
            .ToListAsync();

        SavedDecks.Clear();
        foreach (var d in decks)
        {
            var dc = counts.Where(c => c.DeckId == d.Id).ToList();
            var pk = dc.Where(c => c.Supertype == "Pokémon").Sum(c => c.Count);
            var tr = dc.Where(c => c.Supertype == "Trainer").Sum(c => c.Count);
            var en = dc.Where(c => c.Supertype == "Energy").Sum(c => c.Count);
            SavedDecks.Add(new DeckSummaryVm(d.Id, d.Name, d.UpdatedAt, pk, tr, en));
        }
    }
}
