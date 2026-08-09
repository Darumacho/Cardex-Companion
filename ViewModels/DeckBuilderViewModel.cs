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
        string number, string imageUrl, string? supertype, string? types, string? subtypes,
        string? rarity, decimal? cmLow, decimal? tcgLow, int deckQty, bool isOwned,
        ImageCacheService imageCache)
    {
        CardId   = cardId; Name     = name;   SetId   = setId;
        SetName  = setName; Number  = number; ImageUrl = imageUrl;
        Supertype = supertype; Types = types; Subtypes = subtypes;
        Rarity   = rarity; CmLow   = cmLow;  TcgLow  = tcgLow;
        _deckQuantity = deckQty; IsOwned = isOwned;
        _imageCache = imageCache;
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
    public string CardId   { get; }
    public string Name     { get; }
    public string SetId    { get; }
    public string SetName  { get; }
    public string Number   { get; }
    public string? Supertype { get; }
    public string? Subtypes  { get; }
    public string? ImageUrl  { get; }
    public decimal? CmLow    { get; }
    public decimal? TcgLow   { get; }

    [ObservableProperty] private int _quantity;

    public bool IsBasicEnergy =>
        Supertype == "Energy" && (Subtypes is null || Subtypes.Contains("Basic Energy"));

    public int MaxQuantity => IsBasicEnergy ? 60 : 4;

    public decimal? LineCmPrice  => CmLow  is null ? null : CmLow  * Quantity;
    public decimal? LineTcgPrice => TcgLow is null ? null : TcgLow * Quantity;

    public DeckEntryVm(string cardId, string name, string setId, string setName,
        string number, string? supertype, string? subtypes,
        string? imageUrl, decimal? cmLow, decimal? tcgLow, int quantity)
    {
        CardId = cardId; Name = name; SetId = setId; SetName = setName;
        Number = number; Supertype = supertype; Subtypes = subtypes;
        ImageUrl = imageUrl; CmLow = cmLow; TcgLow = tcgLow;
        _quantity = quantity;
    }

    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(LineCmPrice));
        OnPropertyChanged(nameof(LineTcgPrice));
    }
}

public record DeckSummaryVm(int Id, string Name, DateTime UpdatedAt);
public record SetFilterItem(string Display, string Value, bool IsHeader = false);

// ─────────────────────────────────────────────────────────────────────────────
// Main ViewModel
// ─────────────────────────────────────────────────────────────────────────────

public partial class DeckBuilderViewModel : ObservableObject
{
    private readonly AppDbContext _db;
    private readonly ImageCacheService _imageCache;
    private Dictionary<string, string> _setNames = [];
    private HashSet<string> _ownedIds = [];

    // ── Browser ──────────────────────────────────────────────────────────────
    public ObservableCollection<DeckBrowserCardVm> BrowserCards { get; } = [];
    public ObservableCollection<SetFilterItem> SetOptions { get; } = [];

    [ObservableProperty] private string _cardSearch = "";
    [ObservableProperty] private string _supertypeFilter = "All";
    [ObservableProperty] private string _typeFilter = "All";
    [ObservableProperty] private string _subtypeFilter = "All";
    [ObservableProperty] private string _setFilter = "All";
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

    public decimal? TotalCmPrice => DeckEntries.Any(e => e.CmLow.HasValue)
        ? DeckEntries.Where(e => e.CmLow.HasValue).Sum(e => e.CmLow!.Value * e.Quantity)
        : null;
    public decimal? TotalTcgPrice => DeckEntries.Any(e => e.TcgLow.HasValue)
        ? DeckEntries.Where(e => e.TcgLow.HasValue).Sum(e => e.TcgLow!.Value * e.Quantity)
        : null;

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

    // ─────────────────────────────────────────────────────────────────────────
    public DeckBuilderViewModel(AppDbContext db, ImageCacheService imageCache)
    {
        _db = db;
        _imageCache = imageCache;
    }

    public async Task InitializeAsync()
    {
        _setNames = await _db.CachedSets.ToDictionaryAsync(s => s.SetId, s => s.Name);
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

        await LoadSavedDecksAsync();
    }

    // ── Recherche ────────────────────────────────────────────────────────────
    partial void OnSupertypeFilterChanged(string value) => _ = SearchCardsAsync();
    partial void OnTypeFilterChanged(string value)      => _ = SearchCardsAsync();
    partial void OnSubtypeFilterChanged(string value)   => _ = SearchCardsAsync();
    partial void OnSetFilterChanged(string value)       => _ = SearchCardsAsync();
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
                    c.Number, c.ImageSmall,
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
    [RelayCommand]
    private void AddCardToDeck(DeckBrowserCardVm card)
    {
        var existing = DeckEntries.FirstOrDefault(e => e.CardId == card.CardId);
        if (existing is not null)
        {
            if (existing.Quantity < existing.MaxQuantity && TotalCards < 60)
            {
                existing.Quantity++;
                card.DeckQuantity = existing.Quantity;
            }
        }
        else if (TotalCards < 60)
        {
            var entry = CreateEntry(card.CardId, card.Name, card.SetId,
                _setNames.GetValueOrDefault(card.SetId, card.SetId),
                card.Number, card.Supertype, card.Subtypes,
                card.ImageUrl, card.CmLow, card.TcgLow, 1);
            InsertEntry(entry);
            card.DeckQuantity = 1;
        }
        NotifyDeckChanged();
    }

    [RelayCommand]
    private void IncreaseDeckEntry(DeckEntryVm entry)
    {
        if (entry.Quantity < entry.MaxQuantity && TotalCards < 60)
        {
            entry.Quantity++;
            UpdateBrowserQty(entry.CardId, entry.Quantity);
            NotifyDeckChanged();
        }
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
        var cardIds   = deckCards.Select(dc => dc.CardId).ToList();
        var cards     = await _db.CachedCards.Where(c => cardIds.Contains(c.CardId))
                                             .ToDictionaryAsync(c => c.CardId);

        DeckEntries.Clear();
        foreach (var dc in deckCards)
        {
            if (!cards.TryGetValue(dc.CardId, out var c)) continue;
            var entry = CreateEntry(c.CardId, c.Name, c.SetId,
                _setNames.GetValueOrDefault(c.SetId, c.SetId),
                c.Number, c.Supertype, c.Subtypes,
                c.ImageSmall, c.CmLow, c.TcgLow, dc.Quantity);
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
        decimal? cmLow, decimal? tcgLow, int qty)
    {
        var resolved = InferSupertype(supertype, subtypes, name);
        var entry = new DeckEntryVm(cardId, name, setId, setName,
            number, resolved, subtypes, imageUrl, cmLow, tcgLow, qty);
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
        OnPropertyChanged(nameof(TotalCmPrice));
        OnPropertyChanged(nameof(TotalTcgPrice));
        OnPropertyChanged(nameof(PriceSummary));
    }

    private async Task LoadSavedDecksAsync()
    {
        var decks = await _db.Decks.OrderByDescending(d => d.UpdatedAt).ToListAsync();
        SavedDecks.Clear();
        foreach (var d in decks)
            SavedDecks.Add(new DeckSummaryVm(d.Id, d.Name, d.UpdatedAt));
    }
}
