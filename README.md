**More Macros** is a Dalamud plugin for FFXIV that gives you unlimited macro slots, with no 15 line limit.

<hr>

You will not find this plugin in the official plugin repository.
To install it, add this custom repository in `/xlsettings` > Experimental > Custom Plugin Repositories:
https://raw.githubusercontent.com/Originaljsx/MoreMacros/main/repo.json

<hr>

To get started, open the macro window with `/moremacros`. It works like the game's macro window: click a slot, give it a name, icon and text. Pages are tabs above the grid, and macros can be dragged between slots.

## Features

### Unlimited macros

Macros are stored by the plugin, outside the game's 100 Individual + 100 Shared slots. Run one with `/mm <name>`.
They run through the game's own macro engine, so `/wait`, `<t>`, `/macrolock`, `/macroerror` and `/micon` work as usual.

### No 15 line limit

Longer macros are split into linked 15-line macros automatically. `/macrocancel`, or starting another macro, stops the whole chain.

### Per-job variants

Each macro can be tied to a job. If several macros share a name, `/mm <name>` runs the one for your current job and falls back to the "Any job" one.

### Hotbars

Drag a macro from the grid straight onto a hotbar slot. Behind the scenes a hotbar can only hold a game macro, so this uses one Individual macro slot containing `/mm <name>` (reused if it already exists). "Add to hotbar macros" also writes a one-line macro (`/mm <name>`) into the first empty Individual slot; drag that onto a hotbar.

### Import

Copies an existing game macro into the plugin. Auto-translate phrases are not preserved.

## Building

Needs the .NET 10 SDK and a Dalamud install (XIVLauncher). On Windows, run `dotnet build -c Release`.
From WSL, `./deploy.sh` builds against the Windows Dalamud install and copies the plugin to `%AppData%\XIVLauncher\devPlugins\MoreMacros`.
