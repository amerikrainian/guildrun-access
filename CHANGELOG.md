# Changelog

## V0.0.4

- The battle board is read as the hex grid it is with the Q E A D Z C scheme; arrows move between groups with up/down and units within said group. Shift alongside letters move heroes.
- A fighting unit's health is no longer re-read while focus rests on it; its line is read as focus lands, and the party and enemies buffers keep the live numbers.
- Every control standing for a hero now opens with its classes and rank after its name ("Skorn, Warrior, rank C"): board cells, units in a fight, party and reserve slots, the hero menus, the battle result and end screen rows; a hero card adds the rank its badge shows.

## V0.0.3

- The main menu no longer reads as eight entries for a moment before the game hides the ones it does not offer.
- The boss victory's end screen now offers its Continue (the game's click-anywhere), and Escape presses it.
- Equipping an item to a hero whose item slots are full is refused.
- Shift+arrows on the placement board move the focused hero one cell, speaking its new coordinates.
- The game's unexpected-error dialog now reads the exception's first line on its stack trace control, holds every line in the control buffer, copies the whole trace to the clipboard on Enter, and writes it to the mod's log.

## V0.0.2

- Read out relic picker.
- Enemies the game's own data leaves nameless should read now.
- The mod now checks for updates after loading and alerts for new updates.

## V0.0.1

- First release.
