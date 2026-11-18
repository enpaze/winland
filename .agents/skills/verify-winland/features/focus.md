# Focus

Focus moves the keyboard to another Client, either one picked by a window selector or the neighbour in a direction, and moving focus in a direction puts the cursor in the middle of the window it focuses. The rules are in [Dispatchers](../../../../docs/behavior.md#dispatchers).

## Sub-features

- `dispatcher:focus` focuses the Client in a direction, `left`, `right`, `up` or `down`, or the Client a window selector matches, such as `address:<hwnd>`, `title:<pattern>` or `exe:<pattern>`.
- `cursor-warp` moves the cursor to the middle of the window `focus` focuses in a direction.
- `unmatched-selector` leaves focus where it is when `focus` matches no Client.

## How to get to it (user POV)

- Press `Win+Arrows` to move focus in that direction.
- Run `winland focus right` or `winland focus address:<hwnd>`.
- Run `winland get window` to see which window has focus.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature focus`. It prints `== feature focus: passed`.
- **Tile two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`. Both become Clients and tile side by side across the work area.
- **Focus by address.** The driver runs `winland focus address:<hwnd>` for the left window. `winland -j get window` reports its address.
- **Focus right.** The driver runs `winland focus right`. `winland -j get window` reports the right window, and the cursor sits in the middle of that window, within 4 pixels.
- **Focus left.** The driver runs `winland focus left`. `winland -j get window` reports the left window again.
- **Focus by title pattern.** The driver runs `winland focus title:winlandverify*<n>`, with the right window's number and its title in lower case. `winland -j get window` reports the right window.
- **Focus by exe pattern.** The driver runs `winland focus exe:<EXE>`, the driver process's file name in upper case. It answers `ok`, and `winland -j get window` reports one of the two windows.
- **Focus by address again.** The driver runs `winland focus address:<hwnd>` for the left window. `winland -j get window` reports it.
- **Unmatched selector.** The driver runs `winland focus title:NoSuchWindow`. For half a second after, `winland -j get window` still reports the left window.

## Gotchas

- The driver reads the cursor position after `focus right`. Moving the mouse during a run can fail that check.
- `cursor:no_warps` stops the cursor move, and leaving a fullscreen window for another on its Workspace does not move the cursor.
- A Workspace with a fullscreen or maximized Client offers no window to `focus` in a direction, so focus goes to the neighbouring Monitor or stays.
- `focus` in a direction continues onto the neighbouring Monitor when nothing in that direction can take focus. The driver does not reach that.
- Among Dispatchers, only `focus` takes a window selector. The driver does not assert the reply to an unmatched selector, only that focus stays.
- Both test windows belong to the driver's process, so `exe:` matches both. The driver does not assert which one it focuses.
- The keys in the first list come from the starter config. The driver's config has no binds, so it drives these Dispatchers through the `winland` CLI. [Binds](./binds.md) proves the key path.
