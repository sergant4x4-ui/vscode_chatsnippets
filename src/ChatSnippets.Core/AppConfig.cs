namespace ChatSnippets.Core;

public enum DockSide { Left, Right }

public sealed class Snippet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? IconFile { get; set; }
    public string Text { get; set; } = "";
    public string? Hotkey { get; set; }
    /// <summary>После вставки нажать Enter (сразу отправить сообщение).</summary>
    public bool PressEnter { get; set; }
}

public sealed class WindowSettings
{
    public DockSide Side { get; set; } = DockSide.Right;
    /// <summary>Верх панели в физических пикселях от верха рабочей области монитора.</summary>
    public int Top { get; set; } = 200;
    public bool Pinned { get; set; }
}

public sealed class AppConfig
{
    public List<Snippet> Snippets { get; set; } = new();
    public WindowSettings Window { get; set; } = new();
    /// <summary>Глобальный хоткей «исправить раскладку»; null — не назначен.</summary>
    public string? LayoutFixHotkey { get; set; }
}
