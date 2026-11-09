---
name: npc-ai-tuning
description: lotrnpcs orc AI movement speed, attack cadence and awareness (seek range, scan rate) values and the vanilla code facts behind them
metadata:
  type: project
  modified: 2026-11-03T06:50:00.000Z
---

All set in tools/gen_testnpcs.py AI_TASKS (shared by every test NPC). Vanilla code facts from vsessentialsmod AiTaskSeekEntity / AiTaskBaseTargetable / AiTaskMeleeAttack.

- Speed (user 2026-10-31: orcs 'match my running speed'): seekentity movespeed was 0.04; player WalkVector ~0.025 walk, ~0.05 sprint (1.5*dt*sprint 2.0), both scaled by the same ground physics. 0.0.20 (untested): movespeed 0.03 (~1.2x walk, ~0.6x sprint), sprint anim speed 0.45. 0.0.21 (user: 'increase their awareness'): seekingRange 16->24, searchWaitMs 4000->1000 (vanilla seek scans at most every searchWaitMs; sneaking x0.6 range; no line-of-sight check). alarmHerd needs HerdId>0, unset on creative spawns: future camp spawner should set it.
- Melee cadence: attackDurationMs 900, damagePlayerAtMs 400, cooldown set in FinishExecute (confirmed honored). 0.0.18-0.0.44: cooldown 2200-2600 ms (~3.3 s per swing; user: groups hit too hard). 0.0.45 (user 2026-11-03: 'increase how often they attack by .25 s'): 1950-2350 ms, every NPC, ~2.85-3.25 s per swing. Weapon swing anims are timed to DAMAGE_AT_MS: [[lotrnpcs-weapons]].
- Seek task: ShouldExecute passes ExecutionChance 0.1 and a 50% roll, then scans at most every searchWaitMs (default 4000). Retaliation widens range x1.5. maxFollowTime default 60 s (unchanged).

Related: [[combat-overhaul-numbers]], [[lotrnpcs-build]], [[lotrnpcs-weapons]]

0.0.53 battle spacing (src/Battle.cs, BattleSpacing on FactionSystem.Spacing; config v10 MaxAttackersPerTarget 3, CombatSpacing 1.5 clamp 0.8-4; user raised default to 2.0 in 0.0.55, config v12 migrates only an unchanged 1.5): claims target->attackers (6 s expiry, renewed by seek every 0.5 s and melee swings); Crowded() only for non-player targets, skipped in both tasks' IsTargetableEntity and in seekEntity Notify (call for help). 1.22.7 retaliation uses ShouldRetaliateForRange (doesn't call IsTargetableEntity) so striking back bypasses the cap. Surround: NPC targets only (players vanilla approach); angles evenly spread in bearing order, radius min(spacing, 2.2); seek finishes at spot (0.65) and overrides pathTraverser.CurrentTarget after base ContinueExecute. Push in EntityBehaviorFactionMember (fighting = MarkFighting within 3 s): partition walk every 0.25 s, up to 1.2 b/s, ledge check CanStepTo. Real 1.22.7 vanilla sources: vsessentialsmod master commit 06673b6 (Entity/AI/Tasks/), vsapi 63d33f7; the "stable" branch is 1.20.11. Untested in game.
