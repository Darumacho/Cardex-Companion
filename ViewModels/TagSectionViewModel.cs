using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Cardex.ViewModels;

public partial class TagSectionViewModel : ObservableObject
{
    public TagViewModel Tag { get; }
    public ObservableCollection<SearchResultViewModel> Cards { get; } = [];

    [ObservableProperty] private bool _isExpanded = true;

    [RelayCommand] private void ToggleExpanded() => IsExpanded = !IsExpanded;

    public TagSectionViewModel(TagViewModel tag) => Tag = tag;
}
