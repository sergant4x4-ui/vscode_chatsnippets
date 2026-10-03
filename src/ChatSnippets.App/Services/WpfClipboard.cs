using System.Runtime.InteropServices;
using System.Windows;
using ChatSnippets.Core;

namespace ChatSnippets.App.Services;

/// <summary>Только UI-поток. COMException (CLIPBRD_E_CANT_OPEN и т.п.) → ClipboardBusyException.</summary>
internal sealed class WpfClipboard : IClipboardAccess
{
    public string? ReadText()
    {
        try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
        catch (COMException) { throw new ClipboardBusyException(); }
    }

    public void WriteText(string text)
    {
        try { Clipboard.SetDataObject(text, copy: false); }
        catch (COMException) { throw new ClipboardBusyException(); }
    }

    public void Restore(string? previous)
    {
        try
        {
            if (previous is null) Clipboard.Clear();
            else Clipboard.SetDataObject(previous, copy: false);
        }
        catch (COMException) { throw new ClipboardBusyException(); }
    }
}
