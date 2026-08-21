namespace WinDND.Core.Models;

public sealed class AppSettings
{
    public bool ScheduleEnabled { get; set; } = true;

    public List<ScheduleRule> Rules { get; set; } = [];

    public HardwareControllerSettings Hardware { get; set; } = new();

    public AppSettings Clone() => new()
    {
        ScheduleEnabled = ScheduleEnabled,
        Rules = Rules.Select(rule => rule.Clone()).ToList(),
        Hardware = Hardware.Clone()
    };
}
