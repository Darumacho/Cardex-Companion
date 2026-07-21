using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;

namespace Cardex.ViewModels;

public partial class SetViewModel : ObservableObject
{
    private const string AllRarities = "All rarities";

    public string SetId { get; }
    public string Name { get; }
    public int Total { get; }
    public string Series { get; }
    public string ReleaseDate { get; }
    public string LogoUrl { get; }
    public string SymbolUrl { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private BitmapImage? _logoImage;
    [ObservableProperty] private BitmapImage? _symbolImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredCards))]
    private bool _showDupesOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredCards))]
    private bool _showWantsOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredCards))]
    private string _searchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredCards))]
    private string _selectedRarity = AllRarities;

    public ObservableCollection<CardViewModel> Cards { get; } = [];

    public IReadOnlyList<string> AvailableRarities =>
        new[] { AllRarities }
            .Concat(Cards
                .Select(c => c.Rarity ?? "")
                .Where(r => r.Length > 0)
                .Distinct()
                .OrderBy(r => r))
            .ToList();

    public IEnumerable<CardViewModel> FilteredCards
    {
        get
        {
            var cards = Cards.AsEnumerable();
            if (ShowWantsOnly)
                cards = cards.Where(c => c.IsWanted);
            if (ShowDupesOnly)
                cards = cards.Where(c => c.Quantity > 1);
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var q = SearchText.Trim();
                cards = cards.Where(c =>
                    c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    c.Number.Contains(q, StringComparison.OrdinalIgnoreCase));
            }
            if (SelectedRarity != AllRarities)
                cards = cards.Where(c => c.Rarity == SelectedRarity);
            return cards;
        }
    }

    private int _dbOwnedCount;

    public int OwnedCount => Cards.Count > 0 ? Cards.Count(c => c.IsOwned) : _dbOwnedCount;
    public int WantedCount => Cards.Count(c => c.IsWanted);

    public string CompletionText => $"{OwnedCount} / {Total}";

    public void SetPreloadedCount(int count)
    {
        _dbOwnedCount = count;
        OnPropertyChanged(nameof(OwnedCount));
        OnPropertyChanged(nameof(CompletionText));
        OnPropertyChanged(nameof(IsComplete));
    }

    public bool AllOwned => Cards.Count > 0 && Cards.All(c => c.IsOwned);

    public bool IsComplete => Cards.Count > 0
        ? Cards.All(c => c.IsOwned)
        : _dbOwnedCount > 0 && _dbOwnedCount >= Total;

    public string ToggleAllLabel => AllOwned ? "Uncheck all" : "Check all";

    public SetViewModel(string setId, string name, int total, string series, string releaseDate,
                        string logoUrl, string symbolUrl)
    {
        SetId = setId;
        Name = name;
        Total = total;
        Series = series;
        ReleaseDate = releaseDate;
        LogoUrl = logoUrl;
        SymbolUrl = symbolUrl;
    }

    public void NotifyCardsLoaded()
    {
        OnPropertyChanged(nameof(AvailableRarities));
        if (!AvailableRarities.Contains(SelectedRarity))
            SelectedRarity = AllRarities;
    }

    public void NotifyOwnershipChanged()
    {
        OnPropertyChanged(nameof(OwnedCount));
        OnPropertyChanged(nameof(CompletionText));
        OnPropertyChanged(nameof(AllOwned));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(ToggleAllLabel));
        OnPropertyChanged(nameof(FilteredCards));
    }

    public void NotifyWantsChanged()
    {
        OnPropertyChanged(nameof(WantedCount));
        OnPropertyChanged(nameof(FilteredCards));
    }
}
