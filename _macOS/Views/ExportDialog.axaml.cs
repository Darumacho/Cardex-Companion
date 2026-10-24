using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Cardex.Views;

public enum ExportMode   { Collection, Wants, Duplicates, Missing }
public enum ExportFormat { Normal, Cardmarket, TcgPlayer }

// Affichée par Dialogs.ExportOptionsAsync via ShowDialog<bool> (true = Export).
public partial class ExportDialog : Window
{
    public ExportMode   SelectedMode   { get; private set; } = ExportMode.Collection;
    public ExportFormat SelectedFormat { get; private set; } = ExportFormat.Normal;

    public ExportDialog()
    {
        InitializeComponent();
    }

    private void Export_Click(object? sender, RoutedEventArgs e)
    {
        SelectedMode = RbWants.IsChecked == true      ? ExportMode.Wants
                     : RbDuplicates.IsChecked == true ? ExportMode.Duplicates
                     : RbMissing.IsChecked == true    ? ExportMode.Missing
                     : ExportMode.Collection;

        SelectedFormat = RbCardmarket.IsChecked == true ? ExportFormat.Cardmarket
                       : RbTcgPlayer.IsChecked == true  ? ExportFormat.TcgPlayer
                       : ExportFormat.Normal;

        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
        => Close(false);
}
