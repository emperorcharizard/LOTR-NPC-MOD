# Prompt: lotrnpcs 0.0.45, Mordor orcs hold weapons, and every NPC attacks a little more often

You are working on **lotrnpcs**, a Vintage Story 1.22.7 code mod that adds Lord of the Rings faction NPCs (recruits, warriors and captains of 17 factions) to a LOTR-themed server. The source lives in `/mnt/project-files/lotrnpcs/` (`src/*.cs`, `tools/gen_testnpcs.py`, `tools/bundle_lrc.py`, `tools/tests/`). The last release is 0.0.44.

The owner uploaded five test weapon models (VS Model Creator shape JSON): `Orc_Hammer.json`, `orc_poleaxe.json`, `orc_dagger.json`, `orc_axe.json` and `orc_scimitar.json`, "to be used by the mordor orcs only". Their request, in their words: *"implement the idea of them holding weapons completely with attack animations, the weapons are randomized, and do not affect damage tier. generally, increase how often they attack by .25 s."* Ship it as **0.0.45**.

## Ground rules

1. **Don't break existing worlds.** Keep entity codes `test-*`, the save keys `lotrnpcs:hirelings`, `lotrnpcs:playerfactions` and `lotrnpcs:spawnzones`, packet ids 46301 to 46305, the channel `lotrnpcs-orders`, the hotkey `lotrnpcs-hirelingorders`, every watched attribute already in use, and the config files (config version stays 7 unless the format changes).
2. **Keep every decision the owner already made.** Rank health, melee damage and damage tiers, regen, outfits, portraits, hiring, orders, stances, spawning, faction relations.
3. **Don't decide what's still open.** The hire coin default, the non-orc factions' stats and the Free Peoples alliance question stay as they are.
4. Build against the reference DLLs in `tools/refs` (no game install). Both test suites must pass, and every new behavior gets a test.
5. Player-facing text is plain English in full sentences, with no em-dashes.

## Part 1: Weapons for Mordor orcs only

- **Who.** Only the three Mordor orc types (`test-mordororc-recruit`, `-warrior`, `-captain`). Angmar, Misty Mountains and Isengard orcs, and every other faction, stay unarmed.
- **Randomized.** Each Mordor orc picks one of the five weapons at random the first time it loads and keeps it for life, through saves, reloads and being hired. Orcs already in a world get one the next time they load. If its stored weapon is no longer in the list (the list was edited), it picks again.
- **Cosmetic only.** The weapon must not change damage, damage tier, damage type, reach, timing or anything else in combat, under vanilla or Combat Overhaul. It is never dropped, can't be taken, and doesn't appear in the creative inventory or the handbook. The safest way: the weapon is shown in the hand on the client only, from a watched attribute naming the item, so no server-side system (Combat Overhaul included) can ever see a held item on the NPC.
- **Holding.** Each weapon sits in the right hand by its grip, blade or head the right way round, at a size that matches vanilla weapons (vanilla's falx is about 1.2 blocks long in hand). Work out the hand transforms properly instead of guessing: the models lie along +X like vanilla tools, the orc body is Player Model Lib's seraph skeleton with a `RightHand` attachment point, and the item is drawn with `tpHandTransform` (default origin is 0.5, 0.5, 0.5). Check the result against how vanilla holds its own falx.
- **Attack animations.** Each weapon swings with a fitting vanilla seraph animation instead of the plain punch: a curved slash for the scimitar, a chop for the axe, an overhead blow for the hammer, a stab for the dagger and a thrust for the poleaxe. The blow should land when the damage does (400 ms into the attack), and the swing should not freeze the legs while the orc is running. Unarmed NPCs keep the current punch.
- **The poleaxe.** It is far longer than the others (about 2.3 blocks in hand), so between attacks the orc holds it upright in both hands like a guard, and goes back to that after each thrust.
- **Textures.** Most texture paths in the uploaded models point at files that don't exist in vanilla or in the installed faction mods (`game:mord/rustiron`, `game:uruk/Iron`, `game:rotwarriors/tarnished/rust`, `game:planks/walnut1`, `game:leather/brown`, `game:cloth/black`, `game:plain`, `game:Metal/iron`, ...). Map each one to a real vanilla texture with the same look, using paths that other mods in the server modpack demonstrably use. Keep the owner's original files untouched, and keep the mapping in one table so it's easy to change.
- **Config.** The weapon list lives in the entity files, written by `gen_testnpcs.py`, so it's easy to extend to other factions later.

## Part 2: Attack a little more often

For every NPC, shorten the time between attacks by 0.25 seconds: the melee cooldown goes from 2200 to 2600 ms to 1950 to 2350 ms. One swing used to take about 3.1 to 3.5 seconds and now takes about 2.85 to 3.25 seconds. Damage per hit doesn't change.

## Part 3: Verify and deliver

1. Preview each weapon in hand and through its swing offline (no game install), and compare with vanilla's falx.
2. Add tests: the weapon list is only on Mordor orcs, every listed item exists, every attack and hold animation code exists in the entity's animations and in the body shape, the cooldown change, the client-only display, random pick and re-pick.
3. Build, run both test suites, package `lotrnpcs_0.0.45-test.zip`, compare its file list with 0.0.44, replace the old zip in `dist/`, and sync the sources.
4. Update the design doc and the project notes.
5. Report briefly what was built, the interpretations above (client-only display, poleaxe guard pose, texture swaps), and that it hasn't been tested in game yet. Ask the owner to check the grips and swings in game, since they can only be previewed offline.
