# Prompt: lotrnpcs 0.0.54, random helmets, customizing a hireling's looks, weapon and name, and race names on hire

You are working on **lotrnpcs**, a Vintage Story 1.22.7 code mod that adds Lord of the Rings faction NPCs (recruits, warriors and captains of 17 factions) to a LOTR-themed server. The source lives in `/mnt/project-files/lotrnpcs/` (`src/*.cs`, `tools/gen_testnpcs.py`, `tools/tests/`). Build this on top of 0.0.53 (battle spacing) and ship it as **0.0.54**.

The owner's request, in their words (2026-11-03): *"randomize whether the npcs are wearing helmets or not, and add a function in their specific menu to customize how they look (whether helmet on or off, and facial features) and what their name is. In addition, when hired have their name be randomized anyway to names befitting the race."* Then: *"and also what weapons they hold to customize that."*

## How things work today (check before changing)

- **Gear.** `lotrnpcs:startinggear` (`EntityBehaviorStartingGear`, `LotrNpcsModSystem.cs`) dresses an NPC once, at first spawn, from one random complete set in the entity JSON (`sets`), else from `items`. A code ending in `@slot` goes straight into that gear slot (the Easterling captain's chain coif is worn `@face`). Sets are written by `FACTION_SETS` in `tools/gen_testnpcs.py`. Gear is stored by vanilla `seraphinventory` and never drops.
- **Armor stats don't come from the gear.** NPC protection is `lotrnpcs:rankarmor` (a tier per rank), so taking a helmet off must not change how much damage an NPC takes. Confirm this in the code before relying on it.
- **Looks.** Player Model Lib's `ExtraSkinnable` behavior draws the race model named by the `skinModel` watched attribute (set by `lotrnpcs:presetskinmodel`) and randomizes the skin parts at spawn. Find out from Player Model Lib 1.24.0 (the user runs it; the server modpack zips are in `/mnt/project-files/`) where it stores the chosen parts (likely vanilla's `skinConfig` / `appliedParts` watched tree) and where the client can read each race's parts and their variants. The mod has no compile-time dependency on Player Model Lib and must keep it that way (reflection or plain attribute access only).
- **Weapons.** Only Mordor orcs carry weapons (`Weapons.cs`, `EntityBehaviorHeldWeapon`): five cosmetic models listed per entity, the pick stored in the `lotrnpcsWeapon` watched attribute and shown on the client only. A stored weapon that isn't in the list is picked again on load.
- **Names.** The vanilla `nametag` behavior shows the type name ("Mordor orc recruit"), stored in the `nametag` watched tree under `name`.
- **Talk menu.** Right-clicking an NPC opens the talk dialog (`Hiring.cs`). Its options depend on `TalkState.Mode`: `own` is the owner's hireling.

## Ground rules

1. **Don't break existing worlds.** Keep entity codes `test-*`, save keys, existing packet ids and message types (add new ones at the end of the channel's list), watched attributes and config files. New settings get defaults, and the config version goes up by one with a migration that only adds them.
2. **Keep every decision the owner already made.** Outfits per faction and rank, captain armor, the Mordor-only weapon models and their grips, portraits, stats, hiring, orders, formations, spawning, factions and battle spacing stay as they are.
3. **The server decides.** The client only asks; the server checks that the player owns the hireling, that the part, variant and weapon are allowed for that NPC, and that the name is valid, before changing anything. Nobody can dress an NPC in an item that wasn't part of its own outfit.
4. **Cosmetic only.** Nothing here changes damage, protection, health, AI or prices.
5. Build against `tools/refs` (no game install). Both test suites pass, and every new behavior gets a test.
6. Player-facing text is plain English in full sentences, with no em-dashes.

## Part 1: Random helmets

- When an NPC first spawns, it wears its outfit's helmet (the head slot) only by chance: `HelmetChance` in the config, default 0.6. Face pieces such as the Easterling captain's chain coif and the Haradrim miner mask aren't helmets and stay on.
- A helmet it doesn't wear isn't thrown away. It is kept with the NPC (saved, never dropped, never shown) so the owner can put it on later.
- Captains always wear theirs (`CaptainsAlwaysHelmeted`, default true), because the owner picked captain helmets to mark them out (the red-masked Servant of the Eye, Wight Guard, Gundabad Warlord). Factions whose outfits have no helmet (tormented elves, Haradrim) are unaffected.
- NPCs already in a world keep what they wear today.

## Part 2: "Change how you look" in the hireling's menu

- A new option in the owner's talk menu (Mode `own` only, not borrowers or strangers): *"Let me change how you look."* It opens an appearance dialog with:
  - **Name:** a text box, 1 to 24 characters, letters, spaces, apostrophes and hyphens only, trimmed. Saved to the name tag.
  - **Helmet:** on or off. Only shown when the NPC has a helmet (worn or kept).
  - **Weapon:** a choice from the weapons its type may carry, plus "None". Only shown when that list isn't empty (today: Mordor orcs). The choice is stored in `lotrnpcsWeapon` as today, and choosing "None" must survive reloads (today an empty weapon is picked again on load, so add a stored "none").
  - **Facial features:** one row per face part the race model offers (for example hair style, hair color, beard, eyes, skin tone, face; whatever Player Model Lib lists for that race), each with previous and next buttons and the variant's name. Parts that aren't about the face or hair (body shape, voice) are left out.
- Changes show on the NPC in the world straight away, so the player watches it change while the dialog is open. "Done" keeps them; "Cancel" puts back what it had when the dialog opened.
- A short reply from the NPC when it's done, in the voice of the existing talk lines.

## Part 3: A name befitting the race when hired

- The moment an NPC is hired, it gets a random name from its race's list instead of "Mordor orc recruit". Its faction and rank stay visible in the info text when you look at it.
- Name lists per people, written in the generator so they're easy to edit: Mordor and Angmar orcs, Misty Mountains goblins, Uruk-hai, men of Gondor, Rohan, Dale, Arnor, Dunland, Harad, the East and Black Númenóreans, dwarves and wicked dwarves, elves and tormented elves, and hobbits. At least 40 names each, in the style of the books (Sindarin-sounding elves, Old English-sounding Rohirrim, Khuzdul- and Norse-sounding dwarves, harsh orc names, homely hobbit names with family names). Don't use the names of well-known characters from the books or films.
- Names already given by an owner are never replaced. A hireling dismissed keeps its name.

## Part 4: Config, verify, deliver

1. Add `HelmetChance` (0.6, kept between 0 and 1) and `CaptainsAlwaysHelmeted` (true) to the config file that fits (the faction config, `lotrnpcs-factions.json`, unless there's a better home), with `#Instructions` lines, a version bump and migration.
2. Tests: the helmet roll (0, 1 and captains), the kept helmet goes back on and comes off, the server refuses a stranger's change, a part or weapon the NPC can't have, and a bad name; "None" weapon survives a reload; a hired NPC gets a name from its race's list and a renamed one keeps its name; every name list has 40 or more unique names; config migration.
3. Build, run both suites, package `lotrnpcs_0.0.54-test.zip`, replace the old zip in `dist/`, sync the sources.
4. Update the config guide (https://claude.ai/artifact/HPy9dzDE9pfcGVSyhhzRrn) and the project notes.
5. Report briefly what changed and that it hasn't been tested in game. Ask the owner to hire an NPC, open the new menu, and send a screenshot.

## Decisions this script made that the owner can change

- Helmet chance 60%, and captains always keep their helmets.
- Only the owner can change a hireling's looks (not group members borrowing a shared guard).
- Weapons can only be swapped among what that NPC type may carry (Mordor orcs for now), not given to other factions.
- The name tag shows just the name after hiring, with faction and rank in the info text.
