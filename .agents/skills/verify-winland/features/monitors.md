# Monitors

Winland uses the Monitors Windows reports, with the size, position, scale and work area Windows gives each. A user lists them, focuses one, and moves the Workspace a Monitor shows onto another Monitor with its windows. The rules are in [Monitors](../../../../docs/behavior.md#monitors).

## Sub-features

- `dispatcher:focusmonitor` focuses a Monitor by name, `desc:` prefix, direction, `current`, `+N` or `-N`, or id.
- `dispatcher:movecurrentworkspacetomonitor` moves the focused Monitor's Shown workspace onto another Monitor.
- `request:get monitors` lists the enabled Monitors, and with `all` the disabled ones too.
- `unknown-monitor` answers `error: monitor not found` to a selector that matches no Monitor.
- `monitor-events` reports `focus` when the focused Monitor changes and `moveworkspace` when a Workspace moves.

## How to get to it (user POV)

- Press `Ctrl+Alt+Shift+Tab` to focus the previous Monitor.
- Press `Win+Shift+Alt+Arrows` to move the whole Workspace to the Monitor in that direction.
- Run `winland get monitors` or `winland get monitors all`.
- Run `winland focusmonitor <name>` or `winland movecurrentworkspacetomonitor <name>`.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature monitors`. It prints `== feature monitors: passed`.
- **List Monitors.** The driver runs `winland -j get monitors`. It lists as many Monitors as Windows reports, each with a `width` and `height` above 0 and exactly the keys [Monitors](../../../../docs/behavior.md#monitors) lists, and `winland -j get monitors all` lists at least as many.
- **Focus the focused Monitor.** The driver runs `winland focusmonitor <name>` with the name of the Monitor that reports `focused: true`, then `winland focusmonitor current`. Both reply `ok`, and `winland -j get workspace` reports that Monitor.
- **Unknown Monitor.** The driver runs `winland focusmonitor NoSuchMonitor`. It exits 1 and writes `error: monitor not found` to standard error.
- **One Monitor.** With one Monitor, the driver runs `winland movecurrentworkspacetomonitor current`, which replies `ok`, prints `only one monitor: focusmonitor and movecurrentworkspacetomonitor answered on it, and moving between monitors was not driven`, and ends.
- **Focus another Monitor.** With two or more, the driver follows `winland events`, opens `WinlandVerify 1`, and runs `winland focusmonitor <other>`. `winland -j get workspace` reports the other Monitor, and the Event stream holds a `focus` line naming it.
- **Move the Workspace.** The driver runs `winland focusmonitor <name>` to go back, focuses the window with `winland focuswindow address:<hwnd>`, and runs `winland movecurrentworkspacetomonitor <other>`. The window's Client reports its Workspace on the other Monitor, and the Event stream holds a `moveworkspace` line.
- **Move it back.** The driver runs `winland movecurrentworkspacetomonitor <name>`. The window's Client reports its Workspace on the first Monitor again.

## Gotchas

- On one Monitor, as on CI, the driver does not reach moving focus and a Workspace to another Monitor, and it does not reach plugging or unplugging a Monitor. A pass on one Monitor proves only the listing, the error reply, and that `focusmonitor` and `movecurrentworkspacetomonitor current` answer `ok` on that Monitor.
- `get monitors` lists only the enabled Monitors. Use `get monitors all` to see the disabled ones.
- A failed action, such as `winland focusmonitor NoSuchMonitor`, exits 1 and writes its `error:` reply to standard error, not standard output.
- Focusing another Monitor emits `focus` and not `workspace`, because that Monitor already shows its Workspace. Focusing the Monitor that already has focus emits nothing.
- Moving a Workspace leaves its pinned Clients on the source Monitor, on the Workspace that Monitor shows next.
- Monitor configuration belongs in Windows Settings, not in the Winland config.
- The keys in the first list come from the starter config. The driver's config has no binds, so it drives these Dispatchers through the `winland` CLI. [Binds](./binds.md) proves the key path.
