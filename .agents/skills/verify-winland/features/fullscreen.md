# Fullscreen

Fullscreen makes the Focused window cover its whole Monitor without a frame, and maximize makes it fill the work area. Asking again puts the window back in its tile. While a Client covers its Workspace, the other windows there lose the `allowedOverFullscreen` flag. The rules are in [Fullscreen](../../../../docs/behavior.md#fullscreen).

## Sub-features

- `dispatcher:fullscreen` toggles fullscreen, or maximize with `1`, `maximize` or `maximized`.
- `fullscreen-mode` covers the Monitor's whole rectangle and reports `fullscreen` `2` for the Client.
- `maximize-mode` fills the work area and reports `fullscreen` `1` for the Client.
- `allowed-over-fullscreen` takes the `allowedOverFullscreen` flag from the other windows on the Workspace while a Client covers it.
- `restore-tile` puts the Client back in its tile, side by side with its neighbour, when the mode ends.

## How to get to it (user POV)

- Press `Win+F` for fullscreen and `Win+Alt+F` to maximize.
- Click a window's maximize button, press `Win+Up`, or double-click its title bar to maximize it.
- Run `winland dispatch fullscreen` or `winland dispatch fullscreen 1`.
- Run `winland clients` to see each Client's `fullscreen` mode and `allowedOverFullscreen` flag.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature fullscreen`. It prints `== feature fullscreen: passed`.
- **Tile two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`, which tile side by side, and focuses the left one with `winland dispatch focuswindow address:<hwnd>`.
- **Go fullscreen.** The driver runs `winland dispatch fullscreen`. The left window covers the whole Monitor, its Client reports `fullscreen: 2`, and the right window's Client reports `allowedOverFullscreen: false`.
- **Leave fullscreen.** The driver runs `winland dispatch fullscreen` again. The Client reports `fullscreen: 0`, and the two windows tile side by side again.
- **Maximize.** The driver runs `winland dispatch fullscreen 1`. The left window fills the work area, and its Client reports `fullscreen: 1`.
- **End maximize.** The driver runs `winland dispatch fullscreen 1` again. The Client reports `fullscreen: 0`, and the two windows tile side by side again.

## Gotchas

- Any argument other than `1`, `maximize` or `maximized` toggles fullscreen, including `0` and `2`. To force a mode on or off, add `set` or `unset` as a second argument.
- Maximize fills the work area less `gaps_out`. The driver's config sets every gap to 0, so the window matches the work area there; with gaps it does not.
- Focusing a tiled Client on the same Workspace ends the covering Client's mode. Do not focus the other tile while checking the fullscreen window.
- `fullscreen` does nothing to a pinned Client.
- `movewindow` in a direction, `swapwindow` and `togglesplit` refuse a Client that is fullscreen or maximized.
- The driver does not reach the scrolling Layout's fullscreen, which gives the tile a column of its own, nor a window that covers its Monitor by itself, nor the maximize button, `Win+Up` or a double click.
- The keys in the first list come from the starter config. The driver's config has no binds, so it drives this Dispatcher through `winland dispatch`. [Binds](./binds.md) proves the key path.
