# Data files

These are the files to edit for the NPCs' looks and armor. `python tools/build.py` reads them, checks them and writes
the mod's assets. Both allow `//` comments.

## armor.json

What each race wears, by rank.

- `armor`: race, then rank (`recruit`, `warrior`, `captain`, optionally `archer`), then a list of outfits. Each outfit
  is a list of item codes.
  - An NPC gets one complete outfit at random when it spawns. An outfit with an item from a mod that isn't installed is
    skipped.
  - If no outfit is complete, the NPC wears the plain vanilla rank outfit.
  - Archers wear their race's recruit outfits unless the race lists `archer`. Riders wear their warriors' outfits.
  - An item code may end in `@slot`, such as `@face` or `@neck`, to wear it in that gear slot.
  - The looks menu offers every helmet in a unit's outfits. For Men, Elves and Dwarves it also offers the lower ranks'
    helmets.
- `armorWithoutMods`: outfits for every rank of a race, worn only when none of its `armor` outfits is complete.
- `alwaysHelmeted`: units that always spawn with a helmet. Others sometimes spawn bare-headed (`HelmetChance` in the
  server config). Orcs always may.
- `hats`: a chance and a list of hats, by race (the hobbits).

## races.json

Each race's Player Model Lib model and the looks its NPCs may have.

- `model` is a model code. Most are LOTR Races and Classes' own (`lrc:freepeople`, `lrc:longbeard`, ...).
  `lotrnpcs:goblin`, `lotrnpcs:mordororc` and `lotrnpcs:uruk` are ours, built by `tools/lrc_models.py` from LRC's orc
  models with looks LRC doesn't have.
- `parts` limits skin parts to the listed variants: hair colour, hair style, skin, eye colour and so on. Parts not
  listed may be anything the model offers.
  - The NPC gets looks from these lists when it spawns, and the looks menu offers only these.
  - The build stops if a variant isn't in the model.

Race codes are the middle of the NPC type codes, `test-<race>-<rank>`. `elf` is the Wood Elves.

## Adding a faction

A new faction still needs code as well as data. Use an existing faction as the pattern.

1. `tools/gen_testnpcs.py`:
   - its NPC types (`ranks(...)` in `TEST_NPCS`);
   - `FACTION_OF`;
   - `CREATIVE_FACTIONS`;
   - names (`NAMES_OF`) and weapons (`WEAPONS_OF` or `WEAPON_PREFS`);
   - mounts (`MOUNTS_OF`).
2. `races.json`: its race and model.
3. `armor.json`: its outfits.
4. `src/FactionConfig.cs`:
   - `DefaultFactions`;
   - its side in `FreePeoples` or `Shadow`.
5. `src/FactionTiers.cs`: its tier.
6. `src/SpawnZones.cs`: the faction list.
7. `src/Joining.cs`: the join menu's order and section.

The offline tests in `tools/tests` name the places they find missing.
