using System.Runtime.InteropServices;
using ChatSnippets.App.Interop;
using ChatSnippets.Core;

namespace ChatSnippets.App.Services;

internal sealed class KeySender : IKeySender
{
    static readonly int[] Modifiers = { Native.VK_SHIFT, Native.VK_CONTROL, Native.VK_MENU, Native.VK_LWIN, Native.VK_RWIN };

    public Task SendCtrlAsync(int vk) => SendAsync(new[]
    {
        Key(Native.VK_CONTROL, false), Key(vk, false),
        Key(vk, true), Key(Native.VK_CONTROL, true),
    });

    public Task SendKeyAsync(int vk) => SendAsync(new[] { Key(vk, false), Key(vk, true) });

    static async Task SendAsync(Native.INPUT[] inputs)
    {
        // Хоткей вроде Ctrl+Alt+1 ещё зажат: без ожидания получилось бы Ctrl+Alt+клавиша.
        var deadline = DateTime.UtcNow.AddSeconds(1);
        while (AnyModifierDown() && DateTime.UtcNow < deadline)
            await Task.Delay(15);

        Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
    }

    static bool AnyModifierDown() => Modifiers.Any(vk => (Native.GetAsyncKeyState(vk) & 0x8000) != 0);

    static Native.INPUT Key(int vk, bool up) => new()
    {
        type = Native.INPUT_KEYBOARD,
        u = new Native.INPUTUNION { ki = new Native.KEYBDINPUT { wVk = (ushort)vk, dwFlags = up ? Native.KEYEVENTF_KEYUP : 0 } },
    };
}
