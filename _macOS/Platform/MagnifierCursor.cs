using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Cardex.Platform;

// Curseur « loupe » sur les images de carte. Remplace CursorHelper.cs (P/Invoke Win32) :
// même dessin, mais Avalonia accepte directement un bitmap comme curseur.
public static class MagnifierCursor
{
    public static Cursor Create()
    {
        try
        {
            const int S = 32;
            var bitmap = new RenderTargetBitmap(new PixelSize(S, S));
            using (var dc = bitmap.CreateDrawingContext())
            {
                var dark = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), 3.5);
                var lite = new Pen(Brushes.White, 2.2);
                dc.DrawEllipse(null, dark, new Point(12, 12), 9, 9);
                dc.DrawEllipse(null, lite, new Point(12, 12), 8.5, 8.5);
                dc.DrawLine(dark, new Point(19, 19), new Point(29, 29));
                dc.DrawLine(lite, new Point(19, 19), new Point(28, 28));
            }
            return new Cursor(bitmap, new PixelPoint(6, 6));
        }
        catch
        {
            return new Cursor(StandardCursorType.Hand);
        }
    }
}
