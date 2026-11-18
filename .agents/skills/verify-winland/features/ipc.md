# IPC

The `winland` CLI talks to the running Daemon over the IPC pipe. A user runs a Dispatcher with `winland <action> [args]`, asks for the Clients, the focused window and the Workspaces with `winland get`, inspects a Window without changing it, and follows the Event stream. Failed commands answer `error:` and exit 1. The rules are in [IPC pipe](../../../../docs/behavior.md#ipc-pipe), [Window inspection](../../../../docs/behavior.md#window-inspection) and [Other queries](../../../../docs/behavior.md#other-queries).

## Sub-features

- `action-command` runs a Dispatcher named by the first word with the rest as its arguments and replies `ok`, or `error:` for `resize` with no argument, which only a mouse drag runs.
- `unknown-command` prints the usage to standard error and exits 2 for a first word that is no Dispatcher and no other command.
- `request:get windows` lists the windows Winland manages, or replies `no open windows`.
- `request:get workspaces` lists the Workspaces.
- `request:get workspace` reports the focused Workspace and how many windows it holds.
- `request:get window` reports the focused window. With none, the text reply is empty and the JSON reply is `null`, and the CLI exits 0.
- `request:inspect` reads a Window under the pointer or a selected Client without changing it, and reports why it is managed or floating.
- `request:events` prints every event as it happens, one `name>>data` line each. With `-j` each line is one JSON object, `{"event":"<name>", ...}`.
- `help` prints the usage with every Dispatcher name of the invoked binary for `winland help` and `winland --help`, without a Daemon, and exits 0.
- `version` prints the invoked binary's version, such as `winland 0.0.0 (a1b2c3d)`, without a Daemon.
- `command-errors` writes a failed reply to standard error, starting with `error:`, and exits 1.

## How to get to it (user POV)

- Run `winland <action> [args]`, such as `winland workspace 2`.
- Run `winland get windows`, `winland get window`, `winland get workspaces` or `winland get workspace`. Add `-j`, before or after the command, for JSON.
- Run `winland [-j] version` for the version of the `winland` you ran. It needs no running Winland.
- Run `winland [-j] inspect` for the Window under the pointer, or `winland [-j] inspect <selector>`, such as `winland inspect title:My Doc`, for a Client.
- Run `winland [-j] events` to follow the Event stream.
- Run `winland help` to list the commands and the Dispatcher names.
- Set `WINLAND_PIPE` to talk to a Daemon started with that pipe name instead of `\\.\pipe\winland-<SessionId>`.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds no lines to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature ipc`. It prints `== feature ipc: passed`.
- **Empty replies.** With no window open, `winland get windows` replies `no open windows`, `winland get window` prints nothing and exits 0, and `winland -j get window` replies `null`.
- **Open a window.** The driver starts `winland -j events` and opens `WinlandVerify 1`. It becomes a Client, and the Event stream holds an `openwindow` JSON object with the window's `address`, with `0x`, and `workspace` id `1`. The new window takes focus, so the stream also holds a `focus` JSON object whose `window` has the window's `address`, `title` `WinlandVerify 1` and `class` `WinlandVerifyWindow`.
- **Queries.** The driver focuses the window with `winland focus address:<hwnd>`. `winland get windows` names `WinlandVerify 1`, `winland -j get window` reports title `WinlandVerify 1`, `winland -j get workspace` reports `windows` `1` and id `1`, and `winland -j get workspaces` lists id `1`. `winland get window -j` answers as `winland -j get window` does. The window's `monitor` is the Workspace's `monitor` name, and the two replies have exactly the camelCase keys in [IPC pipe](../../../../docs/behavior.md#ipc-pipe).
- **Inspect a Client.** The driver runs `winland -j inspect address:<hwnd>`. It reports `status` `found`, `target` `selector`, `managed` `true` and `class` `WinlandVerifyWindow`.
- **Inspect by pattern.** The driver runs `winland -j inspect winlandverify*`, a class pattern in lower case, and `winland -j inspect exe:<EXE>`, the driver process's file name in upper case. Each reports the window's address.
- **Inspect a missing Client.** The driver runs `winland -j inspect title:NoSuchWindow`. It exits 0 and reports `status` `not_found` and `reason` `no_client`.
- **Malformed selector.** The driver runs `winland inspect pid:abc`. It exits 1, and standard error starts with `error: invalid selector`.
- **Unknown command.** The driver runs `winland nosuchcommand`, `winland dispatch workspace 2` and `winland clients`. Each exits 2, prints nothing to standard output, and writes the usage, starting `usage: winland`, to standard error.
- **Drag outside a drag.** The driver runs `winland resize`. It exits 1, and standard error is `error: resize is a drag; bind it to a mouse button`.
- **Help.** The driver runs `winland help`, `winland --help` and `winland help -j`. Each exits 0, writes nothing to standard error, and prints an `actions:` line that names `workspace` and `reload`.
- **Version without a Daemon.** The driver stops Winland. `winland get windows` now exits 1, `winland version` exits 0 with one line, `winland <version> (<commit>)`, and `winland -j version` exits 0 with JSON whose `version` is the same and whose keys are `version`, `commit` and `dirty`.

## Gotchas

- With no window, `get windows` replies `no open windows` and `get window` prints nothing. With `-j` they reply `[]` and `null`. Assert on the form you asked for.
- An inspection that finds no Window succeeds with exit 0. Only a malformed selector exits 1. Check `status`, not the exit code.
- `winland inspect` with no selector reads whatever Window is under the pointer now. The driver inspects only by selector, so it does not reach pointer inspection.
- `winland help` and `winland version` need no Daemon and describe the binary you ran, not the running Daemon. They prove nothing about the pipe.
- The CLI talks to the pipe `WINLAND_PIPE` names, or the session's default pipe when it is unset. Run every command from the shell that holds the test Daemon's `WINLAND_PIPE`, or you talk to the user's own Winland.
- The CLI gives up after 10 seconds and reports `Winland is not answering.` An `events` stream has no deadline, but a subscriber 256 lines behind is disconnected.
