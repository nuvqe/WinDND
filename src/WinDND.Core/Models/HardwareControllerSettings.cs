namespace WinDND.Core.Models;

public sealed class HardwareControllerSettings
{
    public bool Enabled { get; set; }

    public MicrophoneScope MicrophoneScope { get; set; } = MicrophoneScope.AllActive;

    public HardwareKeyRouting KeyRouting { get; set; } = HardwareKeyRouting.WinDndOnly;

    public HardwareButtonAction LeftButtonAction { get; set; } = HardwareButtonAction.ToggleMicrophone;

    public HardwareButtonAction RightButtonAction { get; set; } = HardwareButtonAction.ToggleOutput;

    public HardwareIndicator LeftIndicator { get; set; } = HardwareIndicator.Microphone;

    public HardwareIndicator RightIndicator { get; set; } = HardwareIndicator.Microphone;

    public string MutedColor { get; set; } = "#FF0000";

    public string UnmutedColor { get; set; } = "#0000FF";

    public bool ConfigureKeys { get; set; } = true;

    public int VendorId { get; set; } = 0x8089;

    public List<int> ProductIds { get; set; } = [0x0002, 0x0003, 0x000C];

    public HardwareControllerSettings Clone() => new()
    {
        Enabled = Enabled,
        MicrophoneScope = MicrophoneScope,
        KeyRouting = KeyRouting,
        LeftButtonAction = LeftButtonAction,
        RightButtonAction = RightButtonAction,
        LeftIndicator = LeftIndicator,
        RightIndicator = RightIndicator,
        MutedColor = MutedColor,
        UnmutedColor = UnmutedColor,
        ConfigureKeys = ConfigureKeys,
        VendorId = VendorId,
        ProductIds = [.. ProductIds]
    };
}

public enum MicrophoneScope
{
    AllActive,
    DefaultCommunications
}

public enum HardwareKeyRouting
{
    WinDndOnly,
    ForegroundApplicationOnly,
    WinDndAndForegroundApplication
}

public enum HardwareButtonAction
{
    None,
    ToggleMicrophone,
    ToggleOutput
}

public enum HardwareIndicator
{
    None,
    Microphone,
    Output
}
