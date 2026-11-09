# lotrnpcs handoff (2026-11-03)

This note carries the state of the LOTR NPCs mod work over to a new thread. The old thread hit its attachment limit.

## Where things are

- **Latest build:** 0.0.46, at `/mnt/project-files/lotrnpcs/dist/lotrnpcs_0.0.46-test.zip`. Only the latest zip is kept in `dist/`.
- **Sources:** `/mnt/project-files/lotrnpcs/` holds `src/*.cs` and the tools in `tools/`:
  - `gen_testnpcs.py` writes the entities, items, lang and modinfo.
  - `bundle_lrc.py` copies the race models.
  - `tests/factions` and `tests/spawnzones` are the test suites.
  - `weapons/` holds the owner's weapon models and their textures.
  - `preview/` holds the offline renderer.
- **Build, test and package steps:** the project memory file `lotrnpcs-build`. The weapon facts are in `lotrnpcs-weapons`, and the AI timing is in `npc-ai-tuning`.
- **Design doc:** https://claude.ai/artifact/UeA7nQZNTbCBngv7PvF8VR

## What 0.0.45 and 0.0.46 did

- **Weapons:** Mordor orcs (recruit, warrior and captain only) each carry one of the owner's five weapons: scimitar, axe, hammer, dagger or poleaxe. The weapon is picked at random and kept for life.
  - The weapons are cosmetic. Only the client puts them in the hand, so damage, tier and timing are unchanged.
  - Each weapon has its own vanilla swing, timed to land with the damage at 400 ms. The poleaxe is held upright in both hands between thrusts.
- **Attack speed:** every NPC attacks 0.25 s more often, with a cooldown of 1950 to 2350 ms (was 2200 to 2600).
- **Textures (0.0.46):** the weapons use the owner's own textures, bundled as `lotrnpcs:item/orcweapon/...`. The owner's set has no "plain", so the dagger's cord keeps a vanilla string texture.
- **Dagger (0.0.46):** it is rolled over in the hand (rotation x 180), after the owner said it was upside down in game.
- **Logs:** the owner's 0.0.45 logs showed no lotrnpcs errors. All errors came from other mods.

## Open items

1. **Dagger grip:** I read "upside down" as a roll, so the edge now faces up. If the owner meant a reverse grip, with the blade pointing down out of the fist, change the dagger's grip and rotation in the `WEAPONS` table in `gen_testnpcs.py`.
2. **In-game check:** the scimitar, axe and hammer grips, and all five swings, haven't been checked in game yet. Only the poleaxe and dagger were seen in screenshots.
3. **Free Peoples alliance:** should the Free Peoples factions ship allied by default? A decision card asked this in the old thread. It is unanswered.
4. **Hire coin:** the default hire coin `coinage:coin-silver-lg` doesn't exist in the owner's world, which uses planchets. The owner hasn't picked a coin.
5. **Non-orc stats:** the non-orc factions still copy the orc stats.

## Things to keep

- **Save compatibility:** don't change these.
  - Entity codes `test-*`.
  - Save keys `lotrnpcs:hirelings`, `lotrnpcs:playerfactions` and `lotrnpcs:spawnzones`.
  - Packets 46301 to 46305.
  - Channel `lotrnpcs-orders` and hotkey `lotrnpcs-hirelingorders`.
  - Config version 7.
  - Watched attribute `lotrnpcsWeapon`.
- **Shared folder:** `/mnt/project-files` adds a C2PA chunk to PNG files written there. Check copied PNGs by their pixels, not with `cmp`.
