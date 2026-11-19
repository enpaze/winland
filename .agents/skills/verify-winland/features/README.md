# Winland verification map

This directory is the maintained source for verifying what a Winland user sees. Read this index before you drive Winland, then use the matching feature file as the recipe. [behavior.md](../../../../docs/behavior.md) holds the full rules for each feature.

## Baseline preconditions

- Run from the repository root, in a signed-in Windows 11 desktop session. Session 0 and a locked screen have no desktop to drive.
- No `winland.exe` runs on this desktop, including an installed copy. Every driver skips while one runs. `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` names any that runs.
- The .NET SDK that `global.json` pins is on `PATH`.
- Each driver starts its own Winland on a pipe named `winland-verify-<guid>`, with its state and config in `%TEMP%\winland-verify-<guid>`. Its config sets every gap and border to 0, turns animations off, ignores every window, and tiles only windows of class `WinlandVerifyWindow` titled `WinlandVerify *` on Workspace 1. A feature's driver adds lines to that config where its file says so.
- The `install`, `update` and `tweaks` drivers change this user's install, `PATH` and Windows settings and put them back. They run only with `WINLAND_VERIFY_SYSTEM=1`, as CI sets it. In an elevated shell they run Winland's CLI as a standard user.

## Driving conventions

- Drive a feature with `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature <id>`. Add `-NoBuild` to reuse the last Release build.
- Each driver opens its own test windows, sends real keys, mouse input and `winland` CLI commands, and reads the windows, the CLI replies and the Event stream.
- To explore by hand, start an isolated build as [Run a dev build](../../../../docs/development.md#run-a-dev-build) says, keep its `WINLAND_PIPE` in your shell, and run the `winland` CLI from the dev build folder. Add `-j` for JSON replies.
- Treat every command as literal. Dispatcher names and selectors are case-sensitive where `docs/behavior.md` says so.
- Never drive a Winland that this run did not start.

## Proof and skip reporting

- The driver writes each action with its reply, as `winland workspace 2 -> exit 0 stdout: ok`, and each observed state, as `saw: workspace 2 hides both windows`. The console and `TestResults/feature-<id>.trx` both hold these lines.
- A pass prints `== feature <id>: passed, evidence in TestResults/feature-<id>.trx`.
- After its journey, every driver stops its Winland and checks that every test window is visible again, the taskbars are visible, and no `winland` process remains. A pass includes that check.
- A driver whose every test skipped fails, because it verified nothing. Report the skip reason it prints.
- Each feature's `Gotchas` names what its driver cannot reach on a desktop. Do not report those parts as verified.
- A driver that fails runs once more. A pass on the second run prints `FLAKY:` and is a bug in the driver, not a pass.

## Feature entry contract

Each feature file starts with an H1 title and one paragraph that says what the user sees and links its sections of `docs/behavior.md`. It then has exactly four H2 sections in this order.

1. `Sub-features` lists one bullet per behavior, starting with a backticked id. `dispatcher:<name>`, `request:<request>` and `install:<command>` name a Dispatcher, a pipe request such as `get windows`, or an install command, and each of those belongs to exactly one feature. Other ids are lowercase words joined by hyphens.
2. `How to get to it (user POV)` lists every way a user reaches the feature.
3. `Driving it with drive.ps1` starts with `Preconditions:`, names the driver command, and uses labeled bullets that pair each user action with the exact command or input and the observable result.
4. `Gotchas` lists traps that can waste or invalidate a run, and what the driver does not reach.

`FeatureMapTests` in the driver project fails when this map and the code disagree: a Dispatcher, pipe request, install command or `##` section of `behavior.md` that no feature claims, a feature without exactly one driver class tagged `[Trait("Feature", "<id>")]`, or a file out of this shape.

## Features

- [Workspaces](./workspaces.md) covers showing a Workspace and moving windows to one, with or without following them.
- [Layouts](./layouts.md) covers the dwindle, scrolling and floating Layouts, split flips, resizing and switching Layouts.
- [Focus](./focus.md) covers focusing a window by selector or by direction.
- [Window commands](./window-commands.md) covers swapping, moving, floating, pinning and closing the focused window.
- [Fullscreen](./fullscreen.md) covers fullscreen and maximized windows.
- [Special workspaces](./special-workspaces.md) covers a Workspace that opens over the normal one and hides again.
- [Monitors](./monitors.md) covers reporting Monitors, focusing one and moving a Workspace between them.
- [Groups](./groups.md) covers tabbed windows in one tile.
- [Window rules](./window-rules.md) covers floating, placing, fading and ignoring windows by executable, class and title.
- [Launching](./launching.md) covers `exec` and `exec-once`.
- [Binds](./binds.md) covers key chords and focus that follows the pointer.
- [Mouse drag](./mouse-drag.md) covers moving and resizing windows with a bound mouse button.
- [Touchpad swipe](./touchpad-swipe.md) covers switching Workspace with a touchpad swipe.
- [Decoration](./decoration.md) covers borders and inactive opacity.
- [Animations](./animations.md) covers windows moving to their tiles through frames.
- [Shell windows](./shell-windows.md) covers the taskbar and the shell's flyouts.
- [Urgency](./urgency.md) covers marking a window urgent instead of switching to it.
- [Virtual desktops](./virtual-desktops.md) covers letting go of windows on another virtual desktop and taking them back.
- [Notification area](./notification-area.md) covers the tray icon and its menu.
- [IPC](./ipc.md) covers queries, inspection and the Event stream on the pipe.
- [Config](./config.md) covers config files, `winland check` and reload on save.
- [Daemon](./daemon.md) covers one instance per session, crash recovery and a clean exit.
- [Install](./install.md) covers installing for this user or the machine, and uninstalling.
- [Update](./update.md) covers replacing an install with a newer release.
- [Tweaks](./tweaks.md) covers turning Windows Snap and touchpad workspace swipes off, and back on.
