# Special workspaces

A special workspace holds windows out of sight until the user shows it over the normal Workspace of the focused Monitor, whose Clients stay shown under it, and hides it again. The starter config calls the special workspace named `scratchpad` the scratchpad. The rules are in [Special workspaces](../../../../docs/behavior.md#special-workspaces).

## Sub-features

- `dispatcher:togglespecialworkspace` shows a special workspace on the focused Monitor, or hides it when it is already shown there.
- `special-address` sends a window to a special workspace with `special`, `special:` or `special:<name>` in `movetoworkspace` and `movetoworkspacesilent`.
- `normal-workspace-stays-active` keeps the normal Workspace the active one while the special workspace shows over it.
- `activespecial-events` reports each open and close of a special workspace on the Event stream.

## How to get to it (user POV)

- Press `Win+S` or `` Win+` `` to show or hide the scratchpad.
- Press `Win+Alt+S` or `` Win+Shift+` `` to move the window to the scratchpad.
- Run `winland dispatch togglespecialworkspace`, `winland dispatch togglespecialworkspace scratchpad` or `winland dispatch movetoworkspacesilent special`.
- Run `winland dispatch workspace special:<name>` to show a special workspace without hiding it on a second run.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature special-workspaces`. It prints `== feature special-workspaces: passed`.
- **Tile two windows.** The driver follows `winland events`, then opens `WinlandVerify 1` and `WinlandVerify 2`, which tile side by side.
- **Send a window away.** The driver focuses the right window with `winland dispatch focuswindow address:<hwnd>` and runs `winland dispatch movetoworkspacesilent special`. The right window hides, and its Client reports Workspace name `special:special`.
- **Show it.** The driver runs `winland dispatch togglespecialworkspace`. Both windows are visible, the Event stream holds an `activespecial` line with `special:special,`, and `winland -j activeworkspace` still reports id `1`.
- **Hide it.** The driver runs `winland dispatch togglespecialworkspace` again. The right window hides, the left window stays shown, and the Event stream holds an `activespecial` line with an empty name.

## Gotchas

- `togglespecialworkspace` with no argument addresses `special:special`. The starter's `Win+S` addresses `special:scratchpad`, another special workspace. Keep the names the same when you mix keys and `winland dispatch`.
- The normal Workspace stays the active one. `activeworkspace` reports Workspace 1 while the special workspace shows; check the window or the `activespecial` event instead.
- Focusing a Client on the normal Workspace closes the special workspace, and so does changing the Monitor's normal Workspace. Do not focus the left window while checking the open special workspace.
- The special workspace closes when its last Client leaves.
- In events and text replies the unnamed special workspace is id -99. The driver matches a Client's special workspace by its JSON `name`, `special:special`.
- `workspace special:<name>` shows the special workspace and does not hide it again on a second run, unlike `togglespecialworkspace`.
- The keys in the first list come from the starter config. The driver's config has no binds, so it drives this Dispatcher through `winland dispatch`. [Binds](./binds.md) proves the key path.
