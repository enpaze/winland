# Urgency

Winland marks a window urgent instead of switching to it. A dialog that opens while its owner is on a hidden Workspace opens there silently, a Client that flashes for attention, and a Client whose focus Winland sends back are all urgent, and the user stays on the Workspace they are on. The rules are in [Urgency](../../../../docs/behavior.md#urgency), and where an owned window opens is in [Launching](../../../../docs/behavior.md#launching).

## Sub-features

- `owned-window-opens-silently` opens a window owned by a Client on its owner's Workspace, even when that Workspace is hidden.
- `urgent-event` emits `urgent` with the Client's address on the Event stream.
- `workspace-stays` keeps the shown Workspace when a Client becomes urgent.

## How to get to it (user POV)

- Let a program open a dialog while its main window is on a hidden Workspace. The dialog waits on that Workspace and the shown Workspace does not change.
- Let a Client flash for attention, or take the foreground with no click or key just before, so Winland sends the focus back.
- Run `winland events` and watch for `urgent` lines.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds `windowrule = float, class:WinlandVerifyWindow, title:WinlandDialog` to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature urgency`. It prints `== feature urgency: passed`.
- **Follow events.** The driver runs `winland events` and keeps reading it.
- **Tile two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`. Both become Clients and tile side by side across the work area.
- **Switch away.** The driver runs `winland dispatch workspace 2`. Both windows hide.
- **Open a dialog.** The driver opens a 220 by 140 window titled `WinlandDialog`, owned by the left window. Its Client in `winland -j clients` reports Workspace `1`, and the dialog stays hidden.
- **Urgent event.** The Event stream holds an `urgent` line with the dialog's address, written without the `0x` prefix.
- **Workspace stays.** `winland -j activeworkspace` still reports id `2`, and the driver prints `saw: the workspace did not change for the urgent window`.

## Gotchas

- The driver reaches only the owned window that opens on a hidden Workspace. It does not make a Client flash for attention or take the foreground, the other two ways a Client becomes urgent.
- The baseline config manages only windows titled `WinlandVerify *`. The dialog is titled `WinlandDialog`, so the driver adds a `float` rule to make it a Client.
- Addresses on the Event stream have no `0x` prefix, while `winland -j clients` writes them with it. Match the hex digits, not the whole string.
- The focused Client flashing is not urgent, so a check by hand must flash a Client that does not have the focus.
