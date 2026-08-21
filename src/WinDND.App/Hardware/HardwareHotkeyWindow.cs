using System.ComponentModel;
using System.Runtime.InteropServices;
using WinDND.Core.Models;

namespace WinDND.Hardware;

internal sealed class HardwareHotkeyWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF13 = 0x7C;
    private const uint VkF14 = 0x7D;
    private const int LeftHotkeyId = 0x4D11;
    private const int RightHotkeyId = 0x4D12;
    private bool _registered;
    private bool _disposed;

    public HardwareHotkeyWindow()
    {
        CreateHandle(new CreateParams { Caption = "WinDND hardware hotkeys" });
    }

    public event Action<HardwareButtonAction>? ButtonPressed;

    public void Configure(HardwareControllerSettings settings)
    {
        Unregister();
        if (!settings.Enabled)
        {
            return;
        }

        if (!RegisterHotKey(Handle, LeftHotkeyId, ModNoRepeat, VkF13))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "F13 could not be registered for the hardware controller.");
        }

        if (!RegisterHotKey(Handle, RightHotkeyId, ModNoRepeat, VkF14))
        {
            _ = UnregisterHotKey(Handle, LeftHotkeyId);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "F14 could not be registered for the hardware controller.");
        }

        LeftAction = settings.LeftButtonAction;
        RightAction = settings.RightButtonAction;
        _registered = true;
    }

    private HardwareButtonAction LeftAction { get; set; }

    private HardwareButtonAction RightAction { get; set; }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey)
        {
            HardwareButtonAction action = message.WParam.ToInt32() switch
            {
                LeftHotkeyId => LeftAction,
                RightHotkeyId => RightAction,
                _ => HardwareButtonAction.None
            };
            if (action != HardwareButtonAction.None)
            {
                ButtonPressed?.Invoke(action);
            }
        }

        base.WndProc(ref message);
    }

    private void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        _ = UnregisterHotKey(Handle, LeftHotkeyId);
        _ = UnregisterHotKey(Handle, RightHotkeyId);
        _registered = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Unregister();
        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
