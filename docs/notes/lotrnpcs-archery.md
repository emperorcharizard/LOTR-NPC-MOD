---
name: lotrnpcs-archery
description: 0.0.55 Mordor orc archers (rankless unit) and cosmetic Mordor shield: user decisions, code locations, rig transforms, defaults
metadata:
  type: project
---

User decisions (2026-11-03): archers "primarily for mordor, up to the current spec"; "The archers are just an additional unit, they don't have ranks" -> one type test-mordororc-archer, rank "archer", hire price 15 (HirePrices "archer", config v12). Shield: "its also a visual thing, and its a toggle-able accessory. properly rig it, it cant be used in conjunction with the poleaxe." Built 0.0.55, untested in game.

Archers (src/Archery.cs): AiTaskFactionShoot "lotrnpcs:shootattack" (range 20, minRange 4, damage 3 tier 8 piercing, draw 1100 ms, cooldown 2000-2800, arrowSpeed 28, gravityFactor 0.75; ballistic SolveArc with lead; no shot if a friend within 0.75 of the line; retreats from enemies within minRange only if the first 1.2-block step passes CanStepTo, then holds ground 4 s and may shoot down to 1.8). Seek task gets standOff 16 and stops once a clear shot exists. Arrow entity class LotrNpcsArrow (entities/projectile/npcarrow.json), not collectible, skips friends, chest hit under Combat Overhaul (ChestHitDamage). Health 34, fixed bow (no weapon row in looks menu). Formations put archers in the rear spots (Formation.Assign with back flags). Spawning: ArcherShare 0.2 rolled before WarriorShare for each member after the first, captain escorts too.

Shield (Weapons.cs/Looks.cs): config ShieldChance 0.5 rolled once at first spawn (existing NPCs = off), watched attr lotrnpcsShield on/off, client-only DummySlot in LeftHandItemSlot. Poleaxe is "twoHanded": picking it turns the shield off; shield on with poleaxe refused (looks-shieldtwohanded).
Rig (gen_testnpcs.py, LeftHand/HandTpOff): SHIELD 0.0.56 translation (0.2359,0.0682,-0.3688) rot (2,179,-177.9) scale 0.6 = held like a weapon handle, long axis ACROSS the forearm (user: "parallel with the arm instead of perpendicular" was wrong in 0.0.55); solve2.py LeftHand rest@0 -1,0,0 0,-1,0 3.7,0.75,6.0; BOW (our model tools/weapons/orc_bow.json) translation (-0.086,-0.12,0.4777) rot (-165.9,189.9,168.4). Solved offline with scratchpad solve2.py; preview /mnt/project-files/lotrnpcs/previews/shield-bow-0.0.55.png.

Tests: factions 342 pass, spawnzones 55 pass. Zip /mnt/project-files/lotrnpcs/dist/lotrnpcs_0.0.55-test.zip.

Related: [[lotrnpcs-weapons]], [[lotrnpcs-looks]], [[npc-ai-tuning]], [[lotrnpcs-factions]]
