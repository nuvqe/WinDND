using Microsoft.Win32;
using WinDND.Audio;
using WinDND.Core.Models;
using WinDND.Core.Scheduling;
using WinDND.Hardware;
using WinDND.Infrastructure;
using WinDND.Scheduling;

namespace WinDND.UI;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly IAppLogger _logger;
    private readonly SettingsStore _settingsStore;
    private readonly CronScheduleService _cronService = new();
    private readonly AudioController _audio = new();
    private readonly ScheduleCoordinator _scheduler;
    private readonly AudioDeviceMonitor? _audioDeviceMonitor;
    private readonly HardwareHotkeyWindow _hardwareHotkeys = new();
    private readonly SayoO2Controller _hardwareController;
    private readonly System.Windows.Forms.Timer _hardwareTimer = new() { Interval = 1000 };
    private readonly NotifyIcon _notifyIcon = new();
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _muteItem = new();
    private readonly ToolStripMenuItem _scheduleItem = new();
    private readonly Control _dispatcher = new();
    private AppSettings _settings;
    private SettingsForm? _settingsForm;
    private Icon? _currentIcon;
    private bool _exiting;
    private bool _hardwareRefreshErrorLogged;

    public TrayApplicationContext(IAppLogger logger)
    {
        _logger = logger;
        _settingsStore = new SettingsStore(logger);
        _settings = _settingsStore.Load();
        _hardwareController = new SayoO2Controller(logger);
        _dispatcher.CreateControl();

        BuildMenu();
        _notifyIcon.ContextMenuStrip = _menu;
        _notifyIcon.Text = "WinDND";
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();
        _notifyIcon.Visible = true;

        _scheduler = new ScheduleCoordinator(_cronService, _audio, logger, QueueTrayRefresh);
        _scheduler.Configure(_settings, reconcileNow: _settings.ScheduleEnabled);
        _hardwareHotkeys.ButtonPressed += HandleHardwareButton;
        _hardwareTimer.Tick += (_, _) => RefreshHardware();
        ConfigureHardware(force: true);
        try
        {
            _audioDeviceMonitor = new AudioDeviceMonitor(() =>
            {
                _scheduler.ReconcileNow();
                QueueHardwareRefresh();
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Audio-device change monitoring is unavailable.", ex, 2202);
        }
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        UpdateTrayPresentation();
        _logger.Information("WinDND started.", 1000);
    }

    private void BuildMenu()
    {
        _muteItem.Click += (_, _) => ToggleManualMute();
        _scheduleItem.Click += (_, _) => ToggleSchedule();
        ToolStripMenuItem settingsItem = new("Settings…");
        ToolStripMenuItem exitItem = new("Exit");
        settingsItem.Click += (_, _) => ShowSettings();
        exitItem.Click += (_, _) => ExitThread();

        _menu.Items.AddRange(
        [
            _muteItem,
            new ToolStripSeparator(),
            _scheduleItem,
            settingsItem,
            new ToolStripSeparator(),
            exitItem
        ]);
        _menu.Opening += (_, _) => UpdateTrayPresentation();
    }

    private void ToggleManualMute()
    {
        try
        {
            bool muted = _audio.ToggleManualMute();
            _logger.Information(muted ? "Sound manually muted." : "Sound manually restored.", 1200);
            UpdateTrayPresentation();
        }
        catch (Exception ex)
        {
            ShowError("Windows audio could not be changed.", ex);
        }
    }

    private void ToggleSchedule()
    {
        _settings.ScheduleEnabled = !_settings.ScheduleEnabled;
        try
        {
            _settingsStore.Save(_settings);
            _scheduler.Configure(_settings, reconcileNow: _settings.ScheduleEnabled);
            _logger.Information($"Schedule {(_settings.ScheduleEnabled ? "enabled" : "disabled")}.", 1300);
            UpdateTrayPresentation();
        }
        catch (Exception ex)
        {
            _settings.ScheduleEnabled = !_settings.ScheduleEnabled;
            ShowError("The schedule setting could not be saved.", ex);
        }
    }

    private void ShowSettings()
    {
        if (_settingsForm is not null)
        {
            _settingsForm.Activate();
            return;
        }

        SettingsForm form = new(_settings.Clone(), _cronService, _hardwareController.Status);
        _settingsForm = form;
        form.FormClosed += (_, _) =>
        {
            if (form.DialogResult == DialogResult.OK)
            {
                SaveSettings(form.Result);
            }

            _settingsForm = null;
            form.Dispose();
        };
        form.Show();
        form.Activate();
    }

    private void SaveSettings(AppSettings updated)
    {
        try
        {
            bool newlyEnabled = !_settings.ScheduleEnabled && updated.ScheduleEnabled;
            _settingsStore.Save(updated);
            _settings = updated.Clone();
            _scheduler.Configure(_settings, reconcileNow: newlyEnabled);
            ConfigureHardware(force: true);
            _logger.Information($"Settings saved with {_settings.Rules.Count} rule(s).", 1301);
            UpdateTrayPresentation();
        }
        catch (Exception ex)
        {
            ShowError("Settings could not be saved.", ex);
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs args)
    {
        if (args.Mode == PowerModes.Resume)
        {
            ThreadPool.QueueUserWorkItem(_ => _scheduler.ReconcileNow());
        }
    }

    private void QueueTrayRefresh()
    {
        if (_dispatcher.IsDisposed || !_dispatcher.IsHandleCreated)
        {
            return;
        }

        try
        {
            _dispatcher.BeginInvoke(UpdateTrayPresentation);
        }
        catch (InvalidOperationException)
        {
            // The application is exiting.
        }
    }

    private void QueueHardwareRefresh()
    {
        if (_dispatcher.IsDisposed || !_dispatcher.IsHandleCreated)
        {
            return;
        }

        try
        {
            _dispatcher.BeginInvoke(() => RefreshHardware(force: true));
        }
        catch (InvalidOperationException)
        {
            // The application is exiting.
        }
    }

    private void ConfigureHardware(bool force)
    {
        try
        {
            _hardwareHotkeys.Configure(_settings.Hardware);
            _hardwareTimer.Enabled = _settings.Hardware.Enabled;
            RefreshHardware(force);
        }
        catch (Exception ex)
        {
            _hardwareTimer.Stop();
            ShowError("The hardware controller could not be enabled.", ex);
        }
    }

    private void HandleHardwareButton(HardwareButtonAction action)
    {
        switch (action)
        {
            case HardwareButtonAction.ToggleMicrophone:
                ToggleMicrophoneMute();
                break;
            case HardwareButtonAction.ToggleOutput:
                ToggleManualMute();
                break;
        }
    }

    private void ToggleMicrophoneMute()
    {
        try
        {
            bool muted = _audio.ToggleManualMicrophoneMute(_settings.Hardware.MicrophoneScope);
            _logger.Information(muted ? "Microphone manually muted." : "Microphone manually restored.", 1201);
            RefreshHardware(force: true);
        }
        catch (Exception ex)
        {
            ShowError("Windows microphone state could not be changed.", ex);
        }
    }

    private void RefreshHardware(bool force = false)
    {
        if (!_settings.Hardware.Enabled)
        {
            _hardwareController.Update(_settings.Hardware, microphoneMuted: false, outputMuted: false, force);
            return;
        }

        try
        {
            bool microphoneMuted = _audio.AreAllMicrophonesMuted(_settings.Hardware.MicrophoneScope);
            bool outputMuted = _audio.AreAllMuted();
            _hardwareController.Update(_settings.Hardware, microphoneMuted, outputMuted, force);
            _hardwareRefreshErrorLogged = false;
        }
        catch (Exception ex)
        {
            if (!_hardwareRefreshErrorLogged)
            {
                _logger.Error("Hardware indicator state could not be refreshed.", ex, 2501);
                _hardwareRefreshErrorLogged = true;
            }
        }
    }

    private void UpdateTrayPresentation()
    {
        bool muted;
        try
        {
            muted = _audio.AreAllMuted();
        }
        catch
        {
            muted = false;
        }

        _muteItem.Text = muted ? "Unmute / restore sound" : "Mute all sound";
        _scheduleItem.Text = "Schedule";
        _scheduleItem.Checked = _settings.ScheduleEnabled;

        Icon nextIcon = TrayIconFactory.Create(muted, _settings.ScheduleEnabled);
        _notifyIcon.Icon = nextIcon;
        Icon? previous = _currentIcon;
        _currentIcon = nextIcon;
        previous?.Dispose();
        _notifyIcon.Text = $"WinDND - {(muted ? "muted" : "sound on")}, schedule {(_settings.ScheduleEnabled ? "on" : "off")}";
    }

    private void ShowError(string message, Exception exception)
    {
        _logger.Error(message, exception, 2000);
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
        _notifyIcon.BalloonTipTitle = "WinDND";
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(5000);
    }

    protected override void ExitThreadCore()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        try
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _scheduler.Dispose();
            _audioDeviceMonitor?.Dispose();
            _hardwareTimer.Stop();
            _hardwareHotkeys.Dispose();
            _hardwareController.Dispose();
            _settingsForm?.Close();
            _logger.Information("WinDND stopped.", 1001);
        }
        catch (Exception ex)
        {
            _logger.Error("WinDND encountered an error while releasing resources.", ex, 2003);
        }
        finally
        {
            _notifyIcon.Visible = false;
            base.ExitThreadCore();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _scheduler.Dispose();
            _audioDeviceMonitor?.Dispose();
            _hardwareTimer.Dispose();
            _hardwareHotkeys.Dispose();
            _hardwareController.Dispose();
            _notifyIcon.Dispose();
            _menu.Dispose();
            _currentIcon?.Dispose();
            _dispatcher.Dispose();
        }

        base.Dispose(disposing);
    }
}
