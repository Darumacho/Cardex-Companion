using Avalonia;

namespace Cardex;

class Program
{
    // Rien qui dépende d'Avalonia ou d'un SynchronizationContext avant AppMain.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Utilisé aussi par le designer Avalonia.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
}
