# Update

`winland update` replaces an installed copy with the newest release on one channel: stable, the newest published release, or dev, a rolling prerelease built from `main`. It checks the download's SHA-256, stops a running Winland, replaces the files, starts the new version when Winland was running, and keeps the config. A file still open during the replace is renamed to end in `.winland-old` and removed with no error once it is free, including on the next run of that copy. When the release is the running build, it says Winland is up to date and changes nothing. The rules are in [Daemon](../../../../docs/behavior.md#daemon).

## Sub-features

- `install:Update` replaces the install folder `winland` was run from with the newest release on the saved channel, through `winland update`, or saves a channel first and then updates, through `winland update --channel stable` or `winland update --channel dev`.

## How to get to it (user POV)

- Run `winland update` to update on the saved channel, which is stable when `%USERPROFILE%\.winland\channel` is missing.
- Run `winland update --channel stable` or `winland update --channel dev` to switch channel, remember the choice, and update.
- Run the one-line install again. On an installed Winland, `scripts/install.ps1` runs `winland update`. With `-Channel dev` it runs `winland update --channel dev`.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- `WINLAND_VERIFY_SYSTEM=1` is set. Without it the driver skips, and a driver whose every test skipped fails.
- Winland is not installed in `%LOCALAPPDATA%\Programs\Winland` or `C:\Program Files\Winland`, Settings › Apps does not list it for this user, and `%USERPROFILE%\.winland\windows-settings.json` does not exist.
- The PC can reach GitHub, because the driver updates from the dev release there.
- The driver starts no Winland of its own, so it adds no lines to the baseline config.

- **Run the driver.** Run `$env:WINLAND_VERIFY_SYSTEM = '1'`, then `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature update`. It prints `== feature update: passed`.
- **Install for this user.** The driver runs the built `winland install --user --no-logon --no-start`. It exits 0 and prints `Winland is installed for this user`. The driver notes the version Settings › Apps shows.
- **Update to dev.** The driver runs the installed `winland update --channel dev`. It exits 0, and `%USERPROFILE%\.winland\channel` holds `dev`.
- **Replaced or up to date.** When the output says `Winland is updated to <version>.`, Settings › Apps shows that version and `winland.exe` is still in the install folder. Otherwise the output holds `Winland is up to date.`, because this build is the dev release's commit.
- **Quiet leftovers.** The update output and error contain neither `UnauthorizedAccessException` nor `Exception calling "Delete"`. The driver then runs the installed `winland check` on a missing file. That run's output and error omit those same strings, and the install folder has no file whose name contains `.winland-old`.
- **Uninstall.** The driver runs the installed `winland uninstall`. It prints `Winland is uninstalled.`, and the install folder is gone.
- **Put the channel back.** The driver writes back the `channel` file it found, or deletes it when there was none.

## Gotchas

- The install, update and tweaks drivers change this user's install, `PATH` and Windows settings and put them back. This one installs Winland for this user, updates it, and uninstalls it again.
- The driver does not reach the stable channel. It updates from the dev release on GitHub.
- The driver updates only a per-user install. It does not reach an update of the copy in `C:\Program Files\Winland`, which asks for UAC approval.
- When this build is the dev release's commit, update downloads nothing, so the run does not prove a replacement or the removal of a locked `.winland-old` file. Read which of the two lines it printed before you report a replace as verified.
- The run needs GitHub. A used-up GitHub API limit fails the update, and a checksum mismatch can mean the dev release is still uploading. Try again in a minute.
- An existing install fails the driver at its start instead of being replaced. Uninstall your own Winland first, or run the driver on a PC or account without one, as CI does.
