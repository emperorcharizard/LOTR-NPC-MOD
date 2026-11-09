# Prompt: lotrnpcs 0.0.44, Guard here, then a deep polish and optimization pass

You are working on **lotrnpcs**, a Vintage Story 1.22.7 code mod that adds Lord of the Rings faction NPCs (recruits, warriors and captains of 17 factions) to a LOTR-themed server. NPCs spawn in admin zones, fight by faction, and can be hired and given orders. The source lives in `/mnt/project-files/lotrnpcs/` (`src/*.cs`, `tools/gen_testnpcs.py`, `tools/bundle_lrc.py`, `tools/tests/`). The last release is 0.0.43.

The owner asked, in their words: *"do Guard here, then do a deep overhaul polish and refinement of the entire mod. and make it optimized"*. Do all three, in that order, and ship them together as **0.0.44**.

## Ground rules

1. **Don't break existing worlds.** These must keep working unchanged:
   - entity codes `test-*`, even though the "test" name is gone from the game;
   - save keys `lotrnpcs:hirelings`, `lotrnpcs:playerfactions` and `lotrnpcs:spawnzones` (new fields are fine, old saves must still load);
   - entity packet ids 46301 to 46305, the network channel `lotrnpcs-orders` and the hotkey code `lotrnpcs-hirelingorders`;
   - watched attribute names (`lotrnpcsOwnerUid`, `lotrnpcsOrder`, `lotrnpcsHoldX/Y/Z`, `lotrnpcsStance`, `lotrnpcsFormation`, `lotrnpcsSlotRight/Forward`) and the `lotrnpcsOrigin` attribute;
   - the config files `ModConfig/lotrnpcs-factions.json` and `lotrnpcs-spawning.json`. A format change bumps `FactionConfig.CurrentVersion` (now 6) and migrates older files once, keeping every value the admin set.
2. **Keep every decision the owner already made.** Faction relations (all enemies except the Mordor and Isengard example alliance), stances and their meanings, hire prices 5/15/40, MaxHirelings 5, rank health 22/34/48 (rankless 19), melee damage, regen 30 after 10 seconds, the outfits, portraits ("perfect now"), the hold-R orders menu layout rules, spawning rates. Polish means making these work better, not changing them.
3. **Don't decide what's still open.** Leave the hire coin default, the non-orc factions' stats and the Free Peoples alliance question as they are.
4. Player-facing text is plain, friendly English in full sentences. No em-dashes in messages.
5. Build against the reference DLLs in `tools/refs` (no game install). Both test suites must pass, and every new behavior gets a test.

## Part 1: Guard here and the whitelist

Decided in the design doc (2026-10-30): Guard here "wanders within a set radius of the spot where the order was given" and attacks "any non-friendly player inside the radius, even while the owner is offline". Under every order, hirelings "also fight NPCs that are hostile to the owner's side". "Friends never targeted: the owner, the owner's faction group, and the owner's whitelist." The whitelist is managed with `/hirelings whitelist add/remove/list <player>`. The radius is 16 blocks by default and set in the config. When the owner logs off, hirelings stay where they are.

Build it like this (these are interpretations; tell the owner about them):

- **Giving the order.** A new order `guard`, stored in `lotrnpcsOrder`. The guard post is where the owner stands when they give it, found on solid ground, saved in the hold coordinates. It can be given from the orders menu (movement page, "4. Guard here", applying to every hireling within command range) and from the talk menu ("Guard this area"). Follow, Hold and Move there replace it as usual. The movement page then numbers its formations 5 to 9 and 0.
- **Patrolling.** Guards walk to a random free spot inside the area every 6 to 16 seconds, at walking pace, and head back (running when far) whenever they end up outside it, such as after a chase. Formations don't apply to guards. They never teleport to the owner.
- **Who they attack.** The stance still decides how eager they are:
  - *Aggressive* (the default): anything aggressive hirelings already fight (NPCs hostile to the owner, monsters), but only inside the area, plus every player inside the area who isn't the owner, a /group mate, a member of a faction allied to the owner's, or on the owner's whitelist, and those players' hirelings. Creative and spectator players are never targets.
  - *Raid*: every non-allied creature inside the area, as raid already defines it.
  - *Defensive*: only fights back, as now. *Passive*: never fights.
  - Fighting back (grudges, anything going for the owner or a hireling) works as it does now.
- **Leash.** A guard never chases anything more than GuardRadius + 8 blocks from its post. It drops the chase and walks back instead, so it can't be lured away.
- **Owner offline.** Guards keep guarding and keep judging players by the owner's last known race, groups, faction and whitelist.
- **Whitelist.** `/hirelings whitelist add <player>`, `remove <player>` and `list`, for any player (privilege chat). Players are found by name, online or not (last known name). A whitelisted player counts like a /group mate of the owner: the owner's hirelings never attack them on sight under any stance or order, but still fight back if they hurt the owner or a hireling. It's one-way and per owner, saved in the owner's record under `lotrnpcs:hirelings`.
- **Config.** `GuardRadius` in `lotrnpcs-factions.json`, default 16, clamped to 4 to 64, read live so `/lotrfaction reload` applies it. Config version 7, with a `#Instructions` line explaining guarding and the whitelist.
- **Text.** Lang keys for the menu button, talk option and reply, order name ("guarding"), command results and the info text (for example "Your hireling: guarding its post, aggressive stance").

## Part 2: Deep polish and refinement

Go through every file. Fix bugs, robustness gaps and rough edges, and make the code consistent and well documented. At minimum:

- **Robustness.** A bad config file must never crash the server: duplicate faction ids that differ only in case, `null` entries in race, group or monster lists, duplicate price keys, missing relation sides. Log a warning and carry on.
- **Correctness.** Look for state that leaks or goes stale (grudge and disengage tables, closed dialogs, owner records), checks that run in the wrong order, and messages that name a hard-coded key when it may have been rebound (the "hold R" hint).
- **Code quality.** Misplaced or stale doc comments, redundant work (for example a full watched-attribute resync after storing gear), duplicated helpers (player lookup by name, stand-height search), magic numbers.
- **Player-facing text.** Consistent wording across the talk menu, orders menu, info text, chat replies and command help.
- **Generator and tests.** Keep `gen_testnpcs.py` comments accurate, keep the test suites readable, and test what changes.

## Part 3: Optimization

The AI asks "should I attack this?" for every creature near every NPC, about once a second each, plus every melee check, so the targeting predicates are the hot path. Profile them against 0.0.43 with a small benchmark and aim for **no allocations in steady state and several times faster**. Known costs:

- `FactionSystem.Relation` scans the relations list with `ToLowerInvariant` on every call. Precompute a relation table when the config is set.
- `FactionOf`, `RankOf`, `HasMind` and `IsMonster` re-read entity type JSON (with string copies and LINQ) on every call. Cache them per entity type.
- `IsFriend(faction, player)`, `AlliedPlayers`, `PlayerFactions` and `RaidTarget` rebuild a player's race, groups and factions every call. Cache each player's side for about a second, and drop the cache on config reload or `/lotrfaction set/clear`.
- Check cheap things first in `IsTargetableEntity` (alive, sensing range, game mode) before faction logic, and skip `IsTargetingTeam` for players.
- `SpawnZoneSystem.AllowSpawn` runs for every natural spawn attempt of every creature; make it a cached lookup.
- Spawn-item portraits are built on first render, so opening the LOTR NPCs tab builds about 50 at once. Build them a few at a time on the client after joining, and parse each race model file once.
- Avoid LINQ and copies in per-tick code (hireling upkeep, despawn checks) where it's easy.

Every cache must be invalidated correctly: config reload, `/lotrfaction set/clear`, group changes (time-based refresh is fine), whitelist edits.

## Part 4: Verify and deliver

1. Build, run both test suites, and run the benchmark against 0.0.43 and 0.0.44.
2. Package `lotrnpcs_0.0.44-test.zip` (gen, bundle LRC races, dll, CREDITS), compare its file list with 0.0.43, replace the old zip in `dist/`, and sync the sources.
3. Update the design doc's hiring section and the project notes.
4. Report briefly: what Guard here does and how to use it, the interpretations above, the main fixes, and the measured speed-up. Mention that it hasn't been tested in game yet.
