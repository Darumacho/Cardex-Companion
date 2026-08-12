using System.Windows;

namespace Cardex.Views;

public partial class PasteDeckDialog : Window
{
    public string DeckName { get; private set; } = "Imported Deck";
    public string DeckText { get; private set; } = "";

    public PasteDeckDialog()
    {
        InitializeComponent();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        DeckName = string.IsNullOrWhiteSpace(DeckNameBox.Text) ? "Imported Deck" : DeckNameBox.Text.Trim();
        DeckText = DeckTextBox.Text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;
}
