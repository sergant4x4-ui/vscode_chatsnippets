using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChatSnippets.Core;

namespace ChatSnippets.App.ViewModels;

public enum SnippetState { Normal, Success, Error }

public sealed class SnippetViewModel : ObservableBase
{
    static readonly ImageSource Placeholder = MakePlaceholder();
    readonly IconStore _icons;
    ImageSource _icon = Placeholder;
    string _hotkeyDisplay = "";
    SnippetState _state;

    public SnippetViewModel(Snippet model, IconStore icons)
    {
        Model = model;
        _icons = icons;
        Refresh();
    }

    public Snippet Model { get; }
    public ImageSource Icon { get => _icon; private set => Set(ref _icon, value); }
    public string HotkeyDisplay { get => _hotkeyDisplay; private set => Set(ref _hotkeyDisplay, value); }
    public SnippetState State { get => _state; private set => Set(ref _state, value); }
    public string AutomationName => string.IsNullOrEmpty(HotkeyDisplay) ? "Paste snippet" : $"Paste snippet, {HotkeyDisplay}";

    public void Refresh()
    {
        Icon = LoadIcon();
        HotkeyDisplay = Hotkey.TryParse(Model.Hotkey, out var h) ? h.ToString() : "";
        Raise(nameof(AutomationName));
    }

    public async Task FlashAsync(SnippetState state)
    {
        State = state;
        await Task.Delay(600);
        State = SnippetState.Normal;
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
        catch (Exception ex) when (ex is NotSupportedException or IOException or FileFormatException)
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
