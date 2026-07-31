using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cardex.Data;
using Cardex.Models;
using Cardex.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Cardex.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly PokemonTcgService _tcgService;
    private readonly ImageCacheService _imageCache;
    private readonly AppDbContext _db;
    private readonly UpdateService _updateService = new();
    private readonly AppSettings _settings;

    public ObservableCollection<SeriesViewModel> Series { get; } = [];
    public ObservableCollection<SetViewModel> HomeFavorites { get; } = [];
    public ObservableCollection<SetViewModel> HomeCollection { get; } = [];
    public ObservableCollection<SearchResultViewModel> SearchResults { get; } = [];
    public ObservableCollection<SearchResultViewModel> WantedCards { get; } = [];
    public ObservableCollection<SearchResultViewModel> DuplicateCards { get; } = [];
    public ObservableCollection<TagViewModel> Tags { get; } = [];
    public ObservableCollection<TagSectionViewModel> TagSections { get; } = [];
    public ObservableCollection<AchievementViewModel> Achievements { get; } = [];

    private List<TagViewModel> _tagsWithNone = [TagViewModel.NoTag];
    public IReadOnlyList<TagViewModel> TagsWithNone => _tagsWithNone;

    public bool HasWantedCards => WantedCards.Count > 0;
    public bool HasDuplicates  => DuplicateCards.Count > 0;
    public bool HasTags        => Tags.Count > 0;
    public bool HasHomeContent => HasWantedCards || HasDuplicates || TagSections.Count > 0;

    [ObservableProperty] private SetViewModel? _selectedSet;
    [ObservableProperty] private string _globalSearch = "";
    [ObservableProperty] private string _statusText = "Welcome to Cardex";
    [ObservableProperty] private BitmapImage? _appLogo;
    [ObservableProperty] private ImageSource? _appName;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private UpdateInfo? _pendingUpdate;
    [ObservableProperty] private bool _isUpdating;
    [ObservableProperty] private int _updateProgress;
    [ObservableProperty] private bool _allSeriesExpanded = true;
    [ObservableProperty] private bool _isBinderView;
    [ObservableProperty] private bool _isSettingsOpen;
    [ObservableProperty] private bool _showMyCollection = true;
    [ObservableProperty] private int  _totalOwnedCards;
    [ObservableProperty] private bool _isFavoritesSectionExpanded    = true;
    [ObservableProperty] private bool _isMyCollectionSectionExpanded = true;
    [ObservableProperty] private bool _isDuplicatesSectionExpanded   = true;
    [ObservableProperty] private bool _isWantedSectionExpanded       = true;
    [ObservableProperty] private string _newTagName    = "";
    [ObservableProperty] private string _pendingTagColor = "#3a7fc1";

    public System.Windows.Media.SolidColorBrush PendingTagBrush =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(PendingTagColor));

    partial void OnPendingTagColorChanged(string value) => OnPropertyChanged(nameof(PendingTagBrush));

    public string CollectionBorderColor => _settings.CollectionBorderColor;
    public string AchievementSound     => _settings.AchievementSound;

    [RelayCommand]
    private async Task UnlockKonamiCodeAsync()
        => await AchievementService.CheckAsync("konami_code", _db);

    [RelayCommand]
    private async Task UnlockBriggsAsync()
        => await AchievementService.CheckAsync("briggs", _db);

    [RelayCommand]
    private void SetAchievementSound(string value)
    {
        _settings.AchievementSound = value;
        _settings.Save();
        OnPropertyChanged(nameof(AchievementSound));
    }

    public bool HasUpdate => PendingUpdate is not null;

    partial void OnPendingUpdateChanged(UpdateInfo? value)
        => OnPropertyChanged(nameof(HasUpdate));

    partial void OnIsUpdatingChanged(bool value)
        => InstallUpdateCommand.NotifyCanExecuteChanged();

    partial void OnShowMyCollectionChanged(bool value)
    {
        _settings.ShowMyCollection = value;
        _settings.Save();
        RefreshSpecialGroups();
    }

    [RelayCommand] private void ToggleFavoritesSection()    => IsFavoritesSectionExpanded    = !IsFavoritesSectionExpanded;
    [RelayCommand] private void ToggleMyCollectionSection() => IsMyCollectionSectionExpanded = !IsMyCollectionSectionExpanded;
    [RelayCommand] private void ToggleDuplicatesSection()   => IsDuplicatesSectionExpanded   = !IsDuplicatesSectionExpanded;
    [RelayCommand] private void ToggleWantedSection()       => IsWantedSectionExpanded       = !IsWantedSectionExpanded;

    [RelayCommand] private void SelectNewTagColor(string color) => PendingTagColor = color;

    [RelayCommand]
    private async Task AddTagAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTagName)) return;
        var tag = new Models.Tag { Name = NewTagName.Trim(), Color = PendingTagColor };
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();
        Tags.Add(new TagViewModel { Id = tag.Id, Name = tag.Name, Color = tag.Color });
        NewTagName = "";
        await AchievementService.CheckAsync("first_tag", _db);
    }

    [RelayCommand]
    private async Task DeleteTagAsync(TagViewModel tagVm)
    {
        var cardTags = await _db.CardTags.Where(ct => ct.TagId == tagVm.Id).ToListAsync();
        _db.CardTags.RemoveRange(cardTags);
        var tag = await _db.Tags.FindAsync(tagVm.Id);
        if (tag is not null) _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();

        // Reset cards that had this tag (suppress callback to avoid double-write)
        foreach (var series in Series)
            foreach (var set in series.Sets)
                foreach (var card in set.Cards.Where(c => c.SelectedTag?.Id == tagVm.Id).ToList())
                {
                    var cb = card.OnTagChanged;
                    card.OnTagChanged = null;
                    card.SelectedTag = TagViewModel.NoTag;
                    card.OnTagChanged = cb;
                }

        Tags.Remove(tagVm);
        await RefreshTagSectionsAsync();
    }

    [RelayCommand]
    private async Task UpdateTagAsync(TagViewModel tagVm)
    {
        var tag = await _db.Tags.FindAsync(tagVm.Id);
        if (tag is null) return;
        tag.Name = tagVm.Name;
        tag.Color = tagVm.Color;
        await _db.SaveChangesAsync();
        await RefreshTagSectionsAsync();
    }

    private async Task LoadTagsAsync()
    {
        var tags = await _db.Tags.OrderBy(t => t.Id).ToListAsync();
        Tags.Clear();
        foreach (var t in tags)
            Tags.Add(new TagViewModel { Id = t.Id, Name = t.Name, Color = t.Color });
        _tagsWithNone = Tags.Prepend(TagViewModel.NoTag).ToList();
        OnPropertyChanged(nameof(TagsWithNone));
        OnPropertyChanged(nameof(HasTags));
        await RefreshTagSectionsAsync();
    }

    private async Task RefreshTagSectionsAsync()
    {
        var taggedCards = await _db.CardTags.ToListAsync();
        TagSections.Clear();
        if (taggedCards.Count == 0) return;

        var cardIds = taggedCards.Select(ct => ct.CardId).ToList();
        var cards = await _db.CachedCards
            .Where(c => cardIds.Contains(c.CardId))
            .OrderBy(c => c.SetId).ThenBy(c => c.SortOrder)
            .ToListAsync();

        var setIds = cards.Select(c => c.SetId).Distinct().ToList();
        var setNames = await _db.CachedSets
            .Where(s => setIds.Contains(s.SetId))
            .ToDictionaryAsync(s => s.SetId, s => s.Name);

        var ownedQty = await _db.OwnedCards
            .Where(o => cardIds.Contains(o.CardId))
            .ToDictionaryAsync(o => o.CardId, o => o.Quantity);

        foreach (var tag in Tags)
        {
            var tagCardIds = taggedCards
                .Where(ct => ct.TagId == tag.Id)
                .Select(ct => ct.CardId)
                .ToHashSet();
            if (tagCardIds.Count == 0) continue;

            var section = new TagSectionViewModel(tag);
            foreach (var c in cards.Where(c => tagCardIds.Contains(c.CardId)))
                section.Cards.Add(new SearchResultViewModel(
                    c.CardId, c.Name, c.Number, c.SetId,
                    setNames.GetValueOrDefault(c.SetId, c.SetId),
                    c.ImageSmall, c.Rarity,
                    ownedQty.GetValueOrDefault(c.CardId),
                    _imageCache));

            TagSections.Add(section);

            using var sem = new SemaphoreSlim(8, 8);
            _ = Task.WhenAll(section.Cards.Select(async vm =>
            {
                await sem.WaitAsync();
                try { await vm.LoadImageAsync(); }
                catch { }
                finally { sem.Release(); }
            }));
        }
    }

    private async Task OnCardTagChangedAsync(CardViewModel card, TagViewModel? tag)
    {
        var entry = await _db.CardTags.FindAsync(card.CardId);
        if (tag is not null)
        {
            if (entry is null) _db.CardTags.Add(new Models.CardTag { CardId = card.CardId, TagId = tag.Id });
            else entry.TagId = tag.Id;
        }
        else if (entry is not null)
        {
            _db.CardTags.Remove(entry);
        }
        await _db.SaveChangesAsync();
        await RefreshTagSectionsAsync();
    }

    public MainViewModel(PokemonTcgService tcgService, ImageCacheService imageCache, AppDbContext db)
    {
        _tcgService = tcgService;
        _imageCache = imageCache;
        _db = db;
        _settings = AppSettings.Load();
        _showMyCollection = _settings.ShowMyCollection;
        ApplyBorderColor(_settings.CollectionBorderColor);
        WantedCards.CollectionChanged    += (_, _) => { OnPropertyChanged(nameof(HasWantedCards)); OnPropertyChanged(nameof(HasHomeContent)); };
        DuplicateCards.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(HasDuplicates));  OnPropertyChanged(nameof(HasHomeContent)); };
        TagSections.CollectionChanged    += (_, _) => OnPropertyChanged(nameof(HasHomeContent));
        Tags.CollectionChanged           += (_, _) =>
        {
            _tagsWithNone = Tags.Prepend(TagViewModel.NoTag).ToList();
            OnPropertyChanged(nameof(TagsWithNone));
            OnPropertyChanged(nameof(HasTags));
        };

        AchievementService.Unlocked += def =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var idx = Achievements.IndexOf(Achievements.FirstOrDefault(a => a.Id == def.Id)!);
                if (idx >= 0)
                    Achievements[idx] = new AchievementViewModel(def,
                        new UnlockedAchievement { Id = def.Id, UnlockedAt = DateTime.UtcNow });
            });
        };
    }

    public async Task LoadAchievementsAsync()
    {
        var unlocked = await _db.UnlockedAchievements.ToListAsync();
        var map = unlocked.ToDictionary(u => u.Id);
        Achievements.Clear();
        foreach (var def in AchievementService.All)
            Achievements.Add(new AchievementViewModel(def, map.GetValueOrDefault(def.Id)));
    }

    public bool IsHomeVisible => SelectedSet is null;

    public string AppVersion
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "v?" : $"v{v.Major}.{v.Minor}";
        }
    }

    partial void OnSelectedSetChanged(SetViewModel? value)
        => OnPropertyChanged(nameof(IsHomeVisible));

    private CancellationTokenSource? _searchCts;

    partial void OnGlobalSearchChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = Task.Delay(300, token).ContinueWith(
            _ => RunSearchAsync(value, token),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private async Task RunSearchAsync(string query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            SearchResults.Clear();
            return;
        }

        try
        {
            var q = query.Trim();
            var cards = await _db.CachedCards
                .Where(c => EF.Functions.Like(c.Name, $"%{q}%") || EF.Functions.Like(c.Number, $"%{q}%"))
                .OrderBy(c => c.Name)
                .Take(100)
                .ToListAsync(token);

            if (token.IsCancellationRequested) return;

            var setIds = cards.Select(c => c.SetId).Distinct().ToList();
            var setNames = await _db.CachedSets
                .Where(s => setIds.Contains(s.SetId))
                .ToDictionaryAsync(s => s.SetId, s => s.Name, token);

            if (token.IsCancellationRequested) return;

            var cardIds = cards.Select(c => c.CardId).ToList();
            var ownedQty = await _db.OwnedCards
                .Where(o => cardIds.Contains(o.CardId))
                .ToDictionaryAsync(o => o.CardId, o => o.Quantity, token);

            if (token.IsCancellationRequested) return;

            SearchResults.Clear();
            foreach (var c in cards)
                SearchResults.Add(new SearchResultViewModel(
                    c.CardId, c.Name, c.Number, c.SetId,
                    setNames.GetValueOrDefault(c.SetId, c.SetId),
                    c.ImageSmall, c.Rarity,
                    ownedQty.GetValueOrDefault(c.CardId),
                    _imageCache));

            using var sem = new SemaphoreSlim(8, 8);
            _ = Task.WhenAll(SearchResults.Select(async vm =>
            {
                await sem.WaitAsync();
                try { await vm.LoadImageAsync(); }
                catch { }
                finally { sem.Release(); }
            }));
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private async Task OpenSearchResultAsync(SearchResultViewModel result)
    {
        var set = Series.SelectMany(s => s.Sets).FirstOrDefault(s => s.SetId == result.SetId);
        if (set is null) return;
        GlobalSearch = "";
        SearchResults.Clear();
        await SelectSetAsync(set);
    }

    [RelayCommand]
    private void GoHome()
    {
        if (SelectedSet is not null)
            SelectedSet.IsSelected = false;
        SelectedSet = null;
        GlobalSearch = "";
        SearchResults.Clear();
    }

    [RelayCommand]
    public async Task LoadSetsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = "Loading sets…";
        try
        {
            int newCount = 0;
            string? apiError = null;

            try
            {
                var apiSets = await _tcgService.GetSetsAsync();
                var cachedIds = (await _db.CachedSets.Select(s => s.SetId).ToListAsync()).ToHashSet();
                var newSets = apiSets.Where(s => !cachedIds.Contains(s.Id)).ToList();
                newCount = newSets.Count;

                if (newSets.Count > 0)
                {
                    _db.CachedSets.AddRange(newSets.Select(s => new CachedSet
                    {
                        SetId = s.Id, Name = s.Name, Series = s.Series, Total = s.Total,
                        ReleaseDate = s.ReleaseDate, LogoUrl = s.Images.Logo,
                        SymbolUrl = s.Images.Symbol, CachedAt = DateTime.UtcNow
                    }));
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                apiError = ex.Message;
            }

            var allCached = await _db.CachedSets.OrderBy(s => s.ReleaseDate).ToListAsync();

            if (allCached.Count == 0)
            {
                StatusText = apiError is null
                    ? "No sets found"
                    : $"API unavailable and no local cache — check your connection";
                return;
            }

            BuildSeries(allCached.Select(s =>
                new SetData(s.SetId, s.Name, s.Total, s.Series, s.ReleaseDate, s.LogoUrl, s.SymbolUrl)));

            StatusText = apiError is not null
                ? $"{allCached.Count} sets loaded from cache (API error: {apiError})"
                : newCount > 0
                    ? $"{allCached.Count} sets loaded — {newCount} new"
                    : $"{allCached.Count} sets loaded";

            var favoriteIds = (await _db.FavoriteSets.Select(f => f.SetId).ToListAsync()).ToHashSet();
            ApplyFavorites(favoriteIds);
            await ApplyOwnedCountsAsync();
            RefreshSpecialGroups();
            _ = LoadSymbolsAsync(Series.SelectMany(s => s.Sets).ToList());
            _ = LoadWantedCardsAsync();
            _ = LoadDuplicateCardsAsync();
            _ = LoadTagsAsync();
            _ = LoadAchievementsAsync();

            _preloadCts?.Cancel();
            _preloadCts = new CancellationTokenSource();
            _ = PreloadAllCardsAsync(_preloadCts.Token);
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RefreshSelectedSetAsync()
    {
        if (SelectedSet is null || IsBusy) return;

        var set = SelectedSet;
        set.Cards.Clear();
        set.IsLoading = true;

        await _db.CachedCards
            .Where(c => c.SetId == set.SetId)
            .ExecuteDeleteAsync();

        await AchievementService.CheckAsync("refresh_set", _db);
        await SelectSetAsync(set);
    }

    [RelayCommand]
    public async Task RefreshSetsAsync()
    {
        if (IsBusy) return;

        _preloadCts?.Cancel();

        await _db.CachedSets.ExecuteDeleteAsync();
        await _db.CachedCards.ExecuteDeleteAsync();

        SelectedSet = null;
        Series.Clear();

        await LoadSetsAsync();
    }

    private void BuildSeries(IEnumerable<SetData> data)
    {
        Series.Clear();
        var list = data.ToList();
        var grouped = list
            .GroupBy(s => s.Series)
            .OrderBy(g => g.Min(s => s.ReleaseDate));

        foreach (var group in grouped)
        {
            var seriesVm = new SeriesViewModel(group.Key);
            foreach (var s in group.OrderBy(s => s.ReleaseDate))
                seriesVm.Sets.Add(new SetViewModel(s.Id, s.Name, s.Total, s.Series, s.ReleaseDate, s.LogoUrl, s.SymbolUrl));
            Series.Add(seriesVm);
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(SetViewModel set)
    {
        var entry = await _db.FavoriteSets.FindAsync(set.SetId);
        if (set.IsFavorite)
        {
            if (entry is null)
                _db.FavoriteSets.Add(new FavoriteSet { SetId = set.SetId });
        }
        else if (entry is not null)
        {
            _db.FavoriteSets.Remove(entry);
        }
        await _db.SaveChangesAsync();
        RefreshSpecialGroups();
    }

    private void ApplyFavorites(HashSet<string> favoriteIds)
    {
        foreach (var series in Series.Where(s => !s.IsFavoriteGroup))
            foreach (var set in series.Sets)
                set.IsFavorite = favoriteIds.Contains(set.SetId);
    }

    private void RefreshSpecialGroups()
    {
        // Remove existing special groups
        foreach (var g in Series.Where(s => s.IsFavoriteGroup || s.IsMyCollectionGroup || s.IsAllSetsHeader).ToList())
            Series.Remove(g);

        var allSets = Series.SelectMany(s => s.Sets).ToList();

        var favorites = allSets.Where(s => s.IsFavorite).ToList();
        HomeFavorites.Clear();
        foreach (var s in favorites) HomeFavorites.Add(s);

        var collected = allSets
            .Where(s => s.OwnedCount > 0)
            .OrderBy(s => s.ReleaseDate)
            .ToList();

        HomeCollection.Clear();
        foreach (var s in collected) HomeCollection.Add(s);

        TotalOwnedCards = allSets.Sum(s => s.OwnedCount);

        int pos = 0;

        if (favorites.Count > 0)
        {
            var g = new SeriesViewModel("★ Favorites", isFavoriteGroup: true);
            foreach (var s in favorites) g.Sets.Add(s);
            Series.Insert(pos++, g);
        }

        if (collected.Count > 0 && ShowMyCollection)
        {
            var g = new SeriesViewModel($"My Collection  ({TotalOwnedCards})", isMyCollectionGroup: true);
            foreach (var s in collected) g.Sets.Add(s);
            Series.Insert(pos++, g);
        }

        if (Series.Count > pos)
            Series.Insert(pos, new SeriesViewModel("All Sets", isAllSetsHeader: true));
    }

    private async Task ApplyOwnedCountsAsync()
    {
        var ownedCounts = await _db.OwnedCards
            .GroupBy(o => o.SetId)
            .Select(g => new { SetId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.SetId, g => g.Count);

        var excludedCounts = await _db.ExcludedCards
            .GroupBy(e => e.SetId)
            .Select(g => new { SetId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.SetId, g => g.Count);

        var excludedOwnedCounts = await _db.ExcludedCards
            .Where(e => _db.OwnedCards.Any(o => o.CardId == e.CardId))
            .GroupBy(e => e.SetId)
            .Select(g => new { SetId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.SetId, g => g.Count);

        foreach (var series in Series)
            foreach (var set in series.Sets)
            {
                if (ownedCounts.TryGetValue(set.SetId, out var count))
                    set.SetPreloadedCount(count);
                excludedCounts.TryGetValue(set.SetId, out var excTotal);
                excludedOwnedCounts.TryGetValue(set.SetId, out var excOwned);
                set.SetPreloadedExclusionCounts(excTotal, excOwned);
            }
    }

    [RelayCommand]
    public async Task SelectSetAsync(SetViewModel set)
    {
        if (SelectedSet is not null)
            SelectedSet.IsSelected = false;

        set.IsSelected = true;
        SelectedSet = set;

        if (set.LogoImage is null)
            _ = LoadLogoAsync(set);

        if (set.Cards.Count > 0)
        {
            StatusText = $"{set.Name} — {set.CompletionText}";
            return;
        }

        set.IsLoading = true;
        StatusText = $"Loading {set.Name}…";
        try
        {
            var ownedMap = (await _db.OwnedCards
                .Where(o => o.SetId == set.SetId)
                .ToListAsync())
                .ToDictionary(o => o.CardId, o => o.Quantity);

            var wantedIds = (await _db.WantedCards
                .Where(w => w.SetId == set.SetId)
                .Select(w => w.CardId)
                .ToListAsync()).ToHashSet();

            var excludedIds = (await _db.ExcludedCards
                .Where(e => e.SetId == set.SetId)
                .Select(e => e.CardId)
                .ToListAsync()).ToHashSet();

            var cachedCards = (await _db.CachedCards
                .Where(c => c.SetId == set.SetId)
                .ToListAsync())
                .OrderBy(c => CardNumberSort(c.Number))
                .ToList();

            if (cachedCards.Count > 0)
            {
                var setCardIds = cachedCards.Select(c => c.CardId).ToList();
                var cardTagMap = await _db.CardTags
                    .Where(ct => setCardIds.Contains(ct.CardId))
                    .ToDictionaryAsync(ct => ct.CardId, ct => ct.TagId);

                BuildCardViewModels(cachedCards.Select(c =>
                    new CardData(c.CardId, c.Name, c.Number, c.SetId, c.ImageSmall, c.ImageLarge, c.Rarity, c.CmLow, c.TcgLow, c.PricesUpdatedAt, c.CmUrl, c.TcgUrl)),
                    ownedMap, wantedIds, excludedIds, set, cardTagMap);
                StatusText = $"{set.Name} — {set.CompletionText}";
                _ = RefreshPricesIfNeededAsync(set);
            }
            else
            {
                try
                {
                    var apiCards = (await _tcgService.GetCardsAsync(set.SetId))
                        .OrderBy(c => CardNumberSort(c.Number))
                        .ToList();
                    var now = DateTime.UtcNow;

                    _db.CachedCards.AddRange(apiCards.Select((c, i) => new CachedCard
                    {
                        CardId = c.Id, SetId = c.Set.Id, Name = c.Name,
                        Number = c.Number, ImageSmall = c.Images.Small,
                        ImageLarge = c.Images.Large,
                        Rarity = c.Rarity, SortOrder = i,
                        CmLow = c.Cardmarket?.Prices?.LowPrice,
                        TcgLow = ExtractTcgLow(c),
                        PricesUpdatedAt = now,
                        CmUrl = c.Cardmarket?.Url,
                        TcgUrl = c.Tcgplayer?.Url
                    }));
                    await _db.SaveChangesAsync();

                    BuildCardViewModels(apiCards.Select(c =>
                        new CardData(c.Id, c.Name, c.Number, c.Set.Id, c.Images.Small, c.Images.Large, c.Rarity,
                            c.Cardmarket?.Prices?.LowPrice, ExtractTcgLow(c), now,
                            c.Cardmarket?.Url, c.Tcgplayer?.Url)),
                        ownedMap, wantedIds, excludedIds, set);
                    StatusText = $"{set.Name} — {set.CompletionText}";
                }
                catch (Exception ex)
                {
                    StatusText = $"Could not load {set.Name} — API error: {ex.Message}";
                    return;
                }
            }

            set.NotifyCardsLoaded();
            set.NotifyOwnershipChanged();
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            set.IsLoading = false;
        }
    }

    private void BuildCardViewModels(IEnumerable<CardData> cards, Dictionary<string, int> ownedMap,
        HashSet<string> wantedIds, HashSet<string> excludedIds, SetViewModel set,
        Dictionary<string, int>? cardTagMap = null)
    {
        foreach (var card in cards)
        {
            var vm = new CardViewModel(
                card.Id, card.Name, card.Number, card.SetId,
                card.ImageSmall, card.ImageLarge, card.Rarity,
                ownedMap.TryGetValue(card.Id, out var qty) ? qty : 0,
                wantedIds.Contains(card.Id),
                excludedIds.Contains(card.Id),
                _imageCache)
            {
                CmLow = card.CmLow,
                TcgLow = card.TcgLow,
                PricesUpdatedAt = card.PricesUpdatedAt,
                CmUrl = card.CmUrl,
                TcgUrl = card.TcgUrl
            };

            // Set initial tag before wiring OnTagChanged to avoid DB write during init
            if (cardTagMap is not null && cardTagMap.TryGetValue(card.Id, out var tagId))
                vm.SelectedTag = Tags.FirstOrDefault(t => t.Id == tagId) ?? TagViewModel.NoTag;
            else
                vm.SelectedTag = TagViewModel.NoTag;

            vm.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(CardViewModel.Quantity))
                    await OnCardQuantityChangedAsync(vm, set);
                else if (e.PropertyName == nameof(CardViewModel.IsWanted))
                    await OnCardWantedChangedAsync(vm, set);
                else if (e.PropertyName == nameof(CardViewModel.IsExcluded))
                    await OnCardExcludedChangedAsync(vm, set);
            };
            vm.OnTagChanged = OnCardTagChangedAsync;

            set.Cards.Add(vm);
        }
    }

    private async Task OnCardWantedChangedAsync(CardViewModel card, SetViewModel set)
    {
        var entry = await _db.WantedCards.FindAsync(card.CardId);
        if (card.IsWanted)
        {
            if (entry is null)
                _db.WantedCards.Add(new WantedCard { CardId = card.CardId, SetId = card.SetId });
        }
        else if (entry is not null)
        {
            _db.WantedCards.Remove(entry);
        }

        await _db.SaveChangesAsync();
        set.NotifyWantsChanged();
        await LoadWantedCardsAsync();

        if (card.IsWanted)
        {
            var wantedCount = await _db.WantedCards.CountAsync();
            if (wantedCount >= 10) await AchievementService.CheckAsync("wanted_10", _db);
        }
    }

    private async Task LoadWantedCardsAsync()
    {
        var wantedIds = (await _db.WantedCards.Select(w => w.CardId).ToListAsync()).ToHashSet();
        if (wantedIds.Count == 0)
        {
            WantedCards.Clear();
            return;
        }

        var cards = await _db.CachedCards
            .Where(c => wantedIds.Contains(c.CardId))
            .OrderBy(c => c.SetId).ThenBy(c => c.SortOrder)
            .ToListAsync();

        var setIds = cards.Select(c => c.SetId).Distinct().ToList();
        var setNames = await _db.CachedSets
            .Where(s => setIds.Contains(s.SetId))
            .ToDictionaryAsync(s => s.SetId, s => s.Name);

        var ownedQty = await _db.OwnedCards
            .Where(o => wantedIds.Contains(o.CardId))
            .ToDictionaryAsync(o => o.CardId, o => o.Quantity);

        WantedCards.Clear();
        foreach (var c in cards)
            WantedCards.Add(new SearchResultViewModel(
                c.CardId, c.Name, c.Number, c.SetId,
                setNames.GetValueOrDefault(c.SetId, c.SetId),
                c.ImageSmall, c.Rarity,
                ownedQty.GetValueOrDefault(c.CardId),
                _imageCache));

        using var sem = new SemaphoreSlim(8, 8);
        _ = Task.WhenAll(WantedCards.Select(async vm =>
        {
            await sem.WaitAsync();
            try { await vm.LoadImageAsync(); }
            catch { }
            finally { sem.Release(); }
        }));
    }

    private async Task LoadDuplicateCardsAsync()
    {
        var dupes = await _db.OwnedCards.Where(o => o.Quantity > 1).ToListAsync();
        if (dupes.Count == 0)
        {
            DuplicateCards.Clear();
            return;
        }

        var cardIds = dupes.Select(o => o.CardId).ToList();
        var cards = await _db.CachedCards
            .Where(c => cardIds.Contains(c.CardId))
            .OrderBy(c => c.SetId).ThenBy(c => c.SortOrder)
            .ToListAsync();

        var setIds = cards.Select(c => c.SetId).Distinct().ToList();
        var setNames = await _db.CachedSets
            .Where(s => setIds.Contains(s.SetId))
            .ToDictionaryAsync(s => s.SetId, s => s.Name);

        var qtyMap = dupes.ToDictionary(o => o.CardId, o => o.Quantity);

        DuplicateCards.Clear();
        foreach (var c in cards)
            DuplicateCards.Add(new SearchResultViewModel(
                c.CardId, c.Name, c.Number, c.SetId,
                setNames.GetValueOrDefault(c.SetId, c.SetId),
                c.ImageSmall, c.Rarity,
                qtyMap.GetValueOrDefault(c.CardId),
                _imageCache));

        using var sem = new SemaphoreSlim(8, 8);
        _ = Task.WhenAll(DuplicateCards.Select(async vm =>
        {
            await sem.WaitAsync();
            try { await vm.LoadImageAsync(); }
            catch { }
            finally { sem.Release(); }
        }));
    }

    private async Task LoadLogoAsync(SetViewModel set)
    {
        var img = await _imageCache.GetImageAsync(set.LogoUrl, $"logo_{set.SetId}");
        set.LogoImage = img;
    }

    private async Task LoadSymbolsAsync(IReadOnlyList<SetViewModel> sets)
    {
        using var semaphore = new SemaphoreSlim(8, 8);
        var tasks = sets.Select(async set =>
        {
            await semaphore.WaitAsync();
            try
            {
                var img = await _imageCache.GetImageAsync(set.SymbolUrl, $"sym_{set.SetId}");
                set.SymbolImage = img;
            }
            catch { }
            finally { semaphore.Release(); }
        });
        await Task.WhenAll(tasks);
    }

    private CancellationTokenSource? _preloadCts;

    private async Task PreloadAllCardsAsync(CancellationToken token)
    {
        try
        {
            var cachedSetIds = (await _db.CachedCards
                .Select(c => c.SetId).Distinct()
                .ToListAsync(token)).ToHashSet();

            var toLoad = Series.SelectMany(s => s.Sets)
                .Where(s => !cachedSetIds.Contains(s.SetId))
                .ToList();

            if (toLoad.Count == 0) return;

            int done = 0, total = toLoad.Count;
            using var db = new AppDbContext();

            // Semaphore limite à 3 fetches API simultanés ; écriture DB séquentielle
            using var sem = new SemaphoreSlim(3, 3);

            async Task<(string SetId, List<ApiCard>? Cards)> FetchAsync(SetViewModel set)
            {
                await sem.WaitAsync(token);
                try
                {
                    var cards = await _tcgService.GetCardsAsync(set.SetId);
                    return (set.SetId, cards);
                }
                catch (OperationCanceledException) { throw; }
                catch { return (set.SetId, null); }
                finally { sem.Release(); }
            }

            var fetchTasks = toLoad.Select(FetchAsync).ToList();

            foreach (var task in fetchTasks)
            {
                if (token.IsCancellationRequested) break;

                var (setId, cards) = await task;
                done++;

                if (cards is not null && !await db.CachedCards.AnyAsync(c => c.SetId == setId, token))
                {
                    db.CachedCards.AddRange(cards.Select((c, i) => new CachedCard
                    {
                        CardId = c.Id, SetId = c.Set.Id, Name = c.Name,
                        Number = c.Number, ImageSmall = c.Images.Small,
                        Rarity = c.Rarity, SortOrder = i
                    }));
                    await db.SaveChangesAsync(token);
                }

                if (SelectedSet is null)
                    StatusText = $"Indexing… {done}/{total} sets cached";
            }

            if (!token.IsCancellationRequested && SelectedSet is null)
                StatusText = $"{total} sets · all cards indexed — global search ready";
        }
        catch (OperationCanceledException) { }
    }

    private bool _isBulkUpdate;

    private async Task OnCardQuantityChangedAsync(CardViewModel card, SetViewModel set)
    {
        if (_isBulkUpdate) return;

        var entry = await _db.OwnedCards.FindAsync(card.CardId);
        if (card.Quantity > 0)
        {
            if (entry is null)
                _db.OwnedCards.Add(new OwnedCard { CardId = card.CardId, SetId = card.SetId, Quantity = card.Quantity });
            else
                entry.Quantity = card.Quantity;
        }
        else if (entry is not null)
        {
            _db.OwnedCards.Remove(entry);
        }

        await _db.SaveChangesAsync();
        set.NotifyOwnershipChanged();
        RefreshSpecialGroups();
        _ = LoadDuplicateCardsAsync();
        StatusText = $"{set.Name} — {set.CompletionText}";

        if (card.Quantity > 0)
        {
            if (card.Quantity >= 5)  await AchievementService.CheckAsync("duplicate_5", _db);
            if (card.Quantity >= 20) await AchievementService.CheckAsync("duplicate_20", _db);

            var total = await _db.OwnedCards.SumAsync(o => (int?)o.Quantity) ?? 0;
            if (total >= 1000) await AchievementService.CheckAsync("collector_1000", _db);
            if (total >= 2500) await AchievementService.CheckAsync("hoarder_2500", _db);

            if (card.Name.Contains("Darmanitan", StringComparison.OrdinalIgnoreCase))
            {
                var darmanitanIds = await _db.CachedCards
                    .Where(c => EF.Functions.Like(c.Name, "%Darmanitan%"))
                    .Select(c => c.CardId).ToListAsync();
                var darmanitanTotal = await _db.OwnedCards
                    .Where(o => darmanitanIds.Contains(o.CardId) && o.Quantity > 0)
                    .CountAsync();
                if (darmanitanTotal >= 5)
                    await AchievementService.CheckAsync("darmanitan_5", _db);
            }

            if (card.Name.Contains("Garchomp", StringComparison.OrdinalIgnoreCase))
            {
                var garcompIds = await _db.CachedCards
                    .Where(c => EF.Functions.Like(c.Name, "%Garchomp%"))
                    .Select(c => c.CardId).ToListAsync();
                var garcompCount = await _db.OwnedCards
                    .Where(o => garcompIds.Contains(o.CardId) && o.Quantity > 0)
                    .CountAsync();
                if (garcompCount >= 8)
                    await AchievementService.CheckAsync("garchomp_8", _db);
            }

            if (card.Rarity == "Rare Holo Star")
            {
                var holoStarIds = await _db.CachedCards
                    .Where(c => c.Rarity == "Rare Holo Star")
                    .Select(c => c.CardId).ToListAsync();
                var holoStarCount = await _db.OwnedCards
                    .Where(o => holoStarIds.Contains(o.CardId) && o.Quantity > 0)
                    .CountAsync();
                if (holoStarCount >= 5)
                    await AchievementService.CheckAsync("rare_holo_star_5", _db);
            }

            if (card.Name.Contains("Wimpod", StringComparison.OrdinalIgnoreCase) ||
                card.Name.Contains("Golisopod", StringComparison.OrdinalIgnoreCase))
            {
                var wimpodIds = await _db.CachedCards
                    .Where(c => EF.Functions.Like(c.Name, "%Wimpod%") || EF.Functions.Like(c.Name, "%Golisopod%"))
                    .Select(c => c.CardId).ToListAsync();
                var wimpodTotal = await _db.OwnedCards
                    .Where(o => wimpodIds.Contains(o.CardId) && o.Quantity > 0)
                    .CountAsync();
                if (wimpodTotal >= 6)
                    await AchievementService.CheckAsync("wimpod_6", _db);
            }

            string[] regiNames    = ["Regice", "Registeel", "Regirock", "Regigigas", "Regieleki", "Regidrago"];
            string[] cynthiaNames = ["Spiritomb", "Roselia", "Gastrodon", "Lucario", "Milotic", "Garchomp"];

            if (regiNames.Any(n => card.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                if (await OwnsAtLeastOneOfEachAsync(regiNames))
                    await AchievementService.CheckAsync("regis", _db);

            if (cynthiaNames.Any(n => card.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                if (await OwnsAtLeastOneOfEachAsync(cynthiaNames))
                    await AchievementService.CheckAsync("cynthia_wannabe", _db);
        }
        if (set.IsComplete)
        {
            await AchievementService.CheckAsync("first_set", _db);
            var completedCount = Series.SelectMany(s => s.Sets).Count(s => s.IsComplete);
            if (completedCount >= 3) await AchievementService.CheckAsync("triple_threat", _db);
            if (set.Name.Contains("McDonald", StringComparison.OrdinalIgnoreCase))
                await AchievementService.CheckAsync("mcdonalds", _db);
            if (set.SetId == "pop5")
                await AchievementService.CheckAsync("pop5", _db);
            if (set.Series is "Base" or "Gym")
                await AchievementService.CheckAsync("genwunner", _db);
            if (set.Series is "Neo" or "E-Card")
                await AchievementService.CheckAsync("golden_gen", _db);
            if (set.Series == "EX")
                await AchievementService.CheckAsync("too_much_water", _db);
            if (set.Series is "Diamond & Pearl" or "Platinum" or "HeartGold & SoulSilver")
                await AchievementService.CheckAsync("gen4_win", _db);
            if (set.Total > 250)
                await AchievementService.CheckAsync("big_set", _db);
        }
    }

    [RelayCommand]
    public async Task ToggleAllOwnedAsync()
    {
        if (SelectedSet is null || SelectedSet.Cards.Count == 0 || SelectedSet.IsLoading) return;

        bool targetState = !SelectedSet.AllOwned;
        _isBulkUpdate = true;
        try
        {
            if (targetState)
            {
                var existingIds = (await _db.OwnedCards
                    .Where(o => o.SetId == SelectedSet.SetId)
                    .Select(o => o.CardId)
                    .ToListAsync()).ToHashSet();

                foreach (var card in SelectedSet.Cards)
                {
                    card.IsOwned = true;
                    if (!existingIds.Contains(card.CardId))
                        _db.OwnedCards.Add(new OwnedCard { CardId = card.CardId, SetId = card.SetId });
                }
            }
            else
            {
                var toRemove = await _db.OwnedCards
                    .Where(o => o.SetId == SelectedSet.SetId)
                    .ToListAsync();
                _db.OwnedCards.RemoveRange(toRemove);

                foreach (var card in SelectedSet.Cards)
                    card.IsOwned = false;
            }

            await _db.SaveChangesAsync();
            SelectedSet.NotifyOwnershipChanged();
            RefreshSpecialGroups();
            StatusText = $"{SelectedSet.Name} — {SelectedSet.CompletionText}";
        }
        finally
        {
            _isBulkUpdate = false;
        }
    }

    public async Task CheckForUpdateAsync()
    {
        var info = await _updateService.CheckAsync();
        PendingUpdate = info;
    }

    [RelayCommand(CanExecute = nameof(CanInstallUpdate))]
    private async Task InstallUpdateAsync()
    {
        if (PendingUpdate is null) return;
        IsUpdating = true;
        UpdateProgress = 0;
        var progress = new Progress<int>(p => UpdateProgress = p);
        await _updateService.DownloadAndInstallAsync(PendingUpdate, progress);
    }

    private bool CanInstallUpdate() => !IsUpdating;

    private async Task<bool> OwnsAtLeastOneOfEachAsync(string[] names)
    {
        foreach (var name in names)
        {
            var ids = await _db.CachedCards
                .Where(c => EF.Functions.Like(c.Name, $"%{name}%"))
                .Select(c => c.CardId)
                .ToListAsync();
            if (ids.Count == 0 || !await _db.OwnedCards.AnyAsync(o => ids.Contains(o.CardId) && o.Quantity > 0))
                return false;
        }
        return true;
    }

    private Views.SettingsWindow? _settingsWindow;

    [RelayCommand]
    private void ToggleSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Close();
            return;
        }
        _settingsWindow = new Views.SettingsWindow { DataContext = this };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    [RelayCommand]
    private void SetBorderColor(string hex)
    {
        _settings.CollectionBorderColor = hex;
        _settings.Save();
        ApplyBorderColor(hex);
        OnPropertyChanged(nameof(CollectionBorderColor));
    }

    private static void ApplyBorderColor(string hex)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            System.Windows.Application.Current.Resources["CollectionBorderBrush"] =
                new System.Windows.Media.SolidColorBrush(color);
        }
        catch { }
    }

    private async Task OnCardExcludedChangedAsync(CardViewModel card, SetViewModel set)
    {
        var entry = await _db.ExcludedCards.FindAsync(card.CardId);
        if (card.IsExcluded)
        {
            if (entry is null)
                _db.ExcludedCards.Add(new ExcludedCard { CardId = card.CardId, SetId = card.SetId });
        }
        else if (entry is not null)
        {
            _db.ExcludedCards.Remove(entry);
        }
        await _db.SaveChangesAsync();
        set.NotifyExclusionChanged();
        StatusText = $"{set.Name} — {set.CompletionText}";

        if (card.IsExcluded)
        {
            var excludedCount = await _db.ExcludedCards.CountAsync();
            if (excludedCount >= 10) await AchievementService.CheckAsync("excluded_10", _db);
        }
    }

    [RelayCommand]
    private void ToggleAllSeries()
    {
        AllSeriesExpanded = !AllSeriesExpanded;
        foreach (var s in Series.Where(s => !s.IsFavoriteGroup && !s.IsMyCollectionGroup && !s.IsAllSetsHeader))
            s.IsExpanded = AllSeriesExpanded;
    }

    [RelayCommand]
    private async Task BackupCollectionAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Backup collection",
            Filter = "Cardex backup (*.cardex)|*.cardex",
            FileName = $"cardex_backup_{DateTime.Now:yyyy-MM-dd}"
        };
        if (dialog.ShowDialog() != true) return;

        var owned    = await _db.OwnedCards.ToListAsync();
        var wanted   = await _db.WantedCards.ToListAsync();
        var favorite = await _db.FavoriteSets.ToListAsync();

        var backup = new BackupFile(
            Version: "1.3",
            ExportedAt: DateTime.UtcNow,
            OwnedCards: owned.Select(o => new BackupOwned(o.CardId, o.SetId, o.Quantity)).ToList(),
            WantedCards: wanted.Select(w => new BackupWanted(w.CardId, w.SetId)).ToList(),
            FavoriteSets: favorite.Select(f => f.SetId).ToList());

        var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(dialog.FileName, json, Encoding.UTF8);

        StatusText = $"Backup saved — {owned.Count} owned, {wanted.Count} wanted, {favorite.Count} favorite set(s)";
        await AchievementService.CheckAsync("backup_used", _db);
    }

    [RelayCommand]
    private async Task RestoreCollectionAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Restore collection",
            Filter = "Cardex backup (*.cardex)|*.cardex"
        };
        if (dialog.ShowDialog() != true) return;

        var result = MessageBox.Show(
            "Restoring will replace your current collection, want list and favorites.\n\nContinue?",
            "Restore collection",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            var json = await File.ReadAllTextAsync(dialog.FileName, Encoding.UTF8);
            var backup = JsonSerializer.Deserialize<BackupFile>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (backup is null) { StatusText = "Restore failed — invalid file."; return; }

            await _db.OwnedCards.ExecuteDeleteAsync();
            await _db.WantedCards.ExecuteDeleteAsync();
            await _db.FavoriteSets.ExecuteDeleteAsync();

            if (backup.OwnedCards?.Count > 0)
                _db.OwnedCards.AddRange(backup.OwnedCards.Select(o =>
                    new OwnedCard { CardId = o.CardId, SetId = o.SetId, Quantity = o.Quantity }));

            if (backup.WantedCards?.Count > 0)
                _db.WantedCards.AddRange(backup.WantedCards.Select(w =>
                    new WantedCard { CardId = w.CardId, SetId = w.SetId }));

            if (backup.FavoriteSets?.Count > 0)
                _db.FavoriteSets.AddRange(backup.FavoriteSets.Select(s =>
                    new FavoriteSet { SetId = s }));

            await _db.SaveChangesAsync();

            // Recharger l'UI
            var favoriteIds = backup.FavoriteSets?.ToHashSet() ?? [];
            ApplyFavorites(favoriteIds);
            await ApplyOwnedCountsAsync();
            RefreshSpecialGroups();
            await LoadWantedCardsAsync();
            await LoadDuplicateCardsAsync();

            // Vider le set ouvert pour forcer un rechargement
            if (SelectedSet is not null)
            {
                SelectedSet.Cards.Clear();
                await SelectSetAsync(SelectedSet);
            }

            StatusText = $"Collection restored — {backup.OwnedCards?.Count ?? 0} owned, {backup.WantedCards?.Count ?? 0} wanted";
        }
        catch (Exception ex)
        {
            StatusText = $"Restore failed — {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var exportDlg = new Views.ExportDialog { Owner = Application.Current.MainWindow };
        if (exportDlg.ShowDialog() != true) return;

        var mode   = exportDlg.SelectedMode;
        var format = exportDlg.SelectedFormat;

        var saveDlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export CSV",
            Filter = "CSV files (*.csv)|*.csv",
            FileName = CsvFileName(mode, format)
        };
        if (saveDlg.ShowDialog() != true) return;

        var count = await WriteCsvAsync(saveDlg.FileName, mode, format);
        StatusText = $"Exported {count} card(s)";
    }

    private static string CsvFileName(Views.ExportMode mode, Views.ExportFormat format)
    {
        string m = mode switch
        {
            Views.ExportMode.Wants      => "wants",
            Views.ExportMode.Duplicates => "duplicates",
            Views.ExportMode.Missing    => "missing",
            _                           => "collection"
        };
        string f = format switch { Views.ExportFormat.Cardmarket => "_cardmarket", Views.ExportFormat.TcgPlayer => "_tcgplayer", _ => "" };
        return $"cardex_{m}{f}.csv";
    }

    private async Task<int> WriteCsvAsync(string path, Views.ExportMode mode, Views.ExportFormat format)
    {
        List<string> cardIds;
        Dictionary<string, int> qtyMap;

        if (mode == Views.ExportMode.Wants)
        {
            var wanted = await _db.WantedCards.ToListAsync();
            if (wanted.Count == 0) return 0;
            cardIds = wanted.Select(w => w.CardId).ToList();
            var ownedQty = await _db.OwnedCards.Where(o => cardIds.Contains(o.CardId))
                .ToDictionaryAsync(o => o.CardId, o => o.Quantity);
            qtyMap = cardIds.ToDictionary(id => id, id => ownedQty.GetValueOrDefault(id, 0));
        }
        else if (mode == Views.ExportMode.Duplicates)
        {
            var dupes = await _db.OwnedCards.Where(o => o.Quantity > 1).ToListAsync();
            if (dupes.Count == 0) return 0;
            cardIds = dupes.Select(o => o.CardId).ToList();
            qtyMap = dupes.ToDictionary(o => o.CardId, o => o.Quantity);
        }
        else if (mode == Views.ExportMode.Missing)
        {
            var ownedIds = (await _db.OwnedCards.Where(o => o.Quantity > 0).Select(o => o.CardId).ToListAsync()).ToHashSet();
            var excludedIds = (await _db.ExcludedCards.Select(e => e.CardId).ToListAsync()).ToHashSet();
            var missing = await _db.CachedCards
                .Where(c => !ownedIds.Contains(c.CardId) && !excludedIds.Contains(c.CardId))
                .ToListAsync();
            if (missing.Count == 0) return 0;
            cardIds = missing.Select(c => c.CardId).ToList();
            qtyMap = cardIds.ToDictionary(id => id, _ => 0);
        }
        else
        {
            var owned = await _db.OwnedCards.ToListAsync();
            if (owned.Count == 0) return 0;
            cardIds = owned.Select(o => o.CardId).ToList();
            qtyMap = owned.ToDictionary(o => o.CardId, o => o.Quantity);
        }

        var cards = await _db.CachedCards.Where(c => cardIds.Contains(c.CardId)).ToDictionaryAsync(c => c.CardId);
        var setIds = cards.Values.Select(c => c.SetId).Distinct().ToList();
        var sets = await _db.CachedSets.Where(s => setIds.Contains(s.SetId)).ToDictionaryAsync(s => s.SetId, s => s.Name);
        var ordered = cards.OrderBy(kv => kv.Value.SetId).ThenBy(kv => kv.Value.SortOrder).ToList();

        using var writer = new StreamWriter(path, false, Encoding.UTF8);

        if (format == Views.ExportFormat.Cardmarket)
        {
            await writer.WriteLineAsync("Name;Edition;Quantity");
            foreach (var (cardId, card) in ordered)
                await writer.WriteLineAsync(string.Join(";",
                    CsvEscapeSemicolon(card.Name),
                    CsvEscapeSemicolon(sets.GetValueOrDefault(card.SetId, card.SetId)),
                    qtyMap.GetValueOrDefault(cardId, 0)));
        }
        else if (format == Views.ExportFormat.TcgPlayer)
        {
            await writer.WriteLineAsync("Quantity,Name,Set Name,Number,Condition,Language");
            foreach (var (cardId, card) in ordered)
                await writer.WriteLineAsync(string.Join(",",
                    qtyMap.GetValueOrDefault(cardId, 0),
                    CsvEscape(card.Name),
                    CsvEscape(sets.GetValueOrDefault(card.SetId, card.SetId)),
                    CsvEscape(card.Number),
                    "Near Mint", "English"));
        }
        else
        {
            await writer.WriteLineAsync("Name,Set,Number,Rarity,Quantity,Cardmarket (€),TCGPlayer ($),Cardmarket URL,TCGPlayer URL");
            foreach (var (cardId, card) in ordered)
            {
                var qty = qtyMap.GetValueOrDefault(cardId, 0);
                await writer.WriteLineAsync(string.Join(",",
                    CsvEscape(card.Name),
                    CsvEscape(sets.GetValueOrDefault(card.SetId, card.SetId)),
                    CsvEscape(card.Number),
                    CsvEscape(card.Rarity ?? ""),
                    qty,
                    card.CmLow.HasValue ? card.CmLow.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "",
                    card.TcgLow.HasValue ? card.TcgLow.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "",
                    CsvEscape(card.CmUrl ?? ""),
                    CsvEscape(card.TcgUrl ?? "")));
            }
        }

        return cards.Count;
    }

    private static (int, string, int) CardNumberSort(string? number)
    {
        if (number is null) return (1, "", int.MaxValue);
        if (int.TryParse(number, out int n)) return (0, "", n);
        var m = System.Text.RegularExpressions.Regex.Match(number, @"^([A-Za-z]+)(\d+)$");
        if (m.Success) return (1, m.Groups[1].Value, int.Parse(m.Groups[2].Value));
        return (1, number, 0);
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string CsvEscapeSemicolon(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static decimal? ExtractTcgLow(ApiCard card)
    {
        if (card.Tcgplayer?.Prices is null) return null;
        var lows = card.Tcgplayer.Prices.Values
            .Where(p => p.Low.HasValue).Select(p => p.Low!.Value);
        return lows.Any() ? lows.Min() : null;
    }

    private async Task RefreshPricesIfNeededAsync(SetViewModel set)
    {
        try
        {
            var sample = await _db.CachedCards.Where(c => c.SetId == set.SetId).FirstOrDefaultAsync();
            if (sample?.PricesUpdatedAt > DateTime.UtcNow.AddHours(-24)) return;

            var apiCards = await _tcgService.GetCardsAsync(set.SetId);
            var cardMap = apiCards.ToDictionary(c => c.Id);
            var now = DateTime.UtcNow;

            var cached = await _db.CachedCards.Where(c => c.SetId == set.SetId).ToListAsync();
            foreach (var row in cached)
            {
                if (!cardMap.TryGetValue(row.CardId, out var api)) continue;
                row.CmLow = api.Cardmarket?.Prices?.LowPrice;
                row.TcgLow = ExtractTcgLow(api);
                row.PricesUpdatedAt = now;
                row.CmUrl = api.Cardmarket?.Url;
                row.TcgUrl = api.Tcgplayer?.Url;
            }
            await _db.SaveChangesAsync();

            foreach (var vm in set.Cards)
            {
                if (!cardMap.TryGetValue(vm.CardId, out var api)) continue;
                vm.CmLow = api.Cardmarket?.Prices?.LowPrice;
                vm.TcgLow = ExtractTcgLow(api);
                vm.PricesUpdatedAt = now;
                vm.CmUrl = api.Cardmarket?.Url;
                vm.TcgUrl = api.Tcgplayer?.Url;
            }
        }
        catch { }
    }

    private record SetData(string Id, string Name, int Total, string Series, string ReleaseDate, string LogoUrl, string SymbolUrl);
    private record CardData(string Id, string Name, string Number, string SetId, string ImageSmall, string? ImageLarge, string? Rarity,
        decimal? CmLow = null, decimal? TcgLow = null, DateTime? PricesUpdatedAt = null,
        string? CmUrl = null, string? TcgUrl = null);

    private record BackupFile(string Version, DateTime ExportedAt,
        List<BackupOwned>? OwnedCards, List<BackupWanted>? WantedCards, List<string>? FavoriteSets);
    private record BackupOwned(string CardId, string SetId, int Quantity);
    private record BackupWanted(string CardId, string SetId);
}
