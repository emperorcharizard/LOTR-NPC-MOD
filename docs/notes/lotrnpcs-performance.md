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
