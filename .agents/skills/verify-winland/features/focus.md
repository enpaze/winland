# Focus

Focus moves the keyboard to another Client, either one picked by a window selector or the neighbour in a direction, and moving focus in a direction puts the cursor in the middle of the window it focuses. The rules are in [Dispatchers](../../../../docs/behavior.md#dispatchers).

## Sub-features

- `dispatcher:movefocus` focuses the Client in a direction: `l`, `r`, `u` or `d`.
- `dispatcher:focuswindow` focuses the Client a window selector matches, such as `address:<hwnd>` or `title:<text>`.
- `cursor-warp` moves the cursor to the middle of the window `movefocus` focuses.
- `unmatched-selector` leaves focus where it is when `focuswindow` matches no Client.

## How to get to it (user POV)

- Press `Win+Arrows` to move focus in that direction.
- Run `winland dispatch movefocus r` or `winland dispatch focuswindow address:<hwnd>`.
- Run `winland activewindow` to see which window has focus.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature focus`. It prints `== feature focus: passed`.
- **Tile two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`. Both become Clients and tile side by side across the work area.
- **Focus by address.** The driver runs `winland dispatch focuswindow address:<hwnd>` for the left window. `winland -j activewindow` reports its address.
- **Focus right.** The driver runs `winland dispatch movefocus r`. `winland -j activewindow` reports the right window, and the cursor sits in the middle of that window, within 4 pixels.
- **Focus left.** The driver runs `winland dispatch movefocus l`. `winland -j activewindow` reports the left window again.
- **Unmatched selector.** The driver runs `winland dispatch focuswindow title:NoSuchWindow`. For half a second after, `winland -j activewindow` still reports the left window.

## Gotchas

- The driver reads the cursor position after `movefocus`. Moving the mouse during a run can fail that check.
- `cursor:no_warps` stops the cursor move, and leaving a fullscreen window for another on its Workspace does not move the cursor.
- A Workspace with a fullscreen or maximized Client offers no window to `movefocus` in a direction, so focus goes to the neighbouring Monitor or stays.
- `movefocus` continues onto the neighbouring Monitor when nothing in that direction can take focus. The driver does not reach that.
- Among Dispatchers, only `focuswindow` takes a window selector. The driver does not assert the reply to an unmatched selector, only that focus stays.
- The keys in the first list come from the starter config. The driver's config has no binds, so it drives these Dispatchers through `winland dispatch`. [Binds](./binds.md) proves the key path.
