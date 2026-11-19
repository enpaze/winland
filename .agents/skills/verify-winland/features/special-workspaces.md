# Special workspaces

A special workspace holds windows out of sight until the user shows it over the normal Workspace of the focused Monitor, whose Clients stay shown under it, and hides it again. The `scratchpad` action shows and hides one, and the starter config calls the unnamed one, `special:special`, the scratchpad. The rules are in [Special workspaces](../../../../docs/behavior.md#special-workspaces).

## Sub-features

- `dispatcher:scratchpad` shows a special workspace on the focused Monitor, or hides it when it is already shown there.
- `special-address` sends a window to a special workspace with `special`, `special:` or `special:<name>` in `move`, with or without `stay`.
- `normal-workspace-stays-active` keeps the normal Workspace the active one while the special workspace shows over it.
- `activespecial-events` reports each open and close of a special workspace on the Event stream.

## How to get to it (user POV)

- Press `Win+S` or `` Win+` `` to show or hide the scratchpad.
- Press `Win+Alt+S` or `` Win+Shift+` `` to move the window to the scratchpad.
- Run `winland scratchpad`, `winland scratchpad notes` or `winland move special stay`.
- Run `winland workspace special:<name>` to show a special workspace without hiding it on a second run.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature special-workspaces`. It prints `== feature special-workspaces: passed`.
- **Tile two windows.** The driver follows `winland events`, then opens `WinlandVerify 1` and `WinlandVerify 2`, which tile side by side.
- **Send a window away.** The driver focuses the right window with `winland focus address:<hwnd>` and runs `winland move special stay`. The right window hides, and its Client reports Workspace name `special:special`.
- **Show it.** The driver runs `winland scratchpad`. Both windows are visible, the Event stream holds an `activespecial` line with `-99,special:special,`, and `winland -j get workspace` still reports id `1`.
- **Hide it.** The driver runs `winland scratchpad` again. The right window hides, the left window stays shown, and the Event stream holds an `activespecial` line with an empty id and name.

## Gotchas

- `scratchpad` with no argument addresses `special:special`, and so do the starter's `Win+S` and `Win+Alt+S`. `scratchpad notes` addresses `special:notes`, another special workspace.
- The normal Workspace stays the active one. `get workspace` reports Workspace 1 while the special workspace shows; check the window or the `activespecial` event instead.
- Focusing a Client on the normal Workspace closes the special workspace, and so does changing the Monitor's normal Workspace. Do not focus the left window while checking the open special workspace.
- The special workspace closes when its last Client leaves.
- In events and text replies the unnamed special workspace is id -99. The driver matches a Client's special workspace by its JSON `name`, `special:special`.
- `workspace special:<name>` shows the special workspace and does not hide it again on a second run, unlike `scratchpad`.
- The keys in the first list come from the starter config. The driver's config has no binds, so it drives this Dispatcher through the `winland` CLI. [Binds](./binds.md) proves the key path.
