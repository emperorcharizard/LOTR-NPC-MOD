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
