# Prompt: lotrnpcs 0.0.53, battles spread out instead of clumping

You are working on **lotrnpcs**, a Vintage Story 1.22.7 code mod that adds Lord of the Rings faction NPCs to a LOTR-themed server. The source lives in `/mnt/project-files/lotrnpcs/` (`src/*.cs`, `tools/gen_testnpcs.py`, `tools/tests/`). The last release is 0.0.52.

The owner's request, in their words (2026-11-03, with a screenshot of a dozen orcs and a red captain stacked on one spot): *"in combat I have noticed all the npcs clump all together in one spot. maybe put a minimum distance npcs have to one another when fighting each of the other factions ... I want it to feel more like an actual battle, rather than all npcs clumping together."* Ship it as **0.0.53**.

## Why they clump today

- Every NPC runs vanilla's seek task, which picks the **nearest** valid target. In a fight between two groups, most NPCs pick the same one or two enemies.
- Seek walks straight to the target's position, so everyone chasing one enemy ends up on the same block.
- Nothing pushes allies apart except the game's soft entity collision, which lets them overlap.

## Ground rules

1. **Don't break existing worlds.** Keep entity codes `test-*`, save keys, packet ids, watched attributes and config files. New settings get defaults, and the config version goes up by one with a migration that only adds them.
2. **Keep every decision the owner already made.** Damage, health, attack timing, regen, stances, orders, formations (guards in a formation stand still; only Loose patrols), spawning and faction relations stay as they are.
3. **Players aren't made easier or harder.** The attacker limit applies to NPCs fighting NPCs. How many NPCs may go for a player stays unlimited.
4. Build against `tools/refs` (no game install). Both test suites pass, and every new behavior gets a test.
5. Hot paths stay cheap (see the performance notes): no LINQ or allocation in per-tick code, and no scans of every loaded entity per NPC per tick.
6. Player-facing text is plain English in full sentences, with no em-dashes.

## Part 1: Spread out the targets

- Keep a small registry in the faction system: for each NPC being fought, which NPCs are chasing or hitting it right now. The seek and melee tasks add themselves when they start on a target and remove themselves when they finish, die or unload.
- When an NPC looks for a target, an enemy **NPC** that already has `MaxAttackersPerTarget` other attackers (default 3) doesn't count, so the search moves on to the next nearest enemy. A target the NPC is already fighting always stays valid, and so does anyone who just hurt it (striking back ignores the limit).
- If every enemy in reach is taken, the NPC doesn't pick one. It keeps its current order (hirelings follow, hold or guard; wild NPCs stand or wander) and looks again on the next search, so it joins in when a place frees up.
- `MaxAttackersPerTarget` 0 turns the limit off.

## Part 2: Surround instead of stack

- Each attacker on a target gets its own side of it: attacker number i of n approaches a point `CombatSpacing` blocks from the target, at angle 2πi/n around it, starting from the side facing the attacker. It still faces and hits the target as before, since melee reach is about 1.5 to 2 blocks.
- Keep the spot fresh as the target moves (it follows the target) but don't reshuffle every tick.

## Part 3: Keep a minimum distance

- While seeking or fighting, an NPC that stands closer than `CombatSpacing` (default 1.5 blocks) to another NPC of any faction gets a small sideways push away from it each tick, strong enough to separate them over about a second and weak enough not to jitter or knock anyone off a ledge. Only NPCs within a few blocks are checked, through a cheap nearby lookup, at most a few times a second per NPC.
- The push never applies to NPCs that aren't fighting (formations and followers keep their own spacing rules).

## Part 4: Config, verify, deliver

1. Add `MaxAttackersPerTarget` (3) and `CombatSpacing` (1.5, kept between 0.8 and 4) to `lotrnpcs-factions.json`, with `#Instructions` lines, a config version bump and migration.
2. Tests: the registry adds and removes attackers, the limit skips crowded NPC targets but never players or the current target or a retaliation, 0 turns it off, the surround points are spaced and the push separates two overlapping NPCs without moving distant ones, and the config migration.
3. Build, run both suites, package `lotrnpcs_0.0.53-test.zip`, replace the old zip in `dist/`, sync the sources.
4. Update the config guide (https://claude.ai/artifact/HPy9dzDE9pfcGVSyhhzRrn) and the project notes.
5. Report briefly what changed and that it hasn't been tested in game. Ask the owner to stage a fight between two factions and send a screenshot.
