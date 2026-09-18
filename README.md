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
The battle board is a hex grid with the points of the hexes facing up and down, so no cell lies straight
above or below another. On it, Q E A D Z C step to the focused cell's six neighbours the way the letters
sit on the keyboard (A and D along the row, Q and E to the row ahead, Z and C to the row behind), and
Shift with the same letter moves the focused hero there, speaking its new coordinates. The arrows jump
between units instead: Up to the enemy nearest ahead, Down back to a hero, Left and Right round the
units of that side, Home and End to its first and last. During a fight the arrows work the same way over
the fighting units.
Ctrl+C speaks the board position of the focused cell or unit, column then row, and Ctrl+T the battle
timer as the top panel shows it.
While placing, Ctrl+N lists the units near the focused cell, or near the focused hero's cell from its
party slot or card, each with its distance in hexes, nearest first and by name within a distance
("Mushroom Tank 2, Slime 1 3": the last number is the distance); Ctrl+H lists the hostile ones alone: the enemies from a hero, your
heroes from an enemy, and nothing from an empty cell, where only Ctrl+N answers. Both are silent during
a fight.
Enemies that share a name are numbered, "Slime 1" and "Slime 2", counted left to right from the row
farthest from you, the order Left and Right cycle them in. The number stays with the enemy from
placement through the fight, in the board, the buffers, the battle events log and the glance keys.
Ctrl+Shift+M opens the mod menu with its settings and key help.

Everything a control carries beyond its focus line waits in the buffers: Ctrl+Right and Ctrl+Left
switch buffers, Ctrl+Up and Ctrl+Down step through the current one. The control buffer holds the
focused control's own line and one line per tooltip; the hero buffer holds the whole hero a slot,
card or unit concerns (its name, classes and archetypes, its stats and abilities, then what each
ability, class, archetype and stat means) and the items buffer what it wears; relics lists the run's relics; party and enemies
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
