# Changelog

## 1.0.5 (in progress)
- Factions are data: `tools/data/factions.json` lists each faction's id, side, stance, tier and member races, and the mod reads it at start instead of fixed lists in the code (default factions and sides, hire tiers, spawn zone factions, join menu order). Another mod can add a faction file of its own. New config files list the factions in join menu order. No other behaviour change; generated NPC files are identical to 1.0.4.

## 1.0.4 (2026-11-18)
Changes from an outside review of 1.0.3 (owner: "Make it a dependency", "Remove both", "Fix everything else mentioned").
- LOTR Races and Classes is a required mod (`lrc` 0.4.1 in modinfo). Nothing of it is copied any more: most races use LRC's own model (Gondor, Rohan, Dale and Arnor all use `lrc:freepeople`), and what each faction may look like (hair, skin, eyes) is a list of limits in `tools/data/races.json`. Only the goblin, Mordor orc and Uruk-hai bodies are models of ours, made from LRC's with looks it doesn't have. Our weapon animations go onto LRC's bodies by a JSON patch. NPCs saved with an old model take their type's. Zip 2.35 MB to 1.44 MB.
- Guard and shock classes removed (warriors' branches and recruits' classes, and the "change your class" talk option). Existing ones become plain warriors or recruits and keep their XP.
- Tormented elves removed. Existing ones are removed when they load.
- The elves are the Wood Elves (`woodelves`). Config version 36 renames `elves`, keeping its values, and drops `tormentedelves` and the guard and shock values.
- All generated JSON is readable and patchable: short lists on one line, long ones split, blank lines between big sections. The server config is written the same way.
- Armor by race and rank is in `tools/data/armor.json`, with a note per race; `tools/data/README.md` explains the data files and how to add a faction.

## 1.0.3 (2026-11-17)
- Sides: each faction in `lotrnpcs-factions.json` has an `Enemies` line and an `Allies` line. Move a name from one to the other to change a pair; the mod copies it to the other faction. A faction in neither line is neutral. Config version 35.
- Default sides: the Free Peoples (Gondor, Rohan, Dale, Arnor, dwarves, the Shire, elves) are allies, the Shadow (Mordor, Angmar, Misty Mountains, Gundabad, Isengard, Haradrim, Easterlings, Black Númenóreans, wicked dwarves) are allies, and the two are enemies. Dunlendings and tormented elves fight everyone. Old pair entries in `Relations` are dropped once, except alliances with `ShareFriends`.
- Players are safe from NPCs of factions allied to their own, but hire only from their own faction (unless the alliance has `ShareFriends`).
- Wicked dwarf riders ride Teapot's giant boars, like the dwarves.
- Code: plain comments and smaller files; the config notes are regrouped and shorter. No behaviour change.

## 1.0.2 (2026-11-17)
- Retreat: followers start no fight task until it ends, including striking back at whoever hits them; riders drop the enemy they were finishing. Charge still fights. Movement orders' 5 s break-off works the same way.

## 1.0.1 (2026-11-17)
- Every foot soldier's blow lands 400 ms after its swing starts, for every weapon, even if it is hit or turned mid swing (spearmen lost blows when hit). Battle log: `noblow` rows for swings that end without a blow.

## 1.0.0 (2026-11)

First official release. Everything below was built and tested as test builds 0.0.1 to 0.0.168; the per-version history is in `docs/notes/` and `docs/test-checklist.md`.

**Factions and joining**
- 18 factions with their own looks, gear and names; race models bundled from LOTR Races and Classes.
- New players pick a faction from a menu (permanent; admins can change it with `/recruits faction set`).
- Relations in config: ally, neutral or enemy, with shared friends between allies.
- Every faction has the same stats per rank, 3 followers, a roster of 16 and the same prices (equal-stat PvP).

**Hiring, orders and promotion**
- Hire with rusty gears from the talk menu; 3 followers of any rank plus 1 recruit, 16 recruits in all, 1 captain per player.
- Hold R for orders: follow, hold, move, guard (also shared with your group), six formations, four stances, Retreat and Charge.
- Roster window (`/recruits roster`): find, call, promote or dismiss any recruit.
- XP from kills; promotion to warrior, guard, shock trooper, archer, captain or rider. Recruits switch between plain, guard and shock for free.
- Looks menu: rename, helmet, weapon and shield.

**Battles**
- Solid bodies, battle lines for followers in formation, up to 4 attackers on one NPC, hits from behind x1.5, a ±20% spread on hits.
- NPC-v-NPC hits do half damage, so fights last; players take and deal full damage.
- Captains hit harder and take less from other NPCs, but keep normal numbers against players.
- Riders on horses, dire wolves, boars or caribou: charge, strike (x2.5), wheel away, hunt archers first; foot soldiers swing at riders passing through them.
- Archers keep their range and stand their ground on walls.
- Out-of-combat healing; bodies hold a few rusty gears and cloth or leather.

**Spawning and servers**
- Admin spawn zones (`/recruits zone`) with patrol routes, rival groups where factions meet, captains with escorts and the occasional rider.
- Wild NPCs despawn when no one is near or after their lifetime; hired ones never do.
- No recruit starts a fight within 64 blocks of the world spawn.
- Client option to animate far NPCs at a lower rate (`.npcanimlod`) for big battles.
- Testing commands behind `DebugCommands` (off by default).
