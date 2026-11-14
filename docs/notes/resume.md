---
name: resume
description: Where the lotrnpcs work stands and what's next; read first, update before stopping
---

# Where we are (updated 2026-11-13, after 0.0.122)

**Latest build:** 0.0.124 (idle wild allies within JoinFightRange, 32, join a fight in hops, `CallIdleAllies`; 0.0.123 used 16) on top of 0.0.122: wild NPCs fight without battle lines (`WildBattleLines`, false by default; recruits in formation keep lines), 0.0.120/0.0.121 pairing removed (owner chose "Back to 0.0.119 fighting"). In game (17:51): Dunlendings 24-2, only 24 of 38 orcs fought (0.0.123: 27, 26-10); 14 orcs stayed alive and never joined, so the call range isn't the cause (see lotrnpcs-0113.md 0.0.124). 0.0.121 (pair by position) ran 28-0 to the Dunlendings and is superseded. Before it, 0.0.119, GitHub release v0.0.119. Commits up to `1a1c32e` and later doc commits on `main`.

**Recent work (full details in `lotrnpcs-0113.md`):**
- 0.0.118: a battle line counts as met when another line of its side within 24 blocks has met (`SideMetNear`).
- 0.0.119 (owner: "only if they are set in the same formation"): idle allies of the same side and formation join a forming line (`Rally`, "joinLine" notify). In game, whole armies now form one long line before contact, and 60 of 64 NPCs fought (40 before).

- 0.0.120: Rally asks before adding (`WouldRally`), and after contact members pair with the enemy nearest their slot, at most 2 per enemy (`Pair`), for the 110.1 blob. In game (17:10): blob NOT fixed, Dunlendings 27-7, new line-met-front idle; see the 0.0.120 section of lotrnpcs-0113.md.

**Owner's goal for battles (2026-11-13, after the 0.0.121 run), read before more line work:** these are still wild NPCs. The goal is NOT long lines fighting each other, only no giant blob of units; two ranks would be fine. The current line movement looks very choppy. Each test should show what changed from the last one, so compare runs with the same staging.
Later (owner): "I just want combat to be as smooth as possible and as optimized lag wise as possible." Next test to try needs no code: `BattleLines: false` and `MaxAttackersPerTarget: 2` in ModConfig/lotrnpcs-factions.json (the BattleSpacing cap, second ring and push in Battle.cs already exist), same 38 v 26 staging, both armies spawned before the log starts.
And (owner): "not too many conflicting paths": one system decides where a fighting NPC walks (not line slot vs pairing vs rally vs surround spot pulling against each other).

**0.0.125 (riders, owner 2026-11-13):** a seated rider skips its walk, run and idle animations (`Riders.IsLegAnimation`, via `AnimationManager.OnStartAnimation` in `EntityBehaviorRider`), so only the game's seat pose (rideelk, started by `EntityAgent.doMount`) moves its legs; riders weight spears x3 (`held_weapon` in gen_testnpcs.py); `MountedExtraReach` 1 block (was 0.5). Checklist 125: in game they sit and hold spears, but wandered off and parked in fights.
**0.0.126:** the mount walks unless the rider's task moves at `Riders.RunTaskSpeed` (0.029) or more (seek, hide, running follow), so wandering riders stop galloping out of Synergy's range; in a fight a standing mount walks in to `CloseInDistance` (2.5) of its enemy. Checklist 126, not yet in game.

**Owner, 2026-11-13 evening:** fine with a blob if it makes fights simpler; the one requirement is that NPCs never pass through each other (solid bodies). Lag research doc: https://claude.ai/artifact/3KeDXfgY6VNmL21Z75dF9s (proposed 0.0.127: wild NPCs skip line/surround code, a hard contact rule replacing repulseagents between our NPCs, idle scan 2.5 s). Waiting on the owner's go.

**Owner, 2026-11-14 (supersedes the no-blob goal above):** "I am now fine with the blob if that makes fights simpler, my only request is that the mobs dont intersect each other, like they have a solid surface that doesnt let other npcs phase through", plus: research the laggiest parts of the mod. Findings and plan: the 2026-11-14 section of `lotrnpcs-performance.md`.

**Open problems:**
1. **110.1, lines fold into a blob at contact.** Within ~10 s of meeting, both lines collapse into one blob; up to 7 orcs hit one Dunlending in 20 s. The 38 orc v 26 Dunlending fight ended 24-4 for the orcs (110.1 wants it closer than 26-2). Look at what the front and the extras do at contact: front members take the nearest free enemy and hold their side (`HoldsFront`); extras wrap round (`HoldSlot` returns false once the line has met). Staging: batches of 5, columns 40 blocks apart, player at 1500 30 300 (see the "Staged" entries in lotrnpcs-0113.md).
2. Checklist items still open in 110-119: 110.1, 112.2 (needs a second player), 112.6, 112.8 (weapons and shields in hand, by eye), 113.5, 113.8, 113.9, 114.8, 116.2, 117.6 (needs Teapot's Giant Boar), 117.8 (needs Feverstone Wilds removed), 119.2 (recruits in two formations).
3. Sections 1-109 of the checklist were never ticked, although many were tested back then (results in the older notes).

**Test world:** "Emperorcharizars Superflat Place", creative, superflat. The battle area is around 1500 3 300. The owner's hired goblin Muckrot is in the roster; leave it alone.
