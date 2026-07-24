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

    public ObservableCollection<SeriesViewModel> Series { get; } = [];
    public ObservableCollection<SetViewModel> HomeFavorites { get; } = [];
    public ObservableCollection<SearchResultViewModel> SearchResults { get; } = [];
    public ObservableCollection<SearchResultViewModel> WantedCards { get; } = [];
    public ObservableCollection<SearchResultViewModel> DuplicateCards { get; } = [];
    public bool HasWantedCards => WantedCards.Count > 0;
    public bool HasDuplicates => DuplicateCards.Count > 0;
    public bool HasHomeContent => HasWantedCards || HasDuplicates;

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

    public bool HasUpdate => PendingUpdate is not null;

    partial void OnPendingUpdateChanged(UpdateInfo? value)
        => OnPropertyChanged(nameof(HasUpdate));

    partial void OnIsUpdatingChanged(bool value)
        => InstallUpdateCommand.NotifyCanExecuteChanged();

    public MainViewModel(PokemonTcgService tcgService, ImageCacheService imageCache, AppDbContext db)
    {
        _tcgService = tcgService;
        _imageCache = imageCache;
        _db = db;
        WantedCards.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(HasWantedCards)); OnPropertyChanged(nameof(HasHomeContent)); };
        DuplicateCards.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(HasDuplicates)); OnPropertyChanged(nameof(HasHomeContent)); };
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

        int pos = 0;

        if (favorites.Count > 0)
        {
            var g = new SeriesViewModel("★ Favorites", isFavoriteGroup: true);
            foreach (var s in favorites) g.Sets.Add(s);
            Series.Insert(pos++, g);
        }

        if (collected.Count > 0)
        {
            var g = new SeriesViewModel("My Collection", isMyCollectionGroup: true);
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

        foreach (var series in Series)
            foreach (var set in series.Sets)
                if (ownedCounts.TryGetValue(set.SetId, out var count))
                    set.SetPreloadedCount(count);
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

            var cachedCards = await _db.CachedCards
                .Where(c => c.SetId == set.SetId)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();

            if (cachedCards.Count > 0)
            {
                BuildCardViewModels(cachedCards.Select(c =>
                    new CardData(c.CardId, c.Name, c.Number, c.SetId, c.ImageSmall, c.Rarity, c.CmLow, c.TcgLow, c.PricesUpdatedAt, c.CmUrl, c.TcgUrl)),
                    ownedMap, wantedIds, set);
                StatusText = $"{set.Name} — {set.CompletionText}";
                _ = RefreshPricesIfNeededAsync(set);
            }
            else
            {
                try
                {
                    var apiCards = await _tcgService.GetCardsAsync(set.SetId);
                    var now = DateTime.UtcNow;

                    _db.CachedCards.AddRange(apiCards.Select((c, i) => new CachedCard
                    {
                        CardId = c.Id, SetId = c.Set.Id, Name = c.Name,
                        Number = c.Number, ImageSmall = c.Images.Small,
                        Rarity = c.Rarity, SortOrder = i,
                        CmLow = c.Cardmarket?.Prices?.LowPrice,
                        TcgLow = ExtractTcgLow(c),
                        PricesUpdatedAt = now,
                        CmUrl = c.Cardmarket?.Url,
                        TcgUrl = c.Tcgplayer?.Url
                    }));
                    await _db.SaveChangesAsync();

                    BuildCardViewModels(apiCards.Select(c =>
                        new CardData(c.Id, c.Name, c.Number, c.Set.Id, c.Images.Small, c.Rarity,
                            c.Cardmarket?.Prices?.LowPrice, ExtractTcgLow(c), now,
                            c.Cardmarket?.Url, c.Tcgplayer?.Url)),
                        ownedMap, wantedIds, set);
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

    private void BuildCardViewModels(IEnumerable<CardData> cards, Dictionary<string, int> ownedMap, HashSet<string> wantedIds, SetViewModel set)
    {
        foreach (var card in cards)
        {
            var vm = new CardViewModel(
                card.Id, card.Name, card.Number, card.SetId,
                card.ImageSmall, card.Rarity,
                ownedMap.TryGetValue(card.Id, out var qty) ? qty : 0,
                wantedIds.Contains(card.Id),
                _imageCache)
            {
                CmLow = card.CmLow,
                TcgLow = card.TcgLow,
                PricesUpdatedAt = card.PricesUpdatedAt,
                CmUrl = card.CmUrl,
                TcgUrl = card.TcgUrl
            };

            vm.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(CardViewModel.Quantity))
                    await OnCardQuantityChangedAsync(vm, set);
                else if (e.PropertyName == nameof(CardViewModel.IsWanted))
                    await OnCardWantedChangedAsync(vm, set);
            };

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
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export collection",
            Filter = "CSV files (*.csv)|*.csv",
            FileName = "cardex_collection.csv"
        };
        if (dialog.ShowDialog() != true) return;

        var owned = await _db.OwnedCards.ToListAsync();
        if (owned.Count == 0)
        {
            StatusText = "No owned cards to export.";
            return;
        }

        var cardIds = owned.Select(o => o.CardId).ToList();
        var cards = await _db.CachedCards
            .Where(c => cardIds.Contains(c.CardId))
            .ToDictionaryAsync(c => c.CardId);

        var setIds = cards.Values.Select(c => c.SetId).Distinct().ToList();
        var sets = await _db.CachedSets
            .Where(s => setIds.Contains(s.SetId))
            .ToDictionaryAsync(s => s.SetId, s => s.Name);

        var ownedMap = owned.ToDictionary(o => o.CardId, o => o.Quantity);

        using var writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8);
        await writer.WriteLineAsync("Name,Set,Number,Rarity,Quantity,Cardmarket (€),TCGPlayer ($),Cardmarket URL,TCGPlayer URL");

        foreach (var (cardId, card) in cards.OrderBy(kv => kv.Value.SetId).ThenBy(kv => kv.Value.SortOrder))
        {
            var qty = ownedMap.GetValueOrDefault(cardId, 0);
            var setName = sets.GetValueOrDefault(card.SetId, card.SetId);
            await writer.WriteLineAsync(string.Join(",",
                CsvEscape(card.Name),
                CsvEscape(setName),
                CsvEscape(card.Number),
                CsvEscape(card.Rarity ?? ""),
                qty.ToString(),
                card.CmLow.HasValue ? card.CmLow.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "",
                card.TcgLow.HasValue ? card.TcgLow.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "",
                CsvEscape(card.CmUrl ?? ""),
                CsvEscape(card.TcgUrl ?? "")));
        }

        StatusText = $"Collection exported — {cards.Count} card(s)";
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
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
    private record CardData(string Id, string Name, string Number, string SetId, string ImageSmall, string? Rarity,
        decimal? CmLow = null, decimal? TcgLow = null, DateTime? PricesUpdatedAt = null,
        string? CmUrl = null, string? TcgUrl = null);

    private record BackupFile(string Version, DateTime ExportedAt,
        List<BackupOwned>? OwnedCards, List<BackupWanted>? WantedCards, List<string>? FavoriteSets);
    private record BackupOwned(string CardId, string SetId, int Quantity);
    private record BackupWanted(string CardId, string SetId);
}
