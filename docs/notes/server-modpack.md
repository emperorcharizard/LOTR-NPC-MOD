---
name: server-modpack
description: The user's full server modpack (418 zips, 217 mods) ingestion report link and the findings that matter for the LOTR NPC mod
metadata:
  type: reference
  modified: 2026-10-30T23:40:16.732Z
---

Report (Claude Doc, 2026-10-30): https://claude.ai/artifact/GgCjwNHX9rgKdK4ZCgcQ7S "Server modpack: ingestion report". 391 zips in /mnt/project-files/ + 27 chat uploads; 217 distinct mod IDs; 30 byte-identical duplicates. 2026-10-31/11-02 re-uploads of norc, roha, wlel, dale, dwarf, gond, mord are identical to the project copies. Assumed newest version of each is what the server runs (not confirmed by user).

Key findings for the NPC mod:
- "To Rule Them All" mods (same authors as LRC: Dangodangodango, Shakgriig) = per-faction armor with medium/heavy/heavier (vanilla tier 3/4/5) sets: gond (Gondor), roha (Rohan), mord (Mordor orcs + Black Numenorean), iseng (Uruk-hai + Dunlending), dwarf (+ wicked dwarf), norc (Moria/Gundabad orcs, fits Goblins), lotrangmar (domain angmar), dale, wlel, lotrchar (Nazgul), arnor 0.1.1 (chat upload 2026-11-02, not in the modpack; infantry/soldier/lord, same CO values as dwarf). No Easterling or hobbit armor. Shapes are seraph-fitted (fit on LRC dwarf/orc/harfoot untested).
- Combat Overhaul worn-armor resists apply to EntityPlayer only (PlayerDamageModelBehavior). NPCs get fixed resists from CombatOverhaul:EntityDamageModel DefaultResists in entity JSON, so NPC armor is cosmetic and rank toughness must be set in JSON.
- overhaulliblegacycompat ArmorAutoPatcher gives any armor without CO behavior generated resists from vanilla protectionModifiers.
- CORRECTION 2026-10-31: CO fork aliases dependsOn combatoverhaul/armory/crossbows/firearms/quiversandsheaths/toolanimations to its forks, so those patches DO apply (faction armor CO.json confirmed in game, see [[combat-overhaul-numbers]]). Still likely skipped (old IDs, no alias): fantasycreatures->fantasycreaturesupdate (CO's own 84 refs), aldiclasses->faclasses (LRC), xskills->xskillsfork, xlib, stonequarry, maltiezcrossbows, visorhelmets. darkvision and teapotsundeadlegion not installed. User has not decided whether to fix; no change made.
- Dead 2.8.0 mod = working example of a non-player entity with PlayerModelLib:ExtraSkinnable (copies skinModel/skinConfig) and worn gear drawn via vanilla addGearToShape (EntityBehaviorSeraphInventory subclass).
- No required dependency missing; "nationsettlers" still absent.

Related: [[example-npc-mods]], [[npc-mod-maker-goals]], [[vs-base-code-reference]]
