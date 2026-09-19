# Changelog

## V0.0.9

- The game map doesn't fully show its floors, which for us implied we were skipping the second act. We weren't, and now there's an explicit, "More floors not shown", entry to clear up the confusion.
- No more placing heroes over board limit! Not only was it a bug, but it also unfortunately bricked some events!

## V0.0.8

- Shift+q e a d z c now moves a hero from its party slot too, not just from its cell on the board.
- The reroll on the first hero picker now says what it costs.
- Rank up choices now say when a path gives the hero another class, and read the tags on each choice.
- Rank up choices with an active ability now say their mana.
- You can now hide a rank up choice to check your party or the shop first.
- Event choices that show a hero's portrait now say which hero, with their class and rank.
- The mod now speaks the game's language. Translations are welcome, as this is Claude's labor of love.

## V0.0.7

- Added ctrl+n for who's nearby and ctrl+h for hostiles nearby. Only works in setup phase.
- Added enemy numbers, so you can finally tell things like spiders and slimes apart from each other.
- Hero archetypes now read by the game's own names and are explained in the buffers, along with the hero's classes.
- The hero buffer now holds the whole hero: name, stats and abilities, then what every ability, class, archetype and stat means. No more hopping to the control buffer for half of it.
- Added a quests buffer, right after the hero one, and ctrl+q to hear the quests of whatever hero, item or relic you're on.
- Fixed the rift seal's charges being unreadable. Two of its three counts were getting swallowed as repeats; each class group now reads with its own count, and says complete once it's charged.
- Heroes now say what they're wearing during a fight, and their items and quests buffers work there too. They used to go blank the moment combat started.
- Red rift missions now say when they're complete or failed, and ctrl+m reads them from anywhere in the run.
- Escape now opens the pause menu from the first hero picker and from the crossroads.
- Added a key help on f1. It only lists the keys that do something where you are, e.g. ctrl+r in the shop, and pressing enter on one runs it for you and rereads your focus.

## V0.0.6

- More glance keys: ctrl+c to check unit position on the board and ctrl+t for time elapsed in combat.
- Redesign shop and party items views into grids to try and reduce UI verbosity.

## V0.0.5

- Glance keys on the unit under focus, spoken in place with focus unmoved: 1 health, shield and mana; 2 base attack damage, attack, magic and defense; 3 attack speed, crit, range and move speed; 4 regen, omnivamp and resistances when nonzero; 5 statuses; 6 who it is attacking. Shift+2, 3, 4 add each stat's breakdown (base, rank, bonus).
- Ctrl+S speaks the shards. In the shop, Ctrl+R rerolls and Ctrl+F freezes.
- The party and enemies buffers list each unit's status icons with their numbers/stacks.
- Compendium hero pages are no longer groupped under their tabs, so you can read their abilities.
- We no longer duplicate choices for rank A and above. Gotta love fancy UI rendering.
- Allow tab to wrap pretty much anywhere you care to: events, crossroads, and the shop are examples of where this can now be done.

## V0.0.4

- The battle board is read as the hex grid it is with the Q E A D Z C scheme; arrows move between groups with up/down and units within said group. Shift alongside letters move heroes.
- A fighting unit's health is no longer re-read while focus rests on it. It proved to be useless at communicating critical information.
- Every control standing for a hero now opens with its classes and rank after its name. This is probably too verbose, but it's a start.
- The tutorial now completes and is independent of modal timing.
- Consolidated rank picker into one group, like the events.

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
