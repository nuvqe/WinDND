using System.Globalization;

namespace WinDND.Core.Models;

public enum SoundActionKind
{
    Mute,
    Unmute,
    SetVolume
}

public readonly record struct SoundAction(SoundActionKind Kind, int Volume = 0)
{
    public static bool TryParse(string? value, out SoundAction action)
    {
        string normalized = value?.Trim() ?? string.Empty;

        if (normalized.Equals("mute", StringComparison.OrdinalIgnoreCase))
        {
            action = new SoundAction(SoundActionKind.Mute);
            return true;
        }

        if (normalized.Equals("unmute", StringComparison.OrdinalIgnoreCase))
        {
            action = new SoundAction(SoundActionKind.Unmute);
            return true;
        }

        if (int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out int volume)
            && volume is >= 0 and <= 100)
        {
            action = new SoundAction(SoundActionKind.SetVolume, volume);
            return true;
        }

        action = default;
        return false;
    }

    public override string ToString() => Kind switch
    {
        SoundActionKind.Mute => "mute",
        SoundActionKind.Unmute => "unmute",
        SoundActionKind.SetVolume => Volume.ToString(CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException("Unknown sound action.")
    };
}
