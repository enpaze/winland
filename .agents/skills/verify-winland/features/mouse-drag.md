# Mouse drag

A mouse button bound to `movewindow` or `resizewindow` lets the user drag a window. `movewindow` moves the Client with the pointer, and `resizewindow` resizes a floating Client from the grabbed corner. A tiled Client lifts out of its Layout while it moves and drops back in at the pointer. The rules are in [Mouse drag](../../../../docs/behavior.md#mouse-drag).

## Sub-features

- `dispatcher:resizewindow` resizes a floating Client from the corner nearest the pointer, or changes a tiled Client's split ratios.
- `move-drag` moves a floating Client with the pointer while a `movewindow` mouse bind is held.

## How to get to it (user POV)

- Hold `Win` and drag with the left button to move a window, or with the right button to resize it.
- Bind a mouse button to `movewindow` or `resizewindow` in the config. The starter config has `bindd = $mainMod, mouse:left, Drag to move the window, movewindow` and `bindd = $mainMod, mouse:right, Drag to resize the window, resizewindow`. Buttons are `mouse:left`, `mouse:right`, `mouse:middle`, `mouse:x1` and `mouse:x2`.
- Drag a window by its title bar or a sizing border. Winland follows the move and drops a tiled Client back into its Layout.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds these lines to the baseline config:
  - `bind = CTRL ALT SHIFT, mouse:left, movewindow`
  - `bind = CTRL ALT SHIFT, mouse:right, resizewindow`
  - `windowrule = float, class:WinlandVerifyWindow, title:WinlandDrag`

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature mouse-drag`. It prints `== feature mouse-drag: passed`.
- **List the binds.** The driver runs `winland binds`. The reply holds `bind = SHIFT CTRL ALT, mouse:left, movewindow` and `bind = SHIFT CTRL ALT, mouse:right, resizewindow`.
- **Open a floating window.** The driver opens `WinlandDrag` at 400 by 300. Its Client reports `floating: true`, and the driver focuses it with `winland dispatch focuswindow address:<hwnd>`.
- **Move.** The driver glides the real pointer to the middle of the window, holds `Ctrl+Alt+Shift`, presses the left button, glides 160 pixels right and 90 down, and releases. The window's frame moves by 160,90 within 12 pixels and keeps its width.
- **Resize.** The driver puts the pointer 20 pixels inside the bottom right corner, holds `Ctrl+Alt+Shift`, presses the right button, glides 120 pixels right and 70 down, and releases. The window grows at least 90 pixels wider and 50 taller, and its left edge stays within 6 pixels.
- **Move after a resize.** In a second journey the driver opens `WinlandDrag` again and resizes it with the right button as above. It then drags the window's middle with `Ctrl+Alt+Shift` and the left button by -100,-60. The window moves by -100 within 12 pixels and keeps its resized width and height within 4 pixels.
- **Move after a border drag.** The driver presses the left button 2 pixels outside the bottom right corner of the visible frame, on the sizing border, with no modifier, glides 90 right and 60 down, and releases. The window grows at least 60 pixels wider. The same `Ctrl+Alt+Shift` move then keeps the new size.
- **Move after the window resizes itself.** At least 2.5 seconds after opening the window, the driver calls `SetWindowPos` on its own window to grow it by 80,50. `winland clients` reports the new frame size, and the same `Ctrl+Alt+Shift` move keeps it.
- **Lift a resized tile.** In a third journey the driver opens two tiled windows side by side and drags the split 120 pixels right with `Ctrl+Alt+Shift` and the right button. The left tile grows at least 80 pixels wider. While the driver holds a `Ctrl+Alt+Shift` left-button drag from that tile's middle by 60,40, the lifted window keeps the tile's resized width and height within 4 pixels. On release the two windows tile side by side again.

## Gotchas

- `resizewindow` grows from the corner nearest the pointer. Grab near the bottom right corner to keep the left and top edges in place.
- A floating window that resizes itself within 2 seconds of opening is put back at the size Winland opened it at. The driver waits until 2.5 seconds after opening before the self-resize step.
- A drag picks up the window as soon as the pointer moves. A press with no Client under the pointer does not drag.
- The bound button is swallowed, so the window under the pointer does not get the click.
- The `Win` drags in the first list come from the starter config. The driver's config binds the left and right buttons with `Ctrl+Alt+Shift` instead of `Win`.
- The driver lifts a tile and checks that it drops back beside the other, but not where in the Layout it lands. It does not reach a drag onto another Monitor, a drag into or out of a Group, title-bar moves, a border drag on a tiled Client, `resizewindow, 1`, or a drag that ends fullscreen or maximized.
