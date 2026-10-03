using System.Windows;
using System.Windows.Input;
using ChatSnippets.Core;

namespace ChatSnippets.App.Views;

/// <summary>Выбор глобального хоткея для «исправить раскладку». validate возвращает текст ошибки или null.</summary>
public partial class HotkeyPromptWindow : Window
{
    readonly Func<Hotkey, string?> _validate;

    /// <summary>Выбранное сочетание после Save; null — хоткей снят.</summary>
    public string? Result { get; private set; }

    public HotkeyPromptWindow(string? current, Func<Hotkey, string?> validate)
    {
        InitializeComponent();
        _validate = validate;
        HotkeyField.Value = Hotkey.TryParse(current, out var h) ? h : null;
    }

    void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    void HotkeyField_ValueChanged(object? sender, EventArgs e) => Validate();

    bool Validate()
    {
        var error = HotkeyField.Value is { } h ? _validate(h) : null;
        ErrorText.Text = error ?? "";
        ErrorText.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        return error is null;
    }

    void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Validate()) return;
        Result = HotkeyField.Value?.ToString();
        DialogResult = true;
    }
}
