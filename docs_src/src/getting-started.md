# Getting Started

## Basic Navigation

The basics are a screen reader's. Arrows move focus. Tab and Shift+Tab cycle control groups. Enter activates. Backspace is the secondary action where a control has one. Escape backs out. Home and End jump to the ends of a list, Alt+Up and Alt+Down to the previous or next section. Typing letters searches the focused group by name.

Focus announcements are deliberately short: name, value, state, role. Everything longer a control carries waits in the buffers; see [Buffers](buffers.md).

## Keys Here

**Press F1, anywhere.** It lists the keys that do something right where you are, the most particular first: reroll and freeze in the shop, the cell keys on the board, a glance key only while you are on a unit, and what Escape does on this screen. Enter on a row runs that key for you: you hear whatever the key says, then the control you were on.

That list is the reference, and it is always current. [Controls](controls.md) is the full list for when you want to see everything at once.

## The Game's Own Keys

While the mod's navigation is on, the game hears none of the keyboard. Everything the game's hotkeys did is offered through the mod's screens instead (the Heroes panel is in the run's menu, Escape opens the pause menu where nothing else claims it).

Ctrl+Shift+A hands the keyboard back to the game, and takes it again. You should not need it; it is there for the screen the mod does not cover yet.

## The Mod Menu

Ctrl+Shift+M opens the mod menu:

- **Settings**: speak list positions, and whether keyboard navigation is on at launch.
- **Sounds**: the master volume for the mod's cues, then per cue a volume row and an interval row. See [The Board And Combat](combat.md#sounds).
- **All keys**: every key the mod binds, whatever the screen.
- **Close**.

## Languages

The mod speaks the language the game is set to. Switching the language in the game's settings switches the mod's speech immediately. Game text is read from the game itself, already localized; the mod's own words come from `lang\<code>.txt` files in the plugin folder. Edit one to adjust a translation; missing entries fall back to English.
