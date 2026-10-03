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
