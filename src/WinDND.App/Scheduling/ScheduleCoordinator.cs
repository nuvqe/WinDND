using WinDND.Audio;
using WinDND.Core.Models;
using WinDND.Core.Scheduling;
using WinDND.Infrastructure;

namespace WinDND.Scheduling;

internal sealed class ScheduleCoordinator : IDisposable
{
    private static readonly TimeSpan MaximumTimerDelay = TimeSpan.FromDays(1);
    private readonly object _sync = new();
    private readonly CronScheduleService _cronService;
    private readonly AudioController _audio;
    private readonly IAppLogger _logger;
    private readonly Action _stateChanged;
    private readonly System.Threading.Timer _timer;
    private AppSettings _settings = new();
    private ScheduledOccurrence? _scheduledOccurrence;
    private int _executing;
    private bool _disposed;

    public ScheduleCoordinator(
        CronScheduleService cronService,
        AudioController audio,
        IAppLogger logger,
        Action stateChanged)
    {
        _cronService = cronService;
        _audio = audio;
        _logger = logger;
        _stateChanged = stateChanged;
        _timer = new System.Threading.Timer(OnTimer);
    }

    public void Configure(AppSettings settings, bool reconcileNow)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            _settings = settings.Clone();
            ScheduleNextLocked(DateTimeOffset.Now);
        }

        if (reconcileNow && settings.ScheduleEnabled)
        {
            ReconcileNow();
        }
    }

    public void ReconcileNow()
    {
        AppSettings snapshot;
        lock (_sync)
        {
            if (_disposed || !_settings.ScheduleEnabled)
            {
                return;
            }

            snapshot = _settings.Clone();
        }

        ScheduledOccurrence? latest = _cronService.GetMostRecent(
            snapshot.Rules,
            DateTimeOffset.Now,
            TimeZoneInfo.Local);

        if (latest is not null)
        {
            Execute(latest, "Reconciled schedule state");
        }

        lock (_sync)
        {
            if (!_disposed)
            {
                ScheduleNextLocked(DateTimeOffset.Now);
            }
        }
    }

    private void OnTimer(object? state)
    {
        if (Interlocked.Exchange(ref _executing, 1) != 0)
        {
            return;
        }

        try
        {
            AppSettings snapshot;
            ScheduledOccurrence? scheduled;
            lock (_sync)
            {
                if (_disposed || !_settings.ScheduleEnabled)
                {
                    return;
                }

                snapshot = _settings.Clone();
                scheduled = _scheduledOccurrence;
            }

            DateTimeOffset now = DateTimeOffset.Now;
            if (scheduled is not null && scheduled.At <= now.AddMilliseconds(500))
            {
                ScheduledOccurrence occurrence = now - scheduled.At > TimeSpan.FromMinutes(2)
                    ? _cronService.GetMostRecent(snapshot.Rules, now, TimeZoneInfo.Local) ?? scheduled
                    : scheduled;
                Execute(occurrence, "Scheduled rule applied");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("The scheduler could not apply a rule.", ex, 2200);
        }
        finally
        {
            lock (_sync)
            {
                if (!_disposed)
                {
                    ScheduleNextLocked(DateTimeOffset.Now);
                }
            }

            Interlocked.Exchange(ref _executing, 0);
        }
    }

    private void Execute(ScheduledOccurrence occurrence, string reason)
    {
        if (!SoundAction.TryParse(occurrence.Rule.Action, out SoundAction action))
        {
            return;
        }

        try
        {
            _audio.Apply(action);
            _logger.Information(
                $"{reason}: action '{action}', cron '{occurrence.Rule.Cron}', occurrence {occurrence.At:O}.",
                1100);
            _stateChanged();
        }
        catch (Exception ex)
        {
            _logger.Error(
                $"Could not apply action '{action}' for cron '{occurrence.Rule.Cron}'.",
                ex,
                2201);
        }
    }

    private void ScheduleNextLocked(DateTimeOffset after)
    {
        if (!_settings.ScheduleEnabled)
        {
            _scheduledOccurrence = null;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        _scheduledOccurrence = _cronService.GetNext(_settings.Rules, after, TimeZoneInfo.Local);
        if (_scheduledOccurrence is null)
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        TimeSpan delay = _scheduledOccurrence.At - DateTimeOffset.Now + TimeSpan.FromMilliseconds(250);
        delay = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        delay = delay > MaximumTimerDelay ? MaximumTimerDelay : delay;
        _timer.Change(delay, Timeout.InfiniteTimeSpan);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Dispose();
        }
    }
}
