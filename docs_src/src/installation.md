# Installation

The mod requires the Steam version of Guildrun on Windows.

## Turn Off The Steam Overlay First

Do this before anything else. The overlay's default hotkey is Shift+Tab, which the mod uses to move to the previous control group. With the overlay on, every Shift+Tab opens the overlay instead, and you will be very confused about where your focus went.

To turn it off for Guildrun only:

1. In the Steam library, open Guildrun's context menu and choose Properties.
2. On the General page, uncheck "Enable the Steam Overlay while in-game".

To turn it off everywhere: Steam menu, Settings, In Game, uncheck "Enable the Steam Overlay while in-game".

## Installer

The recommended install path is the installer. Download `GuildrunAccessInstaller.exe` from the [latest releases page](https://github.com/amerikrainian/guildrun-access/releases/latest) and run it. It finds your Steam install on its own, downloads the newest release, verifies it, and installs it over the game folder while backing up anything it replaces. It can later update, repair, or uninstall the mod.

The default Steam path on Windows is:

```text
C:\Program Files (x86)\Steam\steamapps\common\Guildrun Demo
```

## Manual Installation

1. Download the latest `GuildrunAccess-vX.Y.Z.zip` release.
2. Extract the zip into the game folder (the one containing `Guildrun.exe`), replacing files when prompted.
3. After extraction, the game folder should contain `winhttp.dll`, `doorstop_config.ini`, `prism.dll`, a `dotnet` folder, and a `BepInEx` folder with the mod under `BepInEx\plugins\GuildrunAccess`.

## First Launch

Launch the game through Steam. The first launch after installing is slow: the loader generates its interop assemblies, which takes a few minutes and makes no sound. Wait it out. Once the main menu is up the mod says "Guildrun Access, version, loaded" and starts reading focus. Later launches are quick.

The same wait comes back once after every game update.

## Updating

At launch the mod checks the releases page and says "update X available". To update, run the installer again and choose update, or extract the latest zip over the game folder.

## Uninstalling

The installer has an uninstall option that restores the game folder from its install records. To remove the mod manually, delete the following from the game folder:

- `winhttp.dll`
- `doorstop_config.ini`
- `.doorstop_version`
- `prism.dll`
- the `dotnet` folder
- the `BepInEx` folder (or just `BepInEx\plugins\GuildrunAccess`, if you use BepInEx for other mods)
- the `GuildrunAccessDocs` folder
