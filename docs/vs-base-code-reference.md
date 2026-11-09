# Vintage Story NPC modding: base code reference

Oct 30, 2026 · @Emperorcharizar

## What was pulled in

All four repos are public and cloned cleanly from github.com/anegostudios. They hold C# source only: the game's JSON assets (entity types, shapes, dialogue files, trade lists, activity collections) are not in any of them and ship in the game install under `assets/survival/...`. To see real NPC JSON, read it from a local install or from the example mods.

| Repo | Version | What matters for NPCs |
| --- | --- | --- |
| [vsapi](https://github.com/anegostudios/vsapi) | 1.22.7 | Base types: `Entity`, `EntityAgent`, `EntityHumanoid`, `EntityBehavior`, `ModSystem`, `ICoreAPI` registration methods. Also generated JSON docs in `docs/json-docs/`. |
| [vsessentialsmod](https://github.com/anegostudios/vsessentialsmod) | 1.22.7 | The AI task system, pathfinding, core behaviors (health, nametag, taskai, emotionstates), and the `EntityType` JSON loader. |
| [vssurvivalmod](https://github.com/anegostudios/vssurvivalmod) | 1.22.7 | Trader and villager entities, dialogue, trading, outfits, the activity system, `/npc` commands, plus most entity registrations. |
| [vscreativemod](https://github.com/anegostudios/vscreativemod) | 1.22.0-rc.1 | WorldEdit only. Nothing NPC-specific (one test entity, `EntityTestShip`). |

## NPC entity classes

The vanilla NPC line is a single inheritance chain, and the class name you put in an entity JSON's `class` field picks where on it your NPC sits. For most NPC types you can reuse these classes and only write JSON; subclass only when you need new interaction logic.

```
Entity                          vsapi/Common/Entity/Entity.cs
└─ EntityAgent                  vsapi/Common/Entity/EntityAgent.cs        (controls, inventory hands, AI-capable)
   └─ EntityHumanoid            vsapi/Common/Entity/EntityHumanoid.cs     (humanoid animation handling)
      └─ EntityDressedHumanoid  vssurvivalmod/Lore/Village/EntityDressedHumanoid.cs   (random outfits)
         └─ EntityTradingHumanoid  vssurvivalmod/Systems/Trading/EntityTradingHumanoid.cs (trade inventory, "opentrade")
            ├─ EntityTrader     vssurvivalmod/Systems/Trading/EntityTrader.cs   (wandering traders, talk sounds)
            └─ EntityVillager   vssurvivalmod/Lore/Village/EntityVillager.cs    (personalities, voices, hairstyling)
```

| JSON `class` | C# type | Use it for |
| --- | --- | --- |
| `EntityAgent` (engine built-in) | `EntityAgent` | Simple mobs or guards with no outfits or trading |
| `EntityDressedHumanoid` | `EntityDressedHumanoid` | Humanoids with random outfits from an outfit config, no trade |
| `EntityTrader` | `EntityTrader` | Merchants with a trade list and dialogue |
| `EntityVillager` | `EntityVillager` | Village NPCs with personality voices, dialogue, optional trade |
| `EntityPlayerBot` / `EntityAnimalBot` | `vssurvivalmod/Entities/` | Scripted NPCs driven by the `/npc` command list |

Key attributes read by these classes (under `attributes` in the entity JSON): `outfitConfigFileName` (default `traderaccessories`), `outfit`, `partialRandomOutfits`, `outfitversion`, `tradePropsFile` or `tradeProps`, `personalities`, `personality`, `voiceSounds`, `isMultiSoundVoice`.

## Entity JSON (assets/\<modid>/entities/\*.json)

The schema is the `EntityType` class in `vsessentialsmod/Loading/EntityType.cs`; every field there maps 1:1 to a JSON key. Behaviors are listed twice, once under `client` and once under `server`; behaviors that sync state (health, nametag, conversable) must appear on both sides, and \`conversable\` throws if the two sides name different dialogue files.

| Block | Fields | Notes |
| --- | --- | --- |
| Top level | `code`, `class`, `variantgroups`, `habitat`, `hitboxSize`, `collisionBoxSize`, `selectionBoxSize`, `eyeHeight`, `weight`, `canClimb`, `knockbackResistance`, `fallDamageMultiplier`, `attributes`, `behaviorConfigs`, `sounds`, `idleSoundChance`, `drops`, `tags` | `class` = the registered entity class name |
| `client` (`ClientEntityConfig`) | `renderer` (usually `Shape`), `shape`, `textures`, `size`, `glowLevel`, `animations`, `behaviors` | Renderers registered in `vsessentialsmod/Core.cs`: `Shape`, `PlayerShape`, `Item`, `Dummy` |
| `server` (`ServerEntityConfig`) | `behaviors`, `attributes`, `spawnConditions` | `taskai`, `health`, `conversable`, `activitydriven` config lives here |

Shapes go in `assets/<modid>/shapes/entity/...` and are VS Model Creator JSON. Humanoid NPCs in vanilla reuse the seraph (player) shape with skin parts and outfits rather than a bespoke model. `behaviorConfigs` holds attributes keyed by behavior code that get merged into the matching client and server behavior entries, so shared config is written once.

## AI tasks

NPC brains are the `taskai` behavior (`vsessentialsmod/Entity/AI/BehaviorTaskAI.cs`). It reads `aitasks` from its server config, looks each `code` up in `AiTaskRegistry`, and instantiates the class with `(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)`. `AiTaskManager` runs up to 8 slots in parallel; in each slot the highest `priority` task whose `ShouldExecute()` passes wins, and `priorityForCancel` decides whether it can be interrupted.

- Registry and manager: `vsessentialsmod/Entity/AI/AiTaskManager.cs` (`AiTaskRegistry`, `AiTaskManager`, extension `sapi.RegisterAiTask<T>(code)`)
- Interface: `vsessentialsmod/Entity/AI/IAiTask.cs` (`ShouldExecute`, `StartExecute`, `ContinueExecute`, `FinishExecute`, `Notify`)
- Legacy base: `Entity/AI/AiTaskBase.cs`, tasks in `Entity/AI/Tasks/`
- Refactored base (codes end in `-r`): `Entity/AI/TasksRefactored/AiTaskBase.cs` (`AiTaskBaseR` + typed `AiTaskBaseConfig` loaded with `LoadConfig<T>`). New code should target this one.
- Pathfinding: `Entity/Pathfinding/` (A\* in `Astar/`, `WaypointsTraverser`, `StraightLineTraverser`)

| Task codes | Purpose |
| --- | --- |
| `wander`, `idle`, `lookaround`, `lookatentity`, `getoutofwater` | Ambient behavior |
| `seekentity`, `meleeattack`, `shootatentity-r`, `throwatentity`, `stayinrange` | Hostile or guard combat |
| `fleeentity`, `stayclosetoentity`, `gotoentity`, `cometoowner` | Following, fleeing, escorting |
| `stayclosetoguardedentity`, `seektargetingentity`, `meleeattacktargetingentity` | Bodyguard pattern (protect an entity, attack whoever targets it), in `Tasks/Guard/` |
| `jealousseekentity`, `jealousmeleeattack` | Territorial variants |
| `seekfoodandeat`, `useinventory`, `eathelditem-r` | Needs and items |
| `turretmode`, `bellalarm`, `followleadholder` | Special cases |

Refactored `-r` versions exist for wander, idle, lookaround, lookatentity, fleeentity, seekentity, meleeattack, stayclosetoentity, stayinrange, turretmode, bellalarm, cometoowner, getoutofwater and seekfoodandeat. Common config keys on every task: `code`, `enabled`, `id`, `priority`, `priorityForCancel`, `slot`, `minCooldownMs`/`maxCooldownMs`, `minCooldownHours`/`maxCooldownHours`, `executionChance`, `animation`, `animationSpeed`, `sound`, `whenInEmotionState`, `whenNotInEmotionState`, `duringDayTimeFrames (legacy) or duringDayTimeFramesHours (-r)`, `stopOnHurt`, `minDurationMs`/`maxDurationMs`.

Custom task recipe: subclass `AiTaskBaseR`, add a config class deriving `AiTaskBaseConfig`, call `AiTaskRegistry.Register<MyTask>("mymod-mytask")` in your ModSystem's `Start` (the registry is a plain string dictionary with no domain, so prefix codes to avoid clashes), then reference that code in `aitasks`.

## Entity behaviors useful for NPCs

Behaviors are listed by `code` in the entity JSON and registered with `api.RegisterEntityBehaviorClass(code, type)`. Vanilla registrations are in `vsessentialsmod/Core.cs` (around line 144) and `vssurvivalmod/Systems/Core.cs` (around line 1062). Base class: `vsapi/Common/Entity/EntityBehavior.cs` (override `Initialize`, `OnGameTick`, `OnInteract`, `OnEntityReceiveDamage`, `GetInteractionHelp`, `ToBytes`/`FromBytes`).

| Code | Class and file | What it gives an NPC |
| --- | --- | --- |
| `taskai` | `EntityBehaviorTaskAI`, essentials `Entity/AI/BehaviorTaskAI.cs` | The AI task brain |
| `controlledphysics` / `repulseagents` / `interpolateposition` | essentials `Entity/Behavior/` | Movement physics, pushing, smooth client movement |
| `health` | `BehaviorHealth.cs` | HP, damage, death |
| `nametag` | `BehaviorNameTag.cs` | Name above head; `selectFromRandomName`, `showtagonlywhentargeted`, `triggeredNameReveal` |
| `emotionstates` | `BehaviorEmotionStates.cs` | Moods like aggressive or fleeing that gate AI tasks |
| `conversable` | `EntityBehaviorConversable`, survival `Systems/Dialogue/BehaviorConversable.cs` | Right-click dialogue |
| `activitydriven` | `EntityBehaviorActivityDriven`, survival `Systems/EntityActivitySystem/` | Scheduled daily routines |
| `villagerinventory` | `EntityBehaviorVillagerInv`, survival `Lore/Village/EntityDressedHumanoid.cs` | Inventory for dressed humanoids |
| `extraskinnable` | survival `Entity/Behavior/BehaviorExtraSkinnable.cs` | Seraph-style skin parts (hair, eyes, skin color) |
| `commandable`, `ownable` | survival `Entity/Behavior/BehaviorCommandable.cs`, `Systems/Riding/EntityBehaviorOwnable.cs` | Follow/stay commands and ownership, for companions |
| `despawn`, `idleanimations`, `boss`, `mortallywoundable`, `reviveondeath` | essentials and survival | Lifecycle and presentation extras |

## Dialogue

Dialogue is the `conversable` behavior plus a JSON file of components, all in `vssurvivalmod/Systems/Dialogue/`. Behavior config: `dialogue` (asset path, required on both client and server, must match), `approachPlayer` (default true: the NPC walks to you via `AiTaskGotoEntityConversable` before talking).

File lookup (`BehaviorConversable.cs`, `loadDialogue`) tries, in order: `<path>-<personality>-<characterclass>.json`, then `<path>-<personality>.json`, then `<path>.json`. So one NPC type can have per-personality and per-player-class variants for free.

The file is `{ "components": [ ... ] }`, parsed by `Component/DialogueComponentConverter.cs`:

| `type` | Class | Key fields |
| --- | --- | --- |
| `talk` | `DlgTalkComponent` | `owner` (`player` = clickable choices, otherwise NPC line), `text[]` each with `value`, `jumpTo`, `conditions` |
| `condition` | `DlgConditionComponent` | `variable`, `isvalue` or `isnotvalue`, `thenJumpTo`, `elseJumpTo` |
| `trigger`, `setvariables`, `jump` | `DlgGenericComponent` | `trigger`, `triggerdata`, `setVariables`, `jumpTo` |

Every component also takes `code`, `sound`, `setVariables`, `trigger`. Built-in trigger values handled in `BehaviorConversable.cs`: `closedialogue`, `playanimation`, `giveitemstack`, `takefrominventory`, `spawnentity`, `repairheldtool`, `repairheldarmor`, `attack`, `revealname`, `unlockdoor`. Entity classes add their own: `opentrade` (`EntityTradingHumanoid`) and `openhairstyling` (`EntityVillager`). Custom NPC types can hook `DialogueController.DialogTriggers` to add more.

Variables (`VariablesModSystem.cs`) are scoped `entity`, `group`, `player`, `global`, or `entityplayer`, and are shared with the activity system, so dialogue can change routines and vice versa. Supporting files: `DialogueController.cs` (state machine per player), `GuiDialogueDialog.cs` (client UI).

## Trading, outfits, routines and scripted NPCs

**Trading** (`vssurvivalmod/Systems/Trading/`). `EntityTradingHumanoid` loads `tradeProps` inline or from `tradePropsFile`. Shape (`TradeClasses.cs`, `TradeItem.cs`): `money` (NatFloat), `buying` and `selling`, each `{ maxItems, list: [TradeItem] }`. A `TradeItem` is a JSON item stack plus `price`, `stock`, `restock { quantity, hourDelay }`, `supplyDemand { priceChangePerPurchase, priceChangePerDay }`. `ClimateSpecificTraderTypes.cs` swaps trader variants by climate.

**Outfits** (`HumanoidOutfits.cs`, `EntityDressedHumanoid.cs`). Outfit configs live at `config/<outfitConfigFileName>.json` and define slots with weighted shape and texture variants. Entities pick fixed slots via `outfit` and random ones via `partialRandomOutfits`; bump `outfitversion` to re-roll existing NPCs. Hair styles: `ModSystemNPCHairStyling.cs`.

**Activity system** (`vssurvivalmod/Systems/EntityActivitySystem/`). The `activitydriven` behavior loads `config/activitycollections/<activityCollectionPath>.json`, a set of activities each with conditions and an action list. Conditions (`Conditions/`): time of day, month, temperature, light level, temporal storm, random, variable, vicinity of block, entity or position, held item, dressed, mounted, emotion state, after another activity. Actions (`Action/`): goto, teleport, turn, lookat block or entity, play or stop animation, play sound or song, talk, equip or unequip, dress or undress, mount or unmount block, activate block, set variable, start activity, trigger emotion state, wait, jump, and `StandardAIAction` which runs a normal AI task. There is an in-game editor via `/dev aedit` (`Gui.cs`), which can save collections straight to JSON. This is the best fit for village-style NPCs with daily schedules.

**Scripted NPCs** (`vssurvivalmod/Systems/NpcControl.cs`). `/npc` commands (`list`, `enqueue` tp/goto/playanim/lookat, `upd`, `start`, `stop`, `clear`, `exec`) drive `EntityAnimalBot` and `EntityPlayerBot`, mainly for cinematics. Debug: `/dev talk`, `/dev pro freeze|unfreeze|set`, `/dev astardebug` in `Lore/Village/ModSystemVillagerDebug.cs`.

## Registering code in a ModSystem

A code mod is a class deriving `ModSystem` (`vsapi/Common/API/ModSystem.cs`; hooks `ShouldLoad`, `ExecuteOrder`, `StartPre`, `Start`, `AssetsLoaded`, `AssetsFinalize`, `StartClientSide`, `StartServerSide`, `Dispose`). Entity classes and behaviors must be registered on both sides, so do it in `Start`. Registration methods live on `ICoreAPI` (`vsapi/Common/API/ICoreAPI.cs`, lines 46 to 113) and `ICoreClientAPI.RegisterEntityRendererClass`. The vanilla pattern to copy is `vssurvivalmod/Systems/Core.cs` around lines 1031 to 1095.

```csharp
public class NpcMakerModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        api.RegisterEntity("EntityMyNpc", typeof(EntityMyNpc));                 // JSON "class"
        api.RegisterEntityBehaviorClass("mymod-schedule", typeof(MyBehavior)); // JSON behavior "code"
        AiTaskRegistry.Register<AiTaskMyPatrol>("mymod-patrol");               // JSON aitasks "code"
    }
}
```

Your mod references `VintagestoryAPI.dll`, plus `VSEssentials.dll` and `VSSurvivalMod.dll` from the game's `Mods` folder when you subclass trader, villager or AI task types, and declares them as dependencies in `modinfo.json`.

## What this means for the NPC mod maker

Most NPC types can be pure JSON on top of vanilla classes, so the mod maker is mainly a JSON generator with a small C# layer for anything vanilla lacks.

| NPC type idea | Base class | Behaviors | AI tasks |
| --- | --- | --- | --- |
| Merchant | `EntityTrader` | `taskai`, `conversable`, `nametag`, `health` | `idle`, `lookatentity`, `wander` (short range) |
| Villager with a routine | `EntityVillager` | add `activitydriven`, `villagerinventory`, `extraskinnable` | `idle`, `lookatentity`; routine from an activity collection |
| Guard | `EntityDressedHumanoid` | `taskai`, `health`, `emotionstates` | `seekentity`, `meleeattack`, `stayclosetoguardedentity`, `wander` |
| Companion / follower | `EntityDressedHumanoid` | `commandable`, `ownable` | `stayclosetoentity`, `cometoowner`, `meleeattack` |
| Quest giver | `EntityVillager` | `conversable` with variables and `giveitemstack` / `takefrominventory` triggers | `idle`, `lookatentity` |

Open question: the vanilla NPC JSON (trader, villager, outfit and activity configs) is not in these repos. The example mods you plan to upload, or a copy of `assets/survival/entities/humanoid/` from a game install, would fill that gap.
