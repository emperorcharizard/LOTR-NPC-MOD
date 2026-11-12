# Prompt: research and act on client frame rate with many NPCs

You are working on **lotrnpcs**, a Vintage Story 1.22.7 code mod (this repo: `src/*.cs`, `tools/gen_testnpcs.py`, `tools/build.py`, `tools/tests/`). Owner, 2026-11-12: *"Research ways optimization / fps can be improved then act on it."*

## What is already known (docs/notes/lotrnpcs-performance.md)

- Owner's PC (i7-12700KF, RTX 3070): `/recruits bench` gives about 153 FPS at 50 fighting NPCs, 95 at 100 and 56 at 200 (1% lows 85, 49, 32). The server holds about 29 ticks/s at 200, so server AI is not the limit.
- The client frame goes mostly to `esr-afteranim`: about 30-34 µs of CPU per drawn NPC per frame. Whole-model rebuilds (`esr-tesseleateshape`) were fixed in 0.0.114 and no longer show.
- NPCs are seraph-skeleton humanoids (Player Model Lib) wearing armor from other mods (several attached shapes each), holding our weapon models, with Combat Overhaul / Overhaul Lib loaded. OptiTime logs "Entity Animation optimization disabled to prevent conflicts" (Overhaul Lib compatibility mode).

## Research (answer each from evidence: the game's DLLs in `%APPDATA%/Vintagestory` read by reflection or IL, mod zips in `VintagestoryData/Mods`, our code, the in-game logs)

1. What exactly runs in `esr-afteranim` for one NPC each frame (the animator's pose calculation, per element or per joint, attachments), and what makes it scale: elements in the composite shape, joints, active animations, attached armor shapes?
2. Does the game already skip or slow animation for NPCs that are far away, off screen or not visible? Is there a setting for it? What does OptiTime's "Entity Animation optimization" do, and why does it switch off with Overhaul Lib?
3. How many elements and joints does one of our NPCs have (seraph body + armor + weapon + shield), and how much of that could be cut without a visible change (for example armor parts hidden under other parts)?
4. Which of these can a mod do safely in 1.22.7: animate far NPCs less often (animation LOD), skip animation off screen, reduce elements, lower the entity render distance for our NPCs only?

## Act

Pick the change with the best FPS gain for the least risk, and build it:
- No visible change up close (within about 20 blocks), no change on the server or in combat, and nothing that breaks Combat Overhaul, Player Model Lib or OptiTime.
- Any new option goes in the client side with a sensible default, and is described in the config guide. Testing hooks go behind `DebugCommands`.
- Both test suites pass (`python tools/build.py`), with a test for any new logic.
- Measure before and after with `/recruits bench 100` and `200` if the game can be driven; otherwise say what to run.
- Record the findings, the numbers and anything rejected (and why) in `docs/notes/lotrnpcs-performance.md`, then commit and push.
