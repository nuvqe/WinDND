# Hardware validation checklist

The current SayoDevice O2/O2C-compatible controller support was implemented from the published legacy HID protocol. Validate each physical device before relying on its LED state during a call.

## Expected defaults

| Item | Default |
| --- | --- |
| USB vendor ID | `8089` |
| USB product IDs | `0002`, `0003` |
| Left key | `F13`, toggle all active microphones |
| Right key | `F14`, toggle output sound |
| Both indicators | Microphone state |
| Muted color | Red (`#FF0000`) |
| Unmuted color | Blue (`#0000FF`) |

## First connection

1. If the vendor provides a configuration tool, save or photograph the keypad's original configuration first.
2. Connect the keypad directly to the PC for initial testing rather than through a dock or KVM.
3. Open **WinDND → Settings → Hardware controller**.
4. Confirm the displayed vendor and product IDs. Change the configured IDs only if Windows reports different values for the intended device.
5. Enable the controller and automatic key programming, then save.
6. Confirm that WinDND reports a connected compatible device.

This PowerShell command can help identify likely devices without changing them:

```powershell
Get-CimInstance Win32_PnPEntity |
    Where-Object PNPDeviceID -Match 'VID_8089' |
    Select-Object Name, PNPDeviceID, Status
```

## Functional tests

- Press the left button repeatedly and confirm every active input changes state exactly once per press.
- Confirm red means every selected microphone is muted.
- Unmute any selected microphone outside WinDND and confirm both indicators change to blue within a few seconds.
- Press the right button and confirm all active outputs mute and restore their previous levels.
- Test built-in, USB, Bluetooth, dock, webcam, and virtual audio devices that you actually use.
- Test while Teams, Zoom, a browser call, or another conferencing application is running.
- Test after sleep/resume, sign-out/sign-in, reboot, USB disconnect/reconnect, and audio-device hot-plug.
- Test through the USB hub, dock, or KVM used in normal operation.
- Confirm **Exit** stops button handling and that the app starts again at the next sign-in.

Do not treat the LED as a safety indicator until all relevant microphones have passed these tests. Conferencing applications can have their own software mute state; WinDND represents the Windows input endpoint state.

## What to collect when something fails

- Keypad make/model and a link to its product page.
- Hardware revision, if printed on the device.
- The full `VID_xxxx&PID_xxxx` identifier from Windows.
- WinDND and Windows versions.
- Whether automatic programming, key presses, left RGB, and right RGB work independently.
- Sanitized WinDND entries from Event Viewer.
- Results from `WinDND.exe --check-microphone`.

Open a [hardware compatibility report](https://github.com/nuvqe/WinDND/issues/new?template=hardware_compatibility.yml) with those results.

## Demo media shot list

Once the hardware is validated, capture a short, silent loop that shows:

1. The keypad and Windows microphone settings in the same frame.
2. A left-button press changing every microphone to muted and both LEDs to red.
3. A second press restoring microphones and changing both LEDs to blue.
4. A right-button press muting and restoring output sound.

Prefer a 16:9 MP4 source and export a small, readable GIF for the README. Avoid rapid cuts or flashing. Put final files at `docs/assets/windnd-demo.gif` and `docs/assets/windnd-demo.mp4`; then replace the README screenshot table with the GIF and link the MP4 as the full demonstration.
