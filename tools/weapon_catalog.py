"""The owner's custom melee weapons, shared by lotrnpcs (NPCs hold them, gen_testnpcs.py) and lotrweapons (player
items): each weapon's model, texture, grip, roll, scale, swing and hold, the hand transform worked out from them, and
the writer that turns a model into a game shape with its textures. One table for both mods, so a grip fix reaches both.

Loaded by path (the generators run with python -I, so tools/ isn't on sys.path). Shields and bows still live in
gen_testnpcs.py; they move here when lotrweapons gets them.
"""
import copy
import json
import math
import os
import shutil
import sys

# Weapons (0.0.45, user, 2026-11-03): Mordor orcs carry one of the user's five test models, picked at random per orc
# and kept for life, with an attack animation to match. They're only for looks ("do not affect damage tier"): the
# client draws the weapon in the hand and the server never sees it (Weapons.cs).
# The user's models are kept unchanged in tools/weapons. Most of their textures name files that exist neither in vanilla
# nor in the installed faction mods. The user sent their own copies of those (0.0.46), kept unchanged in
# tools/weapons/textures at the path the models name: each is bundled as lotrnpcs:item/orcweapon/<that path>.
# WEAPON_TEXTURES swaps the rest for a vanilla texture of the same kind that other mods in the server's modpack use.
# Keys are compared in lowercase.
WEAPON_TEXTURES = {
    # The dagger's cord wraps and knot: the user's textures don't include "plain".
    "game:plain": "game:block/cloth/burlap/string",
}
# Textures the models name that are fine as they are.
WEAPON_TEXTURES_OK = {"game:block/metal/ingot/iron", "game:block/metal/tarnished/rusty-iron", "game:item/tool/handle",
                      "game:block/leather/plain", "game:block/cloth/burlap/string", "game:item/resource/hide/pelt"}
# How each weapon sits in the hand and swings. The models lie along +X like vanilla tools, centred on z = 8.
# grip: the point the hand closes on, in model units (x along the weapon, y across it).
# rotation: turns the weapon in the hand. Scimitar: tipped 20 degrees down, the way vanilla holds its falx. Axe and
# poleaxe: rolled over so the blade leads the swing (and faces forward in the poleaxe's guard pose). Dagger: rolled over
# too (0.0.46, user: "this sword is upside down" in game; 0.0.47's reverse grip was undone, user: "as of .46 the
# dagger is faced correctly").
# attack: the vanilla body animation swung with it, and impact the frame where the blow lands, so the animation speed
# can put that frame at DAMAGE_AT_MS. The poleaxe's poleaxechop (0.0.68) is our own two-handed chop, added to the body by
# bundle_lrc.py from tools/anims (it used vanilla's spear thrust until then). hold: an animation held between attacks (the poleaxe, about 2.3 blocks long in
# hand, is held slanted across the body in both clasped hands with vanilla's holdbothhands; 0.0.45's
# holdbothhandslarge spread the hands so the left one missed the shaft, user 2026-11-03 screenshot). Checked offline against vanilla's falx with the body's own skeleton.
WEAPON_SCALE = 0.6
# 0.0.65 (user, 2026-11-04: "These are all the Mordor orc replacement weapons and their textures."): the owner's new
# models, kept unchanged in tools/weapons under their own names, each painted with one texture of its own
# (tools/weapons/atlas, bundled as lotrnpcs:item/orcweapon/atlas/<weapon>). orc_dagger.json is the old dagger
# reshaped (same size and handle), so it keeps its grip and its roll; morc_knife_.json is new, a curved knife with a
# loop handle, held at the loop's middle and rolled over like the dagger. Grips are the middle of each model's
# handle wrap (checked offline with the body, previews/weapons-0.0.65.png). Was: scimitar (2.0, 0.75), axe
# (1.9, 0.75), hammer (1.1, 0.75), poleaxe (4.0, 0.75) on the 0.0.45 models.
# 0.0.76 (user, 2026-11-04: "have the right arm higher on the poleaxe while holding it"): between swings the poleaxe is
# held with poleaxehold (tools/anims, injected by bundle_lrc), the game's holdbothhands with the right arm re-solved so
# the right hand sits 0.25 blocks further up the shaft, same shaft line and angle (solved offline, error 0.03 px). The
# grip moves the same distance up the shaft (0.25 blocks / 0.6 scale = 6.67 px from 2.6), so the poleaxe itself stays
# where it was and the left hand, unchanged, now holds it below the right. In the chop the poleaxe slides 0.25 blocks
# down its own line, so both hands stay on the shaft. previews/poleaxe-hold-0.0.76.png.
POLEAXE_GRIP_X = round(2.6 + 0.25 * 16 / WEAPON_SCALE, 2)
# 0.0.96 (owner, 2026-11-07, screenshot of a dwarf with the warhammer standing far above its head: "Fix the way the
# poleaxes are held, this great hammer isnt held correctly"; chose "On the shoulder"): poleaxehold is now a shoulder
# carry. The right hand holds the shaft in front of the shoulder, elbow down by the ribs, and the shaft rests on top of
# the right shoulder, rising 28 degrees toward the back with the head behind. Solved offline for the human and dwarf
# bodies together (shaft within 0.4 px of the shoulder top on both, nothing through the head or torso;
# previews/shoulder-carry-0.0.96.png). Only the right arm and the item are held, so the left arm swings while walking.
# The poleaxe, the dwarven battleaxe and the warhammer use it; the chop starts from it. The captain's sword keeps the
# 0.0.76 two-handed guard, now named greatswordhold (owner flagged only the poleaxe-type weapons).
CARRY = {"holdElements": ["UpperArmR", "LowerArmR", "ItemAnchor"]}
WEAPONS = {
    "scimitar": {"model": "morc_scimitar.json", "atlas": "Morc Scimitar Texture.png", "name": "Orc scimitar",
                 "grip": (2.75, 0.75), "rotation": {"x": 180, "z": 20}, "attack": "falx", "impact": 12.5},
    "axe": {"model": "Morc Short Axe.json", "atlas": "Morc Short Axe Texture.png", "name": "Orc axe",
            "grip": (2.4, 0.75), "rotation": {"x": 180}, "attack": "axehit", "impact": 22.5},
    "hammer": {"model": "hammer_1.json", "atlas": "MORC Hammer 1 Texture.png", "name": "Orc hammer",
               "grip": (1.5, 0.75), "rotation": {}, "attack": "hammerhit", "impact": 17},
    "dagger": {"model": "orc_dagger.json", "atlas": "Orc Shortsword Texture.png", "name": "Orc dagger",
               "grip": (2.6, 0.95), "rotation": {"x": 180}, "attack": "knifestab", "impact": 10},
    "knife": {"model": "morc_knife_.json", "atlas": "Morc Knife.png", "name": "Orc knife",
              "grip": (2.5, 0.75), "rotation": {}, "attack": "knifestab", "impact": 10},
    "poleaxe": {"model": "poleaxe_1_reference.json", "atlas": "Poleaxe 1 Texture.png", "name": "Orc poleaxe",
                "grip": (POLEAXE_GRIP_X, 0.75), "rotation": {"x": 180}, "attack": "poleaxechop", "impact": 14, "hold": "poleaxehold",
                "twoHanded": True} | CARRY,
}
# The Misty Mountains orcs' weapons (0.0.75, user, 2026-11-04: yes to "give the Moria bow, sword and spear to the
# Misty Mountains orcs"; "moria = misty mountains"). The owner's models, packed against one texture each. Melee ranks
# pick one at random and keep their shield (neither takes both hands).
# Sword: lies along +X like the others, straight edge on -Y with the spurs on the back (+Y), so it needs no roll: the
# edge then leads the swing on the same side as the scimitar's (checked offline against the scimitar, which the owner
# confirmed in game in 0.0.66). Grip at the middle of the grip sleeve. Swung like the scimitar (falx).
# Spear: 60 px long (about 2.3 blocks in hand), head on +X. Held a little behind its middle (x -4) on the shaft's axis,
# level at the side, and thrust with the game's spearhit (impact frame 10, as the poleaxe had until 0.0.68). Between
# thrusts the right arm holds the game's spearidle pose (the arm at rest) so the walk's arm swing doesn't drive the butt
# into the ground (checked offline, walk frame 10). Only the right arm is held, so the shield arm moves as before.
MM_WEAPONS = {
    "mmsword": {"model": "misty_mountain_orc_sword.json", "atlas": "Moria Orc Sword Texture.png", "name": "Orc sword",
                "grip": (3.6, 0.9), "rotation": {}, "attack": "falx", "impact": 12.5},
    "mmspear": {"model": "moria_spear.json", "atlas": "Moria Spear Texture.png", "name": "Orc spear",
                "grip": (-4.0, 0.75), "rotation": {}, "attack": "spearhit", "impact": 10, "hold": "spearidle",
                "holdElements": ["UpperArmR", "LowerArmR", "ItemAnchor"]},
}
# 0.0.76 (user, 2026-11-04): the owner's Blockbench models, converted by tools/bbmodel_to_vs.py (originals in
# tools/weapons/source). grip is (x, y, z) on the haft's axis, measured on the converted model; rotation turns the model
# so it lies like the others (along +X, flat in X/Y, its edge or blade where the Mordor equivalent has it).
# Elven spear (elves): 87 px, so scaled to the Moria spear's length in hand (60 px at 0.6); its blade is wide in Z, so
# rolled 90 degrees into Y like the Moria spear's; held 43% up from the butt like the Moria spear; thrust.
# Dwarves: walking axe (blade toward -Z: rolled -90 so it ends where the Mordor axe's blade is after its own roll; held
# 10 px behind the head like the Mordor axe; chop), battleaxe (two-handed, double-bladed, rolled 90; the poleaxe's chop
# and hold; held at the handle wrap), dagger (along +X already, double-edged; at 1.25 blocks it slashes like the
# scimitar), boar spear (no texture in the file: painted with the game's wood and iron by bbmodel_to_vs --untextured
# until the owner sends its PNG; thrust like the Moria spear, held 43% up).
SPEAR_HOLD = {"hold": "spearidle", "holdElements": ["UpperArmR", "LowerArmR", "ItemAnchor"]}
ELF_WEAPONS = {
    # 0.0.94 (owner, 2026-11-06: "the whole spear becomes 60, dont change the size of the blade or hilt of the model"):
    # tools/weapons/source/elven_spear_60.bbmodel, the owner's original with the plain shaft cut 26.9 units at the butt
    # (texture cropped, butt cap moved up, the rest untouched), converted at build time ("source"), at the normal
    # weapon scale: 60 units, 2.25 blocks like the Moria spear. Grip 43% up from the butt on the shaft axis.
    "elfspear": {"source": "elven_spear_60.bbmodel", "name": "Elven spear", "grip": (1.15, 0.0, 11.39),
                 "rotation": {"x": 90}, "attack": "spearhit", "impact": 10} | SPEAR_HOLD,
}
DWARF_WEAPONS = {
    # 0.0.77 (owner: "the walking axe is also upside down"): rolled +90 instead of -90, the blade on the other side.
    "dwaxe": {"model": "dwarven_walking_axe.json", "atlas": "dwarven_walking_axe.png", "name": "Dwarven axe",
              "grip": (-3.0, 3.97, 7.37), "rotation": {"x": 90}, "attack": "axehit", "impact": 22.5,
              # 0.0.94 (owner: "the walking axe by 20%, as well as the two handed axe 20%. and the dwarf spear by 10%").
              "scale": round(WEAPON_SCALE * 1.2, 3)},
    "dwbattleaxe": {"model": "dwarven_battleaxe.json", "atlas": "dwarven_battleaxe.png", "name": "Dwarven battleaxe",
                    "grip": (-13.0, 1.05, 8.05), "rotation": {"x": 90}, "attack": "poleaxechop", "impact": 14,
                    "hold": "poleaxehold", "twoHanded": True, "scale": round(WEAPON_SCALE * 1.2, 3)} | CARRY,
    "dwdagger": {"model": "dwarven_dagger.json", "atlas": "dwarven_dagger.png", "name": "Dwarven dagger",
                 "grip": (-6.0, 2.5, 8.1), "rotation": {}, "attack": "falx", "impact": 12.5,
                 # 0.0.94 (owner: "the dwarven sword needs to drop down to dagger size"): the Mordor knife's length,
                 # 20.3 of its 33 units (0.76 blocks, was 1.24).
                 "scale": round(WEAPON_SCALE * 20.3 / 33.0, 3)},
    # 0.0.77 (owner: "Flip the boar spear 180 degrees so the blade points toward the ground instead of directly up as
    # it is now") turned it end over end (z 180); 0.0.79 (owner: "the dwarven boar spear is now held backwards since the
    # blade is behind the body instead of in front"): point forward again, the blade turned over instead (x 180, a half
    # turn about the shaft), so the side of the head that stood up now hangs toward the ground.
    "dwspear": {"model": "dwarven_boar_spear.json", "name": "Dwarven boar spear", "grip": (-8.6, 0.0, 8.0),
                "rotation": {"x": 180}, "attack": "spearhit", "impact": 10, "scale": round(WEAPON_SCALE * 1.1, 3)} | SPEAR_HOLD,
    # 0.0.95 (owner, 2026-11-07: dwarven_warhammer_iron_varient.bbmodel and dwarven-shorthammer_iron.bbmodel; chose to
    # replace the war pick stand-in, warhammer two-handed and shorthammer one-handed): tools/weapons/source, converted at
    # build time, at the normal weapon scale (no size given). Both lie along +X with the head wide in Z, the flat face
    # toward -Z like the walking axe's blade, so rolled x 90 like the dwarf axes (the roll the owner confirmed for the
    # walking axe in 0.0.77). Warhammer: 56.2 units (2.1 blocks), pommel at x -43; the battleaxe's two-handed hold and
    # chop, right hand at x -28 (27% up from the butt, as the battleaxe and poleaxe are held), so the left hand, 0.25
    # blocks lower, is on the shaft above the pommel wrap. Shorthammer: 31.6 units (1.2 blocks), held near the end of
    # the handle (x 4, just above the pommel), swung like the war pick it replaces.
    "dwwarhammer": {"source": "dwarven_warhammer_iron.bbmodel", "name": "Dwarven warhammer", "grip": (-28.0, 2.05, 8.05),
                    "rotation": {"x": 90}, "attack": "poleaxechop", "impact": 14, "hold": "poleaxehold", "twoHanded": True} | CARRY,
    "dwshorthammer": {"source": "dwarven_shorthammer_iron.bbmodel", "name": "Dwarven shorthammer", "grip": (4.0, 1.05, 8.05),
                      "rotation": {"x": 90}, "attack": "hammerhit", "impact": 17},
}
# 0.0.112 (owner, 2026-11-09: the steel warhammer and shorthammer, "let the steel variants only be used by captains"):
# the same geometry as the iron ones (checked: same boxes), so the same holds; captains carry these instead of iron.
DWARF_WEAPONS |= {
    "dwwarhammersteel": dict(DWARF_WEAPONS["dwwarhammer"], source="dwarven_warhammer_steel.bbmodel", name="Dwarven steel warhammer"),
    "dwshorthammersteel": dict(DWARF_WEAPONS["dwshorthammer"], source="dwarven_shorthammer_steel.bbmodel", name="Dwarven steel shorthammer"),
}
# 0.0.112 (owner, 2026-11-09, sent with the steel hammers): one-handed swords and spears for Gondor, Rohan, the Black
# Numenoreans and elves, and the Easterling axe, each replacing its faction's game stand-in (owner chose "replace
# stand-ins"). Measured on the converted models (offline render and boxes):
# - The swords (Gondor iron and steel, Rohan, Black Numenorean steel, Morgul blade) lie along +X with the blade and
#   crossguard flat in X/Z, so rolled x 90 into X/Y like the Haradrim swords; straight and double-edged. Held at the
#   middle of the handle (x 0 to 5, y and z centred on 0 and 8). 38 to 45 units (1.4 to 1.7 blocks), normal scale.
# - The elven longsword's handle runs from x 1 to 17 and its curved blade from 18 to 38, so it is two-handed, held like
#   the Captain's sword: right hand by the guard (x 14.5), the left 0.25 blocks lower on the handle. Its convex (edge)
#   side is +Z, so x 90 puts the edge on -Y, where the Haradrim swords' edges lead after their own roll.
# - The spears lie along +X with the head wide in Y already (no roll), held 43% up from the butt like the Moria spear:
#   Numenorean 58 units, Rohan 65 units.
# - The Easterling axe is 68 units (2.6 blocks) with a spear tip and a hook, its blade toward +Y already (no roll): a
#   halberd, so two-handed with the battleaxe's hold and chop, both hands on the red wrap (x 20.8 to 27.6).
_SWORD = {"rotation": {"x": 90}, "attack": "falx", "impact": 12.5}
NEW_WEAPONS = {
    "gdsword": {"source": "gondor_iron_sword.bbmodel", "name": "Gondorian sword", "grip": (2.5, 0.0, 8.0)} | _SWORD,
    "gdsteelsword": {"source": "gondor_steel_sword.bbmodel", "name": "Gondorian steel sword", "grip": (2.5, 0.0, 8.0)} | _SWORD,
    "rhsword": {"source": "rohan_sword.bbmodel", "name": "Rohirric sword", "grip": (2.5, 0.0, 8.0)} | _SWORD,
    "bnsteelsword": {"source": "black_numenorean_steel_sword.bbmodel", "name": "Black Numenorean steel sword", "grip": (2.5, 0.0, 8.0)} | _SWORD,
    "morgulblade": {"source": "morgul_blade.bbmodel", "name": "Morgul blade", "grip": (2.5, 0.0, 8.0)} | _SWORD,
    "elflongsword": {"source": "elven_longsword.bbmodel", "name": "Elven longsword", "grip": (14.5, 0.0, 8.3),
                     "rotation": {"x": 90}, "attack": "poleaxechop", "impact": 14, "hold": "greatswordhold", "twoHanded": True},
    "bnspear": {"source": "numenorean_spear.bbmodel", "name": "Numenorean spear", "grip": (-6.8, 0.0, 8.0),
                "rotation": {}, "attack": "spearhit", "impact": 10} | SPEAR_HOLD,
    "rhspear": {"source": "rohan_spear.bbmodel", "name": "Rohirric spear", "grip": (-1.2, 0.75, 8.0),
                "rotation": {}, "attack": "spearhit", "impact": 10} | SPEAR_HOLD,
    "eastaxe": {"source": "easterling_axe.bbmodel", "name": "Easterling axe", "grip": (27.0, 0.75, 8.0),
                "rotation": {}, "attack": "poleaxechop", "impact": 14, "hold": "poleaxehold", "twoHanded": True} | CARRY,
}
# 0.0.113 (owner, 2026-11-10: rohan_axe.bbmodel and "Easterling Scimitar.bbmodel", sent without notes; they fill the
# model checklist's "Rohan axe, one-handed" and "Easterlings saber" slots). Measured on the boxes:
# - Rohan axe: laid out like the orc axe (handle along +X, head at the far end, blade toward +Y, centred on z 8), so
#   the orc axe's roll, swing and hold; grip on the middle of the wrap (x -1 to 4.1). 41.5 units, 1.56 blocks at the
#   normal scale (the orc axe is 24 units, 0.91 blocks; the Rohirric sword 1.4-1.7).
# - Easterling scimitar: along +X, flat in X/Y (z 1.6 to 3.4), the tip curving up to +Y, so the cutting edge (its
#   "Sharp" boxes) is on the convex -Y side already, where the other swords' edges end up after their roll: no roll.
#   Grip in the middle of the handle (x 2 to 5.6, y 0.6 to 1.6, z 2.5). 24.7 units, 0.93 blocks. One-handed, swung
#   like the other swords, so it goes with the Easterling shield.
NEW_WEAPONS |= {
    "rhaxe": {"source": "rohan_axe.bbmodel", "name": "Rohirric axe", "grip": (1.5, 0.75, 8.0),
              "rotation": {"x": 180}, "attack": "axehit", "impact": 22.5},
    "eastscimitar": {"source": "easterling_scimitar.bbmodel", "name": "Easterling scimitar", "grip": (3.8, 1.1, 2.5),
                     "rotation": {}, "attack": "falx", "impact": 12.5},
}
# 0.0.94 (owner, 2026-11-06: Haradrim_Spear.bbmodel "for haradrim only"; "replace the default option they have now for
# spear / pike"): kept as tools/weapons/source/haradrim_spear.bbmodel and converted at build time. It lies along +Z,
# blade at the far end and wide in X, so turned y 90 (along +X, blade forward) and x 90 (blade wide in Y, like the
# Moria spear's). 63 units, 2.36 blocks. Grip 43% up from the butt on the shaft axis, like the Moria spear.
HARADRIM_WEAPONS = {
    "hdspear": {"source": "haradrim_spear.bbmodel", "name": "Haradrim spear", "grip": (11.0, 1.0, 26.2),
                "rotation": {"x": 90, "y": 90}, "attack": "spearhit", "impact": 10} | SPEAR_HOLD,
    # 0.0.97 (owner, 2026-11-07: haradrim_sword.bbmodel and haradrim_curved_sword.bbmodel, sent without notes): both lie
    # along +X with the blade flat in X/Y. The sword's blade sweeps down to its tip with the silver edge strips on its
    # top (+Y), the convex side, like a scimitar; the curved sword bends up past the guard and hooks down at the tip,
    # convex side +Y too. So both are rolled x 180 like the Mordor scimitar, putting the edge where the scimitar's and
    # the Moria sword's lead the swing (my reading: the curved sword has no marked edge). Held at the middle of the grip
    # (sword x 0 to 6, curved sword x 0.2 to 3.5), normal weapon scale: sword 41 units (1.54 blocks), curved sword 34
    # units (1.28 blocks). One-handed, swung like the scimitar (falx), so they go with a shield.
    "hdsword": {"source": "haradrim_sword.bbmodel", "name": "Haradrim sword", "grip": (3.0, 0.75, 8.0),
                "rotation": {"x": 180}, "attack": "falx", "impact": 12.5},
    "hdcurvedsword": {"source": "haradrim_curved_sword.bbmodel", "name": "Haradrim curved sword", "grip": (1.85, 0.75, 8.0),
                      "rotation": {"x": 180}, "attack": "falx", "impact": 12.5},
}
# 0.0.95 (owner, 2026-11-06: anduril.bbmodel, "Gondorian captain sword option. Rename it in game to captains sword.",
# "Its a greatsword not a regular sword", "53\" total length"): tools/weapons/source/gondor_captain_sword.bbmodel, along
# +X with the blade and crossguard in X/Y (no roll). 70.3 units overall, so scale 0.306 makes it 1.35 blocks (53 inches
# at 1 block = 1 m). Two-handed with the battleaxe's hold and chop: right hand at the top of the handle (x 6.6, by the
# guard), so the left hand, 0.25 blocks (13 units) lower, holds the pommel (x -8.2 to -4.7): a hand-and-a-half grip.
GONDOR_WEAPONS = {
    "gdcaptsword": {"source": "gondor_captain_sword.bbmodel", "name": "Captain's sword", "grip": (6.6, 0.0, 8.0),
                    "rotation": {}, "attack": "poleaxechop", "impact": 14, "hold": "greatswordhold", "twoHanded": True,
                    # 0.0.96 (owner, 2026-11-07: "the gondorian anduril sword is also very small"; chose 1.5x): 0.459, 2.0
                    # blocks. The left hand, 0.25 blocks below the right, now holds the handle (x -2.1), above the pommel.
                    "scale": 0.459},
}
# Every melee weapon made from the owner's own models (the NPCs' game stand-ins are in gen_testnpcs.py).
MELEE = {**WEAPONS, **MM_WEAPONS, **ELF_WEAPONS, **DWARF_WEAPONS, **HARADRIM_WEAPONS, **GONDOR_WEAPONS, **NEW_WEAPONS}


# Shields and bows (moved from gen_testnpcs.py for lotrweapons, 2026-11-14; unchanged).
# The Mordor orcs' shield (0.0.55, user, 2026-11-03: "Take this shield model for the mordor orcs. its also a visual
# thing, and its a toggle-able accessory. properly rig it, it cant be used in conjunction with the poleaxe."). The
# user's model, unchanged in tools/weapons/orc_shield.json, a kite shield along +X (point at -X) with its face to +Z
# and a grip bar behind it. Held in the left hand at the grip bar's middle (3.7, 0.75, 6) like a weapon's handle, so
# the shield lies across the forearm and turns with it (user, 0.0.56: "The shield should rotate with their arm ... it
# is parallel with the arm instead of perpendicular"): with the arm hanging, top forward, point back, face out to the
# left, at the weapons' 0.6 scale. Solved and checked offline from four sides and
# in the swing and walk poses (tools/preview); the numbers come from that solve.
# 0.0.58 (user, 2026-11-03: "There is an issue with the shield clipping with the npc"): the 0.0.56 solve used the bare
# body, where the shield's back sat within a pixel of the forearm. 0.0.58 slid it 2.5 pixels out along its face,
# which left the grip bar 1.2 pixels off the hand (user, 0.0.58: "the mordor orc shield now floats off of the body").
# 0.0.59: slid 1.0 pixel instead, same rotation. The grip bar rests on the hand's surface, and the shield clears the
# bare arm and side in every idle, walk and sprint frame, with half a pixel of armor allowed for (checked offline,
# tools/preview/shieldmeasure.py; previews/shield-0.0.59.png). Was (0.2359, 0.0682, -0.3688), then (0.2314, 0.0591, -0.1086).
# 0.0.71: the owner's morc_shield.json and "Mordor Orc Shield Texture.png" (one packed texture, same geometry as the
# 0.0.55 orc_shield.json, so the same hold).
SHIELD = {"model": "morc_shield.json", "atlas": "Mordor Orc Shield Texture.png", "name": "Orc shield",
          "hand": {"translation": {"x": 0.2341, "y": 0.0646, "z": -0.2647}, "rotation": {"x": 2.0, "y": 179.0, "z": -177.9},
                   "origin": {"x": 0, "y": 0, "z": 0}, "scale": WEAPON_SCALE}}


# The Misty Mountains orcs' shield (0.0.71, the owner's misty_mountain_orc_shield.json). 0.0.72: the owner re-exported
# it against the packed "Moria Orc Shield Texture.png" (same geometry), so it draws from that one texture. Same hold as
# the Mordor shield, moved to this model's grip (3.25, 2.4, 6.3) instead of (3.7, 0.75, 6.0), with 0.0.59's 1 px offset.
MM_SHIELD = {"model": "misty_mountain_orc_shield.json", "atlas": "Moria Orc Shield Texture.png", "name": "Orc shield",
             "hand": {"translation": {"x": 0.2025, "y": 0.1673, "z": -0.2804}, "rotation": {"x": 2.0, "y": 179.0, "z": -177.9},
                      "origin": {"x": 0, "y": 0, "z": 0}, "scale": WEAPON_SCALE}}


def place_grip(hand, old_grip, new_grip, rotation):
    """A held item's transform turned to rotation and moved so new_grip lands where old_grip did under hand."""
    def rot(v, r):
        rx, ry, rz = (math.radians(r.get(a, 0)) for a in "xyz")
        ry -= math.pi  # the hand attachment points' rotationY of -180
        x, y, z = v
        x, y = x * math.cos(rz) - y * math.sin(rz), x * math.sin(rz) + y * math.cos(rz)
        x, z = x * math.cos(ry) + z * math.sin(ry), -x * math.sin(ry) + z * math.cos(ry)
        y, z = y * math.cos(rx) - z * math.sin(rx), y * math.sin(rx) + z * math.cos(rx)
        return x, y, z
    a = rot(tuple(v / 16 for v in old_grip), hand["rotation"])
    b = rot(tuple(v / 16 for v in new_grip), rotation)
    t = hand["translation"]
    return dict(hand, rotation={k: round(v, 2) for k, v in rotation.items()},
                translation={k: round(t[k] + a[i] - b[i], 4) for i, k in enumerate("xyz")})


# 0.0.76 shields (owner's Blockbench models): upright, face +Z, straps on the back, so turned -90 degrees about Z on
# top of the Mordor shield's hold (their long axis is Y where the Mordor shield's is X, point or bottom to -X) and moved
# so their handle lands where the Mordor grip bar does. Handles measured on the converted models: the kite shield's two
# upright straps (x 5.4 to 10.6), held between them on the centreline; the tower shield's handle bar; the iron shield's
# grip bar on its two posts.
MORDOR_GRIP = (3.7, 0.75, 6.0)


# Their straps sit closer to the shield than the Mordor grip post, so each is slid out along its face by SHIELD_OUT px
# to clear the forearm with half a pixel of armor, as the Mordor shield does (tools/preview/shieldmeasure.py).
SHIELD_OUT = 1.4


# 0.0.96 (owner, 2026-11-07, screenshot of a dwarf recruit: "move the dwarven shields a little bit closer to their
# models, same with the elven shields, and the gondor shields"): SHIELD_OUT kept every point clear of the forearm with
# half a pixel of sleeve, but on each of these a small part on the back (the tower shield's lower strap, the iron
# shield's grip posts, the curved shield's rim, the round shield's handle, the elven shield's bar posts) touched first,
# leaving the plate itself about a pixel off the arm. These are moved in by SHIELD_IN px (model units, x0.6 in the
# world): the plate then sits on the sleeve, and those back parts sink into the arm, hidden behind the shield.
# Measured with the race's own body (dwarf, elf, human). The curved Gondor shield's sides wrap toward the arm, so it
# moves less.
# 0.0.100 (owner, 2026-11-08: "make sure the positioning of all the weapons seems correct on your end, with no floating
# gaps"): measured every held item against the hand (bottom 3 px of the lower arm) on the human, dwarf and elf bodies.
# The Numenorean kite, Easterling and Gondorian shields' grips stopped 0.46 to 0.65 px short of the hand (the left hand
# visibly not touching the handle). Moved in like the others: kite and Easterling 1.0, Gondor 0.75 -> 1.75. Now every
# shield touches the hand; their backs sink at most 0.44 px into the forearm (Mordor's 0.38, Haradrim's 0.69, hidden
# behind the shield) and 0.31 into the upper arm (the dwarven tower shield's 0.25).
SHIELD_IN = {"dwtowershield": 1.0, "dwshield": 1.0, "elfshield": 1.0, "boromirshield": 1.0, "gondorshield": 1.75,
             "kiteshield": 1.0, "eastshield": 1.0}


_NEW_SHIELD_ROT = dict(SHIELD["hand"]["rotation"], z=SHIELD["hand"]["rotation"]["z"] - 90)


KITE_SHIELD = {"model": "numenorean_kite_shield.json", "atlas": "numenorean_kite_shield.png", "name": "Numenorean shield",
               "hand": place_grip(SHIELD["hand"], MORDOR_GRIP, (8.0, 21.9, 12.8 - SHIELD_OUT + SHIELD_IN["kiteshield"]), _NEW_SHIELD_ROT)}


DW_TOWER_SHIELD = {"model": "dwarven_tower_shield.json", "atlas": "dwarven_tower_shield.png", "name": "Dwarven tower shield",
                   "hand": place_grip(SHIELD["hand"], MORDOR_GRIP, (5.25, 18.0, 7.3 - SHIELD_OUT + SHIELD_IN["dwtowershield"]), _NEW_SHIELD_ROT)}


DW_IRON_SHIELD = {"model": "dwarven_iron_shield.json", "atlas": "dwarven_iron_shield.png", "name": "Dwarven shield",
                  "hand": place_grip(SHIELD["hand"], MORDOR_GRIP, (7.0, 17.85, 4.9 - SHIELD_OUT + SHIELD_IN["dwshield"]), _NEW_SHIELD_ROT)}


# 0.0.77 (owner: "here is a model for the easterling shield", easternlingshieldV1.bbmodel, kept as
# tools/weapons/source/easterling_shield.bbmodel): built like the tower shield, with the same upright handle bar on the
# back (x 6 to 6.5, y 15 to 17, z 6.78 to 7.98 on the converted model), so the tower shield's hold at that bar's middle.
EASTERLING_SHIELD = {"model": "easterling_shield.json", "atlas": "easterling_shield.png", "name": "Easterling shield",
                     "hand": place_grip(SHIELD["hand"], MORDOR_GRIP, (6.25, 16.0, 7.38 - SHIELD_OUT + SHIELD_IN["eastshield"]), _NEW_SHIELD_ROT)}


# 0.0.94 (owner's models, 2026-11-05/06, converted at build time from tools/weapons/source): all three are upright
# with the face to +Z and the handle behind, like the tower shield, so the same hold at their own handle, slid out by
# SHIELD_OUT. Gondor's curved shield (gondorianshieldV2) has a flat grip plate behind the middle (x 4.5 to 10.5, y 13.7
# to 19.7, z 7.8 to 8): held at its middle, as the tower shield's handle sits 0.5 behind its plate. Boromir's round
# shield (boromir_v6): its handle bar (4.5 to 7.5, 8.5 to 9.5, z 2 to 3). The elven shield: the bar on two posts
# behind it (x 7.5 to 8.5, y 18.5 to 30.5, z 0 to 1); it is the tallest shield (39 units, 1.48 blocks: tower 32).
# tools/preview/shieldmeasure.py: Gondor's as the tower shield (worst 0.40 px into a 1 px armor margin), Boromir's and
# the elven one clear. Lowest point while walking or running: Gondor's at the feet' level, the elven one 1 px below.
GONDOR_SHIELD = {"source": "gondorian_shield.bbmodel", "name": "Gondorian shield",
                 "hand": place_grip(SHIELD["hand"], MORDOR_GRIP, (7.5, 16.68, 7.3 - SHIELD_OUT + SHIELD_IN["gondorshield"]), _NEW_SHIELD_ROT)}


BOROMIR_SHIELD = {"source": "boromir_shield.bbmodel", "name": "Gondorian round shield",
                  "hand": place_grip(SHIELD["hand"], MORDOR_GRIP, (6.0, 9.0, 2.5 - SHIELD_OUT + SHIELD_IN["boromirshield"]), _NEW_SHIELD_ROT)}


ELF_SHIELD = {"source": "elven_shield.bbmodel", "name": "Elven shield",
              "hand": place_grip(SHIELD["hand"], MORDOR_GRIP, (8.0, 24.5, 0.5 - SHIELD_OUT + SHIELD_IN["elfshield"]), _NEW_SHIELD_ROT)}


# 0.0.97 (owner, 2026-11-07: haradrim_light_shield.bbmodel and haradrim_heavy_shield.bbmodel, no notes): both face +Z
# with the grip and a forearm strap behind. Each turned so it stands along the forearm with the strap toward the
# elbow (checked offline: the forearm runs from the grip toward the strap): the light shield (upright, long axis Y,
# strap below the grip) on the Mordor shield's own rotation, the heavy wicker shield (long axis X, strap at x 7.6)
# a quarter turn further (+90). Held at the grip's middle: light (10, 4.75, 5.9), heavy (16, 0.75, 5.55). Moved in
# toward the arm until the back rests on the sleeve (0.0.96's rule; tools: shieldfit-style measurement on the human
# body): light 0.6 px (crosspiece and back rim sink at most 0.45), heavy 0.5 px (battens at most 0.15; the grips and
# straps sit on the hand and forearm as modelled).
HD_LIGHT_SHIELD = {"source": "haradrim_light_shield.bbmodel", "name": "Haradrim light shield",
                   "hand": place_grip(SHIELD["hand"], MORDOR_GRIP, (10.0, 4.75, 5.9 + 0.6), SHIELD["hand"]["rotation"])}


HD_HEAVY_SHIELD = {"source": "haradrim_heavy_shield.bbmodel", "name": "Haradrim heavy shield",
                   "hand": place_grip(SHIELD["hand"], MORDOR_GRIP, (16.0, 0.75, 5.55 + 0.5),
                                      dict(SHIELD["hand"]["rotation"], z=SHIELD["hand"]["rotation"]["z"] + 90))}


# 0.0.112 (owner, 2026-11-09: "replace the black numenorean shield with the new one"; black nemenorian shield.bbmodel):
# a flat plate with a rim and a red emblem on its +Y face, point toward +Z, and no strap or handle. Held as the kite
# shield: first turned x 90 (face +Y to the kite's +Z, point +Z to its -Y), folded into one rotation (solved offline,
# hold @ Rx(90) = Rx(x) Ry(y - 180) Rz(z)); the grip on the centreline the kite's share of the length below the top
# (z 9.96), 0.1 behind the rim's back (y 16.0) as the kite's grip is behind its straps. Checked offline against the
# human body over idle, walk, sprint and attack poses (shieldfit): at most 0.34 px into a 1 px torso / 0.5 px arm armor
# margin (the kite shield: 0.55).
BN_SHIELD = {"source": "black_numenorean_shield.bbmodel", "name": "Black Numenorean shield",
             "hand": place_grip(SHIELD["hand"], MORDOR_GRIP, (8.0, 16.0, 9.96), {"x": -62.55, "y": 267.67, "z": 154.53})}


SHIELDS = {"shield": SHIELD, "mmshield": MM_SHIELD, "kiteshield": KITE_SHIELD, "bnshield": BN_SHIELD, "dwtowershield": DW_TOWER_SHIELD,
           "dwshield": DW_IRON_SHIELD, "eastshield": EASTERLING_SHIELD, "gondorshield": GONDOR_SHIELD,
           "boromirshield": BOROMIR_SHIELD, "elfshield": ELF_SHIELD, "hdlightshield": HD_LIGHT_SHIELD,
           "hdheavyshield": HD_HEAVY_SHIELD}


# The archers' bow (0.0.55): our own simple model (tools/weapons/orc_bow.json, dark wood, leather grip, iron nocks),
# since no bow model was supplied; the owner can send one to replace it. Held in the right hand as vanilla holds bows,
# grip at (0, 0, 8). 0.0.59 (user, 2026-11-03: "update the animations of the bow to be at rest until it is drawn to
# fire. then an animation to draw back and fire"): two items with the same model. The rest bow is in the hand of the
# idling, walking arm. 0.0.61 (user, 2026-11-03: "for the bow do option C", of three holds in
# previews/bow-idle-options.png; 0.0.59's upright hold read as a thin stick in game): carried level at the side, front
# end tipped 20 degrees down, string up, turned 15 degrees so the back end swings clear of the leg, grip in the hand
# (tools/preview/bowrest.py <body> <bow> 20 trail 15: clear of the legs and torso in every idle, walk and sprint
# frame; previews/bow-rest-0.0.61.png). Was upright along the leg: rotation (178.7, -161.1, -104.2). The drawn bow, shown only while drawing and for the first moment of the release (DRAWN_WHILE), has
# the 0.0.55 full-draw transform: stave upright with the string toward the archer (vanilla's bowaim pose). Until
# 0.0.58 the archer held the bow out in front between shots (holdinglanternrighthand), which is gone.
# 0.0.71 (user, 2026-11-04: "why do they still have the test bow. I gave you a json file called orc_bow"): the owner's
# own orc_bow model (86 elements, 37 px, grip at (2.6, 0.9, 8), limbs and string on -Y), with the nine textures it
# names copied from the owner's texture pack (the bundled "Mordor Orc Bow Texture.png" is a packed sheet whose matching
# JSON wasn't sent; pelt and string are vanilla). Same holds as before, re-solved for its grip and string side:
# rest = bowrest.py 20 trail 15 with BOW_GRIP=2.6,0.9,8 BOW_STRING=-1; drawn = 0.0.55's rotation rolled 180 degrees
# about the stave, translated to the new grip.
BOW = {"model": "orc_bow.json", "name": "Orc bow",
       "hand": {"translation": {"x": 0.1628, "y": 0.0973, "z": -0.4936}, "rotation": {"x": -1.9, "y": 176.1, "z": -157.9},
                "origin": {"x": 0, "y": 0, "z": 0}, "scale": WEAPON_SCALE}}


BOW_DRAWN = {"model": "orc_bow.json", "name": "Orc bow", "shape": "bow",
             "hand": {"translation": {"x": 0.2316, "y": 0.1989, "z": -0.4317}, "rotation": {"x": 14.1, "y": 170.1, "z": -168.4},
                      "origin": {"x": 0, "y": 0, "z": 0}, "scale": WEAPON_SCALE}}


# The string pulled back (0.0.73, user, 2026-11-04: "whenever the bow is drawn back, can the string on it change to a
# pulled back state?"). Measured offline (the bow on the seraph body, its full-draw hold, vanilla bowaim over DRAW_MS):
# the draw hand reaches the string at frame 9 and pulls it straight back, in the string's plane near the grip's middle,
# to y -16.0 at frame 15, where the pose holds. Three bows show the string as two segments from the tips to the apex at
# x 2.2, z 8, each from the frame the hand gets that far: a third (y -8.5, frame 11), two thirds (-12.3, frame 13) and
# the full draw (-16.0, frame 15). The release and the shove show the straight drawn bow (the string snaps back).
DRAW_MS_FOR_POSES = 1100  # = DRAW_MS below (asserted)


BOW_STRING_APEX_X = 2.2


BOW_PULLS = [("bow-pull1", -8.5, 11), ("bow-pull2", -12.3, 13), ("bow-pull3", -16.0, 15)]


BOW_PULL_ITEMS = {name: {"model": "orc_bow.json", "name": "Orc bow", "pull": y, "hand": BOW_DRAWN["hand"]} for name, y, _ in BOW_PULLS}


# The Misty Mountains archers' bow (0.0.75, the owner's moria_bow.json, packed against "Moria Bow Texture.png"): 47 px
# long, grip at (3.25, 0.8, 8), string on -Y at y -9.2 (deeper than the Mordor bow's -5.8). Same rest and full-draw
# holds as the Mordor bow, moved to this grip (move_grip, the way the Misty Mountains shield was moved in 0.0.71). The
# draw hand is where it is for the Mordor bow, so the pulled string follows it from where it reaches this deeper
# string (frame 11.3 of 16): the apex at x 2.85 (2.2 moved by the grip) and y -10.5, -13.3, -16.1 at frames 12, 13.5
# and 15 (the Mordor bow's hand positions, moved by the grip, read between its measured frames 11, 13 and 15).
MM_BOW_GRIP, ORC_BOW_GRIP = (3.25, 0.8, 8.0), (2.6, 0.9, 8.0)


def move_grip(hand, old_grip, new_grip):
    """A held item's transform with the item moved so new_grip lands where old_grip did (same rotation)."""
    rx, ry, rz = (math.radians(hand["rotation"].get(a, 0)) for a in "xyz")
    ry -= math.pi
    x, y, z = ((o - n) / 16 for o, n in zip(old_grip, new_grip))
    x, y = x * math.cos(rz) - y * math.sin(rz), x * math.sin(rz) + y * math.cos(rz)
    x, z = x * math.cos(ry) + z * math.sin(ry), -x * math.sin(ry) + z * math.cos(ry)
    y, z = y * math.cos(rx) - z * math.sin(rx), y * math.sin(rx) + z * math.cos(rx)
    t = hand["translation"]
    return dict(hand, translation={"x": round(t["x"] + x, 4), "y": round(t["y"] + y, 4), "z": round(t["z"] + z, 4)})


MM_BOW = {"model": "moria_bow.json", "atlas": "Moria Bow Texture.png", "name": "Orc bow",
          "hand": move_grip(BOW["hand"], ORC_BOW_GRIP, MM_BOW_GRIP)}


MM_BOW_DRAWN = {"model": "moria_bow.json", "name": "Orc bow", "shape": "mmbow",
                "hand": move_grip(BOW_DRAWN["hand"], ORC_BOW_GRIP, MM_BOW_GRIP)}


MM_BOW_APEX_X = round(BOW_STRING_APEX_X + MM_BOW_GRIP[0] - ORC_BOW_GRIP[0], 2)


MM_BOW_PULLS = [("mmbow-pull1", -10.5, 12), ("mmbow-pull2", -13.3, 13.5), ("mmbow-pull3", -16.1, 15)]


MM_BOW_PULL_ITEMS = {name: {"model": "moria_bow.json", "atlas": "Moria Bow Texture.png", "name": "Orc bow", "pull": y,
                            "apexX": MM_BOW_APEX_X, "atlasName": "mmbow", "hand": MM_BOW_DRAWN["hand"]} for name, y, _ in MM_BOW_PULLS}


def pull_string(shape, apex_y, apex_x=None):
    """The model's straight "bowstring" replaced by two segments from its ends to the apex (x apex_x, by default
    BOW_STRING_APEX_X, and z 8)."""
    apex_x = BOW_STRING_APEX_X if apex_x is None else apex_x
    string = next(e for e in shape["elements"] if e["name"] == "bowstring")
    shape["elements"].remove(string)
    y_mid = (string["from"][1] + string["to"][1]) / 2
    half_t = (string["to"][1] - string["from"][1]) / 2
    for name, tip_x in (("bowstring top", string["to"][0]), ("bowstring bottom", string["from"][0])):
        dx, dy = apex_x - tip_x, apex_y - y_mid
        length = math.hypot(dx, dy)
        shape["elements"].append({
            "name": name, "from": [tip_x, round(y_mid - half_t, 4), string["from"][2]],
            "to": [round(tip_x + length, 4), round(y_mid + half_t, 4), string["to"][2]],
            "rotationOrigin": [tip_x, round(y_mid, 4), 8], "rotationZ": round(math.degrees(math.atan2(dy, dx)), 2),
            "faces": copy.deepcopy(string["faces"])})


WEAPON_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "weapons")
WEAPON_TEXTURE_DIR = os.path.join(WEAPON_DIR, "textures")
WEAPON_ATLAS_DIR = os.path.join(WEAPON_DIR, "atlas")
WEAPON_SOURCE_DIR = os.path.join(WEAPON_DIR, "source")


def _load_bbmodel_converter():
    """tools/bbmodel_to_vs.py, loaded by path (gen runs with python -I, so its folder isn't on sys.path)."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("bbmodel_to_vs", os.path.join(os.path.dirname(os.path.abspath(__file__)), "bbmodel_to_vs.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


BBMODEL = _load_bbmodel_converter()
# Packed textures live in atlas/. gen also looks in pending/, because the text patch the project keeps between rebases
# can't move PNGs: a texture wired in mid-patch can stay there until the next rebase moves it.
WEAPON_ATLAS_DIRS = [WEAPON_ATLAS_DIR, os.path.join(WEAPON_DIR, "pending")]


def atlas_png(file_name):
    for folder in WEAPON_ATLAS_DIRS:
        if os.path.exists(os.path.join(folder, file_name)):
            return os.path.join(folder, file_name)
    sys.exit(f"weapon texture {file_name} is in none of {WEAPON_ATLAS_DIRS}")


def own_weapon_textures():
    """The user's textures for the weapon models, by the lowercase "game:" path the models name them with."""
    found = {}
    for root, _, files in os.walk(WEAPON_TEXTURE_DIR):
        for name in files:
            if name.lower().endswith(".png"):
                rel = os.path.relpath(os.path.join(root, name), WEAPON_TEXTURE_DIR)[:-4].replace(os.sep, "/")
                found["game:" + rel.lower()] = os.path.join(root, name)
    return found


def hand_transform(grip, rotation, scale=WEAPON_SCALE):
    """The item's tpHandTransform: the grip point lands on the hand's attachment point, after the hand's own 180 degree
    turn and the weapon's rotation, at WEAPON_SCALE. Origin 0, so translation is in the weapon's own block units."""
    import math
    rx, ry, rz = (math.radians(rotation.get(a, 0)) for a in "xyz")
    ry -= math.pi  # the RightHand attachment point's rotationY of -180

    def rot(v):  # the game's Mat4f.RotateByXYZ: z first, then y, then x
        x, y, z = v
        x, y = x * math.cos(rz) - y * math.sin(rz), x * math.sin(rz) + y * math.cos(rz)
        x, z = x * math.cos(ry) + z * math.sin(ry), -x * math.sin(ry) + z * math.cos(ry)
        y, z = y * math.cos(rx) - z * math.sin(rx), y * math.sin(rx) + z * math.cos(rx)
        return x, y, z
    # grip: (x, y) on the model's centre plane z = 8, or (x, y, z) (0.0.76).
    gz0 = grip[2] / 16 if len(grip) > 2 else 0.5
    gx, gy, gz = rot((grip[0] / 16, grip[1] / 16, gz0))
    return {"translation": {"x": round(-gx, 4), "y": round(-gy, 4), "z": round(-gz, 4)},
            "rotation": {a: rotation.get(a, 0) for a in "xyz"},
            "origin": {"x": 0, "y": 0, "z": 0}, "scale": scale}


def weapon_shape(name, w, own, bundled, domain, assets, folder):
    """The game shape for one weapon (or shield or bow) entry, textured: a packed texture ("atlas", or the one embedded in
    a .bbmodel "source") is written to <assets>/textures/item/<folder>/atlas/<name>.png, and textures of the owner's set
    (own, from own_weapon_textures) are renamed to <domain>:item/<folder>/<path> and added to bundled, for
    copy_own_textures to write. Returns the shape, ready for json."""
    sheet = None
    if w.get("source"):
        # 0.0.94: converted from the owner's Blockbench project at build time (bbmodel_to_vs), so the text patch
        # carries the model and its embedded texture without a separate PNG.
        shape, sheet = BBMODEL.convert(BBMODEL.load(os.path.join(WEAPON_SOURCE_DIR, w["source"])))
        w = dict(w, atlas=w["source"])
    else:
        with open(os.path.join(WEAPON_DIR, w["model"]), encoding="utf-8-sig") as f:
            shape = json.load(f)
    if w.get("atlas"):
        # One texture for the whole model (0.0.65): every face draws from it, whatever the model's own name for it.
        def retexture(elements):
            for e in elements:
                for face in (e.get("faces") or {}).values():
                    if isinstance(face, dict) and face.get("texture"):
                        face["texture"] = "#atlas"
                retexture(e.get("children") or [])
        retexture(shape["elements"])
        size = [shape.get("textureWidth", 16), shape.get("textureHeight", 16)]
        # The pulled-string bows share their bow's texture (atlasName).
        atlas_name = w.get("atlasName", name)
        shape["textures"] = {"atlas": f"{domain}:item/{folder}/atlas/{atlas_name}"}
        shape["textureSizes"] = {"atlas": size}
        dest = os.path.join(assets, "textures", "item", folder, "atlas", atlas_name + ".png")
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        if sheet is not None:
            sheet.save(dest, optimize=True)
        else:
            shutil.copyfile(atlas_png(w["atlas"]), dest)
        shape["textures"], textures = {}, shape["textures"]
    else:
        textures = {}
    for key, path in shape["textures"].items():
        if path.lower() in own:
            # Asset paths are lowercase in the game, so the bundled copy is too.
            fixed = f"{domain}:item/{folder}/" + path.lower().split(":", 1)[1]
            bundled.add(path.lower())
        else:
            fixed = WEAPON_TEXTURES.get(path.lower(), path)
            if fixed not in WEAPON_TEXTURES_OK and fixed not in WEAPON_TEXTURES.values():
                sys.exit(f"{w['model']}: texture {key} = {path} is neither the user's, known good, nor mapped in WEAPON_TEXTURES")
        textures[key] = fixed
    shape["textures"] = textures
    shape.pop("editor", None)
    return shape


def copy_own_textures(bundled, own, assets, folder):
    """Writes the owner's textures the shapes use (bundled) to <assets>/textures/item/<folder>/<path>.png."""
    for path in sorted(bundled):
        dest = os.path.join(assets, "textures", "item", folder, *path.split(":", 1)[1].split("/")) + ".png"
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copyfile(own[path], dest)
