using System.Windows.Interop;
using ChatSnippets.App.Interop;
using ChatSnippets.Core;

namespace ChatSnippets.App.Services;

/// <summary>Глобальные хоткеи через RegisterHotKey на скрытом message-only окне.</summary>
internal sealed class HotkeyService : IDisposable
{
    const int ProbeId = 0x7FFF;
    readonly HwndSource _source;
    readonly Dictionary<int, string> _idToSnippet = new();
    int _nextId = 1;

    public event Action<string>? Pressed;

    public HotkeyService()
    {
        // HWND_MESSAGE = -3: окно без видимости, получающее только сообщения.
        _source = new HwndSource(new HwndSourceParameters("ChatSnippetsHotkeys") { ParentWindow = new IntPtr(-3) });
        _source.AddHook(WndProc);
    }

    public bool TryRegister(string snippetId, Hotkey hotkey)
    {
        var id = _nextId++;
        if (!Native.RegisterHotKey(_source.Handle, id, (uint)hotkey.Modifiers | Native.MOD_NOREPEAT, (uint)hotkey.VirtualKey))
            return false;
        _idToSnippet[id] = snippetId;
        return true;
    }

    /// <summary>Свободно ли сочетание в системе (проба: занять и сразу освободить).</summary>
    public bool IsFree(Hotkey hotkey)
    {
        var ok = Native.RegisterHotKey(_source.Handle, ProbeId, (uint)hotkey.Modifiers, (uint)hotkey.VirtualKey);
        if (ok) Native.UnregisterHotKey(_source.Handle, ProbeId);
        return ok;
    }

    public void UnregisterAll()
    {
        foreach (var id in _idToSnippet.Keys) Native.UnregisterHotKey(_source.Handle, id);
        _idToSnippet.Clear();
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && _idToSnippet.TryGetValue(wParam.ToInt32(), out var snippetId))
        {
            handled = true;
            Pressed?.Invoke(snippetId);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.Dispose();
    }
}
