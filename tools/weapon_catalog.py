"""The owner's custom melee weapons, shared by lotrnpcs (NPCs hold them, gen_testnpcs.py) and lotrweapons (player
items): each weapon's model, texture, grip, roll, scale, swing and hold, the hand transform worked out from them, and
the writer that turns a model into a game shape with its textures. One table for both mods, so a grip fix reaches both.

Loaded by path (the generators run with python -I, so tools/ isn't on sys.path). Shields and bows still live in
gen_testnpcs.py; they move here when lotrweapons gets them.
"""
import json
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
