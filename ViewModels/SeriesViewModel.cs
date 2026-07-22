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
