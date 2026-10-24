using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cardex.Platform;

namespace Cardex.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, Window_PreviewKeyDown, RoutingStrategies.Tunnel);
    }

    // MainViewModel appelle Show() comme côté WPF, où l'Owner était fixé dans le constructeur ;
    // en Avalonia le propriétaire se passe à Show(owner).
    public new void Show()
    {
        if (Dialogs.MainWindow is { } owner) base.Show(owner);
        else base.Show();
    }

    // Menu Help → Keyboard Shortcuts
    public void ShowShortcutsTab() => Tabs.SelectedItem = ShortcutsTab;

    private void Window_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Meta && Dialogs.MainWindow is MainWindow mw)
        {
            mw.FocusSearch();
            e.Handled = true;
        }
    }
}
