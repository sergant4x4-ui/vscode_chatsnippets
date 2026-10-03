using ChatSnippets.Core;
using Xunit;

namespace ChatSnippets.Core.Tests;

public class PasteCoordinatorTests
{
    sealed class Fakes : IForegroundWindow, IClipboardAccess, IKeySender
    {
        public string? Process = "Code";
        public string? ClipboardText = "старое";
        public int BusyFailuresLeft;
        public List<string> Log = new();

        public string? GetProcessName() => Process;

        void MaybeBusy() { if (BusyFailuresLeft > 0) { BusyFailuresLeft--; throw new ClipboardBusyException(); } }
        public object? Snapshot() { MaybeBusy(); Log.Add("read"); return ClipboardText; }
        public void WriteText(string text) { MaybeBusy(); Log.Add("write:" + text); ClipboardText = text; }
        public void Restore(object? previous) { MaybeBusy(); Log.Add("restore:" + ((string?)previous ?? "<null>")); ClipboardText = (string?)previous; }
        public string? Selection;          // что скопируется по Ctrl+C, если есть выделение
        public string AllText = "";        // что скопируется по Ctrl+C после Ctrl+A
        public string? Pasted;             // что реально вставилось по Ctrl+V
        bool _selectedAll;
        public string? ReadText() { MaybeBusy(); Log.Add("text"); return ClipboardText; }
        public Task SendCtrlAsync(int vk)
        {
            Log.Add("ctrl+" + char.ToLowerInvariant((char)vk));
            if (vk == 'A') _selectedAll = true;
            if (vk == 'C') { if (Selection is not null) ClipboardText = Selection; else if (_selectedAll) ClipboardText = AllText; }
            if (vk == 'V') Pasted = ClipboardText;
            return Task.CompletedTask;
        }
    }

    static PasteCoordinator Make(Fakes f) => new(f, f, f, _ => Task.CompletedTask);

    [Theory]
    [InlineData("Code")]
    [InlineData("code")]
    [InlineData("Code - Insiders")]
    public async Task Pastes_WhenVsCodeIsActive(string process)
    {
        var f = new Fakes { Process = process };
        var result = await Make(f).PasteAsync("текст");
        Assert.Equal(PasteResult.Pasted, result);
        Assert.Equal(new[] { "read", "write:текст", "ctrl+v", "restore:старое" }, f.Log);
        Assert.Equal("старое", f.ClipboardText);
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData(null)]
    public async Task DoesNothing_WhenOtherProcessActive(string? process)
    {
        var f = new Fakes { Process = process };
        Assert.Equal(PasteResult.NotVsCode, await Make(f).PasteAsync("текст"));
        Assert.Empty(f.Log);
        Assert.Equal("старое", f.ClipboardText);
    }

    [Fact]
    public async Task RetriesBusyClipboard_ThenSucceeds()
    {
        var f = new Fakes { BusyFailuresLeft = 2 };
        Assert.Equal(PasteResult.Pasted, await Make(f).PasteAsync("т"));
        Assert.Contains("ctrl+v", f.Log);
    }

    [Fact]
    public async Task GivesUp_WhenClipboardStaysBusy_AndSendsNoKeys()
    {
        var f = new Fakes { BusyFailuresLeft = 99 };
        Assert.Equal(PasteResult.ClipboardBusy, await Make(f).PasteAsync("т"));
        Assert.DoesNotContain("ctrl+v", f.Log);
    }

    [Fact]
    public async Task RestoresNull_WhenClipboardHadNoText()
    {
        var f = new Fakes { ClipboardText = null };
        await Make(f).PasteAsync("т");
        Assert.Equal("restore:<null>", f.Log[^1]);
    }

    [Fact]
    public async Task StillPasted_WhenRestoreFailsBusy()
    {
        var f = new Fakes();
        var delays = 0;
        var c = new PasteCoordinator(f, f, f, _ => { delays++; if (delays == 1) f.BusyFailuresLeft = 99; return Task.CompletedTask; });
        Assert.Equal(PasteResult.Pasted, await c.PasteAsync("т"));
    }

    [Fact]
    public async Task ConcurrentPastes_AreSerialized()
    {
        var f = new Fakes();
        var c = new PasteCoordinator(f, f, f, async _ => await Task.Yield());
        await Task.WhenAll(c.PasteAsync("a"), c.PasteAsync("b"));
        Assert.Equal(new[]
        {
            "read", "write:a", "ctrl+v", "restore:старое",
            "read", "write:b", "ctrl+v", "restore:старое",
        }, f.Log);
    }

    // ---- исправление раскладки ----

    [Fact]
    public async Task FixLayout_UsesSelection_WhenThereIsOne()
    {
        var f = new Fakes { Selection = "ghbdtn" };
        Assert.Equal(LayoutFixResult.Fixed, await Make(f).FixLayoutAsync());
        Assert.Equal("привет", f.Pasted);
        Assert.Equal("старое", f.ClipboardText);               // прежний буфер возвращён
        Assert.DoesNotContain("ctrl+a", f.Log);                // выделение было — всё поле не трогаем
    }

    [Fact]
    public async Task FixLayout_SelectsAll_WhenNothingSelected()
    {
        var f = new Fakes { AllText = "ghbdtn vbh" };
        Assert.Equal(LayoutFixResult.Fixed, await Make(f).FixLayoutAsync());
        Assert.Equal("привет мир", f.Pasted);
        Assert.Equal("старое", f.ClipboardText);
        Assert.True(f.Log.IndexOf("ctrl+a") < f.Log.LastIndexOf("ctrl+c"));
    }

    [Fact]
    public async Task FixLayout_EmptyField_NothingPasted_ClipboardRestored()
    {
        var f = new Fakes { AllText = "" };
        Assert.Equal(LayoutFixResult.NothingToFix, await Make(f).FixLayoutAsync());
        Assert.Null(f.Pasted);
        Assert.Equal("старое", f.ClipboardText);
    }

    [Fact]
    public async Task FixLayout_NothingChanges_NothingPasted()
    {
        var f = new Fakes { Selection = "123" };
        Assert.Equal(LayoutFixResult.NothingToFix, await Make(f).FixLayoutAsync());
        Assert.Null(f.Pasted);
    }

    [Fact]
    public async Task FixLayout_NotVsCode_DoesNothing()
    {
        var f = new Fakes { Process = "chrome", Selection = "ghbdtn" };
        Assert.Equal(LayoutFixResult.NotVsCode, await Make(f).FixLayoutAsync());
        Assert.Empty(f.Log);
    }

    [Fact]
    public async Task FixLayout_BusyClipboard_GivesUp()
    {
        var f = new Fakes { BusyFailuresLeft = 99, Selection = "ghbdtn" };
        Assert.Equal(LayoutFixResult.ClipboardBusy, await Make(f).FixLayoutAsync());
        Assert.Null(f.Pasted);
    }
}
