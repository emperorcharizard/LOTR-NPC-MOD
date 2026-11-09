# Prompt: lotrnpcs 0.0.59, bow at rest until drawn, shield back on the arm, helmet text, one hire coin

You are working on **lotrnpcs**, a Vintage Story 1.22.7 code mod (source `lotrnpcs/`). Build on 0.0.58 and ship **0.0.59**.

The owner's words (2026-11-03), after testing 0.0.58 with screenshots: *"the mordor orc shield now floats off of the body. the R command menu is working as intended again. update the animations of the bow to be at rest until it is drawn to fire. then an animation to draw back and fire. The mordor orcs cycle between the mordor orc helmet, the angmar soldier helmet, and no helmet. As do the archers. the warriors have the mordor helmet, the angmar soldier helmet, and the no helmet option. The captain has the no helmet option and the servant of the eye option, but hte text is cropped out. create a script of this information to act upon. Set the hiring coin id mordor uses to be standard for all races."* With reference art of archers: bows held low and relaxed at rest, and drawn to the cheek, bow arm straight, when shooting.

**Confirmed in game (0.0.58):** the R orders menu opens again (after rejoining a world too); the helmet chooser works and offers what was built (recruits and archers: their Mordor and Angmar helmets plus No helmet; warriors: theirs plus No helmet; captains: the red-masked Servant of the Eye plus No helmet).

## Ground rules

Same as 0.0.58: keep codes, save keys, packets and settings; keep the owner's earlier decisions; build against `tools/refs`; both suites pass with tests for the new behavior; plain English, no em-dashes.

## Part 1: Shield floats off the body

0.0.58 slid the shield 2.5 px out along its face. Measured: that leaves the grip bar 1.2 px off the bare hand, and the owner's armor is thinner than assumed, so it floats. 0.0.57 (no slide) clipped. **Fix:** slide 1.0 px instead. The grip bar then rests on the hand's surface (0.0 px gap) and the shield clears the bare arm, with about half a pixel of armor allowed for. Update the grip test to 1.0 px. Preview: `previews/shield-0.0.59.png`.

## Part 2: The bow at rest until drawn, then draw and fire

Today the archer holds the bow out in front between shots (vanilla's `holdinglanternrighthand` pose, owner's screenshot), because the bow's hand transform is solved for the full-draw pose only.

**Build:**
- **At rest:** no hold pose. The archer's arms idle, walk and run as usual, and the bow hangs in the right hand, stave upright along the leg, upper limb a little forward, string toward the back (like the owner's first reference). New item `lotrnpcs:orcweapon-bow` keeps that code and gets the rest transform, solved offline so the grip is in the hand and the limbs clear the leg in idle and walk.
- **Drawing and firing:** a second item, `lotrnpcs:orcweapon-bow-drawn`, with today's full-draw transform. The client shows it only while the draw (`bowaim`) or release (`bowhit`) animation runs, and the rest bow otherwise. That is a new optional `drawnItem` and `drawnWhile` (animation codes) on a heldweapon entry (`Weapons.cs`), checked each client tick with `AnimManager.IsAnimationActive`. The server never sees a held item, as before.
- Draw timing (1100 ms), cooldowns, damage and the shove when cornered stay the same. The shove plays with the bow at rest.
- Tests: the archer entry has no hold pose and names the drawn item and the two animations; both bow items exist with their transforms; the rest bow's grip lands in the hand.

## Part 3: Helmet text cropped

The helmet's name with "(1 of 2)" is longer than the value column (240 px), so "Servant of the Eye Helmet (Red Mask) (1 of 2)" wraps and the second line is cut off. **Fix:** widen the looks dialog from 440 to 600 px, so every row's value has 400 px. No other change.

**Open question for the owner (built as is until answered):** recruits only see the raider-tier helmets and warriors the warrior-tier ones. Should every Mordor recruit, warrior and archer choose from all four shared helmets? Recommended: keep the tiers, so a helmet still tells you the rank.

## Part 4: One hire coin for every faction

The owner's Mordor entry uses `coinage:planchet-silver-lg` (server log: "hired ... for 15 coinage:planchet-silver-lg"). The shipped default, `coinage:coin-silver-lg`, doesn't exist in their world.
- The default `HireCoin` becomes `coinage:planchet-silver-lg`.
- Config version 13: every faction still on the old default `coinage:coin-silver-lg` gets the planchet. A coin the admin chose otherwise stays.
- The `#Instructions` line and the config guide name the planchet.
- Tests: new files, prices list, a version 12 file with mixed coins migrates only the old default.

## Build and check

Version 0.0.59. Regenerate, bundle (restore `npcarrow.json` after bundling), build, run both suites, package `dist/lotrnpcs_0.0.59-test.zip`, end the round per `claude/THREADS.md`. Tell the owner to check: the shield on the arm, front and back; an archer idling and walking with the bow down, then drawing and firing; the captain's helmet name in full; hiring a non-Mordor NPC takes silver planchets.
