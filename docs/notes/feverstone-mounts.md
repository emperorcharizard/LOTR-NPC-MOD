---
name: feverstone-mounts
description: Feverstone Wilds 3.1.1 (game 1.22.7) read for mounted units (Rohan riders, warg riders): what it has, how the game's riding works, why an NPC can't steer a mount on its own, the plan and the owner's open questions
metadata:
  type: project
---

Owner, 2026-11-12: "I want to use it as a compatibility mod for mounted units, like warg riders or rohan riders." "Riders ... be an upgrade from warrior like the captain." Then: "riders could spawn naturally, but would be rarer than captains." Read from `Downloads/FeverstoneWilds-v3.1.1-1.22.7.zip` (unpacked to the session scratchpad; it's also in the owner's Mods folder). Nothing has been built yet.

## What Feverstone has

- modid `feverstonewilds`, code mod (feverstonewilds.dll), game 1.22.7, which matches ours. ModConfig/FeverstoneWildsConfig.json can switch each creature off (`FSWHorseEnabled`, `FSWTameDirewolfEnabled`); a switched-off creature has no entity type.
- Horse: `feverstonewilds:horse-{wild|tamed}-{male|female}-{donkey|shire|quarter}`. Percheron and arabian are in the variant list but skipped, so they don't exist. Health: donkey 24, quarter 28, shire 34. Gaits walk 0.019, sprint 0.1067. It can be ridden only when `-tamed-` (or `taminghorse-*`, which is being saddle-broken). One seat, `Rider` attachment point, controllable.
- Dire wolf (the warg): `feverstonewilds:tame-direwolf-{male|female}`, health 32, one controllable `Rider` seat. The wild `direwolf` can't be ridden.
- Tack is wearables in the mount's attachable slots: `fsmountwearables` with breed (donkey, shire, quarter, direwolf, ...), category (head, middleback, lowerbackside) and type (saddle, bridle, saddlebags). The dire wolf also has a `companionhelmet-skullhelmetdirewolf`.
- Both use the **game's own** `rideable` and `gait` behaviors. Only the griffon has Feverstone's own flying ride (`flightrideable`), which isn't needed here.

## How the game's riding works (vssurvivalmod BehaviorRideable, 1.22)

- `EntityAgent.TryMount(IMountableSeat)` / `TryUnmount()` and `MountedOn` are in VintagestoryAPI; `EntityBehaviorGait` (CurrentGait, AngularVelocity, SetIdle) is in VSEssentials. All of these are in tools/refs already. `EntityBehaviorRideable` (SpeedUp, SlowDown, Controller, SeatsToMotion) is in VSSurvivalMod, which is not in tools/refs.
- The controller is the first passenger in a controllable seat. Nothing checks that it is a player.
- **The blocker:** the rider's keys (forward, back, sprint, left, right, jump) are read only on the rider's own client, which sends the gait to the server (`ReceiveGaitFromClient`). The server then moves the mount from `CurrentGait` and `AngularVelocity`. An NPC has no client, so a mounted NPC would sit still.
- While anyone is seated, every AI task of the mount is blocked (`TaskManager_OnShouldExecuteTask`), and wandering stays blocked for 24 game hours after dismounting.
- The controller can't hurt its own mount. When the mount dies, its riders are unmounted.
- Not checked yet: whether riding needs a saddle (`CanRide` comes from the attachable behavior), and whether the rider's sitting animation plays for an NPC (it isn't started in BehaviorRideable).

## Plan (needs an in-game spike first)

1. A new branch rank `rider`, promoted from warrior next to captain (`Promotion.Targets`: warrior -> captain, rider). Like guard/shock it only exists for factions with a mount, and only when Feverstone is loaded with that creature on (Offers already skips ranks with no entity type; the mount code must be checked too).
2. When a rider spawns or is promoted, the server spawns its mount next to it (tamed horse or tame dire wolf, with saddle and bridle in its tack slots) and calls `rider.TryMount(seat)`.
3. **Steering, our own code:** each server tick, for a mounted rider, take where its own AI wants to go (its path target or follow point, from the rider's walk vector) and set the mount's yaw and gait (idle, walk or sprint by distance) through EntityBehaviorGait. This needs no VSSurvivalMod reference. Risk: no pathfinding for the mount itself, so it can get stuck on walls. Fallback: let the mount run our follow and seek tasks itself (unblock its tasks while an NPC rides it).
4. Combat from the saddle: melee reach may need to grow because the rider sits higher. Horse archers are a later step.
5. Wild spawns: a rider chance below CaptainGroupChance (0.12) in lotrnpcs-spawning.json, e.g. `RiderGroupChance`.
6. Saves: check that NPC mounts stay mounted after a reload (players' mounts do, through `mountedOn`).

Spike first: one admin command that spawns a horse and mounts a test NPC on it. Check, in game, that it sits on it with the right animation, that our steering moves the horse, and that both survive a save and reload. Only then build the rank. Owner, 2026-11-12: the spike waits until after the 0.0.114 in-game test.

## Open questions for the owner

- Which factions ride what? Suggested: Rohan, Gondor, Dale, Arnor on horses (quarter or shire); Isengard, Mordor, Gundabad, Angmar orcs on dire wolves; Shire hobbits on donkeys, or none. Elves and dwarves: none?
- Optional dependency (no Feverstone means no riders), or required?
- When the rider dies, does the mount stay as a tamed animal anyone can take, run off, or vanish? And when the mount dies, does the rider fight on foot?
- Do riders earn XP, and what does promotion to rider cost?
- In the wild: does a rider lead a group like a captain, or ride alone?

## Owner's answers, 2026-11-12

- Who rides: the suggested set (Rohan, Gondor, Dale, Arnor on horses; Isengard, Mordor, Gundabad, Angmar orcs on dire wolves; no hobbits), plus **elves on elk** and **dwarves on Teapot's giant boars**. Those are two more mods, both in the owner's ModsByServer/199.115.74.6-9100 folder: elkjaunt 1.4.0 with elkvariants 3.7.2, and teapotsgiantboar 1.0.14. Not read yet: whether their elk and boar use the game's rideable behavior (as Feverstone's horse and wolf do) and what their entity codes are.
- Dependency: optional. Without Feverstone (or the elk / boar mods) the mod works as now, with no riders for those factions.
- When a rider dies its mount runs off: it turns wild and wanders away (no free tamed mounts to farm). When the mount dies, the rider fights on foot.
- Wild riders: in groups, at a chance below a captain's, not leading the group.
- Still open (default proposed, not asked): riders earn XP like guards and shock troopers and are the last rank on their path; promoting to rider costs the captain's price.

## Spike, 2026-11-12 (src/MountSpike.cs, `/recruits mountspike`, needs DebugCommands)

Owner, same day: "the goal is to have [the mount] spawn as a part of the unit" and "the behavior should remain the same for commanding the unit": a rider spawns already seated, and takes the same orders (follow, hold, move, guard, formations, fights) as on foot; the mount only carries it.

The command: `spawn [npc code] [mount code]` makes a new NPC and its mount together where you look (default Rohan warrior on `feverstonewilds:horse-tamed-male-quarter`) and seats it; `ai | controls | direct` seat the NPC you look at, or switch its steering; `off` gets it off. Steering modes: `ai` (the goal) turns the mount toward the rider's own walk vector, so its normal tasks drive it; `controls` presses the rider's movement keys and lets the game's EntityBehaviorRideable read them; `direct` sets the mount's gait and yaw ourselves. Once a second each rider logs mode, yaw, gait and walk vector to server-main.log.

Results in game (2026-11-12, 0.0.116 build, Misty Mountains warrior on a Feverstone tame dire wolf):
- **Riding works.** Rider and saddled mount spawn together and the NPC sits in the saddle (upright, seen from the side and front). It **stays mounted after a save, quit and reload** (the game's own mountedOn save).
- **Tack:** the game's `TryGiveItemStack` on a mount throws on the server (EntityBehaviorAttachable maps client selection boxes), and the accessory behavior is named `dressable` on the server. The code puts the saddle and bridle straight into the tack container (the EntityBehaviorContainer that isn't EntityBehaviorHarvestable), at the slot whose `forCategoryCodes` names saddle-/bridle- (wolf and horse: saddle 3, bridle 0). They show on the model.
- **Players only, in vanilla:** `EntityRideableSeat.CanMount` returns false for any EntityAgent that isn't a player (an isinst on the rider), then checks taming generation and the player's ownership (`EntityBehaviorOwnable.IsOwner` also only accepts players). A Harmony prefix (both sides) returns true for lotrnpcs riders; players keep every vanilla check.
- **Steering:** pressing the rider's keys (`controls`) does nothing: the game turns a rider's keys and the mount's gait into motion only on the controlling player's client. Setting the gait and yaw (`direct`) turns the mount but doesn't move it. Setting the mount's own `Controls.WalkVector` on the server (as AI pathing moves any creature) moves it: about 23 blocks in 3 s at 0.06 a tick (sprint), 0.03 walk.
- **Same commands as on foot (`ai`):** a mounted rider's own walk vector stays 0 (the game doesn't move a mounted entity), but its AI keeps running (FactionSeek, ChestMeleeAttack seen while mounted) and its path-follower (`EntityBehaviorTaskAI.PathTraverser`, Active + CurrentTarget) holds where it wants to go. Steering the mount toward that target carried the rider into a fight with a Gondor warrior, and the battle log shows the mounted rider landing 6 melee hits (2.4 to 3.4 damage, about every 2.7 s) before it died. So follow, hold, move, guard, formations and fights can all drive the mount through the rider's normal tasks.
- **Rough edges for the real feature:** the mount's yaw snaps to the target each tick (needs a turn rate); the walk/sprint choice is by distance only; nothing yet turns the mount loose when its rider dies (owner: it should run off) or puts the rider on foot when the mount dies; melee reach from the saddle wasn't tuned; horses, elk and boars not tried yet (same code, other entity codes).

Testing note for whoever drives the game by script: never send Escape. Computer use treats an Escape keypress as the user stopping the turn, so scripts that open the pause menu with Escape cut themselves off.

## Built: the rider rank (0.0.117, 2026-11-12/13)

- `test-<race>-rider` types for rohan, gondor, dale, arnor (Feverstone horses, quarter or shire), uruk, mordororc, gundabadorc, angmarorc (Feverstone tame dire wolves), elf (vanilla `game:tameddeer-elk-*-adult`) and dwarf (`teapotsgiantboar:tame-giantboar-giant-*`). The type attribute `lotrnpcsMounts` lists the codes; `Riders.AvailableMounts` keeps those that exist in the world, so without the mounts' mods no rider rank is offered and none spawn wild (optional dependency, as the owner chose).
- Rank: promoted from warrior, guard or shock next to captain (`Promotion.Targets`), last on its path. Fights with the faction's warrior health and damage and costs the captain's promotion price unless the config lists `rider` (`Promotion.StatRank`, `PriceRank`). Wild groups: one warrior rides at `RiderChance` 0.06 (spawning config).
- `src/Riders.cs`: `EntityBehaviorRider` (server) spawns a saddled mount on first appearance and seats the rider; steers it each 100 ms toward the rider's AI path target (walk 0.017 / run 0.033 a tick, about 4 and 8 blocks a second, turning at 5 rad/s); when the rider dies the mount gallops away from the killer for 8 s and is despawned; when the mount dies or is lost the rider goes on foot for good (`lotrnpcsMountLost`). Tack goes in by category from any mod (`attachableToEntity.categoryCode`). `RiderSystem` patches `EntityRideableSeat.CanMount` for lotrnpcs NPCs. Melee reach from the saddle grows by half a block and the seat height (`Riders.MountedReach`).
- In game (Rohan rider on a Feverstone horse): spawns mounted; with no extra reach it never landed a hit on a Mordor warrior (7 hits taken, 0 given), with the reach it traded hits evenly; a hired warrior promoted to rider through the promotion screen (36 gears, captain card "Captain places full" beside it) came mounted, and on Follow its horse carried it 40 blocks after the owner and stopped beside them. The first speed (0.06) was a 16 blocks/s gallop, now halved.
- Second pass (2026-11-13): riders stay mounted after leaving and rejoining the world (117.5). Two riders side by side never hit each other at first: melee contact is measured from just ahead of the attacker along its facing (AiTaskBaseTargetable.hasDirectContact) and a rider turns only 0.2 rad in the saddle, so with no path the mount now turns in place to face the rider's fight target; after that the Rohan and Gondor riders traded hits (34 -> 26 and 34 -> 11). When a rider died its horse was gone afterwards (ran off and despawned), and a rider whose horse was killed went on foot and got no new one (117.4). A Mordor orc rode a Feverstone dire wolf and an elf the game's elk, both saddled and fighting each other; a dwarf rider without Teapot's Giant Boar stayed on foot with no error (117.6 except the boar).
- 117.7 passed: with RiderChance 1 for the test, a Rohan zone group spawned as "recruit, recruit, rider" and the rider sat on its own shire mare; groups without a warrior got none. Config put back to 0.06.
- Not seen yet: giant boar riders (mod not in this world). 117.8 (no Feverstone) deliberately not tried in the owner's world: loading a save without a mod deletes that mod's creatures in it (the owner's own tamed horses included); the "no mount type, no rider rank" rule is covered by the unit tests instead.

