# Daemon

One Daemon runs per session. A second `winland` started in the same session says so and exits. A Daemon that is killed leaves its hidden windows hidden, and the next start shows them and manages them again. `exit`, from the CLI or the tray's Quit, stops Winland and puts every window back. The rules are in [Daemon](../../../../docs/behavior.md#daemon).

## Sub-features

- `dispatcher:exit` stops Winland and shows every window it hid.
- `single-instance` makes a second `winland` in the same session print `Winland is already running in this session.` and exit with code 1.
- `crash-recovery` shows, on the next start, the windows a killed Daemon left hidden, and manages them again.
- `maintenance-usage` makes invalid maintenance arguments print their usage and exit with code 2.

## How to get to it (user POV)

- Run `winland`, or `winland --config <file>`, or start Winland from the Start menu. An installed Winland also starts at logon.
- Run `winland` again while one runs to see the single-instance message.
- Quit from the tray menu, or run `winland exit`. Either puts every window back.
- After a crash, start Winland again. It reads `%USERPROFILE%\.winland\state.json`, or the file `WINLAND_STATE` names, and shows the windows the crash left hidden.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature daemon`. It prints `== feature daemon: passed`.
- **Second instance.** The driver starts a second `winland --config <the run's config>` with the same `WINLAND_PIPE` and `WINLAND_STATE`. It exits 1, and standard error holds `Winland is already running in this session.`
- **Bad maintenance arguments.** The driver runs `winland update --channel nightly`. It exits 2.
- **Hide two windows.** The driver opens `WinlandVerify 1` and `WinlandVerify 2`, which tile side by side, and runs `winland workspace 2`. Both windows hide. The driver waits until the state file exists, then half a second more.
- **Kill.** The driver kills the `winland` process. Both windows stay hidden.
- **Restart.** The driver starts `winland` again with the same pipe, state file and config. Both windows show, and both are Clients again.
- **Exit.** The driver runs `winland workspace 2`, and both windows hide again. It runs `winland exit`. Winland stops, and both windows show again.

## Gotchas

- The driver does not reach log off and shut down.
- A Daemon started with a different `WINLAND_PIPE` counts as a separate instance. Start the second `winland` with the first one's `WINLAND_PIPE`, or it starts beside it instead of refusing.
- The state file is written at most four times a second, so a kill within that quarter second can leave an older state. Wait for the state file before you kill, as the driver does.
- A killed process cannot put anything back. The windows stay hidden until the next start with the same state file. Give that start the same `WINLAND_STATE`, or nothing recovers them.
