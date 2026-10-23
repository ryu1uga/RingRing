using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace RingRing.Hotkeys;

/// <summary>
/// Atajos globales de teclado (funcionan aunque RingRing no tenga el foco).
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001, ModControl = 0x0002, ModShift = 0x0004, ModWin = 0x0008, ModNoRepeat = 0x4000;

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _handlers = [];
    private int _nextId = 1;

    public HotkeyManager()
    {
        // Ventana invisible solo para recibir WM_HOTKEY.
        _source = new HwndSource(new HwndSourceParameters("RingRingHotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE: ventana solo de mensajes
        });
        _source.AddHook(WndProc);
    }

    /// <summary>Registra un atajo. Devuelve un error legible o null si salió bien.</summary>
    public string? Register(string gesture, Action handler)
    {
        if (!TryParse(gesture, out var modifiers, out var key, out var parseError))
            return parseError;

        var id = _nextId++;
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!RegisterHotKey(_source.Handle, id, modifiers | ModNoRepeat, vk))
            return $"El atajo {gesture} ya lo usa otra aplicación.";

        _handlers[id] = handler;
        return null;
    }

    public void UnregisterAll()
    {
        foreach (var id in _handlers.Keys)
            UnregisterHotKey(_source.Handle, id);
        _handlers.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    /// <summary>Convierte "Ctrl+Alt+1" en modificadores + tecla.</summary>
    public static bool TryParse(string gesture, out uint modifiers, out Key key, out string? error)
    {
        modifiers = 0;
        key = Key.None;
        error = null;

        if (string.IsNullOrWhiteSpace(gesture))
        {
            error = "Atajo vacío.";
            return false;
        }

        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts[..^1])
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= ModControl; break;
                case "alt": modifiers |= ModAlt; break;
                case "shift": modifiers |= ModShift; break;
                case "win" or "windows": modifiers |= ModWin; break;
                default:
                    error = $"Modificador desconocido: {part}";
                    return false;
            }
        }

        var keyText = parts[^1];
        // Permite escribir "1" en vez de "D1".
        if (keyText.Length == 1 && char.IsDigit(keyText[0])) keyText = "D" + keyText;

        if (!Enum.TryParse(keyText, ignoreCase: true, out key) || key == Key.None)
        {
            error = $"Tecla desconocida: {parts[^1]}";
            return false;
        }

        if (modifiers == 0)
        {
            error = "El atajo necesita al menos Ctrl, Alt, Shift o Win.";
            return false;
        }

        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _handlers.TryGetValue(wParam.ToInt32(), out var handler))
        {
            handler();
            handled = true;
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
