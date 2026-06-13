using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Fh6.Telemetry.Overlay.Interop;

/// <summary>Registers system-wide hotkeys that fire even while the game has focus.</summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public GlobalHotkey(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd)!;
        _source.AddHook(WndProc);
    }

    /// <summary>Registers a virtual-key code (no modifiers). Returns false if registration failed.</summary>
    public bool Register(uint virtualKey, Action onPressed)
    {
        var id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, 0, virtualKey))
            return false;
        _actions[id] = onPressed;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys)
            UnregisterHotKey(_hwnd, id);
        _actions.Clear();
        _source.RemoveHook(WndProc);
    }
}
