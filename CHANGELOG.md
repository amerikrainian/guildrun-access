# Changelog

## V0.0.4

- The battle board is read as the hex grid it is with the Q E A D Z C scheme; arrows move between groups with up/down and units within said group. Shift alongside letters move heroes.
- A fighting unit's health is no longer re-read while focus rests on it. It proved to be useless at communicating critical information.
- Every control standing for a hero now opens with its classes and rank after its name. This is probably too verbose, but it's a start.
- The tutorial now completes and is independent of modal timing.
- Consoledate rank picker into one group, like the events.

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
