using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ChatSnippets.App.Localization;
using ChatSnippets.Core;

namespace ChatSnippets.App.ViewModels;

/// <summary>Постоянная плитка «исправить раскладку»: ghbdtn ⇄ привет.</summary>
public sealed class LayoutFixViewModel : TileViewModel
{
    public LayoutFixViewModel()
    {
        Icon = BuildIcon();
        SetHotkey(null);
    }

    Hotkey? _hotkey;

    public override string AutomationName =>
        _hotkey is { } h ? Loc.T("LayoutNameKey", h.ToString()) : Loc.T("LayoutName");

    public override string TooltipText => Loc.T("LayoutTip");

    public void SetHotkey(string? text)
    {
        _hotkey = Hotkey.TryParse(text, out var h) ? h : null;
        Relocalize();
    }

    public override void Relocalize()
    {
        HotkeyDisplay = _hotkey?.ToString() ?? Loc.T("LayoutLabel");
        base.Relocalize();
    }

    static ImageSource BuildIcon()
    {
        var text = new FormattedText("Aя", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI Semibold"), 24, Brushes.White, 1.0);
        var geometry = text.BuildGeometry(new Point(0, 0));
        var bounds = geometry.Bounds;
        geometry = Geometry.Combine(geometry, Geometry.Empty, GeometryCombineMode.Union,
            new TranslateTransform(-bounds.X, -bounds.Y));
        var drawing = new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0x4C, 0x8D, 0xFF)), null, geometry);
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}
