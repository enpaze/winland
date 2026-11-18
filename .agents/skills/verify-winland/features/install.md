# Install

A user installs Winland for every user of the PC, with one administrator approval, or for themselves only, without one. An install copies Winland to its folder, adds that folder to `PATH`, adds a Start menu shortcut and a Settings › Apps entry, and can start Winland at logon. `winland uninstall` removes all of that and keeps `%USERPROFILE%\.winland` unless `--purge` is given. The rules are in [Daemon](../../../../docs/behavior.md#daemon).

## Sub-features

- `install:Machine` copies Winland to `C:\Program Files\Winland` with one UAC approval and adds the logon task, through `winland install`.
- `install:ForUser` copies Winland to `%LOCALAPPDATA%\Programs\Winland` without approval and starts it at logon from the user's Run key, through `winland install --user`.
- `install:UserCleanup` removes a per-user install after the machine install succeeds, so logon starts only one, through `winland install --winland-cleanup-user`, which `scripts/install.ps1` runs from the installed copy in a normal terminal.
- `install:Remove` stops Winland and removes the install, its shortcut, its Settings › Apps entry and its `PATH` entry, through `winland uninstall` or `winland uninstall --purge`.

## How to get to it (user POV)

- Run `irm https://raw.githubusercontent.com/enpaze/winland/main/scripts/install.ps1 | iex` in PowerShell. It asks whether to start at sign-in, start now, and turn off Snap and touchpad swipes.
- Run `& ([scriptblock]::Create((irm https://raw.githubusercontent.com/enpaze/winland/main/scripts/install.ps1))) -Channel dev` to install the dev release.
- Download `Winland-win-x64.zip` from Releases, unzip it and run `winland\winland.exe install`.
- Run `winland install` for the PC or `winland install --user` for this user only. Add `--no-logon` to skip starting at logon, or `--no-start` to leave Winland stopped.
- Run `winland uninstall`, or uninstall Winland from Settings › Apps. Run `winland uninstall --purge` to delete `%USERPROFILE%\.winland` too.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- `WINLAND_VERIFY_SYSTEM=1` is set. Without it the driver skips, and a driver whose every test skipped fails.
- Winland is not installed in `%LOCALAPPDATA%\Programs\Winland` or `C:\Program Files\Winland`, Settings › Apps does not list it for this user, and `%USERPROFILE%\.winland\windows-settings.json` does not exist.
- The driver starts no Winland of its own, so it adds no lines to the baseline config.

- **Run the driver.** Run `$env:WINLAND_VERIFY_SYSTEM = '1'`, then `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature install`. It prints `== feature install: passed`.
- **Install for this user.** The driver runs the built `winland install --user --no-logon --no-start`. It exits 0 and prints `Winland is installed for this user`. `%LOCALAPPDATA%\Programs\Winland\winland.exe` exists, the Start menu holds `Winland.lnk`, Settings › Apps lists Winland, the install folder is on the user `PATH`, and the Run key has no `Winland` entry.
- **Run the installed copy.** The driver runs the installed `winland check <empty file>`. It exits 0.
- **Uninstall.** The driver runs the installed `winland uninstall`. It exits 0 and prints `Winland is uninstalled.` Within 30 seconds the install folder is gone, and the shortcut, the Settings › Apps entry and the `PATH` entry are gone too.

## Gotchas

- The install, update and tweaks drivers change this user's install, `PATH` and Windows settings and put them back. This one installs Winland for this user and uninstalls it again.
- The driver does not reach the machine install, which needs UAC approval and the native launcher a release package holds, or the one-line `scripts/install.ps1`, which `scripts/install.tests.ps1` covers. It also does not reach `winland install --winland-cleanup-user`, `winland uninstall --purge`, or a start at logon.
- An existing install fails the driver at its start instead of being replaced. Uninstall your own Winland first, or run the driver on a PC or account without one, as CI does.
- In an elevated shell the driver runs every `winland` command as a standard user, and its output says `(as a standard user)`.
- Uninstall's helper removes the install folder only after `winland` has exited, so the folder lingers for a moment. Wait for it, as the driver does, before you call the uninstall incomplete.
