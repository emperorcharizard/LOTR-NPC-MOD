---
name: lotrnpcs-review-answers
description: 2026-11-09 design review of lotrnpcs (doc link) and the user's answers to its questions, incl. what to build after the in-game test
metadata:
  type: project
---

Doc: "LOTR NPCs mod: design review" https://claude.ai/artifact/XURXPMgYM266SZTsm4AamD (per-area tables: question / for / against / recommendation / answer column, which I fill as the user answers). User asked 2026-11-09 to be asked the questions in chat; asked one card at a time.

Answers (2026-11-09):
- Q1 FEATURE FREEZE until an in-game test of 0.0.98-0.0.108 (yes, test first). I only fix what the test finds.
- Q2 Battle log: BUILT in 0.0.109 ([[lotrnpcs-0109]]).
- Q3 Hurt-window switch: BUILT in 0.0.109 as NpcHitsIgnoreHurtWindow (default off). Mainly helps archers/feel, likely not the orc-vs-Haradrim cause.
- Q4 Hire coin: keep rusty gears LONG TERM; HireCoin per faction stays admin-configurable.
- Q5 No upkeep. Q6 Death stays permanent (re-hire / captain survival declined).
- Q7 Blocs NOT allied by default (all enemies; only mordor-isengard ships allied).
- Q8 Config no-guard radius around world spawn: guards never attack players on sight there, still fight back. After test.
- Q9 Faction switching: first pick free, then admin only (/lotrfaction set). After test.
- Q10 Drops: wild NPCs leave a vanilla drifter-style harvestable body (knife): rusty gears recruit 0-1, warrior 1-2, captain 3-5, plus a little cloth/leather (orcs maybe bones), no armor/weapons, rots normally. User: HIRED NPCs also leave a body, loot configurable separately, defaults same as wild. All configurable. After test.
- Q11 Zone patrol flag = YES (admin gives a zone 2+ points, groups walk between them). After test.
- Q12 Rival fights: NO cap.
- Q13 Captains in BATTLE LINES = held back as reserve (behind front rank, join at contact or when front thins). After test. (Follow formations keep captains front-middle, 0.0.106.)
- Q14 Git: DONE 2026-11-09. Repo emperorcharizard/LOTR-NPC-MOD (private, saved to project). First commit f31b35a = 0.0.108 source (lotrnpcs dir + docs + docs/handoff; no dist zips, no build-inputs). Clone to /home/user/lotr-npc-mod and build from it from now on; commit each build. (Older repo emperorcharizard/Vintage-Story-NPC-LOTR exists, from 2026-10-30, unused.)
- Q15 Config: drop migrations older than v19 (reset to defaults) once both servers run 0.0.108.
- Not asked (folded into the test or user actions): 5-systems check, archers, Not Only Spear removal, PML upstream report, tick-time measure, dwarf chop gap, game shields/falx.
- Later (user, 2026-11-09, after the army-games gap review): cavalry is wanted, but later (not in the next builds). Faction standing and invasions are rejected (user, 2026-11-10): don't build or propose them. Also rejected: nemesis captains, surrender-and-recruit, morale/routing (user thinks it wouldn't work well). Wanted: roster screen (list of hired NPCs with rank, XP, order, location) and branching promotion (pick a path at promotion). Maybe: contracts (faction NPCs give goals).
