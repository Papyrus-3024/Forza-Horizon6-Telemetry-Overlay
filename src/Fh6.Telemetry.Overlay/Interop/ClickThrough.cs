using System.Runtime.InteropServices;

namespace Fh6.Telemetry.Overlay.Interop;

/// <summary>Toggles a window between click-through and interactive via extended styles.</summary>
public static class ClickThrough
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    public static void SetClickThrough(IntPtr hwnd, bool enabled)
    {
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        style |= WS_EX_LAYERED;
        if (enabled) style |= WS_EX_TRANSPARENT;
        else style &= ~WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, style);
    }
}
