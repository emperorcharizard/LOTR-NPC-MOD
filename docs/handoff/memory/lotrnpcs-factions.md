---
name: lotrnpcs-factions
description: How the lotrnpcs faction system (0.0.31-0.0.35) decides who NPCs attack: 17 factions, config file, admin-set player factions, hire prices, migration
metadata:
  type: project
  modified: 2026-11-02T20:50:58.422Z
---

src/Factions.cs, built 0.0.31-0.0.35 (2026-11-02) on the user's word. See [[npc-mod-maker-goals]].

- 17 factions (user, 0.0.35): Men gondor, rohan, dale, arnor, haradrim, dunlendings, blacknumenoreans, easterlings; dwarves (Longbeards+Firebeards), wickeddwarves; shire (all hobbit sub-races); elves (Noldor+Sindar+Avari, one armor set), tormentedelves; mordor, angmar, mistymountains, isengard. NPC attribute `lotrnpcsFaction`; ranked NPCs have `lotrnpcsRank` (test-hobbit = shire recruit since 0.0.36, user "make them recruit equivalent"; rankless test-goblin/test-uruk have none); /lotrzone accepts all 17 factions, Shire groups are all hobbits (missing ranks spawn as recruits).
- ModConfig/lotrnpcs-factions.json: "#Instructions" (rewritten each load), Version (4 = 0.0.35), per faction Stance, FriendlyRaces (PML skinModel codes), FriendlyGroups, HireCoin (default coinage:coin-silver-lg), HirePrices (recruit 5, warrior 15, captain 40; missing ranks filled); Relations {A,B,ally/enemy/neutral,ShareFriends}; MonsterCodes; GrudgeSeconds 30; CallForHelpRange 16. Collections null until FillDefaults (Newtonsoft appends otherwise). lotrnpcs-spawning.json also has "#Instructions".
- Stances: all aggressive except Shire (defensive). Factions friendly ONLY via Relations; no entry = enemies. Default Relations: no-op {gondor,rohan,enemy} + real {mordor,isengard,ally}. Card "ship Free Peoples allied?" unanswered; built with my "no".
- Membership: race in FriendlyRaces, or group in FriendlyGroups, or admin-set faction (/lotrfaction set <player> <faction>, clear, members; saved in savegame key lotrnpcs:playerfactions). Admin-set faction REPLACES race; groups still count. ShareFriends on an alliance extends membership both ways.
- Orc players join only via groups (user). MY call in 0.0.35, told to user: gondor/rohan/dale/arnor list no races because one LRC Men of the West race (lrc:freepeople) fits all four; those players join by group or /lotrfaction set. Other default races (my guesses): haradrim lrc:haradrim; dunlendings lrc:wildmen; blacknumenoreans lrc:bnumenorean; easterlings lrc:easterling faraces:easterling; dwarves lrc:longbeard lrc:firebeardsandbroadbeams faraces:dwarf; wickeddwarves lrc:wickeddwarf; shire lrc:harfoot/stoor/fallohide faraces:hobbit; elves lrc:noldor/sindar/avari faraces:elf; tormentedelves lrc:tormentedelf.
- Migrate (once, then stores): v0->1 defensive gondor/rohan/dwarves/dunlendings -> aggressive; v1->2 drop orc races from mordor, swap gondor-rohan ally example; v2->3 add mordor-isengard ally unless pair exists; v3->4 add only the 7 new 0.0.35 factions, strip lrc:freepeople/faraces:numenorean/faraces:northman from gondor+rohan, fill hire prices.
- Rules: factions attack enemy NPCs and monsters on sight; neutral leaves NPCs alone; anyone who hurts an NPC gets a 30 s grudge from it and allies; allies within 16 blocks help. Creative/spectator never targeted.
- AI: `lotrnpcs:seekentity` (AiTaskFactionSeek) and `lotrnpcs:meleeattack` override IsTargetableEntity*; behavior `lotrnpcs:factionmember`.
- /lotrfaction check|list|reload|set|clear|members (controlserver). Hiring itself not built.
- Tests: tools/tests/factions 95 checks, spawnzones 49 (0.0.36). Fights between factions not yet confirmed in game.

See also [[lotrnpcs-titles-rivals]] (0.0.56).
