# LOTR NPCs 1.0.4

## New in 1.0.4

- **LOTR Races and Classes is now required.** The NPCs use its own race models instead of copies bundled in this mod, so the mod is smaller (1.4 MB) and stays in step with LRC. Install LRC 0.4.1 or newer on the server and every client.
- **Guard and shock troops removed.** Guards and shock troops already in a world become plain warriors (guard and shock recruits become plain recruits) and keep their XP. Riders and archers stay.
- **Tormented elves removed.** Their NPCs are removed when they load, and hired ones free their roster places.
- **Wood Elves.** The elves are now the Wood Elves (config id `woodelves`; old configs are moved over once, with your values).
- **Readable files.** Every JSON file in the mod is laid out for people to read and patch, and the server's `lotrnpcs-factions.json` has a blank line between factions and between its instruction lines.

## New in 1.0.3

- **Sides you can read:** each faction in the config has an `Enemies` line and an `Allies` line. To change who fights whom, move a name from one line to the other and run `/recruits faction reload`.
- **Default sides:** the Free Peoples are allies, the Shadow are allies, and the two are at war. The Dunlendings fight everyone.
- **Safe among allies:** players are not attacked by NPCs of factions allied to their own, but still hire only from their own faction.
- **Wicked dwarf riders** ride giant boars, like the dwarves.

## Fixes since 1.0.0

- **Same blow timing for every weapon (1.0.1):** a soldier's blow lands 400 ms after its swing starts, whatever it holds. A spearman hit mid swing no longer loses the blow (spear-armed sides, like Gondor, lost about 15% of their swings before).
- **Retreat holds (1.0.2):** retreating followers start no fights for the whole Retreat time, not even striking back at whoever hits them, and riders stop charging the enemy they were on. Charge still overrides it.


Soldiers of every people of Middle-earth for Vintage Story. They spawn in zones you mark, fight each other and you by faction, and players can join a faction and hire, command and promote their own band.

## What's in it

- **17 factions:** Gondor, Rohan, Dale, Arnor, Haradrim, Dunlendings, Black Númenóreans, Easterlings, dwarves, wicked dwarves, the Shire, the Wood Elves, Mordor, Angmar, the Misty Mountains, Gundabad and Isengard.
- **Ranks:** recruit, warrior, archer, captain and mounted rider, each with its own gear.
- **Equal factions:** every faction has the same stats, band size and prices, so battles are decided by numbers and tactics.
- **Joining:** new players pick a faction from a menu. Enemies attack on sight, and the Shire only defends itself.
- **Hiring and orders:** talk to an NPC to hire it with rusty gears, and hold R for orders: follow, hold, move, guard, six formations, four stances, Retreat and Charge. `/recruits roster` shows every recruit wherever it is.
- **Promotion:** recruits earn XP from kills and are promoted to warrior, archer, captain or rider.
- **Battles:** solid bodies, hits from behind do x1.5, riders charge and wheel away, captains lead, archers keep their distance, and wounded NPCs heal after a fight.
- **Spawning:** admins mark zones with `/recruits zone`, and wild groups spawn, patrol and despawn on their own.

## Requirements

- Vintage Story 1.22 (tested on 1.22.7).
- **Player Model Lib** 1.23.8 or newer (tested with 1.23.8; the mod also guards against the camera near-plane bug some Player Model Lib versions have with NPCs).
- **LOTR Races and Classes** 0.4.1 or newer (since 1.0.4): the NPCs use its race models.

## Optional mods

Without these the mod falls back to vanilla gear, or leaves out that mount.

- **To Rule Them All faction armor** (Gondor, Rohan, Mordor, Isengard, dwarves, Moria orcs, Angmar, Dale, Wood Elves, Arnor): NPCs wear their faction's armor.
- **Gundabad bone armor** and **Haradrim armor**: Gundabad and Haradrim gear.
- **Feverstone Wilds:** horses and tame dire wolves for riders.
- **Teapot's Giant Boar:** boars for dwarf riders.
- Wood Elf riders use the game's own caribou, made rideable by this mod.

## Server notes

- Config files are in `ModConfig`: `lotrnpcs-factions.json` (factions, hiring, battle settings) and `lotrnpcs-spawning.json` (zones). See the config guide: `docs/config-guide.md`.
- **Synergy** freezes creature AI more than 48 blocks from players (`EntityActivationRangeEnabled` in `ModConfig/Synergy.json`). Fights that drift past that stop until a player comes close.
- Entity codes keep their `test-` prefix (for example `lotrnpcs:test-gondor-warrior`), so NPCs in existing worlds keep working. Players only see names like "Gondor warrior".
- Testing commands (`/recruits battlelog`, `bench`, `xp`, `zone spawnnow`) are off unless `DebugCommands` is true.

## Guides

- Players: `docs/player-guide.md`
- Admins: `docs/config-guide.md`

## Credits

- Race models: **LOTR Races and Classes** by Dango Dango Dango, Shakgriig and Buggoi (a required mod).
- Orc weapon models and textures: the server's owner.
- Mod by Emperorcharizar.
