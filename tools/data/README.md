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

## factions.json

The factions, in the order the join menu and the creative tab list them: id, the race part of their NPC type codes,
side (`free`, `shadow` or `none`), stance, balance tier and member races. The build writes
`config/lotrnpcs-factions/lotrnpcs.json`, which the mod reads when the server starts (`src/FactionRegistry.cs`); the
default sides, tiers, zone factions and join menu all come from it. Another mod can add factions with its own
`assets/<its id>/config/lotrnpcs-factions/<name>.json` in the same format; it still needs NPC types for them.

## Adding a faction

A new faction now needs data only, plus a build. Use an existing faction as the pattern.

1. `factions.json`: its id, races, side, stance, tier and member races.
2. `races.json`: its race and model.
3. `armor.json`: its outfits.
4. `tools/gen_testnpcs.py`:
   - its NPC types (`ranks(...)` in `TEST_NPCS`);
   - names (`NAMES_OF`) and weapons (`WEAPONS_OF` or `WEAPON_PREFS`);
   - mounts (`MOUNTS_OF`);
   - talk lines and folk name (`FACTION_TALK`, `FOLK`).

The offline tests in `tools/tests` name the places they find missing.
