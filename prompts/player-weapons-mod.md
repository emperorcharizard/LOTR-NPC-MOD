# Prompt: LOTR Weapons, the NPCs' custom weapons as player items (a second mod), then anvil smithing

You are working in the **lotrnpcs** repo (Vintage Story 1.22.7 code mod, LOTR faction NPCs, currently 0.0.139). Read
`CLAUDE.md`, `docs/notes/resume.md` and `docs/notes/lotrnpcs-weapons.md` before anything else, and follow the owner's
rules in `CLAUDE.md` (no Escape key in game, ask before a GitHub release, debug features behind `DebugCommands`).

The owner's request, in their words: *"I want to take all the weapons so far that are custom made then make them into
items for players as a different mod, same rigs and animations. And to eventually begin creating recipes and forge
items on anvil."*

So: a **new, separate mod** whose items are the owner's custom weapon models that the NPCs already carry, usable by
players, held exactly as the NPCs hold them (same shapes, textures, hand transforms) and swung with the same body
animations. Later: smithing recipes so players forge them on the anvil. The NPC mod keeps working as it does now.

Work in phases. Each phase ends in a build, passing tests, a commit, and a short report. Don't start smithing (phase 7)
until the owner has held and swung the weapons in game.

---

## Ground rules

1. **Don't break the NPC mod or existing worlds.** lotrnpcs keeps its item codes (`lotrnpcs:orcweapon-*`), its
   client-only cosmetic weapons (`src/Weapons.cs`, the server never sees an NPC's held item), its animation codes
   (`lotrnpcs-<name>-attack`, `-hold`) and its build output. Prove this: the generated `build/gen` tree of lotrnpcs is
   byte-identical before and after your refactor (diff the two trees, not just the zip size).
2. **One source of truth for the weapons.** The weapon data (model file, texture, grip, rotation, scale, attack anim,
   impact frame, hold anim, two-handed) lives today in `tools/gen_testnpcs.py` (`WEAPONS`, `MM_WEAPONS`, `ELF_WEAPONS`,
   `DWARF_WEAPONS`, `NEW_WEAPONS`, `HARADRIM_WEAPONS`, `GONDOR_WEAPONS`, `SHIELDS`, `BOW`, `MM_BOW`, ...). Both mods
   read it from one place. Never copy the tables: a grip fix made for the NPCs must reach the player items too.
3. **The owner's files stay untouched.** `tools/weapons/*.json`, `tools/weapons/source/*.bbmodel`,
   `tools/weapons/atlas/*.png`, `tools/weapons/textures/**`, `tools/anims/*.json` are read, never edited.
4. **The new mod does not depend on lotrnpcs, and lotrnpcs does not depend on it.** Either can be installed alone.
5. **Smallest change that works** (the repo's Ponytail setting). No speculative features, no config for things nobody
   asked to configure.
6. Player-facing text is plain English, full sentences, no em-dashes. Item names are the ones already in the tables
   (`"name"`), e.g. "Gondorian steel sword", "Captain's sword".
7. Decisions that belong to the owner (listed below) are asked with AskUserQuestion, with your recommendation first.
   Everything else, decide, note it, move on.

---

## What exists today (inventory to start from)

Verify this list against the code before using it; the code wins.

**Melee weapons with the owner's own models (32).** Key, in-game name, source, NPC attack anim, hold anim:

| Key | Name | Model | Attack | Hold | Hands |
|---|---|---|---|---|---|
| scimitar | Orc scimitar | morc_scimitar.json + atlas | falx | | 1 |
| axe | Orc axe | Morc Short Axe.json + atlas | axehit | | 1 |
| hammer | Orc hammer | hammer_1.json + atlas | hammerhit | | 1 |
| dagger | Orc dagger | orc_dagger.json + atlas | knifestab | | 1 |
| knife | Orc knife | morc_knife_.json + atlas | knifestab | | 1 |
| poleaxe | Orc poleaxe | poleaxe_1_reference.json + atlas | poleaxechop* | poleaxehold* | 2 |
| mmsword | Orc sword (Misty Mountains) | misty_mountain_orc_sword.json | falx | | 1 |
| mmspear | Orc spear (Misty Mountains) | moria_spear.json | spearhit | spearidle | 1 |
| elfspear | Elven spear | elven_spear_60.bbmodel | spearhit | spearidle | 1 |
| elflongsword | Elven longsword | elven_longsword.bbmodel | poleaxechop* | greatswordhold* | 2 |
| dwaxe | Dwarven axe | dwarven_walking_axe.json (scale x1.2) | axehit | | 1 |
| dwbattleaxe | Dwarven battleaxe | dwarven_battleaxe.json (x1.2) | poleaxechop* | poleaxehold* | 2 |
| dwdagger | Dwarven dagger | dwarven_dagger.json (scaled down) | falx | | 1 |
| dwspear | Dwarven boar spear | dwarven_boar_spear.json (x1.1, untextured stand-in paint) | spearhit | spearidle | 1 |
| dwwarhammer / dwwarhammersteel | Dwarven (steel) warhammer | dwarven_warhammer_{iron,steel}.bbmodel | poleaxechop* | poleaxehold* | 2 |
| dwshorthammer / dwshorthammersteel | Dwarven (steel) shorthammer | dwarven_shorthammer_{iron,steel}.bbmodel | hammerhit | | 1 |
| gdsword / gdsteelsword | Gondorian (steel) sword | gondor_{iron,steel}_sword.bbmodel | falx | | 1 |
| gdcaptsword | Captain's sword | gondor_captain_sword.bbmodel (scale 0.459) | poleaxechop* | greatswordhold* | 2 |
| rhsword | Rohirric sword | rohan_sword.bbmodel | falx | | 1 |
| rhaxe | Rohirric axe | rohan_axe.bbmodel | axehit | | 1 |
| rhspear | Rohirric spear | rohan_spear.bbmodel | spearhit | spearidle | 1 |
| bnsteelsword | Black Numenorean steel sword | black_numenorean_steel_sword.bbmodel | falx | | 1 |
| bnspear | Numenorean spear | numenorean_spear.bbmodel | spearhit | spearidle | 1 |
| morgulblade | Morgul blade | morgul_blade.bbmodel | falx | | 1 |
| eastaxe | Easterling axe (halberd) | easterling_axe.bbmodel | poleaxechop* | poleaxehold* | 2 |
| eastscimitar | Easterling scimitar | easterling_scimitar.bbmodel | falx | | 1 |
| hdspear | Haradrim spear | haradrim_spear.bbmodel | spearhit | spearidle | 1 |
| hdsword | Haradrim sword | haradrim_sword.bbmodel | falx | | 1 |
| hdcurvedsword | Haradrim curved sword | haradrim_curved_sword.bbmodel | falx | | 1 |

`*` = the repo's own animations in `tools/anims` (poleaxechop, poleaxehold, greatswordhold), which `tools/bundle_lrc.py`
injects into the NPC bodies (`seraph-faceless`, `dwarf-faceless`, `elf-faceless`). The others are vanilla seraph
animations. The vanilla stand-ins (`GAME_WEAPONS`, `FALX`, entries with an `"item"` key) are **not** custom and are
out of scope.

**Shields with the owner's models (12):** `SHIELDS` in gen (`shield`, `mmshield`, `kiteshield`, `bnshield`,
`dwtowershield`, `dwshield`, `eastshield`, `gondorshield`, `boromirshield`, `elfshield`, `hdlightshield`,
`hdheavyshield`), each with a solved off-hand transform (`place_grip`, `SHIELD_OUT`, `SHIELD_IN`).

**Bows (2):** the Mordor orc bow (`orc_bow.json`) and the Moria bow (`moria_bow.json`), each with a rest hold, a drawn
hold and three pulled-string stages (`BOW_PULLS`, `pull_string`).

**How the NPCs hold them (the "rig"):** `hand_transform()` / `place_grip()` / `move_grip()` in gen turn a grip point
and rotation into the item's `tpHandTransform` at `WEAPON_SCALE` 0.6 (math in `lotrnpcs-weapons.md`). Models from
`.bbmodel` are converted at build time by `tools/bbmodel_to_vs.py`. Offline previews: `tools/preview/heldpreview.py`,
`vsrender.py`, `zoompreview.py`, `tipdir.py`, `shieldfit.py`, `bowrest.py`.

**The server:** runs **Combat Overhaul fork 0.18.9** (it redefines the game's weapons under the same codes; see
`docs/notes/combat-overhaul-numbers.md`, `vanilla-weapon-codes.md`, `server-modpack.md`), **Player Model Lib**, and
**LOTR Races and Classes** (players can be dwarves, elves, hobbits, orcs: each race has its own body shape). The LOTR
faction armor mods ("To Rule Them All") ship vanilla items plus a Combat Overhaul JSON patch gated by
`dependsOn: combatoverhaul`, which the CO fork aliases to itself. That is the pattern to copy if the owner picks it.

---

## The owner's decisions (2026-11-14), settled: don't ask again

1. **Mod id and name:** `lotrweapons`, "LOTR Weapons". A content-only (JSON) mod if vanilla classes are enough
   (prefer that: less to maintain); a code mod only if something needs it.
2. **Combat system:** vanilla item classes, plus a JSON patch with `dependsOn` Combat Overhaul that turns them into CO
   weapons when CO is installed (the faction armor mods' pattern). Owner: "so it can still be used without the mod".
   The mod must load and work with no CO installed.
3. **Stats:** each weapon copies its nearest game weapon, in the same metal (scimitar, swords, Morgul blade = falx;
   one-handed axes = bearded/battle axe; hammers = club/warhammer; daggers and knives = knife; spears = spear;
   two-handed axes, poleaxe, halberd, warhammer, great swords = the game's/CO's two-handed equivalents). Write the
   table of which game item each one copies into the notes. No extras (Morgul blade effects and so on) unless asked.
4. **Tiers: iron and steel only.** Every melee weapon comes in exactly two variants, **iron** and **steel**
   (`variantgroups` `metal: ["iron", "steel"]`), each with the stats of its game equivalent in that metal. No bronze,
   no meteoric iron.
   - **Looks:** "the skin difference can be solved later". For now both variants use the weapon's one model and
     texture, except where the owner already made both: the Gondorian sword (`gdsword` / `gdsteelsword`), the dwarven
     warhammer (`dwwarhammer` / `dwwarhammersteel`) and shorthammer (`dwshorthammer` / `dwshorthammersteel`) each
     become **one** weapon whose iron variant uses the iron model and steel variant the steel model
     (`shapeByType` / `texturesByType`). The Black Numenorean steel sword's model serves both its variants. That makes
     29 melee weapons, 58 items. Keep the per-variant shape and texture in one place so the owner's steel skins drop in
     later with a one-line change each.
   - **Shields and bows: iron only**, one item each (12 shields, 2 bows), no metal variant group.
5. **Order of work:** melee weapons first (owner: "Do melee weapons first"), then shields, then bows.

**Still open** (ask with AskUserQuestion when you reach them, recommendation first; don't ask about anything you can
find in the code, vanilla assets or the notes):

6. **Where the mod lives.** Recommended: this repo, folder `lotrweapons/` (its own modinfo, lang, version, notes),
   built by `tools/build.py --mod weapons` (or a sibling script) into `releases/lotrweapons_<version>-test.zip`,
   sharing the weapon catalog and the converter. A separate repo is possible but splits the source of truth.

---

## Phase 1: one weapon catalog for both mods

- Move the weapon, shield and bow tables and the transform helpers (`hand_transform`, `place_grip`, `move_grip`,
  `pull_string`, `WEAPON_SCALE`, the anim/impact data) out of `gen_testnpcs.py` into one module, e.g.
  `tools/weapons/catalog.py`, loaded by path the way gen loads `bbmodel_to_vs.py` (gen runs under `python -I`).
- Keep the owner's history comments with the entries they explain (they record why each grip and roll is what it is).
- gen imports the catalog; NPC-only data (`WEAPONS_OF`, `WEAPON_PREFS`, `SHIELD_OF`, `held_weapon`, ...) stays in gen.
- Also split the shape/texture writer (`write_weapons`) so it can write a weapon's shape and atlas into either mod's
  asset tree under that mod's domain.
- **Proof:** build before and after, `diff -r` the two `build/gen` trees: no difference. Both offline suites pass.
- Commit as a refactor on its own.

## Phase 2: the new mod's skeleton and build

- `modinfo.json`: content or code mod per decision 1, `side: Universal`, dependencies only on the game (and none on
  lotrnpcs, Player Model Lib or CO). Version starts at 0.0.1, kept in one place like gen's `"version"`.
- Build writes `releases/lotrweapons_<version>-test.zip`; the lotrnpcs zip is unchanged. Add the new build to CI
  (`.github/workflows`) next to the existing one.
- `CREDITS.txt`: the models and textures are the owner's (see `tools/CREDITS.txt`, whose texture origin note says some
  look like other LOTR mods' art). Before any public release, ask the owner whether they may be redistributed in a
  standalone player mod; note the answer.

## Phase 3: the items

For each weapon in scope, write `itemtypes/<code>.json`, the shape (from the catalog, through the same converter, the
same atlas texture), and the lang entry.

- **Codes:** readable and smithing-friendly, one itemtype per weapon with the metal as its last variant:
  `lotrweapons:sword-gondor-{iron,steel}`, `axe-orc-{iron,steel}`, `poleaxe-orc-{iron,steel}`,
  `spear-haradrim-{iron,steel}`; shields and bows without a metal part (`shield-gondor`, `bow-orc`). The catalog key
  maps to the code in one table. Never reuse `orcweapon-*`. Lang: "Gondorian sword (iron)" / "(steel)" or the game's
  own pattern for metal names, whichever vanilla uses for its falx.
- **Third person:** `tpHandTransform` is **exactly** the NPC's transform from the catalog (same grip, rotation,
  scale). The player's seraph has the same `RightHand` attachment point as the NPC body, so the weapon sits the same.
  Check it on the LRC race bodies too (dwarf, elf, hobbit, orc), since players use them.
- **New transforms the NPCs never needed:** `fpHandTransform` (first person; start from vanilla's falx/axe/spear
  first-person values and solve offline for grip-on-hand), `guiTransform` (inventory icon, whole weapon visible, tilted
  like vanilla tools), `groundTransform`, and `toolrackTransform` if the weapon type racks in vanilla. Two-handed
  weapons: check vanilla's two-handed items for the flag that empties or blocks the off hand.
- **Visible:** in the creative inventory (its own tab "lotrweapons"), in the handbook, durability, stack size 1,
  `tool` type where vanilla's equivalent has one (so axes chop and knives harvest only if vanilla's equivalent does;
  ask if unsure), combat stats per decision 3.
- Read vanilla's own JSON before writing any field: `assets/survival/itemtypes/tool/blade.json`, `axe.json`,
  `spear.json`, `club.json`, `knife.json`, `shield.json`, `bow.json` in the owner's game install
  (`%APPDATA%\Vintagestory\assets\survival\...`, read over computer use if the files aren't in this repo), and the CO
  fork's `assets/game/itemtypes/combatoverhaul/*.json` for decision 2. Copy fields from there; don't guess field names.

## Phase 4: the same animations for players

The NPC attack is "vanilla body animation, sped so the blow lands at `DAMAGE_AT_MS`, upper body weighted"; the hold
is an animation played between swings. Players need the same look.

- **Vanilla path:** vanilla melee items name their player animations in attributes (`heldTpHitAnimation`,
  `heldTpIdleAnimation`, `heldTpUseAnimation`, ready/hold variants; confirm the exact names in vanilla JSON). The
  vanilla codes (falx, axehit, hammerhit, knifestab, spearhit, spearidle) already exist on the player's seraph body.
- **The three custom animations** (poleaxechop, poleaxehold, greatswordhold) don't exist on the player's body. Add them
  with JSON patches (`op: addmerge` / `add` to `/animations/-`) on the player body shapes: vanilla
  `game:entity/humanoid/seraph-faceless` (check which shape the player entity really uses in 1.22, and how Player
  Model Lib swaps it), and each LRC race body players use (`dependsOn` LRC so the patch is skipped without it). The
  animation JSON comes from `tools/anims` unchanged. Check for code clashes, like `bundle_lrc.add_animations` does.
- **Timing:** the NPC speeds each animation so its impact frame lands at a fixed ms. For players the blow timing comes
  from the item's attack stats; pick each animation's speed so the visible impact frame matches when damage lands, and
  write that rule into the catalog (one rule, both mods).
- **Combat Overhaul path:** CO drives player weapons with its own first- and third-person animation system, not the
  vanilla attributes. Read the CO fork's weapon JSONs and find out whether a CO weapon can name a body (shape)
  animation, or needs CO's own keyframe animations. If it can't use the same animations, stop and give the owner the
  options (CO animations that look close, vanilla behaviour under CO, or something else), with a recommendation.
- First person: confirm what the player sees of the swing with each item; fix `fpHandTransform` if the weapon clips
  the camera.

## Phase 5: combat

- Stats per decisions 3 and 4, in one table (catalog or the new mod's own data): for each weapon, the game item it
  copies and that item's iron and steel numbers (durability, damage, tier, attack range, speed). Vanilla values in the
  item JSON; CO values in the CO patch (in CO, metal sets only the tier, per `combat-overhaul-numbers.md`).
- Two-handed weapons: whatever vanilla/CO does for its two-handed weapons (off hand blocked, reach, speed). Spears:
  thrown or not, as vanilla's spear does; if thrown, the projectile uses the same shape.
- Test that a player hit with each weapon does what its equivalent does, with and without CO (damage log; CO's
  numbers in `combat-overhaul-numbers.md`).

## Phase 6: shields, then bows (after the owner tries the melee weapons)

- **Shields (iron only):** vanilla shield class and attributes (block chance, damage absorption) copied from the
  nearest iron-tier vanilla shield; `tpOffHandTransform` = the catalog's solved off-hand transform; check it on the
  player's left arm.
- **Bows (iron tier only):** stats copied from the nearest game bow (game bows have no metal; under CO, give them
  the iron tier the CO bows use for that level, and ask the owner if no game bow is a clear match); vanilla bow class; the rest and drawn holds from the catalog; the pulled-string stages as the bow's draw
  shapes if vanilla bows support per-stage shapes (check `bow.json`), else the drawn shape only, and tell the owner.

## Phase 7 (later, after the owner has used the weapons in game): forging on the anvil

Don't start until the owner says so. Then:

- **Read vanilla first:** `assets/survival/recipes/smithing/*.json` (format: `ingredient` with the metal ingot or work
  item, `pattern` as layers of 16 strings of 16 characters, `name`, `output`), and the grid recipes that join a forged
  head or blade with a handle (`recipes/grid/tool/*.json`). Use the same two-step flow: forge the blade or head
  (`lotrweapons:swordblade-gondor-steel`, ...) on the anvil, then assemble in the crafting grid with a stick or leather,
  as vanilla does for its blades, axes and spears. Head and blade items need their own shapes; cut them from the
  owner's model (the model's blade/head elements only) in the converter, and ask the owner to approve them.
- **Patterns:** write a tool that projects the blade or head boxes of each model onto the 16x16 anvil grid and prints a
  draft pattern plus a preview image, so the owner sees what they'll forge. Long weapons and two-handed heads: check
  vanilla's largest patterns and the voxels-per-ingot rule, and scale or split so the cost lands near the equivalent
  vanilla weapon's.
- **Materials:** iron and steel ingots only, matching the weapons' two variants, so one recipe per weapon with
  `ingot-{metal}` limited to iron and steel and the output `...-{metal}` (vanilla's wildcard recipes do this).
  Shields' metal parts and any bow fittings: iron only. Smithing temperature and work rules from vanilla.
- **Handbook:** each weapon's page shows its recipe (vanilla does this when the recipe and output exist).
- **Tests:** every pattern is 16 wide, every layer the same size, ingredient and output codes exist, every weapon in
  scope has exactly one recipe chain, each output resolves.
- Then in game: forge one of each weapon type in survival.

---

## Tests (offline, in `tools/tests/`, run by the build)

Add a suite for the new mod, and keep the NPC suites passing:

- Every item in scope has a shape, its textures exist in the zip, its lang name exists and matches the catalog.
- Every melee weapon has exactly the variants iron and steel, the steel variant's stats are its game equivalent's
  steel stats (and iron's iron), and the three owner-made pairs use their steel models for steel. Shields and bows
  have no metal variants.
- Every animation an item names exists on the player body shapes it can be used with (vanilla seraph, and each LRC
  race when LRC is present).
- Each item's `tpHandTransform` equals the NPC item's (same catalog entry).
- No `lotrnpcs` domain reference in the new mod, no `lotrweapons` reference in lotrnpcs.
- The CO patch (if decision 2a) targets only codes that exist and carries `dependsOn`.
- Later: the smithing checks above.

## In-game test (owner's Windows PC, computer use; scripts in `tools/game/`)

- Install both zips (restart.ps1 installs the newest zip from `releases/`; make sure it picks up the second mod too,
  or extend it). Creative world "Emperorcharizars Superflat Place".
- Give yourself each weapon from the creative tab (or `/giveitem lotrweapons:<code>`), look at it in first and third
  person (F5), swing it at a test target, hold a two-handed one, raise a shield, draw a bow. Screenshots of each
  grip; compare third person with an NPC holding the same weapon side by side.
- Add a "LOTR Weapons" section to `docs/test-checklist.md` with one line per check; tick what passes.
- Don't send Escape. Close menus with R or by clicking.

## Notes and delivery

- New running note `docs/notes/lotrweapons.md` (decisions, codes table, what was tested), listed in
  `docs/notes/MEMORY.md`; a line in `docs/notes/resume.md` saying where it stands and what's next.
- Commit each phase on its own; push per the session's git instructions. Ask before a GitHub release.
- Report at the end of each phase: what was built, which decisions you made yourself and why, what was only checked
  offline, and what the owner should look at in game (grips in first person, the custom swings on each race body).
