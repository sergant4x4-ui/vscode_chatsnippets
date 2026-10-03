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

    [Theory]
    [InlineData("{\"Snippets\":null,\"Window\":null}")]
    [InlineData("{\"Snippets\":[null],\"Window\":{}}")]
    [InlineData("null")]
    public void Load_ValidJsonWithNulls_NormalizesInsteadOfCrashing(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, json);
        var cfg = new ConfigStore(ConfigPath).Load();
        Assert.NotNull(cfg.Snippets);
        Assert.NotNull(cfg.Window);
        Assert.DoesNotContain(null, cfg.Snippets);
    }

    [Fact]
    public void Load_RepairsEmptyAndDuplicateIds()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, "{\"Snippets\":[{\"Id\":\"\",\"Text\":\"a\"},{\"Id\":\"x\",\"Text\":\"b\"},{\"Id\":\"x\",\"Text\":\"c\"}]}");
        var ids = new ConfigStore(ConfigPath).Load().Snippets.Select(s => s.Id).ToList();
        Assert.Equal(3, ids.Distinct().Count());
        Assert.DoesNotContain("", ids);
    }
}
