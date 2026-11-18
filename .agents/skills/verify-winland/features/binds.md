# Binds

A bind runs a Dispatcher when the user presses its key chord, and the chord does not reach the focused window. With `input:follow_mouse` on, moving the pointer onto a Client focuses it. `winland get binds` lists every bind as a config line. How a bind reads its modifiers is in [Modifiers](../../../../docs/behavior.md#modifiers), and the pointer and keyboard rules are in [Cursor and input](../../../../docs/behavior.md#cursor-and-input).

## Sub-features

- `request:get binds` lists the config's binds, one config line each, or as JSON with each bind's `key`, `modMask` and `dispatcher` with `-j`. A mouse button prints by name, such as `mouse:left`.
- `key-chord` runs a bind's Dispatcher when its chord goes down, with modifiers taken from a config variable such as `$hyper+F9`.
- `follow-mouse` focuses the Client under the pointer when `input:follow_mouse` is true.

## How to get to it (user POV)

- Press any hotkey in README.md's Hotkeys tables. The starter config sets `$mod = Win` and describes each bind.
- Run `winland get binds` to list the hotkeys as config lines with their descriptions, or `winland -j get binds` for JSON.
- Add a `bind = <chord>, ["description",] <action> [args]` line to the config, such as `bind = Win+Shift+B, "Open the browser", exec start https://`. Modifiers can come from a variable, such as `$mod = Win` with `bind = $mod+B, ...`.
- Set `input:follow_mouse = true` or `input:follow_mouse = false` in the config. It defaults to true.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds these lines to the baseline config:
  - `$hyper = Ctrl+Alt+Shift`
  - `bind = $hyper+F9, workspace 4`
  - `bind = $hyper+F10, "Back to workspace 1", workspace 1`
  - `bind = $hyper+MOUSE:X2, workspace 1`
  - `input:follow_mouse = true`

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature binds`. It prints `== feature binds: passed`.
- **List the binds.** The driver runs `winland get binds`. The reply holds `bind = Ctrl+Alt+Shift+F9, workspace 4`, `bind = Ctrl+Alt+Shift+F10, "Back to workspace 1", workspace 1` and `bind = Ctrl+Alt+Shift+mouse:x2, workspace 1`. `winland -j get binds` holds a bind with key `F9`, `modMask` `13` and dispatcher `workspace`, and a bind with key `mouse:x2` and `mouse` `true`.
- **Tile two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`. Both become Clients and tile side by side across the work area.
- **Press a bind.** The driver sends real `Ctrl+Alt+Shift+F9` key input. `winland -j get workspace` reports `4`, and the left window hides.
- **Press the other bind.** The driver sends `Ctrl+Alt+Shift+F10`. `winland -j get workspace` reports `1`, and the left window shows again.
- **Follow the mouse.** The driver focuses the left window with `winland focus address:<hwnd>`, then moves the real pointer to the middle of the right window 15 times, a pixel apart. `winland -j get window` reports the right window's address.

## Gotchas

- A move after the pointer has been still for half a second does not move focus yet. One pointer move may not focus the Client under it.
- A context menu or other unmanaged window over a Client keeps focus where it is.
- A modifier other than `Win`, `Shift`, `Ctrl` or `Alt`, such as `SUPER`, warns `Unknown modifier` and the bind is skipped. `bindd` and other flag letters warn as unsupported keywords. Check `winland get errors` when a chord does nothing.
- A key remapper with its own low-level hook, such as PowerToys Keyboard Manager, can change which key Winland sees and which binds run.
- The driver does not reach `cursor:no_warps`, `cursor:hide_on_key_press`, wheel notches, `input:follow_mouse = false`, keyboard layout switches, or held keys after a desktop switch.
