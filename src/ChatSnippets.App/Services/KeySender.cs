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
