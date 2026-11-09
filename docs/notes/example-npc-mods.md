---
name: example-npc-mods
description: The example mod zips the user uploaded (incl. Coinage, PML, Combat Overhaul, LRC), where they live, the ingestion report link and key takeaways
metadata:
  type: reference
  modified: 2026-10-30T18:01:51.480Z
---

Ingestion report (Claude Doc, 2026-10-30): https://claude.ai/artifact/9sck2wfzbPAMgdUa4VRy8u — "Example NPC mods: ingestion report".

Where the zips are: 24 in /mnt/project-files/ (Hirelings, NationGuard, NationWarband, duskridge_settlers_core, lotrchar, petai, skeletons, spawnersapi, spookynights, tasshunting, teapotsgiantboar x8, teapotsgreatbighorn x3); 16 more were chat uploads copied to /mnt/project-files/uploads/hearth/ (named by id, no extension): EidolonUnchained, aletraders x2, animaltrader x2, animationslib, AttributeRenderingLibrary, autoconfiglib x2, DST_NPCAdditions, FA_Races, fantasycreaturesreborn, teapotsundeadlegion, genelib x2, HoR-PerformanceOptimizer, coinage 2.2.0/2.2.2/2.3.0/2.4.0, combatoverhaulfork 0.18.1/0.18.8/0.18.9, playermodellib 1.23.8/1.24.0, PlayerLists 2.3.7, LRC 0.4.1 (all added 2026-10-30).

Key takeaways:
- No archive has C# source. On 2026-10-30 the user approved a decompiler: install with `apt-get install dotnet-sdk-10.0` (dot.net installer host is blocked by the proxy) then `dotnet tool install -g ilspycmd`; decompile with `ilspycmd -p -o <out> <dll>`. Decompiled output lives only in the session, not project files.
- NationGuard/NationWarband brains live in their EntityDressedHumanoid subclasses (no taskai); Hirelings hooks conversable OnControllerCreated/CanConverse for dialogue commands.
- Best reference designs: Hirelings (custom tasks on AiTaskBaseR + dialogue triggers as command UI), Ale/Animal Trader (pure-JSON EntityTrader in game domain), DST (JSON faction warfare via entityCodes + *ByType), Duskridge (EntityPlayerBot + ~19 custom tasks + ~35 Harmony patches).
- Coinage (player-minted coins, coinage:coin-{metal}-{size}) has no currency attribute, so vanilla traders won't accept its coins without a patch adding attributes.currency.value.
- Player Model Lib on NPCs (report section 9): race body swap via PlayerModelLib:ExtraSkinnable likely works on non-players but model is chosen only by WatchedAttributes "skinModel" (needs code); WearablesTesselator throws for non-EntityPlayer, so race-fitted gear is player-only. Untested in game.
- LRC 0.4.1 (LOTR Races and Classes, report section 10): the user said to use it PRIMARILY for how the NPCs look (2026-10-30). 17 PML models, codes lrc:<key>: Men freepeople/wildmen/easterling/haradrim/bnumenorean; Dwarves longbeard/firebeardsandbroadbeams/wickeddwarf; Hobbits harfoot/fallohide/stoor; Orcs orc (0.77 scale, goblin ears) and uruk; elves avari/noldor/sindar/tormentedelf. No goblin model. Shares 56 textures with FA Races.
- PML model codes are domain-prefixed (lrc:orc, faraces:dwarf); NPC skinModel needs the full code.
- 2026-10-30 later: the user uploaded the whole server modpack (418 zips); see [[server-modpack]] for its report and findings (armor resistances, faction armor, skipped patches).
- The user wants uploaded materials treated as authoritative, conflicts flagged not silently resolved, and no file modifications during ingestion.

Related: [[vs-base-code-reference]]
