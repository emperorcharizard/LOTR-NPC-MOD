# LOTR NPCs 1.0.0

Soldiers of every people of Middle-earth for Vintage Story. They spawn in zones you mark, fight each other and you by faction, and players can join a faction and hire, command and promote their own band.

## What's in it

- **18 factions:** Gondor, Rohan, Dale, Arnor, Haradrim, Dunlendings, Black Númenóreans, Easterlings, dwarves, wicked dwarves, the Shire, elves, tormented elves, Mordor, Angmar, the Misty Mountains, Gundabad and Isengard.
- **Ranks:** recruit (plain, guard or shock), warrior, guard, shock trooper, archer, captain and mounted rider, each with its own gear.
- **Equal factions:** every faction has the same stats, band size and prices, so battles are decided by numbers and tactics.
- **Joining:** new players pick a faction from a menu. Enemies attack on sight, and the Shire only defends itself.
- **Hiring and orders:** talk to an NPC to hire it with rusty gears, and hold R for orders: follow, hold, move, guard, six formations, four stances, Retreat and Charge. `/recruits roster` shows every recruit wherever it is.
- **Promotion:** recruits earn XP from kills and are promoted to warrior, guard, shock, archer, captain or rider.
- **Battles:** solid bodies, hits from behind do x1.5, riders charge and wheel away, captains lead, archers keep their distance, and wounded NPCs heal after a fight.
- **Spawning:** admins mark zones with `/recruits zone`, and wild groups spawn, patrol and despawn on their own.

## Requirements

- Vintage Story 1.22 (tested on 1.22.7).
- **Player Model Lib** 1.23.8 or newer (tested with 1.24.0; this mod fixes that version's camera near-plane bug for NPCs).
- The race models come with this mod (from LOTR Races and Classes, with its authors' permission). LOTR Races and Classes itself is **not** required.

## Optional mods

Without these the mod falls back to vanilla gear, or leaves out that mount.

- **To Rule Them All faction armor** (Gondor, Rohan, Mordor, Isengard, dwarves, Moria orcs, Angmar, Dale, elves, Arnor): NPCs wear their faction's armor.
- **Gundabad bone armor** and **Haradrim armor**: Gundabad and Haradrim gear.
- **Feverstone Wilds:** horses and tame dire wolves for riders.
- **Teapot's Giant Boar:** boars for dwarf riders.
- Elf riders use the game's own caribou, made rideable by this mod.

## Server notes

- Config files are in `ModConfig`: `lotrnpcs-factions.json` (factions, hiring, battle settings) and `lotrnpcs-spawning.json` (zones). See the config guide: `docs/config-guide.md`.
- **Synergy** freezes creature AI more than 48 blocks from players (`EntityActivationRangeEnabled` in `ModConfig/Synergy.json`). Fights that drift past that stop until a player comes close.
- Entity codes keep their `test-` prefix (for example `lotrnpcs:test-gondor-warrior`), so NPCs in existing worlds keep working. Players only see names like "Gondor warrior".
- Testing commands (`/recruits battlelog`, `bench`, `xp`, `zone spawnnow`) are off unless `DebugCommands` is true.

## Guides

- Players: `docs/player-guide.md`
- Admins: `docs/config-guide.md`

## Credits

- Race models, skins and textures: **LOTR Races and Classes** by Dango Dango Dango, Shakgriig and Buggoi, used with their permission.
- Orc weapon models and textures: the server's owner.
- Mod by Emperorcharizar.
