using System.IO;

namespace ChatSnippets.App.Services;

/// <summary>Простой журнал в %AppData%\ChatSnippets\log.txt: чтобы «пропало окно» не было загадкой.</summary>
internal static class AppLog
{
    static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChatSnippets", "log.txt");
    static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 200_000) info.Delete();
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* журнал не должен ронять программу */ }
    }
}
