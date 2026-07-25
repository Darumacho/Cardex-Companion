using System.Windows;

namespace Cardex.Views;

public enum ExportMode { Collection, Wants, Duplicates }

public partial class ExportDialog : Window
{
    public ExportMode SelectedMode { get; private set; }

    public ExportDialog()
    {
        InitializeComponent();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        SelectedMode = RadioWants.IsChecked == true ? ExportMode.Wants
                     : RadioDuplicates.IsChecked == true ? ExportMode.Duplicates
                     : ExportMode.Collection;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
