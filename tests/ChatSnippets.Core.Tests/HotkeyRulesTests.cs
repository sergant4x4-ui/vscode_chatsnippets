using ChatSnippets.Core;
using Xunit;

namespace ChatSnippets.Core.Tests;

public class HotkeyRulesTests
{
    static Snippet S(string id, string? hk) => new() { Id = id, Hotkey = hk };
    static Hotkey H(string text) { Hotkey.TryParse(text, out var h); return h; }

    [Fact]
    public void FindConflict_FindsOtherSnippetWithSameHotkey()
    {
        var all = new[] { S("a", "Ctrl+Alt+1"), S("b", "Ctrl+Alt+2") };
        Assert.Equal("b", HotkeyRules.FindConflict(all, "a", H("Ctrl+Alt+2"))?.Id);
    }

    [Fact]
    public void FindConflict_IgnoresSelf()
    {
        var all = new[] { S("a", "Ctrl+Alt+1") };
        Assert.Null(HotkeyRules.FindConflict(all, "a", H("Ctrl+Alt+1")));
    }

    [Fact]
    public void FindConflict_IgnoresBrokenHotkeyStrings()
    {
        var all = new[] { S("a", "мусор"), S("b", null) };
        Assert.Null(HotkeyRules.FindConflict(all, null, H("Ctrl+Alt+1")));
    }

    [Fact]
    public void NextFreeDefault_SkipsTaken()
    {
        var all = new[] { S("a", "Ctrl+Alt+1"), S("b", "Ctrl+Alt+2") };
        Assert.Equal("Ctrl+Alt+3", HotkeyRules.NextFreeDefault(all).ToString());
    }

    [Fact]
    public void NextFreeDefault_AfterDigitsGoesToLetters()
    {
        var all = "1234567890".Select(d => S("s" + d, $"Ctrl+Alt+{d}")).ToList();
        Assert.Equal("Ctrl+Alt+Q", HotkeyRules.NextFreeDefault(all).ToString());
    }
}
