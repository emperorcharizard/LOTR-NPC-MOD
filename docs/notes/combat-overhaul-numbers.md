---
name: combat-overhaul-numbers
description: Combat Overhaul (server combat mod) live armor table, faction armor resists, drifter stats and the orc rank damage/tier numbers, verified in game
metadata:
  type: reference
  modified: 2026-10-31T00:30:54.707Z
---

Source: decompiled combatoverhaulfork 0.18.9 / overhaulliblegacycompat 1.2.15 in the scratchpad co/ folder (zips in /mnt/project-files/). Verified against the user's 0.0.14 in-game damage log on 2026-10-31.

- Player hit: zone picked, then flat reduction, then damage *= table[resist-1][attackTier-1] for Blunt/Slashing/Piercing. Resists of all worn armor on that zone are SUMMED. The LIVE table is overhaulliblegacycompat assets combatoverhaul/config/armor-config.json (24 rows x 12 tier cols), NOT the 9-col default in DamageResistData.cs. Rows used: S6 [.10 .12 .15 .20 .25 .50 1 ...]; S12 [.01 .01 .02 .04 .06 .08 .16 .30 .60 .80 .90 .99]; S13 [.01 .01 .01 .02 .04 .06 .12 .25 .50 .75 .85 .95]; S14 [.01 x4 .02 .04 .08 .12 .25 .50 .70 .85]. Any attack tier >= 7 goes fully through S6.
- Non-player hit (entity has CombatOverhaul:EntityDamageModel): x0.5 if attack tier < resist, else x1.
- CO weapons: same damage in every metal; metal only sets tier.
- CO drifters: health 8/16/24/32/40/48; melee dmg/tier 6/1, 8/2, 10/3, 12/4, 14/6, 16/7.
- LOTR faction armor (gond, mord, angmar, same in all): body tierthree (bronze) P12 S12 B10, tierfour (iron) P14 S13 B12, tierfive (steel) P15 S14 B13, gambeson P2 S2 B6. Their CO.json patches DO apply: CO fork's JsonPatchDependencyAliasPatch maps dependsOn "combatoverhaul" to combatoverhaulfork (also armory->armoryfork, crossbows, firearms, quiversandsheaths, toolanimations). Confirmed: Angmar Raider mail (tierthreechain) = S12 in game. CO vanilla iron plate only S6.
- 0.0.14 in game (8/t4, 9/t5, 10/t6 slashing, chest-only): through vanilla plate 1.6/2.25; through Angmar bronze-tier mail 0.32 (recruit), 0.8 (captain). Faction armor made orcs near harmless.
- CO vanilla body armor is LAYERED (user point 2026-10-31): Outer plate + Middle chain + Skin gambeson, resists summed. Slashing: plate copper 4/bronze 5/iron 6/steel 7, chain 2/3/4/5, tailored linen gambeson 2, polar bear hide (Outer) 3. Full iron stack S12, steel S14, bronze S10: on par with faction armor (one piece fills all three layers). Only partial sets get shredded. CONFIRMED in game with 0.0.14 (user log 2026-10-31): steel plate+chain+tailored gambeson S14 -> 0.08 (8/t4), 0.4 (10/t6); meteoric iron plate+chain+basic gambeson S11 -> 1.0 (10/t6); plate+chain S10 -> 1.5; bear hide+copper chain+leather jerkin S5 -> full 10. Basic gambeson = S1.
- Target: user chose balance vs FACTION IRON armor (S13), then iterated in game (0.0.15 4/t8 4/t9 6/t9; 0.0.16 5/t8 5/t9 7/t9). t8-9 out-pierces CO nightmare drifters (t7).
- 0.0.17 (6/t8, 6/t9, 7/t10) CONFIRMED in game: every hit matched the table (Angmar Soldier breastplate S13, Wight Guard S14, Raider S12). User: too high 'when surrounded' (orcs semi-rare but come in groups).
- 0.0.18 (4/t8, 5/t9, 7/t10, cooldown 2200-2600 ms) and 0.0.19 (3/t8, 4/t9, 7/t9, same cooldown) both matched the table in game (Raider S12: 0.0.19 gives 0.9/2.4/4.2). User died to a test group in both. 0.0.19 per hit: steel 0.36/1/1.75, faction iron 0.75/2/3.5, unarmored 3/4/7.
- Movement/awareness tuning: see [[npc-ai-tuning]].
- Chest-only (0.0.14, confirmed in game): AI task lotrnpcs:meleeattack sends player hits as CO DirectionalTypedDamageSource (via reflection) with Collider "UpperTorso" -> Torso.
- Damage log shows input damage unscaled (GlobalConstants.CreatureDamageModifier 1 in test world; server world config may differ).

Related: [[npc-mod-maker-goals]], [[lotrnpcs-build]], [[server-modpack]]
