using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;

namespace Cardex.ViewModels;

public partial class SetViewModel : ObservableObject
{
    public string SetId { get; }
    public string Name { get; }
    public int Total { get; }
    public string Series { get; }
    public string ReleaseDate { get; }
    public string LogoUrl { get; }
    public string SymbolUrl { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private BitmapImage? _logoImage;
    [ObservableProperty] private BitmapImage? _symbolImage;

    public ObservableCollection<CardViewModel> Cards { get; } = [];

    public int OwnedCount => Cards.Count(c => c.IsOwned);

    public string CompletionText => $"{OwnedCount} / {Total}";

    public bool AllOwned => Cards.Count > 0 && Cards.All(c => c.IsOwned);

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

    public void NotifyOwnershipChanged()
    {
        OnPropertyChanged(nameof(OwnedCount));
        OnPropertyChanged(nameof(CompletionText));
        OnPropertyChanged(nameof(AllOwned));
        OnPropertyChanged(nameof(ToggleAllLabel));
    }
}
