# Troubleshooting And Known Issues

## No Speech

If the mod does not speak:

1. Wait. The first launch after installing, and after every game update, takes minutes of silence while the loader generates its assemblies.
2. Confirm `winhttp.dll`, `doorstop_config.ini`, `prism.dll`, `dotnet`, and `BepInEx` sit next to `Guildrun.exe`, and the mod is at `BepInEx\plugins\GuildrunAccess`.
3. Launch through Steam.
4. Check the BepInEx log:

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\Guildrun Demo\BepInEx\LogOutput.log
   ```

5. Look for `Guildrun Access`. Loader problems are at the top of the file.

The log is truncated on every launch, so whatever is in it is from the current session. Attach it to any bug report.

## Shift+Tab Opens Something Else

That is the Steam overlay. Turn it off; see [Installation](installation.md#turn-off-the-steam-overlay-first).

## The Keyboard Stopped Working

You pressed Ctrl+Shift+A, which hands the keyboard to the game. Press it again.

## The Tutorial Keeps Coming Back

The game drops a run that ends before the tutorial's save point (the third shop) and replays the tutorial from the start, its fixed hero choices and locked difficulty included. That is the game's rule, not the mod's. Play the first run to the third shop.

## Game Updates

The game updates often. After an update, a screen the mod used to read may go quiet or read wrong until the mod catches up. The update check at launch will tell you when it has.

## Untranslated Game Text

Now and then you will hear "No translation found for" something. That is the game's own text, read as it draws it.

## Unread Things

Progression milestones that reward a hero or a token slot are icons with no tooltip; their title is all there is to read.
