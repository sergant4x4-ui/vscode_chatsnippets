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
