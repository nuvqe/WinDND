using System.Runtime.InteropServices;
using WinDND.Core.Models;

namespace WinDND.Hardware;

internal sealed class HardwareHotkeyWindow : NativeWindow, IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmHardwareButton = 0x8001;
    private const uint VkF13 = 0x7C;
    private const uint VkF14 = 0x7D;
    private readonly LowLevelKeyboardProc _keyboardProc;
    private readonly HashSet<uint> _pressedKeys = [];
    private IntPtr _hook;
    private bool _disposed;

    public HardwareHotkeyWindow()
    {
        _keyboardProc = KeyboardHookCallback;
        CreateHandle(new CreateParams { Caption = "WinDND hardware hotkeys" });
    }

    public event Action<HardwareButtonAction>? ButtonPressed;

    public void Configure(HardwareControllerSettings settings)
    {
        UninstallHook();
        if (!settings.Enabled || settings.KeyRouting == HardwareKeyRouting.ForegroundApplicationOnly)
        {
            return;
        }

        LeftAction = settings.LeftButtonAction;
        RightAction = settings.RightButtonAction;
        KeyRouting = settings.KeyRouting;
        _hook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "F13/F14 key monitoring could not be enabled for the hardware controller.");
        }
    }

    private HardwareButtonAction LeftAction { get; set; }

    private HardwareButtonAction RightAction { get; set; }

    private HardwareKeyRouting KeyRouting { get; set; }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHardwareButton)
        {
            HardwareButtonAction action = message.WParam.ToInt32() switch
            {
                (int)VkF13 => LeftAction,
                (int)VkF14 => RightAction,
                _ => HardwareButtonAction.None
            };
            if (action != HardwareButtonAction.None)
            {
                ButtonPressed?.Invoke(action);
            }
        }

        base.WndProc(ref message);
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            int messageId = message.ToInt32();
            bool isKeyDown = messageId is WmKeyDown or WmSysKeyDown;
            bool isKeyUp = messageId is WmKeyUp or WmSysKeyUp;
            if (isKeyDown || isKeyUp)
            {
                uint virtualKey = unchecked((uint)Marshal.ReadInt32(data));
                if (virtualKey is VkF13 or VkF14)
                {
                    if (isKeyDown && _pressedKeys.Add(virtualKey))
                    {
                        _ = PostMessage(Handle, WmHardwareButton, (IntPtr)virtualKey, IntPtr.Zero);
                    }
                    else if (isKeyUp)
                    {
                        _pressedKeys.Remove(virtualKey);
                    }

                    if (KeyRouting == HardwareKeyRouting.WinDndOnly)
                    {
                        return (IntPtr)1;
                    }
                }
            }
        }

        return CallNextHookEx(_hook, code, message, data);
    }

    private void UninstallHook()
    {
        _pressedKeys.Clear();
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        _ = UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UninstallHook();
        DestroyHandle();
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookId,
        LowLevelKeyboardProc callback,
        IntPtr module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
