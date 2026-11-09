# Server modpack: ingestion report

Oct 30, 2026 · @Emperorcharizar

## 1. Summary

This report covers the whole modpack uploaded on 2026-10-30: 391 zips in project files plus 27 chat uploads (16 from today and 11 earlier ones), 418 archives in all. All 418 opened cleanly and each has a `modinfo.json`. They hold **217 distinct mods**. Thirty archives are byte-identical copies, either the "`<name> 2.zip`" duplicates or chat uploads that match a project file. Most mods come in several versions. I assumed the newest version of each is the one on the server.

This report builds on two earlier ones. The [Example NPC mods: ingestion report](https://claude.ai/artifact/9sck2wfzbPAMgdUa4VRy8u) covers the first 40 example mods, plus Coinage, Player Model Lib, Combat Overhaul and LRC. The [base code reference](https://claude.ai/artifact/WacMjb2Cg1z4t9pCimnSDc) covers the Vintage Story source.

**Headline findings for the NPC mod**

1. **Ready-made faction armor exists.** The *To Rule Them All* series has armor for Gondor, Rohan, Mordor, Isengard, Dunland, Dwarves, the Misty Mountain orcs, Dale, Angmar and the Woodland Realm. It comes from Dangodangodango and Shakgriig, two of LRC's authors. Each faction has medium, heavy and heavier sets, which line up with the design's two or three ranks per race (section 2). Easterlings and the Shire have no set.
2. **Worn armor protects players only.** In Combat Overhaul, armor resistances are applied only to players (`PlayerDamageModelBehavior` requires an `EntityPlayer`). NPCs get fixed resistances from `CombatOverhaul:EntityDamageModel` in their entity JSON. So an NPC's armor is cosmetic, and its protection has to be written per rank in its entity file (section 3). Confirmed from decompiled code.
3. **A working example of a non-player entity with a player look.** The *Dead* mod's corpse entity uses `PlayerModelLib:ExtraSkinnable`. It copies `skinModel` and `skinConfig` onto the corpse and draws worn armor with the game's `addGearToShape`. This is the closest existing pattern for drawing LRC bodies plus faction armor on NPCs (section 3).
4. **Some compatibility patches never run.** Several mods are installed as forks with new mod IDs. Combat Overhaul's engine maps the old IDs `combatoverhaul`, `armory`, `crossbows`, `firearms`, `quiversandsheaths` and `toolanimations` to their forks, so those patches do run. That includes the *To Rule Them All* armor values. *Correction 2026-10-31: the first version of this report said they were skipped.* Patches for other renamed mods (`fantasycreatures`, `aldiclasses`, `xskills` and others) are still skipped (section 4).
5. **No required dependency is missing.** Every mod's required dependencies are in the pack at a new-enough version. The missing mods named in the earlier report (overhaullib, overhaulliblegacycompat, jaunt, jsonpatcheslib, configlib) are now present. Only "nationsettlers" is still not found.

## 2. Faction armor (To Rule Them All)

There are ten content mods, each in its own domain with no DLL and no dependencies: `gond`, `roha`, `mord`, `iseng`, `dwarf`, `norc`, `lotrangmar` (domain `angmar`), `dale`, `wlel` and `lotrchar`. Each one adds:

- body, head and leg armor as ordinary wearable items (`<domain>:armor-{body|head|legs}-<tier>-<construct>-<material>-<extra>`), limited by `allowedVariants`
- seraph-fitted shapes under `shapes/entity/humanoid/seraph/<domain>/armor/`
- a faction schematic item that the crafting recipes require and give back
- a Combat Overhaul patch

The tier part of the code sets the vanilla protection values: `tiertwogambeson`, `tierthree*`, `tierfour*` or `tierfive*`.

### Sets by faction (body piece names from the mods' own lang files)

| Faction (v1 design) | Mod | Medium (tier 3) | Heavy (tier 4) | Heavier (tier 5) | Extras |
| --- | --- | --- | --- | --- | --- |
| Gondor | gond 0.5.2 | Man-at-Arms Breastplate (blue, black, green, yellow, White Tree) | Infantry Breastplate (also cloaked) | Fountain Guard Breastplate (also cloaked) | Ranger Tunic (tier 2), Ranger Tunic chainmail (tier 4); helmets with black masks and Citadel and Fountain variants |
| Rohan | roha 0.1.3 | Rohirric Man-at-Arms Mail (green, red) | Eorling Mail (green, red) | Rohirric Huscarl Mail |  |
| Mordor Orcs | mord 0.2.3 | Orc Raider Breastplate (black, brown) | Orc Warrior Breastplate (black, red) | none for orcs | Human sets: Black Númenórean (tier 3), Servant of the Eye (tier 4), Black Númenórean Lord (tier 5, also cloaked) |
| Dunlendings | iseng 0.5.1 | Dunlending Raider Mail | Dunlending Warrior Mail | Dunlending Warlord Breastplate | Same mod has Uruk-hai Scout (tier 3) and Infantry (tier 4, clean or painted), plus scout, infantry, berserker, archer and general helmets |
| Dwarves | dwarf 0.1.3 | Dwarf Militia Mail | Dwarf Warrior Breastplate | Dwarf Lord Breastplate | A matching Wicked Dwarf set (footman, warrior, lord) |
| Goblins | norc 0.1.3 ("Misty Mountain Orcs") | Moria Orc Breastplate | Gundabad Orc Breastplate | Gundabad Warlord Breastplate | Two heavy helmets |
| Easterlings | none |  |  |  | **Gap**: no Easterling armor in the pack |
| Shire | none |  |  |  | No hobbit armor. The Shire is passive in the design, so vanilla clothes may be enough |
| Not in v1 | lotrangmar 0.2.0, dale 0.1.2, wlel 0.1.3 | Angmar Raider; Lake Guard; Woodland Sentinel | Angmar Soldier; Dale Soldier; Wood-elf Warrior | Angmar Wight Guard; Dale Captain; Mirkwood Gate Warden | Angmar Ranger (tier 2) |
| Named characters | lotrchar 0.2.5 |  |  | Nazgûl and Witch-king plate (tier 5) | Nazgûl cloak hood, robe, pants and boots (clothes) |
| Not in v1 (added 2026-11-02) | arnor 0.1.1 | Arnorian Infantry Mail | Arnorian Soldier Breastplate | Arnorian Lord Breastplate | Matching helmets and leggings. Same layout, schematic recipe and Combat Overhaul values as the dwarf set. Not in the server modpack upload |

**What this means for NPCs (my interpretation):**

- Medium, heavy and heavier map directly onto the design's ranks, and the mods' own names already read as ranks: militia, warrior, lord.
- For Mordor, the orc line has only two tiers. A third Mordor rank would use the human Servant of the Eye or Black Númenórean pieces, or the Isengard Uruk-hai sets.
- Isengard's Dunlending sets fit the Dunlendings faction. The Misty Mountain orcs (Moria and Gundabad) fit the Goblins faction. That pairs well with LRC's small `lrc:orc` body.
- Every shape is fitted to the standard seraph body. LRC's fitted-armor replacements (`model-replacements-byshape.json`) cover only vanilla armor and only for players. These sets have not been checked on LRC's dwarf body (factor 0.95, its own shape), the 0.77-scale orc or the 0.7 harfoot. Clipping is possible. **Untested.**
- The NPC mod can reference these shapes directly in NPC outfits (for example `gond:entity/humanoid/seraph/gond/armor/body/gondorarmorheavy/gondorarmorheavy`). It does not need the items unless NPCs should drop them, and the design says wild NPCs drop nothing in v1.

**Versions.** Changes between the uploaded versions are small. Most steps touch about four files (recipes, lang and patches) with no new items. Angmar 0.1.2 to 0.2.0 adds 6 files (the ranger set) and changes 13.

**Packaging quirk.** Nine of the ten modinfo files (all but norc) have two `"version"` keys: first `"1.22.7"` (apparently the intended game version), then the mod version. The parser keeps the last one, so they load correctly, but there is no real `game` dependency.

## 3. Armor resistances, players and NPCs

**Where resistances come from.** Combat Overhaul (`combatoverhaulfork` 0.18.9 on the engine `overhaulliblegacycompat` 1.2.15) gives each armor item a `CombatOverhaul:Armor` behavior. That behavior sets `Resists` for `PiercingAttack`, `SlashingAttack` and `BluntAttack`, the body `Zones` it covers (Head, Neck, Face, Torso, Arms, Hands, Legs, Feet), the layers it occupies and player stat penalties.

The following add these values through JSON patches:

- `combatoverhaulfork` patches vanilla armor.
- Armory, Visored Helmets, Alt. Metal Armor, Not Only Spear and the *To Rule Them All* mods patch their own items.

**The automatic fallback (confirmed from decompiled code).** At startup, `ArmorAutoPatcher` in `overhaulliblegacycompat` finds every item whose `clothescategory` is `armorhead`, `armorbody` or `armorlegs` and that has no Combat Overhaul armor behavior. It gives each one generated values:

- piercing = slashing = floor(2 + 12 × ((relativeProtection − 0.4) / 0.57) × (0.2 + 0.8 × protectionTier / 4))
- blunt is half of that
- every generated piece gets the same −5% stat penalties

The patcher then removes the vanilla `protectionModifiers`. It only touches armor that has no Combat Overhaul behavior. The *To Rule Them All* armor has its own Combat Overhaul patches, and those do apply (section 4), so it keeps the mod's own values. The table compares the two for reference:

| Vanilla tier in the item code | Generated (pierce/slash/blunt) | The mod's own body values (these apply) |
| --- | --- | --- |
| tier two gambeson (0.75, tier 2) | 6 / 6 / 3 | 2 / 2 / 6 |
| tier three (0.88, tier 3) | 10 / 10 / 5 | chain 12 / 12 / 10 |
| tier four (0.90, tier 4) | 12 / 12 / 6 | plate 14 / 13 / 12 |
| tier five (0.94, tier 4) | 13 / 13 / 6 | plate 15 / 14 / 13 |

The generated values would be slightly weaker against blades and much weaker against blunt damage. They matter only for armor whose Combat Overhaul patch is skipped or missing.

**Players only (confirmed from decompiled code).** `PlayerDamageModelBehavior.ApplyArmorResists` reads the `character` inventory of an `EntityPlayer`, and its constructor rejects any other entity. Non-player entities use `CombatOverhaul:EntityDamageModel` instead. It has fixed `DefaultResists` (with `DefaultResistsByType`) and optional `ResistsForColliders`, and those values come only from the entity JSON. Combat Overhaul's own patches use this to give animals natural armor. Bears, for example, get blunt 7 to 8, slash 5 to 6 and pierce 2 to 3.

For the NPC mod this means:

- Armor drawn on an NPC is cosmetic.
- To make a Gondor heavy NPC as tough as a player in Gondor heavy armor, its entity JSON (or a `*ByType` rank variant) needs `DefaultResists` set to roughly those armor values, for example 14 / 13 / 12.
- Hirelings would not get tougher from gear they wear unless we write code for it. The design already says gear is cosmetic and fixed per race and rank, so this matches it.

**A working example of rendering (confirmed from decompiled code).** *Dead* 2.8.0 (SiiMeR) leaves a player corpse entity (`dead:playercorpse`, class `deadplayercorpse`):

- With Player Model Lib installed, a patch switches the corpse to `PlayerModelLib:ExtraSkinnable` and adds `PlayerModelLib:EntityTextureSourceBehavior`.
- On death it copies `skinModel`, `skinConfig`, `voicetype`, `voicepitch`, `entitySize` and `carryRigShape` from the player's watched attributes.
- Its `deadgear` behavior subclasses the game's `EntityBehaviorSeraphInventory`. It draws worn armor through `EntityBehaviorContainer.addGearToShape`, not Player Model Lib's player-only `WearablesTesselator`.

That is the pattern for "LRC body plus faction armor" on an NPC: set the two skin attributes, keep a gear inventory, and draw it with `addGearToShape`. Race-fitted armor replacements would still be missing on non-seraph bodies.

## 4. Problems found

### Patches that check for the wrong mod ID

A JSON patch with `dependsOn` runs only when a mod with that exact ID is loaded. There is one exception. `overhaulliblegacycompat` patches the game's patch loader (`JsonPatchDependencyAliasPatch`) so that `combatoverhaul`, `armory`, `crossbows`, `firearms`, `quiversandsheaths` and `toolanimations` also match their fork IDs. *Correction 2026-10-31: I missed this at first, and the earlier rows for `combatoverhaul` and `armory` were removed. The other thread confirmed in game that the faction armor values apply.* The renamed mods below have no such alias, so patches written for their original IDs are skipped. I searched every patch in the newest version of each mod.

The counts below are references to the ID across a mod's patch files, not the number of patches. To confirm, look at the patch loader's summary in `server-main.log` on the server.

| Patches look for | Installed as | Whose patches are skipped | Effect |
| --- | --- | --- | --- |
| `fantasycreatures` | `fantasycreaturesupdate` | **combatoverhaulfork itself** (84) | Combat Overhaul's own compatibility for Fantasy Creatures (armor, colliders, damage model) does not apply |
| `aldiclasses` | `faclasses` (the mod's domain is `aldiclasses`, its ID is `faclasses`) | lrc (17), em (2) | LRC's alternative class lists for Aldi's Classes are skipped |
| `xskills` | `xskillsfork` | floralzonescosmopolitanregion (15), faclasses (11), teapotsgiantboar (8), floralzoneseastasiaticregion (8), coinage (2) | xSkills compatibility skipped |
| `xlib` | `xlibfork` | tankardsandgoblets (8) | Skipped |
| `stonequarry` | `stonequarryforked` | betterruins (15) | Skipped |
| `maltiezcrossbows` | `crossbowsfork` | skeletons (5) | Skipped |
| `visorhelmets` | `visorhelmetsfork` | playermodellib (1) | Player Model Lib's fitted versions of Visored Helmets skipped |
| `combatoverhaull` (typo) | none | spearexpantion (2) | Never runs |

The fix would be a small patch mod that copies these patches with the corrected IDs, or editing the IDs in the mods. Either way it means changing other people's mods, so it's your call. **No change made.**

### Optional mods that look intended but are absent

- `darkvision`: LRC has 17 patch references for race night vision that need it, so those do not run.
- `teapotsundeadlegion`: it was in the earlier example uploads but is not in this pack. Giant Boar (14) and Great Bighorn (4) have compatibility patches for it.
- Most other absent IDs (about 200) are ordinary optional compatibility for mods the server doesn't run, for example Primitive Survival, Wildcraft and Floral Zones regions. That is normal and harmless.

### Duplicates and naming

- Thirty archives are exact copies:
  - 20 "`<name> 2.zip`" files: lotrchar (2), petai (2), skeletons, spawnersapi, spookynights, tasshunting (2), Giant Boar (8) and Great Bighorn (3).
  - 10 chat uploads identical to project files: Coinage (4), Combat Overhaul (3), Player Model Lib (2) and PlayerLists.
- Zip names that don't match the mod ID:
  - `aqueductsandsluices-1.4.6` is `hardcorewaterforked`.
  - `drbraziers` is `hqzlights`.
  - `wanderers-sketchbook` is `arthursjournal`.
  - `expanded_matter` is `em`.
  - `angmar` is `lotrangmar`.
  - `Alt. Metal Armor.zip` is altarmor 1.3.5.
  - `apewindows-v1.22.0-1..0.zip` is version 1.5.0.
- CarryOn and CarryOnLib come as pre.7, pre.8 and rc.1. rc.1 is the newest, and CarryOn rc.1 requires CarryOnLib rc.1, which is present.

### Game version targets

Most mods declare game 1.22.x or none. Five still declare 1.21: archering, fairplayguardian (1.21.0-rc.1), foxtaming, orekiwoofsbeehives and roamingbees. Vintage Story treats that as a minimum, so it isn't an error, but those five may not have been updated for 1.22.

### Dependency check

Every required dependency of the newest version of each mod is present at a new-enough version. Nothing required is missing.

## 5. Full catalogue (218 mods)

The newest version comes last in each Versions cell. The description column is each mod's own modinfo text, trimmed. "Kind" means the mod ships a DLL (code) or not (content). The categories are my own grouping.

### LOTR content (14)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| Aldi's Classes (`faclasses`) | 2.0.0 | content | Classes for Fourth Age server | Owns domain aldiclasses |
| Fourth Age Races (`faraces`) | 1.3.3 | content | Races for the Fourth Age server | Alternate looks; goblin model |
| LOTR Races and Classes (`lrc`) | 0.4.1 | content | Mod adding races from LOTR, with minimalist classes. | Primary NPC looks (first report, section 10) |
| To Rule Them All: Angmar (`lotrangmar`) | 0.1.0, 0.1.1, 0.1.2, 0.2.0 | content | — | Out of v1 |
| To Rule Them All: Dale and Dorwinion (`dale`) | 0.1.1, 0.1.2 | content | — | Out of v1 |
| To Rule Them All: Dwarves (`dwarf`) | 0.1.2, 0.1.3 | content | — | Dwarf NPC armor |
| To Rule Them All: Gondor (`gond`) | 0.5.0, 0.5.1, 0.5.2 | content | — | Gondor NPC armor |
| To Rule Them All: Isengard (`iseng`) | 0.5.0, 0.5.1 | content | — | Uruk-hai and Dunlending NPC armor |
| To Rule Them All: Middle-Earth Characters (`lotrchar`) | 0.2.4, 0.2.5 | content | — | Nazgûl gear |
| To Rule Them All: Misty Mountain Orcs (`norc`) | 0.1.2, 0.1.3 | content | — | Goblin-faction NPC armor |
| To Rule Them All: Mordor (`mord`) | 0.2.2, 0.2.3 | content | — | Mordor NPC armor |
| To Rule Them All: Rohan (`roha`) | 0.1.2, 0.1.3 | content | — | Rohan NPC armor |
| To Rule Them All: Woodland Realm (`wlel`) | 0.1.2, 0.1.3 | content | — | Out of v1 |
| To Rule Them All: Arnor (arnor) | 0.1.1 (chat upload, 2026-11-02) | content | — | Out of v1 (Arnor armor) |

### NPCs, traders and spawning (12)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| Ale Traders (`aletraders`) | 1.0.0, 1.1.0 | content | Adds the Vintner and Mixologist traders, who sell top-shelf supplies, and buy all your spirits. | Pure-JSON trader pattern |
| Animal Trader (`animaltrader`) | 1.2.1, 1.2.2 | content | Adds the Livestock Trader, who sells baby animals and buys animal products. | Pure-JSON trader pattern |
| Battle Towers (`battletowers`) | 1.1.0 | content | Create towers and underworld towers to fight and conquer | Spawner-block towers (worldgen; pre-generated map) |
| Dead (`dead`) | 2.3.1, 2.7.4, 2.7.5, 2.8.0 | code | Leaves a lootable corpse of the player (with their exact skin and worn gear) at the death spot on respawn. | Proof: PML skin + gear on non-player |
| Duskridge Settlers (`duskridgesettlerscore`) | 0.8.1 | code | Build and manage settlements with working NPCs, jobs, zones, storage, combat, and more. | Reference design |
| Hirelings (`hirelings`) | 0.1.0 | code | Hire guards and workers from traders. Swordsmen, spearmen and archers defend your base with patrol routes,… | Reference design |
| NationGuard (`nationguard`) | 1.7.0 | code | Fixed guards that automatically shoot arrows at the players on their list. | Reference design |
| NationWarband (`nationwarband`) | 1.0.43 | code | Soldiers grouped in squads that obey a commander player. | Reference design |
| SpawnersAPI (`spawnersapi`) | 1.1.6 | code | Add custom entities spawners to the game | Spawner pattern |
| Traders Don't Sell Gems (`tradersdontsellgems`) | 1.0.0 | content | Removes purchasable gem materials from the Treasure Hunter trader, requiring players to find gems in the… |  |
| Traders Expansion (`tradie`) | 2.0.1, 2.0.3 | content | Adds 6 new traders: clockmaker, antiquarian, artist, lepidopteris, sommelier and potter. | Pure-JSON trader pattern |
| Traders Revised 2.0 Patch (`tradersrevised2patch`) | 1.0.1 | content | Adds copies of traders to the sale lists for relocation as well as more modded trades. Aiden patched this… |  |

### Creatures and mounts (25)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| Animal cages (`animalcages`) | 5.0.0, 5.0.1 | code | Mod that adds cages for animals so you can catch, transport and display them. |  |
| Butchering (`butchering`) | 1.13.6, 1.14.3 | code | Carry animals back to your place for proper butchering. |  |
| Catch Livestock (`catchlivestock`) | 0.2.4 | code | Right click to pick up a tamed animal |  |
| Eidolon Unchained (`unchained`) | 1.9.0 | code | Adds a walking Eidolon with multiple devastating attacks. | Enemies sharing the world |
| Elk Jaunt Integration (`elkjaunt`) | 1.4.0 | content | Adds the Jaunt Stamina and Entity Movement System to the elk |  |
| ElkVariants (`elkvariants`) | 3.6.2, 3.7.2 | code | Adds more varieties of elk |  |
| Equus: Medieval Horses (`equusdestrier`) | 1.1.1, 1.1.2 | code | Honse |  |
| Equus: Tack and Equipment (`tackandequipment`) | 1.3.0 | content | Adds Equipment for Monoceros and all Equus mods |  |
| Equus: Wild Horses 2.0 (`equusferus`) | 1.0.4, 1.0.6 | code | Adds tameable wild horses to the game with coat genetics, successor to the original Equus mod |  |
| Fantasy Creatures Mod (`fantasycreaturesupdate`) | 0.8.3 | content | The age of Men is over... | Enemies sharing the world |
| Fox taming (`foxtaming`) | 1.7.6 | content | Tamable foxes |  |
| Genelib (`genelib`) | 3.2.0, 3.2.1 | code | Calculates genetic inheritance for other mods. |  |
| Jaunt: Extended Riding System (`jaunt`) | 3.1.0, 3.3.1 | code | Library providing an extended rideable system with additional control patterns and stamina |  |
| OrekiWoof's Roaming Bees (`roamingbees`) | 2.0.0 | code | Cute immersive roaming bees. Now on vanilla skeps and other mods' hives. |  |
| Pet AI (`petai`) | 5.1.1, 5.1.2 | code | Mod that focuses on implementing various new AI behaviours and tasks centered around taming. | Taming AI reference |
| Shear Lite (`shearlite`) | 1.1.0 | content | Lightweight Sheep Shearing |  |
| ShearLib (`shearlib`) | 1.3.0 | code | Harvest items from living animals |  |
| Skeletons (`skeletons`) | 0.6.2 | content | — | PML-based creature |
| Spooky Nights (`spookynights`) | 2.0.2 | code | A small overhaul mod introducing spectral creatures that haunt the darkness. Experience the chilling… | Enemies sharing the world |
| TassHunting (`tasshunting`) | 0.14.12, 0.14.41 | code | Hunting overhaul and tweaks to make the game feel a bit more realistic and alive. Arrows that stick in… |  |
| Teapot's Giant Boar (`teapotsgiantboar`) | 1.0.6, 1.0.7, 1.0.8, 1.0.9, 1.0.10, 1.0.13, 1.0.14, 1.1.0 | content | Chonky boys that can be your companions and mounts. In wild, they might trigger your bear traumas. |  |
| Teapot's Great Bighorn (`teapotsgreatbighorn`) | 1.0.3, 1.0.4, 1.0.5 | content | Big mounts with bigger horns! Run across the wilderness in style! |  |
| Underwater Horrors (`underwaterhorrors`) | 0.15.1 | code | Adds ocean horror creatures: a stalking Sea Serpent and a Kraken with grabbing tentacles. | Enemies sharing the world |
| Walkers (`walkers`) | 1.1.1 | code | Something's moving out there. A forest stalker that wears an innocent face: the Skinwalker. | Enemies sharing the world |
| Wolf taming (`wolftaming`) | 5.0.1 | code | Mod that focuses on making wolves tameable and usable by the player. |  |

### Combat, weapons and armor (18)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| (Not Only)Spear-Expantion (`spearexpantion`) | 0.4.0, 0.4.1 | content | Adds end game spears, pikes, other weapons as a little bit of alcohol and armor) |  |
| Alternative armor designs (`altarmor`) | 1.3.5, 1.3.8 | content | Craft numerous variants of vanilla armor with different ingame designs |  |
| Archering (`archering`) | 1.0.7 | code | Changes archery mechanics to swaying and varying draw times and weights. |  |
| Ballista (`ballista`) | 0.1.0, 0.3.0 | code | Adds a functional ballista to the game |  |
| CO Damage Effects (`codamageeffects`) | 2.1.4, 2.1.8 | code | Applies configurable status effects (bleed, slow, intoxication, knockdown, dismount, poison, burning) to… | Status effects on CO hits |
| Combat Overhaul (`combatoverhaulfork`) | 0.18.1, 0.18.8, 0.18.9 | code | — | Combat rules NPCs fight under |
| Combat Overhaul : Armory (`armoryfork`) | 1.11.0, 1.11.3, 1.11.4 | content | Weapons, shields and armor | Weapons for NPCs |
| Crossbows (`crossbowsfork`) | 1.10.0, 1.10.1 | code | Medieval crossbows |  |
| Downed (`downed`) | 2.4.0, 2.7.5, 2.7.6, 2.7.7 | code | Replaces the vanilla player downed state with a survivable, invulnerable downed state | Player downed state |
| Grindable Arrows (`grindablearrows`) | 1.0.1, 1.0.4 | code | Sharpen your arrows on a grinding wheel to shoot your enemies with more powerful arrows. |  |
| More Arrows (`morearrowsfork`) | 1.2.3 | content | Add more arrows for Combat Overhaul |  |
| Overhaul lib (`overhaullib`) | 2.0.10 | code | — |  |
| Overhaul lib legacy compat (`overhaulliblegacycompat`) | 1.2.5, 1.2.14, 1.2.15 | code | — | Combat engine; NPC resists |
| Quivers And Sheaths (`quiversfork`) | 0.9.31, 0.9.32 | code | — |  |
| Re-Reforged Tools (`rereforgedtools`) | 1.0.2 | content | A 1.22 revival of ODs's Reforged Tools mod replacing vanilla tool and weapon models |  |
| Toolsmith (`toolsmith`) | 1.2.18, 1.2.19 | code | Upgrade your tools with proper handles and bindings for durability and other buffs! Plus, returning the… |  |
| Vigor (`vigor`) | 122.7.3, 122.7.4, 122.7.5 | code | A streamlined stamina and exhaustion system for Vintage Story. |  |
| Visored Helmets (`visorhelmetsfork`) | 1.1.9 | code | Adds helmets for each metal type that have openable visors |  |

### Player model, clothing and cosmetics (10)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| C&N Jewelry (`canjewelry`) | 0.6.24, 0.7.9, 0.7.11, 0.7.12 | code | Gems and jewelry with buffs. |  |
| C&N Jewelry Adornments (`canjewelryadornments`) | 1.0.1 | code | Wearable jewelry - rings, earrings, tiaras, necklaces, monocles and more - plus display stands, all… |  |
| Dressmakers (`dressmakers`) | 1.8.0 | content | Craft and trade beautiful clothing items. Requires Tailor's Delight 2.0.0 or better, and Expanded Matter… |  |
| Emotes (`emotes`) | 2.9.0, 2.10.0 | code | Adds more custom emotes to the game. |  |
| EyesGlowInTheDark (`eyesglowinthedark`) | 2.0.0 | code | Players' eyes glow in the dark. |  |
| Player Model Library (`playermodellib`) | 1.23.8, 1.24.0 | code | — | Required for LRC bodies |
| Regalia (`regalia`) | 1.8.1, 1.8.2 | content | A collection of noble-inspired and fancy clothing | Lord/noble outfits |
| Regalia: Hanfu (`hanfu`) | 1.1.1, 1.2.0 | content | A collection of traditionally Asian-inspired clothing across dynasties and cultures |  |
| Tailor's Delight (`tailorsdelight`) | 2.2.2 | content | New tools for tailors: awls, needles, spindles & rulers. Craft and decorate with cloth, thread, leather… |  |
| Vanity (`vanity`) | 2.4.8 | code | Make your gear invisible to show off your style!—or just run around naked, I guess? |  |

### Skills, classes and effects (13)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| Alchemy (`alchemy`) | 2.1.17, 2.1.22, 2.1.23, 2.1.24 | code | An alchemy mod that adds a couple of player enhancing potions. |  |
| Alchemy Exclusive Potions (`alchemyexclusivepotions`) | 2.0.1 | code | Prevents multiple Alchemy mod potion effects from being active simultaneously. Only one potion may be… |  |
| Birthdays (`birthdays`) | 0.5.2 | code | Gives your Seraph a birthday! Get a random gift to celebrate! |  |
| BrainFreeze (`brainfreeze`) | 1.3.7, 1.3.8 | code | The frozen world reimagined |  |
| effectshud (`effectshud`) | 0.3.3, 0.3.6 | code | Add different potion effects |  |
| Hydrate Or Diedrate (`hydrateordiedrate`) | 2.5.1, 2.5.6, 2.5.8 | code | Adds a thirst and hot weather mechanic to the game. |  |
| InsanityLib (`insanitylib`) | 2.0.3, 2.1.4, 2.1.5 | code | Library with utility code for modding Vintage Story |  |
| NoFoodHealing (`nofoodhealing`) | 3.1.0 | code | Removes healing properties from food items. |  |
| Novelty! (`novelty`) | 1.2.4 | code | Adds a damage and mining speed buff for eating varied meals. |  |
| SlowTox (`slowtox`) | 5.0.0, 5.1.0 | code | Realistic intoxication, with benefits! |  |
| XLib Fork (`xlibfork`) | 1.0.35, 1.0.39, 1.0.41 | code | Adds a leveling and an effect api to the game. |  |
| xSkills Fork (`xskillsfork`) | 1.0.93, 1.1.6, 1.1.8, 1.1.9, 1.1.10 | code | Adds skills to the game. | Player power vs NPCs |
| xSkills Gilded (`xskillsgilded`) | 1.3.49, 1.3.51 | code | Add a new, fancy GUI for xSkills. |  |

### Food and drink (12)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| A Culinary Artillery (`aculinaryartillery`) | 2.0.0-dev.21, 2.0.0-dev.24, 2.0.0-dev.26, 2.0.0-dev.28 | code | An armory of tools for food-based mods and modpack makers |  |
| Coffee and Tea (`coffeeandtea`) | 0.0.22 | code | Coffee and tea shrubs, harvesting, drying, roasting and hot brewing with coffee drink buffs and clay… |  |
| Eternal Stew (`eternalstew`) | 2.0.0, 2.1.0 | code | Eternal Stew is built around a simple idea: throw anything in a pot, walk away, and come back to hot food. |  |
| Expanded Foods: Chef's Tricks (`efchefstricks`) | 1.0.0-dev.3, 1.0.0-dev.4, 1.0.0-dev.5, 1.0.0-dev.6 | content | Non-cooking things you can do with chef's tools that you may not expect. |  |
| Expanded Foods: Core (`expandedfoods`) | 2.0.0-dev.12, 2.0.0-dev.14, 2.0.0-dev.15, 2.0.0-dev.16, 2.0.0-dev.17 | content | New recipes for existing food types. |  |
| FixPerishRate (`fixperishrate`) | 1.0.1 | code | Fix perish rate |  |
| FoodShelves (`foodshelves`) | 3.0.4, 3.0.5, 3.1.1 | code | Contains various food shelves and food storage, mainly made to be used alongside Expanded Foods. |  |
| Foragers Gamble (`foragersgamble`) | 1.3.0, 1.3.1 | code | Masks food safety and names until after eaten. |  |
| Grapes and wine (`apegrapes`) | 1.4.0 | content | Adds grapes and grape vines/bushes |  |
| Stone Bake Oven (`stonebakeoven`) | 1.3.5, 1.4.0 | code | Creates a usable large stone oven for baking and more. |  |
| Tankards, Goblets and More! (`tankardsandgoblets`) | 1.4.2, 1.4.4 | content | Drink in style! Adds a variety of tankards, goblets and bottles into Vintage Story. |  |
| Useful Licorice (`licoriceuseful`) | 1.2.1 | content | Makes licorice actually useful. |  |

### Plants, farming and world (22)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| AdvancedGeology (`advancedgeology`) | 0.9.8, 2.1.0, 2.1.1 | code | Overhauls ore generation to be highly realistic |  |
| Crop Forecaster (`cropforecaster`) | 2.1.0 | code | Predicts crop viability dynamically directly in the Block Info HUD. |  |
| Featherclay (`featherclay`) | 1.0.0 | content | Turn feathers into fireclay |  |
| Floral Zones: Cosmopolitan Region (`floralzonescosmopolitanregion`) | 1.0.11, 1.0.14 | code | Adds plant life with large, cosmopolitan ranges. |  |
| Floral Zones: East Asiatic Region (`floralzoneseastasiaticregion`) | 1.0.20, 1.0.25 | code | Adds plant life from the East Asiatic region. |  |
| Fog Off (`fogoff`) | 1.0.9 | code | Gets rid of fog in select areas and adds fog with a range of colors |  |
| Hardcore Water : Transport Edition Forked (`hardcorewaterforked`) | 1.4.6 | code | Previously prevented buckets from placing source blocks, but now adds methods of water transportation for… |  |
| Hybrasil Night Skies (`hybrasilskies`) | 1.0.0 | code | Auroras for days |  |
| lakulsenhancedvanillatrees (`lakulsenhancedvanillatrees`) | 1.1.1, 1.1.2 | content | — |  |
| Limestone from Caves (`limestonefromcaves`) | 1.0.2 | content | Changes the drops from Stalactites & Stalacmites from their usual stonetype to limestone. |  |
| Natural Fertilizer (`naturalfertilizer`) | 1.5.1 | code | Adds natural fertilizer created from animal droppings |  |
| NDL FlowerGrowth (`ndlflowergrowth`) | 2.2.0, 2.6.0, 2.6.1 | code | NDL-ExpandedGrowth: Flower Growth Module |  |
| NDL MushroomGrowth (`ndlmushroomgrowth`) | 2.2.0, 2.4.4 | code | NDL-ExpandedGrowth: Mushroom Growth Module |  |
| Not Enough Sand (`notenoughsand`) | 4.1.0 | content | Allows you to pulverize more rocks and turn them into sand, among other things. |  |
| OrekiWoof's Simple Immersive Beehive (`orekiwoofsbeehives`) | 2.0.0 | code | Beehive with 8 slots for honey frames. Boosts your plants. |  |
| Salt and sands (`saltandsands`) | 1.3.0 | code | Adds an array of ocean-enriching content, cultivate shellfish for meat and shells! |  |
| Salt and Sands - Balance Patch (`saltandsandsbalance`) | 1.0.0 | content | Balance adjustments for Salt and Sands mod - nerfs shellfish saturation, pearl drops and shell lime output… |  |
| solariasauce (`solariasauce`) | 1.0.7 | content | Foliage color tint overhaul for a lush fantasy landscape where the grass is always greener. |  |
| Stable Surface (`stablesurface`) | 0.0.3 | code | Makes all regions above sea level stable |  |
| Tree Shaker (`treeshaker`) | 1.1.1 | code | Tired of manually collecting fruit? Work smarter, not harder with Tree Shaker to collect fruit! |  |
| Water Under Farmland (`waterunderfarmland122`) | 1.1.0 | code | Water directly below farmland sets that farmland to 100% or configurable moisture; inspired by Zach2039's… |  |
| Waterfall (`waterfall`) | 1.2.4 | code | Water must be manually spread from naturally generated liquid bodies. |  |

### Worldgen and structures (1)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| BetterRuins (`betterruins`) | 0.6.3, 0.6.4 | content | Adds a lot of structures to the worldgen over and underground. Builders: Bellcross, Dampus, hřbitovník ,… |  |

### Building and decoration (24)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| Archimedes Screw (`thetruearchimedesscrew`) | 0.2.0 | code | Adds a mechanically powered Archimedes screw that can pump water against gravity. |  |
| BigBoards signs (`bigboards`) | 1.0.4, 1.0.5 | content | Adds big boards to write information for players. |  |
| Billposting (`billposting`) | 2.0.0, 2.0.3 | code | Makes it possible to pin parchment to walls, as billposts |  |
| Chiseled Block Retention (`cbr`) | 2.0.1 | content | Makes it so chiseled blocks are always solid |  |
| chiseltools (`chiseltools`) | 1.17.3, 1.17.9 | code | Quentin's Chiseling Tools' |  |
| Darker Ebony (`darkerebony`) | 1.0.0 | content | Darkens Ebony Texture |  |
| Daubweave (`daubweave`) | 1.0.0 | content | Cheaper daub, better dye ratio, and craftable blocks, all in one package! |  |
| DRBraziers (`hqzlights`) | 1.1.3 | content | Metal braziers |  |
| Dumbwaiter (`dumbwaiter`) | 0.3.5, 0.3.6, 0.3.7 | code | Medieval-style dumbwaiter lift. Move between vertically-separated stations via an immersive transition… |  |
| Majestic Maple (`majesticmaple`) | 1.4.0 | content | Less ugly maple wood textures |  |
| Metal Leaf (`metalleaf`) | 1.4.3 | content | Smith metal into leaf to cheaply apply metallic textures to a chiseled block. |  |
| Meteoric Cleanup (`meteoriccleanup`) | 1.0.0 | content | Removes the ugly yellow of meteoric iron |  |
| Millwright (`millwright`) | 1.3.3, 1.3.5 | code | Building better windmills since 1386. |  |
| Opine Upon Pine (`opineuponpine`) | 1.0.10, 1.1.0 | content | Darker, desaturated, brown textures for the Pine blocks. |  |
| Plumb and Cube Fork (`plumbandcubefork`) | 2.0.0 | code | For all your reinforcement desires. |  |
| Portcullis (`portcullis`) | 1223.0.4, 1227.0.3, 1227.0.4 | code | Adds a portcullis, Gates and Drawbrigdes and stuff |  |
| Posts and Beams (`postsandbeams`) | 1.5.1 | code | Adds wood beams and posts, used for supporting unstable rock. |  |
| Rope Bridges Continued (`ropebridgescontinued`) | 1.2.1 | content | Adds rope bridges. Originally by ozBillo, continued for modern Vintage Story. |  |
| Scaffolding (`scaffolding`) | 1.3.1 | code | Adds scaffolding to the game! |  |
| Seamarks -- Beacons & Lighthouses (`seamarks`) | 1.0.0, 1.0.1 | code | Build lighthouses and fire beacons to help navigate the world and its oceans. |  |
| Shipwright: Vessels of Distant Shores (`shipwright`) | 1.4.1, 1.5.1 | code | Adds various sailed and rowed boats |  |
| Unleaded Windows (`apewindows`) | 1.5.0 | code | Craft windows without the need of lead ingots. |  |
| VS Roofing Mod (`vsroofing`) | 1.6.3, 2.0.3, 2.0.6, 2.1.0, 2.1.1 | code | Gridless roof crafting/building system with dynamic slopes and corners. |  |
| woodenfortifications (`woodenfortifications`) | 2.0.11, 2.1.0 | code | Primitive defences for your home sweet home |  |

### Crafting and industry (15)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| BellowsFix (`bellowsfix`) | 1.0.3 | code | Implements the vanilla big bellows. |  |
| Better Nails (`betternails`) | 2.0.3, 3.0.3 | code | Configurable smithing output amounts for metal nails and strips. |  |
| Big Bellows Revival (`bigbellowsrevival`) | 1.1.2 | code | Mechanical power, animation and air delivery for the vanilla large bellows. |  |
| Blacksmithname (`blacksmithname`) | 1.3.0, 1.3.1 | code | Add name to forged items |  |
| Clay Press (`claypress`) | 1.1.0 | content | Automate your clay tile & shingle needs, no hands required (no, really, hands off)! |  |
| Expanded Matter (`em`) | 3.7.0, 3.8.1, 3.8.2 | content | A library that extends the base game with new materials and variants. |  |
| Fill My Bloomery (`fillmybloomery`) | 1.0.3 | code | Adds the ability to fully fill bloomeries in one click by holding control and shift when depositing. A… |  |
| Glassmaking Fork (`glassmakingfork`) | 1.5.8 | code | Core system for melting and processing glass |  |
| Grinding Wheel Rework (`grindingwheelrework`) | 1.1.5, 1.1.11 | code | Grinding Wheel Rework expands the vanilla grinding wheel to support more tools and turns sharpening into a… |  |
| Immersive Fibercraft (`spinningwheel`) | 1.2.9, 1.2.14 | code | Adds immersive crafting mechanics for spinning and weaving. |  |
| IndustrialStory (`industrialstory`) | 0.7.7 | code | — |  |
| Knapster (`knapster`) | 4.0.0, 4.1.1 | code | Easier crafting and gathering, for those with low manual dexterity. |  |
| StackableBloomery (`stackablebloomery`) | 1.0.0 | content | Makes bloomery bases and bloomery chimneys stack to 64. |  |
| Stone Quarry Forked (`stonequarryforked`) | 3.5.3 | code | Create quarries to collect rocks as stone slabs. |  |
| TanningRedux (`tanningredux`) | 1.0.2 | content | Simple mod for refactoring tanning numbers and process |  |

### Storage and inventory (9)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| Carry On (`carryon`) | 2.0.0-pre.7, 2.0.0-pre.8, 2.0.0-rc.1 | code | Adds the capability to carry various things |  |
| CarryOnLib (`carryonlib`) | 1.0.0-pre.7, 1.0.0-pre.8, 1.0.0-rc.1 | code | Library for the CarryOn mod |  |
| Containers Bundle (`containersbundle`) | 1.3.1, 1.3.2, 1.4.0, 1.4.1 | code | Adds 11 new containers to enrich game's container progression and variability |  |
| Envelopes (`envelopes`) | 3.8.4, 3.9.0, 3.9.1 | code | Adds envelopes and parcels to the game, sealable with wax |  |
| More Piles (`morepiles`) | 3.0.1 | code | — |  |
| PurposefulStorage (`purposefulstorage`) | 2.0.1, 2.1.1, 2.2.1 | code | Adds a variety of storage blocks designed for both aesthetic appeal and functional item organization. |  |
| Shelf Obsessed (`shelfobsessed`) | 2.1.5, 2.2.1 | content | When in doubt, put it on the shelf! |  |
| Storage Tweaks (`storagetweaks`) | 1.3.0, 1.3.4, 1.4.2 | code | Adds compact/sort and quick-unload buttons to chests. |  |
| Xandus Inventory Tweaks Fork (`xinvtweaksfork`) | 1.9.1 | code | Fork of Xandu's Inventory Tweaks for version 1.22 with fixed duplicating bugs |  |

### Server, chat, admin and UI (34)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| Back to the Hole (`backtothehole`) | 1.0.1 | content | Where does this drifter go? Into the square hole. :) |  |
| BedSpawn (`bedspawnv2`) | 1.7.0, 1.7.1 | code | Sets your "return point" when you sleep in a bed. |  |
| Coinage (`coinage`) | 2.2.0, 2.2.2, 2.3.0, 2.4.0 | code | Adds customizable coins and coinmaking tools to the game | Hiring currency |
| Commercially (`commercially`) | 0.1.1 | code | To be added |  |
| Crawl&Dive (`crawlanddive`) | 0.2.5 | code | Crawl, Dive and slide! |  |
| Dana Tweaks (`danatweaks`) | 4.0.1, 4.1.0 | code | Various tweaks |  |
| Dars Commands (`darscommands`) | 1.1.6, 1.2.0 | code | Fun Admin Commands. |  |
| Denied Fix (`deniedfix`) | 1.0.1, 1.0.2 | code | Compatibility fix for Denied mod. Adds area denial blocks for survival. |  |
| Entity Nametags (`entitynametag`) | 2.6.0, 2.7.0 | code | Adds a nametag item that can be used to name entities (elks, boats, etc.). Can also be used to take… |  |
| Fair Claims (`fairclaims`) | 0.3.0 | code | — |  |
| FairPlayGuardian (`fairplayguardian`) | 1.2.2, 1.2.4 | code | FairPlayGuardian is a mod that allows server owners to choose what client side mods are allowed |  |
| Farseer (`farseer`) | 1.4.0 | code | Draws silhouettes of the terrain outside of your view distance |  |
| Footprints (`footprints`) | 1.2.3, 1.2.10, 1.2.12, 1.2.13, 1.2.14 | code | Animals and Players leave footprints when moving. |  |
| ForgottenAdmins (`forgottenadmins`) | 1.1.1 | code | (Russian, translated) Admin panel for Vintage Story: players, teleport, spawn, inventory and custom coordinates. |  |
| GlobalChatBlock (`globalchatblock`) | 1.0.0 | code | Blocks global chat. |  |
| HidePlayerInfo (`hideplayerinfo`) | 1.3.1 | code | Hide player pins on map and player nametags unless looked at. |  |
| Improved Handbook Recipes (Fork) (`improvedhandbookrecipesfork`) | 1227.0.5 | code | Makes recipes easier to use from the handbook, by letting you scroll freely among variants, adding… |  |
| NoHoldFruitPress (`noholdfruitpress`) | 0.1.3 | code | With this mod you can sneak+right-click fruitpress instead of keeping right-click pressed until it is… |  |
| PlayerLists (`playerlists`) | 2.3.7 | code | View the player list on Vintage Story servers |  |
| resonator range increase (`resonatorrangeincrease`) | 1.0.1 | code | increase resonator range by 10 times |  |
| RP Voice Chat (`rpvoicechat`) | 2.5.7, 2.6.5, 2.6.6 | code | A mod that adds in game voice proximity chat! Also bunch of in-game communication tools! |  |
| Searchable (`searchable`) | 2.4.0, 2.5.0 | code | Adds a command to search through other players' inventories. |  |
| Stick Step Sounds (`stickstepsounds`) | 1.0.0 | content | Plays stick cracking sound when walking on loose sticks |  |
| svanaxfdc (`svanaxfdc`) | 1.1.0 | code | Provides an easy way to adjust fall damage |  |
| Terra Tag (`terratag`) | 0.4.5, 0.6.0 | code | Label regions on the world map |  |
| TerraTag Rooms (`terratagrooms`) | 0.1.1 | code | It's like TerraTag but for rooms |  |
| The BASICs (`thebasics`) | 5.9.1 | code | Adds an RP proximity chat system, with configurable talking ranges, customizable name colors, automatic… |  |
| Vinconomy (`vinconomy`) | 5.3.0, 6.0.3 | code | An Economy Mod allowing players to establish their own shops and sell goods to other players |  |
| VS Instruments Redux - Expansion A (`vsinstrumentsreduxexpansiona`) | 2.0.1, 2.0.2 | code | Adds additional instruments and heavy audio files to VS Instruments Redux. Requires the Core mod to function. |  |
| VS Instruments Redux - Expansion B (`vsinstrumentsreduxexpansionb`) | 2.0.4, 2.0.6, 2.0.7 | code | Adds additional instruments and heavy audio files to VS Instruments Redux. Requires the Core mod to function. |  |
| VS Screen Alerts (`vsscreenalerts`) | 1.1.2, 1.1.4 | code | Server command to show arbitrary text on players screens (HUD overlay). |  |
| VSInstrumentsRedux (`vsinstrumentsredux`) | 1.2.6, 2.0.2, 2.0.3, 2.1.0, 2.2.1 | code | Bring your world to life! Play ABC and MIDI files on working in-game instruments, and more! |  |
| Wanderer's Sketchbook (`arthursjournal`) | 2.0.4, 2.0.5 | code | Adds a sketchbook in which you can draw and write! |  |
| Yang's Starter Kit (`yangstartkit`) | 1.1.0 | code | Server-defined starting items for initial spawn. Give everyone food or something to get them started. |  |

### Libraries and performance (9)

| Mod (ID) | Versions uploaded | Kind | What it does (its own description) | NPC relevance |
| --- | --- | --- | --- | --- |
| Animations lib (`animationslib`) | 0.0.2 | code | — |  |
| Attribute Rendering Library (`attributerenderinglibrary`) | 3.2.0 | code | Attribute-based variant system for rendering things and much more. |  |
| Auto Config Lib (`autoconfiglib`) | 2.0.10, 2.0.11 | code | Automatically creates a ConfigLib implementation for mods that don't have one |  |
| CommonLib-Forked (`commonlibforked`) | 2.8.1 | code | — |  |
| Config lib (`configlib`) | 1.13.2 | code | A universal place to configure your mods. Makes it possible for content mods to have configs too. |  |
| Dear ImGui (`vsimgui`) | 1.2.7 | code | Brings famous immediate mode GUI to Vintage Story |  |
| HoR Performance Optimizer (`horperfopt`) | 1.0.5 | code | Unified performance optimizer for heavily modded servers. Optimizes crafting grid recipe matching (80-90%… |  |
| JSON Patches lib (`jsonpatcheslib`) | 1.5.3, 1.5.9 | code | Implements more convenient syntax and more functionality for patching JSON assets. |  |
| Lodestone (`lodestone`) | 1.1.1 | code | Optimizations for large modpacks: faster handbook loading and search, lang lookups and crafting grid, with… |  |

## 6. Version changes

110 mods were uploaded in more than one version. I compared each consecutive pair file by file: files added, removed and changed, and changes to dependencies. Most steps are small, usually a rebuilt DLL plus two to eight changed JSON files. The table lists only the steps that change structure or dependencies.

| Mod | Step | What changed |
| --- | --- | --- |
| AdvancedGeology | 0.9.8 → 2.1.0 | Major rewrite: 1,510 files added, 85 removed, 563 changed (mostly textures and worldgen) |
| VSInstrumentsRedux | 1.2.6 → 2.0.2 | Restructured: 1,351 files removed, 287 added. Expansion A 2.0.2 needs core 2.0.2 or newer |
| Expanded Foods | dev.12 → dev.14 → dev.15 | dev.14 raises the A Culinary Artillery minimum to dev.22. dev.15 adds 740 files |
| Vinconomy | 5.3.0 → 6.0.3 | 92 files removed, 38 added. Adds a new dependency on `commercially` 0.1.1, which is present |
| xSkills Fork | 1.0.93 → 1.1.6 → 1.1.8 | 1.1.6 adds 389 files. The XLib Fork minimum rises to 1.0.39, then to 1.0.41 in 1.1.8 |
| C&N Jewelry | 0.6.24 → 0.7.9 | 78 files removed, 27 added |
| Dana Tweaks | 4.0.1 → 4.1.0 | 64 files removed |
| VS Roofing | 1.6.3 → 2.0.3 | 146 files added |
| Combat Overhaul | 0.18.1 → 0.18.8 | Drops the `configlib` and `animationslib` dependencies. Engine minimum rises to 1.2.3 |
| CO Damage Effects | 2.1.4 → 2.1.8 | Moves its dependency from `overhaullib` to `overhaulliblegacycompat` |
| Crossbows | 1.10.0 → 1.10.1 | Drops `animationslib` |
| Alt. Metal Armor | 1.3.5 → 1.3.8 | 73 files added (more armor variants) |
| Armory | 1.11.0 → 1.11.3 | 21 files added |
| Floral Zones Cosmopolitan | 1.0.11 → 1.0.14 | 82 added, 26 removed |
| Grindable Arrows, Grinding Wheel Rework | 1.0.1 → 1.0.4, 1.1.5 → 1.1.11 | About 40 files removed and 40 added, which looks like renamed lang files |
| Storage Tweaks | 1.3.0 → 1.3.4 | Game target loosened from 1.21.0 to `*` |
| Portcullis | 1223.0.4 → 1227.0.3 | Version numbers follow the game build (1.22.3 to 1.22.7). 31 files added |
| Angmar | 0.1.2 → 0.2.0 | 6 files added (ranger set), 13 changed |
| Player Model Lib | 1.23.8 → 1.24.0 | 14 files changed, nothing added or removed (see the first report, section 9) |
| Coinage | 2.2.0 → 2.4.0 | DLL and a few JSON changes (see the first report, section 5) |

One oddity: CarryOn and CarryOnLib version numbers do not sort by plain number. The real order is pre.7, pre.8, rc.1, and rc.1 is the newest.

## 7. Knowledge limitations

- **Depth varies by mod.** For all 418 archives I read:
  - `modinfo.json`
  - the file listing and asset categories
  - every `dependsOn` in patch files
  - a keyword search for resistance and armor terms in JSON and DLL strings

  I read these in depth:
  - the ten *To Rule Them All* mods (items, variants, shapes, recipes, patches and lang)
  - Battle Towers, Walkers, Aldi's Classes and Dead (entities and configs)

  The other mods are described from their own metadata, not from a full read.
- **Decompiled this round:** `overhaulliblegacycompat` 1.2.15 and Dead 2.8.0. Earlier decompiles are listed in the first report. No other DLL in this pack has been decompiled. Claims about other code mods come from their metadata and JSON only.
- **Which version runs on the server is not known.** I assumed the newest of each. If the server runs an older one, its row in section 6 shows what differs.
- **Nothing has been tested in game.** That includes:
  - the skipped patches (confirm with `server-main.log`)
  - the generated armor values
  - faction armor fit on LRC's dwarf, orc and hobbit bodies
  - the Dead rendering pattern on our own NPCs
- **Not resolved:** the "nationsettlers" mod mentioned in the first report is still not in the pack.
- **No files were changed.** Extracted copies and the decompiled sources live only in this session, not in project files.
