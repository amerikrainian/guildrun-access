# Guildrun Access

A [Guildrun](https://store.steampowered.com/app/4425970/) mod that makes the game playable by blind
users.

Be warned! The game is in EA and frequently receives updates; expect the mod to lag behind and/or change as the game evoles.

## Install

Download `GuildrunAccessInstaller.exe` from the
[latest release](https://github.com/amerikrainian/guildrun-access/releases) and run it. It finds
your Steam install, downloads the mod, and can later update, repair, or uninstall it. Manual
alternative: extract the release zip over the game folder (the one holding `Guildrun.exe`).

The first launch after installing generates the loader's interop assemblies and can take a few
minutes; the mod starts speaking at the main menu.

At launch the mod checks the releases page and says "update X available" when a newer version
exists. Up to date, or offline, it says nothing. To update, run the installer again and choose
update, or extract the latest zip over the game folder.

Requires the Steam version of the game on Windows and a screen reader (NVDA, JAWS, or SAPI).

## Keys

Focus mode is on at launch (Ctrl+Shift+A toggles it). Arrows navigate, Tab and Shift+Tab cycle
control groups, Enter activates, Backspace is the secondary action, Escape backs out, Home and End
jump, Alt+Up and Alt+Down jump sections, and typing letters searches the focused group.
On the placement board, Shift+arrows move the focused hero one cell, speaking its new coordinates.
Ctrl+Shift+M opens the mod menu with its settings and key help.

Everything a control carries beyond its focus line waits in the buffers: Ctrl+Right and Ctrl+Left
switch buffers, Ctrl+Up and Ctrl+Down step through the current one. The control buffer holds the
focused control's own line and one line per tooltip; the hero and items buffers describe the hero a
slot, card or unit concerns and what it wears; relics lists the run's relics; party and enemies
list every unit on the board; combat is the battle events log, newest line first.

## Building from source

```
dotnet build GuildrunAccess.slnx -c Debug                              # builds and deploys into the game
dotnet test src/GuildrunAccess.Tests/GuildrunAccess.Tests.csproj       # unit tests, no game needed
```

Run `setup-bepinex.ps1` once first, then launch the game once through Steam so the interop
assemblies exist. A release is `build_release.ps1` (the mod zip), `build-installer.ps1` (the
installer exe) and `create-release.ps1 vX.Y.Z` (the GitHub release from a pushed tag and the
CHANGELOG section).

Runs on [BepInEx 6](https://github.com/BepInEx/BepInEx) (vendored); speech via
[Prism](https://github.com/ethindp/prism). The installer is adapted from the
[Non-Visual Calculus](https://github.com/rashadnaqeeb/NonVisualCalculus) installer by Rashad
Naqeeb (MIT).
