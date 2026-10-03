using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;

namespace ChatSnippets.App.Localization;

/// <summary>Надписи интерфейса на двух языках. Язык переключается на лету: XAML привязан к индексатору.</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();
    public static string Language { get; private set; } = "ru";
    public static event Action? Changed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => T(key);

    public static void SetLanguage(string? language)
    {
        var value = language == "en" ? "en" : "ru";
        if (value == Language) return;
        Language = value;
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item[]"));
        Changed?.Invoke();
    }

    public static string T(string key) =>
        Strings.TryGetValue(key, out var pair) ? (Language == "en" ? pair.En : pair.Ru) : key;

    public static string T(string key, params object[] args) => string.Format(T(key), args);

    static readonly Dictionary<string, (string Ru, string En)> Strings = new()
    {
        ["Drag"] = ("Потяните здесь, чтобы переместить панель", "Drag here to move the panel"),
        ["Pin"] = ("Держать панель открытой", "Keep panel open"),
        ["Settings"] = ("Настройки", "Settings"),
        ["Hide"] = ("Свернуть панель", "Hide panel"),
        ["HotkeyMenu"] = ("Горячая клавиша…", "Hotkey..."),
        ["EditMenu"] = ("Изменить…", "Edit..."),
        ["Delete"] = ("Удалить", "Delete"),
        ["AddIcon"] = ("Добавить иконку", "Add icon"),
        ["EditModeTip"] = ("Режим правки: клик по иконке меняет картинку, текст или горячую клавишу", "Edit mode: click an icon to change its picture, text or hotkey"),
        ["EditModeName"] = ("Режим правки", "Edit mode"),
        ["EditBtn"] = ("Правка", "Edit"),
        ["DoneBtn"] = ("Готово", "Done"),
        ["LayoutHotkeyMenu"] = ("Хоткей смены раскладки…", "Layout fix hotkey..."),
        ["LeftSide"] = ("Панель слева", "Left side"),
        ["RightSide"] = ("Панель справа", "Right side"),
        ["Exit"] = ("Выход", "Exit"),
        ["LangRu"] = ("Русский", "Русский"),
        ["LangEn"] = ("English", "English"),
        ["EditIconTitle"] = ("Изменить иконку", "Edit icon"),
        ["ChooseImage"] = ("Выбрать картинку…", "Choose image..."),
        ["TextToPaste"] = ("Текст для вставки", "Text to paste"),
        ["PressEnterOpt"] = ("Нажать Enter после вставки (отправить сразу)", "Press Enter after pasting (send at once)"),
        ["Hotkey"] = ("Горячая клавиша", "Hotkey"),
        ["PressCombo"] = ("Нажмите сочетание клавиш", "Press the combination"),
        ["PressComboClear"] = ("Нажмите сочетание клавиш (Backspace — убрать)", "Press the combination (Backspace clears)"),
        ["Save"] = ("Сохранить", "Save"),
        ["Cancel"] = ("Отмена", "Cancel"),
        ["ImagesFilter"] = ("Картинки|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|Все файлы|*.*", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|All files|*.*"),
        ["CopyFail"] = ("Не удалось скопировать картинку: {0}", "Could not copy the image: {0}"),
        ["UsedByIcon"] = ("Это сочетание уже назначено другой иконке.", "This combination is already used by another icon."),
        ["UsedByProgram"] = ("Это сочетание занято другой программой.", "This combination is taken by another program."),
        ["DeleteConfirm"] = ("Удалить эту иконку?", "Delete this icon?"),
        ["LayoutHotkeyTitle"] = ("Хоткей смены раскладки", "Layout fix hotkey"),
        ["LayoutHotkeyHelp"] = ("Выделите текст в чате (или ничего не выделяйте) и нажмите это сочетание: ghbdtn ⇄ привет.", "Select text in the chat (or leave nothing selected) and press this combination to fix ghbdtn ⇄ привет."),
        ["PasteSnippet"] = ("Вставить текст", "Paste snippet"),
        ["PasteSnippetKey"] = ("Вставить текст, {0}", "Paste snippet, {0}"),
        ["NoText"] = ("(текст не задан)", "(no text)"),
        ["EnterAuto"] = ("⏎ Enter нажмётся сам", "⏎ Enter is pressed automatically"),
        ["LayoutLabel"] = ("Раскладка", "Layout"),
        ["LayoutName"] = ("Сменить раскладку введённого текста", "Fix layout of the typed text"),
        ["LayoutNameKey"] = ("Сменить раскладку введённого текста, {0}", "Fix layout of the typed text, {0}"),
        ["LayoutTip"] = ("Сменить раскладку введённого текста\nghbdtn ⇄ привет — выделенного, а если ничего не выделено, то всего текста в поле\nПравый клик — назначить горячую клавишу",
                         "Fix layout of the typed text\nghbdtn ⇄ привет — the selection, or the whole field if nothing is selected\nRight-click to set a hotkey"),
        ["Error"] = ("Ошибка: {0}", "Error: {0}"),
        ["HotkeysFailed"] = ("Не удалось занять сочетания (их использует другая программа): {0}", "Could not register these combinations (used by another program): {0}"),
    };
}

/// <summary>{l:Loc Ключ} — надпись, которая меняется вместе с языком.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
