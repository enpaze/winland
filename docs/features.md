# Features

Every user-facing feature of Winland, where it lives in the code, and the command that drives it on a real desktop and checks what a user would see. [behavior.md](behavior.md) holds the full rules for each one.

Run one driver with `pwsh -NoProfile -File scripts/smoke.ps1 -Feature <id>`, or every driver with no `-Feature`. Each driver starts its own Winland on its own pipe, with a config that ignores every window but its test windows. It opens those windows, sends real keys, mouse input and CLI commands, and checks the windows, the replies and the events. It then stops Winland and checks that every test window is visible again and the taskbar is back. Stop your own Winland first, because a driver will not start beside one. The `install`, `update` and `tweaks` drivers change this user's install, `PATH` and Windows settings and then put them back, so they run only with `WINLAND_SMOKE_SYSTEM=1`, as CI sets it. On an elevated runner they run Winland's CLI as a standard user.

`FeatureMapTests` in `tests/Winland.Daemon.Tests` fails when this map and the code disagree. Every Dispatcher, pipe request, install command and `##` section of `behavior.md` belongs to a feature, and every feature has one driver class in `tests/Winland.Smoke.Tests/Features`, tagged `[Trait("Feature", "<id>")]`. CI runs every driver.

A feature lists its fields as `- Name: value`. The fields are `Spec`, `Code`, `Dispatchers`, `Requests`, `Install commands`, `Driver` and `Not driven`. `Not driven` names what the driver cannot reach on a desktop.

## workspaces

Show another Workspace, and move windows onto other Workspaces, with or without following them.

- Spec: [Dispatchers](behavior.md#dispatchers)
- Code: [Workspaces.cs](../src/Winland.Application/WindowManager/Workspaces.cs), [Workspace](../src/Winland.Domain/Workspace)
- Dispatchers: `workspace`, `movetoworkspace`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature workspaces`

## layouts

Tile windows in the dwindle, scrolling or floating Layout, flip splits, resize tiles, and switch Layouts.

- Spec: [Layout](behavior.md#layout), [Window placement](behavior.md#window-placement)
- Code: [Layout](../src/Winland.Domain/Layout), [LayoutCycle.cs](../src/Winland.Application/WindowManager/LayoutCycle.cs), [Placement.cs](../src/Winland.Application/WindowManager/Placement.cs)
- Dispatchers: `layout`, `cyclelayout`, `layoutmsg`, `togglesplit`, `resizeactive`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature layouts`

## focus

Focus a window by selector or by direction, with the cursor moved to it.

- Spec: [Dispatchers](behavior.md#dispatchers)
- Code: [Focus.cs](../src/Winland.Application/WindowManager/Focus.cs), [FocusState.cs](../src/Winland.Domain/Focus/FocusState.cs), [Foreground.cs](../src/Winland.Infrastructure/Window/Foreground.cs)
- Dispatchers: `movefocus`, `focuswindow`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature focus`

## window-commands

Swap, move, float, pin and close the focused window.

- Spec: [Dispatchers](behavior.md#dispatchers)
- Code: [WindowDispatchers.cs](../src/Winland.Application/WindowManager/WindowDispatchers.cs), [FloatingLayer.cs](../src/Winland.Application/WindowManager/FloatingLayer.cs), [Pin.cs](../src/Winland.Application/WindowManager/Pin.cs)
- Dispatchers: `swapwindow`, `movewindow`, `togglefloating`, `pin`, `killactive`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature window-commands`

## fullscreen

Make the focused window fullscreen over its Monitor, or maximized over its work area, and back.

- Spec: [Fullscreen](behavior.md#fullscreen)
- Code: [Fullscreen.cs](../src/Winland.Application/WindowManager/Fullscreen.cs), [Maximize.cs](../src/Winland.Application/WindowManager/Maximize.cs), [FullscreenCover.cs](../src/Winland.Domain/Fullscreen/FullscreenCover.cs)
- Dispatchers: `fullscreen`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature fullscreen`

## special-workspaces

Keep windows on a special workspace that opens over the normal one and hides again.

- Spec: [Special workspaces](behavior.md#special-workspaces)
- Code: [Special.cs](../src/Winland.Application/WindowManager/Special.cs)
- Dispatchers: `togglespecialworkspace`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature special-workspaces`

## monitors

Report the Monitors Windows has, focus one, and move a Workspace between them.

- Spec: [Monitors](behavior.md#monitors)
- Code: [Monitors.cs](../src/Winland.Application/WindowManager/Monitors.cs), [Hotplug.cs](../src/Winland.Application/WindowManager/Hotplug.cs), [MonitorAdapter.cs](../src/Winland.Infrastructure/Monitor/MonitorAdapter.cs)
- Dispatchers: `focusmonitor`, `movecurrentworkspacetomonitor`
- Requests: `monitors`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature monitors`
- Not driven: on one Monitor, as on CI, moving focus and a Workspace to another Monitor, and plugging or unplugging a Monitor.

## groups

Hold several windows in one tile with a tab bar, cycle them, and take them out again.

- Spec: [Groups](behavior.md#groups)
- Code: [Groups.cs](../src/Winland.Application/WindowManager/Groups.cs), [GroupMembers.cs](../src/Winland.Application/WindowManager/GroupMembers.cs), [GroupBars.cs](../src/Winland.Application/WindowManager/GroupBars.cs), [Group](../src/Winland.Infrastructure/Group)
- Dispatchers: `togglegroup`, `moveintogroup`, `changegroupactive`, `moveoutofgroup`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature groups`

## window-rules

Float, place, make transparent or ignore windows by their executable, class and title.

- Spec: [Window rule matches](behavior.md#window-rule-matches), [What a rule gives a window](behavior.md#what-a-rule-gives-a-window)
- Code: [WindowRule](../src/Winland.Domain/WindowRule), [ClientProps.cs](../src/Winland.Domain/Client/ClientProps.cs), [DynamicRules.cs](../src/Winland.Application/WindowManager/DynamicRules.cs)
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature window-rules`

## launching

Start programs through `exec` and `exec-once`, run by `cmd.exe` as the normal user.

- Spec: [Launch lines](behavior.md#launch-lines), [Launching](behavior.md#launching)
- Code: [Dispatch.cs](../src/Winland.Application/WindowManager/Dispatch.cs), [Launch](../src/Winland.Domain/Launch), [ProcessLauncher.cs](../src/Winland.Infrastructure/Window/ProcessLauncher.cs)
- Dispatchers: `exec`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature launching`
- Not driven: dropping administrator rights, because the driver's Winland runs unelevated.

## binds

Run Dispatchers from key chords, and move focus with the pointer.

- Spec: [Modifiers](behavior.md#modifiers), [Cursor and input](behavior.md#cursor-and-input)
- Code: [Bind](../src/Winland.Domain/Bind), [KeyboardHook](../src/Winland.Infrastructure/Input/KeyboardHook), [MouseHook.cs](../src/Winland.Infrastructure/Input/MouseHook.cs), [PointerInteraction.cs](../src/Winland.Application/WindowManager/PointerInteraction.cs)
- Requests: `binds`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature binds`

## mouse-drag

Move and resize windows by dragging them with a bound mouse button.

- Spec: [Mouse drag](behavior.md#mouse-drag)
- Code: [Drag.cs](../src/Winland.Application/WindowManager/Drag.cs), [BindDrag.cs](../src/Winland.Infrastructure/Input/BindDrag.cs), [NativeMove.cs](../src/Winland.Application/WindowManager/NativeMove.cs)
- Dispatchers: `resizewindow`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature mouse-drag`

## touchpad-swipe

Switch Workspace with a three- or four-finger swipe on a precision touchpad.

- Spec: [Touchpad swipe](behavior.md#touchpad-swipe)
- Code: [Swipe.cs](../src/Winland.Application/WindowManager/Swipe.cs), [SwipePreview.cs](../src/Winland.Application/WindowManager/SwipePreview.cs), [TouchpadSwipe.cs](../src/Winland.Infrastructure/Input/TouchpadSwipe.cs)
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature touchpad-swipe`
- Not driven: the swipe itself. Windows has no API that injects precision-touchpad input, so the driver checks only which `gesture` lines Winland takes and which it warns about.

## decoration

Draw a border around each Client and make inactive Clients transparent.

- Spec: [Decoration](behavior.md#decoration)
- Code: [BorderHost.cs](../src/Winland.Infrastructure/Window/BorderHost.cs), [OpacityAdapter.cs](../src/Winland.Infrastructure/Window/OpacityAdapter.cs), [DecorationOptions.cs](../src/Winland.Domain/Config/DecorationOptions.cs)
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature decoration`

## animations

Move windows to their tiles through frames instead of at once.

- Spec: [Animations](behavior.md#animations)
- Code: [Animations.cs](../src/Winland.Application/WindowManager/Animations.cs), [Motion](../src/Winland.Infrastructure/Motion)
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature animations`

## shell-windows

Leave the taskbar and the shell's flyouts unmanaged, and hide the taskbar while Winland runs.

- Spec: [Shell windows](behavior.md#shell-windows)
- Code: [Taskbar.cs](../src/Winland.Infrastructure/Session/Taskbar.cs), [ShellFlyouts.cs](../src/Winland.Infrastructure/Session/ShellFlyouts.cs)
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature shell-windows`

## urgency

Mark a window urgent instead of switching to it when it opens or asks for focus on a hidden Workspace.

- Spec: [Urgency](behavior.md#urgency)
- Code: [Urgency.cs](../src/Winland.Application/WindowManager/Urgency.cs), [WindowLifecycle.cs](../src/Winland.Application/WindowManager/WindowLifecycle.cs)
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature urgency`

## virtual-desktops

Let go of windows on another Windows virtual desktop and take them back, in their places, on return.

- Spec: [Virtual desktops](behavior.md#virtual-desktops)
- Code: [WindowLifecycle.cs](../src/Winland.Application/WindowManager/WindowLifecycle.cs), [WindowAdapter](../src/Winland.Infrastructure/Window/WindowAdapter)
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature virtual-desktops`

## notification-area

Show an icon with a menu to reload, edit the config, open the log and quit.

- Spec: [Notification area](behavior.md#notification-area)
- Code: [Tray](../src/Winland.Infrastructure/Tray), [Tray](../src/Winland.Domain/Tray)
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature notification-area`
- Not driven: the icon's picture and tooltip, and the balloons, which no API reads back.

## ipc

Answer queries, inspect windows, and stream events on the pipe the `winland` CLI uses.

- Spec: [IPC pipe](behavior.md#ipc-pipe)
- Code: [CommandProtocol](../src/Winland.Application/Dispatch/CommandProtocol), [Queries.cs](../src/Winland.Application/WindowManager/Queries.cs), [EventStream.cs](../src/Winland.Application/WindowManager/EventStream.cs), [Inspection.cs](../src/Winland.Application/WindowManager/Inspection.cs), [Dispatch](../src/Winland.Infrastructure/Dispatch), [ControlCommand.cs](../src/Winland.Daemon/ControlCommand.cs)
- Requests: `dispatch`, `clients`, `workspaces`, `activeworkspace`, `activewindow`, `inspect`, `events`, `version`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature ipc`

## config

Read one config file and the files it sources, check a draft with `winland check`, and reload when a file is saved.

- Spec: [Config files](behavior.md#config-files)
- Code: [Config](../src/Winland.Daemon/Config), [Config](../src/Winland.Domain/Config), [ConfigWatcher.cs](../src/Winland.Infrastructure/Config/ConfigWatcher.cs), [Config.cs](../src/Winland.Application/WindowManager/Config.cs)
- Requests: `reload`, `configerrors`, `files`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature config`

## daemon

Run once per session, put hidden windows back after a crash, and exit cleanly.

- Spec: [Daemon](behavior.md#daemon)
- Code: [Program.cs](../src/Winland.Daemon/Program.cs), [Session](../src/Winland.Daemon/Session), [Recovery.cs](../src/Winland.Application/WindowManager/Recovery.cs), [InstanceLock.cs](../src/Winland.Infrastructure/Session/InstanceLock.cs)
- Dispatchers: `exit`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature daemon`
- Not driven: log off and shut down.

## install

Install Winland for this user or for the machine, and uninstall it.

- Spec: [Daemon](behavior.md#daemon)
- Code: [AppInstall.cs](../src/Winland.Install/Setup/AppInstall.cs), [InstallCommand.cs](../src/Winland.Install/Setup/InstallCommand.cs), [Setup](../src/Winland.Daemon/Setup), [install.ps1](../scripts/install.ps1)
- Install commands: `ForUser`, `Machine`, `UserCleanup`, `Remove`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature install`
- Not driven: the machine install, which needs UAC approval and the native launcher a release package holds, and the one-line `scripts/install.ps1`, which `scripts/install.tests.ps1` covers.

## update

Replace an installed copy with the newest stable or dev release.

- Spec: [Daemon](behavior.md#daemon)
- Code: [Release](../src/Winland.Install/Release), [MaintenanceCommand.cs](../src/Winland.Daemon/Setup/MaintenanceCommand.cs)
- Install commands: `Update`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature update`
- Not driven: the stable channel. The driver updates from the dev release on GitHub.

## tweaks

Turn Windows Snap and the touchpad's workspace swipes off, and put them back.

- Spec: [Daemon](behavior.md#daemon)
- Code: [DesktopSettings.cs](../src/Winland.Install/Setup/DesktopSettings.cs), [MaintenanceCommand.cs](../src/Winland.Daemon/Setup/MaintenanceCommand.cs)
- Install commands: `Tweaks`, `RestoreTweaks`
- Driver: `pwsh -NoProfile -File scripts/smoke.ps1 -Feature tweaks`
