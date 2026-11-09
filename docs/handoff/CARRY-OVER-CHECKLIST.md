# Moving the LOTR NPC mod back to your other account: your checklist

Everything the Claude on your other account needs is in one file: **lotrnpcs-handoff.zip** (this bundle, made 2026-11-04 at version 0.0.72). Docs and links from this account won't open there, so everything is copied inside the zip, including the in-game test checklist.

## 1. Download before you leave

- [ ] **lotrnpcs-handoff.zip.** It holds:
  - `HANDOFF.md`: the full briefing (section 3 "Summary" and the "Update 2026-11-04 (third account)" notes cover everything done here)
  - `THREADS.md`: how the project should start, end and move threads, with the round log
  - `lotrnpcs/`: the mod's source, tools, tests and previews, with `dist/lotrnpcs_0.0.72-test.zip`, the latest build
  - `docs/`: the in-game test checklist, design doc, config guide and reference reports
  - `memory/`: memory notes from all three accounts
  - `build-inputs/`: LRC 0.4.1 and FA Races 1.3.3 (needed to build), plus your Gundabad armor mod, **0.2.0 being the one to run**

## 2. In the other account's project

1. **Replace** the old `lotrnpcs-handoff.zip` (the 0.0.63 one) with this one.
2. **Delete** the project's `source/source.patch` if it has one. This bundle already contains every change, and the patch starts over from it.
3. Send this as your first message:

> I'm back from another account. Replace the old base with the new lotrnpcs-handoff.zip I uploaded and follow THREADS.md to start the thread. Read HANDOFF.md fully, especially the third account's update notes, and save the key facts to project memory. Confirm you can build 0.0.72 and run both test suites without changing anything, then tell me the next open question.

## 3. Keep on your own computer, upload only when asked

- [ ] Your server modpack zips (faction armor mods, Player Model Lib, Combat Overhaul fork, Coinage and the rest).
- [ ] Your weapon models and textures in progress.
- [ ] Game logs and screenshots when you report an in-game test.

## 4. What Claude will ask first

- How 0.0.72 looks in game, using `docs/test-checklist.md`, with **gundabad-bone-armor 0.2.0** installed. Most important: does the poleaxe orc actually swing?
- Whether to drop the old leggings' waist piece under the new Gundabad chest piece (recommended).
- Your weapon models for the other factions, and the bow JSON re-saved against its packed texture, if you want it to use that one PNG.
