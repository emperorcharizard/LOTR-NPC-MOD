---
name: lotrnpcs-build
description: How the lotrnpcs mod is built and where its source, test zips and build tooling live (no game DLLs available)
metadata:
  type: project
  modified: 2026-11-03T06:20:00.000Z
---

Mod id `lotrnpcs`. Source and builds in /mnt/project-files/lotrnpcs/ (src/, tools/, dist/ holds only the latest zip). PROVEN: NPCs render PML race models when our `lotrnpcs:presetskinmodel` (BEFORE PlayerModelLib:ExtraSkinnable) sets watched attr skinModel to a "domain:key" code. Melee AI works; on-sight attacks follow world creatureHostility. User tests singleplayer with LRC and server mods on, and runs PML 1.24.0 (modinfo still says 1.23.8).

Looks from LRC 0.4.1, bundled with the authors' permission and credit; NO LRC dependency. tools/bundle_lrc.py copies the LRC races we use into assets/lotrnpcs as PML models lotrnpcs:<race> (Enabled:false, traits cleared); tools/gen_testnpcs.py writes entities, items, lang, modinfo. GOTCHA: a // comment before the first { breaks PML config parsing. Armor: vanilla `seraphinventory` + our server `lotrnpcs:startinggear` (first spawn, random complete set from TRTA mods' own codes, else vanilla `items`); client `lotrnpcs:geartextures` (BEFORE ExtraSkinnable) wraps PML's texture source. PML RandomizeSkin skips a part when a vanilla seraphrandomizer constraint names a code the part lacks. Gear doesn't drop (seraphinventory dropContentsOnDeath false). Dwarves are beardless unless the beard is preset (PML forces 'none' 70%). 0.0.26: own creative tab, prefixandcreature-<code> lang keys (death message), Easterling kniaz hair and hobbitskin7 dropped (grey). Don't touch LRC dwarf beards dwarf-1..4 'CHARmod/hair' texture: harmless warning; removing the textures key made PML NRE and dwarves invisible (0.0.33, reverted 0.0.34).

PML 1.24.0 BUG: PlayerSkinBehavior.UpdateEntityProperties calls SetZNear for every skinned entity, shrinking the camera near plane per NPC (render holes, flicker). Fixed in 0.0.28 by `NpcCameraGuard` Harmony prefix: only the local player's entity may run it. User confirmed fixed.

Build facts:
- Since 0.0.29: compiled against REAL reference DLLs in tools/refs (VintagestoryAPI + VSEssentials built from open-source vsapi/vsessentialsmod 1.22.7, AssemblyVersion forced 1.0.0.0; 0Harmony 2.4.2 NuGet; see tools/refs/README.txt). Target net10.0. `cd src && dotnet build -c Release -o out`.
- Real refs are runnable: tools/tests/spawnzones (DispatchProxy fakes for ICoreServerAPI) and tools/tests/factions load generated entity JSON. Lang isn't loaded in the zone harness, so parser error texts throw.
- .NET: apt-get update first, then apt dotnet-sdk-10.0 (DOTNET_ROOT=/usr/lib/dotnet). Factions test: build with -o bin/run, copy vanilla-*.txt to bin/, and put seraph-faceless.json (FA Races zip) in <gen>/assets/lotrnpcs/shapes/entity/humanoid/. Quick packaging when only generated JSON/DLL change: unzip last dist zip, copy changed files from a fresh gen (PNGs differ only by C2PA chunk), rezip.
- Assets: python3 -I tools/gen_testnpcs.py <out>, then python3 -I tools/bundle_lrc.py <lrc assets/lrc> <out>/assets/lotrnpcs; copy lotrnpcs.dll + tools/CREDITS.txt; zip modinfo.json, dll, CREDITS.txt, assets.
- After a DLL change: full game restart and ONE lotrnpcs zip in Mods.
- Test world runs VS v1.22.7; server version unconfirmed, so no "game" dependency in modinfo.

0.0.30 spawning: SpawnZones.cs (/lotrzone, savegame key lotrnpcs:spawnzones, ModConfig/lotrnpcs-spawning.json); spawner logic takes PlayerSpot because the harness can't fake IPlayer.

0.0.38-0.0.42: names without "Test:" (codes stay test-*). Spawn items show PML head-and-shoulders portraits (src/Portrait.cs). Creative tab name needs lang key "game:tabname-lotrnpcs". test-goblin/test-uruk hidden from creative (entities kept); hobbit back in 0.0.42 (user). Portrait textures: body/skin codes must win over gear (vanilla gear adds unprefixed codes like "seraph"). User 2026-11-03: portraits "perfect now". Regen 30 (config v6). Outfits: [[lotrnpcs-outfits]]. Angmar shapes log harmless empty step-parent warnings.

0.0.44 (2026-11-03): config v7 (GuardRadius); 0.0.46 tests: factions 241, spawnzones 51, weapons in [[lotrnpcs-weapons]]; benchmark tools/tests/bench and hot-path notes in [[lotrnpcs-performance]]. Portraits prebuild client-side once the player is in creative mode.

Related: [[npc-mod-maker-goals]], [[vs-base-code-reference]]
