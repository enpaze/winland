# Virtual desktops

Winland's Workspaces are not Windows virtual desktops. When the user switches to another virtual desktop, the windows left behind stop being Clients, and when the user comes back Winland takes them back on their Workspace, in their former tiles. The rules are in [Virtual desktops](../../../../docs/behavior.md#virtual-desktops).

## Sub-features

- `release-on-switch` stops a shown Client from being a Client when a virtual desktop switch cloaks it.
- `adopt-on-return` takes the window back, silently, on its Workspace when it is uncloaked.
- `tiles-return` puts the tiles back in their former arrangement.

## How to get to it (user POV)

- Press `Win+Ctrl+D` to make and show a new virtual desktop, and `Win+Ctrl+F4` to close it and go back.
- Run `winland get windows` after the switch and after coming back to see which windows Winland holds.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature virtual-desktops`. It prints `== feature virtual-desktops: passed`.
- **Tile two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`. Both become Clients and tile side by side across the work area, and the driver notes both tiles.
- **Switch desktop.** The driver presses `Win+Ctrl+D` as real keys. Within 15 seconds neither window is in `winland -j get windows`.
- **Come back.** The driver presses `Win+Ctrl+F4` as real keys to close that desktop and go back. Within 15 seconds both windows are Clients again, and each reports Workspace `1`.
- **Former tiles.** Both windows return to the tiles they had before the switch.

## Gotchas

- The driver makes a real virtual desktop. It presses `Win+Ctrl+F4` to close it even when the run fails, but a run killed in between leaves the extra desktop. Close it with `Win+Ctrl+F4`.
- The driver's config has no binds, so `Win+Ctrl+D` and `Win+Ctrl+F4` reach Windows. A bind consumes its key, and the starter config binds `Win+Ctrl+Right` and `Win+Ctrl+Left` to the next and previous tab of a Group, so those chords do not reach Windows while it is loaded.
- The driver checks only two tiled Clients on the shown Workspace. It does not reach a Client Winland has hidden, which stays a Client while it is cloaked, or a floating, pinned or fullscreen Client keeping that state across the switch.
