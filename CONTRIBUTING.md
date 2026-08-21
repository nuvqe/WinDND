# Contributing to WinDND

Open an issue before starting a substantial change so the intended behavior can be agreed upon.

## Development

WinDND requires Windows 10 1809 or later and the .NET 10 SDK. Run the canonical build from PowerShell:

```powershell
.\build.ps1
```

The script restores locked dependencies, runs the tests, publishes the self-contained x64 application, and builds the WiX 7 installer. Keep warnings at zero, add tests for behavior changes, and do not commit generated files under `artifacts`, `bin`, or `obj`.

By submitting a contribution, you represent that you have the right to submit it. Unless you explicitly state otherwise, intentionally submitted contributions are provided under the project's [Apache License 2.0](LICENSE), as described in section 5 of that license.
