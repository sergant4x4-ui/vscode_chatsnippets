using ChatSnippets.Core;
using Xunit;

namespace ChatSnippets.Core.Tests;

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Alt+1", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 0x31)]
    [InlineData("ctrl + alt + q", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 0x51)]
    [InlineData("Shift+F5", HotkeyModifiers.Shift, 0x74)]
    [InlineData("Win+Ctrl+0", HotkeyModifiers.Win | HotkeyModifiers.Ctrl, 0x30)]
    public void TryParse_ValidText(string text, HotkeyModifiers mods, int vk)
    {
        Assert.True(Hotkey.TryParse(text, out var h));
        Assert.Equal(mods, h.Modifiers);
        Assert.Equal(vk, h.VirtualKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+1+2")]
    [InlineData("Ctrl+Alt+1+")]
    [InlineData("Ctrl+Space")]
    [InlineData("Ctrl+F25")]
    public void TryParse_Invalid(string? text)
    {
        Assert.False(Hotkey.TryParse(text, out _));
    }

    [Fact]
    public void ToString_UsesCanonicalOrder()
    {
        var h = new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Alt | HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0x41);
        Assert.Equal("Ctrl+Alt+Shift+Win+A", h.ToString());
    }

    [Fact]
    public void RoundTrip_FunctionKey()
    {
        Assert.True(Hotkey.TryParse("Ctrl+F12", out var h));
        Assert.Equal("Ctrl+F12", h.ToString());
        Assert.Equal(0x7B, h.VirtualKey);
    }

    [Fact]
    public void TryCreate_RejectsNoModifier()
    {
        Assert.False(Hotkey.TryCreate(HotkeyModifiers.None, 0x31, out _));
    }

    [Fact]
    public void TryCreate_RejectsUnsupportedKey()
    {
        Assert.False(Hotkey.TryCreate(HotkeyModifiers.Ctrl, 0x20, out _));
    }
}
