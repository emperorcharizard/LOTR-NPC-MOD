---
name: lotrnpcs-looks
description: lotrnpcs 0.0.54 plan - random helmets, owner menu to change a hireling's name/helmet/weapon/face, race names on hire
metadata:
  type: project
---

User 2026-11-03: "randomize whether the npcs are wearing helmets or not, and add a function in their specific menu to customize how they look (whether helmet on or off, and facial features) and what their name is. In addition, when hired have their name be randomized anyway to names befitting the race." plus "and also what weapons they hold to customize that."

Script: /mnt/project-files/lotrnpcs/prompts/looks-and-names.md. BUILT 0.0.54 (2026-11-03, untested in game): src/Looks.cs (Apply validates owner/name/part/weapon/helmet, all-or-nothing) + src/LooksDialog.cs (client, every click sent at once as entity packet 46306 LooksRequest JSON; Cancel resends the opening snapshot). Config v11 HelmetChance/CaptainsAlwaysHelmeted. Kept helmet = watched itemstack attr lotrnpcsKeptHelmet; head slot EnumCharacterDressType.ArmorHead. Weapon "none" persists (PickWeapon keeps it). Names: gen NAMES/HOBBIT_NAMES/HOBBIT_SURNAMES -> entity attrs lotrnpcsNames/lotrnpcsSurnames; lotrnpcsNamed bool stops renaming; infotext then shows the type name.
My defaults (told the user, awaiting objection): HelmetChance 0.6, captains always helmeted (CaptainsAlwaysHelmeted), owner-only "Let me change how you look." option, weapon swap only among the type's own weapon list (Mordor orcs today) plus None, name tag shows only the name after hire with faction/rank in infotext. Unworn helmets kept with the NPC, never dropped. Names: 40+ per people, book style, no canon character names.

PML 1.24.0 facts (decompiled with ilspycmd): skin in WatchedAttributes skinConfig/appliedParts/<part>=variant; client PML only re-tesselates when the MODEL changes, so our hireable client listener calls MarkShapeModified on skinConfig changes. Face parts read from our bundled assets/lotrnpcs/config/customplayermodels/<race>.json (category standard only).

To do (owner, 2026-11-12: "the misty mountains orc warriors should also get the moria orc helmet option"): add norc("head", "tierthreechain", "norchelmetmedium") (in game "Moria Orc Helmet") to the goblin warrior heads in tools/gen_testnpcs.py FACTION_SETS (now GUNDABAD_HEAD and MORD_WARRIOR_HEAD only; recruits already have it). Guards and shock troopers take the warrior's sets, so check they get it too. Not built yet.

Related: [[lotrnpcs-hiring]], [[lotrnpcs-weapons]], [[lotrnpcs-outfits]]

0.0.55 (BUILT 2026-11-03, untested in game; prompt /mnt/project-files/lotrnpcs/prompts/hobbits-archers-orcs.md): hobbits wear "clothes similar to vanilla traders, minus any helmets" plus "a hat on rare occasion" -> 12 vanilla wearable sets, barefoot, hat 0.125 from 6 vanilla hats (startinggear "hats", clothes head slot, not the helmet toggle). Shared orc helmets: Mordor/Angmar/MM share raider/soldier/warrior helmets; Moria/Gundabad MM-only; captains and Isengard unchanged. Mordor orc shield + archers: see [[lotrnpcs-archery]].
Mordor orc "FA-like" look (user 2026-11-03: "FA has the tusks. lotr chars does not."): the tusks were LRC's UNUSED orc_angry/orc_determined faces (elements "orkteeth") that bundle_lrc.py picked. 0.0.57 uses LRC seraphfaces angry/serious/determined/fierce (codes kept, old orcs lose tusks); orc face shapes removed from the bundle. Goblin faces have no tusks. Mordor skins stay orcskin 2/3/8 (darker than LRC default grey-blue) unless the user asks.

Related: [[lotrnpcs-hiring]], [[lotrnpcs-weapons]], [[lotrnpcs-outfits]]
