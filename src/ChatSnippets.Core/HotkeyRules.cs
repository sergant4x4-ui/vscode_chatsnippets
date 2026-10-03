namespace ChatSnippets.Core;

public static class HotkeyRules
{
    public static Snippet? FindConflict(IEnumerable<Snippet> all, string? excludeId, Hotkey candidate) =>
        all.FirstOrDefault(s => s.Id != excludeId && Hotkey.TryParse(s.Hotkey, out var h) && h == candidate);

    /// <summary>Первое свободное Ctrl+Alt+1..9,0, затем Q,W,E,R,T,Y,U,I,O,P; null, если всё занято.</summary>
    public static Hotkey? NextFreeDefault(IEnumerable<Snippet> all)
    {
        var list = all.ToList();
        foreach (var key in "1234567890QWERTYUIOP")
        {
            Hotkey.TryParse($"Ctrl+Alt+{key}", out var h);
            if (FindConflict(list, null, h) is null) return h;
        }
        return null;
    }
}
