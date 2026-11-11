---
name: lotrnpcs-weapons
description: lotrnpcs 0.0.45 cosmetic Mordor orc weapons - how they're held, animated, textured and previewed offline, and the VS rendering facts behind it
metadata:
  type: project
  modified: 2026-11-03T07:05:00.000Z
---

User 2026-11-03 uploaded 5 test weapon models (scimitar, axe, hammer, dagger, poleaxe) "to be used by the mordor orcs only": hold them "with attack animations, randomized, and do not affect damage tier". Built in 0.0.45 (prompt: /mnt/project-files/lotrnpcs/prompts/mordor-weapons.md), seen in game 2026-11-03.

- Originals kept unchanged in tools/weapons/*.json; gen_testnpcs.py WEAPONS table (grip, rotation, attack anim, impact frame, hold anim) writes shapes/item/orcweapon/* and itemtypes/orcweapon-* (no creative tab, handbook excluded). ARMED_FACTIONS = {"mordororc"}.
- Cosmetic by design: src/Weapons.cs EntityBehaviorHeldWeapon. Server picks at random, stores watched attr lotrnpcsWeapon (re-picks if no longer listed); ONLY the client sets EntityAgent.RightHandItemSlot (DummySlot), so CO/vanilla server code never sees a held item. Melee task swaps animMeta per weapon; poleaxe holds holdbothhandslarge between attacks (stopped on swing/death).
- Anims (seraph-faceless): scimitar falx (impact frame 12.5), axe axehit (22.5), hammer hammerhit (17), dagger knifestab (10), poleaxe spearhit (10); speed = impact/30/0.4 so the blow lands at damagePlayerAtMs 400. Average blend, elementWeight upper body 10, legs 0.5, default weight 0.01 (Average anims pull unanimated elements toward rest by their weight).
- Hand math (verified vs vanilla falx): item matrix = anchor * T(origin) * S(scale) * T(ap.pos/16 + translation) * RotateXYZ(ap.rot + rotation) * T(-origin); RightHand ap rotationY -180; ModelTransform default origin is (0.5,0.5,0.5) unless JSON sets it. We use origin 0, scale 0.6, translation = -R*grip. Scimitar z -20 matches vanilla falx; axe/poleaxe x 180 so the blade leads.
- Textures: the models name paths that exist nowhere in the modpack (game:mord/rustiron, uruk/Iron, ...). 0.0.45 used vanilla stand-ins; 0.0.46 bundles the user's own copies (sent as their Model Creator textures.zip, 747 PNGs; we keep only the 9 used, unchanged, in tools/weapons/textures) as lotrnpcs:item/orcweapon/<lowercased path>. Only game:plain (dagger cord) is not in their set and keeps game:block/cloth/burlap/string. Origin of their textures unknown (looks like a mix of LOTR faction mods' art); credited to the server owner.
- Dagger: 0.0.46 roll (rotation x 180) is CORRECT (user 2026-11-03: "as of .46 the dagger is faced correctly"). 0.0.47 tried a reverse grip (z 180) on a misread; 0.0.48 reverted it.
- Poleaxe 0.0.48: hold anim holdbothhands (hands clasped, pole slanted across the body through both). 0.0.45's holdbothhandslarge spread the hands and the left missed the shaft (user screenshot). Preview: previews/poleaxe-hold-0.0.48.png. Spear thrust (spearhit) unchanged.
- Offline previews need the seraph body: build/gen/assets/lotrnpcs/shapes/entity/humanoid/seraph-faceless.json (from the build; the FA Races zip was removed in 0.0.114). Hand-fit search script idea: compute left-hand distance to the pole axis per anim.
- Was: 0.0.46 dagger rotation x 180 (user screenshot: "this sword is upside down"). Model as authored has spine +y, belly -y; the screenshot looked like my offline render to me, so likely taste rather than a math error (unconfirmed). Poleaxe guard pose seen in game and looked as intended.
- /mnt/project-files adds a C2PA "caBX" chunk to PNGs written there (pixels unchanged): verify synced PNGs by pixels, not cmp.
- Offline preview: tools/preview/vsrender.py + zoompreview.py/tipdir.py render the body skeleton posed by a shape animation holding an item. Sheet: /mnt/project-files/lotrnpcs/previews/weapons-0.0.45.png.
- bundle_lrc.py used to wipe all of shapes/ and textures/; it now clears only LRC's own shape (0.0.45) and texture (0.0.46) folders.

Related: [[npc-ai-tuning]], [[lotrnpcs-build]], [[npc-mod-maker-goals]]
