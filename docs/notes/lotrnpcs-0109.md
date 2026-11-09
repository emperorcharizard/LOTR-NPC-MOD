---
name: lotrnpcs-0109
description: lotrnpcs 0.0.109 (2026-11-09): battle log (/recruits battlelog) and NpcHitsIgnoreHurtWindow switch, built for the in-game test pass
metadata:
  type: project
---

- Built 2026-11-09 from the git repo (commit 67c0cea, emperorcharizard/LOTR-NPC-MOD). Zip + test-checklist copy in /mnt/project-files/lotrnpcs/0.0.109/. Factions 554 PASS, zones pass. Untested in game. Feature freeze otherwise until the in-game pass ([[lotrnpcs-review-answers]]).
- src/BattleLog.cs: FactionSystem.Log, runtime only. /recruits battlelog on|off|(totals), controlserver. Counts per faction: melee/arrows landed, lost to hurt window (target alive + "invulnerable" activity and no IgnoreInvFrames), lost other; damage; kills; deaths (FactionMember.OnEntityDeath); turn waits; idle NPC-seconds (Fighting() and no swing/shot for 5 s, ticked once a second; includes NPCs waiting in a line's rear slot). CSV in GetOrCreateDataPath("Logs")/lotrnpcs-battle-<time>.csv, flushed every 5 s and on shutdown.
- Config NpcHitsIgnoreHurtWindow (default false, no version bump): melee and arrow hits on non-players set IgnoreInvFrames and melee skips hit-turn waits for such targets. Players keep window + turns.
- AiTaskChestMeleeAttack.attackTarget now always does vanilla's hit itself (DamageSource or CO chest source for players; IMeleeAttackListener; saturated emotion) to read ReceiveDamage's result.
- docs/test-checklist.md got sections 98-109 (the list for the pass; 109.1 = 100 orcs v 50 Haradrim with log, then again with the switch on).
