namespace WinDND.Core.Models;

public sealed class ScheduleRule
{
    public bool Enabled { get; set; } = true;

    public string Action { get; set; } = "mute";

    public string Cron { get; set; } = "0 22 * * *";

    public ScheduleRule Clone() => new()
    {
        Enabled = Enabled,
        Action = Action,
        Cron = Cron
    };
}
