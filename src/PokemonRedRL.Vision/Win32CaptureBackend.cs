using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PokemonRedRL.Vision;

public sealed class Win32CaptureBackend : IWindowCaptureBackend
{
    public Win32CaptureBackend()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("PrintWindow capture requires an interactive Windows desktop.");
    }

    public WindowState? Inspect(long windowHandle)
    {
        var previousDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            var handle = new IntPtr(windowHandle);
            if (!IsWindow(handle) || !GetClientRect(handle, out var rect)) return null;
            GetWindowThreadProcessId(handle, out var pid);
            using var process = Process.GetProcessById(checked((int)pid));
            return new WindowState(process.Id, process.StartTime.ToUniversalTime().Ticks,
                rect.Right - rect.Left, rect.Bottom - rect.Top, GetDpiForWindow(handle), IsIconic(handle));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return null;
        }
        finally
        {
            if (previousDpi != IntPtr.Zero) SetThreadDpiAwarenessContext(previousDpi);
        }
    }

    public byte[]? CaptureRgb(long windowHandle, FrameRegion region, WindowState state)
    {
        if (!region.Fits(state.Width, state.Height) || (long)state.Width * state.Height > 16_777_216)
            return null;
        var previousDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
        IntPtr screen = IntPtr.Zero, context = IntPtr.Zero, bitmap = IntPtr.Zero, previousBitmap = IntPtr.Zero;
        try
        {
            screen = GetDC(IntPtr.Zero);
            if (screen == IntPtr.Zero) return null;
            context = CreateCompatibleDC(screen);
            if (context == IntPtr.Zero) return null;
            var info = new BitmapInfo
            {
                Size = (uint)Marshal.SizeOf<BitmapInfo>(), Width = state.Width, Height = -state.Height,
                Planes = 1, BitCount = 32
            };
            bitmap = CreateDIBSection(screen, ref info, 0, out var bits, IntPtr.Zero, 0);
            if (bitmap == IntPtr.Zero || bits == IntPtr.Zero) return null;
            previousBitmap = SelectObject(context, bitmap);
            if (previousBitmap == IntPtr.Zero || previousBitmap == new IntPtr(-1)) return null;
            if (!PrintWindow(new IntPtr(windowHandle), context, 3)) return null;
            GdiFlush();
            var row = new byte[checked(region.Width * 4)];
            var rgb = new byte[checked(region.Width * region.Height * 3)];
            for (var y = 0; y < region.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(bits, checked(((region.Y + y) * state.Width + region.X) * 4)), row, 0, row.Length);
                for (var x = 0; x < region.Width; x++)
                {
                    var target = (y * region.Width + x) * 3;
                    rgb[target] = row[x * 4 + 2];
                    rgb[target + 1] = row[x * 4 + 1];
                    rgb[target + 2] = row[x * 4];
                }
            }
            return rgb;
        }
        finally
        {
            if (previousBitmap != IntPtr.Zero && previousBitmap != new IntPtr(-1)) SelectObject(context, previousBitmap);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (context != IntPtr.Zero) DeleteDC(context);
            if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
            if (previousDpi != IntPtr.Zero) SetThreadDpiAwarenessContext(previousDpi);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter;
        public uint ClrUsed, ClrImportant;
    }

    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr handle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(IntPtr handle, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint pid);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr handle);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr handle, IntPtr context);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PrintWindow(IntPtr handle, IntPtr context, uint flags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr context);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr context, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr context, IntPtr bitmap);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr handle);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(IntPtr context);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GdiFlush();
}