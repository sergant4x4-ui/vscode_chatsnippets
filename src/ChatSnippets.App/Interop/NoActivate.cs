using System.Windows;
using System.Windows.Interop;

namespace ChatSnippets.App.Interop;

internal static class NoActivate
{
    /// <summary>Окно не забирает фокус и не показывается в Alt+Tab. Вызывать из SourceInitialized.</summary>
    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
    }
}
