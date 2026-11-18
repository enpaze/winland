# Tweaks

`winland tweaks --off` turns Windows Snap off, so Snap by keyboard or from the snap layouts flyout cannot move a tile over its neighbours, which Winland does not see as a move. `--touchpad` also turns the three- and four-finger touchpad swipes off. Winland records each old value first, and `winland tweaks --restore` or `winland uninstall` puts them back. The rules are in [Daemon](../../../../docs/behavior.md#daemon).

## Sub-features

- `install:Tweaks` records the old values in `%USERPROFILE%\.winland\windows-settings.json` and turns Snap off, through `winland tweaks --off`, and the touchpad swipes too, through `winland tweaks --off --touchpad`.
- `install:RestoreTweaks` puts back the recorded Snap and touchpad values and removes the record, through `winland tweaks --restore` from a normal terminal.

## How to get to it (user POV)

- Answer yes to the install script's question about Windows Snap and touchpad workspace swipes. `-NoTweaks` leaves them alone without asking.
- Run `winland tweaks --off`, or `winland tweaks --off --touchpad` to turn the swipes off too.
- Run `winland tweaks --restore` from a normal terminal to put them back without uninstalling.
- Run `winland uninstall`, which puts back every recorded value.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- `WINLAND_VERIFY_SYSTEM=1` is set. Without it the driver skips, and a driver whose every test skipped fails.
- Winland is not installed in `%LOCALAPPDATA%\Programs\Winland` or `C:\Program Files\Winland`, Settings › Apps does not list it for this user, and `%USERPROFILE%\.winland\windows-settings.json` does not exist.
- The driver starts no Winland of its own, so it adds no lines to the baseline config.

- **Run the driver.** Run `$env:WINLAND_VERIFY_SYSTEM = '1'`, then `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature tweaks`. It prints `== feature tweaks: passed`.
- **Turn Snap on first.** The driver turns Snap on, so the restore must turn it back on, and notes Snap and the touchpad values `ThreeFingerSlideEnabled` and `FourFingerSlideEnabled` under `HKCU\Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad`.
- **Turn them off.** The driver runs the built `winland tweaks --off --touchpad`. It exits 0. Within 5 seconds Snap is off and both touchpad values are 0, and `%USERPROFILE%\.winland\windows-settings.json` exists.
- **Restore.** The driver runs the built `winland tweaks --restore`. It exits 0. Within 5 seconds Snap and both touchpad values are as they were before, and `windows-settings.json` is gone.
- **Put Snap back.** If the record is still there, the driver runs `winland tweaks --restore` again. It then sets Snap as it was before the driver started.

## Gotchas

- The install, update and tweaks drivers change this user's install, `PATH` and Windows settings and put them back. This one changes Snap and the touchpad swipes for this user and puts them back.
- A `windows-settings.json` left from an earlier run fails the driver at its start, so it never overwrites values it did not record. Run `winland tweaks --restore` from a normal terminal to put them back and remove the record.
- `winland tweaks --restore` refuses in an elevated terminal. In an elevated shell the driver runs every `winland` command as a standard user.
- A touchpad value that was absent is recorded as absent and removed again, not written as a guessed number. On a PC without those values, the restore leaves them absent.
- `--restore` cannot be combined with `--off` or `--touchpad`.
- The driver does not reach `winland tweaks --off` without `--touchpad`, the install script's question, or the restore that `winland uninstall` does.
