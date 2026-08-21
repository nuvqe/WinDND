using WinDND.Core.Models;

namespace WinDND.Core.Scheduling;

public sealed record ScheduledOccurrence(int RuleIndex, ScheduleRule Rule, DateTimeOffset At);
