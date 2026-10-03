using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ChatSnippets.Core;
using Microsoft.Win32;

namespace ChatSnippets.App.Views;

public partial class EditSnippetWindow : Window
{
    readonly Snippet _working;
    readonly IconStore _icons;
    readonly IEnumerable<Snippet> _others;
    readonly Func<Hotkey, bool> _isFreeInSystem;
    readonly Hotkey? _originalHotkey;
    string? _iconFile;

    public bool Deleted { get; private set; }

    public EditSnippetWindow(Snippet working, bool isNew, IconStore icons, IEnumerable<Snippet> others, Func<Hotkey, bool> isFreeInSystem)
    {
        InitializeComponent();
        _working = working; _icons = icons; _others = others; _isFreeInSystem = isFreeInSystem;
        _iconFile = working.IconFile;
        TextBox.Text = working.Text;
        _originalHotkey = Hotkey.TryParse(working.Hotkey, out var h) && !isNew ? h : null;
        HotkeyField.Value = Hotkey.TryParse(working.Hotkey, out var shown) ? shown : null;
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        ShowPreview();
    }

    void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    void ChooseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|All files|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try { _iconFile = _icons.Import(dialog.FileName); }
        catch (IOException ex) { ShowError("Не удалось скопировать картинку: " + ex.Message); return; }
        ShowPreview();
    }

    void ShowPreview()
    {
        if (string.IsNullOrEmpty(_iconFile) || !File.Exists(_icons.PathOf(_iconFile))) { Preview.Source = null; return; }
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(_icons.PathOf(_iconFile));
            bmp.EndInit();
            Preview.Source = bmp;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException) { Preview.Source = null; }
    }

    void HotkeyField_ValueChanged(object? sender, EventArgs e) => ValidateHotkey();

    /// <summary>Показывает ошибку рядом с полем ДО сохранения. true — сочетание допустимо (или пустое).</summary>
    bool ValidateHotkey()
    {
        if (HotkeyField.Value is not { } hotkey) { HideError(); return true; }
        var conflict = HotkeyRules.FindConflict(_others, _working.Id, hotkey);
        if (conflict is not null) { ShowError("Это сочетание уже назначено другой иконке."); return false; }
        if (hotkey != _originalHotkey && !_isFreeInSystem(hotkey)) { ShowError("Это сочетание занято другой программой."); return false; }
        HideError();
        return true;
    }

    void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateHotkey()) return;
        _working.Text = TextBox.Text;
        _working.Hotkey = HotkeyField.Value?.ToString();
        _working.IconFile = _iconFile;
        DialogResult = true;
    }

    void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this, "Удалить эту иконку?", "Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        Deleted = true;
        DialogResult = true;
    }

    void ShowError(string text) { ErrorText.Text = text; ErrorText.Visibility = Visibility.Visible; }
    void HideError() => ErrorText.Visibility = Visibility.Collapsed;
}
