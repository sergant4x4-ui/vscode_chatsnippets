namespace ChatSnippets.Core;

public interface IForegroundWindow { string? GetProcessName(); }

public interface IClipboardAccess
{
    string? ReadText();
    void WriteText(string text);
    /// <summary>previous == null → буфер очищается (в нём не было текста).</summary>
    void Restore(string? previous);
}

public interface IKeySender
{
    /// <summary>Дожидается отпускания Ctrl/Alt/Shift/Win и посылает Ctrl+V.</summary>
    Task SendCtrlVAsync();
}

public sealed class ClipboardBusyException : Exception { }

public enum PasteResult { Pasted, NotVsCode, ClipboardBusy }

public sealed class PasteCoordinator(
    IForegroundWindow foreground, IClipboardAccess clipboard, IKeySender keys, Func<TimeSpan, Task> delay)
{
    static readonly string[] VsCodeProcesses = { "Code", "Code - Insiders" };
    const int Attempts = 3;
    static readonly TimeSpan RetryPause = TimeSpan.FromMilliseconds(50);
    static readonly TimeSpan RestorePause = TimeSpan.FromMilliseconds(150);

    public async Task<PasteResult> PasteAsync(string text)
    {
        var process = foreground.GetProcessName();
        if (process is null || !VsCodeProcesses.Contains(process, StringComparer.OrdinalIgnoreCase))
            return PasteResult.NotVsCode;

        string? previous;
        try
        {
            previous = await RetryAsync(clipboard.ReadText);
            await RetryAsync(() => { clipboard.WriteText(text); return 0; });
        }
        catch (ClipboardBusyException)
        {
            return PasteResult.ClipboardBusy;
        }

        await keys.SendCtrlVAsync();
        await delay(RestorePause);

        try { await RetryAsync(() => { clipboard.Restore(previous); return 0; }); }
        catch (ClipboardBusyException) { /* текст уже вставлен; старый буфер не вернули — не критично */ }

        return PasteResult.Pasted;
    }

    async Task<T> RetryAsync<T>(Func<T> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { return action(); }
            catch (ClipboardBusyException) when (attempt < Attempts) { await delay(RetryPause); }
        }
    }
}
