using System.Globalization;
using System.Windows;
using System.Windows.Media;
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

    public override string AutomationName =>
        HotkeyDisplay == "Layout" ? "Сменить раскладку введённого текста" : $"Сменить раскладку введённого текста, {HotkeyDisplay}";

    public override string TooltipText =>
        "Сменить раскладку введённого текста\nghbdtn ⇄ привет — выделенного, а если ничего не выделено, то всего текста в поле\nПравый клик — назначить горячую клавишу";

    public void SetHotkey(string? text)
    {
        HotkeyDisplay = Hotkey.TryParse(text, out var h) ? h.ToString() : "Layout";
        Raise(nameof(AutomationName));
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
