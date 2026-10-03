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
        string text;
        try { text = File.ReadAllText(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new AppConfig(); }
        if (string.IsNullOrWhiteSpace(text)) return new AppConfig();

        try { return Normalize(JsonSerializer.Deserialize<AppConfig>(text, Options)); }
        catch (JsonException)
        {
            try { File.Move(path, path + ".bad", overwrite: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return new AppConfig();
        }
    }

    /// <summary>Синтаксически верный JSON с null-ами или дублями не должен ронять программу.</summary>
    static AppConfig Normalize(AppConfig? config)
    {
        config ??= new AppConfig();
        config.Window ??= new WindowSettings();
        config.Snippets = (config.Snippets ?? new List<Snippet>()).Where(s => s is not null).ToList();
        var seen = new HashSet<string>();
        foreach (var s in config.Snippets)
        {
            s.Text ??= "";
            if (string.IsNullOrWhiteSpace(s.Id) || !seen.Add(s.Id))
            {
                s.Id = Guid.NewGuid().ToString("N");
                seen.Add(s.Id);
            }
        }
        return config;
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
