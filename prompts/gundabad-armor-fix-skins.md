# Prompt: lotrnpcs 0.0.63 and gundabad-bone-armor 0.1.1, armor that sits on the body, lighter Gundabad skins

The owner's words (2026-11-03), with a screenshot of a Gundabad orc recruit in 0.0.62: *"The armor is not sitting correctly on the npcs. How is this corrected. Also, gundabad orcs have an average lighter skintone. Create a prompt to follow in this regard."* The screenshot: the bone shoulder pieces float above the head, the gauntlets float beside the body, the chest piece sits high.

## Part 1: The armor model (the cause is in gundabad-bone-armor 0.1.0, not lotrnpcs)

In a Vintage Story armor shape, each top-level piece names a body part (`stepParentName`) and the game hangs it on that part, so its `from`, `to` and `rotationOrigin` must be **relative to that body part**. Working armor does this: LRC's armor starts each piece within a pixel of its part (for example `ArmorUpperTorso` at (-0.3, -0.2, -0.3) on `UpperTorso`). The bone armor's pieces are in **whole-body coordinates** instead: `GB_UpperTorso` at (-2.5, 18.8, -3.1), `GB_UpperArmR` at (-1.5, 16.8, -5.1), and so on. The game adds each part's own position on top (UpperTorso already sits 19.3 px up), which throws the pieces up and out, as in the screenshot. It would look the same on a player, so the fix belongs in the armor mod.

**Measured frame:** every piece, compared with the body part it covers, fits one consistent frame: centred on the body (x 8.5, z 8 on the seraph body) with the ground at y 0. So the conversion is mechanical. For each top-level step-parented element: `from`, `to` and `rotationOrigin` += (8.5, 0, 8) minus the absolute rest position of the body part's `from` (the sum of `from` down the seraph's element tree). Children stay as they are (they're relative to their piece). Body parts' small rest rotations aren't undone: the pieces should follow their limb.

**Build:** `tools/fix_stepparent_armor.py <mod.zip> <out.zip>` (keeps everything else byte for byte, bumps the version to 0.1.1). Check it offline by hanging the pieces on the seraph body and rendering before and after (`previews/gundabad-armor-fix.png`). Give the owner `gundabad-bone-armor-0.1.1.zip` to replace 0.1.0. It's their mod, so say what changed and that the same fix works for any armor made in that frame.

## Part 2: Lighter skins for Gundabad orcs (lotrnpcs)

Today Gundabad orcs use the Mordor orc race model (`lotrnpcs:mordororc`, owner's choice: Mordor orc body at 0.9), so they get Mordor's skins: LRC orcskin 8, 3 and 2, average brightness 95.

**Build:** a race model of their own, `lotrnpcs:gundabadorc` in `bundle_lrc.py`: the Mordor orc in every way (body, 0.9 size, tuskless faces, ears, eyes, hair) except `baseskin` and `face`: **orcskin 2, 1 and 7**, the three lightest LRC orc skins (brightness 105, 117 and 145, average 122). orcskin 7 is the pale blue-grey the Misty Mountains also use, and orcskin 1 is a warm one no race used. The Gundabad entities use it; their creative icon uses orcskin 1. Gundabad orcs already in a world keep the Mordor model (`presetskinmodel` only sets a model the NPC doesn't have yet); they are only 0.0.62 test spawns, so the owner respawns them. Tests: the model's skin list, the entities' model.

## Build and check

lotrnpcs 0.0.63: regenerate, bundle (restore `npcarrow.json`), build, both suites, package, end the round per `claude/THREADS.md`. The owner installs gundabad-bone-armor 0.1.1 in place of 0.1.0, then checks the bone armor on all three ranks from the front, side and back, and the lighter skins.
