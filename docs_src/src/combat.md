# The Board And Combat

Guildrun is an auto-battler. You place heroes on a hex board; once you press Fight, they and the enemies walk, target, and swing on their own until one side is gone or the timer runs out. Your decisions are the placement, the kit, and the battle speed. This page is about getting the placement right without seeing the board.

## The Grid

The board is a pointy-top hex grid: rows of cells, each odd row shifted half a cell to the right. No cell lies straight up or down; every cell has six neighbours, two above, two to the sides, two below. Enemies stand at the top, your heroes at the bottom.

## Moving Around

Two ways to move, and you will use both.

- **Q, E, A, D, Z, C** step to the focused cell's six neighbours: up left, up right, left, right, down left, down right. Look at the letters on your keyboard; they sit around S the way neighbours sit around a cell.
- **The arrows** jump between units. Up goes toward the enemies, Down toward your heroes, Left and Right along a side.

Ctrl+C reads the focused cell's coordinates.

## Placing Heroes

Shift plus a cell letter moves the focused hero to that neighbouring cell. It works from the hero's party slot too, focus staying on the slot, so you can shuffle a hero across the board from the party list. A second press starts from where the first one left the hero.

While placing, two glances help with spacing:

- **Ctrl+N**: every unit near the focused cell or hero, nearest first, with its distance in cells.
- **Ctrl+H**: only the hostile ones: the enemies from a hero, the heroes from an enemy.

## Reading Units

The digit glances read the unit under focus without moving: 1 vitals, 2 attack, magic and defense, 3 speed, crit, range and move speed, 4 sustain, 5 statuses, 6 its target. Shift+2, 3, or 4 add each stat's breakdown. The hero, items, party, and enemies buffers have the rest.

## The Fight

Alt+F, or the Fight button, starts the battle. During the fight the arrows still walk the units, and the glances read them live: health, statuses, the current target. Ctrl+T reads the timer.

The mod speaks nothing as the fight happens; a play-by-play of an auto-battler is a wall of noise. What the game draws (damage numbers, heals, statuses, deaths, casts) goes to the battle log instead: the combat buffer during the fight, and the battle events group afterwards, newest first.

## Sounds

What the fight shows but never sounds, the mod plays as a cue:

- Crits, heals, a hero dropping below a quarter health, deaths on either side.
- A hero or enemy casting, a hero's ability ready (mana full).
- Burn, frost, poison, stun, and shields landing on a unit and wearing off, panned a little left for your heroes and a little right for enemies. Other statuses on heroes share a generic pair.
- Rush and stall starting, shards gained, stats going up, a taunt.

Plain damage has no cue on purpose; it would be a rattle.

The mod menu's Sounds screen sets a master volume and, per cue, a volume (Left and Right step it, Enter previews it) and an interval: the least seconds between two plays of that cue. Enter on an interval row to type a number; blank means every event. The cues play under the game's SFX volume, so the game's slider governs them too.
