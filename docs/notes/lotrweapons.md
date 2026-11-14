---
name: lotrweapons
description: LOTR Weapons, the second mod - the owner's custom NPC weapons as player items (iron/steel), decisions and progress
metadata:
  type: project
  modified: 2026-11-14
---

Owner, 2026-11-14: "take all the weapons so far that are custom made then make them into items for players as a
different mod, same rigs and animations. And to eventually begin creating recipes and forge items on anvil."
Full plan: `prompts/player-weapons-mod.md`.

## Decisions (owner, 2026-11-14)
- Mod id `lotrweapons`, "LOTR Weapons".
- Vanilla item classes plus a Combat Overhaul JSON patch (`dependsOn`), "so it can still be used without the mod".
- Stats copy the nearest game weapon.
- Every melee weapon in two variants, iron and steel ("the skin difference can be solved later"). The three pairs the
  owner already made (Gondorian sword, dwarven warhammer, dwarven shorthammer) become one weapon each, steel variant
  on the steel model: 29 melee weapons, 58 items.
- Shields and bows: iron only.
- Order: melee weapons first ("Do melee weapons first"), then shields, then bows.
- Still open: where the mod lives (recommended: `lotrweapons/` in this repo, sharing the catalog).

## Progress
- Phase 1 done (2026-11-14): the owner's 32 melee entries, `hand_transform`, the texture tables and the shape writer
  (`weapon_shape`, `copy_own_textures`) moved from gen_testnpcs.py to `tools/weapon_catalog.py` (`MELEE` = all 32).
  gen loads it by path and writes lotrnpcs' weapons through it with domain `lotrnpcs`, folder `orcweapon`. Proof: gen
  output (282 files) byte-identical before and after (`diff -r`). Shields and bows stay in gen until their phase.
  Not built with dotnet here (no SDK in the cloud container); the change touches only gen's Python.
