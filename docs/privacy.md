# WinDND privacy

WinDND is designed to operate entirely on the local Windows computer.

## What WinDND accesses

- Windows Core Audio endpoint names, volume levels, and mute states.
- Configured cron rules and hardware-controller preferences.
- F13 and F14 messages used for the optional two-button controller. Depending on the configured key-routing mode, WinDND can suppress them, pass them to the foreground application, or do both.
- Compatible HID device configuration and RGB state when hardware support is enabled.

WinDND changes endpoint volume and mute state. It does not open an audio capture stream, record microphone audio, inspect call content, or store keystrokes.

## Data storage

Per-user settings are stored at:

```text
%LOCALAPPDATA%\WinDND\settings.json
```

Installed builds log lifecycle, scheduling, audio-device, and error events to **Event Viewer → Windows Logs → Application** under source `WinDND`. Development runs may instead write `%LOCALAPPDATA%\WinDND\WinDND.log`.

Logs can contain application paths, audio-device details, hardware identifiers, or exception messages. Review and sanitize entries before posting them publicly.

## Network and telemetry

WinDND has no account system, analytics, telemetry, advertisements, cloud service, automatic updater, or application-level network communication. GitHub is used only by people who choose to download releases or participate in the project.

The installer configures WinDND to start at user sign-in. It does not install an audio driver or a Windows service.

## Removing local data

Uninstall WinDND through Windows **Installed apps**. To remove saved per-user configuration and fallback logs as well, delete `%LOCALAPPDATA%\WinDND` after uninstalling.
