# Prompt: lotrnpcs 0.0.60, the archers hold the bow wrong when drawing

You are working on **lotrnpcs** (source `lotrnpcs/`). Build on 0.0.59 and ship **0.0.60**.

The owner's words (2026-11-03), with four screenshots of 0.0.59: *"the bow is held wrong. the shield is now correct though. create a prompt to act on this to correct it."*

**Confirmed in game (0.0.59):** the shield, slid 1 px out, now sits right. Don't touch it.

## What the screenshots show

- **Drawing (screenshot 1):** the archer raises its bow arm, but the bow lies along the arm and points forward and down like a stick, instead of standing upright with the string toward the archer.
- **At rest (screenshots 2 to 4):** the bow hangs in the hand along the leg, tip near the ground. That is the 0.0.59 design.

## Cause (checked in the vsapi source)

0.0.59 shows the drawn bow (`orcweapon-bow-drawn`) while `AnimManager.IsAnimationActive("lotrnpcs-bow-draw")` is true. But the game's `AnimationManager` keys running animations by the **shape animation's name** (`ActiveAnimationsByAnimCode[animdata.Animation]`, so `bowaim` and `bowhit`), not by the entity's animation code. The check was never true, so the drawn bow never showed: the archer drew and fired holding the rest bow, and the rest bow's transform, made for a hanging arm, points along a raised arm. The 0.0.59 test passed because it faked the animation check.

Offline renders confirm it: `previews/bow-0.0.60.png`, top row, is the rest bow in the draw and shove poses (matches screenshot 1); bottom row is the drawn bow in the same poses.

## Fix

1. **`Weapons.cs`:** before asking the animation manager, turn each `drawnWhile` code into the animation name the game tracks (`AnimationMetaData.Animation` from the entity's client animation list, cached; falling back to the code itself). Put the lookup in a static helper so the test can check it against the real entity file.
2. **The shove:** add `lotrnpcs-bow-attack` (the unarmed `hit`, used when an enemy is too close to shoot) to `DRAWN_WHILE` with 0 ms, so the archer bashes with the upright bow instead of jabbing with the rest bow pointing forward (bottom row of the preview).
3. **Rest pose:** unchanged. Ask the owner after they test 0.0.60 whether the hanging bow is right, or whether they want it held another way (across the body, slung on the back).
4. **Tests:** the archer's `drawnWhile` codes are all in its animation list, and the helper resolves them to `bowaim`, `bowhit` and `hit`, the names the game tracks. Replace the faked check with one that uses those names.

## Build and check

Version 0.0.60; regenerate, bundle (restore `npcarrow.json`), build, both suites, package `dist/lotrnpcs_0.0.60-test.zip`, end the round per `claude/THREADS.md`. Owner checks: the bow stands upright while the archer draws and fires, swaps back to hanging after the shot, and stays upright during a close-range shove.
