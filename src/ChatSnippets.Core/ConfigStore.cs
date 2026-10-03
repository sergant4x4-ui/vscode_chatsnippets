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
