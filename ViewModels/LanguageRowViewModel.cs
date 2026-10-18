using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cardex.ViewModels;

// Une ligne du select "copies possédées par langue" sur une carte (visible uniquement quand plus
// d'une langue est suivie, voir CardViewModel.IsMultiLanguageMode). Checked est dérivé de Quantity
// (pas de case séparée) : cocher/décocher la langue est un raccourci pour 1/0 exemplaire.
public partial class LanguageRowViewModel : ObservableObject
{
    public string Code { get; }
    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Checked))]
    private int _quantity;

    public bool Checked => Quantity > 0;

    private readonly Action _onChanged;

    public LanguageRowViewModel(string code, string label, int quantity, Action onChanged)
    {
        Code = code;
        Label = label;
        _quantity = quantity;
        _onChanged = onChanged;
    }

    partial void OnQuantityChanged(int value) => _onChanged();

    [RelayCommand] private void Increment() => Quantity++;
    [RelayCommand] private void Decrement() => Quantity = Math.Max(0, Quantity - 1);
    [RelayCommand] private void Toggle() => Quantity = Quantity > 0 ? 0 : 1;
}
