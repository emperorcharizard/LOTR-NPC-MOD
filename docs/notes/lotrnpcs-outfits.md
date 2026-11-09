---
name: lotrnpcs-outfits
description: Outfits the user picked for Haradrim, Easterlings and tormented elves (0.0.41), the "@slot" gear syntax, and where vanilla item codes/names come from
metadata:
  type: project
  modified: 2026-11-03T03:32:37.897Z
---

User's picks (2026-11-03, built in lotrnpcs 0.0.41, FACTION_SETS in tools/gen_testnpcs.py). Replaces the 0.0.38/0.0.39 borrowed and mixed outfits.
- Tormented elves: Angmar Ranger tunic + leggings (angmar mod, tiertwogambeson), NO helmets; captain adds the vanilla Rotten king crown (game:clothes-head-rottenking).
- Easterlings: recruit tin bronze brigandine set, warrior tin bronze plate set, captain tin bronze plate body/legs + Woodland Sentinel helmet (wlel ...woodlandhelmetmedium...) + iron chain coif "covering his face" (game:armor-head-chain-iron@face).
- Haradrim: every rank alike (user named no rank split; I told them). Nadiyan guard boots, Nadiyan guard OR hunter pants (user said "hunter pants"; I chose Nadiyan hunter pants), any Nadiyan cape (hunter, peasant1-3, shepherd), black OR red tailored gambeson body+legs, Forgotten noble robe (upperbodyover), sturdy leather belt, fur wrap (clothes-shoulder-furwrap, worn @neck because the cape has the shoulder slot), miner mask (clothes-face-miner). 20 combinations.
Other factions' armor: [[npc-mod-maker-goals]] (orcs) and gen_testnpcs.py comments.

"@slot" syntax (0.0.41): a startinggear code may end in @<EnumCharacterDressType> (face, neck...) to go straight into that gear slot if free (GearCode in LotrNpcsModSystem.cs), so two pieces of the same slot can be worn. Vanilla gear renderer draws every slot.

Vanilla clothing English names: VS wiki table at raw.githubusercontent.com/BlackberryMuffin/vs-wiki-table-generator (scripts/clothes/re_input/table.txt); saved as tools/tests/factions/vanilla-clothes-codes.txt (code<TAB>name), armor codes in vanilla-armor-codes.txt. Tests check every game: code against them. User's game is 1.22.7.

Open: user hasn't seen 0.0.41 in game (clipping of two head pieces / cape + fur wrap untested).
