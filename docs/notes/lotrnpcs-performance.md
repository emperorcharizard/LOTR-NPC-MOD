---
name: lotrnpcs-performance
description: lotrnpcs hot-path facts: targeting benchmark (tools/tests/bench) and 0.0.43 vs 0.0.44 numbers, VS API costs found (string.Intern in GetString), cache rules
metadata:
  type: project
  modified: 2026-11-03T06:14:38.099Z
---

User asked 2026-11-03 for "a deep overhaul polish and refinement of the entire mod. and make it optimized" (0.0.44). The job prompt is saved at /mnt/project-files/lotrnpcs/prompts/guard-and-polish.md.

Benchmark: tools/tests/bench (bench.csproj takes -p:ModDll=<dll> so it builds against any version with the 0.0.43 API). 68 creatures, 24 wild NPCs + 10 hirelings judging all, 4.6M checks, plus a per-kind breakdown. Run: dotnet build -c Release -o outNN [-p:ModDll=...]; copy tools/refs/*.dll and the mod dll into outNN; dotnet outNN/bench.dll <generated assets/lotrnpcs/entities>. The "yes" count must match between versions (same answers).
Results (2026-11-03, this container, noisy): 0.0.43 about 390 ns and 138 bytes per check; 0.0.44 about 80-140 ns and 0 bytes. About 4x faster.

Non-obvious VS API costs (VS 1.22.7 source):
- TreeAttribute.GetString runs string.Intern on every read (global table lookup); EntityPlayer.PlayerUID goes through it. On hot paths read (tree[key] as StringAttribute)?.value instead (lotrnpcs Attrs.String / Attrs.Uid). Was the single biggest cost.
- Server World.LoadedEntities is a ConcurrentDictionary: .Values copies the whole table under all locks; enumerate the dictionary itself.
- ConditionalWeakTable is used for per-entity-type caches (NpcTypeInfo: faction, rank, has AI, maxhealth, monster flag stamped per config) because spawn vetoes may run off the main thread.

Cache rules in 0.0.44 (keep when changing code): relation table rebuilt in SetConfig; player sides cached by entity (online, 1 s expiry, race reference check) and by uid from owner records (reference checks on record, Race, Groups); SetConfig/LoadAssigned clear both, SetAssigned/SetOwner/disconnect call ForgetSide; whitelist is read live. Refresh must assign NEW lists/strings when values change (never mutate in place) or the record cache won't notice.

Frame rate with many NPCs (2026-11-11, owner's client log with `.debug logticks 30`, fixes for 0.0.114, untested in game):
- FPS fell as NPCs were spawned and when they started fighting. Vsync was on (clientsettings vsyncMode 1), so frames over 16.7 ms snap to 33.3 ms (30 FPS): at about 250 drawn entities every frame took 33.27 ms. Per drawn entity about 26-30 us CPU in esr-afteranim; the rest of the frame was in `end` (GPU or vsync wait, the log can't tell which). Synergy's stats: 33 active of 168 before the fight, 212 of 291 after; awake NPCs animate every frame. The mod's AI and tick code did not show in the client profile.
- Whole-model rebuilds (esr-tesseleateshape, 5-19 ms each on the main thread) about 10 a second in the fight. VS 1.22 facts behind them (read from the game DLLs' IL): SyncedTreeAttribute.RemoveAttribute always calls MarkAllDirty, even for a missing key, and DeleteAttributeByPath on a top-level key calls it too; a full sync (FromBytes) runs every modified listener, so the gear (EntityBehaviorContainer.loadInv), Player Model Lib's skinConfig listener and ours all call MarkShapeModified. EntityBehaviorContainer.ToBytes calls storeInv, which marks the gear dirty on every save and every send to a player.
- Fixes: Recruit.DropWatched removes a key from TreeAttribute's internal `attributes` dictionary and marks just that path (the update carries no data and the client's PartialUpdate deletes the key; null Data is not written by Packet_PartialAttribute); a missing key does nothing. GearSyncGuard (LotrNpcsModSystem.cs) is a Harmony prefix that skips storeInv for lotrnpcs NPCs on the server when the gear bytes are unchanged. Weapons.Choose's weapon removal and Looks' kept-helmet removal go through DropWatched too.
- Stragglers (battle log 2026-11-11, goblin 5205 17 blocks out took about 20 s to arrive alone): BattleLineSystem.Join now joins a same-side line whose enemy center (Ex, Ez) is within JoinRadius of the NPC's target before starting a line of one.
- Log noise fixed: LRC's ear and goblin face parts carried a second "Eyes" attachment point (bundle_lrc.py drop_eyes); Angmar's armor parts have stepParentName "" (the game leaves them under their own parent, so they show, but warns each time): tools/angmar_stepparent_patch.py writes tools/patches/angmar-stepparent.json (514 removes, dependsOn lotrangmar), copied into the mod by build.py. Rerun it if Angmar updates its shapes.

Related: [[lotrnpcs-build]], [[lotrnpcs-hiring]], [[npc-ai-tuning]]

Benchmark in game (0.0.114, owner 2026-11-12: "add it", to get FPS for other PC tiers): `/recruits bench <2-300>` (src/Bench.cs) spawns Gondor warriors and archers against Mordor orcs 16 blocks ahead of the admin, waits 5 s, then times the admin's client frames (an IRenderer at EnumRenderStage.Done) and the server's ticks for 30 s; BenchStats.Summarize prints average, 1% low (average of the slowest 1%) and worst. Bench NPCs carry the entity attribute lotrnpcsBench and stay until `/recruits bench clear` despawns them. The only measured PC so far is the owner's (i7-12700KF, RTX 3070, 32 GB). GPU use can't be read from a mod.

In-game results, 2026-11-12, run by Claude through computer use on the owner's PC (i7-12700KF, RTX 3070, 32 GB; world "Emperorcharizars Superflat Place", creative, superflat, day, vsync setting 1, max FPS 241, view distance 256). Bench lines from client-main.log / server-main.log:

| NPCs | client avg | 1% low | worst frame | server ticks/s (worst tick) |
| --- | --- | --- | --- | --- |
| 50 | 153.1 FPS (6.5 ms) | 84.7 FPS (11.8 ms) | 17 ms | 29.3 (36 ms) |
| 100 | 96.8 FPS (10.3 ms) | 51.1 FPS (19.6 ms) | 42 ms | 29.3 (38 ms) |
| 100 (rerun) | 94.6 FPS (10.6 ms) | 48.5 FPS (20.6 ms) | 26 ms | 29.2 (44 ms) |
| 200 | 56.3 FPS (17.8 ms) | 31.7 FPS (31.5 ms) | 37 ms | 29.2 (46 ms) |

- The server holds about 29 ticks/s even at 200 NPCs fighting: the mod's AI is not the limit on this PC.
- 114.3: `.debug logticks` is a toggle (a second call turns it off, whatever the number). With threshold 0, about 1000 consecutive frames with 150-256 NPCs drawn after a fight had no esr-tesseleateshape at all (0.0.113: about 10 rebuilds a second). esr-afteranim was 30-34 us per NPC, the bulk of each frame. One 40 ms frame was doneMTT (27 ms) when 100 NPCs spawned at once. With threshold 30 ms during the 200 fight, no frame was logged.
- 114.6: client-main.log and client-debug.log have 0 "Eyes" duplicate warnings (previous session 210), 0 Angmar step-parent warnings, 0 failed patches. The 144 "Missing mapping for texture code #missing_texture" errors for iseng Dunlending, arnor heavier and angmar armor bodies were already there before 0.0.114 (288 in earlier sessions): those armor mods' own shapes.
- 114.7: lotrnpcs-factions.json is version 30 with no faraces entries.
- 114.8: only the refusal checked (looking at the ground gives the error). The owner has no recruits in this world, so the rest needs a hire first.

## 0.0.116: animation LOD for far NPCs (2026-11-12, prompts/fps-optimization.md)

Research, from the game's IL (VintagestoryLib SystemRenderEntities.OnBeforeRender, VintagestoryAPI AnimationManager.OnClientFrame) and the installed mods:
- Every frame, for each entity renderer, the game frustum-culls (SphereInFrustum + IsChunkRendered sets Entity.IsRendered), then calls AnimManager.OnClientFrame; the profiler mark esr-afteranim follows it. OnClientFrame runs Animator.OnFrame (ClientAnimator: keyframe blend and calculateMatrices over every joint/element of the composite shape) only when the entity IsRendered or IsShadowRendered. So off-screen NPCs already cost nothing; the cost is all in visible ones, per element, every frame, at any distance.
- OptiTime 1.5.12 has "Distance-based animation LOD (skip frames for far entities)" (EntityAnimationOptimizations) but turns it off when Combat Overhaul / Overhaul Lib is loaded ("Overhaullib compatibility mode"): OverhaullibLegacyCompat 1.2.15 patches AnimationManager.OnClientFrame (AnimationPatches.AnimationManagerOnClientFrame) and ClientAnimator.calculateMatrices for its player animations. That is why it shows "OFF (Conflict)" on the owner's PC.
- Rejected for now: cutting elements of the armor shapes (other mods' assets; a visible risk and a big job), lowering our NPCs' render distance (they would pop out of a battle), and skipping shadow-only animation (small).

Built: src/AnimationLod.cs, a Harmony prefix (Priority.First) on AnimationManager.OnClientFrame that only acts on rendered entities of the lotrnpcs domain. Within AnimationFullRateWithin blocks of the player (default 20) they animate every frame; to 2x that every 2nd frame, to 3x every 3rd, beyond every 4th. A skipped frame's time is added to the next run, so animations keep their pace, and each NPC's count starts at its entity id so they skip different frames. Players and other mods' entities are never touched, and nothing changes on the server. Client config ModConfig/lotrnpcs-client.json (FarAnimationLod, AnimationFullRateWithin); chat command `.npcanimlod on | off | <blocks>` switches it live and saves it. Tests: Interval and Step in the factions suite.

Measured on the owner's PC (i7-12700KF, RTX 3070, vsync setting 1, max FPS 241, view distance 256), `/recruits bench 200` (armies 10 to 22 blocks ahead, so this is close range):

| Setting | avg FPS | 1% low | worst frame | server ticks/s |
| --- | --- | --- | --- | --- |
| off (as 0.0.115) | 64.7 | 36.0 | 38 ms | 29.3 |
| 20 blocks (default) | 70.4 | 38.2 | 33 ms | 29.3 |
| 10 blocks | 103.2 | 48.3 | 26 ms | - |
| 4 blocks | 122.2 | 53.6 | 23 ms | 29.3 |

Mid-fight screenshots at 20 blocks looked normal (walking, shields, bows). In real battles NPCs spread farther than the bench, so the default helps more there than in this table. The default is the owner's call: 10 nearly doubles the bench's FPS, with far NPCs animating at half rate past 10 blocks.

