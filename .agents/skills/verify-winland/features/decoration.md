# Decoration

Winland draws a border around each Client, in one color on the focused Client and another on the rest, and can make the Clients that are not focused transparent, so the user sees at a glance which window has the focus. The rules are in [Decoration](../../../../docs/behavior.md#decoration).

## Sub-features

- `client-border` draws a border `general:border_size` pixels wide outside each Client's frame, as click-through windows that follow the Client.
- `inactive-opacity` makes every Client that is not focused as transparent as `decoration:inactive_opacity` says, and keeps the focused Client opaque.
- `decoration-follows-focus` moves the transparency and the focused border to the window that takes the focus.

## How to get to it (user POV)

- Set `general:border_size`, `general:col.active_border` and `general:col.inactive_border` in the config. The starter config has `general:border_size` and `general:col.inactive_border` as comments, and the focused border follows the Windows accent color until `general:col.active_border` is set.
- Set `decoration:active_opacity` and `decoration:inactive_opacity` to fractions from 0 to 1, or give a window an `opacity` window rule.
- Set `decoration:rounding` for the corner radius and `decoration:blur:enabled` for the acrylic backdrop.
- Open the config with Edit config in the tray menu and save it to reload, or run `winland reload`.
- Press `Win+Arrows` to move the focus, which moves the focused border and the opacity with it.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds `general:border_size = 6`, `general:col.active_border = 0xffff0000`, `general:col.inactive_border = 0xff0000ff` and `decoration:inactive_opacity = 0.5` to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature decoration`. It prints `== feature decoration: passed`.
- **Tile two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`. Both become Clients and tile side by side across the work area.
- **Focus the left window.** The driver runs `winland focus address:<hwnd>` for the left window, and `winland -j get window` reports it. The right window's alpha is between 120 and 135 of 255, and the left window is opaque.
- **Border on the focused window.** At least four visible `WinlandBorder` windows of the Winland process lie within 16 pixels of the left window's frame and reach at least 4 pixels past it on every side.
- **Focus the right window.** The driver runs `winland focus address:<hwnd>` for the right window. The left window's alpha drops to between 120 and 135, the right window turns opaque, and the `WinlandBorder` windows surround the right window the same way.

## Gotchas

- The baseline config sets `general:border_size = 0`, so other drivers draw no border. Only this driver's lines turn borders on.
- The driver reads opacity as the window's layered alpha. A window without `WS_EX_LAYERED` counts as opaque, because Winland adds that style only for an alpha below 255.
- The driver sets two border colors but does not read them. It does not reach `decoration:rounding`, `decoration:blur:enabled`, `decoration:active_opacity`, an `opacity` window rule, the Group border, or High Contrast colors.
- The border is four windows kept just above the Client, so a window raised over the Client covers its border too. Check a border by eye with nothing on top of the Client.
- A fullscreen, minimized or hidden Client draws no border, and a Client in fullscreen mode 2 is opaque.
