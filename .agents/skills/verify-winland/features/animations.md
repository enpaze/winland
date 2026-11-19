# Animations

Winland moves windows to their tiles through frames instead of at once: a Window opens from a small rectangle and fades in, a tile that changes its Layout rectangle moves there, and a Workspace with a higher number slides in from the right while one with a lower number slides in from the left. The rules are in [Animations](../../../../docs/behavior.md#animations).

## Sub-features

- `workspace-slide` brings a Workspace with a higher number in from the right, through several frames, across the Monitor's width.
- `motion-ends-on-tile` leaves each window exactly on its Layout rectangle when its motion ends.

## How to get to it (user POV)

- Leave `animations` at its default of true. The starter config has `animations = true` as a comment. False moves every window at once.
- Keep the Windows setting Animation effects on. With it off, every window moves at once.
- Press `Win+1` to `Win+0`, `Win+Tab` or `Win+Shift+Tab` to switch Workspace and watch it slide in, or run `winland workspace 2`.
- Open a window, resize a tile with the keyboard, or toggle fullscreen with `Win+F` to see the other motions.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds `animations = true` to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature animations`. It prints `== feature animations: passed`.
- **Animation effects.** When the Windows setting Animation effects is off, the driver turns it on for the run without saving the change, and prints a line that says so.
- **Tile one window.** The driver opens `WinlandVerify 1`. It becomes a Client and fills the work area.
- **Move it away.** The driver focuses the window with `winland focus address:<hwnd>` and runs `winland move 2 stay`. The window hides.
- **Slide in.** The driver runs `winland workspace 2` and reads the window's left edge every 5 ms for 1.5 seconds. At least three different left edges lie between the right edge of the Monitor and a point 20 pixels right of the tile's left edge, and the driver prints `saw: the window passed through N places between the right edge and its tile`.
- **Rest on the tile.** The window ends on its tile and fills the work area again.

## Gotchas

- The baseline config sets `animations = false`, so every other driver moves windows at once. Only this driver's line turns animations on.
- The driver turns Animation effects off again in its cleanup. A run killed before that leaves the setting on.
- The driver checks only a Workspace with a higher number sliding in from the right. It does not reach a Window opening or fading in, a tile moving or resizing, fullscreen, a Window closing, a lower Workspace sliding in from the left, the special workspace fade, or opacity and border blends.
- A slide is drawn only inside its Monitor. This driver has one Monitor, so it does not check a Monitor beside it.
