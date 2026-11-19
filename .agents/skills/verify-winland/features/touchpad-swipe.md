# Touchpad swipe

On a precision touchpad, a swipe with three or more fingers switches to the next or previous Workspace on the focused Monitor, and the Workspaces follow the fingers while they move. A `gesture` line in the config sets the finger count and the direction. The rules are in [Touchpad swipe](../../../../docs/behavior.md#touchpad-swipe).

## Sub-features

- `gesture-lines` loads `gesture = <fingers>, horizontal, workspace` and `gesture = <fingers>, vertical, workspace` lines without a warning.
- `gesture-warnings` warns about a gesture with another direction or an extra argument, such as `mod:`, and skips the line.

## How to get to it (user POV)

- Swipe three fingers sideways. The starter config has `gesture = 3, horizontal, workspace`. Fingers moving left go to the next Workspace, and fingers moving right go to the previous one.
- Add a `gesture` line to the config, such as `gesture = 4, vertical, workspace`.
- Turn off Windows' own three- and four-finger swipes with `winland tweaks off --touchpad`, or in Settings under Bluetooth & devices, then Touchpad.
- Run `winland get errors` to see the `gesture` lines Winland warned about.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds these lines to the baseline config, as lines 8 to 11:
  - `gesture = 3, horizontal, workspace`
  - `gesture = 4, vertical, workspace`
  - `gesture = 3, diagonal, workspace`
  - `gesture = 3, horizontal, workspace, mod:Win`

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature touchpad-swipe`. It prints `== feature touchpad-swipe: passed`.
- **Read the warnings.** The driver runs `winland get errors`. The reply has exactly two lines. One holds `winland.conf:10: Invalid direction: diagonal.` and the other holds `winland.conf:11: A gesture is <fingers>, horizontal or vertical, workspace.` The two valid lines give no warning.

## Gotchas

- The driver does not reach the swipe itself. Windows has no API that injects precision-touchpad input, so the driver checks only which `gesture` lines Winland takes and which it warns about.
- The line numbers in `get errors` count the seven baseline lines first, so the driver's first `gesture` line is line 8.
- A touchpad that is not a precision touchpad does not swipe. A two-finger gesture never matches, and a locked session does not swipe.
- Windows also acts on the same fingers. Turn its gestures off for the finger count you configure, or one swipe also switches apps or desktops in Windows.
