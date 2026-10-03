using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChatSnippets.App.Localization;
using ChatSnippets.Core;

namespace ChatSnippets.App.ViewModels;

public sealed class SnippetViewModel : TileViewModel
{
    const int TooltipLimit = 600;
    static readonly ImageSource Placeholder = MakePlaceholder();
    readonly IconStore _icons;

    public SnippetViewModel(Snippet model, IconStore icons)
    {
        Model = model;
        _icons = icons;
        Refresh();
    }

    public Snippet Model { get; }

    public override string AutomationName =>
        string.IsNullOrEmpty(HotkeyDisplay) ? Loc.T("PasteSnippet") : Loc.T("PasteSnippetKey", HotkeyDisplay);

    /// <summary>То, что будет вставлено: показываем при наведении (длинный текст обрезаем).</summary>
    public override string TooltipText
    {
        get
        {
            var text = Model.Text;
            if (string.IsNullOrWhiteSpace(text)) return Loc.T("NoText");
            var shown = text.Length > TooltipLimit ? text[..TooltipLimit] + "…" : text;
            return Model.PressEnter ? shown + "\n" + Loc.T("EnterAuto") : shown;
        }
    }

    public void Refresh()
    {
        Icon = LoadIcon();
        HotkeyDisplay = Hotkey.TryParse(Model.Hotkey, out var h) ? h.ToString() : "";
        Raise(nameof(AutomationName));
        Raise(nameof(TooltipText));
    }

    ImageSource LoadIcon()
    {
        if (string.IsNullOrEmpty(Model.IconFile)) return Placeholder;
        var path = _icons.PathOf(Model.IconFile);
        if (!File.Exists(path)) return Placeholder;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;   // не держим файл открытым
            bmp.UriSource = new Uri(path);
            bmp.DecodePixelWidth = 96;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or FileFormatException or UnauthorizedAccessException)
        {
            return Placeholder;
        }
    }

    static ImageSource MakePlaceholder()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x46)), null,
            new RectangleGeometry(new System.Windows.Rect(0, 0, 32, 32), 6, 6)));
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
