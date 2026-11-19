# Groups

A Group holds several Clients in one tile with a tab bar. Only the shown member is in the Layout, and the other members hide but stay Clients. A user makes a Group, adds a neighbor to it, shows another member, and takes a member back out to its own tile. The rules are in [Groups](../../../../docs/behavior.md#groups).

## Sub-features

- `dispatcher:group` makes the focused Client a Group of one, or ends the Group it is in.
- `dispatcher:join` moves the focused Client into the Group of the Client in a direction, and shows it there.
- `dispatcher:tab` shows the next member with `next`, the previous one with `prev`, or the member at a position counted from 1.
- `dispatcher:leave-group` takes the focused Client out of its Group into its own tile next to the Group.
- `group-tile` shows one member in the Group's tile and hides the others, which `winland -j get windows` lists with `hidden` true and their `grouped` list.
- `group-events` reports a new Group on the Event stream as `togglegroup>>1,ADDRESS`.

## How to get to it (user POV)

- Press `Win+G` to make the window a Group or break its Group up, and `Win+Alt+G` to take the window out of its Group.
- Press `Win+Alt+Arrows` to add the window to the Group next to it.
- Press `Win+Alt+Tab` or `Win+Alt+Shift+Tab`, or `Win+Ctrl+Right` or `Win+Ctrl+Left`, for the next or previous tab. Turn the wheel with `Win+Alt` held for the same. Press `Win+Alt+1` to `Win+Alt+5` to go to tab 1 to 5.
- Click a tab to focus that member.
- Run `winland group`, `winland join left`, `winland tab next`, `winland tab prev` or `winland leave-group`.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature groups`. It prints `== feature groups: passed`.
- **Follow events.** The driver starts `winland events` before it opens a window.
- **Tile two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`. Both become Clients and tile side by side across the work area.
- **Make a Group.** The driver focuses the left window with `winland focus address:<hwnd>` and runs `winland group`. The left window's Client lists one address in `grouped`, and the Event stream holds `togglegroup>>1,<address>` for the left window, its address without the `0x`.
- **Join the Group.** The driver focuses the right window and runs `winland join left`. The left window's Client lists two addresses in `grouped` and reports `hidden: true`. The right window shows, the left one hides, and the right window covers at least nine tenths of the work area's width as one tile.
- **Show the other member.** The driver runs `winland tab next`. The left window shows and the right one hides.
- **Take a member out.** The driver runs `winland leave-group`. Both windows show and tile side by side again.

## Gotchas

- A hidden member is still a Client. Read `hidden` and `grouped` from `winland -j get windows` before you call a window lost.
- `tab` on a window that is not grouped is an error, and so is `tab` on a Group of one.
- `leave-group` takes no argument and answers `error: leave-group takes no argument` to one. `join` answers `error: unsupported direction: ` to anything but a direction.
- The Event stream keeps the names `togglegroup`, `moveintogroup` and `moveoutofgroup` for these changes.
- A Group is not rebuilt after a restart. Its members come back as separate Clients.
- The binds in the first list come from the starter config. The driver's config has no binds, so it drives these Dispatchers through the `winland` CLI.
- The driver does not reach the tab bar, its clicks and its wheel, joining or leaving a Group by dragging, or a new window that joins a focused Group.
