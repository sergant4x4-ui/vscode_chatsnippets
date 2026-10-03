namespace ChatSnippets.Core;

public readonly record struct PxRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

/// <summary>Чистая геометрия панели у края монитора. Все числа — физические пиксели.</summary>
public static class DockLogic
{
    public static int PanelLeft(PxRect work, DockSide side, bool expanded, int panelWidth) => side switch
    {
        DockSide.Left => expanded ? work.Left : work.Left - panelWidth,
        _ => expanded ? work.Right - panelWidth : work.Right,
    };

    public static int FlagLeft(PxRect work, DockSide side, bool expanded, int panelWidth, int flagWidth) => side switch
    {
        DockSide.Left => expanded ? work.Left + panelWidth : work.Left,
        _ => expanded ? work.Right - panelWidth - flagWidth : work.Right - flagWidth,
    };

    public static DockSide NearestSide(PxRect work, int panelLeft, int panelWidth) =>
        panelLeft + panelWidth / 2 < work.Left + work.Width / 2 ? DockSide.Left : DockSide.Right;

    public static int ClampTop(PxRect work, int top, int height) =>
        Math.Clamp(top, work.Top, Math.Max(work.Top, work.Bottom - height));

    /// <summary>Шапка 32 + отступы 8+8 + itemCount*64 + (itemCount-1)*4 (в DIP), умножить на масштаб, не выше рабочей области.</summary>
    public static int PanelHeightPx(PxRect work, int itemCount, double scale)
    {
        var dips = 32 + 8 + 8 + itemCount * 64 + Math.Max(0, itemCount - 1) * 4;
        return Math.Min(work.Height, (int)Math.Ceiling(dips * scale));
    }
}
