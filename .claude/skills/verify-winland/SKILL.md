---
name: verify-winland
description: Drive Winland, this repo's Windows tiling window manager, on the real desktop and prove what a user sees. Each feature has a driver that starts an isolated Winland, opens test windows, sends real keys, mouse input and winland CLI commands, and checks windows, replies and events. Use to verify a change to Winland's behavior, before claiming a feature works, or to keep the feature map in features/ in line with the code.
---

# Verify Winland

Winland is a desktop app with no UI of its own beyond borders, tab bars and a tray icon. A user touches it through key chords, the mouse, the tray menu and the `winland` CLI, which talks to the running Daemon over a named pipe. The verification surface is therefore the Windows desktop plus that CLI. Every path below is relative to the repository root. Run every command from there.

The map of every user-facing feature is [features/README.md](../../../.agents/skills/verify-winland/features/README.md). Read it first, then the feature file for what you are proving.

## Launch

You do not start Winland yourself to verify a feature. Each driver starts its own:

```powershell
pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature <id>
```

The script builds the driver project in Release, which builds `winland.exe`, then runs that feature's driver. The driver writes a config to `%TEMP%\winland-verify-<guid>\winland.conf` that ignores every window but its own test windows, and starts `winland.exe --config` on it with its own pipe `winland-verify-<guid>` and its own state file. It needs no seed data: it opens the windows it drives. It is ready when it prints `winland is answering on winland-verify-<guid>`. It tears its Winland down itself.

Add `-NoBuild` to reuse the last Release build. Omit `-Feature` to drive every feature in turn, as CI does.

To explore by hand instead, start an isolated dev build as [Run a dev build](../../../docs/development.md#run-a-dev-build) says, with your own `WINLAND_PIPE`, `WINLAND_STATE` and a `-Config` that ignores every window but yours. It is ready when `winland version` answers on that pipe. Stop it with `scripts/run.ps1 -Stop` from the same shell.

## Doctor

```powershell
pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor
```

It changes nothing. It prints `doctor: ok`, or one `doctor:` line per problem, and exits 1:

- a `winland.exe` runs on this desktop, with its PID and path. Every driver skips while any Winland runs. Ask before you stop one this run did not start, such as the user's installed copy.
- the shell runs in session 0, which has no desktop.
- no .NET SDK matches `global.json`.
- no Release build of the drivers exists yet.

It also prints which build the drivers will use, whether the `install`, `update` and `tweaks` drivers will run, and the feature ids.

## Drive

1. Run the doctor.
2. Open the feature file in [features/](../../../.agents/skills/verify-winland/features/README.md) for what you are proving. Its `Driving it with drive.ps1` section lists each step the driver takes and the state it waits for.
3. Run its driver with `drive.ps1 -Feature <id>`. Drive every entry point the file lists. A proof that drives one convenient entry point is incomplete when the file lists others.
4. To change what a driver proves, edit `.agents/skills/verify-winland/driver/Features/<Name>.cs` and the feature file together.

The `install`, `update` and `tweaks` drivers change this user's install, `PATH` and Windows settings and put them back. Set `$env:WINLAND_VERIFY_SYSTEM = '1'` only when the user agreed to that, or in CI. Without it they skip, and a skipped driver fails.

A driver sends real key and mouse input to the desktop. Tell the user before a run that it will move the pointer and windows, and do not run two drivers at once: the second one skips while the first one's Winland runs.

## Evidence

- The console holds each action with its reply, such as `winland dispatch workspace 2 -> exit 0 stdout: ok`, and each observed state, such as `saw: workspace 2 hides both windows`.
- `TestResults/feature-<id>.trx` holds the same lines. It is in the repository root, ignored by git, and kept by cleanup.
- A pass prints `== feature <id>: passed, evidence in TestResults/feature-<id>.trx`. Quote that line and the `saw:` lines it rests on.
- Every driver ends by checking that every test window is visible again, the taskbars are visible, and no `winland` process remains. That is part of the pass.
- In CI the Drive step runs every driver. A failure uploads `TestResults/` as the `feature-driver-results` artifact.

Proof standards:

- Exercise the real user path: keys, mouse, the tray menu or the `winland` CLI. Do not call internal methods or test-only endpoints.
- Capture the action and the state that follows, not just the final state.
- Check side effects alongside what is visible: the Client list, the Event stream, files the feature writes, registry values it changes.
- A driver whose every test skipped verified nothing. Report its skip reason instead of a pass.
- The `Gotchas` of each feature name what its driver cannot reach. Do not report those parts as verified.

## Cleanup

A driver that finishes stops its own Winland. A run you interrupted can leave a driver's Winland running, windows hidden or the taskbar hidden. Then run:

```powershell
pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Cleanup
```

It stops only the test hosts and `winland.exe` processes this checkout's driver build started, starts Winland once on each leftover `%TEMP%\winland-verify-<guid>` state file so it puts those windows and the taskbar back, and removes those folders. It leaves `TestResults/` alone. Stop a dev build you started by hand with `scripts/run.ps1 -Stop`. Never stop a Winland by name.

## Helpers

`.agents/skills/verify-winland/drive.ps1` is the only helper:

| Command | What it does |
| --- | --- |
| `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` | Read-only readiness check. |
| `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature <id>` | Builds and drives one feature. `-NoBuild` skips the build. |
| `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1` | Drives every feature, and runs a failed driver once more. A pass on the second run prints `FLAKY:`. |
| `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Cleanup` | Stops what an interrupted run left and puts windows back. |

The drivers are in `.agents/skills/verify-winland/driver/`, an xunit project in `Winland.slnx`. `FeatureMapTests` there runs with the unit tests in `scripts/test.ps1` and fails when the map and the code disagree. Its message says what to add or fix.

Claude Code reads a copy of this file at `.claude/skills/verify-winland/SKILL.md`, because Windows checkouts do not keep symlinks. Change this file, then copy it over that one. `FeatureMapTests` fails until they match.
