using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChatSnippets.Core;

namespace ChatSnippets.App.Views;

/// <summary>Поле, которое ловит комбинацию клавиш вместо текста. Backspace/Delete очищает.</summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(Hotkey?), typeof(HotkeyBox),
        new PropertyMetadata(null, (d, _) => ((HotkeyBox)d).Text = ((HotkeyBox)d).Value?.ToString() ?? ""));

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsUndoEnabled = false;
        ContextMenu = null;
    }

    public Hotkey? Value { get => (Hotkey?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public event EventHandler? ValueChanged;

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab or Key.Escape or Key.Enter) return;      // навигация и кнопки диалога работают как обычно

        e.Handled = true;
        if (key is Key.Back or Key.Delete) { Set(null); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System or Key.ImeProcessed) return;

        var mods = HotkeyModifiers.None;
        var kb = Keyboard.Modifiers;
        if (kb.HasFlag(ModifierKeys.Control)) mods |= HotkeyModifiers.Ctrl;
        if (kb.HasFlag(ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (kb.HasFlag(ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (kb.HasFlag(ModifierKeys.Windows)) mods |= HotkeyModifiers.Win;

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (Hotkey.TryCreate(mods, vk, out var hotkey)) Set(hotkey);
    }

    void Set(Hotkey? value)
    {
        Value = value;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }
}
