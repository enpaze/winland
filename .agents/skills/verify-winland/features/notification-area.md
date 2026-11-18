# Notification area

Winland shows an icon in the notification area with the number of the Workspace the focused Monitor is showing. A click or a right-click on it opens a menu to reload the config, edit it, open the log, and quit. The icon also carries config notifications as balloons. The rules are in [Notification area](../../../../docs/behavior.md#notification-area).

## Sub-features

- `tray-menu` opens a menu with Reload, Edit config, Open log and Quit, with the access keys R, E, L and Q, from a click or a right-click on the icon.
- `tray-reload` reads the config again when Reload is chosen, as `winland reload` does.
- `tray-quit` runs `exit` when Quit is chosen, which stops Winland and puts its windows back.

## How to get to it (user POV)

- Click or right-click the Winland icon in the notification area. Windows may keep it behind the `^` arrow. Its tooltip reads `Winland, Workspace N`.
- In the menu, press `R` for Reload, `E` for Edit config, `L` for Open log, or `Q` for Quit.
- Click a config notification balloon to open the config file, as Edit config does.
- Run `winland reload` or `winland dispatch exit` for the same Reload and Quit without the menu.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature notification-area`. It prints `== feature notification-area: passed`.
- **Follow events.** The driver runs `winland events` and keeps reading it.
- **Hide a window.** The driver opens `WinlandVerify 1`, which becomes a Client, and runs `winland dispatch workspace 2`. The window hides.
- **Find the icon.** Winland has its notification-area icon window, of class `WinlandTray`.
- **Open the menu.** The driver sends the icon a right-click. The menu opens, and the driver prints its items, which include `&Reload`, `&Edit config` and `&Quit`.
- **Reload.** The driver presses `R` in the menu. The Event stream holds a `configreloaded` line.
- **Quit.** The driver opens the menu again and presses `Q`. Winland exits, and the window hidden on Workspace 1 is visible again.

## Gotchas

- The driver does not reach the icon's picture and tooltip, or the balloons, because no API reads them back. Do not report the Workspace number on the icon as verified.
- The driver sends the right-click to Winland's icon window instead of clicking the icon, so it does not prove where Windows shows the icon or that a physical click reaches it.
- The driver checks that Edit config is in the menu but does not choose it, and it does not check Open log. Open log is in the menu only while `winland.log` exists, so its absence is not a fault.
- The driver does not restart Explorer, so it does not reach the icon coming back afterwards.
