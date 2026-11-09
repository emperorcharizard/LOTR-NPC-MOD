# Prompt: lotrnpcs 0.0.62, Gundabad faction (work in progress); weapons stay bundled

The owner's words (2026-11-03), with `gundabad-bone-armor-0.1.0.zip`: *"the bows are now held, and shot correctly. I am going to attach a series of files for updated models. Use these as items to call upon rather than implement directly into the mod. remove all the shape files and assets for the current weapons. This is an effort to save space, let me know if its not."* Earlier: *"I added a mod called gundabadarmor_0.1.0. call on it for a gundabad faction for a work in progress."*

**Confirmed in game (0.0.61):** archers hold the bow level at rest (option C), draw and shoot correctly.

## Weapons: not moved (owner's choice after the numbers)

Measured in the 0.0.61 zip (1.95 MB): race skins and faces 1,475 KB (76%), race shapes 170 KB, entities/lang/config 142 KB, DLL 89 KB, **all weapons, shield, bow and arrow 76 KB (4%)**. Moving them to another mod saves little, adds a required mod, and the NPC grips would still need our own item entries. Asked; the owner chose **keep them bundled**. Their coming models replace the current ones in `tools/weapons/` (like the pending bow in `tools/weapons/pending/`).

## Gundabad faction (work in progress)

**The mod:** mod id `gundabad`, "Gundabad Bone Armor" 0.1.0, content only. Two items, no helmet:
- `gundabad:armor-body-tierthreebrig-gundabadbonearmor-gundabadbonearmor-none`: Gundabad Bone Chestplate (with gauntlets)
- `gundabad:armor-legs-tierthreebrig-gundabadboneleggings-gundabadboneleggings-none`: Gundabad Bone Leggings (with boots)

Its shapes step-parent only to standard seraph elements (UpperTorso, LowerTorso, arms, feet), all present on our NPC body, so it renders (unlike the Angmar step-parent warnings).

**Owner's decisions (asked 2026-11-03):** Mordor orc body at 0.9 size (`lotrnpcs:mordororc`, Mordor skins and faces); **no helmets**. Recommended defaults built: a new 18th faction `gundabad`, aggressive, joined by group or `/lotrfaction set` only (like every orc faction), hostile to every faction until the config allies it, the planchet at 5/15/40 like everyone else, no weapons, shield or archers (those are Mordor's).

**Build:**
- Entity types `test-gundabadorc-recruit`, `-warrior`, `-captain`, faction `gundabad`, all three ranks in the bone set (the mod has only one). Without the mod: the vanilla outfit with its helmets removed. Rank stats as for every orc. Names from the orc list. Talk lines of their own. Portrait scowls.
- Creative names end in "(WIP)" until the owner calls it done.
- `Factions.cs`: default faction, config **version 14** adds `gundabad` once if missing (as 0.0.35 added factions). `SpawnZones.Factions` lists it so `/lotrzone` accepts it.
- Tests: 18 default factions, the migration adds Gundabad and keeps the rest, the three types wear only the two bone pieces and never a helmet, the zone list includes it.
