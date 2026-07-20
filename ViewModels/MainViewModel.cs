using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cardex.Data;
using Cardex.Models;
using Cardex.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace Cardex.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly PokemonTcgService _tcgService;
    private readonly ImageCacheService _imageCache;
    private readonly AppDbContext _db;

    public ObservableCollection<SeriesViewModel> Series { get; } = [];

    [ObservableProperty] private SetViewModel? _selectedSet;
    [ObservableProperty] private string _statusText = "Welcome to Cardex";
    [ObservableProperty] private bool _isBusy;

    public MainViewModel(PokemonTcgService tcgService, ImageCacheService imageCache, AppDbContext db)
    {
        _tcgService = tcgService;
        _imageCache = imageCache;
        _db = db;
    }

    [RelayCommand]
    public async Task LoadSetsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = "Loading sets…";
        try
        {
            var sets = await _tcgService.GetSetsAsync();
            var owned = await _db.OwnedCards.Select(o => o.SetId).Distinct().ToListAsync();

            Series.Clear();
            var grouped = sets
                .GroupBy(s => s.Series)
                .OrderBy(g => sets.First(s => s.Series == g.Key).ReleaseDate);

            foreach (var group in grouped)
            {
                var seriesVm = new SeriesViewModel(group.Key);
                foreach (var set in group.OrderBy(s => s.ReleaseDate))
                {
                    seriesVm.Sets.Add(new SetViewModel(
                        set.Id, set.Name, set.Total, set.Series, set.ReleaseDate,
                        set.Images.Logo, set.Images.Symbol));
                }
                Series.Add(seriesVm);
            }

            StatusText = $"{sets.Count} sets loaded";
            _ = LoadSymbolsAsync(Series.SelectMany(s => s.Sets).ToList());
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
            var ownedList = await _db.OwnedCards
                .Where(o => o.SetId == set.SetId)
                .Select(o => o.CardId)
                .ToListAsync();
            var ownedIds = ownedList.ToHashSet();

            var cards = await _tcgService.GetCardsAsync(set.SetId);

            foreach (var card in cards)
            {
                var vm = new CardViewModel(
                    card.Id, card.Name, card.Number, card.Set.Id,
                    card.Images.Small, card.Rarity,
                    ownedIds.Contains(card.Id),
                    _imageCache);

                vm.PropertyChanged += async (s, e) =>
                {
                    if (e.PropertyName == nameof(CardViewModel.IsOwned))
                        await OnCardOwnershipChangedAsync(vm, set);
                };

                set.Cards.Add(vm);
            }

            set.NotifyOwnershipChanged();
            StatusText = $"{set.Name} — {set.CompletionText}";
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

    private bool _isBulkUpdate;

    private async Task OnCardOwnershipChangedAsync(CardViewModel card, SetViewModel set)
    {
        if (_isBulkUpdate) return;

        if (card.IsOwned)
        {
            if (!await _db.OwnedCards.AnyAsync(o => o.CardId == card.CardId))
            {
                _db.OwnedCards.Add(new OwnedCard { CardId = card.CardId, SetId = card.SetId });
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            var entry = await _db.OwnedCards.FindAsync(card.CardId);
            if (entry is not null)
            {
                _db.OwnedCards.Remove(entry);
                await _db.SaveChangesAsync();
            }
        }
        set.NotifyOwnershipChanged();
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
            StatusText = $"{SelectedSet.Name} — {SelectedSet.CompletionText}";
        }
        finally
        {
            _isBulkUpdate = false;
        }
    }
}
