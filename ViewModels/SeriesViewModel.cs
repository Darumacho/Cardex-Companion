using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Cardex.ViewModels;

public partial class SeriesViewModel : ObservableObject
{
    public string SeriesName { get; }

    [ObservableProperty] private bool _isExpanded = true;

    public ObservableCollection<SetViewModel> Sets { get; } = [];

    public SeriesViewModel(string seriesName) => SeriesName = seriesName;
}
