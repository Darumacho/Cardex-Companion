using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Cardex.ViewModels;

public partial class SeriesViewModel : ObservableObject
{
    public string SeriesName { get; }
    public bool IsFavoriteGroup { get; }
    public bool IsMyCollectionGroup { get; }
    public bool IsAllSetsHeader { get; }

    [ObservableProperty] private bool _isExpanded = true;

    public ObservableCollection<SetViewModel> Sets { get; } = [];

    public bool IsRealSeriesGroup => !IsFavoriteGroup && !IsMyCollectionGroup && !IsAllSetsHeader;

    public bool? AreAllIgnored => Sets.Count == 0 ? false
        : Sets.All(s => s.IsIgnored) ? true
        : Sets.Any(s => s.IsIgnored) ? null
        : false;

    public bool HasVisibleSets => IsAllSetsHeader || Sets.Any(s => !s.IsIgnored);

    public void NotifyIgnoredChanged()
    {
        OnPropertyChanged(nameof(AreAllIgnored));
        OnPropertyChanged(nameof(HasVisibleSets));
    }

    public SeriesViewModel(string seriesName,
        bool isFavoriteGroup = false,
        bool isMyCollectionGroup = false,
        bool isAllSetsHeader = false)
    {
        SeriesName = seriesName;
        IsFavoriteGroup = isFavoriteGroup;
        IsMyCollectionGroup = isMyCollectionGroup;
        IsAllSetsHeader = isAllSetsHeader;
    }
}
