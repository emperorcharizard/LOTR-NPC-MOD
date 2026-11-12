# LOTR NPCs: config guide

Nov 3, 2026 (updated for 0.0.92) · @Emperorcharizar

## Overview

The mod has two config files in the server's `ModConfig` folder. Both are created with their defaults on first start. Your values are kept on updates, and older files are upgraded once. Since 0.0.74 the player command is `/recruits` (it was `/hirelings`). 0.0.76 moved the faction file to version 16: no alliances by default, and promotion prices. 0.0.90 moved it to version 19: each faction has its own follow limit, roster, health and damage, and recruits of different factions share a band by shares (see "Faction balance" below). 0.0.91 moved it to version 20: health and damage per rank instead of two multipliers, and a random spread on every hit. 0.0.92 moved it to version 21: players pick their faction from a menu; race and groups no longer make anyone a member (see "Joining a faction" below). 0.0.93 moved it to version 22: promotion now costs the hire price of the new rank less 3 coins (less 4 for a captain); promotion prices still as shipped are updated, ones you changed stay. 0.0.94 moved it to version 23: the hire coin is the rusty gear (`game:gear-rusty`) for now; factions still on the planchet switch once, a coin you chose stays. The spawning file got a version of its own (1) in 0.0.94: an unchanged `MaxNearby` 8 becomes 24. 0.0.112 moved the faction file to version 29: `NpcHitsIgnoreHurtWindow` turns on once (NPC hits on NPCs skip the hurt window), and files older than version 19 (before 0.0.90) are no longer upgraded step by step; they keep their own values.

| File | Controls | After editing |
| --- | --- | --- |
| `lotrnpcs-factions.json` | Who fights whom, faction membership, hiring, recruit limits, healing, guard radius, helmets and shields, battle spacing | `/recruits faction reload` (no restart) |
| `lotrnpcs-spawning.json` | Natural spawning inside admin zones | Restart the server |

Each file starts with a `#Instructions` list explaining it. That list is rewritten on every start, so edit the settings below it, not the list itself. Don't change `Version`.

## lotrnpcs-factions.json

This file decides who each NPC faction counts as a friend, which factions fight each other, what hiring costs, and how recruits behave.

### Joining a faction (0.0.92)

A player's faction is the one they pick from the join menu, or the one an admin sets. Race and `/group` no longer count (unless `RaceAndGroupMembership` is turned on, below).

- The menu opens right after a new player makes their character, and on every join until they have a faction. It lists every faction with `Joinable` true as a card under Free Peoples, Shadow or Others (0.0.93), with the faction's portrait, its name, how many recruits can follow at once ("followers") and how many it can have in all ("total"). Only a stance other than aggressive is shown (the Shire's "defensive"). The wicked dwarves are under Shadow (0.0.95); the Dunlendings and tormented elves under Others. Factions you add go under Others. Picking asks for a confirmation, because it is permanent.
- A player who closes the menu without picking gets a reminder in their chat: type `/recruits join` to open it again. Until they pick, they belong to no faction: aggressive factions attack them on sight and they can't hire.
- After the pick only an admin changes it: `/recruits faction set <player> <faction>` moves them, `/recruits faction clear <player>` takes them out so they can pick again. `/recruits faction members` lists who is in what.
- Allies with `ShareFriends` still treat each other's members as friends, and let them hire each other's NPCs.

| Setting | Default | What it does |
| --- | --- | --- |
| `Joinable` (each faction) | true | false leaves the faction out of the join menu. Admins can still set players to it. |
| `RaceAndGroupMembership` (top of the file) | false | true brings back the rule from before 0.0.92: a player is also a member when their race is in `FriendlyRaces` or they are in one of the faction's `FriendlyGroups` (a picked or admin-set faction replaces the race; groups still count). |

### Per faction (under `Factions`)

There is one entry per faction, keyed by its id: gondor, rohan, dale, arnor, haradrim, dunlendings, blacknumenoreans, easterlings, dwarves, wickeddwarves, shire, elves, tormentedelves, mordor, angmar, mistymountains, gundabad, isengard. Gundabad (since 0.0.62) is added once to older files.

| Setting | Default | What it does |
| --- | --- | --- |
| `Stance` | `aggressive` (Shire: `defensive`) | `aggressive` attacks every non-member player on sight. `defensive` only fights players who hurt it or its allies. Both attack enemy NPCs and monsters on sight. |
| `Joinable` | true | Since 0.0.92: false leaves the faction out of the join menu. |
| `FriendlyRaces` | Per faction, e.g. dwarves = `lrc:longbeard`, `lrc:firebeardsandbroadbeams`. Men of the West and orc factions list none. | Only with `RaceAndGroupMembership` true (off since 0.0.92): players of these Player Model Lib races are members. `/recruits faction check <player>` shows a player's race code. |
| `FriendlyGroups` | empty | Only with `RaceAndGroupMembership` true: players in any of these vanilla /group names are members. |
| `HireCoin` | `game:gear-rusty` | The item paid to hire and to promote: the game's rusty gear, the same for every faction (since 0.0.94, owner: "change the coins to rusty gears for now"; it was the large silver planchet `coinage:planchet-silver-lg` from 0.0.59). Factions still on the planchet or the old `coinage:coin-silver-lg` were switched once; a coin you chose stays. |
| `HirePrices` | by tier (table below); Men recruit 5, warrior 15, captain 40, archer 15 | How many coins each rank costs. Archers (the rankless bow unit every faction but the Shire has) use the archer price. Other rankless NPCs cost a recruit's price. 0 makes hiring free. |
| `PromotePrices` | the faction's hire price for the rank less 3, less 4 for a captain; Men warrior 12, archer 12, captain 36 | Since 0.0.76: what promoting a recruit costs in `HireCoin`, by the rank it becomes. A rank you leave out gets the default. 0 makes it free. See "Promotion" below. |
| `MaxFollowers` | by tier (Men 5) | Since 0.0.90: how many of this faction's recruits can follow a player at once. Each uses 1/`MaxFollowers` of the following (1 to 32). |
| `MaxRecruits` | by tier (Men 16) | Since 0.0.90: how many of this faction's recruits a player can have. Each uses 1/`MaxRecruits` of the roster (1 to 256). |
| `Health` | by tier, per rank (table below) | Since 0.0.91: the health of the faction's recruits, warriors, captains and archers, a whole number each. Loaded NPCs take it after `/recruits faction reload`. A rank left out keeps the unit's own. (0.0.90 had `HealthMultiplier`; a version 19 file's value is turned into this table once.) |
| `Damage` | by tier, per rank | Since 0.0.91: the damage of one hit of each rank, melee and arrows (an archer's is its arrow and its shove). The damage tier doesn't change. (Was `DamageMultiplier` in 0.0.90.) |

A player is a member of the faction they picked or an admin set (see "Joining a faction"). Members are never attacked on sight and can hire the faction's NPCs, and so can members of a faction allied to it with `ShareFriends` on.

### Faction balance (0.0.90, per rank since 0.0.91)

The owner's goal: factions "unequally equal". Orcs are weaker and cheaper but more of them follow, elves and dwarves are stronger but fewer, and a full band of any faction should win about as often as any other. The Uruk-hai are the exception: slightly stronger than Men, at a 10% higher cost. Since 0.0.91 this holds rank by rank (owner: "balance it for all factions on an average"): at each rank, a full band of each faction wins about half its fights averaged over every other faction's full band of that rank.

| Tier | Factions | Follow / roster | Hire R/W/C/A | Promote W/A/C |
| --- | --- | --- | --- | --- |
| Men | gondor, rohan, dale, arnor, haradrim, easterlings, blacknumenoreans, shire | 5 / 16 | 5/15/40/15 | 12/12/36 |
| Uruk-hai | isengard | 5 / 15 | 6/17/44/17 | 14/14/40 |
| Dunlendings | dunlendings | 6 / 19 | 4/13/33/13 | 10/10/29 |
| Orcs | mordor, angmar, gundabad | 7 / 22 | 4/11/29/11 | 8/8/25 |
| Goblins | mistymountains | 8 / 26 | 3/9/25/9 | 6/6/21 |
| Elves | elves, tormentedelves | 4 / 13 | 6/19/50/19 | 16/16/46 |
| Dwarves | dwarves, wickeddwarves | 4 / 13 | 6/19/50/19 | 16/16/46 |

Health / damage of one hit, by rank (0.0.91 defaults):

| Tier | Recruit | Warrior | Captain | Archer |
| --- | --- | --- | --- | --- |
| Men | 22 / 3.05 | 34 / 4 | 48 / 7.05 | 34 / 3 |
| Uruk-hai | 23 / 2.95 | 32 / 4.3 | 48 / 7.15 | 35 / 2.95 |
| Dunlendings | 18 / 2.7 | 29 / 3.4 | 41 / 6 | 28 / 2.6 |
| Orcs | 15 / 2.5 | 27 / 2.8 | 31 / 5.95 | 22 / 2.45 |
| Goblins | 16 / 1.85 | 21 / 2.85 | 31 / 4.75 | 19 / 2.2 |
| Elves | 27 / 3.65 | 37 / 5.4 | 53 / 9.6 | 37 / 4.25 |
| Dwarves | 30 / 3.25 | 46 / 4.35 | 63 / 7.9 | 46 / 3.4 |

**Hit spread.** `DamageSpread` (top of the file, 0.2): every NPC hit deals its damage times a random 80% to 120%. Without it, every NPC of a rank has the same health and damage, so a fight between two same-rank bands is decided by whole numbers of hits and lands at about 30% or 70%, never even. With it, every single same-rank pairing lands between about 37% and 63% in the simulator. 0 turns it off.

**Shares.** Each recruit uses a share of the player's band: 1/`MaxFollowers` of the following and 1/`MaxRecruits` of the roster, and the shares must add up to no more than 1. So factions mix: 3 Gondor men and 2 Mordor orcs use 3/5 + 2/7 = 0.89 of the following (a third orc or a goblin won't fit), and 2 elves and 2 dwarves fill it exactly. Talk menus and the follow order say what still fits ("Room in your band: Mordor orcs 1, Men of Gondor 0."), and since 0.0.91 also the roster ("Room on your roster: Mordor orcs 12, Men of Gondor 8."): when you talk to an NPC you could hire, after every hire, and in chat when you hire.

**Lowering limits.** When a player joins, or after `/recruits faction reload`, a player over the new limits loses the most recently hired recruits until they fit (far-away ones when they next load), and any borrowed shared guards go back to keep watch first. Recruits hired before 0.0.90 count at the Men's share until they next load. A follower that loads into a band that filled up meanwhile keeps watch where it stands instead.

**Upgrading.** A version 19 file's multipliers become the per-rank tables: ones still as 0.0.90 shipped them take the fitted tables, ones you changed become the units' own values times yours. A faction you add yourself gets the Men's values.

How the numbers were found is in `tools/balance/` (a fight simulator; see its README; `fitspread.py` refits the per-rank tables).

### Relations between factions (under `Relations`)

Each entry has `A`, `B`, `Relation` and `ShareFriends`. Two factions with no entry are enemies.

| Relation | Effect |
| --- | --- |
| `ally` | Never fight. They come to help each other within `CallForHelpRange`. |
| `neutral` | Leave each other's NPCs alone. |
| `enemy` | Attack each other's NPCs on sight. |

With `ShareFriends: true` on an alliance, each faction's members are also safe from the other's NPCs. Without it, a player is only safe from their own factions.

No factions are allies by default (since 0.0.76). The file ships with one entry, Gondor and Rohan as enemies, an example of the format that changes nothing. Older files lose the Mordor and Isengard alliance that shipped from 0.0.34 to 0.0.75, but only if it is still exactly as shipped (`ally`, `ShareFriends: false`). An alliance you added or changed stays.

### Promotion (0.0.76)

A hired recruit or warrior earns 1 XP for each NPC of this mod that dies within 10 seconds of its hit. Several recruits that hit the same NPC each get the point. Monsters, animals and players give nothing, and archers and captains earn nothing. XP stops at the requirement and starts again from 0 after a promotion.

| Rank | Needs | Can become | Cost |
| --- | --- | --- | --- |
| Recruit | 15 XP | Warrior or archer | `PromotePrices` warrior / archer (Men 12 each) |
| Warrior | 40 XP | Captain | `PromotePrices` captain (Men 36) |

Promotion needs both the XP and the coins. Since 0.0.93 the default price is what hiring that rank costs, less 3 coins, or less 4 for a captain (owner: "the cost of the actual unit minus 3-4 coins"). A faction whose hire prices you changed gets its promotion prices from your hire prices when the file is upgraded.

The owner promotes it from its talk menu (right-click). Its owner and the players in the owner's /group see `XP: (x/y)` when they look at it. A promoted NPC keeps its name, face, orders, post and weapon, and wears the new rank's armor. It wears a helmet if it wore one before.

### Everything else

| Setting | Default | What it does |
| --- | --- | --- |
| `MonsterCodes` | drifters, bowtorn, shivers, bells, locusts | Creatures every faction attacks on sight. `*` is a wildcard, e.g. `game:drifter-*`. |
| `GrudgeSeconds` | 30 | How long NPCs keep fighting someone who hurt them or an ally. |
| `CallForHelpRange` | 16 blocks | How far allies come to help. |
| `MaxRecruits` | 16 | The baseline roster: the Men's, and the value for a faction without its own (since 0.0.90 each faction has one). |
| `MaxFollowers` | 5 | The baseline follow limit: the Men's, and the value for a faction without its own. It also sets the wild group scale (a faction's groups grow with its `MaxFollowers` over this). Borrowed shared guards count toward the following by their faction. A hire past the following guards where it was hired. |
| `SpawnSafeRadius` | 64 blocks (0 = off) (0.0.112) | Within this many blocks of the world spawn, no recruit starts a fight with a player. They still fight back against a player who attacks them, their owner or their group. |
| `WildBodyLoot`, `HiredBodyLoot` | rusty gears (recruit 0-1, warrior and archer 1-2, captain 3-5) plus up to 1 cloth (recruits) or leather (0.0.112) | By rank, what a dead wild NPC's body and a dead recruit's body hold, as a list of `{ "Code": item code, "Min": n, "Max": n }`. A player harvests the body with a knife, as with a drifter. Armor and weapons are never dropped. Rankless NPCs use the recruit list; an empty list leaves an empty body. Bodies rot away after 3 in-game hours. |
| `GuardRadius` | 16 blocks (4 to 64) | How far around their post guards patrol and attack intruders. |
| `MaxAttackersPerTarget` | 0 = no limit (3 before 0.0.82, 2 in 0.0.82 to 0.0.84) | How many NPCs may fight one NPC at a time. With a limit, the rest look for another enemy, wait one step behind in a battle line, or, outside a line, join a full enemy anyway when there is no other within reach. Either way an NPC takes an enemy nobody is fighting before one already being fought. Players are never limited, and an NPC always strikes back at whoever hits it. |
| `CombatSpacing` | 1.5 blocks (0.8 to 4; 2 before 0.0.82) | How far apart fighting NPCs of the same side keep, and the gap between neighbours in a battle line. Enemies only keep `DuelDistanceMin` apart. Since 0.0.87 this also sets how many fight one enemy at once: as many as fit round it this far apart (5 by default); the others take another enemy with room first, or else wait in a second ring one `CombatSpacing` farther out (2.8 blocks) and step in when a place opens. |
| `DuelDistanceMin`, `DuelDistanceMax` | 1.0 and 1.6 blocks (0.0.82) | How far from the center of the NPC it fights an attacker stands: it closes in when farther, steps back when closer, and stands and fights anywhere in between. Kept within 0.6 to 2.2. Players are approached the vanilla way. |
| `NpcHitsIgnoreHurtWindow` | true (false in 0.0.109 to 0.0.111) | NPC hits on NPCs and creatures land even within half a second of another hit (the game's hurt window), so every attacker and arrow counts. Players always keep the hurt window. On by default since 0.0.112 (owner: "keep the i-frames off for now"); config version 29 turns an old false on once. |
| `BattleLines` | false (true in 0.0.82 to 0.0.84) | Off by default since 0.0.85 (owner: NPCs should "fight first, then maintain formation second"; a line keeps them in their places until an enemy is near). When true: Groups line up when they meet another faction's NPCs: wild NPCs, and recruits following in any formation but Loose. Each NPC takes an enemy nobody is fighting first, so the lines pair off, and extras join in on enemies already being fought (with a `MaxAttackersPerTarget` limit, the rest wait one step behind until a place opens). Loose followers, guards, archers, and fights with players or monsters make no lines. |
| `LineHoldRange` | 6 blocks (2 to 24) (0.0.82) | Wild NPCs and aggressive or raid recruits march their line to meet the enemy; defensive recruits hold it until an enemy is this close. |
| `HelmetChance` | 0.6 (0 to 1) | Chance a newly spawned NPC wears its outfit's helmet. An unworn helmet is kept, and the owner can put it on from the recruit's menu. Helmets are only for looks; protection comes from rank. |
| `CaptainsAlwaysHelmeted` | false (true before 0.0.82) | true makes every captain always wear its helmet. Since 0.0.82 the mod decides per unit which may spawn bare-headed (all orcs and Uruk-hai, and the units of other factions the owner's loadout list marks); the others always spawn with theirs. The owner can take any helmet off in the looks menu. |
| `ShieldChance` | 0.5 (0 to 1) | Chance a newly spawned NPC that can carry a shield gets one: Mordor and Misty Mountains orcs, Black Numenoreans and dwarves (each dwarf picks one of its two). NPCs with a two-handed weapon (the poleaxe, the dwarven battleaxe) never do. Shields are only for looks, and the owner can toggle one from the recruit's menu. |
| `HealthRegenSpeed` | 30 | Out-of-combat healing as a multiple of vanilla creature healing (30 = 0.3 health a second; a recruit heals fully in about 1.2 minutes). |
| `RegenDelaySeconds` | 10 | Seconds after its last hit before an NPC heals at that speed. |

## lotrnpcs-spawning.json

This file controls natural spawning, which only happens inside zones admins mark with `/recruits zone`. Each player standing in a zone gets their own spawn timer.

| Setting | Default | What it does |
| --- | --- | --- |
| `MinutesBetweenGroupsMin` / `Max` | 3 / 5 | Every 3 to 5 minutes (random), each player in a zone gets a roll for a group. |
| `GroupChance` | 0.5 | The chance each roll actually spawns a group (0 to 1; 1 = every time, as before 0.0.52). |
| `GroupSizeAvg` / `Var` / `Max` | 3 / 2 / 5 | Group size: average, plus or minus the variation, never above the maximum, for a faction with the baseline `MaxFollowers` (5). Since 0.0.90 other factions scale by their `MaxFollowers`: Dunlendings up to 6, orcs up to 7, goblins up to 8, elves and dwarves up to 4. |
| `WarriorShare` | 0.35 | Chance for each member after the first (always a recruit) to be a warrior. About 3 recruits per warrior. The Shire is all hobbit farmers. |
| `ArcherShare` | 0.2 | Chance for each member after the first to be an archer, for factions that have one (every faction but the Shire). Rolled before the warrior chance, and also applies to captain escorts. |
| `CaptainGroupChance` | 0.12 | Chance a group is instead a captain with an escort. |
| `CaptainEscortMin` / `Max` | 1 / 3 | Recruits, warriors and archers with a captain. The most scales like the group size (orcs 4, goblins 5, elves and dwarves 2). |
| `RivalGroupChance` / `RivalDistanceMin` / `Max` | 0.5 / 16 / 32 blocks | Where zones give the same ground to two factions at war (one zone listing both, or overlapping zones), the chance a group brings a group of the enemy 16 to 32 blocks away, so they fight. 0 turns it off. The second group still respects MaxNearby, the player distance and the zones. |
| `MinDistanceToPlayer` / `Max` | 40 / 80 blocks | How far from the player a group appears, always inside a zone that lists its faction. |
| `MaxNearby` / `NearbyRange` | 24 / 128 blocks | No group spawns while zone NPCs worth 24 are within 128 blocks of the player (24 since 0.0.94, owner: "increase the max natural cap by at least 3x"; was 8). Since 0.0.90 each counts by its faction: an orc 5/7, a goblin 5/8, an elf or dwarf 5/4, a man 1. |
| `MaxBlockLight` | 7 (0 to 32) | No spawns where torches and lamps light the ground brighter than this. Daylight doesn't count. |
| `MaxLifetimeMinutes` / `LifetimeDespawnDistance` | 20 minutes / 64 blocks | Since 0.0.91: a zone NPC alive longer than this disappears as soon as it isn't fighting and no player is within 64 blocks, so areas don't fill up while players stay nearby. 0 turns it off. Hired recruits and creative-spawned NPCs never despawn. |
| `DespawnAfterMinutesAway` / `DespawnDistance` | 5 minutes / 128 blocks | Zone NPCs farther than this from every player for that long disappear, so new groups keep coming. Creative-spawned and hired NPCs never despawn. |

The zones themselves aren't in this file. They are saved with the world and managed with `/recruits zone`.

## Related commands

Since 0.0.89 every command of the mod is under `/recruits` (`/help recruits` lists them all; `/help recruits <command>` explains one). For players: `dismissall`, `whitelist`, `orders`. For admins only: `faction` (was `/recruits faction`) and `zone` (was `/recruits zone`). These change things the config files don't hold.

| Command | What it does |
| --- | --- |
| `/recruits faction check <player>` | Which factions treat a player as a friend, and which attack them on sight. |
| `/recruits faction set <player> <faction>` / `clear <player>` | Make a player a faction member (replaces their race), or undo it. |
| `/recruits faction members [faction]` | Players an admin has set to a faction. |
| `/recruits faction list` | Every faction's stance, races, groups and allies (no prices or coin since 0.0.89; those are in the file). |
| `/recruits faction reload` | Apply edits to lotrnpcs-factions.json without a restart. |
| `/recruits zone add x z x z <factions>` | Mark a spawn zone between two corners. Zones may overlap and list several factions. |
| `/recruits zone list` / `here` / `remove <n>` | List zones, see the ones you stand in, or remove one. |
| `/recruits zone addfaction` / `removefaction` | Change which factions a zone spawns. |
| `/recruits zone patrol <n>` / `patrolclear <n>` | 0.0.112: add where you stand as the zone's next patrol point, or remove them all. With 2 or more points, wild groups spawned in the zone walk them in order, round and round, pausing at each; they still fight whatever they meet, and hiring one ends its patrol. Groups already spawned keep the route they had. |
| `/recruits zone spawnnow` | Spawn a group near you right away, for testing. |
| `/recruits xp <amount>` | 0.0.114, admins: give XP to the hired recruit or warrior you are looking at (a negative amount takes it away). XP stays between 0 and what its rank needs; filling it tells the owner it can be promoted. |
| `/recruits whitelist` | Any player (0.0.76): opens the whitelist menu, to add and remove players with buttons. |
| `/recruits whitelist add/remove/list <player>` | Any player: players your recruits never attack on sight. |
| `/recruits dismissall` (or `fireall`), then `/recruits dismissall confirm` | Any player: dismiss every recruit you have, as Dismiss in the talk menu does (fees not refunded). Ones far away are freed when their area next loads. |
