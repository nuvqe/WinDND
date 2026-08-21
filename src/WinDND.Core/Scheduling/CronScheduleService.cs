using CronExpressionDescriptor;
using Cronos;
using WinDND.Core.Models;

namespace WinDND.Core.Scheduling;

public sealed class CronScheduleService
{
    private static readonly TimeSpan[] LookbackWindows =
    [
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(32),
        TimeSpan.FromDays(370),
        TimeSpan.FromDays(366 * 5)
    ];

    public string Describe(string expression)
    {
        _ = Parse(expression);
        return ExpressionDescriptor.GetDescription(expression, new Options
        {
            ThrowExceptionOnParseError = true,
            Use24HourTimeFormat = true,
            Verbose = true
        });
    }

    public bool TryValidate(ScheduleRule rule, out string error)
    {
        if (!SoundAction.TryParse(rule.Action, out _))
        {
            error = "Action must be mute, unmute, or a volume from 0 to 100.";
            return false;
        }

        try
        {
            _ = Parse(rule.Cron);
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is CronFormatException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    public ScheduledOccurrence? GetNext(
        IReadOnlyList<ScheduleRule> rules,
        DateTimeOffset after,
        TimeZoneInfo timeZone)
    {
        ScheduledOccurrence? result = null;

        for (int index = 0; index < rules.Count; index++)
        {
            ScheduleRule rule = rules[index];
            if (!rule.Enabled || !TryValidate(rule, out _))
            {
                continue;
            }

            DateTimeOffset? next = Parse(rule.Cron).GetNextOccurrence(after, timeZone, inclusive: false);
            if (next is null)
            {
                continue;
            }

            if (result is null || next < result.At || (next == result.At && index > result.RuleIndex))
            {
                result = new ScheduledOccurrence(index, rule, next.Value);
            }
        }

        return result;
    }

    public ScheduledOccurrence? GetMostRecent(
        IReadOnlyList<ScheduleRule> rules,
        DateTimeOffset now,
        TimeZoneInfo timeZone)
    {
        foreach (TimeSpan lookback in LookbackWindows)
        {
            ScheduledOccurrence? result = GetMostRecentWithin(rules, now - lookback, now, timeZone);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private static ScheduledOccurrence? GetMostRecentWithin(
        IReadOnlyList<ScheduleRule> rules,
        DateTimeOffset start,
        DateTimeOffset end,
        TimeZoneInfo timeZone)
    {
        ScheduledOccurrence? result = null;

        for (int index = 0; index < rules.Count; index++)
        {
            ScheduleRule rule = rules[index];
            if (!rule.Enabled || !SoundAction.TryParse(rule.Action, out _))
            {
                continue;
            }

            CronExpression expression;
            try
            {
                expression = Parse(rule.Cron);
            }
            catch (CronFormatException)
            {
                continue;
            }

            DateTimeOffset cursor = start;
            DateTimeOffset? latest = null;
            while (true)
            {
                DateTimeOffset? next = expression.GetNextOccurrence(cursor, timeZone, inclusive: false);
                if (next is null || next > end)
                {
                    break;
                }

                latest = next;
                cursor = next.Value;
            }

            if (latest is not null
                && (result is null || latest > result.At || (latest == result.At && index > result.RuleIndex)))
            {
                result = new ScheduledOccurrence(index, rule, latest.Value);
            }
        }

        return result;
    }

    private static CronExpression Parse(string expression) =>
        CronExpression.Parse(expression.Trim(), CronFormat.Standard);
}
