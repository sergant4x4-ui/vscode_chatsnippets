# Chat Snippets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Windows-панель крупных иконок, которая выезжает из-за края монитора и вставляет назначенный текст (по клику или глобальной горячей клавише) в окно VS Code.

**Architecture:** `ChatSnippets.Core` (net8.0, без UI) держит всю проверяемую логику: хоткеи, конфиг, геометрию докинга, координатор вставки через интерфейсы. `ChatSnippets.App` (WPF, net8.0-windows) содержит окна, Win32-реализации интерфейсов и контроллер выезда. Позиционирование окон — в физических пикселях через `SetWindowPos`, чтобы не зависеть от смешанного DPI.

**Tech Stack:** C# 12, .NET 8, WPF, xUnit, System.Text.Json, P/Invoke (user32).

**Spec:** `docs/superpowers/specs/2026-10-03-chat-snippets-design.md`
Внешний вид: `F:\_AI Project\EvpatiyBoxApp\.concept\CHAT_SNIPPETS_LAYOUT_SPEC.md` и `...\chat-snippets-ui-mockup-vertical-80px.png` (числа оттуда приоритетны).

## Global Constraints

- Окно панели: ширина ровно 80 DIP, кнопка 64×64 DIP, боковые отступы 8 DIP, зазор 4 DIP, шапка 32 DIP, одна колонка.
- Флажок: 16 DIP × 64 DIP, цвет акцента `#4C8DFF`.
- Цвета: окно `#1E1F24`, кнопка `#2B2D34`, рамка `#3A3D46`, акцент `#4C8DFF`, текст `#E6E6E6`, вторичный `#B9BCC5`, успех `#35D978`, ошибка `#FF5B5B`. Шрифт Segoe UI. Без градиентов.
- Подписи диалога — точные английские: `Edit icon`, `Choose image...`, `Text to paste`, `Hotkey`, `Press the combination`, `Delete`, `Save`, `Cancel`.
- Окна панели и флажка не забирают фокус (`WS_EX_NOACTIVATE`) и всегда `Topmost`.
- Вставка только если активный процесс — `Code` или `Code - Insiders`. Enter не нажимается.
- Автосворачивание: курсор вне панели 1,5 с; повторный клик по флажку; кнопка minus. После вставки НЕ сворачивать.
- Хоткей обязан содержать модификатор. Конфиг: `%AppData%\ChatSnippets\config.json`, иконки: `%AppData%\ChatSnippets\icons`.
- Коммиты — на русском, коротко; в конце каждого коммита строка `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Проект: `F:\_AI Project\ChatSnippets`. Все команды выполнять оттуда.

## Review Focus

- Хоткей `Ctrl+Alt+1` ещё зажат в момент вставки → отправленный Ctrl+V превратится в Ctrl+Alt+V. Ожидание: перед `SendInput` ждём отпускания модификаторов (до 1 с). Тест — Task 5 (`SendCtrlVAsync` вызывается, координатор не торопит), реализация — Task 6.
- Буфер обмена занят другой программой → 3 попытки, потом `ClipboardBusy`, без вставки. Тест — Task 5.
- Правая панель на левом мониторе при соседнем мониторе справа: «спрятанная за краем» панель была бы видна на соседнем. Ожидание: в свёрнутом виде окно панели скрыто (`SW_HIDE`), а не просто сдвинуто. Реализация — Task 8.
- Битый или пустой `config.json` → программа стартует с пустым списком, исходник уходит в `.bad`. Тест — Task 3.
- Два экземпляра программы → второй тихо завершается (иначе хоткеи будут заняты). Реализация — Task 10.
- Файл картинки удалён с диска → заглушка вместо падения. Реализация — Task 7.
- Хоткей занят другой программой → понятное сообщение при сохранении, не исключение. Реализация — Task 9.

---

### Task 1: Каркас решения

**Files:**
- Create: `ChatSnippets.sln`, `.gitignore`, `src/ChatSnippets.Core/ChatSnippets.Core.csproj`, `src/ChatSnippets.App/ChatSnippets.App.csproj`, `tests/ChatSnippets.Core.Tests/ChatSnippets.Core.Tests.csproj`
- Modify: `docs/superpowers/specs/2026-10-03-chat-snippets-design.md` (раздел уточнений)

**Interfaces:**
- Produces: решение, собираемое `dotnet build`, и проходящий пустой `dotnet test`.

- [ ] **Step 1: Создать проекты**

```bash
cd "F:/_AI Project/ChatSnippets"
dotnet new sln -n ChatSnippets
dotnet new classlib -n ChatSnippets.Core -o src/ChatSnippets.Core -f net8.0
dotnet new wpf -n ChatSnippets.App -o src/ChatSnippets.App -f net8.0
dotnet new xunit -n ChatSnippets.Core.Tests -o tests/ChatSnippets.Core.Tests -f net8.0
rm src/ChatSnippets.Core/Class1.cs tests/ChatSnippets.Core.Tests/UnitTest1.cs
dotnet sln add src/ChatSnippets.Core src/ChatSnippets.App tests/ChatSnippets.Core.Tests
dotnet add src/ChatSnippets.App reference src/ChatSnippets.Core
dotnet add tests/ChatSnippets.Core.Tests reference src/ChatSnippets.Core
```

- [ ] **Step 2: `.gitignore`**

```
bin/
obj/
.vs/
*.user
```

- [ ] **Step 3: Проверить сборку и тесты**

Run: `dotnet build && dotnet test`
Expected: `Build succeeded`, тестов 0 (допустимо «No test is available» — тогда это норма до Task 2).

- [ ] **Step 4: Дописать в конец спецификации раздел**

```markdown

## Уточнения при реализации (2026-10-03)
- Док: по окончании перетаскивания панель прилипает к ближайшему краю рабочей области монитора (а не только в пределах 16 DIP), т.к. без дока нет флажка.
- Окно с `AllowsTransparency=True` и скруглённой рамкой (на Windows 10 `WindowChrome` не скругляет углы).
- Шестерёнка открывает меню: «Left side» / «Right side» и «Exit». Других настроек нет (в панели нет значка в трее, выйти можно только так).
- Позиционирование — в физических пикселях (`SetWindowPos`), масштаб берётся из DPI окна.
- В свёрнутом виде окно панели скрыто (`SW_HIDE`), виден только окно-флажок.
- Буфер обмена восстанавливается только если там был текст; картинка/файлы в буфере при вставке теряются.
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Каркас решения: Core, App, Tests" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Hotkey (разбор, форматирование, проверка)

**Files:**
- Create: `src/ChatSnippets.Core/Hotkey.cs`
- Test: `tests/ChatSnippets.Core.Tests/HotkeyTests.cs`

**Interfaces:**
- Produces:
  - `[Flags] enum HotkeyModifiers { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }` (значения = Win32 `MOD_*`)
  - `readonly record struct Hotkey(HotkeyModifiers Modifiers, int VirtualKey)`
  - `static bool Hotkey.TryParse(string? text, out Hotkey hotkey)`
  - `static bool Hotkey.TryCreate(HotkeyModifiers modifiers, int virtualKey, out Hotkey hotkey)` (false, если нет модификатора или клавиша не из A–Z, 0–9, F1–F24)
  - `override string ToString()` → `"Ctrl+Alt+1"` (порядок Ctrl, Alt, Shift, Win, клавиша)

- [ ] **Step 1: Failing-тесты**

```csharp
using ChatSnippets.Core;
using Xunit;

namespace ChatSnippets.Core.Tests;

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Alt+1", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 0x31)]
    [InlineData("ctrl + alt + q", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, 0x51)]
    [InlineData("Shift+F5", HotkeyModifiers.Shift, 0x74)]
    [InlineData("Win+Ctrl+0", HotkeyModifiers.Win | HotkeyModifiers.Ctrl, 0x30)]
    public void TryParse_ValidText(string text, HotkeyModifiers mods, int vk)
    {
        Assert.True(Hotkey.TryParse(text, out var h));
        Assert.Equal(mods, h.Modifiers);
        Assert.Equal(vk, h.VirtualKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]            // нет модификатора
    [InlineData("Ctrl+Alt")]     // нет клавиши
    [InlineData("Ctrl+1+2")]     // две клавиши
    [InlineData("Ctrl+Alt+1+")]  // пустой токен
    [InlineData("Ctrl+Space")]   // неподдерживаемая клавиша
    [InlineData("Ctrl+F25")]
    public void TryParse_Invalid(string? text)
    {
        Assert.False(Hotkey.TryParse(text, out _));
    }

    [Fact]
    public void ToString_UsesCanonicalOrder()
    {
        var h = new Hotkey(HotkeyModifiers.Win | HotkeyModifiers.Alt | HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0x41);
        Assert.Equal("Ctrl+Alt+Shift+Win+A", h.ToString());
    }

    [Fact]
    public void RoundTrip_FunctionKey()
    {
        Assert.True(Hotkey.TryParse("Ctrl+F12", out var h));
        Assert.Equal("Ctrl+F12", h.ToString());
        Assert.Equal(0x7B, h.VirtualKey);
    }

    [Fact]
    public void TryCreate_RejectsNoModifier()
    {
        Assert.False(Hotkey.TryCreate(HotkeyModifiers.None, 0x31, out _));
    }

    [Fact]
    public void TryCreate_RejectsUnsupportedKey()
    {
        Assert.False(Hotkey.TryCreate(HotkeyModifiers.Ctrl, 0x20 /* Space */, out _));
    }
}
```

- [ ] **Step 2: Run — FAIL**

Run: `dotnet test --filter HotkeyTests`
Expected: ошибка компиляции «Hotkey does not exist».

- [ ] **Step 3: Реализация**

```csharp
namespace ChatSnippets.Core;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8,
}

public readonly record struct Hotkey(HotkeyModifiers Modifiers, int VirtualKey)
{
    public static bool TryCreate(HotkeyModifiers modifiers, int virtualKey, out Hotkey hotkey)
    {
        hotkey = default;
        if (modifiers == HotkeyModifiers.None || !IsSupportedKey(virtualKey)) return false;
        hotkey = new Hotkey(modifiers, virtualKey);
        return true;
    }

    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var mods = HotkeyModifiers.None;
        var vk = 0;
        foreach (var raw in text.Split('+'))
        {
            var token = raw.Trim();
            switch (token.ToLowerInvariant())
            {
                case "ctrl": mods |= HotkeyModifiers.Ctrl; break;
                case "alt": mods |= HotkeyModifiers.Alt; break;
                case "shift": mods |= HotkeyModifiers.Shift; break;
                case "win": mods |= HotkeyModifiers.Win; break;
                default:
                    if (vk != 0 || !TryKeyFromName(token, out vk)) return false;
                    break;
            }
        }
        return TryCreate(mods, vk, out hotkey);
    }

    public static bool IsSupportedKey(int vk) =>
        (vk >= 0x30 && vk <= 0x39) || (vk >= 0x41 && vk <= 0x5A) || (vk >= 0x70 && vk <= 0x87);

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyName(VirtualKey));
        return string.Join('+', parts);
    }

    static string KeyName(int vk) =>
        vk >= 0x70 && vk <= 0x87 ? $"F{vk - 0x6F}" : ((char)vk).ToString();

    static bool TryKeyFromName(string name, out int vk)
    {
        vk = 0;
        if (name.Length == 1)
        {
            var c = char.ToUpperInvariant(name[0]);
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) { vk = c; return true; }
            return false;
        }
        if ((name[0] == 'F' || name[0] == 'f') && int.TryParse(name.AsSpan(1), out var n) && n >= 1 && n <= 24)
        {
            vk = 0x6F + n;
            return true;
        }
        return false;
    }
}
```

- [ ] **Step 4: Run — PASS**

Run: `dotnet test --filter HotkeyTests`
Expected: все зелёные.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Hotkey: разбор, формат, проверка модификатора" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Конфиг, правила хоткеев, хранилище иконок

**Files:**
- Create: `src/ChatSnippets.Core/AppConfig.cs`, `src/ChatSnippets.Core/ConfigStore.cs`, `src/ChatSnippets.Core/HotkeyRules.cs`, `src/ChatSnippets.Core/IconStore.cs`
- Test: `tests/ChatSnippets.Core.Tests/ConfigStoreTests.cs`, `HotkeyRulesTests.cs`, `IconStoreTests.cs`

**Interfaces:**
- Consumes: `Hotkey.TryParse`, `Hotkey.TryCreate` (Task 2).
- Produces:
  - `enum DockSide { Left, Right }`
  - `class Snippet { string Id; string? IconFile; string Text; string? Hotkey; }` (Id по умолчанию `Guid.NewGuid().ToString("N")`)
  - `class WindowSettings { DockSide Side = Right; int Top = 200; bool Pinned; }` (Top — физические пиксели от верха рабочей области)
  - `class AppConfig { List<Snippet> Snippets; WindowSettings Window; }`
  - `class ConfigStore(string path)`: `AppConfig Load()`, `void Save(AppConfig)`; `static string DefaultPath` = `%AppData%\ChatSnippets\config.json`
  - `static class HotkeyRules`: `Snippet? FindConflict(IEnumerable<Snippet> all, string? excludeId, Hotkey candidate)`, `Hotkey NextFreeDefault(IEnumerable<Snippet> all)`
  - `class IconStore(string directory)`: `string Import(string sourcePath)` (возвращает имя файла), `string PathOf(string fileName)`; `static string DefaultDirectory`

- [ ] **Step 1: Failing-тесты**

`ConfigStoreTests.cs`:
```csharp
using ChatSnippets.Core;
using Xunit;

namespace ChatSnippets.Core.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "cs-tests-" + Guid.NewGuid().ToString("N"));
    string ConfigPath => Path.Combine(_dir, "config.json");
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var cfg = new ConfigStore(ConfigPath).Load();
        Assert.Empty(cfg.Snippets);
        Assert.Equal(DockSide.Right, cfg.Window.Side);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new ConfigStore(ConfigPath);
        var cfg = new AppConfig();
        cfg.Snippets.Add(new Snippet { Id = "a", IconFile = "x.png", Text = "привет\nмир", Hotkey = "Ctrl+Alt+1" });
        cfg.Window.Side = DockSide.Left;
        cfg.Window.Top = 321;
        cfg.Window.Pinned = true;
        store.Save(cfg);

        var back = store.Load();
        var s = Assert.Single(back.Snippets);
        Assert.Equal("a", s.Id);
        Assert.Equal("x.png", s.IconFile);
        Assert.Equal("привет\nмир", s.Text);
        Assert.Equal("Ctrl+Alt+1", s.Hotkey);
        Assert.Equal(DockSide.Left, back.Window.Side);
        Assert.Equal(321, back.Window.Top);
        Assert.True(back.Window.Pinned);
    }

    [Fact]
    public void Save_LeavesNoTempFile()
    {
        new ConfigStore(ConfigPath).Save(new AppConfig());
        Assert.False(File.Exists(ConfigPath + ".tmp"));
        Assert.True(File.Exists(ConfigPath));
    }

    [Fact]
    public void Load_CorruptFile_MovesToBadAndReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, "{ это не json");
        var cfg = new ConfigStore(ConfigPath).Load();
        Assert.Empty(cfg.Snippets);
        Assert.False(File.Exists(ConfigPath));
        Assert.Equal("{ это не json", File.ReadAllText(ConfigPath + ".bad"));
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, "");
        Assert.Empty(new ConfigStore(ConfigPath).Load().Snippets);
    }
}
```

`HotkeyRulesTests.cs`:
```csharp
using ChatSnippets.Core;
using Xunit;

namespace ChatSnippets.Core.Tests;

public class HotkeyRulesTests
{
    static Snippet S(string id, string? hk) => new() { Id = id, Hotkey = hk };
    static Hotkey H(string text) { Hotkey.TryParse(text, out var h); return h; }

    [Fact]
    public void FindConflict_FindsOtherSnippetWithSameHotkey()
    {
        var all = new[] { S("a", "Ctrl+Alt+1"), S("b", "Ctrl+Alt+2") };
        Assert.Equal("b", HotkeyRules.FindConflict(all, "a", H("Ctrl+Alt+2"))?.Id);
    }

    [Fact]
    public void FindConflict_IgnoresSelf()
    {
        var all = new[] { S("a", "Ctrl+Alt+1") };
        Assert.Null(HotkeyRules.FindConflict(all, "a", H("Ctrl+Alt+1")));
    }

    [Fact]
    public void FindConflict_IgnoresBrokenHotkeyStrings()
    {
        var all = new[] { S("a", "мусор"), S("b", null) };
        Assert.Null(HotkeyRules.FindConflict(all, null, H("Ctrl+Alt+1")));
    }

    [Fact]
    public void NextFreeDefault_SkipsTaken()
    {
        var all = new[] { S("a", "Ctrl+Alt+1"), S("b", "Ctrl+Alt+2") };
        Assert.Equal("Ctrl+Alt+3", HotkeyRules.NextFreeDefault(all).ToString());
    }

    [Fact]
    public void NextFreeDefault_AfterDigitsGoesToLetters()
    {
        var all = "1234567890".Select(d => S("s" + d, $"Ctrl+Alt+{d}")).ToList();
        Assert.Equal("Ctrl+Alt+Q", HotkeyRules.NextFreeDefault(all).ToString());
    }
}
```

`IconStoreTests.cs`:
```csharp
using ChatSnippets.Core;
using Xunit;

namespace ChatSnippets.Core.Tests;

public sealed class IconStoreTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "cs-icons-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public void Import_CopiesFileKeepingExtension()
    {
        Directory.CreateDirectory(_root);
        var src = Path.Combine(_root, "Моя Картинка.PNG");
        File.WriteAllBytes(src, new byte[] { 1, 2, 3 });
        var store = new IconStore(Path.Combine(_root, "icons"));

        var name = store.Import(src);

        Assert.EndsWith(".png", name);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(store.PathOf(name)));
    }

    [Fact]
    public void Import_MissingSource_Throws()
    {
        var store = new IconStore(Path.Combine(_root, "icons"));
        Assert.Throws<FileNotFoundException>(() => store.Import(Path.Combine(_root, "нет.png")));
    }
}
```

- [ ] **Step 2: Run — FAIL**

Run: `dotnet test`
Expected: ошибки компиляции (типы не существуют).

- [ ] **Step 3: Реализация**

`AppConfig.cs`:
```csharp
namespace ChatSnippets.Core;

public enum DockSide { Left, Right }

public sealed class Snippet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? IconFile { get; set; }
    public string Text { get; set; } = "";
    public string? Hotkey { get; set; }
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
}
```

`ConfigStore.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatSnippets.Core;

public sealed class ConfigStore(string path)
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChatSnippets", "config.json");

    public AppConfig Load()
    {
        if (!File.Exists(path)) return new AppConfig();
        try
        {
            var text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return new AppConfig();
            return JsonSerializer.Deserialize<AppConfig>(text, Options) ?? new AppConfig();
        }
        catch (JsonException)
        {
            File.Move(path, path + ".bad", overwrite: true);
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
```

`HotkeyRules.cs`:
```csharp
namespace ChatSnippets.Core;

public static class HotkeyRules
{
    public static Snippet? FindConflict(IEnumerable<Snippet> all, string? excludeId, Hotkey candidate) =>
        all.FirstOrDefault(s => s.Id != excludeId && Hotkey.TryParse(s.Hotkey, out var h) && h == candidate);

    /// <summary>Первое свободное Ctrl+Alt+1..9,0, затем Q,W,E,R,T,Y,U,I,O,P.</summary>
    public static Hotkey NextFreeDefault(IEnumerable<Snippet> all)
    {
        var list = all.ToList();
        foreach (var key in "1234567890QWERTYUIOP")
        {
            Hotkey.TryParse($"Ctrl+Alt+{key}", out var h);
            if (FindConflict(list, null, h) is null) return h;
        }
        throw new InvalidOperationException("Свободных сочетаний по умолчанию не осталось");
    }
}
```

`IconStore.cs`:
```csharp
namespace ChatSnippets.Core;

public sealed class IconStore(string directory)
{
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChatSnippets", "icons");

    public string Import(string sourcePath)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Картинка не найдена", sourcePath);
        Directory.CreateDirectory(directory);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath).ToLowerInvariant();
        File.Copy(sourcePath, PathOf(name));
        return name;
    }

    public string PathOf(string fileName) => Path.Combine(directory, fileName);
}
```

- [ ] **Step 4: Run — PASS**

Run: `dotnet test`
Expected: все зелёные.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Конфиг, правила хоткеев, хранилище иконок" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: DockLogic (геометрия в пикселях)

**Files:**
- Create: `src/ChatSnippets.Core/DockLogic.cs`
- Test: `tests/ChatSnippets.Core.Tests/DockLogicTests.cs`

**Interfaces:**
- Consumes: `DockSide` (Task 3).
- Produces:
  - `readonly record struct PxRect(int Left, int Top, int Width, int Height)` со свойствами `Right`, `Bottom`, методом `bool Contains(int x, int y)`
  - `static class DockLogic`:
    - `int PanelLeft(PxRect work, DockSide side, bool expanded, int panelWidth)`
    - `int FlagLeft(PxRect work, DockSide side, bool expanded, int panelWidth, int flagWidth)`
    - `DockSide NearestSide(PxRect work, int panelLeft, int panelWidth)`
    - `int ClampTop(PxRect work, int top, int height)`
    - `int PanelHeightPx(PxRect work, int itemCount, double scale)` (itemCount включает кнопку «+»)

- [ ] **Step 1: Failing-тесты**

```csharp
using ChatSnippets.Core;
using Xunit;

namespace ChatSnippets.Core.Tests;

public class DockLogicTests
{
    static readonly PxRect Work = new(0, 0, 1920, 1040);

    [Theory]
    [InlineData(DockSide.Left, true, 0)]
    [InlineData(DockSide.Left, false, -80)]
    [InlineData(DockSide.Right, true, 1840)]
    [InlineData(DockSide.Right, false, 1920)]
    public void PanelLeft(DockSide side, bool expanded, int expected) =>
        Assert.Equal(expected, DockLogic.PanelLeft(Work, side, expanded, 80));

    [Theory]
    [InlineData(DockSide.Left, true, 80)]
    [InlineData(DockSide.Left, false, 0)]
    [InlineData(DockSide.Right, true, 1824)]
    [InlineData(DockSide.Right, false, 1904)]
    public void FlagLeft(DockSide side, bool expanded, int expected) =>
        Assert.Equal(expected, DockLogic.FlagLeft(Work, side, expanded, 80, 16));

    [Fact]
    public void PanelLeft_SecondMonitorOffset()
    {
        var second = new PxRect(1920, 0, 1280, 1024);
        Assert.Equal(1920, DockLogic.PanelLeft(second, DockSide.Left, true, 80));
        Assert.Equal(3200 - 80, DockLogic.PanelLeft(second, DockSide.Right, true, 80));
    }

    [Theory]
    [InlineData(10, DockSide.Left)]
    [InlineData(1700, DockSide.Right)]
    [InlineData(880, DockSide.Left)]   // центр 920 < 960
    [InlineData(1000, DockSide.Right)] // центр 1040 >= 960
    public void NearestSide_OnPrimary(int panelLeft, DockSide expected) =>
        Assert.Equal(expected, DockLogic.NearestSide(Work, panelLeft, 80));

    [Fact]
    public void NearestSide_SecondMonitor()
    {
        var second = new PxRect(1920, 0, 1280, 1024);
        Assert.Equal(DockSide.Left, DockLogic.NearestSide(second, 1930, 80));
        Assert.Equal(DockSide.Right, DockLogic.NearestSide(second, 3000, 80));
    }

    [Theory]
    [InlineData(-50, 100, 0)]
    [InlineData(500, 100, 500)]
    [InlineData(5000, 928, 1040 - 928)]
    public void ClampTop(int top, int height, int expected) =>
        Assert.Equal(expected, DockLogic.ClampTop(Work, top, height));

    [Fact]
    public void ClampTop_PanelTallerThanWork_PinsToTop() =>
        Assert.Equal(0, DockLogic.ClampTop(Work, 300, 2000));

    [Fact]
    public void PanelHeight_ThirteenItems_Is928AtScale1() =>
        Assert.Equal(928, DockLogic.PanelHeightPx(new PxRect(0, 0, 1920, 2000), 13, 1.0));

    [Fact]
    public void PanelHeight_ScalesWithDpi() =>
        Assert.Equal(1392, DockLogic.PanelHeightPx(new PxRect(0, 0, 1920, 2000), 13, 1.5));

    [Fact]
    public void PanelHeight_CappedByWorkArea() =>
        Assert.Equal(800, DockLogic.PanelHeightPx(new PxRect(0, 0, 1920, 800), 13, 1.0));

    [Fact]
    public void PanelHeight_OnlyAddButton() =>
        Assert.Equal(32 + 8 + 64 + 8, DockLogic.PanelHeightPx(new PxRect(0, 0, 1920, 2000), 1, 1.0));
}
```

- [ ] **Step 2: Run — FAIL**

Run: `dotnet test --filter DockLogicTests`
Expected: ошибка компиляции.

- [ ] **Step 3: Реализация**

```csharp
namespace ChatSnippets.Core;

public readonly record struct PxRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

/// <summary>Чистая геометрия панели у края монитора. Все числа — физические пиксели.</summary>
public static class DockLogic
{
    public static int PanelLeft(PxRect work, DockSide side, bool expanded, int panelWidth) => side switch
    {
        DockSide.Left => expanded ? work.Left : work.Left - panelWidth,
        _ => expanded ? work.Right - panelWidth : work.Right,
    };

    public static int FlagLeft(PxRect work, DockSide side, bool expanded, int panelWidth, int flagWidth) => side switch
    {
        DockSide.Left => expanded ? work.Left + panelWidth : work.Left,
        _ => expanded ? work.Right - panelWidth - flagWidth : work.Right - flagWidth,
    };

    public static DockSide NearestSide(PxRect work, int panelLeft, int panelWidth) =>
        panelLeft + panelWidth / 2 < work.Left + work.Width / 2 ? DockSide.Left : DockSide.Right;

    public static int ClampTop(PxRect work, int top, int height) =>
        Math.Clamp(top, work.Top, Math.Max(work.Top, work.Bottom - height));

    /// <summary>Шапка 32 + отступы 8+8 + itemCount*64 + (itemCount-1)*4 (в DIP), умножить на масштаб, не выше рабочей области.</summary>
    public static int PanelHeightPx(PxRect work, int itemCount, double scale)
    {
        var dips = 32 + 8 + 8 + itemCount * 64 + Math.Max(0, itemCount - 1) * 4;
        return Math.Min(work.Height, (int)Math.Ceiling(dips * scale));
    }
}
```

- [ ] **Step 4: Run — PASS**

Run: `dotnet test`
Expected: все зелёные.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "DockLogic: позиции панели и флажка, ближайший край, высота" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: PasteCoordinator (логика вставки через интерфейсы)

**Files:**
- Create: `src/ChatSnippets.Core/PasteCoordinator.cs`
- Test: `tests/ChatSnippets.Core.Tests/PasteCoordinatorTests.cs`

**Interfaces:**
- Produces:
  - `interface IForegroundWindow { string? GetProcessName(); }`
  - `interface IClipboardAccess { string? ReadText(); void WriteText(string text); void Restore(string? previous); }` (методы бросают `ClipboardBusyException`, если буфер занят; `Restore(null)` очищает буфер)
  - `interface IKeySender { Task SendCtrlVAsync(); }`
  - `class ClipboardBusyException : Exception`
  - `enum PasteResult { Pasted, NotVsCode, ClipboardBusy }`
  - `class PasteCoordinator(IForegroundWindow, IClipboardAccess, IKeySender, Func<TimeSpan, Task> delay)` с `Task<PasteResult> PasteAsync(string text)`

- [ ] **Step 1: Failing-тесты**

```csharp
using ChatSnippets.Core;
using Xunit;

namespace ChatSnippets.Core.Tests;

public class PasteCoordinatorTests
{
    sealed class Fakes : IForegroundWindow, IClipboardAccess, IKeySender
    {
        public string? Process = "Code";
        public string? ClipboardText = "старое";
        public int BusyFailuresLeft;
        public List<string> Log = new();

        public string? GetProcessName() => Process;

        void MaybeBusy() { if (BusyFailuresLeft > 0) { BusyFailuresLeft--; throw new ClipboardBusyException(); } }
        public string? ReadText() { MaybeBusy(); Log.Add("read"); return ClipboardText; }
        public void WriteText(string text) { MaybeBusy(); Log.Add("write:" + text); ClipboardText = text; }
        public void Restore(string? previous) { MaybeBusy(); Log.Add("restore:" + (previous ?? "<null>")); ClipboardText = previous; }
        public Task SendCtrlVAsync() { Log.Add("ctrl+v"); return Task.CompletedTask; }
    }

    static PasteCoordinator Make(Fakes f) => new(f, f, f, _ => Task.CompletedTask);

    [Theory]
    [InlineData("Code")]
    [InlineData("code")]
    [InlineData("Code - Insiders")]
    public async Task Pastes_WhenVsCodeIsActive(string process)
    {
        var f = new Fakes { Process = process };
        var result = await Make(f).PasteAsync("текст");
        Assert.Equal(PasteResult.Pasted, result);
        Assert.Equal(new[] { "read", "write:текст", "ctrl+v", "restore:старое" }, f.Log);
        Assert.Equal("старое", f.ClipboardText);
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData(null)]
    public async Task DoesNothing_WhenOtherProcessActive(string? process)
    {
        var f = new Fakes { Process = process };
        Assert.Equal(PasteResult.NotVsCode, await Make(f).PasteAsync("текст"));
        Assert.Empty(f.Log);
        Assert.Equal("старое", f.ClipboardText);
    }

    [Fact]
    public async Task RetriesBusyClipboard_ThenSucceeds()
    {
        var f = new Fakes { BusyFailuresLeft = 2 };
        Assert.Equal(PasteResult.Pasted, await Make(f).PasteAsync("т"));
        Assert.Contains("ctrl+v", f.Log);
    }

    [Fact]
    public async Task GivesUp_WhenClipboardStaysBusy_AndSendsNoKeys()
    {
        var f = new Fakes { BusyFailuresLeft = 99 };
        Assert.Equal(PasteResult.ClipboardBusy, await Make(f).PasteAsync("т"));
        Assert.DoesNotContain("ctrl+v", f.Log);
    }

    [Fact]
    public async Task RestoresNull_WhenClipboardHadNoText()
    {
        var f = new Fakes { ClipboardText = null };
        await Make(f).PasteAsync("т");
        Assert.Equal("restore:<null>", f.Log[^1]);
    }

    [Fact]
    public async Task StillPasted_WhenRestoreFailsBusy()
    {
        var f = new Fakes();
        var delays = 0;
        // Занятость наступает только на restore: включаем её после ctrl+v через delay.
        var c = new PasteCoordinator(f, f, f, _ => { delays++; if (delays == 1) f.BusyFailuresLeft = 99; return Task.CompletedTask; });
        Assert.Equal(PasteResult.Pasted, await c.PasteAsync("т"));
    }
}
```

- [ ] **Step 2: Run — FAIL**

Run: `dotnet test --filter PasteCoordinatorTests`
Expected: ошибка компиляции.

- [ ] **Step 3: Реализация**

```csharp
namespace ChatSnippets.Core;

public interface IForegroundWindow { string? GetProcessName(); }

public interface IClipboardAccess
{
    string? ReadText();
    void WriteText(string text);
    /// <summary>previous == null → буфер очищается (в нём не было текста).</summary>
    void Restore(string? previous);
}

public interface IKeySender
{
    /// <summary>Дожидается отпускания Ctrl/Alt/Shift/Win и посылает Ctrl+V.</summary>
    Task SendCtrlVAsync();
}

public sealed class ClipboardBusyException : Exception { }

public enum PasteResult { Pasted, NotVsCode, ClipboardBusy }

public sealed class PasteCoordinator(
    IForegroundWindow foreground, IClipboardAccess clipboard, IKeySender keys, Func<TimeSpan, Task> delay)
{
    static readonly string[] VsCodeProcesses = { "Code", "Code - Insiders" };
    const int Attempts = 3;
    static readonly TimeSpan RetryPause = TimeSpan.FromMilliseconds(50);
    static readonly TimeSpan RestorePause = TimeSpan.FromMilliseconds(150);

    public async Task<PasteResult> PasteAsync(string text)
    {
        var process = foreground.GetProcessName();
        if (process is null || !VsCodeProcesses.Contains(process, StringComparer.OrdinalIgnoreCase))
            return PasteResult.NotVsCode;

        string? previous;
        try
        {
            previous = await RetryAsync(clipboard.ReadText);
            await RetryAsync(() => { clipboard.WriteText(text); return 0; });
        }
        catch (ClipboardBusyException)
        {
            return PasteResult.ClipboardBusy;
        }

        await keys.SendCtrlVAsync();
        await delay(RestorePause);

        try { await RetryAsync(() => { clipboard.Restore(previous); return 0; }); }
        catch (ClipboardBusyException) { /* текст уже вставлен; старый буфер не вернули — не критично */ }

        return PasteResult.Pasted;
    }

    async Task<T> RetryAsync<T>(Func<T> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { return action(); }
            catch (ClipboardBusyException) when (attempt < Attempts) { await delay(RetryPause); }
        }
    }
}
```

- [ ] **Step 4: Run — PASS**

Run: `dotnet test`
Expected: все зелёные.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "PasteCoordinator: проверка VS Code, буфер с повторами и восстановлением" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Win32-слой (App): P/Invoke, вставка, глобальные хоткеи

**Files:**
- Create: `src/ChatSnippets.App/Interop/Native.cs`, `Interop/NoActivate.cs`, `Services/ForegroundProbe.cs`, `Services/WpfClipboard.cs`, `Services/KeySender.cs`, `Services/HotkeyService.cs`
- Modify: `src/ChatSnippets.App/ChatSnippets.App.csproj` (манифест DPI), create `src/ChatSnippets.App/app.manifest`

**Interfaces:**
- Consumes: `IForegroundWindow`, `IClipboardAccess`, `IKeySender`, `ClipboardBusyException`, `Hotkey` (Tasks 2, 5).
- Produces:
  - `static class Native` — `GetForegroundWindow`, `GetWindowThreadProcessId`, `SendInput`, `GetAsyncKeyState`, `RegisterHotKey`, `UnregisterHotKey`, `GetCursorPos`, `SetWindowPos`, `ShowWindow`, `GetWindowLong/SetWindowLong`, `MonitorFromWindow`, `GetMonitorInfo`
  - `static class NoActivate { void Apply(Window w) }` — вызывать из `SourceInitialized`
  - `ForegroundProbe : IForegroundWindow`, `WpfClipboard : IClipboardAccess`, `KeySender : IKeySender`
  - `HotkeyService : IDisposable`: `bool TryRegister(string id, Hotkey hotkey)`, `void UnregisterAll()`, `bool IsFree(Hotkey hotkey)` (проба: регистрирует и сразу снимает), `event Action<string>? Pressed` (аргумент — id сниппета)

Этот слой — тонкие обёртки над Win32; автотестов нет, проверка ручная (Step 5).

- [ ] **Step 1: `app.manifest` (PerMonitorV2) и подключение**

`src/ChatSnippets.App/app.manifest`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```
В `ChatSnippets.App.csproj` в `<PropertyGroup>` добавить `<ApplicationManifest>app.manifest</ApplicationManifest>`, `<AssemblyName>ChatSnippets</AssemblyName>`.

- [ ] **Step 2: `Interop/Native.cs`**

```csharp
using System.Runtime.InteropServices;

namespace ChatSnippets.App.Interop;

internal static class Native
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4;
    public const uint SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040, SWP_NOZORDER = 0x0004;
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_NOREPEAT = 0x4000;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_V = 0x56;
    public const uint INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi; // задаёт правильный размер union на x64
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public INPUTUNION u; }

    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);
}
```

- [ ] **Step 3: `Interop/NoActivate.cs`**

```csharp
using System.Windows;
using System.Windows.Interop;

namespace ChatSnippets.App.Interop;

internal static class NoActivate
{
    /// <summary>Окно не забирает фокус и не показывается в Alt+Tab. Вызывать из SourceInitialized.</summary>
    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW);
    }
}
```

- [ ] **Step 4: Сервисы**

`Services/ForegroundProbe.cs`:
```csharp
using System.Diagnostics;
using ChatSnippets.App.Interop;
using ChatSnippets.Core;

namespace ChatSnippets.App.Services;

internal sealed class ForegroundProbe : IForegroundWindow
{
    public string? GetProcessName()
    {
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        try { return Process.GetProcessById((int)pid).ProcessName; }
        catch (ArgumentException) { return null; }           // процесс уже завершился
        catch (InvalidOperationException) { return null; }
    }
}
```

`Services/WpfClipboard.cs`:
```csharp
using System.Runtime.InteropServices;
using System.Windows;
using ChatSnippets.Core;

namespace ChatSnippets.App.Services;

/// <summary>Только UI-поток. COMException (CLIPBRD_E_CANT_OPEN и т.п.) → ClipboardBusyException.</summary>
internal sealed class WpfClipboard : IClipboardAccess
{
    public string? ReadText()
    {
        try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
        catch (COMException) { throw new ClipboardBusyException(); }
    }

    public void WriteText(string text)
    {
        try { Clipboard.SetDataObject(text, copy: false); }
        catch (COMException) { throw new ClipboardBusyException(); }
    }

    public void Restore(string? previous)
    {
        try
        {
            if (previous is null) Clipboard.Clear();
            else Clipboard.SetDataObject(previous, copy: false);
        }
        catch (COMException) { throw new ClipboardBusyException(); }
    }
}
```

`Services/KeySender.cs`:
```csharp
using System.Runtime.InteropServices;
using ChatSnippets.App.Interop;
using ChatSnippets.Core;

namespace ChatSnippets.App.Services;

internal sealed class KeySender : IKeySender
{
    static readonly int[] Modifiers = { Native.VK_SHIFT, Native.VK_CONTROL, Native.VK_MENU, Native.VK_LWIN, Native.VK_RWIN };

    public async Task SendCtrlVAsync()
    {
        // Хоткей вроде Ctrl+Alt+1 ещё зажат: без ожидания получилось бы Ctrl+Alt+V.
        var deadline = DateTime.UtcNow.AddSeconds(1);
        while (AnyModifierDown() && DateTime.UtcNow < deadline)
            await Task.Delay(15);

        var inputs = new[]
        {
            Key(Native.VK_CONTROL, false), Key(Native.VK_V, false),
            Key(Native.VK_V, true), Key(Native.VK_CONTROL, true),
        };
        Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
    }

    static bool AnyModifierDown() => Modifiers.Any(vk => (Native.GetAsyncKeyState(vk) & 0x8000) != 0);

    static Native.INPUT Key(int vk, bool up) => new()
    {
        type = Native.INPUT_KEYBOARD,
        u = new Native.INPUTUNION { ki = new Native.KEYBDINPUT { wVk = (ushort)vk, dwFlags = up ? Native.KEYEVENTF_KEYUP : 0 } },
    };
}
```

`Services/HotkeyService.cs`:
```csharp
using System.Windows.Interop;
using ChatSnippets.App.Interop;
using ChatSnippets.Core;

namespace ChatSnippets.App.Services;

/// <summary>Глобальные хоткеи через RegisterHotKey на скрытом message-only окне.</summary>
internal sealed class HotkeyService : IDisposable
{
    const int ProbeId = 0x7FFF;
    readonly HwndSource _source;
    readonly Dictionary<int, string> _idToSnippet = new();
    int _nextId = 1;

    public event Action<string>? Pressed;

    public HotkeyService()
    {
        // HWND_MESSAGE = -3: окно без видимости, получающее только сообщения.
        _source = new HwndSource(new HwndSourceParameters("ChatSnippetsHotkeys") { ParentWindow = new IntPtr(-3) });
        _source.AddHook(WndProc);
    }

    public bool TryRegister(string snippetId, Hotkey hotkey)
    {
        var id = _nextId++;
        if (!Native.RegisterHotKey(_source.Handle, id, (uint)hotkey.Modifiers | Native.MOD_NOREPEAT, (uint)hotkey.VirtualKey))
            return false;
        _idToSnippet[id] = snippetId;
        return true;
    }

    /// <summary>Свободно ли сочетание в системе (проба: занять и сразу освободить).</summary>
    public bool IsFree(Hotkey hotkey)
    {
        var ok = Native.RegisterHotKey(_source.Handle, ProbeId, (uint)hotkey.Modifiers, (uint)hotkey.VirtualKey);
        if (ok) Native.UnregisterHotKey(_source.Handle, ProbeId);
        return ok;
    }

    public void UnregisterAll()
    {
        foreach (var id in _idToSnippet.Keys) Native.UnregisterHotKey(_source.Handle, id);
        _idToSnippet.Clear();
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && _idToSnippet.TryGetValue(wParam.ToInt32(), out var snippetId))
        {
            handled = true;
            Pressed?.Invoke(snippetId);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.Dispose();
    }
}
```

- [ ] **Step 5: Сборка и временная ручная проверка**

Run: `dotnet build`
Expected: `Build succeeded`, 0 ошибок.

Временная проверка (в `App.xaml.cs` на время, затем откатить): в `OnStartup` создать `HotkeyService`, зарегистрировать `Ctrl+Alt+9` → по `Pressed` вызвать `new PasteCoordinator(new ForegroundProbe(), new WpfClipboard(), new KeySender(), Task.Delay).PasteAsync("тест")` и показать результат в `MessageBox` — НЕ показывать MessageBox до вставки (он заберёт фокус). Проверить: в VS Code в поле чата Ctrl+Alt+9 вставляет «тест»; в браузере — ничего; прежний буфер на месте (Ctrl+V потом вставляет старое). Откатить временный код: `git checkout src/ChatSnippets.App/App.xaml.cs`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Win32-слой: вставка Ctrl+V, буфер, глобальные хоткеи, DPI-манифест" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Тема и главное окно (внешний вид по макету)

**Files:**
- Create: `src/ChatSnippets.App/Themes/Theme.xaml`, `ViewModels/ObservableBase.cs`, `ViewModels/SnippetViewModel.cs`, `ViewModels/PanelViewModel.cs`, `Views/PanelWindow.xaml`, `Views/PanelWindow.xaml.cs`
- Modify: `src/ChatSnippets.App/App.xaml` (подключить тему, убрать `StartupUri`), `App.xaml.cs` (временный показ окна с тестовыми данными)

**Interfaces:**
- Consumes: `Snippet`, `AppConfig`, `IconStore` (Task 3), `NoActivate` (Task 6).
- Produces:
  - `enum SnippetState { Normal, Success, Error }`
  - `SnippetViewModel : ObservableBase` — `Snippet Model`, `ImageSource Icon`, `string HotkeyDisplay`, `SnippetState State`, `string AutomationName`; `void Refresh()` (перечитать иконку и подпись из `Model`); `async Task FlashAsync(SnippetState state)` (ставит состояние на 600 мс и возвращает `Normal`)
  - `PanelViewModel` — `ObservableCollection<SnippetViewModel> Items`, `bool Pinned` (двусторонне), события `ExecuteRequested(SnippetViewModel)`, `EditRequested(SnippetViewModel)`, `AddRequested`, `event`-прокси через `ICommand ExecuteCommand`, `EditCommand`, `AddCommand`
  - `PanelWindow` — публичные: `Border TitleBar` (для перетаскивания), события `MinimizeClicked`, `SideChosen(DockSide)`, `ExitRequested`; окно без фокуса, `Topmost`

- [ ] **Step 1: `ViewModels/ObservableBase.cs`**

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ChatSnippets.App.ViewModels;

public abstract class ObservableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 2: `ViewModels/SnippetViewModel.cs`**

```csharp
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
```

- [ ] **Step 3: `ViewModels/PanelViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using System.Windows.Input;
using ChatSnippets.Core;

namespace ChatSnippets.App.ViewModels;

public sealed class RelayCommand(Action<object?> run) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => run(parameter);
}

public sealed class PanelViewModel : ObservableBase
{
    bool _pinned;

    public PanelViewModel(AppConfig config, IconStore icons)
    {
        Items = new ObservableCollection<SnippetViewModel>(config.Snippets.Select(s => new SnippetViewModel(s, icons)));
        _pinned = config.Window.Pinned;
        ExecuteCommand = new RelayCommand(p => ExecuteRequested?.Invoke((SnippetViewModel)p!));
        EditCommand = new RelayCommand(p => EditRequested?.Invoke((SnippetViewModel)p!));
        AddCommand = new RelayCommand(_ => AddRequested?.Invoke());
    }

    public ObservableCollection<SnippetViewModel> Items { get; }
    public bool Pinned { get => _pinned; set { if (Set(ref _pinned, value)) PinnedChanged?.Invoke(value); } }

    public ICommand ExecuteCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand AddCommand { get; }

    public event Action<SnippetViewModel>? ExecuteRequested;
    public event Action<SnippetViewModel>? EditRequested;
    public event Action? AddRequested;
    public event Action<bool>? PinnedChanged;
}
```

- [ ] **Step 4: `Themes/Theme.xaml`**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Color x:Key="WindowBgColor">#1E1F24</Color>
    <SolidColorBrush x:Key="WindowBg" Color="#1E1F24"/>
    <SolidColorBrush x:Key="ButtonBg" Color="#2B2D34"/>
    <SolidColorBrush x:Key="ButtonBgHover" Color="#33363F"/>
    <SolidColorBrush x:Key="ButtonBgPressed" Color="#23252B"/>
    <SolidColorBrush x:Key="BorderBrushNormal" Color="#3A3D46"/>
    <SolidColorBrush x:Key="Accent" Color="#4C8DFF"/>
    <SolidColorBrush x:Key="TextPrimary" Color="#E6E6E6"/>
    <SolidColorBrush x:Key="TextSecondary" Color="#B9BCC5"/>
    <SolidColorBrush x:Key="Success" Color="#35D978"/>
    <SolidColorBrush x:Key="Danger" Color="#FF5B5B"/>
    <SolidColorBrush x:Key="DashedBorder" Color="#737782"/>

    <Style TargetType="TextBlock">
        <Setter Property="FontFamily" Value="Segoe UI"/>
        <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
    </Style>

    <!-- Кнопка сниппета 64x64: иконка 32 + подпись хоткея. Размер не меняется ни в одном состоянии. -->
    <Style x:Key="SnippetButton" TargetType="Button">
        <Setter Property="Width" Value="64"/>
        <Setter Property="Height" Value="64"/>
        <Setter Property="Margin" Value="0,0,0,4"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Grid>
                        <Border x:Name="Bd" CornerRadius="11" BorderThickness="1"
                                Background="{StaticResource ButtonBg}" BorderBrush="{StaticResource BorderBrushNormal}">
                            <StackPanel x:Name="Inner" VerticalAlignment="Center" HorizontalAlignment="Center"
                                        RenderTransformOrigin="0.5,0.5">
                                <StackPanel.RenderTransform><TranslateTransform x:Name="Shift" Y="0"/></StackPanel.RenderTransform>
                                <Image Source="{Binding Icon}" Width="32" Height="32" RenderOptions.BitmapScalingMode="HighQuality"/>
                                <TextBlock Text="{Binding HotkeyDisplay}" Margin="0,2,0,0" FontSize="9"
                                           Foreground="{StaticResource TextSecondary}" TextAlignment="Center"/>
                            </StackPanel>
                        </Border>
                        <Border x:Name="CheckBadge" Visibility="Collapsed" Width="16" Height="16" CornerRadius="8"
                                Background="{StaticResource Success}" HorizontalAlignment="Right" VerticalAlignment="Top"
                                Margin="0,-3,-3,0">
                            <TextBlock Text="&#xE73E;" FontFamily="Segoe MDL2 Assets" FontSize="10" Foreground="#0E1A12"
                                       HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <Border x:Name="Focus" CornerRadius="11" BorderThickness="2" BorderBrush="{StaticResource Accent}"
                                Visibility="Collapsed" IsHitTestVisible="False"/>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource Accent}"/>
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource ButtonBgHover}"/>
                        </Trigger>
                        <Trigger Property="IsKeyboardFocused" Value="True">
                            <Setter TargetName="Focus" Property="Visibility" Value="Visible"/>
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="{StaticResource ButtonBgPressed}"/>
                            <Setter TargetName="Shift" Property="Y" Value="1"/>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="Inner" Property="Opacity" Value="0.4"/>
                        </Trigger>
                        <DataTrigger Binding="{Binding State}" Value="Success">
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource Success}"/>
                            <Setter TargetName="Bd" Property="BorderThickness" Value="2"/>
                            <Setter TargetName="CheckBadge" Property="Visibility" Value="Visible"/>
                        </DataTrigger>
                        <DataTrigger Binding="{Binding State}" Value="Error">
                            <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource Danger}"/>
                            <Setter TargetName="Bd" Property="BorderThickness" Value="2"/>
                        </DataTrigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Кнопка «+»: пунктирная рамка -->
    <Style x:Key="AddButton" TargetType="Button">
        <Setter Property="Width" Value="64"/>
        <Setter Property="Height" Value="64"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Grid Background="Transparent">
                        <Rectangle x:Name="Dash" RadiusX="11" RadiusY="11" Margin="8" StrokeThickness="1.5"
                                   Stroke="{StaticResource DashedBorder}" StrokeDashArray="3 3"/>
                        <TextBlock Text="+" FontSize="26" Foreground="{StaticResource TextPrimary}"
                                   HorizontalAlignment="Center" VerticalAlignment="Center" Margin="0,-3,0,0"/>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Dash" Property="Stroke" Value="{StaticResource Accent}"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Маленькие кнопки шапки 24x24 (глифы Segoe MDL2 Assets) -->
    <Style x:Key="TitleGlyphButton" TargetType="ButtonBase">
        <Setter Property="Width" Value="24"/>
        <Setter Property="Height" Value="24"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
        <Setter Property="Foreground" Value="{StaticResource TextSecondary}"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ButtonBase">
                    <Border x:Name="Bd" Background="Transparent" CornerRadius="5">
                        <TextBlock x:Name="G" Text="{TemplateBinding Content}" FontFamily="Segoe MDL2 Assets" FontSize="13"
                                   Foreground="{TemplateBinding Foreground}"
                                   HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Bd" Property="Background" Value="#2B2D34"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>
```

Примечание: для включённого pin в `PanelWindow.xaml` использовать `Foreground` через триггер на `ToggleButton.IsChecked` (акцентный цвет).

- [ ] **Step 5: `App.xaml` и `Views/PanelWindow.xaml`**

`App.xaml` (убрать `StartupUri`, подключить тему):
```xml
<Application x:Class="ChatSnippets.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Themes/Theme.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```
Удалить `MainWindow.xaml`/`MainWindow.xaml.cs` из шаблона.

`Views/PanelWindow.xaml`:
```xml
<Window x:Class="ChatSnippets.App.Views.PanelWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Chat Snippets" Width="80" Height="928"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        ResizeMode="NoResize" ShowInTaskbar="False" ShowActivated="False" Topmost="True"
        SnapsToDevicePixels="True" UseLayoutRounding="True" FontFamily="Segoe UI">
    <Border Background="{StaticResource WindowBg}" CornerRadius="8" BorderBrush="{StaticResource BorderBrushNormal}" BorderThickness="1">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="32"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <Border x:Name="TitleBar" Grid.Row="0" Background="Transparent" CornerRadius="8,8,0,0" ToolTip="Chat Snippets">
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center">
                    <ToggleButton x:Name="PinButton" Style="{StaticResource TitleGlyphButton}" Content="&#xE718;"
                                  ToolTip="Keep panel open" AutomationProperties.Name="Keep panel open"
                                  IsChecked="{Binding Pinned, Mode=TwoWay}"/>
                    <Button x:Name="GearButton" Style="{StaticResource TitleGlyphButton}" Content="&#xE713;"
                            ToolTip="Settings" AutomationProperties.Name="Settings" Click="GearButton_Click"/>
                    <Button x:Name="MinimizeButton" Style="{StaticResource TitleGlyphButton}" Content="&#xE921;"
                            ToolTip="Hide panel" AutomationProperties.Name="Hide panel" Click="MinimizeButton_Click"/>
                </StackPanel>
            </Border>

            <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Hidden" HorizontalScrollBarVisibility="Disabled">
                <StackPanel Margin="8,8,8,8">
                    <ItemsControl x:Name="SnippetList" ItemsSource="{Binding Items}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Button Style="{StaticResource SnippetButton}"
                                        Command="{Binding DataContext.ExecuteCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                        CommandParameter="{Binding}"
                                        AutomationProperties.Name="{Binding AutomationName}">
                                    <Button.ContextMenu>
                                        <ContextMenu>
                                            <MenuItem Header="Edit..."
                                                      Command="{Binding DataContext.EditCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                                      CommandParameter="{Binding}"/>
                                        </ContextMenu>
                                    </Button.ContextMenu>
                                </Button>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                    <Button Style="{StaticResource AddButton}" Command="{Binding AddCommand}"
                            ToolTip="Add icon" AutomationProperties.Name="Add icon"/>
                </StackPanel>
            </ScrollViewer>
        </Grid>
    </Border>
</Window>
```
(Пункт «Delete» в контекстное меню добавляется в Task 9.)

`Views/PanelWindow.xaml.cs`:
```csharp
using System.Windows;
using System.Windows.Controls;
using ChatSnippets.App.Interop;
using ChatSnippets.App.ViewModels;
using ChatSnippets.Core;

namespace ChatSnippets.App.Views;

public partial class PanelWindow : Window
{
    public event Action? MinimizeClicked;
    public event Action<DockSide>? SideChosen;
    public event Action? ExitRequested;

    public PanelWindow(PanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => NoActivate.Apply(this);
    }

    public Border TitleBarElement => TitleBar;

    void MinimizeButton_Click(object sender, RoutedEventArgs e) => MinimizeClicked?.Invoke();

    void GearButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var left = new MenuItem { Header = "Left side" };
        left.Click += (_, _) => SideChosen?.Invoke(DockSide.Left);
        var right = new MenuItem { Header = "Right side" };
        right.Click += (_, _) => SideChosen?.Invoke(DockSide.Right);
        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(left);
        menu.Items.Add(right);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }
}
```

- [ ] **Step 6: Временный показ для визуальной сверки**

В `App.xaml.cs` заменить содержимое на (временно, будет переписано в Task 10):
```csharp
using System.Windows;
using ChatSnippets.App.ViewModels;
using ChatSnippets.App.Views;
using ChatSnippets.Core;

namespace ChatSnippets.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var cfg = new AppConfig();
        for (var i = 1; i <= 12; i++)
            cfg.Snippets.Add(new Snippet { Text = "t", Hotkey = i <= 9 ? $"Ctrl+Alt+{i}" : i == 10 ? "Ctrl+Alt+0" : i == 11 ? "Ctrl+Alt+Q" : "Ctrl+Alt+W" });
        var window = new PanelWindow(new PanelViewModel(cfg, new IconStore(IconStore.DefaultDirectory)))
        { Left = 200, Top = 20 };
        window.Show();
    }
}
```

- [ ] **Step 7: Визуальная сверка с макетом**

Run: `dotnet run --project src/ChatSnippets.App`
Expected: окно 80 px шириной с тёмным фоном, шапка с тремя глифами, 12 серых плиток 64×64 с подписями `Ctrl+Alt+1`… под (пустым) значком, внизу пунктирная «+». Сверить с `.concept/chat-snippets-ui-mockup-vertical-80px.png` (отступы 8, зазор 4, подписи без плашки). Hover подсвечивает рамку синим. Окно не отнимает фокус у активного окна при клике (проверить: кликнуть по плитке, курсор в другом приложении остаётся).

- [ ] **Step 8: Закрыть программу, Commit**

```bash
git add -A
git commit -m "Тема и главное окно по макету: плитки, шапка, состояния" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Флажок и выезд панели (DockController)

**Files:**
- Create: `src/ChatSnippets.App/Views/FlagWindow.xaml`, `Views/FlagWindow.xaml.cs`, `Services/DockController.cs`
- Modify: `src/ChatSnippets.App/App.xaml.cs` (временно подключить контроллер)

**Interfaces:**
- Consumes: `DockLogic`, `PxRect`, `DockSide`, `WindowSettings` (Tasks 3, 4); `Native`, `NoActivate` (Task 6); `PanelWindow` (Task 7).
- Produces:
  - `FlagWindow`: `event Action? Clicked`; `void SetDirection(bool pointsRight)` (глиф стрелки); `Task FlashAsync()` (зелёная вспышка ~1 с)
  - `DockController(PanelWindow panel, FlagWindow flag, WindowSettings settings, Func<int> itemCount, Action save)`:
    - `void Start()` — показать окна, свернуть панель
    - `void Expand()`, `void Collapse()`, `void Toggle()`, `bool IsExpanded`
    - `void Relayout()` — пересчитать размеры/позиции (смена числа иконок, DPI, смена стороны)
    - `bool Suspended { get; set; }` — не сворачивать по таймеру (открыт диалог)
    - `void SetSide(DockSide side)`
    - `Task FlashFlagAsync()`

- [ ] **Step 1: `Views/FlagWindow.xaml`**

```xml
<Window x:Class="ChatSnippets.App.Views.FlagWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Chat Snippets flag" Width="16" Height="64"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        ResizeMode="NoResize" ShowInTaskbar="False" ShowActivated="False" Topmost="True">
    <Border x:Name="Body" Background="{StaticResource Accent}" CornerRadius="6" Cursor="Hand"
            MouseLeftButtonUp="Body_MouseLeftButtonUp" ToolTip="Chat Snippets">
        <TextBlock x:Name="Arrow" Text="&#xE76C;" FontFamily="Segoe MDL2 Assets" FontSize="11" Foreground="White"
                   HorizontalAlignment="Center" VerticalAlignment="Center"/>
    </Border>
</Window>
```

`Views/FlagWindow.xaml.cs`:
```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ChatSnippets.App.Interop;

namespace ChatSnippets.App.Views;

public partial class FlagWindow : Window
{
    public event Action? Clicked;

    public FlagWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => NoActivate.Apply(this);
    }

    /// <summary>E76C = ›, E76B = ‹.</summary>
    public void SetDirection(bool pointsRight) => Arrow.Text = pointsRight ? "\uE76C" : "\uE76B";

    public async Task FlashAsync()
    {
        Body.Background = (Brush)FindResource("Success");
        await Task.Delay(1000);
        Body.Background = (Brush)FindResource("Accent");
    }

    void Body_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => Clicked?.Invoke();
}
```

- [ ] **Step 2: `Services/DockController.cs`**

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ChatSnippets.App.Interop;
using ChatSnippets.App.Views;
using ChatSnippets.Core;

namespace ChatSnippets.App.Services;

/// <summary>Выезд/уезд панели, флажок, перетаскивание за шапку, таймер ухода мыши. Всё в физических пикселях.</summary>
internal sealed class DockController
{
    const int PanelDips = 80, FlagWidthDips = 16, FlagHeightDips = 64;
    static readonly TimeSpan SlideTime = TimeSpan.FromMilliseconds(180);
    static readonly TimeSpan LeaveDelay = TimeSpan.FromSeconds(1.5);

    readonly PanelWindow _panel;
    readonly FlagWindow _flag;
    readonly WindowSettings _settings;
    readonly Func<int> _itemCount;
    readonly Action _save;
    readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    readonly DispatcherTimer _slideTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };

    IntPtr _panelHwnd, _flagHwnd;
    bool _expanded, _dragging;
    DateTime _outsideSince = DateTime.UtcNow;
    double _slideProgress;          // 0..1
    int _slideFromX, _slideToX;
    Action? _slideDone;
    Native.POINT _dragStartCursor;
    int _dragStartLeft, _dragStartTop;

    public DockController(PanelWindow panel, FlagWindow flag, WindowSettings settings, Func<int> itemCount, Action save)
    {
        _panel = panel; _flag = flag; _settings = settings; _itemCount = itemCount; _save = save;
        _hoverTimer.Tick += (_, _) => OnHoverTick();
        _slideTimer.Tick += (_, _) => OnSlideTick();
        _flag.Clicked += Toggle;
        _panel.MinimizeClicked += Collapse;
        _panel.SideChosen += SetSide;
        var title = _panel.TitleBarElement;
        title.MouseLeftButtonDown += TitleBar_Down;
        title.MouseMove += TitleBar_Move;
        title.MouseLeftButtonUp += TitleBar_Up;
    }

    public bool IsExpanded => _expanded;
    public bool Suspended { get; set; }
    bool Pinned => _settings.Pinned;

    public void Start()
    {
        _panel.Show();
        _flag.Show();
        _panelHwnd = new WindowInteropHelper(_panel).Handle;
        _flagHwnd = new WindowInteropHelper(_flag).Handle;
        _expanded = false;
        Relayout();
        _hoverTimer.Start();
    }

    public Task FlashFlagAsync() => _flag.FlashAsync();

    public void Toggle() { if (_expanded) Collapse(); else Expand(); }

    public void Expand()
    {
        if (_expanded) return;
        _expanded = true;
        _outsideSince = DateTime.UtcNow;
        var m = Measure();
        Native.ShowWindow(_panelHwnd, Native.SW_SHOWNOACTIVATE);
        _flag.SetDirection(_settings.Side == DockSide.Right);       // стрелка внутрь панели
        StartSlide(DockLogic.PanelLeft(m.Work, _settings.Side, false, m.PanelW),
                   DockLogic.PanelLeft(m.Work, _settings.Side, true, m.PanelW), done: null);
    }

    public void Collapse()
    {
        if (!_expanded) return;
        _expanded = false;
        var m = Measure();
        _flag.SetDirection(_settings.Side == DockSide.Left);
        StartSlide(DockLogic.PanelLeft(m.Work, _settings.Side, true, m.PanelW),
                   DockLogic.PanelLeft(m.Work, _settings.Side, false, m.PanelW),
                   done: () => Native.ShowWindow(_panelHwnd, Native.SW_HIDE)); // не светиться на соседнем мониторе
    }

    public void SetSide(DockSide side)
    {
        _settings.Side = side;
        _save();
        Relayout();
    }

    /// <summary>Пересчитать размеры и позиции без анимации (старт, смена числа иконок/стороны/DPI).</summary>
    public void Relayout()
    {
        _slideTimer.Stop();
        var m = Measure();
        _settings.Top = DockLogic.ClampTop(m.Work, m.Work.Top + _settings.Top, m.PanelH) - m.Work.Top;
        var panelX = DockLogic.PanelLeft(m.Work, _settings.Side, _expanded, m.PanelW);
        Native.SetWindowPos(_panelHwnd, Native.HWND_TOPMOST, panelX, m.Work.Top + _settings.Top, m.PanelW, m.PanelH, Native.SWP_NOACTIVATE);
        PlaceFlag(m, _expanded);
        _flag.SetDirection(_expanded ? _settings.Side == DockSide.Right : _settings.Side == DockSide.Left);
        Native.ShowWindow(_panelHwnd, _expanded ? Native.SW_SHOWNOACTIVATE : Native.SW_HIDE);
    }

    // ---- анимация ----
    void StartSlide(int fromX, int toX, Action? done)
    {
        _slideFromX = fromX; _slideToX = toX; _slideDone = done; _slideProgress = 0;
        _slideTimer.Start();
    }

    void OnSlideTick()
    {
        _slideProgress = Math.Min(1, _slideProgress + _slideTimer.Interval / SlideTime);
        var eased = 1 - Math.Pow(1 - _slideProgress, 3);           // ease-out cubic
        var m = Measure();
        var x = (int)Math.Round(_slideFromX + (_slideToX - _slideFromX) * eased);
        Native.SetWindowPos(_panelHwnd, Native.HWND_TOPMOST, x, m.Work.Top + _settings.Top, m.PanelW, m.PanelH, Native.SWP_NOACTIVATE);
        PlaceFlag(m, flagFollowX: x);
        if (_slideProgress >= 1)
        {
            _slideTimer.Stop();
            _slideDone?.Invoke();
            PlaceFlag(m, _expanded);
        }
    }

    // ---- флажок ----
    void PlaceFlag(Metrics m, bool expanded) =>
        Native.SetWindowPos(_flagHwnd, Native.HWND_TOPMOST,
            DockLogic.FlagLeft(m.Work, _settings.Side, expanded, m.PanelW, m.FlagW),
            m.Work.Top + _settings.Top, m.FlagW, m.FlagH, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);

    /// <summary>Флажок едет вместе с панелью: сдвиг от текущего x панели.</summary>
    void PlaceFlag(Metrics m, int flagFollowX)
    {
        var x = _settings.Side == DockSide.Left ? flagFollowX + m.PanelW : flagFollowX - m.FlagW;
        // Для левого края флажок не уходит за край экрана; для правого — не правее края.
        x = _settings.Side == DockSide.Left ? Math.Max(x, m.Work.Left) : Math.Min(x, m.Work.Right - m.FlagW);
        Native.SetWindowPos(_flagHwnd, Native.HWND_TOPMOST, x, m.Work.Top + _settings.Top, m.FlagW, m.FlagH,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }

    // ---- таймер ухода мыши ----
    void OnHoverTick()
    {
        if (!_expanded || Pinned || Suspended || _dragging) { _outsideSince = DateTime.UtcNow; return; }
        Native.GetCursorPos(out var p);
        var m = Measure();
        var panelRect = new PxRect(DockLogic.PanelLeft(m.Work, _settings.Side, true, m.PanelW), m.Work.Top + _settings.Top, m.PanelW, m.PanelH);
        var flagRect = new PxRect(DockLogic.FlagLeft(m.Work, _settings.Side, true, m.PanelW, m.FlagW), m.Work.Top + _settings.Top, m.FlagW, m.FlagH);
        if (panelRect.Contains(p.X, p.Y) || flagRect.Contains(p.X, p.Y)) { _outsideSince = DateTime.UtcNow; return; }
        if (DateTime.UtcNow - _outsideSince >= LeaveDelay) Collapse();
    }

    // ---- перетаскивание за шапку ----
    void TitleBar_Down(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not (System.Windows.Controls.Border or System.Windows.Controls.StackPanel)) return; // клики по кнопкам не тащат
        _dragging = true;
        Native.GetCursorPos(out _dragStartCursor);
        var m = Measure();
        _dragStartLeft = DockLogic.PanelLeft(m.Work, _settings.Side, _expanded, m.PanelW);
        _dragStartTop = m.Work.Top + _settings.Top;
        _flag.Hide();
        ((UIElement)sender).CaptureMouse();
    }

    void TitleBar_Move(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        Native.GetCursorPos(out var p);
        var m = Measure();
        Native.SetWindowPos(_panelHwnd, Native.HWND_TOPMOST,
            _dragStartLeft + p.X - _dragStartCursor.X, _dragStartTop + p.Y - _dragStartCursor.Y,
            m.PanelW, m.PanelH, Native.SWP_NOACTIVATE);
    }

    void TitleBar_Up(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
        Native.GetCursorPos(out var p);
        var m = Measure();
        var left = _dragStartLeft + p.X - _dragStartCursor.X;
        var top = _dragStartTop + p.Y - _dragStartCursor.Y;
        _settings.Side = DockLogic.NearestSide(m.Work, left, m.PanelW);
        _settings.Top = DockLogic.ClampTop(m.Work, top, m.PanelH) - m.Work.Top;
        _save();
        Relayout();
    }

    // ---- измерения ----
    readonly record struct Metrics(PxRect Work, double Scale, int PanelW, int PanelH, int FlagW, int FlagH);

    Metrics Measure()
    {
        var info = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        var monitor = Native.MonitorFromWindow(_panelHwnd, Native.MONITOR_DEFAULTTONEAREST);
        Native.GetMonitorInfo(monitor, ref info);
        var work = new PxRect(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right - info.rcWork.Left, info.rcWork.Bottom - info.rcWork.Top);
        var scale = VisualTreeHelper.GetDpi(_panel).DpiScaleX;
        return new Metrics(work, scale,
            (int)Math.Round(PanelDips * scale),
            DockLogic.PanelHeightPx(work, _itemCount() + 1, scale),
            (int)Math.Round(FlagWidthDips * scale),
            (int)Math.Round(FlagHeightDips * scale));
    }
}
```

- [ ] **Step 3: Временное подключение**

В `App.xaml.cs` после создания `window` заменить `window.Show()` на:
```csharp
var flag = new FlagWindow();
var dock = new Services.DockController(window, flag, cfg.Window, () => cfg.Snippets.Count, () => { });
dock.Start();
```
(добавить `using ChatSnippets.App.Views;` если нужно).

- [ ] **Step 4: Ручная проверка**

Run: `dotnet run --project src/ChatSnippets.App`
Expected:
1. На правом краю экрана виден только синий флажок 16×64.
2. Клик по флажку: панель выезжает за ~0,2 с, флажок встаёт слева от неё, стрелка смотрит наружу.
3. Увести мышь на 1,5 с → панель уезжает; нажатие pin → не уезжает; minus → уезжает сразу; повторный клик по флажку → уезжает.
4. Тянуть панель за пустое место шапки к левому краю и отпустить → прилипает слева, флажок справа от неё, при сворачивании остаётся у левого края.
5. Шестерёнка → «Left side/Right side» переключает сторону, «Exit» пока ничего не делает (подключится в Task 10).
6. Окна не забирают фокус: открыть блокнот, кликнуть флажок и плитку — курсор в блокноте остаётся.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Флажок и выезд панели: анимация, таймер ухода мыши, перетаскивание, док к краю" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Окно «Edit icon», поле хоткея, меню плитки

**Files:**
- Create: `src/ChatSnippets.App/Views/HotkeyBox.cs`, `Views/EditSnippetWindow.xaml`, `Views/EditSnippetWindow.xaml.cs`
- Modify: `src/ChatSnippets.App/Views/PanelWindow.xaml` (пункт «Delete» в меню плитки; перетаскивание для порядка), `ViewModels/PanelViewModel.cs` (команды `DeleteCommand`, `MoveCommand`)

**Interfaces:**
- Consumes: `Hotkey`, `HotkeyRules`, `IconStore`, `Snippet` (Tasks 2–3); `SnippetViewModel`, `PanelViewModel` (Task 7).
- Produces:
  - `HotkeyBox : TextBox` — `Hotkey? Value { get; set; }` (dependency property), `event EventHandler? ValueChanged`
  - `EditSnippetWindow(Snippet working, bool isNew, IconStore icons, IEnumerable<Snippet> others, Func<Hotkey, bool> isFreeInSystem)`:
    - `bool Deleted` (после закрытия), `bool? DialogResult` (true = Save)
    - перед закрытием по Save пишет в `working` поля `Text`, `Hotkey` (строка или null), `IconFile`
  - `PanelViewModel.DeleteCommand`, `PanelViewModel.MoveRequested(SnippetViewModel from, SnippetViewModel to)` (перетаскивание)

- [ ] **Step 1: `Views/HotkeyBox.cs`**

```csharp
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
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Back or Key.Delete) { Set(null); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System or Key.ImeProcessed or Key.Tab or Key.Escape) { e.Handled = key != Key.Tab && key != Key.Escape; return; }

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
```

- [ ] **Step 2: `Views/EditSnippetWindow.xaml`**

```xml
<Window x:Class="ChatSnippets.App.Views.EditSnippetWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:v="clr-namespace:ChatSnippets.App.Views"
        Title="Edit icon" Width="340" SizeToContent="Height"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        ResizeMode="NoResize" ShowInTaskbar="False" Topmost="True"
        WindowStartupLocation="CenterScreen" FontFamily="Segoe UI">
    <Border Background="{StaticResource WindowBg}" CornerRadius="8" BorderBrush="{StaticResource BorderBrushNormal}" BorderThickness="1" Padding="16">
        <StackPanel>
            <TextBlock x:Name="TitleText" Text="Edit icon" FontSize="16" FontWeight="SemiBold" Margin="0,0,0,12"
                       MouseLeftButtonDown="Title_MouseLeftButtonDown" Background="Transparent"/>

            <StackPanel Orientation="Horizontal">
                <Border Width="88" Height="88" CornerRadius="10" Background="{StaticResource ButtonBg}"
                        BorderBrush="{StaticResource BorderBrushNormal}" BorderThickness="1">
                    <Image x:Name="Preview" Width="48" Height="48"/>
                </Border>
                <Button x:Name="ChooseButton" Content="Choose image..." Margin="12,0,0,0" Height="40" Width="190"
                        VerticalAlignment="Top" Click="ChooseButton_Click"/>
            </StackPanel>

            <TextBlock Text="Text to paste" Margin="0,14,0,4" Foreground="{StaticResource TextPrimary}"/>
            <TextBox x:Name="TextBox" Height="120" AcceptsReturn="True" TextWrapping="Wrap" VerticalScrollBarVisibility="Auto"
                     Background="#17181C" Foreground="{StaticResource TextPrimary}" BorderBrush="{StaticResource BorderBrushNormal}"
                     CaretBrush="{StaticResource TextPrimary}" Padding="6"/>

            <TextBlock Text="Hotkey" Margin="0,12,0,4"/>
            <v:HotkeyBox x:Name="HotkeyField" Height="32" Padding="6,5" Background="#17181C"
                         Foreground="{StaticResource TextPrimary}" BorderBrush="{StaticResource BorderBrushNormal}"
                         ValueChanged="HotkeyField_ValueChanged"/>
            <TextBlock Text="Press the combination" Foreground="{StaticResource TextSecondary}" Margin="0,4,0,0" FontSize="12"/>
            <TextBlock x:Name="ErrorText" Foreground="{StaticResource Danger}" FontSize="12" Margin="0,4,0,0"
                       TextWrapping="Wrap" Visibility="Collapsed"/>

            <Grid Margin="0,16,0,0">
                <Button x:Name="DeleteButton" Content="Delete" HorizontalAlignment="Left" Foreground="{StaticResource Danger}"
                        Background="Transparent" BorderThickness="0" Click="DeleteButton_Click"/>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                    <Button x:Name="SaveButton" Content="Save" Width="88" Height="34" IsDefault="True"
                            Background="#1A73FF" Foreground="White" BorderThickness="0" Click="SaveButton_Click"/>
                    <Button Content="Cancel" Width="88" Height="34" Margin="8,0,0,0" IsCancel="True"
                            Background="#2B2D34" Foreground="{StaticResource TextPrimary}" BorderThickness="0"/>
                </StackPanel>
            </Grid>
        </StackPanel>
    </Border>
</Window>
```

- [ ] **Step 3: `Views/EditSnippetWindow.xaml.cs`**

```csharp
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
    readonly bool _isNew;
    readonly IconStore _icons;
    readonly IEnumerable<Snippet> _others;
    readonly Func<Hotkey, bool> _isFreeInSystem;
    readonly Hotkey? _originalHotkey;
    string? _iconFile;

    public bool Deleted { get; private set; }

    public EditSnippetWindow(Snippet working, bool isNew, IconStore icons, IEnumerable<Snippet> others, Func<Hotkey, bool> isFreeInSystem)
    {
        InitializeComponent();
        _working = working; _isNew = isNew; _icons = icons; _others = others; _isFreeInSystem = isFreeInSystem;
        _iconFile = working.IconFile;
        TextBox.Text = working.Text;
        _originalHotkey = Hotkey.TryParse(working.Hotkey, out var h) ? h : null;
        HotkeyField.Value = _originalHotkey;
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
```

- [ ] **Step 4: Перетаскивание порядка и «Delete» в меню**

В `PanelViewModel` добавить:
```csharp
public ICommand DeleteCommand { get; }              // в конструкторе: new RelayCommand(p => DeleteRequested?.Invoke((SnippetViewModel)p!))
public event Action<SnippetViewModel>? DeleteRequested;
public event Action<SnippetViewModel, SnippetViewModel>? MoveRequested;
public void RequestMove(SnippetViewModel from, SnippetViewModel to) => MoveRequested?.Invoke(from, to);
```
В `PanelWindow.xaml` в `ContextMenu` после «Edit...» добавить:
```xml
<MenuItem Header="Delete"
          Command="{Binding DataContext.DeleteCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
          CommandParameter="{Binding}"/>
```
В `DataTemplate` кнопки добавить `AllowDrop="True" PreviewMouseMove="Snippet_PreviewMouseMove" Drop="Snippet_Drop"`, а в `PanelWindow.xaml.cs`:
```csharp
Point _dragOrigin;

void Snippet_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _dragOrigin = e.GetPosition(null);

void Snippet_PreviewMouseMove(object sender, MouseEventArgs e)
{
    if (e.LeftButton != MouseButtonState.Pressed || sender is not Button { DataContext: SnippetViewModel vm }) return;
    var d = e.GetPosition(null) - _dragOrigin;
    if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
    DragDrop.DoDragDrop((DependencyObject)sender, vm, DragDropEffects.Move);
}

void Snippet_Drop(object sender, DragEventArgs e)
{
    if (e.Data.GetData(typeof(SnippetViewModel)) is SnippetViewModel from && sender is Button { DataContext: SnippetViewModel to } && from != to)
        ((PanelViewModel)DataContext).RequestMove(from, to);
}
```
(в XAML кнопки также добавить `PreviewMouseLeftButtonDown="Snippet_PreviewMouseLeftButtonDown"`). Клик без движения по-прежнему вызывает `ExecuteCommand`; DragDrop стартует только после сдвига мыши.

- [ ] **Step 5: Ручная проверка диалога (временно из `App.xaml.cs`)**

Run: временно в `App.xaml.cs` показать `new EditSnippetWindow(new Snippet{Hotkey="Ctrl+Alt+1"}, false, icons, new List<Snippet>(), _ => true).ShowDialog()`; `dotnet run`.
Expected: диалог как на макете (предпросмотр 88×88, `Choose image...`, `Text to paste`, `Hotkey`, подсказка, `Delete` красным слева, `Save` синяя и `Cancel` серая справа). В поле Hotkey нажать Ctrl+Shift+K → показывает `Ctrl+Shift+K`; просто «K» без модификатора — ничего не происходит; Backspace очищает. Выбор картинки показывает превью. Откатить временный код.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Окно Edit icon, поле хоткея, меню плитки, перетаскивание порядка" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Сборка воедино (App), сохранение, приёмка

**Files:**
- Modify: `src/ChatSnippets.App/App.xaml.cs` (полная версия), `src/ChatSnippets.App/ViewModels/PanelViewModel.cs` (при необходимости)
- Create: `README.md`

**Interfaces:**
- Consumes: всё из Tasks 2–9.
- Produces: рабочее приложение.

- [ ] **Step 1: Полный `App.xaml.cs`**

```csharp
using System.Windows;
using ChatSnippets.App.Services;
using ChatSnippets.App.ViewModels;
using ChatSnippets.App.Views;
using ChatSnippets.Core;

namespace ChatSnippets.App;

public partial class App : Application
{
    Mutex? _singleInstance;
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

        // Второй экземпляр тихо выходит: хоткеи всё равно заняты первым.
        _singleInstance = new Mutex(true, "ChatSnippets.SingleInstance", out var isFirst);
        if (!isFirst) { Shutdown(); return; }

        _store = new ConfigStore(ConfigStore.DefaultPath);
        _config = _store.Load();
        _icons = new IconStore(IconStore.DefaultDirectory);
        _vm = new PanelViewModel(_config, _icons);
        _hotkeys = new HotkeyService();
        _paste = new PasteCoordinator(new ForegroundProbe(), new WpfClipboard(), new KeySender(), Task.Delay);

        var panel = new PanelWindow(_vm);
        var flag = new FlagWindow();
        _dock = new DockController(panel, flag, _config.Window, () => _vm.Items.Count, Save);

        _vm.ExecuteRequested += vm => _ = ExecuteAsync(vm, fromHotkey: false);
        _vm.EditRequested += vm => EditSnippet(vm.Model, isNew: false, vm);
        _vm.AddRequested += AddSnippet;
        _vm.DeleteRequested += DeleteSnippet;
        _vm.MoveRequested += MoveSnippet;
        _vm.PinnedChanged += pinned => { _config.Window.Pinned = pinned; Save(); };
        _hotkeys.Pressed += id =>
        {
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

    void AddSnippet()
    {
        var snippet = new Snippet { Hotkey = HotkeyRules.NextFreeDefault(_config.Snippets).ToString() };
        EditSnippet(snippet, isNew: true, existing: null);
    }

    void EditSnippet(Snippet snippet, bool isNew, SnippetViewModel? existing)
    {
        // Редактируем копию: Cancel не должен менять оригинал.
        var working = new Snippet { Id = snippet.Id, IconFile = snippet.IconFile, Text = snippet.Text, Hotkey = snippet.Hotkey };
        _dock.Suspended = true;
        try
        {
            var others = _config.Snippets.Where(s => s.Id != snippet.Id).ToList();
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
        finally { _dock.Suspended = false; }
    }

    void DeleteSnippet(SnippetViewModel vm)
    {
        var answer = MessageBox.Show("Удалить эту иконку?", "Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Yes) DeleteSnippetCore(vm);
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
        return failed;
    }

    void Save()
    {
        try { _store.Save(_config); }
        catch (IOException) { /* диск занят/недоступен — настройки не критичны, повторим при следующем изменении */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
```

Примечание: диалог владеет только собой (`Topmost`, без `Owner`); при `ShowDialog` панель остаётся кликабельной только если не модальна — допустимо, т.к. диалог закрывается Save/Cancel.

- [ ] **Step 2: `README.md`**

```markdown
# Chat Snippets

Панель крупных иконок у края монитора. Клик по иконке или глобальная горячая клавиша вставляет назначенный текст в окно VS Code (туда, где стоит курсор — обычно поле чата Claude Code). Enter не нажимается.

- Запуск: `dotnet run --project src/ChatSnippets.App`
- Флажок у края экрана — клик выдвигает панель; уходит сама, когда мышь вне панели 1,5 с (pin — не уходить).
- Правый клик по иконке — Edit / Delete; «+» — новая иконка; иконки перетаскиваются для смены порядка.
- Шестерёнка — сторона экрана и Exit.
- Данные: `%AppData%\ChatSnippets\config.json`, картинки — `%AppData%\ChatSnippets\icons`.
```

- [ ] **Step 3: Автотесты и сборка**

Run: `dotnet build && dotnet test`
Expected: сборка без ошибок, все тесты Core зелёные.

- [ ] **Step 4: Ручная приёмка (критерии из спецификации)**

Закрыть запущенную копию, `dotnet run --project src/ChatSnippets.App`, пройти по списку:
- [ ] Панель 80 DIP, плитки 64×64, подписи хоткеев под иконками без плашки; вид совпадает с макетом.
- [ ] «+» → диалог, сохранение: плитка появилась, хоткей по умолчанию `Ctrl+Alt+1`.
- [ ] В VS Code курсор в поле чата Claude Code → клик по плитке вставляет текст; через `Ctrl+Alt+1` — тоже; прежний буфер обмена цел (Ctrl+V в блокноте вставляет старое).
- [ ] Активен браузер → клик/хоткей ничего не вставляют, плитка мигает красным.
- [ ] Панель после вставки НЕ сворачивается; сворачивается через 1,5 с ухода мыши, повторным кликом по флажку, по minus; pin удерживает.
- [ ] При вставке по хоткею флажок вспыхивает зелёным (в том числе когда панель свёрнута).
- [ ] Занять сочетание системным (например `Win+D`) → «занято другой программой»; одинаковое у двух иконок → «уже назначено».
- [ ] Перетащить панель за шапку на левую половину → прилипла слева; перезапуск программы — сторона, позиция, pin, список иконок сохранены.
- [ ] Удалить файл картинки в `%AppData%\ChatSnippets\icons` → плитка серая, программа не падает.
- [ ] Порвать `config.json` вручную → программа стартует пустой, рядом появился `config.json.bad`.
- [ ] Запустить второй экземпляр → тихо закрывается.
- [ ] Масштаб Windows 125% и 150%: панель не обрезается, флажок на краю.
- [ ] Второй монитор: панель на правом краю левого монитора не видна на соседнем, когда свёрнута.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Сборка приложения: вставка по клику и хоткею, сохранение, README" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Self-Review (проведено при составлении)

- **Покрытие спецификации:** выезд/флажок/таймер/pin/minus — Task 8; не сворачиваться после вставки — Task 10 (`ExecuteAsync` не трогает `_dock`); проверка `Code.exe` и буфер — Tasks 5–6; хоткеи и конфликты — Tasks 2, 3, 6, 9; хранение и битый конфиг — Task 3; размеры/цвета/состояния — Task 7; диалог — Task 9; одиночный экземпляр — Task 10.
- **Согласованность типов:** `Hotkey`, `DockSide`, `Snippet`, `WindowSettings.Top` (смещение от верха рабочей области, пиксели), `DockLogic.*`, `IKeySender.SendCtrlVAsync`, `PanelViewModel.DeleteRequested/MoveRequested` — имена совпадают между задачами.
- **Известные упрощения (задокументированы в спецификации):** буфер восстанавливается только как текст; приложение без значка в трее (выход через шестерёнку).
