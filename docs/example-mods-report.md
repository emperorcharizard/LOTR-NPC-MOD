# Example NPC mods: ingestion report

Oct 30, 2026 · @Emperorcharizar

44 archives covering 25 distinct mods were extracted and read (Coinage added later the same day). Companion doc: [Vintage Story NPC modding: base code reference](https://claude.ai/artifact/WacMjb2Cg1z4t9pCimnSDc).

## 1. Archive overview

Seven of the 24 mods add human NPCs (guards, soldiers, settlers, bandits, traders); the rest are creatures, pets, player races or libraries. Version is the newest archive provided; older archives are compared in section 5. Type is the modinfo `type` (code = ships a DLL).

| Mod (modid) | Archives | Type | Author | What it is |
| --- | --- | --- | --- | --- |
| **Hirelings** (`hirelings`) | 0.1.0 | code | EzTactics | Hire swordsman, spearman, archer and porter NPCs via contracts sold by traders; patrols, stances, gear, downed state |
| **NationGuard** (`nationguard`) | 1.7.0 | code | amarillo | Stationary archer guards that shoot players on a white/black list |
| **NationWarband** (`nationwarband`) | 1.0.43 | code | amarillo | Soldier squads that obey a commander player (follow, form, charge, mount) |
| **Duskridge Settlers** (`duskridgesettlerscore`) | 0.8.1 | code | Duskridge Interactive | Settlement NPCs with jobs (lumberjack, hauler, fisher, crafter, excavator, healer), zones, storage, combat |
| **DST - NPC additions** (`dstnpc`) | 1.1.6 | code | mjr121 | Hostile human factions (bandits, barbarians with bears, knights errant, poachers) and poacher camps |
| **Ale Traders** (`aletraders`) | 1.0.0, 1.1.0 | content | Shazza | Vintner and Mixologist traders with dialogue, trade lists, trader homes |
| **Animal Trader** (`animaltrader`) | 1.2.1, 1.2.2 | content | Shazza | Livestock trader selling baby animals |
| **Fantasy Creatures** (`fantasycreaturesupdate`) | 0.8.3 | content | Kiptrix, Swiftthistle | Goblins, orks, skeletons, treants, slimes, dragons; one talking dragon |
| **Eidolon Unchained** (`unchained`) | 1.9.0 | code | Nezzych | Walking Eidolon boss with an obstacle-breaking AI task |
| **Spooky Nights** (`spookynights`) | 2.0.2 | code | Laerinok | Spectral variants of wolves, bears, drifters, shivers, bowtorns |
| **Teapot's Undead Legion** (`teapotsundeadlegion`) | 1.1.2 | content | EruTheTeapot | Revenants you can bind and command, plus mule and palanquin revenants |
| **Teapot's Giant Boar** (`teapotsgiantboar`) | 8 archives, 1.0.6 to 1.1.0 | content | EruTheTeapot | Tameable, rideable giant boars |
| **Teapot's Great Bighorn** (`teapotsgreatbighorn`) | 1.0.3, 1.0.4, 1.1.0 | content | EruTheTeapot | Tameable, rideable bighorns |
| **Pet AI** (`petai`) | 5.1.1, 5.1.2 | code | G3rste | Taming, commands, follow/stay AI tasks; used by the Teapot mods |
| **Fourth Age Races** (`faraces`) | 1.3.3 | content | Sir\_Real, TheSlug | 10 playable races (elf, dwarf, orc, hobbit...) via Player Model Lib |
| **Skeletons** (`skeletons`) | 0.6.2 | content | Maltiez | 4 playable skeleton models via Player Model Lib |
| **Middle-Earth Characters** (`lotrchar`) | 0.2.4, 0.2.5 | content | Dangodangodango, Shakgriig | Nazgul and Witch-king wearables for players (no NPCs) |
| **Genelib** (`genelib`) | 3.2.0, 3.2.1 | code | sekelsta | Genetics and breeding library |
| **Animations lib** (`animationslib`) | 0.0.2 | code | Maltiez | Animation system and shaders, Harmony-patched renderer |
| **Attribute Rendering Library** (`attributerenderinglibrary`) | 3.2.0 | code | Craluminum2413 and others | Attribute-driven variant shapes and textures |
| **Auto Config Lib** (`autoconfiglib`) | 2.0.10, 2.0.11 | code | TheInsanityGod | Generates ConfigLib menus for mods without one |
| **TassHunting** (`tasshunting`) | 0.14.12, 0.14.41 | code | Tasshroom33 | Hunting overhaul: bleeding, arrow recovery, angry animals |
| **HoR Performance Optimizer** (`horperfopt`) | 1.0.5 | code | Panduh | Recipe-matching and handbook speedups |

Also included: **SpawnersAPI** (`spawnersapi` 1.1.6, code, BoboDev), a spawner block whose JSON config sets `entitiesToSpawn`, light levels, max entities, spawn distances and bonus health/damage, and which can be disabled by torches. Useful if NPC types need placed spawners.

Added later: **Coinage** (`coinage`, 4 archives: 2.2.0, 2.2.2, 2.3.0, 2.4.0; code; author xel). Players design their own coins (shapes and designs stored in a per-world SQLite database) and mint them with punches, trussells, anvil dies and a mechanical coin press, in every vanilla metal and three sizes (`coinage:coin-{metal}-{sm|md|lg}`). It has no NPCs and does **not** hook into trading: coins carry no `currency` attribute, and its only trader patches (selling a coin pouch at the artisan and clothing traders) ship with `"enabled": false`. Vanilla traders count money through `attributes.currency.value` (`InventoryTrader.CurrencyValuePerItem`), so making NPC traders accept these coins would take a patch adding that attribute per metal and size. That is a suggestion, not something the mod does.

## 2. Project structure

Every archive follows the standard Vintage Story mod layout: `modinfo.json` at the root, an optional DLL (plus `.pdb` and `.deps.json` for most code mods), and an `assets/<domain>/` tree. No archive contains C# source.

```
<mod>.zip
├─ modinfo.json                 type, modid, version, dependencies
├─ <Mod>.dll / .pdb / .deps.json  code mods only
└─ assets/
   ├─ <modid>/
   │  ├─ entities/               entity type JSON (NPC definitions)
   │  ├─ config/dialogue/        conversable dialogue trees
   │  ├─ config/tradelists/      trader stock
   │  ├─ config/customplayermodels/  Player Model Lib races
   │  ├─ itemtypes/ blocktypes/  contracts, spawn eggs, gear
   │  ├─ shapes/ textures/ sounds/
   │  ├─ patches/                JSON patches into game: or other mods
   │  └─ lang/
   └─ game/                      files placed straight into the vanilla domain
```

NPC-relevant files by mod:

| Mod | Entity JSON | Dialogue / trade | Patches | Code |
| --- | --- | --- | --- | --- |
| Hirelings | `entities/hireling.json` (4 variants by `kind`) | `config/dialogue/guard.json`, `porter.json` | `patches/tradelists.json` (contracts into vanilla traders) | `Hirelings.dll` |
| NationGuard | `entities/guard.json` | none | none | `NationGuard.dll` |
| NationWarband | `entities/soldier.json` | none | `patches/crossbow-aim.json` | `NationWarband.dll` |
| Duskridge Settlers | `entities/settler.json` | none | 13 files in `patches/entity/` adding jobs, combat, healing to the settler and making drifters, shivers, bowtorns, locusts hostile to settlers | `DuskridgeSettlers.Core.dll` |
| DST NPC additions | `entities/humanoid/bandit.json`, `barbarian/`, `knighterrant.json`, `poacher.json` | none | 48 patch files, mostly compatibility with other mods | `dstnpc.dll` |
| Ale Traders | 4 files in `assets/game/entities/humanoid/trader-*.json` | `config/dialogue/`, `config/tradelists/` | worldgen structure and creature-item patches | none |
| Animal Trader | 2 files in `assets/game/entities/humanoid/` | same pattern | same pattern | none |
| Fantasy Creatures | 14 files in `entities/land/` | `config/dialogue/dragon-blue.json` | none | none |
| Undead Legion | 4 files in `entities/land/` | none | configlib and imm configs | none (uses Pet AI) |

## 3. Key findings

The five code mods that make controllable or hostile humans each solve the same problem differently, which gives you a spectrum of reference designs from pure JSON to heavy C#.

| Mod | Entity class | Body | Brain | How players control it |
| --- | --- | --- | --- | --- |
| Ale / Animal Trader | vanilla `EntityTrader` | vanilla `trader-female` shape, gender via `disableElements` (`breasts` or `malestuff`) | vanilla `taskai` (melee and seek only when `aggressiveondamage`) | Dialogue, `opentrade` |
| Hirelings | custom `EntityHireling` | seraph `seraph-faceless` + `extraskinnable` random skin parts | `taskai` with 9 custom tasks on the refactored `AiTaskBaseR` base: `hirelingmelee`, `hirelingranged`, `hirelingseek`, `hirelingfollow`, `hirelingpatrol`, `hirelingreturnpost`, `hirelingporterferry`, `hirelingportersort`, `hirelingportercollect` | Dialogue with custom triggers (`hirelingstance`, `hirelingsetroute`, `hirelingopengear`, `hirelingdismiss`...), 5 GUI dialogs, contract items |
| NationGuard | custom `EntityGuard` | vanilla `villager-male` + `game:villageraccessories-male` outfits | no `taskai`; all logic lives in `EntityGuard` (extends `EntityDressedHumanoid`, \~2,000 lines) in `OnGameTick`, with its own line-of-sight service | `/guard` chat commands (spawn, mode, mobs, add/remove player or group, storage, report) |
| NationWarband | custom `EntitySoldier` | vanilla `villager-male` + outfits | no `taskai`; `EntitySoldier` (extends `EntityDressedHumanoid`) moves itself with the vanilla `WaypointsTraverser`, coordinated by a `SquadBrain` class | `/squad`-style commands: stay, follow, goto, form, march, charge, scatter, mount, attack, stance |
| Duskridge Settlers | vanilla `EntityPlayerBot` | seraph + `extraskinnable` | `taskai` with \~19 custom `AiTaskSettler*` tasks plus 10 custom `settler*` behaviors | GUIs (management, jobs, inventory, customization, ledger) and scroll items |
| DST NPC additions | custom `EntityArmed` | own `villager-male` / `villager-female` shapes + own outfit configs | `taskai` mixing vanilla tasks with custom player-only variants (`bowturretmode`, `stayinrangeplayeronly`, `seekplayeronly`, `attackplayeronly`) | None: hostile factions targeting each other by `entityCodes` |

Other findings:

- **Factions by entity code.** DST builds faction warfare purely in JSON: each faction's tasks list enemy codes (knights target `bandit-*`, `poacher-*`, `barbarian-*`), and `*ByType` keys (`enabledByType`, `projectileDamageByType`) give grunt and boss variants different stats.
- **Dialogue drives commands.** Hirelings uses `dialogueByType` to pick the guard or porter tree, and `condition` on choices (`entity.hasroute`) to show options only when they apply. Fantasy Creatures' dragon uses `setVariables` (`entity.hasmet`) for first-meeting vs repeat greetings.
- **Traders in the `game` domain.** Ale and Animal Trader place entity files under `assets/game/entities/humanoid/` with `code: "trader"` and their own `type` state, so they become `game:trader-male-vintner-cold` and slot into vanilla trader systems. They also patch `game:itemtypes/creature/creature.json` to add spawn items and `game:worldgen/structures.json` to add trader homes.
- **Hiring through trade.** Hirelings sells `hirelings:contract-*` items by patching vanilla `trader-survivalgoods` and `trader-treasurehunter` trade lists.
- **Companions via Pet AI.** The Teapot mods use Pet AI tasks (`followmaster`, `stay`, `petmeleeattack`, `petseekentity`, `simplecommand`) and behaviors (`tameable`, `pettableextended`) alongside vanilla `cometoowner` and `followleadholder`.
- **Player races are not NPCs.** FA Races and Skeletons only define `config/customplayermodels/` for Player Model Lib; Middle-Earth Characters only adds player wearables.

## 4. Systems and dependencies

Only Pet AI is both required by other mods and present in the uploads; every other declared or referenced dependency below is missing from what was provided.

| Dependency | Needed by | Kind | Provided? |
| --- | --- | --- | --- |
| Pet AI (`petai` 5.1.1) | Giant Boar, Great Bighorn, Undead Legion | modinfo dependency | Yes (5.1.1 and 5.1.2) |
| Jaunt (`jaunt` 3.1.0) | Giant Boar, Great Bighorn | modinfo dependency | No |
| Player Model Lib (`playermodellib`) | FA Races (1.19.3), Skeletons (1.16.3) | modinfo dependency | Yes, added later (1.23.8, 1.24.0); see section 9 |
| JSON Patches Lib (`jsonpatcheslib` 1.4.1) | Animations lib | modinfo dependency | No |
| ConfigLib (`configlib`) | Auto Config Lib; Teapot mods' `configlib-patches.json` | modinfo dependency / optional config | No |
| Combat Overhaul | DST, Eidolon, Spooky Nights, Teapot mods, Middle-Earth Characters | optional compat patches | Partly: Combat Overhaul Fork added later (0.18.1, 0.18.8, 0.18.9), but its engine library `overhaullib` is still missing; see section 9 |
| "Nation Settlers" | NationGuard and NationWarband read `nationsettlers:ownerUid`, `nationsettlers:groups` attributes | soft integration found in DLL strings | No (not uploaded) |
| Auto Config Lib | DST (per its changelog) | config UI | Yes |
| Harmony (bundled with the game) | Duskridge (about 35 `*HarmonyPatch` classes), Animations lib, ARL, Auto Config Lib, Pet AI, TassHunting, HoR | runtime patching | Ships with the game |

How the systems relate:

- **Vanilla is the base for all of them.** Every NPC mod reuses vanilla classes (`EntityTrader`, `EntityPlayerBot`, `EntityAgent`, `EntityDrifter`, `EntityShiver`, `EntityEidolon`, `EntityGlowingAgent`), vanilla shapes (`seraph-faceless`, `villager-male`, `trader-female`) and vanilla outfit configs (`traderaccessories-male`, `villageraccessories-male`).
- **Cross-mod awareness.** NationGuard and NationWarband share the `nationguard:groupUid` attribute; DST ships compat patches for Fantasy Creatures, Maztec Outlaw, VS Village, Fauna of the Stone Age, XSkills and others; the Teapot mods patch Fantasy Creatures and Undead Legion so their creatures react to each other.
- **Config.** DST (`dstnpcConfig`, versioned config regenerated on mismatch), NationGuard (`NationGuard.json`), NationWarband (`NationWarband.json`), Spooky Nights (server and client JSON with legacy migration) and Hirelings (`HirelingsConfig`) each keep their own ModConfig file.

## 5. Potential issues and version changes

**Conflicts and inconsistencies found**

- `teapotsgreatbighorn_1.1.0.zip` contains a modinfo that says version **1.0.5**, so the file name and the mod's own version disagree.
- DST NPC additions: modinfo says **1.1.6**, but `changelog.txt` opens with **V1.1.16** (and mentions config version 1.7). Either the modinfo or the changelog is out of date.
- `lotrchar` modinfo has two `version` keys (`1.21.6` / `1.22.7`, apparently the game version, then `0.2.4` / `0.2.5`) and a trailing comma; `genelib` modinfo also has a trailing comma. The game's lenient parser probably accepts these, but strict JSON tools reject them.
- **Index-based patches are fragile.** Duskridge patches `/server/behaviors/12/aitasks/-` (12 is `taskai` today) and Undead Legion's config patches `/server/behaviors/6/aitasks/7/...`. Reordering behaviors in those entity files would silently break them. DST's own changelog (V1.1.15) records exactly this kind of silent patch failure.
- NationGuard and NationWarband log messages are in Spanish while their lang files cover `en`, `es-es`, `es-419`; harmless, but worth knowing when reading logs.

**Version differences (older to newer)**

| Mod | Change |
| --- | --- |
| Ale Traders 1.0.0 to 1.1.0 | Trader home spawn `chance` 1 to 0.5 and `MinSpawnDistance` 1500 to 500; two names added to the random name list; dialogue typo fix; new Steep and Brew compatibility patch |
| Animal Trader 1.2.1 to 1.2.2 | Adds muskox babies and sunflower grain to the trade list; same name-list and spawn-distance changes as Ale Traders |
| Pet AI 5.1.1 to 5.1.2 | DLL only. Confirmed by decompiling both: `AiTaskPetMeleeAttack` gains its own `ShouldExecute`, so a pet retaliates against whoever hit it within 15 blocks if it has direct contact, otherwise attacks the nearest target, with reach scaled by taming generation |
| Middle-Earth Characters 0.2.4 to 0.2.5 | Edits to 3 wearable files; modinfo game version 1.21.6 to 1.22.7 |
| Genelib 3.2.0 to 3.2.1 | DLL only. Confirmed: `Array2DAttribute.Equals` made null-safe and compares 2D array dimensions properly |
| Auto Config Lib 2.0.10 to 2.0.11 | DLL only; assembly-scanning and patching code reworked |
| TassHunting 0.14.12 to 0.14.41 | DLL only; many new config options (anger duration, chase range, bleed scaling, bones instead of corpses, dinosaur-mod support) |
| Giant Boar 1.0.6 to 1.1.0 | Steady growth: Polish, Portuguese, Ukrainian lang; config menu (1.0.8); trader patches; bridle2 (1.0.14); tusk helmet and Undead Legion compat (1.1.0) |
| Great Bighorn 1.0.3 to 1.1.0 | Bridle2 (1.0.4); butchering, saddle3, helmet, sounds, trader and Undead Legion patches (1.1.0) |

**Coinage versions** (all four decompiled and compared; assets are identical from 2.2.2 on, and the bundled `Clipper2Lib.dll` is the same file in all four):

| Version | Change |
| --- | --- |
| 2.2.0 to 2.2.2 | Crash guard when switching hotbar slots; coin shaper GUI step logic tidied; new `Polylabel` geometry helper; coin and planchet items become non-displaycaseable and get an `onDisplayTransform`; English lang edits |
| 2.2.2 to 2.3.0 | Null-player guard on the client; fix to the display shelf coin renderer (an inverted null check); coin mesh builder switched from triangles to quads with a mesh validator |
| 2.3.0 to 2.4.0 | Fires a client event `coinage.RECEIVE_COIN_DATA` (shape and design ids) so other mods with custom renderers can refresh coin meshes; renderer tweaks |

For all multi-version mods, the newest archive supersedes the older ones; nothing in an older version was found to be removed without replacement except Giant Boar's `catchlivestock.json` patch (dropped in 1.0.10) and a `game/itemtypes` override (dropped in 1.0.10).

## 6. Confirmed from decompiled code

Decompiling confirmed the section 3 picture and corrected one point: NationGuard and NationWarband do not use behaviors for their brains; the logic sits inside their entity subclasses. Patterns worth reusing in the mod maker:

| Pattern | Where | What it does |
| --- | --- | --- |
| Register AI tasks server-side only | Hirelings `HirelingsModSystem.StartServerSide`; Duskridge per-feature systems | Entity classes, behaviors and items go in `Start` (both sides); `RegisterAiTask<T>(code)` goes in `StartServerSide` because AI only runs on the server |
| Subclass a refactored vanilla task | Hirelings `AiTaskHirelingFollow : AiTaskStayCloseToEntityR` | \~50 lines: override `ShouldExecute` to check stance and set `targetEntity` to the owner, override `SearchForTarget`, stop while in conversation |
| Dialogue as command UI | Hirelings `EntityBehaviorHirelingCore.AfterInitialized` | Hooks `EntityBehaviorConversable.OnControllerCreated` to add a `DialogTriggers` handler, blocks talking via `CanConverse` when downed, and writes entity-scoped variables (`hasroute`, `hasmarks`, `hassort`) so dialogue `condition`s show the right options |
| Permission check per trigger | same | Owner or allow-list check before any command trigger runs; others get `hirelings:msg-not-allowed` |
| Register tasks from the entity class | DST `EntityArmed` static block, like vanilla `EntityButterfly` | Five legacy `AiTaskBaseTargetable` tasks registered when the class loads |
| Config rewrites loaded entities | DST `ModConfig.FactionConfigApplier` in `StartServerSide` | Per-faction enable, spawn settings, hostility to player, health and extra drops applied to entity types at startup instead of shipping alternate JSON |
| Direct pathfinding without `taskai` | NationWarband `EntitySoldier.NavigateTo` | Calls the vanilla `WaypointsTraverser.NavigateTo_Async` directly for squad movement |
| Feature-per-ModSystem | Duskridge (63 ModSystem classes) | Each job, combat role and navigation feature registers its own tasks and behaviors; `settlerseekattacker` and `settlerseekattackerv2` are two codes for the same class |
| Library tasks for companions | Pet AI `PetAI.cs` | `simplecommand` (class `AiTaskTrick`), `followmaster`, `stay`, `petmeleeattack`, `petseekentity`, `seeknest`, `happydance`, `avoidfire`; behaviors `tameable`, `receivecommand`, `givecommand`, `petinventory`, `pettableextended` |

Decompiled source is kept in the working session only, not in the project files, since it is other authors' code.

## 7. Knowledge limitations

- **DLLs are decompiled, not original source.** After your approval (2026-10-30) all 15 code mods were decompiled with ILSpy (about 110,000 lines). Logic is now readable, but local variable names and comments are lost, and some casts show as `//IL_` warnings because the game's own DLLs were not referenced. Only the newest version of each mod was decompiled, plus Pet AI 5.1.1 and Genelib 3.2.0 for the version diffs.
- **Images and sounds were not viewed.** The archives hold several hundred PNG textures and icons and some OGG sounds; I catalogued them but did not look at them individually.
- **Shapes were not opened in a model viewer.** Shape JSON was checked for paths and references only.
- **Vanilla assets are still absent.** References like `game:entity/humanoid/villager-male` or `game:villageraccessories-male` point to files that ship with the game, not with these mods or the anegostudios repos.
- **Large entity files were read selectively.** DST's bandit and knight files are about 70 KB each; I read their class, AI tasks and behaviors, not every outfit entry.

## 8. Project understanding

The uploads are a reference library, not one project: 24 independent mods by different authors that together show how the community builds NPCs on Vintage Story 1.22. They line up as a ladder of complexity that maps directly onto the NPC mod maker:

1. **Pure JSON on vanilla classes** (Ale Traders, Animal Trader, Fantasy Creatures). An entity file, a dialogue file, a trade list and a few patches produce a working trader or monster. This is what a mod maker can generate with no code.
2. **JSON plus a small library** (Teapot mods on Pet AI). Companion behavior comes from reusable AI tasks registered by another mod.
3. **Custom tasks and behaviors on the vanilla AI** (Hirelings, DST). A DLL registers a few entity classes, AI tasks and dialogue triggers; everything is still configured from entity JSON. Hirelings is the cleanest example: one entity file with a `kind` variant, refactored `-R` tasks, and dialogue as the command UI.
4. **Bespoke brains** (NationGuard, NationWarband, Duskridge). Custom behaviors replace or heavily extend `taskai`, with chat commands, GUIs and Harmony patches. Powerful but hard to template.

For the mod maker, levels 1 and 3 are the sweet spot: generate entity, dialogue and trade JSON for each NPC type, and ship a small shared DLL with the few tasks and triggers vanilla lacks (follow, patrol, guard post, faction targeting). The base-code reference doc lists the vanilla pieces each level builds on.

## 9. Dependency mods added later

**Can Fourth Age Races models and skins be used on NPCs, not just players?** Partly. The race models and skin parts can probably go on an NPC, but the races' armor and clothing fitting cannot, and this has not been tested in game. It is read from Player Model Lib 1.24.0's decompiled code:

| Piece | Works on an NPC? | Evidence |
| --- | --- | --- |
| Swapping the body to a race model | Probably yes | The `PlayerModelLib:ExtraSkinnable` behavior (`PlayerSkinBehavior`) checks `entity is EntityPlayer` with a null-safe cast everywhere and skips player-only work for other entities. It swaps the shape in `OnTesselation`, which the normal `Shape` renderer NPCs use also calls. |
| Choosing which race | Needs a little code | The model is read only from the entity's watched attribute `skinModel` (default `seraph`). There is no entity-JSON key for it, so a spawn hook or custom behavior must set `skinModel` to the full model code. Player Model Lib stores every model as domain:key (FromAsset, CustomModelsSystem.cs line 1316), so the FA dwarf is faraces:`dwarf`). |
| Random skin, hair, eyes per NPC | Yes | On the server, an entity with no skin parts chosen gets `RandomizeSkin` automatically. |
| Armor and clothing fitted to the race body | No | `PlayerModelLib:WearablesTesselator` throws `"WearablesTesselator should be attached only to 'EntityPlayer'"` for any other entity. Gear on an NPC would use vanilla fitting, which may not match dwarf or elf proportions. |
| Race traits, class limits, voice, head bobbing | Player only | Those code paths only run when the entity is an `EntityPlayer`; they are skipped quietly for NPCs. |
| Races hidden from players | Usable for NPCs | FA Races sets `Enabled: false` on elf, orc, sorceror and Duende. In Player Model Lib, `Enabled` only filters the player selection lists, so those models still load and could be NPC-only races (inferred). |

A lower-risk alternative that needs no Player Model Lib code: point the NPC's `client.shape` at a race shape directly (`faraces:entity/humanoid/dwarf-faceless`, `elf-faceless` or `seraph-faceless`), copy the race's skin parts into the vanilla `extraskinnable` `skinnableParts` attribute (as Hirelings does for seraphs), and set `client.size` from the race's `SizeRange`. This gives fixed race bodies on NPCs, at the cost of duplicating the race config. Caveat: the race files use Player Model Lib's extended skin-part format (`SkinnablePartExtended`), so some fields may not carry over to vanilla `extraskinnable`. Untested.

Race keys in FA Races (correction found during the LRC upload: the full model code adds the faraces: prefix, for example faraces:dwarf. The key is the dictionary key, not the file name, and case matters): `dwarf`, `Elf`, `hobbit`, `orc`, `goblin`, `easterling`, `northman` (file `northmen.json`), `numenorean` (file `numenorians.json`), `sorceror`, `Duende`.

| Mod | Versions | What it is | Changes between versions |
| --- | --- | --- | --- |
| **Player Model Lib** (`playermodellib`, Maltiez and Caliber) | 1.23.8, 1.24.0 | Loads `config/customplayermodels/` race models, replaces the player's skin, inventory and renderer behaviors, adds a character-creation GUI. Needs `jsonpatcheslib` 1.5.2 and `overhaullib` 2.0.10 (neither uploaded). | 1.24.0 adds per-model `RandomIdleAnimations` (new `RandomIdleAnimationsBehavior` replaces the player's `idleanimations`) and per-model supported or unsupported animations; compat patch updates |
| **Combat Overhaul Fork** (`combatoverhaulfork`, Maltiez and Alodh) | 0.18.1, 0.18.8, 0.18.9 | Mostly content: weapons, armor, animations, and patches adding `CombatOverhaul:EntityColliders` and `CombatOverhaul:EntityDamageModel` to vanilla animals and lore mobs. The DLL holds only 4 classes; the combat engine lives in `overhaulliblegacycompat` and `overhaullib`, which were not uploaded. It does not patch traders or villagers, other than adding a linen sling to the survival goods trade list. | 0.18.1 to 0.18.8 drops the `animationslib` and `configlib` dependencies (animations now built in) and reworks Toolsmith compatibility; bow and sword item and armor recipe tweaks. 0.18.8 to 0.18.9 changes only the build. |
| **PlayerLists** (`playerlists`, BillyGalbreath and midzuyo) | 2.3.7 | Server player-list HUD with ping icons, opened by a hotkey. Nothing NPC-related. Part of the DLL is obfuscated (classes named `A` to `F`). | Single version |

## 10. LRC: primary source for NPC looks

The user's instruction (2026-10-30, with the upload): use LRC primarily for how the NPCs look. This section reads LRC against the v1 design (Men, Dwarves, Hobbits, Orcs, Goblins) and against FA Races, which the design used until now.

**What it is.** LOTR Races and Classes 0.4.1 (modid `lrc`; authors Dango Dango Dango, Shakgriig and Buggoi). It is a content mod with no DLL. It depends on `playermodellib` 1.23.8, and the uploaded 1.24.0 also satisfies that, because Vintage Story dependencies are minimum versions. The zip holds 440 entries (352 files, 158 PNG). Like FA Races, it is a set of Player Model Lib race models plus player traits and classes.

### Race models against the v1 races

| v1 race | LRC model codes (display name) | Shape | Size notes |
| --- | --- | --- | --- |
| Men | `lrc:freepeople` (Men of the West), `lrc:wildmen`, `lrc:easterling`, `lrc:haradrim`, `lrc:bnumenorean` (Black Númenórean) | `lrc:entity/humanoid/seraph-faceless` | Normal size |
| Dwarves | `lrc:longbeard`, `lrc:firebeardsandbroadbeams`, `lrc:wickeddwarf` | `lrc:entity/humanoid/dwarf-faceless` | Longbeard: factor 0.95, box 0.58 x 1.5, eye height 1.46 |
| Hobbits | `lrc:harfoot`, `lrc:fallohide`, `lrc:stoor` | `seraph-faceless` | Harfoot: factor 0.7 |
| Orcs | `lrc:orc`, `lrc:uruk` (Uruk-hai) | `seraph-faceless` | Orc: factor 0.77. Uruk: factor 1.0 |
| Goblins | None |  | **Gap**: see issue 1 |
| Elves (not in v1) | `lrc:avari`, `lrc:noldor`, `lrc:sindar`, `lrc:tormentedelf` | `lrc:entity/humanoid/elf-faceless` |  |

All 17 models are `Enabled: true`, so players can pick them too, unlike FA Races, which hides its orc and elf. Codes come out as `lrc:<key>` because Player Model Lib prefixes the asset's domain (see the section 9 correction). So `lrc:orc` and `faraces:orc` are separate models, and both mods can be installed at once without a code clash (confirmed from code). An NPC's `skinModel` must hold the full code.

**Faction mapping (my interpretation, not decided).**

- Gondor and Rohan: `lrc:freepeople`. There is no Rohirrim model, so the two would differ by gear and hair only.
- Dunlendings: `lrc:wildmen`.
- Easterlings: `lrc:easterling`.
- Dwarves: `lrc:longbeard`.
- Shire: `lrc:harfoot`, `lrc:fallohide` and `lrc:stoor`.
- Mordor Orcs: `lrc:uruk`, and possibly `lrc:orc`.
- Goblins: open.

### How the looks are built

- Skins: each model has random-pickable parts (base skin, eye colour, ears, hair, beard, expression and so on), and Player Model Lib randomizes them. Skin sets:
  - `orcskin1-9`
  - `dwarfskin1-10`
  - `hobbitskin1-8`
  - `humanskin1-30`, split by people. For example, Easterlings use humanskin10 onward and Haradrim use darker tones. Wildmen add `humanskin1a`/`2a` variants.
- The `face` part (target `playermodellib-face`) reuses the same texture as the base skin. Expressions use LRC's own `lrc:seraphfaces/*` shapes (18 codes referenced).
- Dwarves: dwarf beards `lrc:entity/humanoid/hair-face/dwarf-1..5`, plus the vanilla beard set.
- Ears per race: elf, orc and goblin ear shapes under `lrc:entity/humanoid/ears/`.
- Fitted armor: `config/model-replacements-byshape.json` maps 96 vanilla armor and clothing shapes to dwarf-fitted LRC versions, for each of the three dwarf models. Section 9 found that this wearable replacement runs only for players. For dwarf NPCs, outfits would have to name the `lrc:entity/humanoid/armor/...` shapes directly.
- Classes and traits (`lotrwarrior`, `lotrlabourer`, `lotrhunter`, `lotrfarmer`, and race traits in `patches/char-class/`) are player-only and do not matter for NPCs. The two `config` files `characterclasses.json` and `traits.json` are empty arrays.

### Relation to FA Races

56 of LRC's 131 distinct textures are byte-identical to FA Races files:

- 20 seraph body skins
- 30 metal textures
- 3 underwear textures
- 3 face textures

So the two share an origin, but the files do not show which came first. LRC does not depend on FA Races.

LRC covers more peoples than FA Races does:

- Men: 5 models (FA has 3)
- Dwarves: 3 (FA has 1)
- Hobbits: 3 (FA has 1)
- Orcs: 2, both enabled (FA's orc is disabled)

FA has a goblin model (`faraces:goblin`, factor 0.77, enabled) and LRC does not.

### Issues flagged

1. **No goblin model, but LRC's small orc looks goblin-built.** The evidence:
   - `lrc:orc` uses the goblin ear shapes (`goblin_earsomitted`, `goblin_earselated`), while `lrc:uruk` uses the orc ears.
   - The orc trait file defines `goblin-nimbleness-1..4`.
   - A commented-out patch gives the orc model "goblin" classes.
   - Goblin face shapes ship but nothing references them.

   My reading is that `lrc:orc` is LRC's goblin-sized orc and `lrc:uruk` its big orc. Whether Goblins use `lrc:orc` or `faraces:goblin` is a design choice.
2. **`lrc:orc` draws at 0.77 scale but keeps a full-height box** (0.6 x 1.85, eye height 1.7). The smaller values, 0.45 x 1.30 and eye height 1.35, are commented out next to them. That is a player-side mismatch. For NPCs, our entity JSON sets the hitbox, but this is worth knowing.
3. Unused assets: the per-race face shapes (`shapes/entity/humanoid/face/{dwarf,elf,goblin,orc}`) and `textures/entity/humanoid/face/orc/cadmium-orange.png` are not referenced by any LRC config. They are probably work in progress.
4. `patches/availableclasses.json` is commented out entirely. It names models that no longer exist (`dwarf-char`, `elf-char`, `/human`), which suggests older model names.
5. `patches/aldisclasses.json` applies only when a mod `aldiclasses` is installed. That mod was not uploaded.
6. Packaging leftovers that do no harm:
   - an empty folder `shapes/entity/humanoid/armor/Новая папка/`, which is Russian for "New folder" and caused a filename warning on extraction
   - a Krita source file, `textures/icons/hobbit.kra`
   - config files that use `//` comments and trailing commas. Vintage Story accepts these, but strict JSON tools do not.
7. Not checked in game: whether LRC's skins render on NPCs. The route is the same as FA Races (Player Model Lib `ExtraSkinnable` plus `skinModel`), but that route is not proven yet. The first in-game test used the bare key orc, which most likely fell back to the default model, so a retest with full codes (lrc:orc, lrc:uruk, faraces:orc) is pending. The model code must be `lrc:...`.
