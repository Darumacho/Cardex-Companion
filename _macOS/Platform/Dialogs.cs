using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Platform.Storage;
using Cardex.Views;

namespace Cardex.Platform;

// Remplace MessageBox.Show et Microsoft.Win32.Open/SaveFileDialog du projet WPF.
// Avalonia n'a pas de dialogue modal bloquant : tout est asynchrone (await).
public static class Dialogs
{
    public static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    // ── Messages ─────────────────────────────────────────────────────────────

    public static async Task InfoAsync(string message, string title)
        => await ShowMessageAsync(message, title, ["OK"]);

    // Équivalent de MessageBoxButton.YesNo : true si l'utilisateur clique « Yes ».
    public static async Task<bool> ConfirmAsync(string message, string title)
        => await ShowMessageAsync(message, title, ["Yes", "No"]) == "Yes";

    private static async Task<string?> ShowMessageAsync(string message, string title, string[] buttons)
    {
        var owner = MainWindow;
        string? result = null;

        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 320,
            MaxWidth = 520,
            CanResize = false,
            ShowInTaskbar = false,
            // Mêmes couleurs que les autres dialogues de l'app (ExportDialog, PasteDeckDialog)
            Background = new SolidColorBrush(Color.Parse("#1a1a2e")),
            Foreground = new SolidColorBrush(Color.Parse("#e0e0e0")),
            WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        foreach (var label in buttons)
        {
            var button = new Button
            {
                Content = label,
                Width = 80,
                Height = 30,
                // Bouton principal en vert (comme « Export » / « Import »), les autres en bleu
                Background = new SolidColorBrush(Color.Parse(label == buttons[0] ? "#27ae60" : "#0f3460")),
            };
            if (Application.Current?.TryGetResource("DialogButtonStyle", null, out var theme) == true)
                button.Theme = theme as ControlTheme;
            button.IsDefault = label == buttons[0];
            button.IsCancel  = label == buttons[^1];
            button.Click += (_, _) => { result = label; dialog.Close(); };
            buttonPanel.Children.Add(button);
        }

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 12 },
                buttonPanel,
            },
        };

        if (owner is not null) await dialog.ShowDialog(owner);
        else
        {
            var closed = new TaskCompletionSource();
            dialog.Closed += (_, _) => closed.TrySetResult();
            dialog.Show();
            await closed.Task;
        }
        return result;
    }

    // ── Fichiers ─────────────────────────────────────────────────────────────

    // filterName/extension : ex. ("Text file", "txt"). Renvoie le chemin local, ou null si annulé.
    public static async Task<string?> OpenFileAsync(string title, string filterName, string extension)
    {
        var storage = MainWindow?.StorageProvider;
        if (storage is null) return null;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(filterName) { Patterns = [$"*.{extension}"] }],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public static async Task<string?> SaveFileAsync(string title, string filterName, string extension, string suggestedName)
    {
        var storage = MainWindow?.StorageProvider;
        if (storage is null) return null;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType(filterName) { Patterns = [$"*.{extension}"] }],
        });
        return file?.TryGetLocalPath();
    }

    // ── Dialogues applicatifs ────────────────────────────────────────────────

    // Dialogue « coller une decklist » : (nom du deck, texte), ou null si annulé.
    public static async Task<(string Name, string Text)?> PasteDeckAsync()
    {
        if (MainWindow is not { } owner) return null;
        var dlg = new PasteDeckDialog();
        return await dlg.ShowDialog<bool>(owner) ? (dlg.DeckName, dlg.DeckText) : null;
    }

    // Dialogue d'export CSV : (périmètre, format), ou null si annulé.
    public static async Task<(ExportMode Mode, ExportFormat Format)?> ExportOptionsAsync()
    {
        if (MainWindow is not { } owner) return null;
        var dlg = new ExportDialog();
        return await dlg.ShowDialog<bool>(owner) ? (dlg.SelectedMode, dlg.SelectedFormat) : null;
    }
}
