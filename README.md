# SafeScreen

![SafeScreen logo](SafeScreenLogo.png)

SafeScreen is a lightweight Windows tray utility that keeps damaged monitor
edges out of the usable desktop. It reserves excluded strips through the native
Windows AppBar API, so maximized windows stay inside the visible area.

The app is local-only, needs no administrator rights, and makes no network
requests.

## What it does

- Choose the usable area directly on the full screen by dragging its left, top,
  and bottom boundaries.
- Keep the right edge pinned for displays damaged along the left or bottom.
- Toggle restrictions, switch profiles, or reopen the picker from the tray.
- Restore the saved area after Explorer or display-layout changes.
- Start automatically for the current Windows user.
- Use a compact visual editor as a fallback; no numeric input is required.

Keyboard controls in the full-screen picker:

- `Enter` — save;
- `Esc` — cancel;
- `Space` — select the next boundary;
- arrow keys — move the selected boundary by 5 px;
- `Shift` + arrow keys — move it by 20 px.

## Install

1. Download `SafeScreen-v1.4.0-win-x64.zip` from Releases.
2. Extract it into a permanent folder.
3. Run `SafeScreen.exe --install` once.
4. Run `SafeScreen.exe`.

Right-click the tray icon for profiles and controls. Double-click it to open
the full-screen picker.

## Commands

```powershell
.\SafeScreen.exe --install
.\SafeScreen.exe
.\SafeScreen.exe --enable
.\SafeScreen.exe --disable
.\SafeScreen.exe --set 80 40 225
.\SafeScreen.exe --show-settings
.\SafeScreen.exe --stop
.\SafeScreen.exe --uninstall
```

## Build from source

On Windows with .NET Framework installed:

```powershell
.\build.ps1
```

The script uses the local .NET Framework C# compiler and embeds the application
manifest, icon, and logo into one executable.

## Current limits

- The full-screen picker targets the primary display.
- The right boundary is intentionally fixed to the monitor edge.
- SafeScreen does not repair panel hardware; it only avoids damaged pixels.

## Privacy

Settings are stored for the current Windows user under
`HKCU\Software\AWAKE\SafeScreen`. No telemetry, cloud service, account, or
network permission is used. See [PRIVACY.md](PRIVACY.md).

## License

MIT — see [LICENSE](LICENSE).
