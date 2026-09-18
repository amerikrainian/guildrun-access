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

## Keys

Press F1, anywhere. It lists the keys that do something right where you are, the most particular
first: reroll and freeze in the shop, the cell keys on the battle board, a key that reads something
about a unit only while you are on a unit, and what Escape does on this screen. Enter on a row runs
that key for you: you hear whatever the key says, then the control you were on. That list is the
reference, and it is always current, the rest of this section is what can't easily be communicated this way.

The basics are a screen reader's: arrows move, Tab and Shift+Tab cycle control groups, Enter
activates, Escape backs out, and typing letters searches the focused group. Ctrl+Shift+A hands control back to the game and takes it again. Ctrl+Shift+M opens
the mod menu.

Everything a control carries beyond its focus line waits in the buffers, review lists you switch
between and step through. The control buffer holds the focused control's own line
and one line per tooltip; the hero buffer holds the whole hero a slot, card or unit concerns (its
name, classes and archetypes, its stats and abilities, then what each ability, class, archetype and
stat means); the quests buffer the quests of what it wears, one line each with the reward under it;
the items buffer what it wears; relics lists the run's relics; party and enemies list every unit on
the board; combat is the battle events log, newest line first.

## Languages

The mod speaks the game's language. Change it in the game's settings and the mod's own words change
with it, at once; start the game in German and the mod comes up in German. The game's text is the
game's own translation; the mod's words (button, party slot, cost, the key help) come from
`BepInEx\plugins\GuildrunAccess\lang\<language>.txt`: German, Spanish, Brazilian Portuguese, Russian,
Simplified and Traditional Chinese, plus French and Japanese for when the game offers them. The
translations were machine-made against the game's own vocabulary; corrections are welcome. A line
missing from a file is spoken in English, and `en.txt` is the template for a new language.

## Building from source

```
dotnet build GuildrunAccess.slnx -c Debug                              # builds and deploys into the game
dotnet test src/GuildrunAccess.Tests/GuildrunAccess.Tests.csproj       # unit tests
```

Run `setup-bepinex.ps1` once first, then launch the game once through Steam so the interop
assemblies exist. A release is `build_release.ps1` (the mod zip), `build-installer.ps1` (the
installer exe) and `create-release.ps1 vX.Y.Z` (the GitHub release from a pushed tag and the
CHANGELOG section).

Runs on [BepInEx 6](https://github.com/BepInEx/BepInEx) (vendored); speech via
[Prism](https://github.com/ethindp/prism). The installer is adapted from the
[Non-Visual Calculus](https://github.com/rashadnaqeeb/NonVisualCalculus) installer by Rashad
Naqeeb (MIT).
