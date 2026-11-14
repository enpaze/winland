# Every name is the word a Windows user would say

Status: Proposed. The names are not changed yet; [#462](https://github.com/enpaze/winland/issues/462) tracks it.

Every name a user reads or types is the plain word for the thing: `close`, `focus left`, `maximize`, `move-to-workspace 3`, `[border] color-focused`. Commands, Options, Window rule effects and events are all kebab-case. An action has one name, and an argument is a word, such as `left` or `follow`, never a letter or a mode number. An Option is named for what it turns on, never for what it turns off. A chord is written as Windows writes it, `Win+Shift+Q`, with the name printed on the key.

Winland began with Hyprland's names: `killactive`, `movetoworkspacesilent`, `general:col.active_border`, `SUPER SHIFT, Q`. They were kept because renaming them would have cost every user a rewrite. That cost was accepted once, with no migration, because the user Winland is for has never seen Hyprland (ADR 0008). For that user a borrowed name has to be looked up every time, and a plain one can be guessed.

A name is not kept for familiarity with another window manager, and no alias is added for one.
