# IPC

The `winland` CLI talks to the running Daemon over the IPC pipe. A user runs a Dispatcher, asks for the Clients, the focused window and the Workspaces, inspects a Window without changing it, and follows the Event stream. Failed commands answer `error:` and exit 1. The rules are in [IPC pipe](../../../../docs/behavior.md#ipc-pipe), [Window inspection](../../../../docs/behavior.md#window-inspection) and [Other queries](../../../../docs/behavior.md#other-queries).

## Sub-features

- `request:dispatch` runs a named Dispatcher with its arguments and replies `ok`, or `error:` for an unknown name.
- `request:clients` lists the windows Winland manages, or replies `no open windows`.
- `request:workspaces` lists the Workspaces.
- `request:activeworkspace` reports the focused Workspace and how many windows it holds.
- `request:activewindow` reports the focused window, or replies `Invalid` when there is none.
- `request:inspect` reads a Window under the pointer or a selected Client without changing it, and reports why it is managed or floating.
- `request:events` prints every event as it happens, one per line.
- `dispatcher-names` lists the invoked binary's Dispatcher names when `winland dispatch` has no name, without a Daemon.
- `version` prints the invoked binary's version, such as `winland 0.0.0 (a1b2c3d)`, without a Daemon.
- `command-errors` writes a failed reply to standard error, starting with `error:`, and exits 1.

## How to get to it (user POV)

- Run `winland dispatch <name> [args]`, such as `winland dispatch workspace 2`.
- Run `winland clients`, `winland activewindow`, `winland workspaces` or `winland activeworkspace`. Add `-j` for JSON.
- Run `winland [-j] version` for the version of the `winland` you ran. It needs no running Winland.
- Run `winland [-j] inspect` for the Window under the pointer, or `winland [-j] inspect <selector>`, such as `winland inspect title:My Doc`, for a Client.
- Run `winland events` to follow the Event stream.
- Run `winland [-j] dispatch` with no name to list the Dispatcher names.
- Set `WINLAND_PIPE` to talk to a Daemon started with that pipe name instead of `\\.\pipe\winland-<SessionId>`.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature ipc`. It prints `== feature ipc: passed`.
- **Empty replies.** With no window open, `winland clients` replies `no open windows`, `winland activewindow` replies `Invalid`, and `winland -j activewindow` replies `{}`.
- **Open a window.** The driver starts `winland events` and opens `WinlandVerify 1`. It becomes a Client, and the Event stream holds an `openwindow` line with the window's address, without `0x`, followed by Workspace `1`.
- **Queries.** The driver focuses the window with `winland dispatch focuswindow address:<hwnd>`. `winland clients` names `WinlandVerify 1`, `winland -j activewindow` reports title `WinlandVerify 1`, `winland -j activeworkspace` reports `windows` `1`, and `winland -j workspaces` lists id `1`.
- **Inspect a Client.** The driver runs `winland -j inspect address:<hwnd>`. It reports `status` `found`, `target` `selector`, `managed` `true` and `class` `WinlandVerifyWindow`.
- **Inspect a missing Client.** The driver runs `winland -j inspect title:NoSuchWindow`. It exits 0 and reports `status` `not_found` and `reason` `no_client`.
- **Malformed selector.** The driver runs `winland inspect pid:abc`. It exits 1, and standard error starts with `error: invalid selector`.
- **Unknown Dispatcher.** The driver runs `winland dispatch nosuchdispatcher`. It exits 1, and standard error starts with `error:`.
- **List Dispatchers.** The driver runs `winland dispatch` with no name. It exits 0 and prints one name per line, `workspace` among them.
- **Version without a Daemon.** The driver stops Winland. `winland clients` now exits 1, and `winland version` exits 0 with one line, `winland <version> (<commit>)`.

## Gotchas

- With no window, `clients` replies `no open windows` and `activewindow` replies `Invalid`. With `-j` they reply `[]` and `{}`. Assert on the form you asked for.
- An inspection that finds no Window succeeds with exit 0. Only a malformed selector exits 1. Check `status`, not the exit code.
- `winland inspect` with no selector reads whatever Window is under the pointer now. The driver inspects only by selector, so it does not reach pointer inspection.
- `winland dispatch` with no name and `winland version` need no Daemon and describe the binary you ran, not the running Daemon. They prove nothing about the pipe.
- The CLI talks to the pipe `WINLAND_PIPE` names, or the session's default pipe when it is unset. Run every command from the shell that holds the test Daemon's `WINLAND_PIPE`, or you talk to the user's own Winland.
- The CLI gives up after 10 seconds and reports `Winland is not answering.` An `events` stream has no deadline, but a subscriber 256 lines behind is disconnected.
