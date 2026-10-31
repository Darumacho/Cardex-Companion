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
using Avalonia.Threading;
using Cardex.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Cardex.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly PokemonTcgService _tcgService;
    private readonly TCGdexService _tcgdexService;
    private readonly ImageCacheService _imageCache;
    private readonly AppDbContext _db;
    private readonly CardexApiService _cardexApi;
    private readonly UpdateService _updateService = new();
    private readonly PokeSpriteService _pokeSprite = new();
    private readonly AppSettings _settings;

    public ObservableCollection<LanguageOptionViewModel> LanguageOptions { get; } = [];
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
    [ObservableProperty] private Bitmap? _appLogo;
    [ObservableProperty] private Bitmap? _appName;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private UpdateInfo? _pendingUpdate;
    [ObservableProperty] private bool _isUpdating;
    [ObservableProperty] private int _updateProgress;
    [ObservableProperty] private bool _allSeriesExpanded = true;
    [ObservableProperty] private List<TemplateSetEntry> _templateSets = [];
    [ObservableProperty] private bool _isBinderView;
    [ObservableProperty] private bool _isSettingsOpen;

    private int _selectedTab;
    public int SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (!SetProperty(ref _selectedTab, value)) return;
            OnPropertyChanged(nameof(IsCollectionTabActive));
            OnPropertyChanged(nameof(IsDeckBuilderTabActive));
            OnPropertyChanged(nameof(IsHomeVisible));
            OnPropertyChanged(nameof(IsSetViewVisible));
            if (value == 1 && SelectedSet is not null) { SelectedSet.IsSelected = false; SelectedSet = null; }
        }
    }

    public bool IsCollectionTabActive  => _selectedTab == 0;
    public bool IsDeckBuilderTabActive => _selectedTab == 1;

    public DeckBuilderViewModel DeckBuilder { get; }
    [ObservableProperty] private bool _showMyCollection = true;
    [ObservableProperty] private bool _showStats = true;
    [ObservableProperty] private bool _statsTeamMode = false;
    [ObservableProperty] private bool _showLegalityCheck = true;
    [ObservableProperty] private int  _totalOwnedCards;
    [ObservableProperty] private bool _isFavoritesSectionExpanded    = true;
    [ObservableProperty] private bool _isMyCollectionSectionExpanded = true;
    [ObservableProperty] private bool _isDuplicatesSectionExpanded   = true;
    [ObservableProperty] private bool _isWantedSectionExpanded       = true;
    [ObservableProperty] private bool _isStatsSectionExpanded        = false;
    [ObservableProperty] private bool _isApiAnnouncementVisible      = true;
    [ObservableProperty] private string _newTagName    = "";
    [ObservableProperty] private string _pendingTagColor = "#3a7fc1";
    [ObservableProperty] private bool _isSyncing;

    public Avalonia.Media.SolidColorBrush PendingTagBrush =>
        new(Avalonia.Media.Color.Parse(PendingTagColor));

    partial void OnPendingTagColorChanged(string value) => OnPropertyChanged(nameof(PendingTagBrush));

    public string CollectionBorderColor => _settings.CollectionBorderColor;
    public string AchievementSound     => _settings.AchievementSound;

    public string CollectionSyncApiKey
    {
        get => _settings.CollectionSyncApiKey ?? "";
        set
        {
            // Trim défensif : une clé copiée-collée depuis l'email de vérification embarque
            // facilement un espace ou un retour à la ligne, ce qui casse la comparaison exacte
            // côté serveur (`k.KeyValue == keyValue`) et se traduit par un 401 trompeur.
            _settings.CollectionSyncApiKey = value.Trim();
            _settings.Save();
            OnPropertyChanged();
        }
    }

    public string LastSyncStatusText => _settings.LastCollectionSyncAt is { } at
        ? $"Last synced {at.ToLocalTime():dd/MM/yyyy HH:mm} (from {_settings.LastCollectionSyncOrigin})"
        : "Never synced yet";

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

    partial void OnShowStatsChanged(bool value)
    {
        _settings.ShowStats = value;
        _settings.Save();
    }

    partial void OnStatsTeamModeChanged(bool value)
    {
        _settings.StatsTeamMode = value;
        _settings.Save();
    }

    private void OnLanguageOptionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LanguageOptionViewModel.IsTracked))
            OnLanguageOptionToggled();
    }

    // Resynchronise les checkboxes Settings → Languages avec _settings.TrackedLanguages sans
    // repasser par OnLanguageOptionToggled (qui recharge le set ouvert) — utilisé après une
    // synchro qui vient d'écraser TrackedLanguages, pour éviter un double rechargement concurrent
    // avec celui que ReloadAfterSyncApplyAsync fait déjà juste après.
    private void RefreshLanguageOptionsFromSettings()
    {
        foreach (var opt in LanguageOptions)
        {
            var shouldTrack = _settings.TrackedLanguages.Contains(opt.Code);
            if (opt.IsTracked == shouldTrack) continue;
            opt.PropertyChanged -= OnLanguageOptionPropertyChanged;
            opt.IsTracked = shouldTrack;
            opt.PropertyChanged += OnLanguageOptionPropertyChanged;
        }
    }

    private async void OnLanguageOptionToggled()
    {
        // L'anglais reste toujours suivi (données existantes implicitement en anglais) — défensif,
        // la case est aussi désactivée côté UI.
        var en = LanguageOptions.First(o => o.Code == "EN");
        if (!en.IsTracked) en.IsTracked = true;

        _settings.TrackedLanguages = LanguageOptions.Where(o => o.IsTracked).Select(o => o.Code).ToList();
        _settings.Save();

        // IsMultiLanguageMode est figé à la construction des CardViewModel : les sets déjà chargés
        // doivent être reconstruits pour refléter le nouveau réglage (le set ouvert tout de suite,
        // les autres au prochain SelectSetAsync grâce au Cards.Clear()).
        foreach (var series in Series)
            foreach (var set in series.Sets)
                if (set != SelectedSet && set.Cards.Count > 0)
                    set.Cards.Clear();

        if (SelectedSet is not null)
        {
            SelectedSet.Cards.Clear();
            await SelectSetAsync(SelectedSet);
        }
    }

    partial void OnShowLegalityCheckChanged(bool value)
    {
        _settings.ShowLegalityCheck = value;
        _settings.Save();
    }

    [RelayCommand] private void ToggleFavoritesSection()    => IsFavoritesSectionExpanded    = !IsFavoritesSectionExpanded;
    [RelayCommand] private void ToggleMyCollectionSection() => IsMyCollectionSectionExpanded = !IsMyCollectionSectionExpanded;
    [RelayCommand] private void ToggleDuplicatesSection()   => IsDuplicatesSectionExpanded   = !IsDuplicatesSectionExpanded;
    [RelayCommand] private void ToggleWantedSection()       => IsWantedSectionExpanded       = !IsWantedSectionExpanded;
    [RelayCommand] private void ToggleStatsSection()        => IsStatsSectionExpanded        = !IsStatsSectionExpanded;

    partial void OnIsFavoritesSectionExpandedChanged(bool value)
    {
        _settings.HomeFavoritesExpanded = value;
        _settings.Save();
    }

    partial void OnIsMyCollectionSectionExpandedChanged(bool value)
    {
        _settings.HomeMyCollectionExpanded = value;
        _settings.Save();
    }

    partial void OnIsDuplicatesSectionExpandedChanged(bool value)
    {
        _settings.HomeDuplicatesExpanded = value;
        _settings.Save();
    }

    partial void OnIsStatsSectionExpandedChanged(bool value)
    {
        _settings.HomeStatsExpanded = value;
        _settings.Save();
    }

    partial void OnIsWantedSectionExpandedChanged(bool value)
    {
        _settings.HomeWantedExpanded = value;
        _settings.Save();
    }

    [RelayCommand]
    private void DismissApiAnnouncement()
    {
        IsApiAnnouncementVisible = false;
        _settings.ApiAnnouncementDismissed = true;
        _settings.Save();
    }

    [RelayCommand]
    private void OpenApiWebsite()
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://cardex-api.dev/") { UseShellExecute = true });

    [RelayCommand]
    private void OpenMobileAppReleases()
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/Darumacho/CardexMobile/releases") { UseShellExecute = true });

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

    public MainViewModel(PokemonTcgService tcgService, TCGdexService tcgdexService, ImageCacheService imageCache, AppDbContext db, CardexApiService cardexApi)
    {
        _tcgService = tcgService;
        _tcgdexService = tcgdexService;
        _imageCache = imageCache;
        _db = db;
        _cardexApi = cardexApi;
        _settings = AppSettings.Load();
        DeckBuilder = new DeckBuilderViewModel(db, imageCache);
        _showMyCollection = _settings.ShowMyCollection;
        _showStats = _settings.ShowStats;
        _statsTeamMode = _settings.StatsTeamMode;
        _showLegalityCheck = _settings.ShowLegalityCheck;
        _isFavoritesSectionExpanded = _settings.HomeFavoritesExpanded;
        _isMyCollectionSectionExpanded = _settings.HomeMyCollectionExpanded;
        _isDuplicatesSectionExpanded = _settings.HomeDuplicatesExpanded;
        _isWantedSectionExpanded = _settings.HomeWantedExpanded;
        _isStatsSectionExpanded = _settings.HomeStatsExpanded;
        _isApiAnnouncementVisible = !_settings.ApiAnnouncementDismissed;
        ApplyBorderColor(_settings.CollectionBorderColor);

        foreach (var (code, label) in LanguageCatalog.All)
        {
            var opt = new LanguageOptionViewModel(code, label, _settings.TrackedLanguages.Contains(code), isLocked: code == "EN");
            opt.PropertyChanged += OnLanguageOptionPropertyChanged;
            LanguageOptions.Add(opt);
        }

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
            Dispatcher.UIThread.Invoke(() =>
            {
                var idx = Achievements.IndexOf(Achievements.FirstOrDefault(a => a.Id == def.Id)!);
                if (idx >= 0)
                    Achievements[idx] = new AchievementViewModel(def,
                        new UnlockedAchievement { Id = def.Id, UnlockedAt = DateTime.UtcNow });
            });
        };

        DeckBuilder.DeckChanged += () =>
        {
            if (SelectedSet is null) return;
            var deckMap = DeckBuilder.DeckEntries
                .GroupBy(e => e.CardId)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity));
            foreach (var card in SelectedSet.Cards)
                card.DeckQuantity = deckMap.GetValueOrDefault(card.CardId, 0);
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

    public bool IsHomeVisible    => _selectedTab == 0 && SelectedSet is null;
    public bool IsSetViewVisible => _selectedTab == 0 && SelectedSet is not null;

    public string AppVersion
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "v?" : $"v{v.Major}.{v.Minor}";
        }
    }

    partial void OnSelectedSetChanged(SetViewModel? value)
    {
        OnPropertyChanged(nameof(IsHomeVisible));
        OnPropertyChanged(nameof(IsSetViewVisible));
    }

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
        SelectedTab = 0;
        GlobalSearch = "";
        SearchResults.Clear();
    }

    [RelayCommand]
    private void SwitchToCollection()
    {
        GlobalSearch = "";
        SearchResults.Clear();
        SelectedTab = 0;
    }

    [RelayCommand]
    private async Task SwitchToDeckBuilderAsync()
    {
        GlobalSearch = "";
        SearchResults.Clear();
        SelectedTab = 1;
        await DeckBuilder.InitializeAsync();
    }

    [RelayCommand]
    private void AddCardToDeck(CardViewModel card)
    {
        var setName = Series.SelectMany(s => s.Sets)
            .FirstOrDefault(s => s.SetId == card.SetId)?.Name ?? card.SetId;
        DeckBuilder.AddCardFromCollection(
            card.CardId, card.Name, card.SetId, setName,
            card.Number, card.Supertype, card.Subtypes,
            card.ImageUrl, card.ImageLargeUrl, card.Rarity, card.CmLow, card.TcgLow);
        StatusText = $"{card.Name} ajouté au deck";
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
                        SymbolUrl = s.Images.Symbol, CachedAt = DateTime.UtcNow,
                        PtcgoCode = s.PtcgoCode,
                        ShortCode = SetShortCodes.BySetId.TryGetValue(s.Id, out var sc) ? sc : null,
                        StandardLegal = s.Legalities?.Standard == "Legal",
                        ExpandedLegal = s.Legalities?.Expanded == "Legal"
                    }));
                    await _db.SaveChangesAsync();
                }

                // Backfill Standard/Expanded legality for sets cached before this field existed
                // (or whose legality changed, e.g. a rotation update) — reuses the sets already
                // fetched above, no extra API calls.
                var apiById = apiSets.ToDictionary(s => s.Id);
                var existingSets = await _db.CachedSets.Where(s => cachedIds.Contains(s.SetId)).ToListAsync();
                var legalityChanged = false;
                foreach (var cs in existingSets)
                {
                    if (!apiById.TryGetValue(cs.SetId, out var api)) continue;
                    var std = api.Legalities?.Standard == "Legal";
                    var exp = api.Legalities?.Expanded == "Legal";
                    if (cs.StandardLegal != std || cs.ExpandedLegal != exp)
                    {
                        cs.StandardLegal = std;
                        cs.ExpandedLegal = exp;
                        legalityChanged = true;
                    }
                }
                if (legalityChanged) await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                apiError = ex.Message;
            }

            // Les sets absents de pokemontcg.io (promos Mega Evolution, Trainer Kits, etc.) sont
            // ajoutés une fois pour toutes via le seed embarqué TCGdex (App.SeedTcgdexFallbackAsync,
            // appelé avant LoadSetsAsync) plutôt que par découverte live à chaque démarrage.

            var allCached = await _db.CachedSets.OrderBy(s => s.ReleaseDate).ToListAsync();

            if (allCached.Count == 0)
            {
                StatusText = apiError is null
                    ? "No sets found"
                    : $"API unavailable and no local cache — check your connection";
                return;
            }

            BuildSeries(allCached.Select(s =>
                new SetData(s.SetId, s.Name, s.Total, s.Series, s.ReleaseDate, s.LogoUrl, s.SymbolUrl, s.Source, s.ShortCode)));

            TemplateSets = allCached
                .Select(s => new TemplateSetEntry(s.ShortCode ?? "", s.PtcgoCode ?? s.SetId, s.Name, s.Series))
                .ToList();

            StatusText = apiError is not null
                ? $"{allCached.Count} sets loaded from cache (API error: {apiError})"
                : newCount > 0
                    ? $"{allCached.Count} sets loaded — {newCount} new"
                    : $"{allCached.Count} sets loaded";

            var favoriteIds = (await _db.FavoriteSets.Select(f => f.SetId).ToListAsync()).ToHashSet();
            ApplyFavorites(favoriteIds);
            var ignoredIds = (await _db.IgnoredSets.Select(i => i.SetId).ToListAsync()).ToHashSet();
            ApplyIgnored(ignoredIds);
            await ApplyOwnedCountsAsync();
            RefreshSpecialGroups();
            _ = LoadSymbolsAsync(Series.SelectMany(s => s.Sets).ToList());
            _ = LoadWantedCardsAsync();
            _ = LoadDuplicateCardsAsync();
            _ = LoadTagsAsync();
            await LoadAchievementsAsync();
            _ = RefreshStatsAsync();

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
        StatusText = $"Refreshing {set.Name}…";

        // L'API Cardex est nettement plus rapide/fiable que pokemontcg.io pour cette opération
        // précise (mesuré : ~0,3-0,4s et 100% de réussite contre 2-8s et de fréquents 500/502 sur
        // pokemontcg.io, y compris sur des sets conséquents comme Cosmic Eclipse/Ascended Heroes) —
        // le bouton reload la favorise donc en priorité, avec repli sur pokemontcg.io (chemin
        // existant, via SelectSetAsync) si Cardex échoue ou renvoie un résultat incomplet.
        // Scope volontairement limité au reload : le chargement initial d'un set non encore en
        // cache continue de passer par pokemontcg.io/TCGdex comme avant, non touché ici.
        try
        {
            var cardexCards = await _cardexApi.GetSetCardsAsync(set.SetId);
            if (cardexCards.Count > 0 && (set.Total == 0 || cardexCards.Count >= set.Total))
            {
                await _db.CachedCards.Where(c => c.SetId == set.SetId).ExecuteDeleteAsync();
                var now = DateTime.UtcNow;
                _db.CachedCards.AddRange(cardexCards.Select((c, i) => new CachedCard
                {
                    CardId = c.Id, SetId = set.SetId, Name = c.Name, Number = c.Number,
                    ImageSmall = c.ImageSmall ?? "", ImageLarge = c.ImageLarge,
                    Rarity = c.Rarity, SortOrder = i,
                    CmLow = c.CardmarketLowPrice, TcgLow = c.TcgplayerLowPrice, PricesUpdatedAt = now,
                    CmUrl = c.CardmarketUrl, TcgUrl = c.TcgplayerUrl,
                    Supertype = c.Supertype,
                    Subtypes = c.Subtypes is { Count: > 0 } ? string.Join(", ", c.Subtypes) : null,
                    Types    = c.Types    is { Count: > 0 } ? string.Join(", ", c.Types)    : null,
                }));
                await _db.SaveChangesAsync();
                await AchievementService.CheckAsync("refresh_set", _db);
                await SelectSetAsync(set);
                // SelectSetAsync(set) réassigne SelectedSet = set avec la MÊME référence déjà
                // sélectionnée : le setter généré par [ObservableProperty] compare par égalité et
                // n'émet donc aucun PropertyChanged pour "SelectedSet", alors que le binding XAML
                // "SelectedSet.FilteredCards" ne se réévalue, dans ce cas précis, qu'en réaction à
                // CE signal précis (pas seulement à celui de FilteredCards lui-même) — d'où la page
                // restée vierge jusqu'à changer de set et revenir. On force le signal manquant.
                OnPropertyChanged(nameof(SelectedSet));
                return;
            }
        }
        catch { /* best-effort : retombe sur pokemontcg.io ci-dessous */ }

        await _db.CachedCards
            .Where(c => c.SetId == set.SetId)
            .ExecuteDeleteAsync();

        await AchievementService.CheckAsync("refresh_set", _db);
        await SelectSetAsync(set);
        OnPropertyChanged(nameof(SelectedSet));
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
                seriesVm.Sets.Add(new SetViewModel(s.Id, s.Name, s.Total, s.Series, s.ReleaseDate, s.LogoUrl, s.SymbolUrl, s.Source, s.ShortCode));
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

    private void ApplyIgnored(HashSet<string> ignoredIds)
    {
        foreach (var series in Series)
            foreach (var set in series.Sets)
                set.IsIgnored = ignoredIds.Contains(set.SetId);
    }

    [RelayCommand]
    private async Task ToggleSetIgnoredAsync(SetViewModel set)
    {
        var entry = await _db.IgnoredSets.FindAsync(set.SetId);
        if (set.IsIgnored)
        {
            if (entry is null)
                _db.IgnoredSets.Add(new IgnoredSet { SetId = set.SetId });
        }
        else if (entry is not null)
        {
            _db.IgnoredSets.Remove(entry);
        }
        await _db.SaveChangesAsync();
        RefreshIgnoredVisibility();
    }

    [RelayCommand]
    private async Task ToggleSeriesIgnoredAsync(SeriesViewModel series)
    {
        var shouldIgnore = series.AreAllIgnored != true;
        foreach (var set in series.Sets)
            set.IsIgnored = shouldIgnore;

        var setIds = series.Sets.Select(s => s.SetId).ToList();
        var existing = await _db.IgnoredSets.Where(i => setIds.Contains(i.SetId)).ToListAsync();
        _db.IgnoredSets.RemoveRange(existing);
        if (shouldIgnore)
            _db.IgnoredSets.AddRange(setIds.Select(id => new IgnoredSet { SetId = id }));
        await _db.SaveChangesAsync();
        RefreshIgnoredVisibility();
    }

    // Un même SetViewModel peut apparaître dans plusieurs groupes (sa série réelle, Favorites,
    // My Collection — cf. RefreshSpecialGroups), donc on invalide tous les groupes plutôt que de
    // chercher le ou les groupes propriétaires exacts.
    private void RefreshIgnoredVisibility()
    {
        foreach (var s in Series)
            s.NotifyIgnoredChanged();
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
            var ownedRows = await _db.OwnedCards
                .Where(o => o.SetId == set.SetId)
                .ToListAsync();
            var ownedMap = ownedRows.ToDictionary(o => o.CardId, o => o.Quantity);
            var holoIds = ownedRows.Where(o => o.IsHolo).Select(o => o.CardId).ToHashSet();

            var langMap = await BuildLanguageMapAsync(set.SetId, ownedMap);

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
                    new CardData(c.CardId, c.Name, c.Number, c.SetId, c.ImageSmall, c.ImageLarge, c.Rarity, c.CmLow, c.TcgLow, c.PricesUpdatedAt, c.CmUrl, c.TcgUrl, c.Supertype, c.Subtypes)),
                    ownedMap, wantedIds, excludedIds, set, langMap, cardTagMap, holoIds);
                StatusText = $"{set.Name} — {set.CompletionText}";
                _ = RefreshPricesIfNeededAsync(set);
            }
            else if (set.Source == "tcgdex")
            {
                try
                {
                    await LoadCardsFromTcgdexAsync(set, ownedMap, wantedIds, excludedIds, langMap, holoIds);
                    StatusText = $"{set.Name} — {set.CompletionText}";
                }
                catch (Exception ex)
                {
                    StatusText = $"Could not load {set.Name} — API error: {ex.Message}";
                    return;
                }
            }
            else
            {
                try
                {
                    var apiCards = (await _tcgService.GetCardsAsync(set.SetId))
                        .OrderBy(c => CardNumberSort(c.Number))
                        .ToList();

                    // Garde-fou indépendant du fix de pagination ci-dessus (PokemonTcgService) :
                    // l'API peut renvoyer un résultat "réussi" (pas d'exception) mais incomplet pour
                    // d'autres raisons (données manquantes côté pokemontcg.io, tri par numéro bancal
                    // sur certains sets). Sans ce contrôle, un reload affichait silencieusement un
                    // set incomplet comme s'il était à jour — StatusText prévient au moins l'utilisateur.
                    var incomplete = set.Total > 0 && apiCards.Count < set.Total;

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
                        TcgUrl = c.Tcgplayer?.Url,
                        Supertype = c.Supertype,
                        Subtypes = c.Subtypes is { Count: > 0 } ? string.Join(", ", c.Subtypes) : null,
                        Types    = c.Types    is { Count: > 0 } ? string.Join(", ", c.Types)    : null,
                    }));
                    await _db.SaveChangesAsync();

                    BuildCardViewModels(apiCards.Select(c =>
                        new CardData(c.Id, c.Name, c.Number, c.Set.Id, c.Images.Small, c.Images.Large, c.Rarity,
                            c.Cardmarket?.Prices?.LowPrice, ExtractTcgLow(c), now,
                            c.Cardmarket?.Url, c.Tcgplayer?.Url, c.Supertype,
                            c.Subtypes is { Count: > 0 } ? string.Join(", ", c.Subtypes) : null)),
                        ownedMap, wantedIds, excludedIds, set, langMap, holoIds: holoIds);
                    StatusText = incomplete
                        ? $"{set.Name} — only {apiCards.Count}/{set.Total} cards returned by the API, try refreshing again"
                        : $"{set.Name} — {set.CompletionText}";
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

    // Fallback : charge les cartes d'un set qui n'existe que sur TCGdex (pokemontcg.io ne l'a
    // pas). Contrairement à pokemontcg.io, TCGdex ne fournit pas les cartes d'un set en un seul
    // appel avec tous les détails (prix, rareté) — il faut d'abord la liste brève du set, puis
    // un appel par carte. Acceptable ici car ces sets de secours sont peu nombreux.
    private async Task LoadCardsFromTcgdexAsync(SetViewModel set, Dictionary<string, int> ownedMap,
        HashSet<string> wantedIds, HashSet<string> excludedIds, Dictionary<string, Dictionary<string, int>> langMap,
        HashSet<string>? holoIds = null)
    {
        var detail = await _tcgdexService.GetSetDetailAsync(set.SetId)
            ?? throw new InvalidOperationException("Set not found on TCGdex");

        var now = DateTime.UtcNow;
        var cachedCards = new List<CachedCard>();
        var cardData = new List<CardData>();
        int sort = 0;

        foreach (var brief in detail.Cards)
        {
            var full = await _tcgdexService.GetCardAsync(brief.Id);
            if (full is null) continue;

            var number = brief.LocalId.TrimStart('0');
            if (number.Length == 0) number = "0";
            var supertype = TCGdexService.NormalizeSupertype(full.Category);
            var subtypes = TCGdexService.BuildSubtypes(full);
            var imageSmall = full.Image is null ? "" : TCGdexService.BuildCardImageUrl(full.Image, "low");
            var imageLarge = full.Image is null ? null : TCGdexService.BuildCardImageUrl(full.Image, "high");
            var cmLow = full.Pricing?.Cardmarket?.Low;
            var tcgLow = full.Pricing?.Tcgplayer?.Normal?.MarketPrice
                ?? full.Pricing?.Tcgplayer?.ReverseHolofoil?.MarketPrice
                ?? full.Pricing?.Tcgplayer?.Holofoil?.MarketPrice;

            cachedCards.Add(new CachedCard
            {
                CardId = full.Id, SetId = set.SetId, Name = full.Name, Number = number,
                ImageSmall = imageSmall, ImageLarge = imageLarge, Rarity = full.Rarity,
                SortOrder = sort++, CmLow = cmLow, TcgLow = tcgLow, PricesUpdatedAt = now,
                Supertype = supertype, Subtypes = subtypes,
                Types = full.Types is { Count: > 0 } ? string.Join(", ", full.Types) : null,
                Source = "tcgdex"
            });
            cardData.Add(new CardData(full.Id, full.Name, number, set.SetId, imageSmall, imageLarge,
                full.Rarity, cmLow, tcgLow, now, null, null, supertype, subtypes));
        }

        _db.CachedCards.AddRange(cachedCards);
        await _db.SaveChangesAsync();

        BuildCardViewModels(cardData.OrderBy(c => CardNumberSort(c.Number)), ownedMap, wantedIds, excludedIds, set, langMap, holoIds: holoIds);
    }

    // Construit (une fois, à la première ouverture d'un set) la map CardId -> quantité par langue
    // depuis OwnedCardLanguages. Si plus d'une langue est suivie, toute carte déjà possédée
    // (ownedMap) mais sans détail par langue encore enregistré se voit attribuer rétroactivement
    // tout son stock à l'anglais (hypothèse de départ raisonnable, donnée persistée immédiatement).
    private async Task<Dictionary<string, Dictionary<string, int>>> BuildLanguageMapAsync(
        string setId, Dictionary<string, int> ownedMap)
    {
        var langMap = new Dictionary<string, Dictionary<string, int>>();
        if (_settings.TrackedLanguages.Count <= 1) return langMap;

        var rows = await _db.OwnedCardLanguages.Where(l => l.SetId == setId).ToListAsync();
        foreach (var row in rows)
        {
            if (!langMap.TryGetValue(row.CardId, out var dict))
                langMap[row.CardId] = dict = new();
            dict[row.Language] = row.Quantity;
        }

        var toBackfill = ownedMap.Where(kv => kv.Value > 0 && !langMap.ContainsKey(kv.Key)).ToList();
        if (toBackfill.Count > 0)
        {
            foreach (var kv in toBackfill)
            {
                langMap[kv.Key] = new() { ["EN"] = kv.Value };
                _db.OwnedCardLanguages.Add(new OwnedCardLanguage { CardId = kv.Key, SetId = setId, Language = "EN", Quantity = kv.Value });
            }
            await _db.SaveChangesAsync();
        }

        return langMap;
    }

    private async Task OnCardLanguageBreakdownChangedAsync(CardViewModel card)
    {
        if (_isBulkUpdate) return;

        var existing = await _db.OwnedCardLanguages.Where(l => l.CardId == card.CardId).ToDictionaryAsync(l => l.Language);
        foreach (var row in card.LanguageRows)
        {
            if (row.Quantity > 0)
            {
                if (existing.TryGetValue(row.Code, out var entry))
                    entry.Quantity = row.Quantity;
                else
                    _db.OwnedCardLanguages.Add(new OwnedCardLanguage { CardId = card.CardId, SetId = card.SetId, Language = row.Code, Quantity = row.Quantity });
            }
            else if (existing.TryGetValue(row.Code, out var entry))
            {
                _db.OwnedCardLanguages.Remove(entry);
            }
        }
        await _db.SaveChangesAsync();
    }

    private void BuildCardViewModels(IEnumerable<CardData> cards, Dictionary<string, int> ownedMap,
        HashSet<string> wantedIds, HashSet<string> excludedIds, SetViewModel set,
        Dictionary<string, Dictionary<string, int>> langMap,
        Dictionary<string, int>? cardTagMap = null, HashSet<string>? holoIds = null)
    {
        var deckMap = IsDeckBuilderTabActive
            ? DeckBuilder.DeckEntries.GroupBy(e => e.CardId).ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity))
            : null;

        var isMultiLanguage = _settings.TrackedLanguages.Count > 1;
        var trackedLanguages = isMultiLanguage
            ? LanguageCatalog.All.Select(l => l.Code).Where(c => _settings.TrackedLanguages.Contains(c)).ToList()
            : null;

        foreach (var card in cards)
        {
            var vm = new CardViewModel(
                card.Id, card.Name, card.Number, card.SetId,
                card.ImageSmall, card.ImageLarge, card.Rarity,
                ownedMap.TryGetValue(card.Id, out var qty) ? qty : 0,
                wantedIds.Contains(card.Id),
                excludedIds.Contains(card.Id),
                _imageCache, card.Supertype, card.Subtypes,
                trackedLanguages, langMap.GetValueOrDefault(card.Id),
                holoIds?.Contains(card.Id) ?? false)
            {
                CmLow = card.CmLow,
                TcgLow = card.TcgLow,
                PricesUpdatedAt = card.PricesUpdatedAt,
                CmUrl = card.CmUrl,
                TcgUrl = card.TcgUrl,
                ShortCode = set.ShortCode,
                DeckQuantity = deckMap?.GetValueOrDefault(card.Id, 0) ?? 0
            };
            if (isMultiLanguage) vm.OnLanguageBreakdownChanged = OnCardLanguageBreakdownChangedAsync;

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
                else if (e.PropertyName == nameof(CardViewModel.IsHolo))
                    await OnCardHoloChangedAsync(vm);
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

    // Pas de sens de marquer "holo" une carte non possédée — le toggle est désactivé dans la vue
    // tant que Quantity == 0 (voir CardTemplate dans MainWindow.xaml), donc l'entrée OwnedCards
    // existe toujours ici en pratique ; no-op défensif sinon plutôt que de créer une possession.
    private async Task OnCardHoloChangedAsync(CardViewModel card)
    {
        var entry = await _db.OwnedCards.FindAsync(card.CardId);
        if (entry is null) return;
        entry.IsHolo = card.IsHolo;
        await _db.SaveChangesAsync();
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
                        Rarity = c.Rarity, SortOrder = i,
                        Supertype = c.Supertype,
                        Subtypes = c.Subtypes is { Count: > 0 } ? string.Join(", ", c.Subtypes) : null,
                        Types    = c.Types    is { Count: > 0 } ? string.Join(", ", c.Types)    : null,
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

            string[] blueNames    = ["Pidgeot", "Arcanine", "Rhydon", "Exeggutor", "Alakazam", "Blastoise", "Charizard", "Venusaur"];
            string[] redNames     = ["Blastoise", "Charizard", "Venusaur", "Snorlax", "Pikachu", "Espeon"];
            string[] stevenNames  = ["Skarmory", "Aggron", "Cradily", "Armaldo", "Claydol", "Metagross"];
            string[] nNames       = ["Zekrom", "Reshiram", "Zoroark", "Archeops", "Klinklang", "Carracosta", "Vanilluxe"];
            string[] dianthaNames = ["Hawlucha", "Tyrantrum", "Abomasnow", "Gourgeist", "Goodra", "Gardevoir"];
            string[] kukuiNames   = ["Lycanroc", "Ninetales", "Braviary", "Magnezone", "Snorlax", "Incineroar", "Primarina", "Decidueye"];
            string[] leonNames    = ["Aegislash", "Haxorus", "Dragapult", "Charizard", "Inteleon", "Mr. Rime", "Cinderace", "Seismitoad", "Rillaboom", "Rhyperior"];
            string[] geetaNames   = ["Espathra", "Avalugg", "Gogoat", "Kingambit", "Veluza", "Glimmora"];

            if (blueNames.Any(n => card.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                if (await OwnsAtLeastOneOfEachAsync(blueNames))
                    await AchievementService.CheckAsync("blue_team", _db);

            if (redNames.Any(n => card.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                if (await OwnsAtLeastOneOfEachAsync(redNames))
                    await AchievementService.CheckAsync("red_team", _db);

            if (stevenNames.Any(n => card.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                if (await OwnsAtLeastOneOfEachAsync(stevenNames))
                    await AchievementService.CheckAsync("steven_team", _db);

            if (nNames.Any(n => card.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                if (await OwnsAtLeastOneOfEachAsync(nNames))
                    await AchievementService.CheckAsync("n_team", _db);

            if (dianthaNames.Any(n => card.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                if (await OwnsAtLeastOneOfEachAsync(dianthaNames))
                    await AchievementService.CheckAsync("diantha_team", _db);

            if (kukuiNames.Any(n => card.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                if (await OwnsAtLeastOneOfEachAsync(kukuiNames))
                    await AchievementService.CheckAsync("kukui_team", _db);

            if (leonNames.Any(n => card.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                if (await OwnsAtLeastOneOfEachAsync(leonNames))
                    await AchievementService.CheckAsync("leon_team", _db);

            if (geetaNames.Any(n => card.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                if (await OwnsAtLeastOneOfEachAsync(geetaNames))
                    await AchievementService.CheckAsync("geeta_team", _db);
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
            if (set.Series == "Black & White")
                await AchievementService.CheckAsync("unova_enthusiast", _db);
            if (set.Series == "XY")
                await AchievementService.CheckAsync("all_3d_now", _db);
            if (set.Series == "Sun & Moon")
                await AchievementService.CheckAsync("tropical_trip", _db);
            if (set.Series == "Sword & Shield")
                await AchievementService.CheckAsync("collector_bruv", _db);
            if (set.Series == "Scarlet & Violet")
                await AchievementService.CheckAsync("sandwich_bikes", _db);
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

            if (_settings.TrackedLanguages.Count > 1)
            {
                if (targetState)
                {
                    var existingLangIds = (await _db.OwnedCardLanguages
                        .Where(l => l.SetId == SelectedSet.SetId)
                        .Select(l => l.CardId)
                        .ToListAsync()).ToHashSet();
                    foreach (var card in SelectedSet.Cards)
                        if (!existingLangIds.Contains(card.CardId))
                            _db.OwnedCardLanguages.Add(new OwnedCardLanguage { CardId = card.CardId, SetId = card.SetId, Language = "EN", Quantity = 1 });
                }
                else
                {
                    var toRemoveLang = await _db.OwnedCardLanguages
                        .Where(l => l.SetId == SelectedSet.SetId)
                        .ToListAsync();
                    _db.OwnedCardLanguages.RemoveRange(toRemoveLang);
                }
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
        _ = RefreshStatsAsync();
        _settingsWindow = new Views.SettingsWindow { DataContext = this };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    [ObservableProperty] private CollectionStatsViewModel? _stats;

    // Recalculé à chaque ouverture de Settings (pas mis en cache, pas abonné aux changements —
    // un décompte légèrement périmé si on possède/déspossède une carte pendant que Settings est
    // déjà ouvert est acceptable pour une page de stats consultative). Le prix (Cardmarket/
    // TCGPlayer) est délibérément exclu : les cartes venant du fallback TCGdex (Ascended Heroes,
    // promos SWSH/SV...) n'ont jamais de prix récupéré, un classement "cartes les plus chères"
    // les exclurait silencieusement plutôt que de signaler l'absence de donnée.
    private async Task RefreshStatsAsync()
    {
        var allSets = Series.Where(s => s.IsRealSeriesGroup)
            .SelectMany(s => s.Sets)
            .Where(s => !s.IsIgnored)
            .ToList();
        var setsBegun = allSets.Count(s => s.OwnedCount > 0);
        var setsComplete = allSets.Count(s => s.IsComplete);

        var ownedRows = await _db.OwnedCards
            .Where(o => o.Quantity > 0)
            .Join(_db.CachedCards, o => o.CardId, c => c.CardId, (o, c) => new { o.Quantity, o.IsHolo, c.Name, c.Rarity, c.Supertype })
            .ToListAsync();

        var totalOwned = ownedRows.Count;
        var totalHolo = ownedRows.Count(r => r.IsHolo);

        // Regroupe par nom d'espèce nettoyé des décorations TCG (voir CleanSpeciesName) : sans
        // ça, "Lugia", "Shining Lugia", "Lugia V", "Lugia VSTAR"... sont comptés séparément et
        // aucune variante individuelle ne ressort comme "la plus collectée" même en en possédant
        // beaucoup au total. Filtré sur Supertype "Pokémon" : sinon Energy/Trainer homonymes
        // (ex. "Boost Energy" vs un Pokémon) faussaient le classement.
        var topPokemonRanked = ownedRows
            .Where(r => r.Supertype == "Pokémon")
            .GroupBy(r => PokeSpriteService.CleanSpeciesName(r.Name))
            .Select(g => new { Name = g.Key, Count = g.Sum(r => r.Quantity) })
            .OrderByDescending(g => g.Count)
            .ToList();
        var topPokemon = topPokemonRanked.FirstOrDefault();

        var rarityBreakdown = ownedRows
            .GroupBy(r => string.IsNullOrEmpty(r.Rarity) ? "Unknown" : r.Rarity!)
            .Select(g => new { Rarity = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Select(g => new RarityStat(g.Rarity, g.Count,
                totalOwned > 0 ? $"{g.Count * 100.0 / totalOwned:F1}%" : "0%"))
            .ToList();

        var topCategories = Series.Where(s => s.IsRealSeriesGroup)
            .Select(s =>
            {
                var sets = s.Sets.Where(set => !set.IsIgnored).ToList();
                return new { s.SeriesName, Owned = sets.Sum(set => set.EffectiveOwned), Total = sets.Sum(set => set.EffectiveTotal) };
            })
            .Where(x => x.Total > 0)
            .OrderByDescending(x => (double)x.Owned / x.Total)
            .ThenByDescending(x => x.Owned)
            .Take(3)
            .Select(x => new SeriesCompletionStat(x.SeriesName, $"{x.Owned * 100.0 / x.Total:F0}%", x.Owned, x.Total))
            .ToList();

        // Best-effort : nom non reconnu par PokeAPI (carte Trainer/Energy, forme trop exotique) ou
        // API indisponible -> pas de sprite, l'UI masque simplement l'image dans ce cas.
        async Task<Bitmap?> FetchSpriteAsync(string name)
        {
            var spriteUrl = await _pokeSprite.GetSpriteUrlAsync(name);
            if (spriteUrl is null) return null;
            var raw = await _imageCache.GetImageAsync(spriteUrl, $"pokesprite-{name}");
            // Les icônes "box" PokeAPI (Sword/Shield, Gen 7, Scarlet/Violet) ont un gros padding
            // transparent autour du sprite réel (ex. ~20x20px de dessin dans un canevas 68x68) —
            // sans ce recadrage, l'icône affichée minuscule dans un conteneur petit paraît quasi
            // invisible.
            return raw is not null ? CropTransparentPadding(raw) : null;
        }

        Bitmap? topPokemonSprite = topPokemon is not null ? await FetchSpriteAsync(topPokemon.Name) : null;

        // Mode "équipe" : les 6 premiers, avec le sprite du premier déjà récupéré ci-dessus pour
        // éviter un fetch en double.
        var topPokemonTeam = new List<PokemonTeamMember>();
        foreach (var entry in topPokemonRanked.Take(6))
        {
            var sprite = entry == topPokemon ? topPokemonSprite : await FetchSpriteAsync(entry.Name);
            topPokemonTeam.Add(new PokemonTeamMember { Name = entry.Name, Count = entry.Count, Sprite = sprite });
        }

        Stats = new CollectionStatsViewModel
        {
            TotalOwnedCards = totalOwned,
            TotalHoloCards = totalHolo,
            TotalSets = allSets.Count,
            SetsBegun = setsBegun,
            SetsComplete = setsComplete,
            TopPokemonName = topPokemon?.Name,
            TopPokemonCount = topPokemon?.Count ?? 0,
            TopPokemonSprite = topPokemonSprite,
            TopPokemonTeam = topPokemonTeam,
            RarityBreakdown = rarityBreakdown,
            TopCategories = topCategories,
            UnlockedAchievements = Achievements.Count(a => a.IsUnlocked),
            TotalAchievements = Achievements.Count,
        };
    }

    // Les icônes "box" PokeAPI sont fournies sur un canevas bien plus grand que le dessin réel
    // (padding transparent, ex. ~20x20px utiles dans un fichier 68x68) — on recadre sur le
    // rectangle non-transparent, sinon l'icône affichée en petit paraît quasi invisible.
    // Version Avalonia : SkiaSharp via Platform/ImageCrop (FormatConvertedBitmap/CroppedBitmap
    // côté WPF).
    private static Bitmap CropTransparentPadding(Bitmap source) => ImageCrop.CropTransparentPadding(source);

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
            var color = Avalonia.Media.Color.Parse(hex);
            Avalonia.Application.Current!.Resources["CollectionBorderBrush"] =
                new Avalonia.Media.SolidColorBrush(color);
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
        var fileName = await Dialogs.SaveFileAsync("Backup collection", "Cardex backup", "cardex",
            $"cardex_backup_{DateTime.Now:yyyy-MM-dd}.cardex");
        if (fileName is null) return;

        var owned     = await _db.OwnedCards.ToListAsync();
        var ownedLang = await _db.OwnedCardLanguages.ToListAsync();
        var wanted    = await _db.WantedCards.ToListAsync();
        var favorite  = await _db.FavoriteSets.ToListAsync();

        var backup = new BackupFile(
            Version: "1.3",
            ExportedAt: DateTime.UtcNow,
            OwnedCards: owned.Select(o => new BackupOwned(o.CardId, o.SetId, o.Quantity)).ToList(),
            WantedCards: wanted.Select(w => new BackupWanted(w.CardId, w.SetId)).ToList(),
            FavoriteSets: favorite.Select(f => f.SetId).ToList(),
            OwnedCardLanguages: ownedLang.Select(o => new BackupOwnedLanguage(o.CardId, o.SetId, o.Language, o.Quantity)).ToList());

        var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(fileName, json, Encoding.UTF8);

        StatusText = $"Backup saved — {owned.Count} owned, {wanted.Count} wanted, {favorite.Count} favorite set(s)";
        await AchievementService.CheckAsync("backup_used", _db);
    }

    [RelayCommand]
    private async Task RestoreCollectionAsync()
    {
        var fileName = await Dialogs.OpenFileAsync("Restore collection", "Cardex backup", "cardex");
        if (fileName is null) return;

        if (!await Dialogs.ConfirmAsync(
            "Restoring will replace your current collection, want list and favorites.\n\nContinue?",
            "Restore collection")) return;

        try
        {
            var json = await File.ReadAllTextAsync(fileName, Encoding.UTF8);
            var backup = JsonSerializer.Deserialize<BackupFile>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (backup is null) { StatusText = "Restore failed — invalid file."; return; }

            await _db.OwnedCards.ExecuteDeleteAsync();
            await _db.OwnedCardLanguages.ExecuteDeleteAsync();
            await _db.WantedCards.ExecuteDeleteAsync();
            await _db.FavoriteSets.ExecuteDeleteAsync();

            if (backup.OwnedCards?.Count > 0)
                _db.OwnedCards.AddRange(backup.OwnedCards.Select(o =>
                    new OwnedCard { CardId = o.CardId, SetId = o.SetId, Quantity = o.Quantity }));

            if (backup.OwnedCardLanguages?.Count > 0)
                _db.OwnedCardLanguages.AddRange(backup.OwnedCardLanguages.Select(o =>
                    new OwnedCardLanguage { CardId = o.CardId, SetId = o.SetId, Language = o.Language, Quantity = o.Quantity }));

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

    // Origine taguée lors d'une synchro depuis ce Mac — distincte de "Desktop (Windows)" (app WPF,
    // DesktopSyncOrigin côté Windows) pour que chaque appareil voie les synchros des autres comme
    // « venant d'ailleurs ».
    private const string SyncOrigin = "Desktop (Mac)";

    // Synchronisation manuelle (bouton Settings) : pousse l'état local vers l'API Cardex, taggé
    // SyncOrigin. Si le serveur montre que la dernière synchro connue vient d'un autre appareil
    // (mobile ou "Desktop (Windows)") et est plus
    // récente que notre dernier sync connu, on prévient avant d'écraser (décision utilisateur :
    // avertir, ne pas bloquer) — contrairement à AutoPullSyncIfNeededAsync (lancée au démarrage)
    // qui rapatrie automatiquement sans demander.
    [RelayCommand]
    private async Task SyncNowAsync()
    {
        var apiKey = _settings.CollectionSyncApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await Dialogs.InfoAsync("Enter your Cardex API key first (get one at cardex-api.dev).", "Sync");
            return;
        }

        IsSyncing = true;
        try
        {
            var syncService = new CollectionSyncService(_db, _settings, apiKey);
            var remote = await syncService.FetchRemoteStateAsync();

            if (remote is not null && remote.Origin != SyncOrigin)
            {
                var remoteUpdatedAt = DateTime.Parse(remote.UpdatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind);
                if (_settings.LastCollectionSyncAt is null || remoteUpdatedAt > _settings.LastCollectionSyncAt)
                {
                    if (!await Dialogs.ConfirmAsync(
                        $"The last sync came from {remote.Origin} on {remoteUpdatedAt.ToLocalTime():dd/MM/yyyy HH:mm}.\n" +
                        "Syncing now will overwrite those changes with your local collection.\n\nContinue?",
                        "Sync — overwrite warning")) return;
                }
            }

            var updatedAt = await syncService.PushAsync(SyncOrigin);
            _settings.LastCollectionSyncAt = updatedAt;
            _settings.LastCollectionSyncOrigin = SyncOrigin;
            _settings.Save();
            OnPropertyChanged(nameof(LastSyncStatusText));
            StatusText = "Collection synced.";
        }
        catch (Exception ex)
        {
            StatusText = $"Sync failed — {ex.Message}";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    // Détection au démarrage (App.xaml.cs) d'une synchro en attente côté serveur venant de
    // l'AUTRE appareil. Depuis l'incident du 2026-10-15 (un rapatriement automatique silencieux a
    // effacé la collection d'un testeur avec un snapshot vide), on ne l'applique plus jamais sans
    // confirmation explicite — l'utilisateur voit l'origine, l'heure et le nombre de cartes avant
    // de décider, et un backup local est pris avant d'écraser quoi que ce soit. Best-effort : une
    // erreur réseau ne doit jamais bloquer le démarrage.
    public async Task CheckPendingSyncAsync()
    {
        var apiKey = _settings.CollectionSyncApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        try
        {
            var syncService = new CollectionSyncService(_db, _settings, apiKey);
            var remote = await syncService.FetchRemoteStateAsync();
            if (remote is null || remote.Origin == SyncOrigin) return;

            var remoteUpdatedAt = DateTime.Parse(remote.UpdatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind);
            if (_settings.LastCollectionSyncAt is { } lastKnown && remoteUpdatedAt <= lastKnown) return;

            var ownedCount = CollectionSyncService.CountOwnedCards(remote.CollectionJson);

            if (!await Dialogs.ConfirmAsync(
                $"A sync from {remote.Origin} is available, from {remoteUpdatedAt.ToLocalTime():dd/MM/yyyy HH:mm} " +
                $"({ownedCount} owned card(s)).\n\n" +
                "Import it now? Your current local collection will be backed up first, just in case.",
                "Sync available")) return;

            var backupPath = await BackupLocalCollectionBeforeSyncAsync(syncService);

            await syncService.ApplyCollectionJsonAsync(remote.CollectionJson);
            _settings.LastCollectionSyncAt = remoteUpdatedAt;
            _settings.LastCollectionSyncOrigin = remote.Origin;
            _settings.Save();
            OnPropertyChanged(nameof(LastSyncStatusText));

            await ReloadAfterSyncApplyAsync();
            StatusText = $"Collection synced from {remote.Origin} — local backup saved to {backupPath}.";
        }
        catch { /* best-effort, ne doit jamais bloquer le démarrage */ }
    }

    private static string SyncBackupsFolder => Path.Combine(AppPaths.DataFolder, "SyncBackups");

    private static async Task<string> BackupLocalCollectionBeforeSyncAsync(CollectionSyncService syncService)
    {
        var folder = SyncBackupsFolder;
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"pre_sync_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        var json = await syncService.BuildCollectionJsonAsync();
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    // Filet de sécurité demandé après l'incident du 2026-10-15 : permet d'annuler un import de
    // sync malvenu en restaurant le backup local pris juste avant (voir
    // BackupLocalCollectionBeforeSyncAsync, appelé par CheckPendingSyncAsync). N'a pas besoin de
    // clé API ni de réseau — lit uniquement un fichier local déjà présent.
    [RelayCommand]
    private async Task RestoreLastSyncBackupAsync()
    {
        var latest = Directory.Exists(SyncBackupsFolder)
            ? new DirectoryInfo(SyncBackupsFolder).GetFiles("pre_sync_*.json").OrderByDescending(f => f.LastWriteTime).FirstOrDefault()
            : null;
        if (latest is null)
        {
            await Dialogs.InfoAsync("No sync backup found.", "Restore backup");
            return;
        }

        var json = await File.ReadAllTextAsync(latest.FullName);
        var ownedCount = CollectionSyncService.CountOwnedCards(json);

        if (!await Dialogs.ConfirmAsync(
            $"Restore the backup from {latest.LastWriteTime:dd/MM/yyyy HH:mm} ({ownedCount} owned card(s))?\n\n" +
            "This will replace your current collection, wants, tags and achievements.",
            "Restore backup")) return;

        var syncService = new CollectionSyncService(_db, _settings, string.Empty);
        await syncService.ApplyCollectionJsonAsync(json);
        await ReloadAfterSyncApplyAsync();
        StatusText = $"Collection restored from backup ({latest.LastWriteTime:dd/MM/yyyy HH:mm}).";
    }

    private async Task ReloadAfterSyncApplyAsync()
    {
        // TrackedLanguages peut avoir été écrasé par ApplyCollectionJsonAsync (synchronisé lui
        // aussi) — resynchroniser les checkboxes Settings avant de recharger le set ouvert
        // ci-dessous, qui lira la nouvelle valeur.
        RefreshLanguageOptionsFromSettings();

        var favoriteIds = (await _db.FavoriteSets.Select(f => f.SetId).ToListAsync()).ToHashSet();
        ApplyFavorites(favoriteIds);
        await ApplyOwnedCountsAsync();
        RefreshSpecialGroups();
        await LoadWantedCardsAsync();
        await LoadDuplicateCardsAsync();
        await LoadTagsAsync();
        await LoadAchievementsAsync();

        if (SelectedSet is not null)
        {
            SelectedSet.Cards.Clear();
            await SelectSetAsync(SelectedSet);
        }
    }

    [RelayCommand]
    private async Task ImportCollectionAsync()
    {
        var importFile = await Dialogs.OpenFileAsync("Import collection", "Text files", "txt");
        if (importFile is null) return;

        StatusText = "Importing…";

        var lines = await File.ReadAllLinesAsync(importFile, Encoding.UTF8);

        var allSets      = await _db.CachedSets.ToListAsync();
        var setByShort   = allSets
            .Where(s => s.ShortCode != null)
            .ToDictionary(s => s.ShortCode!.ToUpperInvariant());
        var setByPtcgo   = allSets
            .Where(s => s.PtcgoCode != null)
            .ToDictionary(s => s.PtcgoCode!.ToUpperInvariant());
        var setById      = allSets.ToDictionary(s => s.SetId.ToUpperInvariant());
        var setByName    = allSets.ToDictionary(s => s.Name.ToUpperInvariant());

        var ownedDict  = (await _db.OwnedCards.ToListAsync()).ToDictionary(o => o.CardId);
        // SetId -> (Number -> CardId). Résout par Number plutôt que de reconstruire l'ID
        // ("{SetId}-{number}"), car les cartes TCGdex ont un CardId paddé de zéros (ex: "mep-012")
        // différent de leur Number affiché ("12") — la concaténation ne matcherait jamais.
        var cardIdCache = new Dictionary<string, Dictionary<string, string>>();
        var qtyPattern  = new System.Text.RegularExpressions.Regex(@"\s+x(\d+)\s*$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        int importCount = 0;
        var errors = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

            int qty = 1;
            var qtyMatch = qtyPattern.Match(line);
            if (qtyMatch.Success)
            {
                qty  = int.Parse(qtyMatch.Groups[1].Value);
                line = line[..qtyMatch.Index].Trim();
            }

            var dashIdx = line.IndexOf('-');
            if (dashIdx < 1) { errors.Add(rawLine.Trim()); continue; }

            var code   = line[..dashIdx].ToUpperInvariant();
            var number = line[(dashIdx + 1)..].Trim();
            if (string.IsNullOrEmpty(number)) { errors.Add(rawLine.Trim()); continue; }

            if (!setByShort.TryGetValue(code, out var set) && !setByPtcgo.TryGetValue(code, out set) && !setById.TryGetValue(code, out set) && !setByName.TryGetValue(code, out set))
                { errors.Add(rawLine.Trim()); continue; }

            if (!cardIdCache.TryGetValue(set.SetId, out var numberToCardId))
            {
                numberToCardId = await _db.CachedCards
                    .Where(c => c.SetId == set.SetId)
                    .ToDictionaryAsync(c => c.Number, c => c.CardId);
                cardIdCache[set.SetId] = numberToCardId;
            }

            if (!numberToCardId.TryGetValue(number, out var cardId)) { errors.Add(rawLine.Trim()); continue; }

            if (ownedDict.TryGetValue(cardId, out var existing))
                existing.Quantity += qty;
            else
            {
                var newOwned = new OwnedCard { CardId = cardId, SetId = set.SetId, Quantity = qty, OwnedAt = DateTime.UtcNow };
                _db.OwnedCards.Add(newOwned);
                ownedDict[cardId] = newOwned;
            }

            importCount++;
        }

        await _db.SaveChangesAsync();
        await ApplyOwnedCountsAsync();
        RefreshSpecialGroups();

        if (SelectedSet is not null)
        {
            SelectedSet.Cards.Clear();
            await SelectSetAsync(SelectedSet);
        }

        if (errors.Count > 0)
        {
            var errorFile = await Dialogs.SaveFileAsync("Save error report", "Text files", "txt", "import_errors.txt");
            if (errorFile is not null)
                await File.WriteAllLinesAsync(errorFile, errors, Encoding.UTF8);
        }

        StatusText = importCount > 0
            ? $"{importCount} card(s) imported" + (errors.Count > 0 ? $", {errors.Count} line(s) failed" : "")
            : errors.Count > 0 ? $"Import failed — {errors.Count} unrecognized line(s)" : "Nothing to import";
    }

    [RelayCommand]
    private async Task GenerateImportTemplateAsync()
    {
        var templateFile = await Dialogs.SaveFileAsync("Save import template", "Text files", "txt", "cardex_import_template.txt");
        if (templateFile is null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# ============================================================");
        sb.AppendLine("# Cardex — Collection Import Template");
        sb.AppendLine("# ============================================================");
        sb.AppendLine("#");
        sb.AppendLine("# FORMAT: CODE-NUMBER [xQUANTITY]");
        sb.AppendLine("#");
        sb.AppendLine("# Examples:");
        sb.AppendLine("#   PRE-1                  SHORT code  — 1 copy of card #1 from Prismatic Evolutions");
        sb.AppendLine("#   BS-4 x2                SHORT code  — 2 copies of card #4 from Base");
        sb.AppendLine("#   NEOG-7                 SHORT code  — 1 copy of card #7 from Neo Genesis");
        sb.AppendLine("#   sv8pt5-1               Raw set ID  — same card as PRE-1");
        sb.AppendLine("#   Prismatic Evolutions-1 Full name   — same card as PRE-1");
        sb.AppendLine("#");
        sb.AppendLine("# Notes:");
        sb.AppendLine("#   - Lines starting with # are comments and are ignored");
        sb.AppendLine("#   - Empty lines are ignored");
        sb.AppendLine("#   - The quantity suffix x{n} is optional (defaults to 1)");
        sb.AppendLine("#   - Importing the same file twice adds quantities, it does not replace them");
        sb.AppendLine("#   - SHORT codes (2-4 chars) are the simplest key — listed first in table below");
        sb.AppendLine("#   - Also accepted: official PTCGO key, raw set ID, or full set name");
        sb.AppendLine("#");
        sb.AppendLine("# ============================================================");
        sb.AppendLine("# AVAILABLE SET KEYS");
        sb.AppendLine("# ============================================================");
        sb.AppendLine("#");
        sb.AppendLine($"# {"SHORT",-6} {"KEY",-14} {"SET NAME",-42} SERIES");
        sb.AppendLine($"# {new string('-', 82)}");
        foreach (var s in TemplateSets)
            sb.AppendLine($"# {s.ShortCode,-6} {s.Key,-14} {s.Name,-42} {s.Series}");
        sb.AppendLine("#");
        sb.AppendLine("# ============================================================");
        sb.AppendLine("# ADD YOUR CARDS BELOW THIS LINE");
        sb.AppendLine("# ============================================================");
        sb.AppendLine();

        await File.WriteAllTextAsync(templateFile, sb.ToString(), Encoding.UTF8);
        StatusText = "Import template saved";
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (await Dialogs.ExportOptionsAsync() is not var (mode, format)) return;

        var csvFile = await Dialogs.SaveFileAsync("Export CSV", "CSV files", "csv", CsvFileName(mode, format));
        if (csvFile is null) return;

        var count = await WriteCsvAsync(csvFile, mode, format);
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
            qtyMap = cardIds.ToDictionary(id => id, id => ownedQty.GetValueOrDefault(id, 1));
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
            qtyMap = cardIds.ToDictionary(id => id, _ => 1);
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
        if (set.Source == "tcgdex")
        {
            await RefreshPricesFromTcgdexAsync(set);
            return;
        }

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
                if (row.Supertype is null)
                {
                    row.Supertype = api.Supertype;
                    row.Subtypes  = api.Subtypes is { Count: > 0 } ? string.Join(", ", api.Subtypes) : null;
                    row.Types     = api.Types    is { Count: > 0 } ? string.Join(", ", api.Types)    : null;
                }
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

    // Équivalent de RefreshPricesIfNeededAsync pour les sets de secours TCGdex : un appel par
    // carte (pas de fetch bulk côté TCGdex), acceptable vu le faible nombre de cartes de ces sets.
    private async Task RefreshPricesFromTcgdexAsync(SetViewModel set)
    {
        try
        {
            var sample = await _db.CachedCards.Where(c => c.SetId == set.SetId).FirstOrDefaultAsync();
            if (sample?.PricesUpdatedAt > DateTime.UtcNow.AddHours(-24)) return;

            var cached = await _db.CachedCards.Where(c => c.SetId == set.SetId).ToListAsync();
            var now = DateTime.UtcNow;

            foreach (var row in cached)
            {
                var full = await _tcgdexService.GetCardAsync(row.CardId);
                if (full is null) continue;

                row.CmLow = full.Pricing?.Cardmarket?.Low;
                row.TcgLow = full.Pricing?.Tcgplayer?.Normal?.MarketPrice
                    ?? full.Pricing?.Tcgplayer?.ReverseHolofoil?.MarketPrice
                    ?? full.Pricing?.Tcgplayer?.Holofoil?.MarketPrice;
                row.PricesUpdatedAt = now;
            }
            await _db.SaveChangesAsync();

            var byId = cached.ToDictionary(c => c.CardId);
            foreach (var vm in set.Cards)
            {
                if (!byId.TryGetValue(vm.CardId, out var row)) continue;
                vm.CmLow = row.CmLow;
                vm.TcgLow = row.TcgLow;
                vm.PricesUpdatedAt = now;
            }
        }
        catch { }
    }

    private record SetData(string Id, string Name, int Total, string Series, string ReleaseDate, string LogoUrl, string SymbolUrl, string Source = "pokemontcgio", string? ShortCode = null);
    private record CardData(string Id, string Name, string Number, string SetId, string ImageSmall, string? ImageLarge, string? Rarity,
        decimal? CmLow = null, decimal? TcgLow = null, DateTime? PricesUpdatedAt = null,
        string? CmUrl = null, string? TcgUrl = null, string? Supertype = null, string? Subtypes = null);

    private record BackupFile(string Version, DateTime ExportedAt,
        List<BackupOwned>? OwnedCards, List<BackupWanted>? WantedCards, List<string>? FavoriteSets,
        List<BackupOwnedLanguage>? OwnedCardLanguages = null);
    private record BackupOwned(string CardId, string SetId, int Quantity);
    private record BackupWanted(string CardId, string SetId);
    private record BackupOwnedLanguage(string CardId, string SetId, string Language, int Quantity);
}

public record TemplateSetEntry(string ShortCode, string Key, string Name, string Series);
