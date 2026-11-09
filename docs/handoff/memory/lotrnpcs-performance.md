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

Related: [[lotrnpcs-build]], [[lotrnpcs-hiring]], [[npc-ai-tuning]]
