# Prompt: lotrnpcs 0.0.61, bow carried level (option C), and a Gundabad faction (work in progress)

The owner's words (2026-11-03): *"I added a mod called gundabadarmor_0.1.0. call on it for a gundabad faction for a work in progress. for the bow do option C."*

## Part 1: Bow at rest, option C (done in 0.0.61)

Of the three holds in `previews/bow-idle-options.png`, the owner chose C: carried level at the side, pointing forward, string up. Solved with `tools/preview/bowrest.py <body> <bow> 20 trail 15`: front end tipped 20 degrees down, turned 15 degrees so the back end swings clear of the leg. Clear of the legs and torso in every idle, walk and sprint frame (`previews/bow-rest-0.0.61.png`). Only `BOW["hand"]` and the rest-bow test change; drawing, firing and the shove keep the upright drawn bow (0.0.60).

## Part 2: Gundabad faction from gundabadarmor_0.1.0 (blocked: mod not received)

The mod wasn't attached and the owner's PC wasn't reachable, so none of this is built. When the zip arrives:
1. Read its modinfo (mod id, dependencies) and every armor item code, with its lang names, the way the server modpack report covers the other armor mods.
2. Ask the owner the decisions this needs before building, recommending answers: is Gundabad a new 18th faction or does it replace the Misty Mountains' Gundabad helmets and Gundabad Warlord (which come from the norc mod today); which race model (the LRC orc body like Mordor, or the Uruk-hai body); which armor goes to recruit, warrior and captain; stance, hire coin (the planchet) and prices like everyone else; whether it shares orc helmets with Mordor and Angmar; whether it gets archers.
3. Build it as work in progress: entity types `test-gundabadorc-recruit/warrior/captain`, faction id `gundabad` added by a config migration that only adds it (as 0.0.35 added factions), creative items and portraits, `/lotrzone` support, and "(WIP)" in the creative names until the owner says it's done. The armor is read from the installed mod by item code and never bundled; without the mod they wear a vanilla outfit, like every other faction.
