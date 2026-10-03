using System.Windows.Media;

namespace ChatSnippets.App.ViewModels;

public enum SnippetState { Normal, Success, Error }

/// <summary>Общая часть плитки на панели: картинка, подпись под ней, состояние (успех/ошибка), подсказка.</summary>
public abstract class TileViewModel : ObservableBase
{
    ImageSource? _icon;
    string _hotkeyDisplay = "";
    SnippetState _state;
    bool _editMode;

    public ImageSource? Icon { get => _icon; protected set => Set(ref _icon, value); }
    public string HotkeyDisplay { get => _hotkeyDisplay; protected set => Set(ref _hotkeyDisplay, value); }
    public SnippetState State { get => _state; private set => Set(ref _state, value); }
    /// <summary>Режим редактирования: на плитке виден карандаш, клик открывает правку.</summary>
    public bool EditMode { get => _editMode; set => Set(ref _editMode, value); }
    public abstract string AutomationName { get; }
    public abstract string TooltipText { get; }

    /// <summary>Язык сменился: обновить подсказку и подпись.</summary>
    public virtual void Relocalize()
    {
        Raise(nameof(AutomationName));
        Raise(nameof(TooltipText));
    }

    public async Task FlashAsync(SnippetState state)
    {
        State = state;
        await Task.Delay(600);
        State = SnippetState.Normal;
    }
}
