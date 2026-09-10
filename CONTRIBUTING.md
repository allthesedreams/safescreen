# Contributing to SafeScreen

Thanks for helping improve SafeScreen. Keep changes small, local-only, and
focused on making damaged displays easier to use.

## Before opening a change

1. Search existing issues.
2. For bugs, include Windows version, display resolution, scaling, taskbar
   position, and exact SafeScreen margins.
3. Never attach screenshots containing private desktop content unless you have
   redacted them first.

## Build and verify

```powershell
.\build.ps1
.\SafeScreen.exe --show-settings
```

Verify at minimum:

- the full-screen picker opens and saves;
- maximized windows stay inside the selected area;
- tray enable/disable preserves the saved margins;
- autostart can be enabled and disabled;
- Explorer restart or display changes do not permanently remove the reserved area.

## Pull requests

Describe the user problem, the narrow change, manual verification, and any
known limitation. Do not commit binaries, logs, screenshots, credentials, or
machine-specific paths.
