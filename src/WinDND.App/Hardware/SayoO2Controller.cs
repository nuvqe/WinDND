using System.Globalization;
using HidSharp;
using WinDND.Core.Models;
using WinDND.Infrastructure;

namespace WinDND.Hardware;

internal sealed class SayoO2Controller(IAppLogger logger) : IDisposable
{
    private const int LeftLed = 0;
    private const int RightLed = 1;
    private const byte F13Usage = 0x68;
    private const byte F14Usage = 0x69;
    private readonly IAppLogger _logger = logger;
    private string? _devicePath;
    private string? _settingsKey;
    private DateTime _nextDiscoveryUtc;
    private DateTime _nextHealthCheckUtc;
    private Color? _lastLeftColor;
    private Color? _lastRightColor;
    private bool _keysConfigured;
    private bool _unavailableLogged;
    private bool _errorLogged;

    public bool IsConnected => _devicePath is not null;

    public string Status => IsConnected
        ? "Compatible SayoDevice connected."
        : "No compatible SayoDevice detected.";

    public void Update(
        HardwareControllerSettings settings,
        bool microphoneMuted,
        bool outputMuted,
        bool force = false)
    {
        if (!settings.Enabled)
        {
            Reset();
            return;
        }

        string settingsKey = BuildSettingsKey(settings);
        if (!string.Equals(settingsKey, _settingsKey, StringComparison.Ordinal))
        {
            Reset();
            _settingsKey = settingsKey;
            force = true;
        }

        Color mutedColor = ParseColor(settings.MutedColor);
        Color unmutedColor = ParseColor(settings.UnmutedColor);
        Color leftColor = ResolveColor(settings.LeftIndicator, microphoneMuted, outputMuted, mutedColor, unmutedColor);
        Color rightColor = ResolveColor(settings.RightIndicator, microphoneMuted, outputMuted, mutedColor, unmutedColor);
        bool keysReady = !settings.ConfigureKeys || _keysConfigured;
        if (!force && _devicePath is not null && keysReady &&
            _lastLeftColor == leftColor && _lastRightColor == rightColor &&
            DateTime.UtcNow < _nextHealthCheckUtc)
        {
            return;
        }

        HidDevice? device = FindDevice(settings);
        if (device is null)
        {
            _devicePath = null;
            _keysConfigured = false;
            _lastLeftColor = null;
            _lastRightColor = null;
            if (!_unavailableLogged)
            {
                _logger.Information(
                    $"Hardware controller is enabled, but no SayoDevice matching VID {settings.VendorId:X4} was found.",
                    1500);
                _unavailableLogged = true;
            }

            return;
        }

        if (_devicePath is not null && !string.Equals(_devicePath, device.DevicePath, StringComparison.Ordinal))
        {
            _keysConfigured = false;
            _lastLeftColor = null;
            _lastRightColor = null;
            force = true;
        }

        try
        {
            using HidStream stream = device.Open();
            stream.ReadTimeout = 750;
            stream.WriteTimeout = 750;
            Initialize(stream, device);

            if (settings.ConfigureKeys && !_keysConfigured)
            {
                bool changed = ConfigureKey(stream, device, 0, F13Usage);
                changed |= ConfigureKey(stream, device, 1, F14Usage);
                if (changed)
                {
                    SendCommand(stream, device, 4, [0x72, 0x96]);
                    _logger.Information("SayoDevice buttons were configured as F13 and F14.", 1502);
                }

                _keysConfigured = true;
            }

            if (force || _lastLeftColor != leftColor)
            {
                SetStaticColor(stream, device, LeftLed, leftColor);
                _lastLeftColor = leftColor;
            }

            if (force || _lastRightColor != rightColor)
            {
                SetStaticColor(stream, device, RightLed, rightColor);
                _lastRightColor = rightColor;
            }

            if (_devicePath is null)
            {
                _logger.Information(
                    $"SayoDevice connected (VID {device.VendorID:X4}, PID {device.ProductID:X4}).",
                    1501);
            }

            _devicePath = device.DevicePath;
            _nextHealthCheckUtc = DateTime.UtcNow.AddSeconds(5);
            _unavailableLogged = false;
            _errorLogged = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException)
        {
            _devicePath = null;
            _lastLeftColor = null;
            _lastRightColor = null;
            _nextDiscoveryUtc = DateTime.UtcNow.AddSeconds(5);
            if (!_errorLogged)
            {
                _logger.Error("The SayoDevice hardware controller could not be updated.", ex, 2500);
                _errorLogged = true;
            }
        }
    }

    private HidDevice? FindDevice(HardwareControllerSettings settings)
    {
        if (DateTime.UtcNow < _nextDiscoveryUtc)
        {
            return null;
        }

        HidDevice? device = DeviceList.Local
            .GetHidDevices(settings.VendorId)
            .Where(candidate => settings.ProductIds.Count == 0 || settings.ProductIds.Contains(candidate.ProductID))
            .FirstOrDefault(candidate =>
            {
                try
                {
                    return candidate.GetMaxInputReportLength() >= 64 && candidate.GetMaxOutputReportLength() >= 64;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return false;
                }
            });

        _nextDiscoveryUtc = device is null ? DateTime.UtcNow.AddSeconds(5) : DateTime.MinValue;
        return device;
    }

    private static void Initialize(HidStream stream, HidDevice device)
    {
        DateTime now = DateTime.Now;
        SendCommand(stream, device, 0, [(byte)now.Day, (byte)now.Hour, (byte)now.Minute, (byte)now.Second]);
    }

    private static bool ConfigureKey(HidStream stream, HidDevice device, byte keyNumber, byte keyUsage)
    {
        try
        {
            byte[] response = SendCommand(stream, device, 6, [0, keyNumber]);
            if (response.Length > 8 && response[5] == 1 && response[7] == 0 && response[8] == keyUsage)
            {
                return false;
            }
        }
        catch (Exception ex) when (ex is IOException or TimeoutException)
        {
            // Some legacy revisions do not implement key reads but still accept writes.
        }

        // Legacy O2/O2C key command: pattern, key number, keyboard mode, reserved,
        // modifier, and up to three HID keyboard usages.
        SendCommand(stream, device, 6, [1, keyNumber, 1, 0, 0, keyUsage, 0, 0]);
        return true;
    }

    private static void SetStaticColor(HidStream stream, HidDevice device, byte ledNumber, Color color)
    {
        // Legacy O2/O2C lighting command: write, LED, static RGB mode, event,
        // command length, R, G, B, interval.
        SendCommand(stream, device, 7, [1, ledNumber, 0, 0, 0, color.R, color.G, color.B, 0]);
    }

    private static byte[] SendCommand(HidStream stream, HidDevice device, byte command, ReadOnlySpan<byte> payload)
    {
        int reportLength = device.GetMaxOutputReportLength();
        if (reportLength < payload.Length + 4)
        {
            throw new IOException("The SayoDevice HID report is shorter than the requested command.");
        }

        byte[] report = new byte[reportLength];
        report[0] = 2;
        report[1] = command;
        report[2] = checked((byte)payload.Length);
        payload.CopyTo(report.AsSpan(3));
        int checksumIndex = payload.Length + 3;
        report[checksumIndex] = unchecked((byte)report.AsSpan(0, checksumIndex).ToArray().Sum(value => value));
        stream.Write(report);

        byte[] response = new byte[device.GetMaxInputReportLength()];
        int count = stream.Read(response);
        if (count < 3 || response[1] != 0)
        {
            throw new IOException($"SayoDevice rejected command {command}.");
        }

        Array.Resize(ref response, count);
        return response;
    }

    private static Color ResolveColor(
        HardwareIndicator indicator,
        bool microphoneMuted,
        bool outputMuted,
        Color mutedColor,
        Color unmutedColor) => indicator switch
        {
            HardwareIndicator.None => Color.Black,
            HardwareIndicator.Microphone => microphoneMuted ? mutedColor : unmutedColor,
            HardwareIndicator.Output => outputMuted ? mutedColor : unmutedColor,
            _ => Color.Black
        };

    internal static Color ParseColor(string value)
    {
        if (value.Length == 7 && value[0] == '#' &&
            int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
        {
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        throw new FormatException($"'{value}' is not an RGB color in #RRGGBB format.");
    }

    private static string BuildSettingsKey(HardwareControllerSettings settings) => string.Join(
        '|',
        settings.VendorId,
        string.Join(',', settings.ProductIds),
        settings.ConfigureKeys,
        settings.LeftIndicator,
        settings.RightIndicator,
        settings.MutedColor,
        settings.UnmutedColor);

    private void Reset()
    {
        _devicePath = null;
        _settingsKey = null;
        _nextDiscoveryUtc = DateTime.MinValue;
        _nextHealthCheckUtc = DateTime.MinValue;
        _lastLeftColor = null;
        _lastRightColor = null;
        _keysConfigured = false;
        _unavailableLogged = false;
        _errorLogged = false;
    }

    public void Dispose() => Reset();
}
