using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Media;

namespace Cardex.ViewModels;

public partial class TagViewModel : ObservableObject
{
    public static readonly TagViewModel NoTag = new() { Id = 0, Name = "—", Color = "#444455" };

    public int Id { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Brush))]
    private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Brush))]
    private string _color = "#8888aa";

    public SolidColorBrush Brush =>
        new((Color)ColorConverter.ConvertFromString(Color));

    [RelayCommand]
    private void SetColor(string color) => Color = color;
}
