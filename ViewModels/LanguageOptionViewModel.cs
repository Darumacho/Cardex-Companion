using CommunityToolkit.Mvvm.ComponentModel;

namespace Cardex.ViewModels;

// Une ligne de la liste "Languages" dans Settings — langue suivie ou non pour le décompte de
// collection. L'anglais est verrouillé (toujours suivi, données historiques implicitement en EN).
public partial class LanguageOptionViewModel : ObservableObject
{
    public string Code { get; }
    public string Label { get; }
    public bool IsLocked { get; }
    public bool IsEnabled => !IsLocked;

    [ObservableProperty] private bool _isTracked;

    public LanguageOptionViewModel(string code, string label, bool isTracked, bool isLocked = false)
    {
        Code = code;
        Label = label;
        _isTracked = isTracked;
        IsLocked = isLocked;
    }
}
