using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace PokemonRedRL.ControlPanel.Services;

/// <summary>
/// Captures a still frame of an mGBA window using PrintWindow, per the Task 1 frame-capture spike
/// decision (docs/tasks — PrintWindow chosen over other capture APIs). Used for per-tile previews,
/// throttled by the caller (see MainViewModel's preview timer) to the frozen 10 FPS/tile UX contract
/// — this service itself is stateless and does one capture per call.
/// </summary>
public interface IPreviewCaptureService
{
    BitmapSource? Capture(IntPtr windowHandle);
}

public class PreviewCaptureService : IPreviewCaptureService
{
    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    public BitmapSource? Capture(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return null;
        if (!GetClientRect(windowHandle, out var rect)) return null;

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) return null;

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var hdc = graphics.GetHdc();
        try
        {
            if (!PrintWindow(windowHandle, hdc, PW_RENDERFULLCONTENT)) return null;
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }

        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
