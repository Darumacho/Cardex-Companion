using System.Windows;
using System.Windows.Input;

namespace Cardex.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.FocusSearch();
                e.Handled = true;
            }
        }
    }
}
