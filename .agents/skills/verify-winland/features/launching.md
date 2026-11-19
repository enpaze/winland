# Launching

Winland starts programs for the user. An `exec-once` line runs each time Winland starts, and the `exec` Dispatcher runs a command from a bind or the CLI. Both run the command through `cmd.exe /d /s /c`, so `&&` and redirection work, and the program runs as the normal user. The rules are in [Launch lines](../../../../docs/behavior.md#launch-lines) and [Launching](../../../../docs/behavior.md#launching).

## Sub-features

- `dispatcher:exec` runs a command line through `cmd.exe`, with `&&` and redirection, and opens its window on the Workspace it was started on.
- `exec-once` runs a command through `cmd.exe` each time Winland starts.
- `workspace-block` opens the first window of a launch line that starts with `[workspace N]` on Workspace N, without switching to it.
- `rule-block` skips a launch line with any other rule block, with the warning `Unsupported rule block. Only [workspace N] is supported.`, and does not launch it.

## How to get to it (user POV)

- Press `Win+Enter` to open Windows Terminal, `Win+Shift+Enter` or `Win+Shift+B` to open the browser, `Win+Shift+F` to open File Explorer, or `Win+Shift+N` to open Notepad. The starter config binds these to `exec`.
- Add `exec-once = <command>` to the config. It runs each time Winland starts. Add `exec-once = [workspace 2] <command>` to open its first window on workspace 2.
- Run `winland exec <command>`, such as `winland exec "notepad C:\a b.txt"`.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver makes a new folder `%TEMP%\winland-launch-<guid>` and deletes it at the end. Its config adds these lines to the baseline config, with `<folder>` that folder:
  - `exec-once = echo started> "<folder>\once.txt"`
  - `exec-once = [float] echo skipped> "<folder>\once.txt.block"`
  - `exec-once = [workspace 3] powershell -NoProfile -EncodedCommand <script>`, where the script writes its process id to `<folder>\form.pid` and shows a Windows Forms window titled `WinlandLaunched <guid>`
  - `windowrule = tile, title:WinlandLaunched <guid>`

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature launching`. It prints `== feature launching: passed`.
- **exec-once at start.** Winland starts. `once.txt` appears and holds `started`, so `exec-once` ran its command through cmd.
- **Rule block.** `once.txt.block` does not appear for half a second, so the line with `[float]` did not launch.
- **Workspace block.** Within 30 seconds, `winland -j get windows` lists the `WinlandLaunched <guid>` window on workspace `3`, and `winland -j get workspace` still answers workspace 1. The driver then ends the PowerShell process whose id is in `form.pid`, and the window leaves the client list.
- **exec on request.** The driver runs `winland exec 'echo first&& echo second> "<folder>\exec.txt"'`, with the command line as one argument. The reply is `ok`, and `exec.txt` holds `second`, so cmd ran both commands and the redirection.

## Gotchas

- The driver does not reach dropping administrator rights, because the driver's Winland runs unelevated.
- Pass a command line with `&&` or `>` as one quoted argument, as the driver does. Unquoted, the shell you type in reads them before `winland` sees them.
- A console program started through `exec` has no window. The driver's `echo` commands show nothing, so it checks the files they write. To see a console program, start it inside a terminal, as in `exec-once = wt pwsh`.
- A single-instance program, such as `explorer`, Chrome, VS Code, Office or a running Windows Terminal, hands the request to its running copy. Its window opens in a process Winland did not start, on the current Workspace.
- `exec-once` runs once each time Winland starts. Restart Winland to run it again.
- The `[workspace N]` hold lapses 10 seconds after the launch. A program that takes longer to show its first window opens on the current Workspace.
