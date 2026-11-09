---
name: vanilla-weapon-codes
description: Game-domain weapon/shield/bow item codes that exist with Combat Overhaul fork 0.18.9 (read from its assets), for stand-ins after Spear Expansion removal
metadata:
  type: reference
---

Source: /mnt/project-files/combatoverhaulfork_0.18.9.zip, assets/game/itemtypes/combatoverhaul/*.json (CO skips the vanilla files and redefines the same codes; wiki is blocked from the sandbox). Read 2026-11-08.

- Swords: blade-falx-{copper,tinbronze,bismuthbronze,blackbronze,gold,silver,iron,meteoriciron,steel}, blade-blackguard-iron, blade-forlorn-iron, blade-scrap-scrap. So game:blade-falx-iron EXISTS on the CO server (0.0.96 risk "falx code wrong" is cleared for CO servers).
- Ruined (rusty) weapons, metal "ruined": blade-{gladius,arming,claymore,sabre}-ruined, axe-{bearded,battle,bardiche,double}-ruined, club-{flanged,morningstar,spiked,warhammer}-ruined, spear-{boar,voulge,fork,ranseur}-ruined.
- Spears: spear-generic-{chert,granite,andesite,basalt,obsidian,peridotite,flint,copper,tinbronze,bismuthbronze,blackbronze,iron,meteoriciron,steel,ornategold,ornatesilver}, spear-scrap-scrap.
- Clubs: club-generic-wood, club-scrap-scrap, club-scrapmace-scrap.
- Bows: bow-{crude,simple,long,recurve}.
- Shields (vanilla tool/shield, CO patches them): shield-crude-*, shield-woodmetal-*, shield-woodmetalleather-*, shield-metal-*, shield-blackguard-* (variant suffixes not read yet).
- Not seen in CO files but vanilla tools: knife-generic-<metal>, axe-felling-<metal>, hammer-<metal> (unverified codes).
- Not yet checked: whether these items exist identically on a server WITHOUT CO (vanilla 1.22 should have the same codes; unconfirmed).

Related: [[lotrnpcs-base-0097]], [[combat-overhaul-numbers]]
