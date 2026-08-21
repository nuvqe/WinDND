using WinDND.Core.Models;
using WinDND.Core.Scheduling;

namespace WinDND.Core.Tests;

[TestClass]
public sealed class CronScheduleServiceTests
{
    private readonly CronScheduleService _service = new();

    [TestMethod]
    public void Describe_ReturnsHumanReadableText()
    {
        string description = _service.Describe("0 22 * * 1-5");

        StringAssert.Contains(description, "22:00");
    }

    [TestMethod]
    public void Validate_RejectsInvalidCron()
    {
        ScheduleRule rule = new() { Action = "mute", Cron = "not a cron" };

        Assert.IsFalse(_service.TryValidate(rule, out string error));
        Assert.IsFalse(string.IsNullOrWhiteSpace(error));
    }

    [TestMethod]
    public void GetNext_ReturnsNextLocalOccurrence()
    {
        ScheduleRule rule = new() { Action = "25", Cron = "30 8 * * 1-5" };
        DateTimeOffset mondayAtEight = new(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);

        ScheduledOccurrence? result = _service.GetNext([rule], mondayAtEight, TimeZoneInfo.Utc);

        Assert.IsNotNull(result);
        Assert.AreEqual(new DateTimeOffset(2026, 8, 24, 8, 30, 0, TimeSpan.Zero), result.At);
    }

    [TestMethod]
    public void GetNext_WhenRulesCoincide_LaterRowWins()
    {
        ScheduleRule mute = new() { Action = "mute", Cron = "0 22 * * *" };
        ScheduleRule volume = new() { Action = "10", Cron = "0 22 * * *" };
        DateTimeOffset before = new(2026, 8, 21, 21, 0, 0, TimeSpan.Zero);

        ScheduledOccurrence? result = _service.GetNext([mute, volume], before, TimeZoneInfo.Utc);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.RuleIndex);
        Assert.AreEqual("10", result.Rule.Action);
    }

    [TestMethod]
    public void GetMostRecent_FindsLastStateSettingRule()
    {
        ScheduleRule mute = new() { Action = "mute", Cron = "0 22 * * *" };
        ScheduleRule unmute = new() { Action = "unmute", Cron = "0 7 * * *" };
        DateTimeOffset morning = new(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);

        ScheduledOccurrence? result = _service.GetMostRecent([mute, unmute], morning, TimeZoneInfo.Utc);

        Assert.IsNotNull(result);
        Assert.AreEqual("unmute", result.Rule.Action);
        Assert.AreEqual(new DateTimeOffset(2026, 8, 21, 7, 0, 0, TimeSpan.Zero), result.At);
    }
}
