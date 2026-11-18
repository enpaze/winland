# Workspaces

Workspaces let a user show another numbered Workspace, which hides the windows of the one it leaves, and move the focused window to another Workspace, either following it there or staying put. The rules are in [Dispatchers](../../../../docs/behavior.md#dispatchers).

## Sub-features

- `dispatcher:workspace` shows another Workspace and hides the windows of the one it leaves.
- `dispatcher:movetoworkspace` moves the focused window to a Workspace and shows that Workspace.
- `dispatcher:movetoworkspacesilent` moves the focused window to a Workspace and keeps the current one shown.
- `workspace-events` reports each switch and move on the Event stream.

## How to get to it (user POV)

- Press `Win+1` to `Win+0` to show Workspace 1 to 10, `Win+Tab` or `Win+Shift+Tab` for the next or previous one, or `Win+Ctrl+Tab` for the last one.
- Press `Win+Shift+1` to `Win+Shift+0` to move the window and follow it, or `Win+Shift+Alt+1` to `Win+Shift+Alt+0` to move it and stay.
- Run `winland dispatch workspace 2`, `winland dispatch movetoworkspace 3` or `winland dispatch movetoworkspacesilent 3`.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature workspaces`. It prints `== feature workspaces: passed`.
- **Isolation.** The driver runs `winland -j clients`. The reply is `[]`, so Winland adopted none of the user's windows.
- **Tile two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`. Both become Clients and tile side by side across the work area.
- **Switch away.** The driver runs `winland dispatch workspace 2`. Both windows hide, `winland -j activeworkspace` reports id `2`, and the first window's Client reports `hidden: true`.
- **Switch back.** The driver runs `winland dispatch workspace 1`. Both windows show and tile side by side again.
- **Move and stay.** The driver focuses the second window with `winland dispatch focuswindow address:<hwnd>` and runs `winland dispatch movetoworkspacesilent 3`. The second window hides and its Client reports Workspace `3`, Workspace 1 stays active, and the first window fills the work area alone.
- **Move and follow.** The driver focuses the first window and runs `winland dispatch movetoworkspace 3`. `winland -j activeworkspace` reports `3`, and both windows are visible there.
- **Events.** The Event stream from `winland events` holds `openwindow` for `WinlandVerifyWindow,WinlandVerify 1`, a `workspacev2` line, and `movewindowv2` for the second window to Workspace `3`.

## Gotchas

- With no window, `winland clients` replies `no open windows` and `winland -j clients` replies `[]`. Assert on the JSON form.
- `movetoworkspacesilent` leaves focus on the Workspace you are on. Check the window's Client for its Workspace, not `activewindow`.
- The binds in the first list come from the starter config. The driver's config has no binds, so it drives these Dispatchers through `winland dispatch`. [Binds](./binds.md) proves the key path.
