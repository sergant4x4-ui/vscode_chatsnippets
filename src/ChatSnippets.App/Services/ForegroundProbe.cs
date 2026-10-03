using System.Diagnostics;
using ChatSnippets.App.Interop;
using ChatSnippets.Core;

namespace ChatSnippets.App.Services;

internal sealed class ForegroundProbe : IForegroundWindow
{
    public string? GetProcessName()
    {
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        try { return Process.GetProcessById((int)pid).ProcessName; }
        catch (ArgumentException) { return null; }           // процесс уже завершился
        catch (InvalidOperationException) { return null; }
    }
}
