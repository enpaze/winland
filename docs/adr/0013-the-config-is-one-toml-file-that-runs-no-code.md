# The config is one TOML file, and it runs no code

Status: Proposed. The config is not TOML yet; [#462](https://github.com/enpaze/winland/issues/462) tracks it. Today it is one line-based `winland.conf`.

Winland reads `%USERPROFILE%\.winland\config.toml`, or the file `start --config` names. That one file holds every Option, Window rule and Hotkey. It includes no other file and has no variables. Nothing in it runs inside Winland. Logic that needs a program, such as reacting to events, is a script that the `startup` list starts and that talks to the IPC pipe (ADR 0011). The file is also the only way to configure Winland: no Command changes an Option or a Window's rule effects while it runs.

Options have built-in defaults. Hotkeys do not: the file holds every one, so deleting a line removes that Hotkey, and Winland never rewrites a file the user has edited. On first start, when the default file is absent or empty, Winland writes a commented starter. A path given with `--config` never has defaults written to it, and neither does `winland check`. All config reads and the starter write use the shell user's file rights (ADR 0007).

The format is TOML, and not a format of Winland's own, so that an editor highlights it, a published schema completes Option names and underlines mistakes, and a user has no syntax to learn. The cost is that one syntax error makes the whole file unreadable, which ADR 0010 answers.

The config runs no code because it is a file the user edits with any editor (ADR 0007), and Winland runs elevated by default (ADR 0004). A config that ran code, in Lua or C#, would let any program the user runs gain administrator rights by writing it, unless the code ran in a sandbox. .NET has none for C#, and a C# config would also need the .NET SDK on the user's machine, or a compiler inside Winland.
