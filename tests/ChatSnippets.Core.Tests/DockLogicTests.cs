using ChatSnippets.Core;
using Xunit;

namespace ChatSnippets.Core.Tests;

public class DockLogicTests
{
    static readonly PxRect Work = new(0, 0, 1920, 1040);

    [Theory]
    [InlineData(DockSide.Left, true, 0)]
    [InlineData(DockSide.Left, false, -80)]
    [InlineData(DockSide.Right, true, 1840)]
    [InlineData(DockSide.Right, false, 1920)]
    public void PanelLeft(DockSide side, bool expanded, int expected) =>
        Assert.Equal(expected, DockLogic.PanelLeft(Work, side, expanded, 80));

    [Theory]
    [InlineData(DockSide.Left, true, 80)]
    [InlineData(DockSide.Left, false, 0)]
    [InlineData(DockSide.Right, true, 1824)]
    [InlineData(DockSide.Right, false, 1904)]
    public void FlagLeft(DockSide side, bool expanded, int expected) =>
        Assert.Equal(expected, DockLogic.FlagLeft(Work, side, expanded, 80, 16));

    [Fact]
    public void PanelLeft_SecondMonitorOffset()
    {
        var second = new PxRect(1920, 0, 1280, 1024);
        Assert.Equal(1920, DockLogic.PanelLeft(second, DockSide.Left, true, 80));
        Assert.Equal(3200 - 80, DockLogic.PanelLeft(second, DockSide.Right, true, 80));
    }

    [Theory]
    [InlineData(10, DockSide.Left)]
    [InlineData(1700, DockSide.Right)]
    [InlineData(880, DockSide.Left)]
    [InlineData(1000, DockSide.Right)]
    public void NearestSide_OnPrimary(int panelLeft, DockSide expected) =>
        Assert.Equal(expected, DockLogic.NearestSide(Work, panelLeft, 80));

    [Fact]
    public void NearestSide_SecondMonitor()
    {
        var second = new PxRect(1920, 0, 1280, 1024);
        Assert.Equal(DockSide.Left, DockLogic.NearestSide(second, 1930, 80));
        Assert.Equal(DockSide.Right, DockLogic.NearestSide(second, 3000, 80));
    }

    [Theory]
    [InlineData(-50, 100, 0)]
    [InlineData(500, 100, 500)]
    [InlineData(5000, 928, 1040 - 928)]
    public void ClampTop(int top, int height, int expected) =>
        Assert.Equal(expected, DockLogic.ClampTop(Work, top, height));

    [Fact]
    public void ClampTop_PanelTallerThanWork_PinsToTop() =>
        Assert.Equal(0, DockLogic.ClampTop(Work, 300, 2000));

    [Fact]
    public void PanelHeight_ThirteenItems_Is928AtScale1() =>
        Assert.Equal(928, DockLogic.PanelHeightPx(new PxRect(0, 0, 1920, 2000), 13, 1.0));

    [Fact]
    public void PanelHeight_ScalesWithDpi() =>
        Assert.Equal(1392, DockLogic.PanelHeightPx(new PxRect(0, 0, 1920, 2000), 13, 1.5));

    [Fact]
    public void PanelHeight_CappedByWorkArea() =>
        Assert.Equal(800, DockLogic.PanelHeightPx(new PxRect(0, 0, 1920, 800), 13, 1.0));

    [Fact]
    public void PanelHeight_OnlyAddButton() =>
        Assert.Equal(32 + 8 + 64 + 8, DockLogic.PanelHeightPx(new PxRect(0, 0, 1920, 2000), 1, 1.0));
}
