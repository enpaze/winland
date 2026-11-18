# Window rules

Window rules decide, as a window appears, whether Winland adopts it and how: float or tile it, put it on a Workspace, fade it, or leave it alone. A rule matches the window's executable, class and title, as [Window rule matches](../../../../docs/behavior.md#window-rule-matches) says. What a rule keeps giving a window while Winland runs is in [What a rule gives a window](../../../../docs/behavior.md#what-a-rule-gives-a-window).

## Sub-features

- `float-rule` opens a matching window as a floating Client.
- `tile-rule` adopts a matching window as a Client even though the baseline `ignore, class:*` rule also matches it.
- `opacity-rule` changes a matching window's alpha.
- `effect-chain` applies every effect of one rule, `tile; no_blur; opacity 0.5`, and warns about the one it cannot read.
- `workspace-rule` opens a matching window on the Workspace the rule names.
- `ignore-rule` leaves a window that no other rule adopts out of `winland clients`.
- `inspect-reason` explains in `winland -j inspect` that a window floats because of a rule.

## How to get to it (user POV)

- Add a `windowrule` line to `%USERPROFILE%\.winland\winland.conf`, such as `windowrule = float, class:kitty, title:Settings`, and save. Separate several effects with `;`, such as `windowrule = float; size 900 600; workspace 9, exe:Spotify.exe`. Winland reloads the config on save. The tray menu opens the config.
- Match on `exe`, `class` and `title`. Each compares the whole field without case, and `*` matches any run of characters.
- Run `winland -j inspect` to explain the Window under the pointer, or `winland -j inspect address:<hwnd>` for a Client. It offers a draft rule.
- Run `winland configerrors` to see the rules Winland warned about and skipped.

## Driving it with drive.ps1

Preconditions:

- The baseline in [README.md](./README.md) holds, and `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Doctor` prints `doctor: ok`.
- The driver's config adds these lines to the baseline config:
  - `windowrule = float, class:WinlandVerifyWindow, title:WinlandRule Float`
  - `windowrule = workspace 4, class:WinlandVerifyWindow, title:WinlandRule Away`
  - `windowrule = tile; no_blur; opacity 0.5, class:WinlandVerifyWindow, title:WinlandRule Faint`

- **Run the driver.** Run `pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1 -Feature window-rules`. It prints `== feature window-rules: passed`.
- **Open three windows.** The driver opens `WinlandIgnored`, `WinlandRule Float` at 300 by 200, and `WinlandRule Faint`, all of class `WinlandVerifyWindow`.
- **Float.** The `WinlandRule Float` Client reports `floating: true` in `winland -j clients`.
- **Opacity.** `WinlandRule Faint` has an alpha between 120 and 135 out of 255, so it is half transparent.
- **Ignore.** `WinlandIgnored` matches only the baseline `ignore` rule. It stays out of `winland -j clients` for half a second.
- **Tile.** The `WinlandRule Faint` Client reports Workspace `1`.
- **Effect chain.** The same rule tiled and faded `WinlandRule Faint`, and `winland configerrors` holds `Unsupported window rule 'no_blur'.`
- **Workspace.** The driver opens `WinlandRule Away`. Its Client reports Workspace `4`.
- **Inspect.** The driver runs `winland -j inspect address:<hwnd>` on the floating window. The reply holds `status: found`, `floating: true` and `floatingReason: rule`.

## Gotchas

- A field matches the whole value. `title:Notes` does not match a window titled `Release Notes`, and `title:*Notes` does. A test window whose rule misses its title falls to the baseline `ignore` rule and never becomes a Client.
- `float` and `workspace` apply only as the window appears. A reload does not change them for an open window, so open a new window to test a changed rule.
- An unsupported effect warns and the rule's other effects still apply. An unsupported field, a rule with no field, and Hyprland's `match:class kitty, float on` syntax each warn and the whole rule is skipped. Check `winland configerrors` when a rule seems to do nothing.
- The driver does not reach `pin`, `no_focus`, `fullscreen`, `maximize`, `center`, `monitor`, `size`, `move`, `exe:` matches, or a rule matched again after a class or title change.
