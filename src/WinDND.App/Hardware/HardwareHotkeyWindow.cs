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
    private const int WmInput = 0x00FF;
    private const int WmTimer = 0x0113;
    private const int WmHardwareButton = 0x8001;
    private const int WmForegroundChanged = 0x8002;
    private const int ForegroundRehookTimerId = 0x4D13;
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint RidInput = 0x10000003;
    private const uint RimTypeKeyboard = 1;
    private const uint RidevRemove = 0x00000001;
    private const uint RidevInputSink = 0x00000100;
    private const ushort HidUsagePageGeneric = 0x01;
    private const ushort HidUsageGenericKeyboard = 0x06;
    private const uint VkF13 = 0x7C;
    private const uint VkF14 = 0x7D;
    private const long DuplicatePressWindowMilliseconds = 150;
    private const uint ForegroundRehookDelayMilliseconds = 200;
    private readonly LowLevelKeyboardProc _keyboardProc;
    private readonly WinEventProc _winEventProc;
    private readonly HashSet<uint> _pressedKeys = [];
    private readonly Dictionary<uint, long> _lastPressTimes = [];
    private IntPtr _hook;
    private IntPtr _foregroundHook;
    private bool _rawInputRegistered;
    private bool _suppressKeys;
    private bool _disposed;

    public HardwareHotkeyWindow()
    {
        _keyboardProc = KeyboardHookCallback;
        _winEventProc = ForegroundChangedCallback;
        CreateHandle(new CreateParams { Caption = "WinDND hardware hotkeys" });
    }

    public event Action<HardwareButtonAction>? ButtonPressed;

    public void Configure(HardwareControllerSettings settings)
    {
        UnregisterInput();
        if (!settings.Enabled || settings.KeyRouting == HardwareKeyRouting.ForegroundApplicationOnly)
        {
            return;
        }

        LeftAction = settings.LeftButtonAction;
        RightAction = settings.RightButtonAction;
        _suppressKeys = settings.KeyRouting == HardwareKeyRouting.WinDndOnly;
        RegisterRawKeyboardInput();
        if (!TryInstallKeyboardHook())
        {
            UnregisterInput();
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "F13/F14 key monitoring could not be enabled for the hardware controller.");
        }

        _foregroundHook = SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            IntPtr.Zero,
            _winEventProc,
            0,
            0,
            WinEventOutOfContext);
        if (_foregroundHook == IntPtr.Zero)
        {
            UnregisterInput();
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "Foreground application monitoring could not be enabled for the hardware controller.");
        }
    }

    private HardwareButtonAction LeftAction { get; set; }

    private HardwareButtonAction RightAction { get; set; }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmInput)
        {
            HandleRawKeyboardInput(message.LParam);
        }
        else if (message.Msg == WmForegroundChanged)
        {
            _ = KillTimer(Handle, (nuint)ForegroundRehookTimerId);
            _ = SetTimer(
                Handle,
                (nuint)ForegroundRehookTimerId,
                ForegroundRehookDelayMilliseconds,
                IntPtr.Zero);
        }
        else if (message.Msg == WmTimer && message.WParam.ToInt32() == ForegroundRehookTimerId)
        {
            _ = KillTimer(Handle, (nuint)ForegroundRehookTimerId);
            _ = TryInstallKeyboardHook();
        }
        else if (message.Msg == WmHardwareButton)
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

    private void HandleRawKeyboardInput(IntPtr inputHandle)
    {
        uint size = (uint)Marshal.SizeOf<RawInput>();
        uint copied = GetRawInputData(
            inputHandle,
            RidInput,
            out RawInput input,
            ref size,
            (uint)Marshal.SizeOf<RawInputHeader>());
        if (copied is 0 or uint.MaxValue || input.Header.Type != RimTypeKeyboard)
        {
            return;
        }

        uint virtualKey = input.Data.Keyboard.VirtualKey;
        if (virtualKey is not (VkF13 or VkF14))
        {
            return;
        }

        bool isKeyDown = input.Data.Keyboard.Message is WmKeyDown or WmSysKeyDown;
        bool isKeyUp = input.Data.Keyboard.Message is WmKeyUp or WmSysKeyUp;
        if (isKeyDown && _pressedKeys.Add(virtualKey))
        {
            QueueButtonPress(virtualKey);
        }
        else if (isKeyUp)
        {
            _pressedKeys.Remove(virtualKey);
        }
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            int messageId = message.ToInt32();
            if (messageId is WmKeyDown or WmKeyUp or WmSysKeyDown or WmSysKeyUp)
            {
                uint virtualKey = unchecked((uint)Marshal.ReadInt32(data));
                if (virtualKey is VkF13 or VkF14)
                {
                    bool isKeyDown = messageId is WmKeyDown or WmSysKeyDown;
                    if (isKeyDown && _pressedKeys.Add(virtualKey))
                    {
                        QueueButtonPress(virtualKey);
                    }
                    else if (!isKeyDown)
                    {
                        _pressedKeys.Remove(virtualKey);
                    }

                    if (_suppressKeys)
                    {
                        return (IntPtr)1;
                    }
                }
            }
        }

        return CallNextHookEx(_hook, code, message, data);
    }

    private bool TryInstallKeyboardHook()
    {
        IntPtr replacement = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, GetModuleHandle(null), 0);
        if (replacement == IntPtr.Zero)
        {
            return false;
        }

        IntPtr previous = _hook;
        _hook = replacement;
        if (previous != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(previous);
        }

        return true;
    }

    private void ForegroundChangedCallback(
        IntPtr hook,
        uint eventId,
        IntPtr window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        _ = PostMessage(Handle, WmForegroundChanged, IntPtr.Zero, IntPtr.Zero);
    }

    private void QueueButtonPress(uint virtualKey)
    {
        long now = Environment.TickCount64;
        if (_lastPressTimes.TryGetValue(virtualKey, out long lastPress) &&
            now - lastPress < DuplicatePressWindowMilliseconds)
        {
            return;
        }

        _lastPressTimes[virtualKey] = now;
        _ = PostMessage(Handle, WmHardwareButton, (IntPtr)virtualKey, IntPtr.Zero);
    }

    private void RegisterRawKeyboardInput()
    {
        RawInputDevice[] devices =
        [
            new()
            {
                UsagePage = HidUsagePageGeneric,
                Usage = HidUsageGenericKeyboard,
                Flags = RidevInputSink,
                Target = Handle
            }
        ];
        if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "Background F13/F14 monitoring could not be enabled for the hardware controller.");
        }

        _rawInputRegistered = true;
    }

    private void UnregisterInput()
    {
        _pressedKeys.Clear();
        _lastPressTimes.Clear();
        _ = KillTimer(Handle, (nuint)ForegroundRehookTimerId);
        if (_foregroundHook != IntPtr.Zero)
        {
            _ = UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }

        if (_hook != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        if (_rawInputRegistered)
        {
            RawInputDevice[] devices =
            [
                new()
                {
                    UsagePage = HidUsagePageGeneric,
                    Usage = HidUsageGenericKeyboard,
                    Flags = RidevRemove,
                    Target = IntPtr.Zero
                }
            ];
            _ = RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RawInputDevice>());
            _rawInputRegistered = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterInput();
        DestroyHandle();
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr message, IntPtr data);

    private delegate void WinEventProc(
        IntPtr hook,
        uint eventId,
        IntPtr window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VirtualKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct RawInputData
    {
        [FieldOffset(0)]
        public RawKeyboard Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInput
    {
        public RawInputHeader Header;
        public RawInputData Data;
    }

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
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr eventHookModule,
        WinEventProc callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nuint SetTimer(IntPtr window, nuint timerId, uint interval, IntPtr callback);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool KillTimer(IntPtr window, nuint timerId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] devices,
        uint numberOfDevices,
        uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        out RawInput data,
        ref uint size,
        uint headerSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
