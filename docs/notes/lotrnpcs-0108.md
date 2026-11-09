---
name: lotrnpcs-0108
description: lotrnpcs 0.0.108 (2026-11-09): melee 0.25 s faster, stats refit, per-rank hire prices fitted for equal coin, config v28
metadata:
  type: project
  modified: 2026-11-09T19:50:07.687Z
---

- 0.0.108 (user 2026-11-09: "increase attack frequency up a tick or two. and balance combat for equal coin spendage"; then "give as well costs per group to reach their max cap of followers"). /mnt/project-files/lotrnpcs/0.0.108/ (zip, cumulative patch vs 0.0.97, balance-results). Factions 551 PASS, zones pass. Untested in game.
- Melee task cooldown 1700-2100 (was 1950-2350) on every melee NPC; cycle with the 0.9 s swing = 2.6-3.0 s. The 0.0.105/0.0.107 sims wrongly used 1.95-2.35 as the whole cycle; sim105.py MELEE_CD fixed to (2.6, 3.0).
- Stats refit (fit105.py) R(rec,war,cap,arch): Men 22/34/48/35 3.15/3.95/7/3.05; Uruk 23/32/48/36 3/4.3/7.1/3; Levy 18/28/41/28 2.85/3.6/6.1/2.8; Orc 19/30/40/30 2.7/3.35/6.2/2.65; Goblin 17/22/32/22 1.85/2.8/4.7/2.2; Elf 27/37/51/36 3.7/5.3/9.7/4.3; Dwarf 31/47/63/47 3.2/4.15/7.8/3.3.
- Prices: a single multiplier on Men's prices (fitprice.py) was only fair for one spending style (orcs 0.19-0.93 across styles). Per-rank whole-coin fit (fitmix.py, objective over 6 spending styles, budgets 50-250, roster + captain limits): Men 5/15/40/15, Uruk 4/15/41/16, Levy 4/12/35/11, Orc 5/12/29/12, Goblin 3/9/25/10, Elf 6/19/50/20, Dwarf 6/19/50/19. Mixed styles 0.42-0.56; recruit-only buys still skew (Uruk 0.21, Orc 0.74) from roster sizes. Rounding nudges (e.g. orc recruit back to 4) break it (orcs 0.64-0.75): keep fitted values. Uruk-hai lost their 1.1x premium.
- Full following cost (followers x price, rec/war/cap/arch): Men 25/75/200/75, Uruk 20/75/205/80, Levy 24/72/210/66, Orc 30/72/174/72, Goblin 24/72/200/80, Elf 24/76/200/80, Dwarf 24/76/200/76 (captains capped by RosterPerCaptain).
- Config v28: FactionTiers.Tables107 + Hire107; unchanged tables/hire prices move, promotion prices still derived from old hire follow. Runs before the v27 step.
- 0.0.107 logs (user upload 2026-11-09): no lotrnpcs errors; only known seraph-faceless 'Eyes' warning; Not_OnlySpear_Expantion still installed (patches fail harmlessly).
- 2026-11-09 design review doc + the user's answers: see [[lotrnpcs-review-answers]].
