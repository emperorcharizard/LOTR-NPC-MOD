# LOTR NPCs: config guide

Nov 17, 2026 (updated for 1.0.3, faction file version 35, spawning file version 1; release 1.0.0) · @Emperorcharizar

## Overview

The mod has two config files in the server's `ModConfig` folder, plus one small file on each player's PC. The server files are created with their defaults on first start. Your values are kept on updates, and older files are upgraded once.

| File | Controls | After editing |
| --- | --- | --- |
| `lotrnpcs-factions.json` | Who fights whom, faction membership, hiring and promotion, stats per rank, band limits, healing, guard radius, battle behaviour, helmets and shields, testing commands | `/recruits faction reload` (no restart) |
| `lotrnpcs-spawning.json` | Natural spawning inside admin zones | Restart the server |
| `lotrnpcs-client.json` (each player's PC) | Animation detail for far NPCs | `.npcanimlod` in game, or restart the client |

Each server file starts with a `#Instructions` list explaining it. That list is rewritten on every start, so edit the settings below it, not the list itself. Don't change `Version`.

**What changed since the last guide (0.0.92).** The faction file went from version 21 to 34. The big ones:

- 0.0.94: the hire coin is the rusty gear (`game:gear-rusty`). The owner kept it for the 1.0.0 release.
- 0.0.113: guard and shock troopers, promotion branches of the warrior. 0.0.154: guard and shock recruits too.
- 0.0.117: riders, warriors promoted onto a mount (see "Riders").
- 0.0.135: every faction has the same numbers: the same health and damage per rank, 3 followers, a roster of 16 and the same prices. Files still at the old per-faction tables were moved once; tables you changed stay. NPC hits on NPCs do half damage (`NpcVsNpcDamageMultiplier`), so NPC fights last longer.
- 0.0.136: Retreat and Charge orders. 0.0.137: hits from behind do x1.5.
- 0.0.138: one captain per player (`MaxCaptains`) and 1 extra following place for recruits (`RecruitExtraFollowers`).
- 0.0.155: captains fight other NPCs harder (x1.5 dealt, x0.5 taken) but keep 48 health and 7 damage against players.
- 0.0.163: a rider's charge hits do x2.5.

## lotrnpcs-factions.json

This file decides who each NPC faction counts as a friend, which factions fight each other, what hiring costs, and how recruits behave.

### Joining a faction

A player's faction is the one they pick from the join menu, or the one an admin sets. Race and `/group` don't count (unless `RaceAndGroupMembership` is turned on, below).

- The menu opens right after a new player makes their character, and on every join until they have a faction. It lists every faction with `Joinable` true as a card under Free Peoples, Shadow or Others, with the faction's portrait, its name, how many recruits can follow at once and how many it can have in all. Picking asks for a confirmation, because it is permanent.
- A player who closes the menu without picking gets a reminder in chat: `/recruits join` opens it again. Until they pick, they belong to no faction: aggressive factions attack them on sight and they can't hire.
- After the pick only an admin changes it: `/recruits faction set <player> <faction>` moves them, `/recruits faction clear <player>` lets them pick again. `/recruits faction members` lists who is in what.
- Allies with `ShareFriends` treat each other's members as friends, and let them hire each other's NPCs.

| Setting | Default | What it does |
| --- | --- | --- |
| `Joinable` (each faction) | true | false leaves the faction out of the join menu. Admins can still set players to it. |
| `RaceAndGroupMembership` (top of the file) | false | true brings back the rule from before 0.0.92: a player is also a member when their race is in `FriendlyRaces` or they are in one of the faction's `FriendlyGroups`. |

### Per faction (under `Factions`)

One entry per faction, keyed by its id: gondor, rohan, dale, arnor, haradrim, dunlendings, blacknumenoreans, easterlings, dwarves, wickeddwarves, shire, elves, tormentedelves, mordor, angmar, mistymountains, gundabad, isengard.

| Setting | Default | What it does |
| --- | --- | --- |
| `Stance` | `aggressive` (Shire: `defensive`) | `aggressive` attacks every non-member player on sight. `defensive` only fights players who hurt it or its allies. Both attack enemy NPCs and monsters on sight. |
| `Joinable` | true | false leaves the faction out of the join menu. |
| `FriendlyRaces`, `FriendlyGroups` | per faction / empty | Only with `RaceAndGroupMembership` true: players of these Player Model Lib races, or in these /group names, are members. `/recruits faction check <player>` shows a player's race code. |
| `HireCoin` | `game:gear-rusty` | The item paid to hire and to promote, the same for every faction. Kept for the 1.0.0 release (owner's choice). |
| `HirePrices` | recruit 5, warrior 15, captain 40, archer 15 (every faction) | Coins per rank. Rankless NPCs cost a recruit's price. 0 makes hiring free. |
| `PromotePrices` | warrior 12, guard 12, shock 12, archer 12, captain 36 | What a promotion costs, by the rank it becomes: the hire price of that rank less 3 coins (less 4 for a captain). A rider costs the captain's price unless the list has `rider`. A rank left out gets the default. 0 makes it free. |
| `MaxFollowers` | 3 | How many of this faction's ranked recruits can follow a player at once. Each uses 1/`MaxFollowers` of the following (1 to 32). |
| `MaxRecruits` | 16 | How many of this faction's recruits a player can have. Each uses 1/`MaxRecruits` of the roster (1 to 256). |
| `Health` | recruit 22, warrior 34, captain 48, archer 35 (every faction) | Health per rank, a whole number. Guard and shock get the warrior's x1.25 and x0.8, guard and shock recruits the recruit's. A rider without its own entry has the warrior's x1.25. Loaded NPCs take a change after `/recruits faction reload`. |
| `Damage` | recruit 3.15, warrior 3.95, captain 7, archer 3.05 (every faction) | The damage of one hit per rank, melee and arrows (an archer's is its arrow and its shove). Guard and shock get the warrior's x0.8 and x1.25. |

A player is a member of the faction they picked or an admin set. Members can hire the faction's NPCs, and so can members of a faction allied to it with `ShareFriends` on. Members of the faction and of its allies are never attacked on sight.

### Equal factions (since 0.0.135)

The owner's call for PvP (10 v 10 players with their followers, up to 30 v 30): every faction has the same numbers. The table defaults above are the same for all 18 factions, so a fight between two factions is decided by numbers and tactics, not by which faction you joined. The factions still differ in looks, gear, names and mounts.

Ranks against each other, by design (tested in game on 0.0.150 to 0.0.165):

- A rider beats 2 warriors, loses to a captain, and goes for archers first (its hits on archers do x2).
- A captain fights other NPCs with x1.5 damage and takes x0.5 from them, so it beats a rider and about 2 warriors. Against players it has its plain 48 health and 7 damage, so a player in iron or steel armor can kill it.
- Guard and shock troopers trade health for damage (x1.25 / x0.8 and the other way round), so health x damage stays the warrior's.

You can still give factions their own numbers: edit `Health`, `Damage`, prices and limits per faction. The fight simulator in `tools/balance/` (see its README) shows how a change plays out.

**Hit spread.** `DamageSpread` (top of the file, 0.2): every NPC hit deals its damage times a random 80% to 120%, so same-rank fights aren't decided by whole numbers of hits. 0 turns it off.

**Shares.** With every faction at 3 followers and 16 recruits, any 3 ranked recruits can follow at once, from one faction or several allied ones (each uses 1/3 of the following). Talk menus and the follow order say what still fits ("Room in your band: ...", "Room on your roster: ...").

**Lowering limits.** When a player joins, or after `/recruits faction reload`, a player over the new limits loses the most recently hired recruits until they fit (far-away ones when they next load), and any borrowed shared guards go back to keep watch first.

### Sides: `Enemies` and `Allies` (each faction)

Each faction has two lines, the factions it fights and the ones it fights beside:

```json
"gondor": {
  "Enemies": ["haradrim", "dunlendings", "blacknumenoreans", "easterlings", "wickeddwarves", "tormentedelves", "mordor", "angmar", "mistymountains", "gundabad", "isengard"],
  "Allies": ["rohan", "dale", "arnor", "dwarves", "shire", "elves"],
  ...
```

| Relation | Effect |
| --- | --- |
| Allies | Never fight. They come to help each other within `CallForHelpRange`. |
| In neither line | Neutral: they leave each other's NPCs alone. |
| Enemies | Attack each other's NPCs on sight. |

**Default sides.** The Free Peoples (gondor, rohan, dale, arnor, dwarves, shire, elves) are allies of each other, and so is the Shadow (mordor, angmar, mistymountains, gundabad, isengard, haradrim, easterlings, blacknumenoreans, wickeddwarves). The two are enemies. The Dunlendings and the tormented elves are enemies of everyone (owner's choice).

**Changing a pair.** Move the name from one line to the other in either faction, then `/recruits faction reload`. One faction's line is enough: the mod copies the change into the other faction's lines and writes the file back. Allies win: if either faction lists the other as an ally, they are allies, so to make two allies enemies again, move the name back in both factions. A name in neither line of either faction makes the pair neutral.

**`Relations` (optional).** Entries for one pair, `{"A": "gondor", "B": "dale", "Relation": "ally", "ShareFriends": true}`, win over the lines. The only reason to use one is `ShareFriends: true` on an alliance: each faction's members are then also safe from the other's NPCs and can hire them. Without it, a player is only safe from their own faction. Empty by default.

Players: a member is safe from the NPCs of their own faction and of its allies (a Gondor player walks past Rohan NPCs), but hires only from their own faction unless the alliance has `ShareFriends` (owner's choice).

### Promotion and classes

A hired recruit or warrior earns 1 XP for each NPC of this mod that dies within 10 seconds of its hit. Monsters, animals and players give nothing; archers, captains and riders earn nothing.

| Rank | Needs | Can become |
| --- | --- | --- |
| Recruit (or guard / shock recruit) | 15 XP | Warrior, guard, shock trooper or archer |
| Warrior, guard or shock trooper | 40 XP | Captain or rider |

Promotion needs both the XP and the coins in `PromotePrices`. The owner promotes from the talk menu ("Promote...") or the roster. A promoted NPC keeps its name, face, orders, post and weapon, and wears the new rank's armor.

- **Captains.** `MaxCaptains` (1) per player, whatever their faction. Hiring or promoting past it is refused; the promotion screen says "Captain places full". Riders don't count.
- **Recruit classes** (0.0.154). A recruit can be a plain, guard or shock recruit. The owner changes it for free with "Let me change your class." in the talk menu.
- **Riders** (0.0.117). A warrior, guard or shock trooper with 40 XP can become a rider, on its own saddled mount, which follows its orders. Riders fight with the warrior's health and damage x1.25 unless `Health` and `Damage` list `rider`. Their charge hits do x2.5. Mounts come from other mods and are optional: Feverstone Wilds horses (Gondor, Rohan, Dale, Arnor, Dunlendings, Haradrim, Easterlings, Black Númenóreans, tormented elves) and tame dire wolves (Mordor, Angmar, Gundabad, Misty Mountains, Isengard), Teapot's Giant Boar boars (dwarves and wicked dwarves), and the game's own caribou, made rideable by this mod (elves). Without the mount's mod that faction has no rider rank. The Shire has no riders.

### Everything else (top of the file)

| Setting | Default | What it does |
| --- | --- | --- |
| `MaxFollowers`, `MaxRecruits` | 3, 16 | The baseline, used for a faction without its own values. |
| `MaxCaptains` | 1 (0 = no limit) | Captains per player, all factions together. Replaces `RosterPerCaptain` (0.0.138). |
| `RecruitExtraFollowers` | 1 (0 to 10) | Following places for recruits only, past the 3 ranked places. So a band is up to 3 followers of any rank and 1 more recruit. A follower promoted while the ranked places are full guards where it stands until a place frees. |
| `MonsterCodes` | drifters, bowtorn, shivers, bells, locusts | Creatures every faction attacks on sight. `*` is a wildcard, e.g. `game:drifter-*`. |
| `GrudgeSeconds` | 30 | How long NPCs keep fighting someone who hurt them or an ally. |
| `CallForHelpRange` | 16 blocks | How far allies come to help. |
| `JoinFightRange` | 32 blocks (0 = off) | How far a wild NPC that starts a fight calls idle wild NPCs of its faction; they call in turn, so a spread-out army joins. |
| `NpcVsNpcDamageMultiplier` | 0.5 (0 to 10) | Every hit this mod's NPCs land on anything that isn't a player is this times its damage (melee, arrows, charge hits after their x2.5). Hits on players, and players' hits, don't change. |
| `BackHitMultiplier`, `BackArcDegrees` | 1.5, 120 | A hit on one of this mod's NPCs from within a cone this wide behind it does this times the damage. Fighting NPCs keep turned toward the nearest enemy and step backwards instead of turning their back. |
| `RetreatSeconds` | 10 (1 to 60) | After the Retreat order, followers run back to their owner and start no fights for this long. |
| `DamageSpread` | 0.2 (at most 0.9) | Random spread on every NPC hit (see above). |
| `SpawnSafeRadius` | 64 blocks (0 = off) | Within this many blocks of the world spawn, no recruit starts a fight with a player. They still fight back. |
| `WildBodyLoot`, `HiredBodyLoot` | rusty gears (recruit 0-1, warrior and archer 1-2, captain 3-5) plus up to 1 cloth (recruits) or leather | By rank, what a dead wild NPC's body and a dead recruit's body hold, as `{ "Code", "Min", "Max" }`. A player harvests the body with a knife. Armor and weapons are never dropped. Rankless NPCs use the recruit list. Bodies rot away after 3 in-game hours. |
| `GuardRadius` | 16 blocks (4 to 64) | How far around their post guards patrol and attack intruders. |
| `MaxAttackersPerTarget` | 4 (0 = no limit) | How many NPCs may fight one NPC at a time. The rest look for another enemy or wait just outside. Players are never limited, and an NPC always strikes back at whoever hits it. |
| `CombatSpacing` | 1.5 blocks (0.8 to 4) | How far apart fighting NPCs of the same side keep, and the gap between neighbours in a battle line. |
| `DuelDistanceMin`, `DuelDistanceMax` | 1.0 and 1.6 blocks | How far from the NPC it fights an attacker stands. Kept within 0.6 to 2.2. Players are approached the vanilla way. |
| `BattleLines` | true | Recruits following in any formation but Loose line up when they meet another faction's NPCs, and wrap round the enemy's flanks when they outnumber it. Defensive recruits hold the line until an enemy is within `LineHoldRange`. |
| `WildBattleLines` | false | true makes wild NPCs form lines too. Off, they walk straight in, each taking the nearest enemy with room: smoother and lighter on the server. |
| `LineHoldRange` | 6 blocks (2 to 24) | See `BattleLines`. |
| `NpcHitsIgnoreHurtWindow` | true | NPC hits on NPCs and creatures land even within half a second of another hit (the game's hurt window), so every attacker and arrow counts. Players always keep the hurt window. |
| `HelmetChance` | 0.6 (0 to 1) | Chance a newly spawned NPC that may go bare-headed wears its helmet. Helmets are only for looks; protection comes from rank. The owner can put it on or off in the looks menu. |
| `CaptainsAlwaysHelmeted` | false | true makes every captain always wear its helmet. |
| `ShieldChance` | 0.5 (0 to 1) | Chance a newly spawned NPC that can carry a shield gets one. Never with a two-handed weapon. Only for looks; the owner can toggle it in the looks menu. |
| `HealthRegenSpeed` | 30 | Out-of-combat healing as a multiple of vanilla creature healing (30 = 0.3 health a second). |
| `RegenDelaySeconds` | 10 | Seconds after its last hit before an NPC heals at that speed. |
| `DebugCommands` | false | Turns on the testing commands: `/recruits battlelog`, `xp`, `bench` and `zone spawnnow`. Off, they refuse and say how to turn them on. |

## lotrnpcs-spawning.json

Natural spawning only happens inside zones admins mark with `/recruits zone`. Each player standing in a zone gets their own spawn timer.

| Setting | Default | What it does |
| --- | --- | --- |
| `MinutesBetweenGroupsMin` / `Max` | 3 / 5 | Every 3 to 5 minutes (random), each player in a zone gets a roll for a group. |
| `GroupChance` | 0.5 | The chance each roll spawns a group. |
| `GroupSizeAvg` / `Var` / `Max` | 3 / 2 / 5 | Group size: average, plus or minus the variation, never above the maximum. The same for every faction (groups scale with a faction's `MaxFollowers` over the baseline, and all are 3 now). |
| `WarriorShare` | 0.35 | Chance for each member after the first (always a recruit) to be a warrior. The Shire is all hobbit farmers. |
| `ArcherShare` | 0.2 | Chance for each member after the first to be an archer (every faction but the Shire). Rolled before the warrior chance. |
| `RiderChance` | 0.06 | Chance that one warrior of a group comes mounted, when the faction's mount mod is installed. |
| `CaptainGroupChance` | 0.12 | Chance a group is instead a captain with an escort. |
| `CaptainEscortMin` / `Max` | 1 / 3 | Recruits, warriors and archers with a captain. |
| `RivalGroupChance` / `RivalDistanceMin` / `Max` | 0.5 / 16 / 32 blocks | Where zones give the same ground to two factions at war, the chance a group brings an enemy group 16 to 32 blocks away, so they fight. 0 turns it off. |
| `MinDistanceToPlayer` / `Max` | 40 / 80 blocks | How far from the player a group appears, always inside a zone that lists its faction. |
| `MaxNearby` / `NearbyRange` | 24 / 128 blocks | No group spawns while 24 zone NPCs are within 128 blocks of the player. |
| `MaxBlockLight` | 7 (0 to 32) | No spawns where torches and lamps light the ground brighter than this. Daylight doesn't count. |
| `MaxLifetimeMinutes` / `LifetimeDespawnDistance` | 20 minutes / 64 blocks | A zone NPC alive longer than this disappears as soon as it isn't fighting and no player is within 64 blocks. 0 turns it off. |
| `DespawnAfterMinutesAway` / `DespawnDistance` | 5 minutes / 128 blocks | Zone NPCs farther than this from every player for that long disappear, so new groups keep coming. |

Hired recruits and NPCs from creative spawn items never despawn. The zones themselves are saved with the world and managed with `/recruits zone`.

## lotrnpcs-client.json (each player's PC)

`FarAnimationLod` (true) makes this mod's NPCs farther than `AnimationFullRateWithin` blocks (10; 4 to 256) animate every 2nd frame, past twice that every 3rd and past three times that every 4th, which raises FPS in big battles with no change up close. `.npcanimlod on`, `off` or `.npcanimlod <blocks>` changes it in game and saves it.

## Commands

Every command is under `/recruits` (`/help recruits` lists them; `/help recruits <command>` explains one).

**Any player**

| Command | What it does |
| --- | --- |
| `/recruits join` | Opens the faction menu, until you have a faction. |
| `/recruits orders` | Opens the orders menu (the same as holding R). |
| `/recruits roster` | Every recruit you have, wherever they are, with buttons to find, call, promote or dismiss them. |
| `/recruits whitelist` (or `add` / `remove` / `list <player>`) | Players your recruits never attack on sight, even on Guard here or the raid stance. With nothing after it, opens a menu. |
| `/recruits dismissall`, then `/recruits dismissall confirm` | Dismiss every recruit you have (fees not refunded). |

**Admins**

| Command | What it does |
| --- | --- |
| `/recruits faction check <player>` | Which factions treat a player as a friend, and which attack them on sight. |
| `/recruits faction set <player> <faction>` / `clear <player>` | Put a player in a faction, or let them pick again. |
| `/recruits faction members [faction]` / `list` | Players in each faction; every faction's stance and allies. |
| `/recruits faction reload` | Apply edits to lotrnpcs-factions.json without a restart. |
| `/recruits zone add x z x z <factions>` | Mark a spawn zone between two corners. Zones may overlap and list several factions. |
| `/recruits zone list` / `here` / `remove <n>` | List zones, see the ones you stand in, or remove one. |
| `/recruits zone addfaction` / `removefaction` | Change which factions a zone spawns. |
| `/recruits zone patrol <n>` / `patrolclear <n>` | Add where you stand as the zone's next patrol point, or remove them all. With 2 or more points, wild groups walk them in order. |

**Testing** (only with `DebugCommands` on)

| Command | What it does |
| --- | --- |
| `/recruits battlelog on` / `off` | Writes every NPC hit and death to a CSV in the Logs folder; with nothing after it, prints the totals. |
| `/recruits zone spawnnow` | Spawn a group near you right away. |
| `/recruits xp <amount>` | Give XP to the hired recruit you are looking at. |
| `/recruits bench <count>` / `bench clear` | Frame-rate test: spawns Gondor against Mordor in front of you and times frames and server ticks for 30 s. |
