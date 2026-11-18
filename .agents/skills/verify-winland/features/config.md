# Config

Winland reads one config file and the files it `source`s, reloads them when one is saved, and lists each warning with its file and line. `winland check` validates a draft without a Daemon. The rules are in [Config files](../../../../docs/behavior.md#config-files).

## Sub-features

- `request:reload` reads the config again and replies `ok`.
- `request:configerrors` lists each config warning as `file:line: message`, or replies `no config errors`. With `-j` it lists `{"file","line","message"}` objects.
- `request:files` reports the config Winland read and the log it writes, each as a full path.
- `config-check` validates a file and its sources with `winland check [file]`, without a Daemon, and exits 0 when clean, 1 for diagnostics or a read failure, and 2 for invalid usage.
- `reload-on-save` reloads the config when it is saved, applies the new values and reports `configreloaded` on the Event stream.
- `unknown-keyword-warning` warns on an unknown Option, ends the warning `Did you mean '<option>'?` when one Option is within two edits, and loads the rest.

## How to get to it (user POV)

- Edit `%USERPROFILE%\.winland\winland.conf`. Winland writes it with comments the first time it starts.
- Start Winland with `winland --config <file>` to read that `.conf` file instead.
- Save the config, or a file it sources, to reload it. Run `winland reload`, or choose Reload from the tray menu, to read it again by hand.
- Choose Edit config from the tray menu, or click the config-errors notification, to open the file.
- Run `winland check [file]` to validate a draft or the default config.
- Run `winland configerrors` to list the warnings, and `winland files` to show the config and log paths. Add `-j` for JSON.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds `general:gaps_out = 0` to the baseline config.

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature config`. It prints `== feature config: passed`.
- **Check a clean file.** The driver writes `good.conf`, which sets `general:gaps_in = 3` and has `source = ./extra.conf`, and runs `winland check good.conf`. It exits 0 and prints nothing.
- **Check a file with a typo.** The driver writes `bad.conf` with `general:gaps_inn = 4` on line 2 and runs `winland check bad.conf`. It exits 1, and standard error holds `bad.conf:2:` and `Did you mean 'general:gaps_in'?`.
- **Invalid usage and a missing file.** `winland check good.conf bad.conf` exits 2. `winland check missing.conf` exits 1.
- **Files and errors.** `winland -j files` reports the run's config path as `config`. `winland configerrors` replies `no config errors`.
- **Open a window.** The driver opens `WinlandVerify 1`. It becomes a Client and fills the work area, because `gaps_out` is 0.
- **Save the config.** The driver starts `winland events` and appends `general:gaps_out = 40` and `general:nosuchoption = 1` to the config. The Event stream holds `configreloaded`, and the window moves in 40 pixels from the left and right of the work area.
- **List the warning.** Once `configreloaded` arrives, before the window moves, `winland configerrors` names `general:nosuchoption`. `winland -j configerrors` holds an entry whose `file` is the run's config path, whose `line` is above 0, and whose `message` names `general:nosuchoption`.
- **Reload by hand.** The driver runs `winland reload`. It replies `ok`.

## Gotchas

- `winland check` needs no Daemon and changes no running configuration. A clean check prints nothing, so check the exit code. It takes at most one file; two are invalid usage and exit 2.
- No pipe request changes an Option. The config file is the only way to configure Winland, so the driver changes `gaps_out` by saving the file.
- The driver appends to its own config under `%TEMP%\winland-verify-<guid>`. Never edit `%USERPROFILE%\.winland\winland.conf` to test a reload.
- After `winland reload`, the driver waits for `configreloaded` again, but the line from the save already matches. Only the `ok` reply proves the hand reload.
- The driver saves only the config itself, not a file it sources, so it does not reach a reload from a sourced file.
- `configerrors` writes `file:line: message`, and `-j configerrors` writes objects with `file`, `line` and `message`. Match the form you asked for.
- The driver does not check the config-errors notification or the click that opens the file.
