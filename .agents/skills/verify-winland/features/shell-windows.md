# Shell windows

Winland leaves the taskbar, the Start menu, Search and the shell's other flyouts unmanaged, so opening Start does not split the screen. With `general:hide_taskbar` on, the taskbars hide while Winland runs, the Clients tile over the strip they reserved, and a clean exit shows them again. The rules are in [Shell windows](../../../../docs/behavior.md#shell-windows).

## Sub-features

- `hide-taskbar` hides every taskbar while Winland runs when `general:hide_taskbar` is true.
- `work-area-takes-strip` lets the Clients tile over the strip the hidden taskbar reserved.
- `taskbar-unmanaged` never makes the taskbars, of class `Shell_TrayWnd` or `Shell_SecondaryTrayWnd`, Clients.
- `taskbar-back-on-exit` shows the taskbars again when Winland exits cleanly.

## How to get to it (user POV)

- Set `general:hide_taskbar = true` in the config. The starter config has `general:hide_taskbar = false` as a comment. The change applies at once on reload, from a save or `winland reload`.
- Open Start, Search or another flyout from the taskbar. It opens over the tiles and is not a Client.
- Run `winland clients` to see that no taskbar or flyout is listed.
- Quit from the tray menu or run `winland dispatch exit` to get the taskbars back. After a crash, `taskbar.marker` in `%USERPROFILE%\.winland` lets the next start put them back.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds `general:hide_taskbar = true` to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature shell-windows`. It prints `== feature shell-windows: passed`.
- **Hide the taskbars.** Winland starts with the driver's config, and the taskbars hide.
- **Tile over the strip.** The driver opens `WinlandVerify 1`. It becomes a Client and fills the whole Monitor, within 12 pixels, including the strip the taskbar reserved.
- **Taskbar is not a Client.** The driver runs `winland -j clients`. No entry has class `Shell_TrayWnd` or `Shell_SecondaryTrayWnd`, and the driver prints `saw: the taskbar is not a Client`.
- **Exit.** The driver stops Winland. Every taskbar is visible again.

## Gotchas

- The driver checks that the taskbars come back only when they were visible before the run. Start the run with the taskbars shown.
- The hide check passes once any taskbar is hidden. On several Monitors, look at each taskbar to see that all of them hide.
- The driver does not open Start, Search or any other flyout, and does not check layered overlays or windows that are never activated. Open a flyout by hand to see that it does not split the screen.
- The driver does not crash Winland, so it does not reach `taskbar.marker` or a start that puts the taskbars back after a crash. It does not change `general:hide_taskbar` on reload either.
- A window rule that forces adoption, such as `float`, `tile` or `workspace` on `class:*`, still adopts a flyout it matches. Keep such rules narrow when you check flyouts by hand.
