# A bad key is skipped, and a broken file changes nothing

Status: Proposed. The config is not TOML yet; [#462](https://github.com/enpaze/winland/issues/462) tracks it. Today an unknown or malformed line warns and is skipped.

An unknown table or key, a value of the wrong type, or an unknown Command in a Hotkey produces a warning and is skipped. The rest of the config still applies. A file that cannot be parsed as TOML applies nothing: on a reload the last good config keeps running, and at startup Winland runs its built-in defaults, which include the starter's Hotkeys. Either way one Windows notification shows the count and the first problem with its file and line, and a later load replaces it. `winland check` and the Event stream list every warning.

The config reloads when it is saved, so it is often read half-edited. Refusing every setting over one typo would drop every Hotkey until the typo is found, and a user with no Hotkeys cannot easily reach the editor to fix it. That is why a bad key costs only itself.

A TOML file with a syntax error has no keys to skip, so the same reasoning gives a different rule there. Keeping the last good config means a half-typed save changes nothing on screen. Starting on the defaults means a broken file never leaves the keyboard dead.
