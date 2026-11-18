# Layouts

A Layout places the Clients of a Workspace: dwindle splits the area in two for each new window, scrolling puts windows in columns on a strip, and floating leaves every Client floating. A user flips a dwindle split, resizes the focused tile or column, and switches the Workspace to another Layout without losing its Clients. The rules are in [Layout](../../../../docs/behavior.md#layout) and [Window placement](../../../../docs/behavior.md#window-placement).

## Sub-features

- `dispatcher:layout` selects the dwindle, scrolling or floating Layout for the focused Monitor's Shown workspace. `next` goes from dwindle to scrolling to floating and back to dwindle, and `prev` goes the other way.
- `dispatcher:layoutmsg` sends a message to the scrolling Layout, such as `colresize +conf` to step a column's width.
- `dispatcher:split` flips the parent dwindle split between side by side and stacked.
- `dispatcher:resize` with two numbers makes the focused tile wider, narrower, taller or shorter by that many pixels.
- `floating-layout-floats-tiles` floats every tiled Client while the Workspace is floating, and tiles them again when it leaves floating.
- `tiled-layout-report` reports the Workspace's Layout as `tiledLayout` in `get workspace`.

## How to get to it (user POV)

- Set `general:layout = dwindle`, `scrolling` or `floating` in the config.
- Press `Win+J` to flip the split, and `Win+K` or `Win+Alt+K` for the next or previous Layout.
- Press `Win+Alt+Minus` or `Win+Alt+Equal` to make the window narrower or wider by 25 pixels, `Win+Ctrl+Minus` or `Win+Ctrl+Equal` for 300 pixels, and `Win+Shift+Minus` or `Win+Shift+Equal` to make it shorter or taller.
- Run `winland layout scrolling`, `winland layout next`, `winland layout prev`, `winland split`, `winland resize 120 0` or `winland layoutmsg colresize +conf`.
- Run `winland get workspace` to see the Layout as `tiledLayout`.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds `general:layout = dwindle` to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature layouts`. It prints `== feature layouts: passed`.
- **Tile two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`. Both become Clients and tile side by side across the work area, and `winland -j get workspace` reports `tiledLayout` `dwindle`.
- **Flip the split.** The driver runs `winland split`. The two tiles stack one above the other.
- **Flip it back.** The driver runs `winland split` again. The tiles are side by side again.
- **Resize the tile.** The driver focuses the left window with `winland focus address:<hwnd>` and runs `winland resize 120 0`. The left tile grows at least 80 pixels wider, and the two tiles still fill the work area side by side.
- **Select scrolling.** The driver runs `winland layout scrolling`. `winland -j get workspace` reports `tiledLayout` `scrolling`.
- **Widen a column.** The driver focuses the left window and runs `winland layoutmsg colresize +conf`. The left window's column grows more than 40 pixels wider.
- **Cycle to floating.** The driver runs `winland layout next`. `tiledLayout` is `floating`, and both windows' Clients report `floating: true`.
- **Cycle back to dwindle.** The driver runs `winland layout next` again. `tiledLayout` is `dwindle`, the left window's Client reports `floating: false`, and the two windows tile side by side again.

## Gotchas

- `layout` with a name other than `dwindle`, `scrolling` or `floating` answers `error: unknown layout '<name>'` and leaves the Layout unchanged. Selecting the Layout that already runs changes nothing.
- `split` works only in dwindle, and `layoutmsg` only in scrolling. `layoutmsg colresize +conf` steps a scrolling column through `0.333, 0.5, 0.667, 1.0` of the view.
- `layout` lasts until the config is loaded again or Winland restarts, which puts the Workspace back on the Layout the config names. A reload mid-run undoes the switch.
- With an open special workspace on the focused Monitor, `layout` changes the special workspace's Layout, not the normal Workspace's.
- `winland resize -10 0` passes `-10` as a number, not a flag, because the CLI reads an argument that is a number as a number.
- The driver's config sets `general:layout = dwindle`, so the run starts in dwindle whatever the default is. To reproduce a run by hand, set the same line.
- The keys in the first list come from the starter config. The driver's config has no binds, so it drives these Dispatchers through the `winland` CLI. The starter binds no key to `layout` or `layoutmsg`.
