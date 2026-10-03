using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using ChatSnippets.Core;

namespace ChatSnippets.App.Services;

/// <summary>Только UI-поток. COMException (CLIPBRD_E_CANT_OPEN и т.п.) → ClipboardBusyException.</summary>
internal sealed class WpfClipboard : IClipboardAccess
{
    /// <summary>В буфере что-то есть, но скопировать это не удалось: восстановление ничего не трогает.</summary>
    static readonly object NotRestorable = new();

    /// <summary>Копия ВСЕХ форматов буфера (файлы, картинка, форматированный текст), а не только текста.</summary>
    public object? Snapshot()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            var formats = data?.GetFormats(false);
            if (data is null || formats is null || formats.Length == 0) return null;

            var copy = new DataObject();
            var copied = false;
            foreach (var format in formats)
            {
                try
                {
                    var value = data.GetData(format, false);
                    if (value is null) continue;
                    copy.SetData(format, value);
                    copied = true;
                }
                catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException or IOException) { }
            }
            return copied ? copy : NotRestorable;
        }
        catch (COMException) { throw new ClipboardBusyException(); }
    }

    public string? ReadText()
    {
        try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
        catch (COMException) { throw new ClipboardBusyException(); }
    }

    public void WriteText(string text)
    {
        try { Clipboard.SetDataObject(text, copy: true); }
        catch (COMException) { throw new ClipboardBusyException(); }
    }

    public void Restore(object? snapshot)
    {
        if (ReferenceEquals(snapshot, NotRestorable)) return;
        try
        {
            if (snapshot is DataObject data) Clipboard.SetDataObject(data, copy: true);
            else Clipboard.Clear();
        }
        catch (COMException) { throw new ClipboardBusyException(); }
    }
}
