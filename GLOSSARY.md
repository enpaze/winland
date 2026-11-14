# Winland

Winland is a keyboard-driven tiling window manager for Windows 11.

## Language

**Window**:
A top-level OS window, whether or not Winland manages it.
_Avoid_: Client, HWND, app, tile

**Managed**:
The state of a Window that Winland places, sizes, focuses and closes. A Window that no rule adopts stays unmanaged and is left alone.

**Window rule**:
A config entry that matches Windows by what they are and says how they open: managed or not, floating or tiled, and where.
_Avoid_: Prop, window property

**Layout**:
The algorithm that places the managed Windows of a Workspace. Winland ships floating, dwindle and scrolling.
_Avoid_: Tiling mode, arrangement

**Workspace**:
A numbered set of managed Windows on one Monitor that Winland shows or hides together. Each Monitor has its own Workspaces 1 to 10, so Workspace 3 means the third Workspace of the focused Monitor. Owned by Winland, not by Windows virtual desktops.
_Avoid_: Virtual desktop, named workspace

**Shown workspace**:
The Workspace a Monitor shows now. Each Monitor has exactly one Shown workspace, and a Workspace is shown on at most one Monitor.
_Avoid_: Active workspace, current workspace

**Guest**:
A Workspace shown on a Monitor other than its own, because its own Monitor was unplugged. It is numbered after that Monitor's own Workspaces, and returns under its old number when its Monitor comes back.

**Scratchpad**:
The one set of managed Windows that belongs to no Monitor and no Workspace. It opens over the focused Monitor's Shown workspace and closes again, keeping its Windows out of sight until it is next opened.
_Avoid_: Special workspace, named scratchpad

**Focused window**:
The managed Window that gets keyboard input. Each Workspace remembers the Window it last focused, which gets focus back when that Workspace is shown again.
_Avoid_: Active window, focused client

**Monitor**:
A display Windows reports, recognized by its make, model and serial. It shows one of its Workspaces at a time, and can show the Scratchpad over it.
_Avoid_: Screen, output

**Group**:
A set of managed Windows that share one place in a Layout and show one at a time, switched like tabs.
_Avoid_: Tabbed container, stack

**Pinned**:
The state of a floating Window that shows on every Workspace of its Monitor. A child window that opens while its owner is pinned is pinned too.

**Command**:
A named request to the Daemon, with arguments, such as switching to Workspace 2 or listing the Windows. A key, a gesture, the command line and the IPC pipe all run the same Commands.
_Avoid_: Dispatcher, action, keyword

**Hotkey**:
A key or mouse button, with the modifiers held, that runs one Command.
_Avoid_: Bind, keybind, shortcut

**Option**:
A named config setting that holds one value of one type. With no line that sets it, it has its default.
_Avoid_: Setting, variable

**Daemon**:
The `Winland.Daemon` process, built as `winland`. It is the one long-running process, elevated by default: it hosts the window manager, the Windows adapter and the IPC pipe, and it starts at logon.
_Avoid_: Service

**IPC pipe**:
The named pipe that connects the Daemon to commands and scripts in the current user's Windows session. It carries Commands and the Event stream, and is the only way to extend Winland.
_Avoid_: Plugin API

**Event stream**:
The lines the Daemon writes to a program that subscribes on the IPC pipe, one event per line. Window, Workspace and Monitor events and config warnings are reported in it.

**Motion**:
One Window's animation toward its Layout rectangle, or out of sight before it is hidden or closed. A Motion always ends on the newest Layout rectangle.
_Avoid_: Tween, transition
