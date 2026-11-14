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
- Where it lives (2026-11-14, owner gave full permission to proceed; my recommendation taken): this repo. Generator
  `tools/gen_lotrweapons.py`, checks `tools/tests/lotrweapons/check.py`, both run by `tools/build.py`, which also
  writes `releases/lotrweapons_<version>-test.zip`; `tools/game/restart.ps1` installs both mods.

## Progress
- Phase 1 done (2026-11-14): the owner's 32 melee entries, `hand_transform`, the texture tables and the shape writer
  (`weapon_shape`, `copy_own_textures`) moved from gen_testnpcs.py to `tools/weapon_catalog.py` (`MELEE` = all 32).
  gen loads it by path and writes lotrnpcs' weapons through it with domain `lotrnpcs`, folder `orcweapon`. Proof: gen
  output (282 files) byte-identical before and after (`diff -r`). Shields and bows stay in gen until their phase.
  Not built with dotnet here (no SDK in the cloud container); the change touches only gen's Python.
- 0.0.1 built (2026-11-14), not yet in game. Content-only mod, vanilla classes, 29 weapons x iron/steel = 58 items.
  - Sources read: vanilla 1.22.7 assets from the public mirror github.com/caffeineLover/Vintage-Story-Reference
    (versions/1.22.7/Assets), Combat Overhaul from github.com/maltiez2/CombatOverhaul (the server runs a fork,
    0.18.9; same layout). mods.vintagestory.at is blocked from the cloud container; dot.net too (no dotnet here, so
    the C# tests run only in GitHub Actions).
  - Stats (vanilla JSON): swords = falx (iron 5 / tier 4 / 850, steel 5.25 / 5 / 2125, reach 2.5, Sharpenable);
    knives (Orc dagger, Orc knife, Dwarven dagger) = knife-generic (3.5/900, 4/2250, ItemKnife); one-handed axes =
    felling axe (3/4/900, 4/5/1800, ItemAxe, chops wood); spears = spear-generic (4.2/400, 4.4/700, reach 3.5,
    thrown 6.8 tier 1 / 7.0 tier 2, ItemSpear with our own thrown entities). Vanilla has no iron or steel mace, war
    hammer, battle axe, poleaxe, halberd or great sword (only "ruined", 4.5 for every type), so those ("heavy": both
    hammers, poleaxe, battleaxe, halberd, warhammer, elven longsword, captain's sword) copy the falx numbers.
    Owner may want two-handed weapons to hit harder: ask after the in-game test.
  - Hand: tpHandTransform is the NPC item's (checked equal by the test). Vanilla 1.22 tools have no fpHandTransform;
    first person uses the same one.
  - Animations: swing = the NPC's attack animation (heldTpHitAnimation), hold = the NPC's hold (heldTpIdleAnimation
    and heldRightReadyAnimation). The vanilla ones (falx, axehit, hammerhit, knifestab, spearhit, spearidle) are on
    the player already. poleaxechop, poleaxehold, greatswordhold are added by patches/player-animations.json to the
    game's seraph-faceless and LRC's seraph/dwarf/elf-faceless shapes (LRC ones dependsOn lrc), with player.json
    settings copied from axehit (chop: blow at frame 14, speed 1) and holdbothhands (holds), plus "-fp" entries.
  - Icons and ground: rotation = vanilla falx's icon rotation applied after the turn that takes our weapon's in-hand
    lie to the falx's; origin = the model's centre; scale gives every weapon the falx's on-screen length. Offline
    sheet previews/lotrweapons-icons-0.0.1.png (renderer centred on the origin, which is how vanilla's falx icon
    reads). Tune in game with .tfedit.
  - Combat Overhaul: CO replaces vanilla weapons with its own definitions (game/itemtypes/combatoverhaul/sword.json:
    OneHandedStanceByType with Attack/DamageTypes per metal, its own animations "combatoverhaul:sword-falx-slash-1"
    etc., CollidersLib colliders). So under CO the swing is CO's animation, not the NPC's: "same animations" holds only
    without CO. Next build: the CO patch, copying CO's falx/spear/club/axe stances per kind (CO metal sets tier only:
    falx iron tier 5, steel 7, damage 4).
