# Prompt: lotrnpcs 0.0.55, homesteader hobbits, archer units, and Mordor orcs on the LOTR races model

You are working on **lotrnpcs**, a Vintage Story 1.22.7 code mod that adds Lord of the Rings faction NPCs (recruits, warriors and captains of 17 factions) to a LOTR-themed server. The source lives in `/mnt/project-files/lotrnpcs/` (`src/*.cs`, `tools/gen_testnpcs.py`, `tools/bundle_lrc.py`, `tools/tests/`). Build on top of 0.0.54 (looks and names) and ship it as **0.0.55**.

The owner's request, in their words (2026-11-03): *"giving hobbits homesteader clothing, as well as creating archer units. Also, the mordor orc models I believe are based on FA races when they should be based on lotr races."* Then: *"also make npcs inclined to spread out when fighting as a big group just a bit more."* And: *"for all the orc factions except isengard, their helmets should be interchangable, for the raider helmet, soldier helmets, and warrior helmets. the moria and gundabad orc helmets should remain independent for their faction. as well as commander helmets remaining independent for their specific faction."* Then, with a shield model, the shield in Part 0c. Then (2026-11-03): *"run all current commands"*, so build everything here. Where a question below has no answer yet, build the recommended default and say so, and leave the parts that need the owner's eyes (Parts 1 and 2) until they answer.

## What's true today (checked 2026-11-03)

- **Hobbits.** One type, `test-hobbit` (Shire farmers, the Shire's recruit rank), on the `lotrnpcs:hobbit` race model, with no gear at all (`TEST_NPCS` in `gen_testnpcs.py`, empty gear list). They wear nothing but their skin.
- **"Homesteader" clothing isn't found yet.** No item with that name is in the vanilla 1.22.7 clothing list (`tools/tests/factions/vanilla-clothes-codes.txt`), and no file in the server modpack zips in `/mnt/project-files/` has "homestead" in its name. It may be a set inside a mod under another file name, or a vanilla item with a different English name.
- **Ranged attacks.** Every NPC uses our melee task (`lotrnpcs:meleeattack`) and our seek task. Vanilla 1.22.7 has a ranged task, `AiTaskShootAtEntityR` (vsessentialsmod `Entity/AI/TasksRefactored/AiTaskShootAtEntity.cs`, used by bowtorns). Its config sets the projectile entity and item, damage, damage tier and type, speed, gravity, the shot's timing and sound, and whether the arrow can be picked up. Mordor orcs already hold cosmetic weapons drawn on the client only (`Weapons.cs`), which is the pattern a bow in the hand would follow.
- **Mordor orcs' model.** `lotrnpcs:mordororc` is built by `tools/bundle_lrc.py` from **LRC's** `orc-char.json` (LOTR races mod 0.4.1, bundled with the authors' permission): body size 0.9, LRC orc skins and ears. What *is* from FA Races is the vanilla-style base body every NPC shares (`seraph-faceless`, which Player Model Lib swaps for the race model at render time). So the files say the orcs already use the LRC orc. Something in game must look FA-like to the owner, and step 1 below finds out what.

## Ground rules

1. **Don't break existing worlds.** Keep entity codes `test-*`, save keys, packet ids, watched attributes and config files. New entity types get new codes; new settings get defaults, with a config version bump and a migration that only adds them.
2. **Keep every decision the owner already made.** Outfits, captain armor, the Mordor weapons, portraits, stats, hiring and prices, orders, formations, spawning, factions, battle spacing, helmets and names stay as they are.
3. **Don't guess what the owner hasn't said.** Ask first where this script lists a question, and recommend an answer.
4. Build against `tools/refs` (no game install). Both test suites pass, and every new behavior gets a test.
5. Player-facing text is plain English in full sentences, with no em-dashes.

## Part 0: Spread big fights out a little more

0.0.53 spread battles out (`src/Battle.cs`): at most `MaxAttackersPerTarget` (3) NPCs on one enemy NPC, attackers placed around their target at `CombatSpacing` (1.5 blocks, but never farther than 2.2 so they can still hit), and fighting NPCs closer than `CombatSpacing` pushed apart at up to 1.2 blocks a second. The owner wants "just a bit more" spread in big group fights:

- Raise the `CombatSpacing` default from 1.5 to 2.0 blocks. Attackers then stand 2 blocks from their target, still inside melee reach.
- Keep `MaxAttackersPerTarget` at 3 and the push speed as it is, so it's a nudge, not a different behavior.
- Config migration: a file still holding the old default 1.5 becomes 2.0; a value the admin chose stays. Update the `#Instructions` line and the config guide.
- Test: the new default, the migration, and that three attackers at 2.0 still reach their target (front of the attacker within the melee task's 2-block reach).

## Part 0b: Shared orc helmets

Today each orc faction wears only its own helmets (`FACTION_SETS` in `gen_testnpcs.py`):

| Faction | Recruit | Warrior | Captain |
|---|---|---|---|
| Mordor | Mordor Orc Raider helmet | Mordor Orc Warrior helmet | Servant of the Eye, red mask |
| Angmar | Angmar Raider helmet | Angmar Soldier helmet | Wight Guard (two helmets) |
| Misty Mountains | Moria Orc helmet | Gundabad Orc helmet | Gundabad Warlord |
| Isengard | Uruk-hai Scout | Uruk-hai Infantry | Uruk-hai General |

The owner wants Mordor, Angmar and Misty Mountains orcs to share their raider, soldier and warrior helmets. The Moria and Gundabad helmets stay Misty Mountains only, every captain keeps its own faction's helmet, and Isengard is unchanged. Reading of that (confirm with the owner if unsure):

- **Recruits** of all three factions pick from the Mordor Raider and Angmar Raider helmets. Misty Mountains recruits can also wear their Moria helmet.
- **Warriors** of all three factions pick from the Mordor Warrior and Angmar Soldier helmets. Misty Mountains warriors can also wear their Gundabad helmet.
- Mordor and Angmar orcs never wear Moria or Gundabad helmets. Body and leg armor stay each faction's own.
- Build it as extra whole sets (the faction's body and legs with each allowed helmet), so `lotrnpcs:startinggear` keeps choosing among complete sets. A set whose helmet mod isn't installed is skipped, so a server without the Angmar mod still dresses Mordor orcs in Mordor helmets.
- The 0.0.54 helmet roll and the kept-helmet toggle work unchanged with the borrowed helmets. Portraits pick their first set as now.
- Test: every orc recruit and warrior set list holds exactly the allowed helmets, no Mordor or Angmar set has a Moria or Gundabad helmet, the captain and Isengard sets are unchanged, and every listed code exists in its armor mod.

## Part 0c: A shield for Mordor orcs

The owner's words (2026-11-03), with the model `tools/weapons/orc_shield.json`: *"Take this shield model for the mordor orcs. its also a visual thing, and its a toggle-able accessory. properly rig it, it cant be used in conjunction with the poleaxe."*

- **Cosmetic, client only,** like the 0.0.45 weapons (`Weapons.cs`): no blocking, no damage change, nothing dropped. The choice is a watched attribute (`lotrnpcsShield`, "on" or "off"), drawn on the client.
- **Rigged by its grip.** The model has grip elements ("grip post", "grip bar", "grip wrap", "grip wrap core"). Hold it in the left hand at that grip, face out, using the same hand-transform math as the weapons (`lotrnpcs-weapons.md` notes) on the `LeftHand` attachment point, and check it with the offline preview tool from the side and front.
- **Textures** come from the owner's texture pack (`rotwarriors/tarnished/*`, `leather/brown`), bundled under `lotrnpcs:item/orcweapon/...` like the weapon textures, so it doesn't depend on the pack being installed.
- **Random at spawn:** Mordor orcs get a shield by chance (`ShieldChance`, default 0.5), never together with the poleaxe.
- **Toggle in the looks menu:** a "Shield" on/off row for Mordor orcs. Choosing the poleaxe takes the shield off; turning the shield on while holding the poleaxe is refused with a short message. The server checks both.
- Existing orcs get no shield until the owner turns it on.
- Test: the poleaxe and shield never come together (spawn roll, toggle, weapon change), the toggle survives a reload, non-Mordor NPCs can't get one, and the grip lands at the hand's origin.

## Part 1: Mordor orcs on the LOTR races model

1. Ask the owner what looks FA-like (a screenshot next to an LRC orc player helps): the body shape, the face, the skin, the ears, or the size. Compare it with LRC 0.4.1's own orc race as players see it.
2. Fix what actually differs. Likely places: the skins and ears `bundle_lrc.py` picks for `mordororc`, the 0.9 size factor, or Player Model Lib falling back to the base body when the race model doesn't load. Angmar orcs use the same model and change with it, unless the owner says otherwise.
3. Keep the owner's earlier ruling that goblins, Mordor orcs and Uruk-hai are three different-looking races.

## Part 2: Homesteader clothing for hobbits

1. The owner's answer (2026-11-03): *"give them the clothes similar to vanilla traders, minus any helmets. instead maybe a hat on rare occasion."* Vanilla traders' own outfits are drawn shape overlays, not wearable items, so build whole sets from vanilla wearable clothing in the same plain, homely style (shirts, waistcoats, trousers or breeches, belts, aprons, scarves), picked at random per hobbit, and a hat (straw, roll, broadbrim, thrum cap and the like) only rarely (`HatChance`-style roll, about 1 in 8).
2. Dress `test-hobbit` through `lotrnpcs:startinggear` as the other factions are. Every item is vanilla, so nothing is missing on either server.
3. Check the fit on the small harfoot body. Clothing made for the vanilla seraph body may clip or float on it. Hobbits stay barefoot unless the owner says otherwise.
4. Hobbits wear no helmets. Their rare hat is not a helmet: it is rolled at spawn on its own and goes in the clothes head slot, so the looks menu's helmet toggle doesn't cover it (built 0.0.55: chance 0.125, six vanilla hats).

## Part 3: Archer units

The owner's answer (2026-11-03): *"bring up archer units, primarily for mordor, up to the current spec that the npcs are at right now"*. So build Mordor archers first (`test-mordororc-archer`), with everything every NPC has today: faction targeting, battle spacing, random helmets, the looks menu (name, helmet, face; a bow instead of the weapon row; no shield), race names on hire, hire price, orders, formations, guard, portraits, the creative tab and zone spawning. Other factions follow once the owner sees Mordor's. For the open questions below, use the recommendations.

Open questions, with the recommendation used:

- **Who gets archers:** every fighting faction, or only some (for example Gondor, Rohan, elves, Mordor and Isengard)? Recommend factions with a clear bow tradition first.
- **What an archer is:** a separate type per faction (for example `test-gondor-archer`) with its own spawn share and hire price, or a variant of an existing rank? Recommend a separate type at warrior strength and the warrior price, so ranks stay as they are. Owner's answer (2026-11-03): *"The archers are just an additional unit, they don't have ranks"*, so one archer type per faction, with no recruit, warrior or captain versions.
- **How hard they hit:** damage per arrow, range, and time between shots, compared to a warrior's melee. Recommend less damage per hit than a warrior, a range of about 20 blocks, and a shot every 3 to 4 seconds.
- **Arrows:** can players pick them up? Recommend no, so archers can't be farmed for arrows.

Then build it:

- A ranged attack task based on vanilla's `AiTaskShootAtEntityR`, choosing targets by faction exactly as our seek and melee tasks do (including battle spacing's attacker limit and hireling rules), aiming at a player's body under Combat Overhaul like the melee chest hit, and never shooting through allies (no shot when an ally stands in the line of fire).
- Archers keep their distance: they stop at shooting range instead of closing in, back off when an enemy gets close, and fall back to melee only when cornered.
- A bow drawn in the hand on the client only, like the orc weapons, with a draw-and-release animation timed to the shot.
- Natural spawning: archers join groups in zones at a share the owner sets, as warriors and captains do (`lotrnpcs-spawning.json`).
- Hiring: archers can be hired and take orders like any hireling. In formations they stand in the back rank.

## Part 4: Verify and deliver

1. Tests: the orc model fix, the hobbit outfits (every listed item exists, and the fallback works), archer targeting (same faction rules as melee, players aimed at the chest, no shot through an ally), the keep-distance behavior, spawning shares, and the config migration.
2. Build, run both suites, package `lotrnpcs_0.0.55-test.zip`, replace the old zip in `dist/`, and sync the sources.
3. Update the config guide (https://claude.ai/artifact/HPy9dzDE9pfcGVSyhhzRrn) and the project notes.
4. Report briefly what changed and that it hasn't been tested in game. Ask for screenshots of a hobbit, an archer fight and a Mordor orc.
