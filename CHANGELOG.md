# Changelog

Notable user-facing changes to WinDND are recorded here.

## Unreleased

- Added physical O2L RGB V2 demonstration media and confirmed reliable key and RGB behavior.
- Added physical-device support for SayoDevice O2L RGB V2 (PID `000C`), including Lighting v2 RGB control.
- Improved SayoDevice discovery by selecting its vendor control interface and honoring firmware capabilities.
- Authenticode signing and package-manager distribution.

## Initial public release

- Apache License 2.0 source licensing and packaged attribution notices.
- Scheduled mute, unmute, and output-volume rules using five-field cron expressions.
- Human-readable schedule explanations and per-rule enablement.
- Manual mute and volume restoration across all active output endpoints.
- Startup, resume, and device-change reconciliation.
- Optional F13/F14 USB safe-button handling with configurable actions and RGB indicators.
- All-active or default-communications microphone scope.
- Windows Event Viewer logging, auto-start installation, and WiX 7 MSI packaging.
