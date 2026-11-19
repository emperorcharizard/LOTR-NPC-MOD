# LOTR NPCs: how it works and how to set it up

Version 1.0.5 · November 2026

This is the short version. Every setting is listed in `docs/config-guide.md`.

## 1. What the mod is

LOTR NPCs adds soldiers from 17 Middle-earth factions to Vintage Story. They spawn in zones that admins mark, fight each other and players by faction, and can be hired, given orders and promoted.

It needs LOTR Races and Classes (the race models) and Player Model Lib. Armor mods (Gondor, Mordor, Angmar and others) and mount mods are optional. Without them, NPCs wear plain vanilla armor, and that faction has no riders.

## 2. How the code is laid out

The mod is a C# DLL plus generated JSON files. You don't need to read the code to run a server, but this is what each part does.

| Part | What it does | Main files |
| --- | --- | --- |
| Factions | Who is in which faction, who fights whom | `FactionSystem.cs`, `FactionSystem.Sides.cs`, `FactionConfig.cs` |
| Faction list | Reads the factions from faction files at start | `FactionRegistry.cs` |
| Faction packs | Makes NPC types for factions that add-on mods define | `FactionPacks.cs` |
| Spawning | Groups spawning in admin zones, patrols | `SpawnZones.cs`, `ZonePatrol.cs` |
| Hiring | Hiring, talk menu, orders, followers, guards | `HiringSystem*.cs`, `Orders.cs`, `TalkDialog.cs`, `Roster.cs` |
| Ranks | XP, promotion, captains and riders | `Promotion.cs`, `Riders.cs`, `FactionTiers.cs` |
| Fighting | Targeting, battle lines, archers | `FactionSeek.cs`, `Battle.cs`, `BattleLines.cs`, `Archery.cs` |
| Looks | Faces, helmets, outfits, weapons, portraits | `Looks.cs`, `Weapons.cs`, `Portrait.cs` |
| Joining | The join menu for new players | `Joining.cs` |

The NPC types themselves (stats, AI, looks, outfits) are JSON files made by `python tools/build.py` from the data in `tools/data`:

| Data file | What it holds |
| --- | --- |
| `factions.json` | Each faction's id, side, stance, balance tier and member races |
| `races.json` | Each race's model and the looks it may have |
| `armor.json` | What each race wears, by rank |

Change those and run the build to make a new zip.

## 3. The config files

The server makes these in its `ModConfig` folder on first start. Each starts with an `#Instructions` list explaining it.

| File | What it controls | After editing |
| --- | --- | --- |
| `lotrnpcs-factions.json` | Enemies and allies, prices, limits, health and damage per rank, battle behaviour | `/recruits faction reload` |
| `lotrnpcs-spawning.json` | How often and how big zone groups are | Restart the server |
| `lotrnpcs-client.json` (each player) | Animation detail for far NPCs | `.npcanimlod` in game |

The most common edits:

- **Make two factions allies or enemies.** Move the name between the `Enemies` and `Allies` lines of either faction, then `/recruits faction reload`. The mod updates the other faction for you.
- **Change prices.** `HirePrices` and `PromotePrices` per faction. 0 makes it free.
- **Change band size.** `MaxFollowers` (3) and `MaxRecruits` (16) per faction.
- **Make NPCs stronger or weaker.** `Health` and `Damage` per rank, per faction.
- **Turn on testing commands.** `"DebugCommands": true`.

## 4. Adding a faction (no rebuild)

An admin can add a whole faction with a small content mod, called a faction pack. The pack copies a shipped faction's soldiers (their AI, ranks and weapons) and gives them a new name, race model, looks and armor.

A pack is a folder or zip with three files:

```
modinfo.json
assets/<pack id>/config/lotrnpcs-factions/<faction>.json
assets/<pack id>/lang/en.json
```

The faction file:

```json
{
  "id": "lindon",
  "side": "free",
  "stance": "aggressive",
  "tier": "elf",
  "friendlyRaces": ["lrc:noldor"],
  "template": "woodelves",
  "model": "lrc:noldor",
  "parts": { "haircolor": ["eerieblack", "silverchalice"] },
  "names": ["Erestor", "Gildor", "Lindir"],
  "armor": {
    "recruit": [["game:armor-head-brigandine-steel", "game:armor-body-brigandine-steel", "game:armor-legs-brigandine-steel"]],
    "warrior": [["game:armor-head-plate-steel", "game:armor-body-plate-steel", "game:armor-legs-plate-steel"]],
    "captain": [["game:armor-head-plate-gold", "game:armor-body-plate-gold", "game:armor-legs-plate-gold"]]
  },
  "mounts": ["feverstonewilds:horse-tamed-male-quarter"]
}
```

| Field | What it means |
| --- | --- |
| `id` | The new faction's id: letters, digits or underscores |
| `side` | `free`, `shadow` or `none`: its default allies, and where the join menu lists it |
| `stance` | `aggressive` or `defensive` |
| `tier` | Price ladder: `men`, `uruk`, `levy`, `orc`, `goblin`, `elf` or `dwarf` |
| `template` | The shipped faction whose soldiers it copies |
| `model` | Any race model from an installed mod |
| `parts` | Allowed looks (hair colour, skin and so on) |
| `names` | Names its NPCs get when hired |
| `armor` | One or more outfits per rank. Riders wear the warriors', archers the recruits' |
| `mounts` | What its riders ride |

Anything left out comes from the template. The lang file names the units: `"lotrnpcs:item-creature-test-lindon-warrior": "Lindon warrior"`, and the same for `entity-` and `prefixandcreature-`, plus `"lotrnpcs:folk-lindon": "Elves of Lindon"`.

When the server starts, the new faction appears in the join menu, the creative tab and `/recruits zone`. It is added to your `lotrnpcs-factions.json` once, allied to its side. A full example ships as `lindonnpcs_0.1.0.zip`.

**What a pack can't do.** It can't make a new race model or new armor: those come from their own mods, which the pack points to. Our swing animations exist only on LOTR Races and Classes bodies, so a race from another mod may swing with vanilla animations.

## 5. Civil wars

For a civil war, such as Gondor against Gondor, make the rebels a faction of their own:

1. Add a faction pack with a faction file like this one, so the rebels have soldiers to hire and spawn. They copy Gondor's.

```json
{ "id": "gondorrebels", "side": "none", "tier": "men", "template": "gondor" }
```

2. Name its units in the pack's lang file ("Gondor rebel warrior" and so on).
3. Restart the server. `gondorrebels` is added to `lotrnpcs-factions.json`. A faction on no side is an enemy of everyone; move names to its `Allies` line if it should have friends, then `/recruits faction reload`.
4. Move each rebel player with `/recruits faction set <player> gondorrebels`.

From then on they are an ordinary faction: they hire rebel soldiers, and Gondor's NPCs fight them on sight.

## 6. Commands

Every command starts with `/recruits`.

| Command | Who | What it does |
| --- | --- | --- |
| `join` | Players | Opens the faction menu |
| `orders` | Players | Orders menu (also: hold R) |
| `roster` | Players | Every recruit you have |
| `whitelist` | Players | Players your recruits never attack |
| `faction set <player> <faction>` | Admins | Put a player in a faction |
| `faction clear <player>` | Admins | Let a player pick again |
| `faction members` / `list` / `check <player>` | Admins | Who is where, and who fights whom |
| `faction reload` | Admins | Apply config edits |
| `zone add x z x z <factions>` | Admins | Mark a spawn zone |
| `zone list` / `here` / `remove <n>` | Admins | Manage zones |
