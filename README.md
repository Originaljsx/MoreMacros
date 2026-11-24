**More Macros** is a Dalamud plugin for FFXIV that gives every job its own 100 Individual macros.

<hr>

You will not find this plugin in the official plugin repository.
To install it, add this custom repository in `/xlsettings` > Experimental > Custom Plugin Repositories:
https://raw.githubusercontent.com/Originaljsx/MoreMacros/main/repo.json

<hr>

## How it works

The game only has 100 Individual and 100 Shared macro slots. More Macros keeps a separate set of the 100 Individual
macros for each job, and when you change job it swaps that job's set into the slots.

- Hotbars point at slot numbers, so Individual #5 can be one macro on Warrior and a different one, with its own
  name and icon, on Dragoon.
- Everything stays native: edit macros in the game's macro window, drag them to hotbars, use `/micon`, keybinds.
- Shared macros are never touched; use them for macros you want on every job.
- The first time you play a job, it starts with a copy of the macros you had loaded.
- If the game's macro window is open when you change job, the swap waits until you close it.

Open `/moremacros` to see each job's set, copy one job's macros to another, or restore the Individual macros you
had before installing the plugin.

## Building

Needs the .NET 10 SDK and a Dalamud install (XIVLauncher). On Windows, run `dotnet build -c Release`.
From WSL, `./deploy.sh` builds against the Windows Dalamud install and copies the plugin to `%AppData%\XIVLauncher\devPlugins\MoreMacros`.
