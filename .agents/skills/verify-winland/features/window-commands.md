# Window commands

Window commands act on the Focused window: swap it with a neighbour, move it in a direction, float or tile it, pin a floating window so it shows on every Workspace of its Monitor, and close it. The rules are in [Dispatchers](../../../../docs/behavior.md#dispatchers).

## Sub-features

- `dispatcher:swapwindow` exchanges the focused Client with the one in a direction on its Workspace.
- `dispatcher:movewindow` moves the focused Client in a direction, or to another Monitor with `mon:<direction>`.
- `dispatcher:togglefloating` floats a tiled Client, or tiles a floating one again.
- `dispatcher:pin` toggles whether a floating Client is pinned, or sets it with `enable` or `disable`.
- `dispatcher:killactive` asks the focused Client to close.
- `pinned-follows-workspace` keeps a pinned Client shown when its Monitor switches Workspace, and leaves it on that Workspace once unpinned.

## How to get to it (user POV)

- Press `Win+Shift+Arrows` to swap the window with its neighbour, and `Win+Shift+Ctrl+Arrows` to move it to another Monitor.
- Press `Win+T` to float or tile the window, and `Win+O` to pop it out, which floats and pins it.
- Press `Win+Q` or `Win+W` to close the window.
- Run `winland dispatch swapwindow r`, `winland dispatch movewindow l`, `winland dispatch togglefloating`, `winland dispatch pin` or `winland dispatch killactive`.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature window-commands`. It prints `== feature window-commands: passed`.
- **Tile two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`. Both become Clients and tile side by side across the work area.
- **Swap.** The driver focuses the left window with `winland dispatch focuswindow address:<hwnd>` and runs `winland dispatch swapwindow r`. The focused window is now on the right.
- **Move.** The driver runs `winland dispatch movewindow l`. The focused window is back on the left.
- **Float.** The driver runs `winland dispatch togglefloating`. The window's Client reports `floating: true`, and the other window fills the work area.
- **Pin.** The driver runs `winland dispatch pin`. The floating window's Client reports `pinned: true`.
- **Switch with a pin.** The driver runs `winland dispatch workspace 2`. The tile hides, the pinned window stays shown, and its Client reports Workspace `2`.
- **Unpin.** The driver runs `winland dispatch pin` again. The Client reports `pinned: false`.
- **Switch back.** The driver runs `winland dispatch workspace 1`. The tile shows again, and the unpinned window hides, left on Workspace 2.
- **Close.** The driver focuses the tile and runs `winland dispatch killactive`. The window closes and is no longer in `winland -j clients`.

## Gotchas

- `pin` acts only on a floating Client. A tiled, fullscreen or maximized Client does not qualify, so float the window first, as the driver does.
- Changing whether a Client floats clears its pin and emits no `pin` event.
- Unpinning leaves the Client on the Workspace its Monitor shows at that moment. The driver checks this when Workspace 1 shows and the window stays hidden on Workspace 2.
- `swapwindow` answers `error: Can't swap fullscreen window`, and `error: No window to swap with in that direction` when nothing is there or the argument is not a direction.
- `togglefloating` answers `error: togglefloating takes no argument` to an argument, and `pin` answers `error: pin takes toggle, enable or disable` to any other word.
- `movewindow` in a direction and `swapwindow` refuse a Client that is fullscreen or maximized.
- `killactive` asks the application to close, so a real application can ask to save first and stay open.
- The driver does not move a window to another Monitor with `movewindow`.
- The keys in the first list come from the starter config. The driver's config has no binds, so it drives these Dispatchers through `winland dispatch`. [Binds](./binds.md) proves the key path.
