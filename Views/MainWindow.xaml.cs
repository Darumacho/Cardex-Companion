using Cardex.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Cardex.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Charge les images au fur et à mesure que les cartes deviennent visibles
    private async void CardsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        await LoadVisibleCardImagesAsync();
    }

    private async void CardsItemsControl_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadVisibleCardImagesAsync();
    }

    private void RarityFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is string rarity
            && DataContext is MainViewModel vm && vm.SelectedSet is not null)
            vm.SelectedSet.SelectedRarity = rarity;
    }

    private async Task LoadVisibleCardImagesAsync()
    {
        if (DataContext is not MainViewModel vm || vm.SelectedSet is null) return;
        var tasks = vm.SelectedSet.Cards
            .Where(c => c.CardImage is null && !c.IsLoadingImage)
            .Take(20)
            .Select(c => c.LoadImageAsync());
        await Task.WhenAll(tasks);
    }
}
