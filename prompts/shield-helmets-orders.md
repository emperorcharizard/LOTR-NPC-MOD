# Prompt: lotrnpcs 0.0.58, shield clipping, helmet choice, "recruit" orders wording, and the R menu fix

You are working on **lotrnpcs**, a Vintage Story 1.22.7 code mod that adds Lord of the Rings faction NPCs to a LOTR-themed server. Source: `lotrnpcs/` (`src/*.cs`, `tools/gen_testnpcs.py`, `tools/bundle_lrc.py`, `tools/tests/`). Build on top of 0.0.57 and ship it as **0.0.58**.

The owner's words (2026-11-03), after testing 0.0.57 in game with screenshots and logs: *"the model is correct now. There is an issue with the shield clipping with the npc. and the helmets only have the switch on and off, not the option to choose the helmet. The archers work fine. Change the wording of the controls for R to be recruit instead of hireling. But the recruit command menu no longer works (opens) when holding R. Create a script for what i said then act on those instructions."* Then: *"When re binding R to caps lock it didnt open either."*

Confirmed in game by this test (0.0.57): tuskless Mordor orc faces, archers, the looks menu, names and rank titles ("Recruit Lugmakh").

## Ground rules

1. **Don't break existing worlds.** Keep entity codes `test-*`, save keys, packet ids (46306 stays the looks packet), the hotkey code `lotrnpcs-hirelingorders`, watched attributes and config files. No config change is needed for this round.
2. **Keep every decision the owner already made** (outfits, shared orc helmets, the helmet roll, the shield's rules, stats, orders, formations, spawning).
3. Build against `tools/refs`. Both test suites pass, and new behavior gets a test.
4. Player-facing text is plain English in full sentences, with no em-dashes.

## Part 1: The orders menu doesn't open (bug)

**Cause, from the owner's `client-main.log`.** The owner loaded a world at 16:19 (the menu's system started and logged that R is shared with Claims Radar's "Show claims"), left to the main menu, and loaded a world again at 16:22. The second time, `HirelingOrdersSystem.StartClientSide` threw `An item with the same key has already been added. Key: lotrnpcs-hirelingorders` from `RegisterHotKeyFirst`, and the game logged `Failed to start system LotrNpcs.HirelingOrdersSystem`. The game's hotkey list outlives a world session, so the key from the first world is still registered, and registering it first again throws. Everything after that line never ran: no key handler, no key events and no `/lotrorders`. That is why Caps Lock didn't work either; no key could have.

**Fix (`src/Orders.cs`).**
- If `GetHotKeyByCode(HotkeyCode)` already returns the key, reuse it: update its `Name` to the current text, set `TriggerOnUpAlso`, and set our handler again (it replaces the old session's handler). Only register it when it doesn't exist yet. It stays first in the list from the first registration, so Claims Radar still doesn't swallow R.
- Wrap the hotkey setup in a try/catch that logs the error, so a hotkey problem can never again stop the network channel, the key events and `/lotrorders` from starting.
- Workaround for the owner until 0.0.58 is in: quit the game fully and relaunch before joining a world.

## Part 2: "Recruit" instead of "hireling" for the R controls

The owner calls it "the recruit command menu". Change the words in what R controls, and only there:
- Settings > Controls: "LOTR NPCs: recruit orders (hold)".
- The orders menu and its replies: "Recruits near you", "No recruits of yours are near enough to hear you", "Recruits following you", "Recruits holding position", and so on, plus the shared-key warning and the `/lotrorders` description.
- Not changed: the talk menu, `/hirelings` commands, the whitelist messages, the death message and the rank titles. **Flag to the owner:** "recruit" is also the lowest rank's name ("Recruit Lugmakh"), so a menu saying "Recruits near you: 3" counts warriors and captains too. Offer to use "recruits" everywhere hired NPCs are named, or a different word, if they want.

## Part 3: Shield clipping

The 0.0.56 rig was solved against the bare seraph body, where the shield's back rests within 1 pixel of the forearm. In game the orcs wear armor (bracers, pauldrons, chain), which is thicker than that, so the shield sinks into the armored arm and side (owner's screenshots, front and back).

**Fix (`SHIELD` in `tools/gen_testnpcs.py`).** Keep the rotation the owner approved (across the forearm, top forward). Slide the shield straight out along its face normal by 2.5 pixels (world), so it clears 1.5 to 2 pixels of armor:
- translation (0.2359, 0.0682, -0.3688) becomes (0.2314, 0.0591, -0.1086), rotation and scale unchanged.
- Checked offline (scratchpad `shieldfit.py`, measured against the seraph body inflated by 1.5 px of armor on the torso and legs and 1 to 1.5 px on the arms, over idle, walk, sprint and the attack animations): every idle, walk and sprint frame clears. The upper arm still touches the shield edge for a moment at the top of the axe swing. The grip post still bridges the gap to the hand.
- Preview: `previews/shield-0.0.58.png` (0.0.57 above, 0.0.58 below, front and back, rest and walk).

## Part 4: Choose which helmet

Today the looks menu has Helmet: Switch On/Off. The owner wants to choose the helmet itself.

**Which helmets (my call, tell the owner):** the helmets the NPC's own type can spawn with, which are already in its gear sets, plus "No helmet". So a Mordor recruit chooses between the Mordor Raider and Angmar Raider helmets (the shared orc helmets of 0.0.55), a Gondor recruit between the Man-at-Arms helmet with and without chain, and a captain keeps its own captain's helmet. Helmets from mods the server doesn't run are left out. The helmet it wears now is always in the list, even if it's no longer in its sets. Offer the owner a wider list (every helmet of the faction, or of the shared orc pool across ranks) if they want it; it's one list in the generator.

**Build.**
- `gen_testnpcs.py` writes each type's helmet list into its entity attributes as `lotrnpcsHelmets`: every `armor-head-` code in its `lotrnpcs:startinggear` sets, in order, without duplicates. Codes with an `@slot` (the Easterling captain's coif, worn on the face) aren't helmets and stay out.
- `Looks.HelmetChoices(npc)`: the type's list filtered to items the world has, with the helmet it wears or keeps added when missing. Same answer on server and client.
- The looks dialog's Helmet row becomes `< name >` arrows like Weapon, cycling the choices and "No helmet". Shown when the NPC has any choice or wears or keeps a helmet.
- `LooksRequest.Helmet` also takes a helmet item code or "none" ("on" and "off" still work). The server checks the code is one of the NPC's choices, then puts it on: the kept helmet if it matches, else a new one; the replaced helmet is dropped from the NPC (gear never drops into the world). "none" takes the helmet off into the kept slot as Off did.
- Cancel restores the helmet the NPC wore when the dialog opened.
- Tests: the generated helmet lists (Mordor recruit has both raider helmets, no coif in the Easterling captain's list, hobbits have none); Apply puts on a listed helmet, refuses an unlisted one, "none" keeps the old helmet, choosing the kept one puts it back.

## Build and check

- Version 0.0.58 in `gen_testnpcs.py`. Regenerate, bundle LRC, build the DLL, run both test suites, package `dist/lotrnpcs_0.0.58-test.zip`, and keep only that zip in `dist/`.
- **Known build bug (found 2026-11-03):** `bundle_lrc.py` clears `shapes/entity` after `gen_testnpcs.py` has written the arrow model `shapes/entity/npcarrow.json` there, so a clean build following HANDOFF section 7 ships archers with invisible arrows. Until the owner decides on a fix, restore that file after bundling, or repackage from the last zip.
- Update the config guide only if a setting changes (none does this round).
- Tell the owner what to check in game: the menu opens after leaving and rejoining a world; the words in Controls and the menu; the shield clear of the arm in idle and walk; the helmet arrows on a Mordor recruit, a Gondor recruit and a captain.
