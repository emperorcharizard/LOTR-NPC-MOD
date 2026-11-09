---
name: lotrnpcs-base-0097
description: 2026-11-07 handoff back from the second account: lotrnpcs 0.0.97 base location, hash, what changed 0.0.73-0.0.97, open questions
metadata:
  type: project
  modified: 2026-11-07T23:58:54.968Z
---

- Base (2026-11-07): /mnt/project-files/lotrnpcs/0.0.97/lotrnpcs-handoff.zip (+ lotrnpcs_0.0.97-test.zip, byte-identical to the bundle's dist). Supersedes the 0.0.72 base ([[lotrnpcs-0065-0071]]). Bundle's HANDOFF.md section 3 Summary + section 9 and THREADS.md round log are the authoritative state; its memory/ folder has more notes (lotrnpcs-0077.md etc).
- Source hash f2e42b1c3589cb26 (THREADS.md method) matched the round log. Rebuilt 2026-11-08 (user OK'd running the build).
- Build inputs now also: haradrim-armor 0.1.3 (mod id haradrimarmor, owner's), gundabad-bone-armor 0.3.0 (mod id gundabad, owner should run 0.3.0).
- 0.0.73-0.0.97 highlights (all untested in game unless noted): bow string pull poses; server anims need base shape lotrnpcs:entity/humanoid/seraph-faceless (0.0.77); follow-and-fight, crowding fallback; Haradrim armor mod (0.0.78-0.0.81); faction loadouts (0.0.82); targeting limit removed (0.0.84); inner/outer battle rings (0.0.87); edge guard over pits (0.0.88); one command root /recruits (0.0.89); factions "unequally equal" tiers Men/Uruk/Levy/Orc/Goblin/Elf/Dwarf, config v19 (0.0.90); per-rank balance, lifetime despawn (0.0.91); players pick faction, join menu as cards, promotion = hire price -3 (captain -4) (0.0.92-0.0.93); hire coin = rusty gears for now (0.0.94); Gundabad helmet in owner's gundabad mod, dwarf hammers, two-handed captain greatsword (0.0.95); Not Only Spear Expansion dependency removed, falx fallback, shoulder carry (0.0.96); owner's Haradrim sword, curved sword, light (recruit) and heavy (warrior/captain) shields (0.0.97). Factions test 522, zones pass (per handoff).
- Next open question per HANDOFF section 9: in-game test of 0.0.97 together with 0.0.96 (checklist sections 96/97). Biggest risk: falx code wrong -> ten falx-only factions unarmed.
- 0.0.98-0.0.102 work and 2026-11-09 in-game reports: see [[lotrnpcs-0098-0102]].
