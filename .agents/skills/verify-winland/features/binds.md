# Binds

A bind runs a Dispatcher when the user presses its key chord, and the chord does not reach the focused window. With `input:follow_mouse` on, moving the pointer onto a Client focuses it. `winland binds` lists every bind as a config line. How a bind reads its modifiers is in [Modifiers](../../../../docs/behavior.md#modifiers), and the pointer and keyboard rules are in [Cursor and input](../../../../docs/behavior.md#cursor-and-input).

## Sub-features

- `request:binds` lists the config's binds, one config line each, or as JSON with each bind's `key`, `modmask` and `dispatcher` with `-j`. A mouse button prints by name, such as `mouse:left`.
- `key-chord` runs a bind's Dispatcher when its chord goes down, with modifiers taken from a config variable.
- `follow-mouse` focuses the Client under the pointer when `input:follow_mouse` is true.

## How to get to it (user POV)

- Press any hotkey in README.md's Hotkeys tables. The starter config sets `$mainMod = SUPER` and describes each bind.
- Run `winland binds` to list the hotkeys as config lines with their descriptions, or `winland -j binds` for JSON.
- Add a `bind = <modifiers>, <key>, <dispatcher>, <args>` line to the config. Modifiers can come from a variable, such as `$mainMod = SUPER`.
- Set `input:follow_mouse = true` or `input:follow_mouse = false` in the config. It defaults to true.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds these lines to the baseline config:
  - `$hyper = CTRL ALT SHIFT`
  - `bind = $hyper, F9, workspace, 4`
  - `bind = $hyper, F10, workspace, 1`
  - `bind = $hyper, MOUSE:X2, workspace, 1`
  - `input:follow_mouse = true`

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature binds`. It prints `== feature binds: passed`.
- **List the binds.** The driver runs `winland binds`. The reply holds `bind = SHIFT CTRL ALT, F9, workspace, 4` and `bind = SHIFT CTRL ALT, mouse:x2, workspace, 1`. `winland -j binds` holds a bind with key `F9`, `modmask` `13` and dispatcher `workspace`, and a bind with key `mouse:x2` and `mouse` `true`.
- **Tile two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`. Both become Clients and tile side by side across the work area.
- **Press a bind.** The driver sends real `Ctrl+Alt+Shift+F9` key input. `winland -j activeworkspace` reports `4`, and the left window hides.
- **Press the other bind.** The driver sends `Ctrl+Alt+Shift+F10`. `winland -j activeworkspace` reports `1`, and the left window shows again.
- **Follow the mouse.** The driver focuses the left window with `winland dispatch focuswindow address:<hwnd>`, then moves the real pointer to the middle of the right window 15 times, a pixel apart. `winland -j activewindow` reports the right window's address.

## Gotchas

- A move after the pointer has been still for half a second does not move focus yet. One pointer move may not focus the Client under it.
- A context menu or other unmanaged window over a Client keeps focus where it is.
- A modifier list that names no modifier, or keeps letters after the names are removed, warns `Invalid modifier` and the bind is skipped. A bind flag other than `d` is skipped too. Check `winland configerrors` when a chord does nothing.
- A key remapper with its own low-level hook, such as PowerToys Keyboard Manager, can change which key Winland sees and which binds run.
- The driver does not reach `cursor:no_warps`, `cursor:hide_on_key_press`, wheel notches, `input:follow_mouse = false`, keyboard layout switches, or held keys after a desktop switch.
