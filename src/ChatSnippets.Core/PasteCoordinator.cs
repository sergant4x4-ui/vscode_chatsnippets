namespace ChatSnippets.Core;

public interface IForegroundWindow { string? GetProcessName(); }

public interface IClipboardAccess
{
    /// <summary>Снимок всего содержимого буфера (любые форматы). null — буфер пуст.</summary>
    object? Snapshot();
    void WriteText(string text);
    /// <summary>snapshot == null → буфер очищается (он был пуст).</summary>
    void Restore(object? snapshot);
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
    readonly SemaphoreSlim _gate = new(1, 1);   // вставки идут строго по одной: иначе буфер перепутается
    static readonly TimeSpan RetryPause = TimeSpan.FromMilliseconds(50);
    static readonly TimeSpan RestorePause = TimeSpan.FromMilliseconds(700);

    public async Task<PasteResult> PasteAsync(string text)
    {
        await _gate.WaitAsync();
        try { return await PasteCoreAsync(text); }
        finally { _gate.Release(); }
    }

    async Task<PasteResult> PasteCoreAsync(string text)
    {
        var process = foreground.GetProcessName();
        if (process is null || !VsCodeProcesses.Contains(process, StringComparer.OrdinalIgnoreCase))
            return PasteResult.NotVsCode;

        object? previous;
        try
        {
            previous = await RetryAsync(clipboard.Snapshot);
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
