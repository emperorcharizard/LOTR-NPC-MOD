# Changelog

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
