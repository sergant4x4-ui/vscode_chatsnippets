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
