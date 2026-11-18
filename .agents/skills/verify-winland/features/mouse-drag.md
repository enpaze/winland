# Mouse drag

A mouse button bound to `move` or `resize` with no argument lets the user drag a window. `move` moves the Client with the pointer, and `resize` resizes a floating Client from the grabbed corner. A tiled Client lifts out of its Layout while it moves and drops back in at the pointer. The rules are in [Mouse drag](../../../../docs/behavior.md#mouse-drag).

## Sub-features

- `resize-drag` resizes a floating Client from the corner nearest the pointer, or changes a tiled Client's split ratios, while a `resize` mouse bind is held.
- `move-drag` moves a floating Client with the pointer while a `move` mouse bind is held.

## How to get to it (user POV)

- Hold `Win` and drag with the left button to move a window, or with the right button to resize it.
- Bind a mouse button to `move` or `resize` in the config. The starter config has `bindd = $mainMod, mouse:left, Drag to move the window, move` and `bindd = $mainMod, mouse:right, Drag to resize the window, resize`. Buttons are `mouse:left`, `mouse:right`, `mouse:middle`, `mouse:x1` and `mouse:x2`.
- Drag a window by its title bar or a sizing border. Winland follows the move and drops a tiled Client back into its Layout.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds these lines to the baseline config:
  - `bind = CTRL ALT SHIFT, mouse:left, move`
  - `bind = CTRL ALT SHIFT, mouse:right, resize`
  - `windowrule = float, class:WinlandVerifyWindow, title:WinlandDrag`

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature mouse-drag`. It prints `== feature mouse-drag: passed`.
- **List the binds.** The driver runs `winland get binds`. The reply holds `bind = SHIFT CTRL ALT, mouse:left, move` and `bind = SHIFT CTRL ALT, mouse:right, resize`.
- **Open a floating window.** The driver opens `WinlandDrag` at 400 by 300. Its Client reports `floating: true`, and the driver focuses it with `winland focus address:<hwnd>`.
- **Move.** The driver glides the real pointer to the middle of the window, holds `Ctrl+Alt+Shift`, presses the left button, glides 160 pixels right and 90 down, and releases. The window's frame moves by 160,90 within 12 pixels and keeps its width.
- **Resize.** The driver puts the pointer 20 pixels inside the bottom right corner, holds `Ctrl+Alt+Shift`, presses the right button, glides 120 pixels right and 70 down, and releases. The window grows at least 90 pixels wider and 50 taller, and its left edge stays within 6 pixels.

## Gotchas

- A `resize` drag grows from the corner nearest the pointer. Grab near the bottom right corner to keep the left and top edges in place.
- A drag picks up the window as soon as the pointer moves. A press with no Client under the pointer does not drag.
- The bound button is swallowed, so the window under the pointer does not get the click.
- The `Win` drags in the first list come from the starter config. The driver's config binds the left and right buttons with `Ctrl+Alt+Shift` instead of `Win`.
- The driver does not reach a tiled Client lifted and dropped, a drag onto another Monitor, a drag into or out of a Group, title-bar and border moves, or a drag that ends fullscreen or maximized.
