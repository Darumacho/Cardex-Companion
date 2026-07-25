using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32.SafeHandles;

namespace Cardex;

internal static class CursorHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO { public bool fIcon; public int xHotspot; public int yHotspot; public IntPtr hbmMask; public IntPtr hbmColor; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; public int bmiColors; }

    [DllImport("user32.dll")] private static extern IntPtr CreateIconIndirect(ref ICONINFO ii);
    [DllImport("gdi32.dll")]  private static extern bool DeleteObject(IntPtr h);
    [DllImport("gdi32.dll")]  private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")]  private static extern IntPtr CreateBitmap(int w, int h, uint planes, uint bpp, byte[] bits);

    public static Cursor CreateMagnifier()
    {
        try
        {
            const int S = 32;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var dark = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), 3.5);
                var lite = new Pen(Brushes.White, 2.2);
                dc.DrawEllipse(null, dark, new Point(12, 12), 9, 9);
                dc.DrawEllipse(null, lite, new Point(12, 12), 8.5, 8.5);
                dc.DrawLine(dark, new Point(19, 19), new Point(29, 29));
                dc.DrawLine(lite, new Point(19, 19), new Point(28, 28));
            }

            var rtb = new RenderTargetBitmap(S, S, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var pixels = new byte[S * S * 4];
            rtb.CopyPixels(pixels, S * 4, 0);

            // Pbgra32 → Bgra32 (un-premultiply alpha)
            for (int i = 0; i < pixels.Length; i += 4)
            {
                int a = pixels[i + 3];
                if (a > 0 && a < 255)
                {
                    pixels[i]     = (byte)Math.Min(255, pixels[i]     * 255 / a);
                    pixels[i + 1] = (byte)Math.Min(255, pixels[i + 1] * 255 / a);
                    pixels[i + 2] = (byte)Math.Min(255, pixels[i + 2] * 255 / a);
                }
            }

            // 32bpp top-down DIB (biHeight < 0 → top-down, no vertical flip needed)
            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = S, biHeight = -S,
                    biPlanes = 1, biBitCount = 32
                }
            };
            var hColor = CreateDIBSection(IntPtr.Zero, ref bmi, 0, out IntPtr pBits, IntPtr.Zero, 0);
            Marshal.Copy(pixels, 0, pBits, pixels.Length);

            // 1bpp all-zero mask (Vista+ ignores mask when 32bpp alpha is present)
            var hMask = CreateBitmap(S, S, 1, 1, new byte[S * S / 8]);

            var ii = new ICONINFO { fIcon = false, xHotspot = 6, yHotspot = 6, hbmMask = hMask, hbmColor = hColor };
            var hIcon = CreateIconIndirect(ref ii);
            DeleteObject(hColor);
            DeleteObject(hMask);

            return hIcon != IntPtr.Zero
                ? CursorInteropHelper.Create(new SafeFileHandle(hIcon, true))
                : Cursors.Hand;
        }
        catch { return Cursors.Hand; }
    }
}
