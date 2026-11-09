---
name: npc-mod-maker-goals
description: Design doc link + confirmed design decisions for the user's Vintage Story LOTR NPC mod, plus open questions
metadata:
  type: project
  modified: 2026-11-03T06:20:00.000Z
---

Design doc (Claude Doc): https://claude.ai/artifact/UeA7nQZNTbCBngv7PvF8VR "LOTR NPC mod: v1 design". Read before discussing design; update when decisions change.

Confirmed by the user, 2026-10-30 (summary):
- One content mod for a LOTR Vintage Story server (not a framework or tool). Goals: hirelings at the player's base, commanding them in PvP, wild NPC enemies.
- v1 races: Men, Dwarves, Hobbits, Goblins, Mordor orcs, Uruk-hai. Out: elves, creatures, structures, worldgen, wages, mercenaries.
- Allegiance via admin /group groups + friendly races; per-faction stance; NPC-vs-NPC fights; all fight drifters; mod never touches other spawning.
- Per-faction config friendlyRaces (PML codes) AND friendlyGroups, EITHER counts; a player can be friend of several factions. Allies set in config never fight; players safe ONLY from own factions unless an alliance has ShareFriends (user 2026-11-02).
- Map is pre-generated and hand painted (no worldgen).
- Looks: LRC 0.4.1 race models bundled with permission (see [[lotrnpcs-build]]). Goblins, Mordor orcs and Uruk-hai are THREE separate races (user): goblin=small LRC orc body 0.77, goblin ears/faces, pale skins; Mordor=orc body 0.9, tusked faces, dark grey skins; Uruk=uruk body, darkest skins, long hair. Shire=harfoot only (user). Details: [[lotrnpcs-build]].
- v1 factions (user): Gondor, Rohan, Dwarves, Shire, Mordor Orcs, Goblins, Easterlings, Dunlendings, plus Isengard (Uruk-hai) and Angmar orcs. Blocs: Free peoples (Gondor, Rohan, Dwarves, Shire) vs Shadow (Mordor orcs, Angmar orcs, Goblins, Easterlings, Isengard). Since 0.0.33 blocs are lore only; factions are enemies unless config allies them. EVERY faction aggressive to non-members except the Shire (user 2026-11-02).
- Wild NPCs drop nothing; gear cosmetic per race+rank. Ranks (user): THREE per fighting faction (recruit, warrior, captain), each its own spawn, bundling look, stats, hire price. Hobbits: ONE rank, farmers with farm tools; counts as Shire recruit, so Shire zones work (0.0.36).
- Hiring: one-time Coinage fee, cap 5, only from factions friendly to your group. Commands Follow me / Guard here (radius 16, attacks non-friend players, also while owner offline); friends = owner, owner's group, whitelist (/hirelings whitelist). Guard here and the whitelist built in 0.0.44 ([[lotrnpcs-hiring]]). Loyal if owner switches group; death permanent; slow regen; escorts >~64 blocks teleport to owner.

PML route PROVEN in game 2026-10-30 ([[lotrnpcs-build]]). PML wearables are player-only. Rank health DECIDED (user, 2026-10-30): drifter-style health only, NO rank armor (user's choice; don't re-push). Since 0.0.43 (user 2026-11-03: "reduce the health of all the npcs by 6"): recruit 22 / warrior 34 / captain 48, rankless 19 (was 28/40/54); loaded NPCs sync to the type's maxhealth. Server and test world run Combat Overhaul: see [[combat-overhaul-numbers]].

Orc armor looks (user 2026-10-31): orcs wear 'To Rule Them All' faction armor; Angmar orcs are a SEPARATE faction type (Mordor orc stats/model, angmar armor). Same pattern (0.0.23): Goblins = Misty Mountains orcs in norc armor, Uruk-hai = Isengard in iseng armor. Captains (user 2026-11-02, 0.0.27): Angmar = Wight Guard, Misty Mountains = Gundabad Warlord (norc*heavier); Mordor captain (user 2026-11-02, 0.0.29): red Orc Warrior body/legs + Servant of the Eye Helmet (Red Mask). Angmar AGGRESSIVE and in the SHADOW bloc (user 2026-11-02).

TRTA co.json '/class' errors: authors' bug, harmless.

SPAWNING (user 2026-11-02): admin zones (rectangles, may overlap, several factions) REPLACE camps; every 3-5 min window near a player in a zone, own zone timer (0.0.30); since 0.0.52 (user 2026-11-03) each window only ROLLS GroupChance (default 0.5) for a group, lotrnpcs-spawning.json.

**How to apply:** don't re-ask confirmed points; push on open ones one at a time; build only when asked.

Related: [[example-npc-mods]], [[vs-base-code-reference]]
