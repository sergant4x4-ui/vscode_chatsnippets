namespace ChatSnippets.Core;

public interface IForegroundWindow { string? GetProcessName(); }

public interface IClipboardAccess
{
    /// <summary>Снимок всего содержимого буфера (любые форматы). null — буфер пуст.</summary>
    object? Snapshot();
    /// <summary>Текст из буфера или null, если там не текст.</summary>
    string? ReadText();
    void WriteText(string text);
    /// <summary>snapshot == null → буфер очищается (он был пуст).</summary>
    void Restore(object? snapshot);
}

public interface IKeySender
{
    /// <summary>Дожидается отпускания Ctrl/Alt/Shift/Win и посылает Ctrl+клавиша (vk — виртуальный код: C, V, A).</summary>
    Task SendCtrlAsync(int vk);
}

public sealed class ClipboardBusyException : Exception { }

public enum PasteResult { Pasted, NotVsCode, ClipboardBusy }

public enum LayoutFixResult { Fixed, NotVsCode, NothingToFix, ClipboardBusy }

public sealed class PasteCoordinator(
    IForegroundWindow foreground, IClipboardAccess clipboard, IKeySender keys, Func<TimeSpan, Task> delay)
{
    static readonly string[] VsCodeProcesses = { "Code", "Code - Insiders" };
    const int Attempts = 3, VkA = 0x41, VkC = 0x43, VkV = 0x56;
    readonly SemaphoreSlim _gate = new(1, 1);   // вставки идут строго по одной: иначе буфер перепутается
    static readonly TimeSpan RetryPause = TimeSpan.FromMilliseconds(50);
    static readonly TimeSpan CopyPause = TimeSpan.FromMilliseconds(150);
    static readonly TimeSpan RestorePause = TimeSpan.FromMilliseconds(700);

    public async Task<PasteResult> PasteAsync(string text)
    {
        await _gate.WaitAsync();
        try { return await PasteCoreAsync(text); }
        finally { _gate.Release(); }
    }

    async Task<PasteResult> PasteCoreAsync(string text)
    {
        if (!IsVsCodeActive()) return PasteResult.NotVsCode;

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

        await keys.SendCtrlAsync(VkV);
        await delay(RestorePause);

        try { await RetryAsync(() => { clipboard.Restore(previous); return 0; }); }
        catch (ClipboardBusyException) { /* текст уже вставлен; старый буфер не вернули — не критично */ }

        return PasteResult.Pasted;
    }

    bool IsVsCodeActive()
    {
        var process = foreground.GetProcessName();
        return process is not null && VsCodeProcesses.Contains(process, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Берёт выделенный в чате текст (если выделения нет — весь текст поля через Ctrl+A), меняет раскладку
    /// и вставляет обратно поверх. Прежний буфер возвращается.
    /// </summary>
    public async Task<LayoutFixResult> FixLayoutAsync()
    {
        await _gate.WaitAsync();
        try { return await FixLayoutCoreAsync(); }
        finally { _gate.Release(); }
    }

    async Task<LayoutFixResult> FixLayoutCoreAsync()
    {
        if (!IsVsCodeActive()) return LayoutFixResult.NotVsCode;

        object? previous = null;
        var haveSnapshot = false;
        string? original;
        var sentinel = "\u0001ChatSnippets-" + Guid.NewGuid().ToString("N");
        try
        {
            previous = await RetryAsync(clipboard.Snapshot);
            haveSnapshot = true;
            await RetryAsync(() => { clipboard.WriteText(sentinel); return 0; });

            original = await CopyAsync();
            if (original == sentinel)                    // выделения нет → выделяем всё поле и копируем
            {
                await keys.SendCtrlAsync(VkA);
                await delay(CopyPause);
                original = await CopyAsync();
            }
        }
        catch (ClipboardBusyException)
        {
            if (haveSnapshot) await RestoreQuietlyAsync(previous);
            return LayoutFixResult.ClipboardBusy;
        }

        var converted = string.IsNullOrEmpty(original) || original == sentinel ? null : LayoutConverter.Convert(original);
        if (converted is null || converted == original)
        {
            await RestoreQuietlyAsync(previous);
            return LayoutFixResult.NothingToFix;
        }

        try { await RetryAsync(() => { clipboard.WriteText(converted); return 0; }); }
        catch (ClipboardBusyException)
        {
            await RestoreQuietlyAsync(previous);
            return LayoutFixResult.ClipboardBusy;
        }

        await keys.SendCtrlAsync(VkV);
        await delay(RestorePause);
        await RestoreQuietlyAsync(previous);
        return LayoutFixResult.Fixed;
    }

    async Task<string?> CopyAsync()
    {
        await keys.SendCtrlAsync(VkC);
        await delay(CopyPause);
        return await RetryAsync(clipboard.ReadText);
    }

    async Task RestoreQuietlyAsync(object? snapshot)
    {
        try { await RetryAsync(() => { clipboard.Restore(snapshot); return 0; }); }
        catch (ClipboardBusyException) { /* не критично: текст уже обработан */ }
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
