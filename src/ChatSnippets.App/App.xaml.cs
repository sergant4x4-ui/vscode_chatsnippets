using System.IO;
using System.Windows;
using ChatSnippets.App.Services;
using ChatSnippets.App.ViewModels;
using ChatSnippets.App.Views;
using ChatSnippets.Core;

namespace ChatSnippets.App;

public partial class App : Application
{
    const string ShowEventName = "ChatSnippets.Show";
    const string LayoutFixId = "@layoutfix";
    Mutex? _singleInstance;
    EventWaitHandle? _showEvent;
    ConfigStore _store = null!;
    AppConfig _config = null!;
    IconStore _icons = null!;
    PanelViewModel _vm = null!;
    HotkeyService _hotkeys = null!;
    PasteCoordinator _paste = null!;
    DockController _dock = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => AppLog.Write("UNHANDLED: " + args.ExceptionObject);
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Write("DISPATCHER: " + args.Exception);
            MessageBox.Show("Ошибка: " + args.Exception.Message, "Chat Snippets", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // Второй экземпляр тихо выходит: хоткеи всё равно заняты первым.
        _singleInstance = new Mutex(true, "ChatSnippets.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            // Программа уже работает: просим её показать панель (вдруг окна потерялись) и выходим.
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); }
            catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException) { }
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        new Thread(() => { while (_showEvent.WaitOne()) Dispatcher.BeginInvoke(() => _dock?.Reveal()); }) { IsBackground = true }.Start();
        AppLog.Write("Start");

        _store = new ConfigStore(ConfigStore.DefaultPath);
        _config = _store.Load();
        _icons = new IconStore(IconStore.DefaultDirectory);
        _vm = new PanelViewModel(_config, _icons);
        _hotkeys = new HotkeyService();
        _paste = new PasteCoordinator(new ForegroundProbe(), new WpfClipboard(), new KeySender(), Task.Delay);

        var panel = new PanelWindow(_vm);
        var flag = new FlagWindow();
        _dock = new DockController(panel, flag, _config.Window, () => _vm.Items.Count + 1, Save);   // +1: плитка раскладки

        _vm.ExecuteRequested += vm =>
        {
            if (_vm.EditMode) EditSnippet(vm.Model, isNew: false, vm);
            else _ = ExecuteAsync(vm, fromHotkey: false);
        };
        _dock.Collapsed += () => _vm.EditMode = false;
        _vm.EditRequested += vm => EditSnippet(vm.Model, isNew: false, vm);
        _vm.AddRequested += AddSnippet;
        _vm.DeleteRequested += DeleteSnippet;
        _vm.MoveRequested += MoveSnippet;
        _vm.LayoutFixRequested += () =>
        {
            if (_vm.EditMode) ChooseLayoutHotkey();       // в режиме редактирования плитка раскладки открывает выбор хоткея
            else _ = LayoutFixAsync();
        };
        _vm.LayoutHotkeyRequested += ChooseLayoutHotkey;
        _vm.LayoutFix.SetHotkey(_config.LayoutFixHotkey);
        _vm.PinnedChanged += pinned => { _config.Window.Pinned = pinned; Save(); };
        _hotkeys.Pressed += id =>
        {
            if (id == LayoutFixId) { _ = LayoutFixAsync(); return; }
            var vm = _vm.Items.FirstOrDefault(i => i.Model.Id == id);
            if (vm is not null) _ = ExecuteAsync(vm, fromHotkey: true);
        };
        panel.ExitRequested += Shutdown;

        _dock.Start();
        var failed = RegisterAllHotkeys();
        if (failed.Count > 0)
            MessageBox.Show("Не удалось занять сочетания (их использует другая программа): " + string.Join(", ", failed),
                "Chat Snippets", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    async Task ExecuteAsync(SnippetViewModel vm, bool fromHotkey)
    {
        if (string.IsNullOrEmpty(vm.Model.Text)) { await vm.FlashAsync(SnippetState.Error); return; }
        var result = await _paste.PasteAsync(vm.Model.Text);
        if (result == PasteResult.Pasted)
        {
            // Success — только после подтверждённой вставки. Панель НЕ сворачиваем.
            var flash = vm.FlashAsync(SnippetState.Success);
            if (fromHotkey) await Task.WhenAll(flash, _dock.FlashFlagAsync()); else await flash;
        }
        else
        {
            await vm.FlashAsync(SnippetState.Error);
        }
    }

    async Task LayoutFixAsync()
    {
        var result = await _paste.FixLayoutAsync();
        await _vm.LayoutFix.FlashAsync(result == LayoutFixResult.Fixed ? SnippetState.Success : SnippetState.Error);
    }

    void ChooseLayoutHotkey()
    {
        _dock.Suspended = true;
        _hotkeys.UnregisterAll();          // на время диалога снимаем всё, иначе система перехватит нажатие
        try
        {
            var dialog = new HotkeyPromptWindow(_config.LayoutFixHotkey, hotkey =>
                HotkeyRules.FindConflict(_config.Snippets, null, hotkey) is not null ? "This combination is already used by an icon."
                : !_hotkeys.IsFree(hotkey) ? "This combination is taken by another program."
                : null);
            if (dialog.ShowDialog() != true) return;
            _config.LayoutFixHotkey = dialog.Result;
            _vm.LayoutFix.SetHotkey(dialog.Result);
            Save();
        }
        finally
        {
            RegisterAllHotkeys();
            _dock.Suspended = false;
        }
    }

    void AddSnippet()
    {
        var snippet = new Snippet { Hotkey = HotkeyRules.NextFreeDefault(_config.Snippets)?.ToString() };
        EditSnippet(snippet, isNew: true, existing: null);
    }

    void EditSnippet(Snippet snippet, bool isNew, SnippetViewModel? existing)
    {
        // Редактируем копию: Cancel не должен менять оригинал.
        var working = new Snippet { Id = snippet.Id, IconFile = snippet.IconFile, Text = snippet.Text, Hotkey = snippet.Hotkey };
        _dock.Suspended = true;
        _hotkeys.UnregisterAll();
        try
        {
            var others = _config.Snippets.Where(s => s.Id != snippet.Id).ToList();
            if (_config.LayoutFixHotkey is not null)       // хоткей раскладки тоже занят для иконок
                others.Add(new Snippet { Id = LayoutFixId, Hotkey = _config.LayoutFixHotkey });
            var dialog = new EditSnippetWindow(working, isNew, _icons, others, _hotkeys.IsFree);
            if (dialog.ShowDialog() != true) return;

            if (dialog.Deleted) { if (existing is not null) DeleteSnippetCore(existing); return; }

            snippet.Text = working.Text; snippet.Hotkey = working.Hotkey; snippet.IconFile = working.IconFile;
            if (isNew)
            {
                _config.Snippets.Add(snippet);
                _vm.Items.Add(new SnippetViewModel(snippet, _icons));
            }
            else existing?.Refresh();
            SaveAndRebind();
        }
        finally
        {
            RegisterAllHotkeys();
            _dock.Suspended = false;
        }
    }

    void DeleteSnippet(SnippetViewModel vm)
    {
        _dock.Suspended = true;
        try
        {
            var answer = MessageBox.Show("Удалить эту иконку?", "Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer == MessageBoxResult.Yes) DeleteSnippetCore(vm);
        }
        finally { _dock.Suspended = false; }
    }

    void DeleteSnippetCore(SnippetViewModel vm)
    {
        _config.Snippets.Remove(vm.Model);
        _vm.Items.Remove(vm);
        SaveAndRebind();
    }

    void MoveSnippet(SnippetViewModel from, SnippetViewModel to)
    {
        var fromIndex = _vm.Items.IndexOf(from);
        var toIndex = _vm.Items.IndexOf(to);
        _vm.Items.Move(fromIndex, toIndex);
        _config.Snippets.RemoveAt(fromIndex);
        _config.Snippets.Insert(toIndex, from.Model);
        Save();
    }

    void SaveAndRebind()
    {
        Save();
        RegisterAllHotkeys();
        _dock.Relayout();           // число иконок изменилось → высота панели
    }

    List<string> RegisterAllHotkeys()
    {
        _hotkeys.UnregisterAll();
        var failed = new List<string>();
        foreach (var s in _config.Snippets)
            if (Hotkey.TryParse(s.Hotkey, out var h) && !_hotkeys.TryRegister(s.Id, h))
                failed.Add(h.ToString());
        if (Hotkey.TryParse(_config.LayoutFixHotkey, out var layoutKey) && !_hotkeys.TryRegister(LayoutFixId, layoutKey))
            failed.Add(layoutKey.ToString());
        return failed;
    }

    void Save()
    {
        try { _store.Save(_config); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* диск занят/недоступен — настройки не критичны, повторим при следующем изменении */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
