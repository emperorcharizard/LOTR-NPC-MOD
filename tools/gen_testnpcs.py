"""Generates the test NPC mod assets into the folder given as argv[1].

Run bundle_lrc.py into the same folder for the race models these NPCs use.
"""
import copy
import json
import math
import os
import shutil
import sys

out = sys.argv[1]
A = os.path.join(out, "assets", "lotrnpcs")


def write(rel, data):
    path = os.path.join(A, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        json.dump(data, f, indent=2)
        f.write("\n")


ANIMS = [
    {"code": "walk", "animation": "walk", "blendMode": "Average", "mulWithWalkSpeed": True, "animationSpeed": 1.1,
     "triggeredBy": {"onControls": ["Move"], "matchExact": True}},
    {"code": "idle", "animation": "idle1", "blendMode": "Average",
     "triggeredBy": {"onControls": ["Idle"], "matchExact": True, "defaultAnim": True}},
    {"code": "sprint", "animation": "sprint", "animationSpeed": 0.6, "mulWithWalkSpeed": True, "blendMode": "Average",
     "triggeredBy": {"onControls": ["Move", "SprintMode"], "matchExact": True}},
    {"code": "swim", "animation": "swim", "blendMode": "Average",
     "triggeredBy": {"onControls": ["Move", "Swim"], "matchExact": True}},
    {"code": "swimidle", "animation": "swimidle", "blendMode": "Average",
     "triggeredBy": {"onControls": ["Idle", "Swim"], "matchExact": True}},
    {"code": "land", "animation": "land", "blendMode": "Average", "triggeredBy": {"onControls": ["Fall"]}},
    {"code": "die", "animation": "die", "blendMode": "Average", "animationSpeed": 1.5,
     "triggeredBy": {"onControls": ["Dead"]}},
    {"code": "jump", "animation": "newjump", "blendMode": "Average",
     "triggeredBy": {"onControls": ["Idle", "Jump"], "matchExact": True}},
    {"code": "hurt", "animation": "hurt", "blendMode": "Average"},
    {"code": "hit", "animation": "hit", "blendMode": "Add",
     "elementWeight": {"UpperArmR": 4, "LowerArmR": 4, "UpperArmL": 4, "LowerArmL": 4},
     "elementBlendMode": {"UpperArmR": "AddAverage", "LowerArmR": "AddAverage", "UpperArmL": "AddAverage", "LowerArmL": "AddAverage"}},
]

# When a melee hit lands, in ms after the attack starts. The unarmed punch and the archers' shove strike at this moment.
DAMAGE_AT_MS = 400
# 0.0.99 (owner, 2026-11-08: "Review the animations and try to improve them"; chose "Swing sync + weapon wind-ups"):
# until 0.0.98 every weapon struck at DAMAGE_AT_MS, so the game's swings were sped up or slowed down to fit (axehit
# 1.9x, hammerhit 1.4x, knifestab and spearhit 0.8x). Now each swing strikes when it naturally would, close to its own
# speed: light thrusts first, heavy chops last. Only the first blow of a fight comes later; the time between blows
# (attack plus cooldown) is unchanged, so damage per second stays the same. A swing not listed keeps DAMAGE_AT_MS.
HIT_MS = {"knifestab": 350, "spearhit": 350, "falx": 420, "poleaxechop": 500, "hammerhit": 550, "axehit": 600}


def hit_ms(w):
    return HIT_MS.get(w["attack"], DAMAGE_AT_MS)

AI_TASKS = [
    # Our melee task: always hits a player's chest under Combat Overhaul (user: no head shots).
    # 0.0.45 (user, 2026-11-03: "increase how often they attack by .25 s"): the cooldown after each swing is 250 ms
    # shorter, 1950 to 2350 ms (was 2200 to 2600), so one swing every 2.85 to 3.25 s (was 3.1 to 3.5 s). 0.0.108 (owner,
    # 2026-11-09: "increase attack frequency up a tick or two"): 0.25 s shorter again, 1700 to 2100 ms, one swing every 2.6 to 3.0 s.
    {"code": "lotrnpcs:meleeattack", "entityCodes": ["player"], "priority": 3, "slot": 1, "damage": 3, "damageTier": 1,
     "mincooldown": 1700, "maxcooldown": 2100, "attackDurationMs": 900, "damagePlayerAtMs": DAMAGE_AT_MS,
     "animation": "hit", "animationSpeed": 1},
    # Our seek task: vanilla seek, but targets are chosen by faction (Factions.cs): non-friendly players for
    # aggressive factions, enemy factions' NPCs, monsters, and anyone who attacked the faction or its allies.
    # 0.0.94 (owner: "for units that are being damaged but cannot reach their attacker, have them flee and hide behind
    # cover"; recruits too): above seek, shooting and following (Hiding.cs).
    {"code": "lotrnpcs:hide", "priority": 2.8, "movespeed": 0.04, "searchRadius": 16, "stayMs": 10000,
     "animation": "sprint", "animationSpeed": 0.45},
    {"code": "lotrnpcs:seekentity", "entityCodes": ["player"], "priority": 2.5, "movespeed": 0.03, "seekingRange": 24, "searchWaitMs": 1000,
     "animation": "sprint", "animationSpeed": 0.45},
    # Recruits (0.0.37): follow the owner or go back to the spot they hold. Below seek, so fights come first.
    # Recruits never wander (EntityBehaviorHireable blocks it). 0.0.38 (user: "they move at a walking speed"): a
    # little faster than a walking player, and running a little faster than a sprinting one whenever more than 6
    # blocks behind, back to walking within 3.
    {"code": "lotrnpcs:followowner", "priority": 2.0, "walkSpeed": 0.027, "runSpeed": 0.055, "runAnimationSpeed": 0.75,
     "followDistance": 4, "holdDistance": 2},
    {"code": "wander", "priority": 1.0, "movespeed": 0.012, "animation": "walk", "wanderChance": 0.02},
    {"code": "lookaround", "priority": 0.5},
]

# Vanilla body as the base shape: Player Model Lib swaps in the race model at render time.
FA_BODY = "game:entity/humanoid/seraph-faceless"
# 0.0.77: NPCs use our own copy of that body (bundled by bundle_lrc.py: the game's body plus our shape animations
# poleaxechop and poleaxehold). The server builds an NPC's animations from its type's base shape, never from the Player
# Model Lib race model, so with the game's body it refused both ("attempted to play an animation code which its shape
# does not have", owner's 0.0.76 server-debug.log) and the poleaxe and battleaxe hung at the side, one-handed.
# Same elements as the game's body; only the animation list differs.
NPC_BODY = "lotrnpcs:entity/humanoid/seraph-faceless"
FA_BASE_TEXTURE = "game:entity/humanoid/seraph-naked-hairless"


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
# The game's iron falx (0.0.76, user: "the other factions should just have an iron falx placeholder until I can get the
# actual model"), held with the game's own hand transform and swung like the scimitar. Code from the game's item list
# (wiki image Blade-falx-iron); a wrong code only leaves the hand empty, with a warning in the client log.
FALX = {"falx": {"item": "game:blade-falx-iron", "attack": "falx", "impact": 12.5}}
# 0.0.101 (owner, 2026-11-08: "with the removal of not only spears expansion, what base game equivalents can be given";
# then "build it that way"): the game's own weapons as stand-ins until the owner's models arrive, held with the game's
# hand transforms. Codes read from the server's Combat Overhaul fork 0.18.9 (assets/game/itemtypes/combatoverhaul),
# which redefines the game's weapons under the same codes; "ruined" ones are rusty. Spears thrust and are held level
# like the owner's spears, swords slash like the falx, clubs and maces swing like the hammer, axes like the axe. The
# bardiche (a polearm) is swung one-handed like the axe, with no shield; the game's item has no two-handed hold of ours.
_GAME_SPEAR = {"attack": "spearhit", "impact": 10} | SPEAR_HOLD
_GAME_SWORD = {"attack": "falx", "impact": 12.5}
_GAME_CLUB = {"attack": "hammerhit", "impact": 17}
_GAME_AXE = {"attack": "axehit", "impact": 22.5}
GAME_WEAPONS = {
    "steelspear": {"item": "game:spear-generic-steel"} | _GAME_SPEAR,
    "ironspear": {"item": "game:spear-generic-iron"} | _GAME_SPEAR,
    "bbronzespear": {"item": "game:spear-generic-blackbronze"} | _GAME_SPEAR,
    "flintspear": {"item": "game:spear-generic-flint"} | _GAME_SPEAR,
    "silverspear": {"item": "game:spear-generic-ornatesilver"} | _GAME_SPEAR,
    "scrapspear": {"item": "game:spear-scrap-scrap"} | _GAME_SPEAR,
    "steelfalx": {"item": "game:blade-falx-steel"} | _GAME_SWORD,
    "blackguard": {"item": "game:blade-blackguard-iron"} | _GAME_SWORD,
    "forlorn": {"item": "game:blade-forlorn-iron"} | _GAME_SWORD,
    "ruinedsabre": {"item": "game:blade-sabre-ruined"} | _GAME_SWORD,
    "ruinedgladius": {"item": "game:blade-gladius-ruined"} | _GAME_SWORD,
    "woodclub": {"item": "game:club-generic-wood"} | _GAME_CLUB,
    "scrapmace": {"item": "game:club-scrapmace-scrap"} | _GAME_CLUB,
    "ruinedmace": {"item": "game:club-flanged-ruined"} | _GAME_CLUB,
    "ruinedbeardedaxe": {"item": "game:axe-bearded-ruined"} | _GAME_AXE,
    "ruinedbattleaxe": {"item": "game:axe-battle-ruined"} | _GAME_AXE,
    "ruinedbardiche": {"item": "game:axe-bardiche-ruined", "twoHanded": True} | _GAME_AXE,
}
# Every orc faction but Isengard may carry the Mordor dagger and axe (0.0.76, user: "the orc dagger and the orc axe
# should now be eligible accessories for all orc factions except isengard").
ORC_SHARED = {k: WEAPONS[k] for k in ("dagger", "axe")}
FALX_RACES = ["gondor", "rohan", "dale", "arnor", "haradrim", "easterling", "dunlending", "blacknumenorean",
              "tormentedelf", "wickeddwarf", "uruk"]
# Each armed race's weapons (Mordor since 0.0.45, Misty Mountains 0.0.75, everyone but the Shire 0.0.76).
# 0.0.96 (owner, 2026-11-07: "Let the gundabad orcs use the moria spear"): the Moria spear joins Gundabad's orc dagger
# and axe (held as the Misty Mountains orcs hold it; Gundabad carry no shield).
WEAPONS_OF = {"mordororc": WEAPONS, "goblin": {**MM_WEAPONS, **ORC_SHARED}, "angmarorc": ORC_SHARED,
              "gundabadorc": {**ORC_SHARED, "mmspear": MM_WEAPONS["mmspear"]},
              "elf": ELF_WEAPONS, "dwarf": DWARF_WEAPONS} | {r: FALX for r in FALX_RACES}
# 0.0.82 to 0.0.95 used items of the (Not Only) Spear Expansion mod (domain spearexpantion) as stand-ins: pike, halberd,
# war pick, flanged mace, common mace, khopesh, bartka, seax. 0.0.96 (owner, 2026-11-07: "remove the not only spears
# expansion dependancy, as it wont be implemented into the server"): all gone. Each faction now carries only the
# owner's models and the game's own falx; a rank left with nothing falls back to WEAPONS_OF (the falx for Men, Uruks,
# tormented elves). The weapons each faction still needs are in docs/model-checklist.md, with the owner's preferences
# from the 0.0.82 loadout list ("prefer spears" and the like), so they can be weighted again when the models come.
# NPCs already holding a Spear Expansion item pick again when they load (it is no longer in their list).
ALL_WEAPONS = {**WEAPONS, **MM_WEAPONS, **ELF_WEAPONS, **DWARF_WEAPONS, **HARADRIM_WEAPONS, **GONDOR_WEAPONS, **FALX, **GAME_WEAPONS}
# Weapons by rank, with weights (0.0.82, owner's list: "Prefer spears" and the like). "Prefer" = weight 3 against 1
# for the faction's other weapons (my default). Races and ranks not listed carry WEAPONS_OF's list, all weight 1.
PREFER = 3
_DWARF_RANKS = {"recruit": {"dwaxe": PREFER, "dwbattleaxe": PREFER, "dwwarhammer": PREFER, "dwshorthammer": PREFER, "dwdagger": 1, "dwspear": 1},
                "warrior": {"dwaxe": PREFER, "dwbattleaxe": PREFER, "dwwarhammer": PREFER, "dwshorthammer": PREFER, "dwdagger": 1, "dwspear": 1},
                "captain": {"dwwarhammer": PREFER, "dwshorthammer": PREFER, "dwaxe": 1, "dwbattleaxe": 1, "dwdagger": 1, "dwspear": 1}}
# 0.0.101 game stand-ins by faction (owner: "build it that way"), every rank alike; PREFER marks the owner's 0.0.82
# preferences (Gondor, Arnor: spears). Replaced by the owner's models as they come (docs/model-checklist.md).
_RANKS = ("recruit", "warrior", "captain")
GAME_LOADOUTS = {
    "gondor": {"steelspear": PREFER, "steelfalx": 1},
    "arnor": {"steelspear": PREFER, "steelfalx": 1},
    "rohan": {"ironspear": 1, "falx": 1},
    "dale": {"ironspear": 1, "falx": 1},
    "dunlending": {"woodclub": 1, "flintspear": 1, "ruinedbeardedaxe": 1},
    "blacknumenorean": {"blackguard": 1, "ruinedmace": 1, "bbronzespear": 1},
    "easterling": {"bbronzespear": 1, "forlorn": 1, "ruinedbardiche": 1},
    "uruk": {"falx": 1, "ironspear": 1, "ruinedbattleaxe": 1},
    "tormentedelf": {"ruinedsabre": 1, "ruinedgladius": 1, "silverspear": 1},
    "angmarorc": {"dagger": 1, "axe": 1, "scrapspear": 1, "scrapmace": 1},
    "gundabadorc": {"dagger": 1, "axe": 1, "mmspear": 1, "scrapmace": 1},
}
WEAPON_PREFS = {
    **{f: {r: dict(w) for r in _RANKS} for f, w in GAME_LOADOUTS.items()},
    # Gondor captains: the owner's captain's sword (0.0.95). Recruits and warriors: the game stand-ins (0.0.101).
    "gondor": {"recruit": dict(GAME_LOADOUTS["gondor"]), "warrior": dict(GAME_LOADOUTS["gondor"]), "captain": {"gdcaptsword": 1}},
    # Haradrim (owner, 0.0.86: no falx): the owner's spear (0.0.94), sword and curved sword (0.0.97), evenly.
    "haradrim": {r: {"hdspear": 1, "hdsword": 1, "hdcurvedsword": 1} for r in ("recruit", "warrior", "captain")},
    # Elves: the owner's elven spear, plus the falx as their sword.
    "elf": {r: {"elfspear": 1, "falx": 1} for r in ("recruit", "warrior", "captain")},
    # Dwarves: "Prefer axes and warhammers"; Lords "Prefer warhammers" (0.0.95: the owner's warhammer and shorthammer).
    "dwarf": _DWARF_RANKS,
    # Wicked dwarves: the dwarves' rules and models.
    "wickeddwarf": _DWARF_RANKS,
}
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
SHIELDS = {"shield": SHIELD, "mmshield": MM_SHIELD, "kiteshield": KITE_SHIELD, "dwtowershield": DW_TOWER_SHIELD,
           "dwshield": DW_IRON_SHIELD, "eastshield": EASTERLING_SHIELD, "gondorshield": GONDOR_SHIELD,
           "boromirshield": BOROMIR_SHIELD, "elfshield": ELF_SHIELD, "hdlightshield": HD_LIGHT_SHIELD,
           "hdheavyshield": HD_HEAVY_SHIELD}
# Races whose melee NPCs may carry a shield, and which. Several: each NPC that rolls a shield picks one at random
# (0.0.76, dwarves). Mordor since 0.0.55, Misty Mountains 0.0.71, Black Numenoreans and dwarves 0.0.76.
# 0.0.101: a full "domain:code" is a game shield (stand-ins: Rohan and Dale the wood-and-metal shield, Dunlendings the
# crude one), drawn as the game draws it.
SHIELD_OF = {"rohan": ["game:shield-woodmetal"], "dale": ["game:shield-woodmetal"], "dunlending": ["game:shield-crude"],
             "mordororc": ["shield"], "goblin": ["mmshield"], "blacknumenorean": ["kiteshield"],
             "dwarf": ["dwtowershield", "dwshield"], "easterling": ["eastshield"],
             # 0.0.94: Gondor (owner: "gondor only"; both shields at random, "A") and elves (owner: "elves only").
             "gondor": ["gondorshield", "boromirshield"], "elf": ["elfshield"],
             # 0.0.97 (my call, the owner sent the two Haradrim shields without notes): by rank, as their armor goes
             # (infantry, heavy infantry, warlord): the light shield for recruits, the heavy one for warriors and
             # captains. A dict by rank instead of a list.
             "haradrim": {"recruit": ["hdlightshield"], "warrior": ["hdheavyshield"], "captain": ["hdheavyshield"]}}
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
# When the drawn bow shows: the whole draw, the first 200 ms of the 450 ms release, while the arm comes down from the
# aim (after that the rest bow fits the lowered arm), and the shove when an enemy is too close to shoot (0.0.60: an
# upright bow bash, where the rest bow pointed forward like a stick). Milliseconds after the animation starts; 0 = while
# it runs. Weapons.cs looks these codes up as the shape animations the game tracks (bowaim, bowhit, hit).
DRAWN_WHILE = {"lotrnpcs-bow-draw": 0, "lotrnpcs-bow-release": 200, "lotrnpcs-bow-attack": 0}
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
BOW_POSES = ([{"item": f"lotrnpcs:orcweapon-{name}", "while": {"lotrnpcs-bow-draw": [round(frame * DRAW_MS_FOR_POSES / 16), 0]}}
              for name, _, frame in reversed(BOW_PULLS)]
             + [{"item": "lotrnpcs:orcweapon-bow-drawn", "while": {a: [0, ms] for a, ms in DRAWN_WHILE.items()}}])
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
MM_BOW_POSES = ([{"item": f"lotrnpcs:orcweapon-{name}", "while": {"lotrnpcs-bow-draw": [round(frame * DRAW_MS_FOR_POSES / 16), 0]}}
                 for name, _, frame in reversed(MM_BOW_PULLS)]
                + [{"item": "lotrnpcs:orcweapon-mmbow-drawn", "while": {a: [0, ms] for a, ms in DRAWN_WHILE.items()}}])
# Each archer race's bow, by item and poses. 0.0.76 (user: "the default bow should be the vanilla longbow, not the orc
# bow"): every race not listed carries the game's longbow, with the game's own hand transform and no string stages.
BOW_OF = {"mordororc": ("lotrnpcs:orcweapon-bow", BOW_POSES), "goblin": ("lotrnpcs:orcweapon-mmbow", MM_BOW_POSES),
          # 0.0.101 game stand-ins: the recurve bow for elves, Easterlings and Haradrim, the simple bow for Dunlendings.
          "elf": ("game:bow-recurve", []), "easterling": ("game:bow-recurve", []), "haradrim": ("game:bow-recurve", []),
          "dunlending": ("game:bow-simple", [])}
DEFAULT_BOW = ("game:bow-long", [])


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
# Archer animations: draw over DRAW_MS (vanilla bowaim, 16 frames, held at the end), loose (bowhit), and a shove with
# the bow when cornered (the unarmed hit, timed like the weapons). No pose between shots since 0.0.59: the arms idle.
DRAW_MS = 1100
assert DRAW_MS == DRAW_MS_FOR_POSES, "keep the pulled-string timing in step with the draw"
BOW_ANIMS = [
    {"code": "lotrnpcs-bow-draw", "animation": "bowaim", "animationSpeed": round(16 / 30 / (DRAW_MS / 1000), 3),
     "blendMode": "Average", "weight": 0.01, "elementWeight": None},
    {"code": "lotrnpcs-bow-release", "animation": "bowhit", "animationSpeed": 1, "blendMode": "Average", "weight": 0.01,
     "elementWeight": None},
    {"code": "lotrnpcs-bow-attack", "animation": "hit", "animationSpeed": round(7 / 30 / (400 / 1000), 3),
     "blendMode": "Average", "weight": 0.01, "elementWeight": None},
]
# Factions whose NPCs carry these weapons (user: "to be used by the mordor orcs only").
ARMED_FACTIONS = set(WEAPONS_OF)  # Mordor since 0.0.45, Misty Mountains since 0.0.75
# Factions with an archer unit (0.0.55 Mordor; 0.0.69 every orc faction; 0.0.70 user, 2026-11-04: "every faction
# (except hobbits) gets archers."). Same unit, stats, bow and arrows as Mordor's, in the faction's recruit armor; the
# bow is the one stand-in model for everyone until the owner's own bows arrive.
ARCHER_RACES = ["mordororc", "angmarorc", "goblin", "uruk", "gundabadorc", "gondor", "rohan", "dale", "arnor", "haradrim",
                "dunlending", "blacknumenorean", "easterling", "dwarf", "wickeddwarf", "elf", "tormentedelf"]
# A swing moves the arms, torso and head, but hardly the legs, so an orc that attacks while running keeps running.
UPPER_BODY = ["UpperArmR", "LowerArmR", "ItemAnchor", "UpperArmL", "LowerArmL", "ItemAnchorL", "UpperTorso", "Neck", "Head"]
LEGS = ["UpperFootL", "LowerFootL", "UpperFootR", "LowerFootR"]
ATTACK_WEIGHTS = {e: 10 for e in UPPER_BODY} | {"LowerTorso": 3} | {e: 0.5 for e in LEGS}
HOLD_WEIGHTS = {e: 10 for e in ["UpperArmR", "LowerArmR", "UpperArmL", "LowerArmL"]}
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


def weapon_anims():
    anims = []
    for a in BOW_ANIMS:
        anims.append(dict(a, elementWeight=HOLD_WEIGHTS if a["elementWeight"] == "hold" else ATTACK_WEIGHTS))
    for name, w in ALL_WEAPONS.items():
        anims.append({"code": f"lotrnpcs-{name}-attack", "animation": w["attack"],
                      "animationSpeed": round(w["impact"] / 30 / (hit_ms(w) / 1000), 3),
                      "blendMode": "Average", "weight": 0.01, "elementWeight": ATTACK_WEIGHTS})
        if w.get("hold"):
            anims.append({"code": f"lotrnpcs-{name}-hold", "animation": w["hold"], "blendMode": "Average", "weight": 0.01,
                          "elementWeight": {e: 10 for e in w["holdElements"]} if w.get("holdElements") else HOLD_WEIGHTS})
    return anims


def weapon_list(weapons=WEAPONS, weights=None):
    """The lotrnpcs:heldweapon behavior's list (weights: name -> weight, 0.0.82; 1 is left out)."""
    return [{"item": w.get("item", f"lotrnpcs:orcweapon-{name}"), "attackAnimation": f"lotrnpcs-{name}-attack"}
            | ({"hitMs": hit_ms(w)} if hit_ms(w) != DAMAGE_AT_MS else {})
            | ({"holdAnimation": f"lotrnpcs-{name}-hold"} if w.get("hold") else {})
            | ({"twoHanded": True} if w.get("twoHanded") else {})
            | ({"weight": weights[name]} if weights and weights.get(name, 1) != 1 else {}) for name, w in weapons.items()]


def held_weapon(faction, rank):
    """The lotrnpcs:heldweapon behavior for an NPC: archers keep their bow; other Mordor orcs pick a weapon and may
    carry the shield."""
    if rank == "archer":
        bow, poses = BOW_OF.get(faction, DEFAULT_BOW)
        return {"code": "lotrnpcs:heldweapon", "fixed": True,
                "weapons": [{"item": bow, "attackAnimation": "lotrnpcs-bow-attack"} | ({"poses": poses} if poses else {})]}
    prefs = WEAPON_PREFS.get(faction, {}).get(rank)
    listed = {name: ALL_WEAPONS[name] for name in prefs} if prefs else WEAPONS_OF.get(faction, {})
    behavior = {"code": "lotrnpcs:heldweapon", "weapons": weapon_list(listed, prefs)}
    if faction in SHIELD_OF:
        shields = SHIELD_OF[faction]
        shields = shields.get(rank, []) if isinstance(shields, dict) else shields
        if shields:
            behavior["shields"] = [s if ":" in s else f"lotrnpcs:orcweapon-{s}" for s in shields]
    return behavior


def write_weapons():
    own = own_weapon_textures()
    bundled = set()
    items = {**{k: w for k, w in ALL_WEAPONS.items() if "item" not in w}, **SHIELDS, "bow": BOW, "bow-drawn": BOW_DRAWN, **BOW_PULL_ITEMS, "mmbow": MM_BOW, "mmbow-drawn": MM_BOW_DRAWN, **MM_BOW_PULL_ITEMS}
    for name, w in items.items():
        if w.get("shape"):
            # Another pose of an item already written (the drawn bow): only the item, on that item's shape.
            write(f"itemtypes/orcweapon-{name}.json", {
                "code": f"orcweapon-{name}", "class": "Item", "maxstacksize": 1,
                "shape": {"base": f"lotrnpcs:item/orcweapon/{w['shape']}"},
                "attributes": {"handbook": {"exclude": True}},
                "tpHandTransform": w["hand"],
                "guiTransform": {"rotation": {"x": 0, "y": 90, "z": -45}, "origin": {"x": 0.5, "y": 0.1, "z": 0.5}, "scale": 1.2},
            })
            continue
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
            shape["textures"] = {"atlas": f"lotrnpcs:item/orcweapon/atlas/{atlas_name}"}
            shape["textureSizes"] = {"atlas": size}
            dest = os.path.join(A, "textures", "item", "orcweapon", "atlas", atlas_name + ".png")
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
                fixed = "lotrnpcs:item/orcweapon/" + path.lower().split(":", 1)[1]
                bundled.add(path.lower())
            else:
                fixed = WEAPON_TEXTURES.get(path.lower(), path)
                if fixed not in WEAPON_TEXTURES_OK and fixed not in WEAPON_TEXTURES.values():
                    sys.exit(f"{w['model']}: texture {key} = {path} is neither the user's, known good, nor mapped in WEAPON_TEXTURES")
            textures[key] = fixed
        shape["textures"] = textures
        shape.pop("editor", None)
        if "pull" in w:
            pull_string(shape, w["pull"], w.get("apexX"))
        write(f"shapes/item/orcweapon/{name}.json", shape)
        write(f"itemtypes/orcweapon-{name}.json", {
            "code": f"orcweapon-{name}",
            "class": "Item",
            "maxstacksize": 1,
            "shape": {"base": f"lotrnpcs:item/orcweapon/{name}"},
            # Only ever shown in an NPC's hand: not in the creative inventory, not in the handbook.
            "attributes": {"handbook": {"exclude": True}},
            "tpHandTransform": w["hand"] if "hand" in w else hand_transform(w["grip"], w["rotation"], w.get("scale", WEAPON_SCALE)),
            "guiTransform": {"rotation": {"x": 0, "y": 90, "z": -45}, "origin": {"x": 0.5, "y": 0.1, "z": 0.5}, "scale": 1.2},
        } | ({"tpOffHandTransform": w["hand"]} if name in SHIELDS else {}))
    # The arrow's shaft, head and fletching (write_arrow) are the owner's textures too. Until 0.0.65 the old weapon
    # models also used them, so they came along with those.
    bundled |= {"game:planks/walnut1", "game:mord/rustiron", "game:cloth/black"} & set(own)
    for path in sorted(bundled):
        dest = os.path.join(A, "textures", "item", "orcweapon", *path.split(":", 1)[1].split("/")) + ".png"
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copyfile(own[path], dest)
    write_arrow(bundled)
    unused = sorted(set(own) - bundled)
    if unused:
        sys.exit(f"weapon textures no model uses: {unused}")


def write_arrow(bundled):
    """The archers' arrow (0.0.55): a vanilla-style projectile of our own class (Archery.cs), drawn with the same
    bundled textures as the weapons: a walnut shaft, a rusty iron head and black fletching. It flies tip first along
    +X, as the game turns projectiles."""
    tex = {"shaft": "lotrnpcs:item/orcweapon/planks/walnut1", "head": "lotrnpcs:item/orcweapon/mord/rustiron",
           "fletch": "lotrnpcs:item/orcweapon/cloth/black"}
    for path in ("game:planks/walnut1", "game:mord/rustiron", "game:cloth/black"):
        assert path in bundled, path

    def box(name, f, t, texture):
        return {"name": name, "from": f, "to": t,
                "faces": {k: {"texture": "#" + texture, "uv": [0, 0, 2, 2]} for k in ("north", "east", "south", "west", "up", "down")}}
    write("shapes/entity/npcarrow.json", {"textureWidth": 16, "textureHeight": 16, "textures": tex, "elements": [
        box("shaft", [0, 0.85, 7.5], [14, 1.85, 8.5], "shaft"),
        box("head", [14, 0.6, 7.25], [16.5, 2.1, 8.75], "head"),
        box("fletching up", [0.2, 1.85, 7.95], [3.2, 3.0, 8.05], "fletch"),
        box("fletching down", [0.2, -0.3, 7.95], [3.2, 0.85, 8.05], "fletch"),
        box("fletching side", [0.2, 1.3, 6.8], [3.2, 1.4, 9.2], "fletch"),
    ]})
    physics = {"code": "passivephysics", "groundDragFactor": 1, "airDragFactor": 0.25, "gravityFactor": 0.75}
    write("entities/projectile/npcarrow.json", {
        "code": "npcarrow",
        "class": "LotrNpcsArrow",
        "hitboxSize": {"x": 0.125, "y": 0.125},
        "attributes": {"handbook": {"exclude": True}},
        "client": {"renderer": "Shape", "size": 0.75, "shape": {"base": "lotrnpcs:entity/npcarrow"},
                   "behaviors": [physics, {"code": "interpolateposition"}]},
        # Gone 20 seconds after it was shot, wherever it landed.
        "server": {"behaviors": [physics, {"code": "despawn", "minSeconds": 20}]},
        "sounds": {},
    })


ANIMS += weapon_anims()


def nametag(name):
    return {"code": "nametag", "showtagonlywhentargeted": True, "selectFromRandomName": [name]}


# Archers (0.0.55, user, 2026-11-03: "bring up archer units, primarily for mordor, up to the current spec that the npcs
# are at right now"; "The archers are just an additional unit, they don't have ranks"). Every faction but the Shire since 0.0.70. Same
# seek, melee and hiring as everyone; they shoot from up to 24 blocks out and 20 above or below (0.0.74, user,
# 2026-11-04; was 20 out and 12 up or down), one arrow every 3.1 to 3.9 seconds (a
# 1.1 second draw, then 2 to 2.8 seconds), 3 piercing damage at tier 8 (a warrior's sword hits for 4 at tier 9),
# stop 16 blocks from a target they can see instead of walking up to it, and step back from enemies within 2 blocks (0.0.74, user, 2026-11-04: "step back when they are within 2 blocks"; was 4).
ARCHER_TASK = {"code": "lotrnpcs:shootattack", "priority": 2.6, "slot": 0, "range": 24, "verticalRange": 20, "minRange": 2,
               "damage": 3, "damageTier": 8, "damageType": "PiercingAttack", "drawMs": DRAW_MS, "releaseMs": 450,
               "mincooldown": 2000, "maxcooldown": 2800, "arrowSpeed": 28, "gravityFactor": 0.75, "dispersionDeg": 2.5,
               "projectileCode": "lotrnpcs:npcarrow", "shootSound": "game:sounds/bow-release",
               "animation": "lotrnpcs-bow-draw", "releaseAnimation": "lotrnpcs-bow-release"}
ARCHER_STAND_OFF = 16


def entity(code, name, extra_attributes, skin_client, skin_server, health=25, armor_tier=0, melee=(3, 1), archer=False):
    ai_tasks = [dict(t, damage=melee[0], damageTier=melee[1], damageType="SlashingAttack") if t["code"] == "lotrnpcs:meleeattack" else t
                for t in AI_TASKS]
    if archer:
        ai_tasks = [dict(t, standOff=ARCHER_STAND_OFF) if t["code"] == "lotrnpcs:seekentity" else t for t in ai_tasks]
        ai_tasks.insert(1, ARCHER_TASK)
    return {
        "code": code,
        "class": "EntityAgent",
        "tags": ["humanoid", "habitat-land"],
        "canClimb": True,
        "hitboxSize": {"x": 0.6, "y": 1.85},
        "eyeHeight": 1.7,
        "attributes": extra_attributes,
        "behaviorConfigs": {"controlledphysics": {"stepHeight": 1.1}},
        "client": {
            "renderer": "Shape",
            "size": 1.05,
            "pitchStep": False,
            "shape": {"base": NPC_BODY},
            "texture": {"base": FA_BASE_TEXTURE},
            "behaviors": [
                {"code": "repulseagents"},
                nametag(name),
                # Right-click talk menu and recruit info (Hiring.cs).
                {"code": "lotrnpcs:hireable"},
                {"code": "controlledphysics"},
                {"code": "interpolateposition"},
            ] + skin_client,
            "animations": ANIMS,
        },
        "server": {
            "behaviors": [
                {"code": "repulseagents"},
                nametag(name),
                {"code": "controlledphysics"},
            ] + ([{"code": "lotrnpcs:rankarmor", "tier": armor_tier}] if armor_tier else []) + [
                # Talk menu, hiring and recruit upkeep (Hiring.cs). Before health, so it can cancel friendly fire.
                {"code": "lotrnpcs:hireable"},
                {"code": "health", "currenthealth": health, "maxhealth": health},
                # Fights back with its allies when hurt (Factions.cs).
                {"code": "lotrnpcs:factionmember"},
                {"code": "taskai", "aiCreatureType": "Humanoid", "aitasks": ai_tasks},
                # 0.0.88 (owner: NPCs "will fling themselves off the edge" of deep pits): right after the AI, so it can
                # stop a step the AI just set (EdgeGuard.cs).
                {"code": "lotrnpcs:edgeguard"},
            ] + skin_server,
        },
        "sounds": {"hurt": "game:player/hurt", "death": "game:player/death", "swim": "game:player/swim"},
    }


# Player Model Lib draws a race model. Model codes are "domain:key"; ours are bundled by bundle_lrc.py.
# Each entry: entity code, display name, model code, skin texture for the creative item icon, starting gear.
SKINS = "lotrnpcs:entity/humanoid/skinparts/"
ORC_SKIN = SKINS + "orc/orcskin8"
# Rank outfits are vanilla armor items, worn through the vanilla gear inventory. Looks only for now:
# armor on NPCs doesn't reduce damage in vanilla, and it isn't dropped on death.
RECRUIT_GEAR = ["game:armor-body-jerkin-leather", "game:armor-legs-jerkin-leather"]
WARRIOR_GEAR = ["game:armor-head-chain-iron", "game:armor-body-chain-iron", "game:armor-legs-chain-iron"]
# Rank strength (user): health scales like drifters (tainted 22, nightmare 40, double-headed 54), no rank armor.
# Melee was tuned in game under Combat Overhaul: slashing, about 0.75, 2 and 3.5 damage per torso hit through
# iron LOTR faction armor (13 slashing resist), and 3, 4 and 7 on an unarmored player.
# 0.0.43 (user, 2026-11-03: "reduce the health of all the npcs by 6"): 22, 34 and 48 (were 28, 40 and 54), and 19
# for the two rankless leftovers (was 25). NPCs already in a world are brought down on load (Factions.cs).
# Each entry is (health, rank armor tier, (melee damage, damage tier)); armor tier 0 leaves out lotrnpcs:rankarmor.
RANK_STATS = {"recruit": (22, 0, (3, 8)), "warrior": (34, 0, (4, 9)), "captain": (48, 0, (7, 9)),
              # 0.0.55 archers (a unit, not a rank): a warrior's health, a recruit's shove when cornered.
              "archer": (34, 0, (3, 8))}
RANKLESS_STATS = (19, 0, (3, 1))
CAPTAIN_GEAR = ["game:armor-head-antique-blackguard-damaged", "game:armor-body-antique-blackguard-damaged",
                "game:armor-legs-antique-blackguard-damaged"]
# Gundabad's fallback when its armor mod is missing: the rank outfits without helmets (user: "No helmets").
GUNDABAD_FALLBACK = {rank: [c for c in gear if "armor-head-" not in c]
                     for rank, gear in (("recruit", RECRUIT_GEAR), ("warrior", WARRIOR_GEAR), ("captain", CAPTAIN_GEAR),
                                        ("archer", RECRUIT_GEAR))}
# Faction armor (user): each orc faction wears its own "To Rule Them All" armor mod. lotrnpcs:startinggear picks
# one whole set at random and falls back to the vanilla outfit above when the armor mod isn't installed.
def mord(part, tier, construct, material, last="none"):
    return f"mord:armor-{part}-{tier}-{construct}-{material}-{last}"
def angmar(part, tier, construct):
    return f"angmar:armor-{part}-{tier}-{construct}-{construct}-none"
MORD_RAIDER_HEAD = mord("head", "tierthreechain", "servanthelmetorcmedium", "servanthelmetorcmedium")
MORD_RAIDER_LEGS = mord("legs", "tierthreechain", "servantleggingsorcmedium", "servantleggingsorcmedium")
MORD_WARRIOR_HEAD = mord("head", "tierfourplate", "servanthelmetorcheavy", "servanthelmetorcheavy")
# Captain helmet (user): Servant of the Eye Helmet (Red Mask), worn with the red Orc Warrior armor.
MORD_CAPTAIN_HEAD = mord("head", "tierfourplate", "servanthelmethumanheavy", "servanthelmethumanheavy", "maskred")
def mord_warrior(colour, head=MORD_WARRIOR_HEAD):
    return [head, mord("body", "tierfourplate", "servantarmororcheavy", "servantarmororcheavy" + colour),
            mord("legs", "tierfourplate", "servantleggingsorcheavy", "servantleggingsorcheavy" + colour)]
def norc(part, tier, construct):
    return f"norc:armor-{part}-{tier}-{construct}-{construct}-none"
def uruk(part, tier, construct, finish):
    return f"iseng:armor-{part}-{tier}-{construct}-{construct}-{finish}"
GUNDABAD_BODY = norc("body", "tierfourplate", "norcarmorheavy")
GUNDABAD_LEGS = norc("legs", "tierfourplate", "norcleggingsheavy")
URUK_INFANTRY_LEGS = uruk("legs", "tierfourplate", "urukhaileggingsheavy", "clean")
ANGMAR_SOLDIER_BODY = angmar("body", "tierfourplate", "angmararmorheavy")
ANGMAR_SOLDIER_LEGS = angmar("legs", "tierfourplate", "angmarleggingsheavy")
# 0.0.82 loadouts (owner's list, 2026-11-04, prompts/0.0.82-loadouts-combat.md): "All orcs and uruks can be unhelmed
# unless otherwise noted. If there is a color variant, make sure the whole set is the same color." Each set below is
# one whole outfit; a set per helmet a unit may wear.
ANGMAR_RAIDER_HEAD = angmar("head", "tierthreescale", "angmarhelmetmedium")
GUNDABAD_HEAD = norc("head", "tierfourplate", "norchelmetheavy")
GUNDABAD_TUSKED_HEAD = norc("head", "tierfourplate", "norchelmetheavytwo")
def mord_raider(colour, head=MORD_RAIDER_HEAD):
    return [head, mord("body", "tierthreechain", "servantarmororcmedium", "servantarmororcmedium" + colour), MORD_RAIDER_LEGS]
FACTION_SETS = {
    # Mordor (user, 2026-11-02): Orc Raider (black or brown) for recruits, Orc Warrior for warriors, Orc Warrior red
    # with the red-masked Servant of the Eye helmet for captains. 0.0.82 (owner's list): "Mordor Orc: Raider or warrior
    # set. Helmet can also be angmar orc helmet." "Black Uruk: Mordor orc warrior set only." (black or red, legs to
    # match). Captains unchanged.
    "mordororc": {
        "recruit": [mord_raider(c, head) for c in ("black", "brown") for head in (MORD_RAIDER_HEAD, ANGMAR_RAIDER_HEAD)]
                   + [mord_warrior(c, head) for c in ("black", "red") for head in (MORD_WARRIOR_HEAD, ANGMAR_RAIDER_HEAD)],
        "warrior": [mord_warrior("black"), mord_warrior("red")],
        "captain": [mord_warrior("red", MORD_CAPTAIN_HEAD)],
    },
    # Angmar: Raider for recruits, Soldier for warriors, Wight Guard (either helmet) for captains (user). 0.0.82
    # (owner's list): "Angmar Orc: Full set, can wear both mordor orc helmets, gundabad tusked helmet, or angmar orc
    # helmet." "Angmar Soldier: Full set only, either helmet." Every rank stays an orc (owner: "Factions should not mix
    # races").
    "angmarorc": {
        "recruit": [[head, angmar("body", "tierthreechain", "angmararmormedium"), angmar("legs", "tierthreechain", "angmarleggingsmedium")]
                    for head in (ANGMAR_RAIDER_HEAD, MORD_RAIDER_HEAD, MORD_WARRIOR_HEAD, GUNDABAD_TUSKED_HEAD)],
        "warrior": [[angmar("head", "tierfourplate", helmet), ANGMAR_SOLDIER_BODY, ANGMAR_SOLDIER_LEGS]
                    for helmet in ("angmarhelmetheavy", "angmarhelmetheavy2")],
        "captain": [[angmar("head", "tierfiveplate", helmet), angmar("body", "tierfiveplate", "angmararmorheavier"),
                     angmar("legs", "tierfiveplate", "angmarleggingsheavier")]
                    for helmet in ("angmarhelmetheavier", "angmarhelmetheavier2")],
    },
    # Misty Mountains (norc mod): Moria Orc for recruits, Gundabad Orc for warriors, Gundabad Warlord for captains (user).
    # 0.0.82 (owner's list, "Northern Orcs", and "give the new option listed"): "Moria orc: Full moria set, can wear
    # gundabad warrior helmet, orc snaga helmet" (snaga = the Mordor Orc Raider helmet, my guess: no item has that
    # name); "Gundabad orc: Full gundabad plate, can wear either gundabad orc warrior helmet or black uruk helmet"
    # (= Mordor's Orc Warrior helmet; this replaces 0.0.77's Moria helmet); "Gundabad warlord: Full gundabad warlod
    # plate. Can also have Gundabad orc warrior helmet."
    "goblin": {
        "recruit": [[head, norc("body", "tierthreechain", "norcarmormedium"), norc("legs", "tierthreechain", "norcleggingsmedium")]
                    for head in (norc("head", "tierthreechain", "norchelmetmedium"), GUNDABAD_HEAD, MORD_RAIDER_HEAD)],
        "warrior": [[head, GUNDABAD_BODY, GUNDABAD_LEGS] for head in (GUNDABAD_HEAD, MORD_WARRIOR_HEAD)],
        "captain": [[head, norc("body", "tierfiveplate", "norcarmorheavier"), norc("legs", "tierfiveplate", "norcleggingsheavier")]
                    for head in (norc("head", "tierfiveplate", "norchelmetheavier"), GUNDABAD_HEAD)],
    },
    # Isengard (iseng mod): Uruk-hai Scout for recruits, Infantry (clean or painted) for warriors,
    # painted Infantry with the General helmet for captains.
    "uruk": {
        "recruit": [[uruk("head", "tierthreechain", "urukhaihelmetscout", "clean"),
                     uruk("body", "tierthreechain", "urukhaiarmormedium", "clean"),
                     uruk("legs", "tierthreechain", "urukhaileggingsmedium", "clean")]],
        # 0.0.82 (owner's list): "Uruk-hai Warrior: Uruk warrior only. Any Uruk helmet except for scout or general."
        # (infantry, berserker, archer; clean or painted like the body; the legs exist only clean). "Uruk-hai Captain:
        # Same as warrior but only General helmets."
        "warrior": [[uruk("head", "tierfourplate", helmet, f), uruk("body", "tierfourplate", "urukhaiarmorheavy", f), URUK_INFANTRY_LEGS]
                    for helmet in ("urukhaihelmetinfantry", "urukhaihelmetberserker", "urukhaihelmetarcher") for f in ("clean", "painted")],
        "captain": [[uruk("head", "tierfourplate", "urukhaihelmetgeneral", f), uruk("body", "tierfourplate", "urukhaiarmorheavy", f), URUK_INFANTRY_LEGS]
                    for f in ("clean", "painted")],
    },
}

# Gundabad (0.0.62, work in progress; user, 2026-11-03: "I added a mod called gundabadarmor_0.1.0. call on it for a
# gundabad faction for a work in progress"): the owner's Gundabad Bone Armor mod (mod id gundabad) has one set, a
# chestplate with gauntlets and leggings with boots, and no helmet. Every rank wears it, bare-headed (user: "No
# helmets"). Without the mod they wear the vanilla rank outfit, minus its helmets (GUNDABAD_FALLBACK).
GUNDABAD_BONE = ["gundabad:armor-body-tierthreebrig-gundabadbonearmor-gundabadbonearmor-none",
                 "gundabad:armor-legs-tierthreebrig-gundabadboneleggings-gundabadboneleggings-none"]
FACTION_SETS["gundabadorc"] = {rank: [GUNDABAD_BONE] for rank in ("recruit", "warrior", "captain")}
# 0.0.76 (user: "remove wip from gundabad. and give the warrior the gundabad helmet / armor from the angmar misty
# mountains mod"): warriors wear the norc mod's Gundabad Orc set, as Misty Mountains warriors do. Recruits and
# captains keep the bone armor.
FACTION_SETS["gundabadorc"]["warrior"] = [[norc("head", "tierfourplate", "norchelmetheavy"), GUNDABAD_BODY, GUNDABAD_LEGS]]
# 0.0.95 (owner, 2026-11-06: "make the gundabad tusked orc helmet an option for the gundabad orcs. and give the gundabad
# captain the norc warlord options" ... "give the gundabad captain this helmet option" (Gundabad_Helmet_Variant_1);
# "make the tusked helmet, warrior +. have the misty mountains captain keep the gundabad helmet, and the gundabad
# captain, get this variant"): warriors also wear the tusked helmet; captains wear the Gundabad Warlord body and legs
# with the warlord helmet, the tusked helmet or the owner's helmet (gundabad-bone-armor 0.3.0,
# tools/gundabad_helmet_mod.py), or the bone armor as before. Recruits stay bare-headed in bone armor.
GUNDABAD_HELMET_ONE = "gundabad:armor-head-tierfourplate-gundabadhelmetone-gundabadhelmetone-none"
GUNDABAD_WARLORD = [norc("body", "tierfiveplate", "norcarmorheavier"), norc("legs", "tierfiveplate", "norcleggingsheavier")]
FACTION_SETS["gundabadorc"]["warrior"] = [[head, GUNDABAD_BODY, GUNDABAD_LEGS] for head in (GUNDABAD_HEAD, GUNDABAD_TUSKED_HEAD)]
FACTION_SETS["gundabadorc"]["captain"] = ([[head] + GUNDABAD_WARLORD for head in
                                          (norc("head", "tierfiveplate", "norchelmetheavier"), GUNDABAD_TUSKED_HEAD, GUNDABAD_HELMET_ONE)])
# 0.0.96 (owner, 2026-11-07: "the gundabad captain should not spawn in the bone armor like it does"): the bone armor
# set is gone from captains. Captains already spawned keep what they wear (gear is handed out at spawn).

# Shared orc helmets (0.0.55, user, 2026-11-03: "for all the orc factions except isengard, their helmets should be
# interchangable, for the raider helmet, soldier helmets, and warrior helmets. the moria and gundabad orc helmets
# should remain independent for their faction. as well as commander helmets remaining independent"). Mordor, Angmar
# and Misty Mountains recruits and warriors also get each shared helmet with their own body and legs, as extra whole
# sets after their own (portraits show the first). A set whose helmet mod isn't installed is skipped at spawn.
ANGMAR_SOLDIER_HEAD = angmar("head", "tierfourplate", "angmarhelmetheavy")
SHARED_ORC_HELMETS = {"recruit": [MORD_RAIDER_HEAD, ANGMAR_RAIDER_HEAD], "warrior": [MORD_WARRIOR_HEAD, ANGMAR_SOLDIER_HEAD]}
SHARED_HELMET_FACTIONS = ("mordororc", "angmarorc", "goblin")


def is_head(code):
    return code.split(":", 1)[-1].startswith("armor-head-")


def share_orc_helmets():
    for faction in SHARED_HELMET_FACTIONS:
        for rank, helmets in SHARED_ORC_HELMETS.items():
            sets = FACTION_SETS[faction][rank]
            extra = []
            for own in list(sets):
                rest = [c for c in own if not is_head(c)]
                for helmet in helmets:
                    candidate = [helmet] + rest
                    if candidate not in sets and candidate not in extra:
                        extra.append(candidate)
            sets.extend(extra)


# 0.0.82: the owner's loadout list names every helmet each orc unit may wear, so the shared helmets are no longer added
# (share_orc_helmets is kept for reference, not called).

# 0.0.35 (user, 2026-11-02): the other factions wear their own "To Rule Them All" sets where a mod has one, with
# the medium set for recruits, the heavy set for warriors and the heaviest for captains. Haradrim, Easterlings and
# Tormented Elves have no armor mod of their own, so 0.0.38 (user, 2026-11-03: "the most appropriate armor sets in
# regard to similarity to their intention") borrowed the closest evil-aligned sets, mixed with vanilla pieces in
# 0.0.39; since 0.0.41 they wear the outfits the user picked, at the end of FACTION_SETS.
def trta(domain, part, tier, construct, material=None, last="none"):
    return f"{domain}:armor-{part}-{tier}-{construct}-{material or construct}-{last}"
def trta_set(domain, tier, head, body, legs, last="none"):
    """A whole set whose items repeat their construct as the material."""
    return [trta(domain, "head", tier, head, last=last), trta(domain, "body", tier, body, last=last),
            trta(domain, "legs", tier, legs, last=last)]
def gond(part, tier, construct, material=None, last="none"):
    return trta("gond", part, tier, construct, material, last)
GONDOR_INFANTRY_LEGS = gond("legs", "tierfourplate", "gondorleggingsheavy")
def roha(part, tier, construct, material=None):
    return trta("roha", part, tier, construct, material)
def bnum(part, tier, construct, material=None, last="none"):
    return trta("mord", part, tier, construct, material, last)
def van(part, kind):
    """A vanilla armor piece, such as van("body", "lamellar-copper")."""
    return f"game:armor-{part}-{kind}"
ANGMAR_RANGER = [trta("angmar", "body", "tiertwogambeson", "angmararmorranger"),
                 trta("angmar", "legs", "tiertwogambeson", "angmarleggingsranger")]
# Helmets that also hide beards and mustaches (0.0.81, owner: "beards cannot be worn with these helmets either", the
# Haradrim helmets: veils and a mask over the face). Wildcards, read by lotrnpcs:helmethidehair.
BEARD_HIDING_HELMETS = ["haradrimarmor:armor-head-*"]
# 0.0.96 (owner, 2026-11-07: "with the new gundabad helmet, the ears stick out from the sides"): helmets that also hide
# the ears skin part (orc ears), so they don't poke out of the helmet's sides. Only the owner's Gundabad Helmet.
EAR_HIDING_HELMETS = [GUNDABAD_HELMET_ONE]

# The haradrimarmor mod's items (tools/haradrim_armor_mod.py: armor-<part>-<tier>-haradrim<set><piece>-...-none).
HARADRIM_ARMOR = {rank: [f"haradrimarmor:armor-{part}-{tier}-haradrim{rank}{piece}-haradrim{rank}{piece}-none"
                         for part, piece in (("head", "helmet"), ("body", "armor"), ("legs", "leggings"))]
                  for rank, tier in (("infantry", "tierthreechain"), ("heavy", "tierfourplate"), ("warlord", "tierfiveplate"))}
NADIYAN_CAPES = ["hunter", "peasant1", "peasant2", "peasant3", "shepherd"]
HARADRIM_OUTFITS = [["game:clothes-nadiya-foot-guard", f"game:clothes-nadiya-lowerbody-{pants}", f"game:clothes-nadiya-shoulder-{cape}",
                     van("body", f"tailored-{colour}-linen"), van("legs", f"tailored-{colour}-linen"),
                     "game:clothes-upperbodyover-forgotten", "game:clothes-waist-sturdy-leather-belt",
                     "game:clothes-shoulder-furwrap@neck", "game:clothes-face-miner"]
                    for colour in ("red", "black") for pants in ("guard", "hunter") for cape in NADIYAN_CAPES]
FACTION_SETS.update({
    # Gondor: Man-at-Arms (black, or the White Tree, with or without the coif) for recruits, Infantry (with or
    # without the cloak) for warriors, Fountain Guard with Infantry leggings for captains (the mod has no Fountain
    # Guard leggings).
    # 0.0.82 (owner's list): "Gondorian Man-At-Arms: Either MAA set. MAA or Levy helmet." Every Man-at-Arms colour with
    # its own legs (the White Tree breastplate has no legs of its own: black). "Gondorian Infantry: Full plate only."
    # "Gondorian Fountain Guard: Fountain Guard with cloak only ... Legs should be infantry." Rangers: the archers.
    "gondor": {
        "recruit": [[helmet, gond("body", "tierthreechain", *body), gond("legs", "tierthreechain", "gondorleggingsmedium", "gondorleggingsmedium" + legs)]
                    for body, legs in ((("gondorarmorarms", "gondorarmorarms" + c), c) for c in ("blue", "black", "green", "yellow"))
                    for helmet in (gond("head", "tierthreechain", "gondorhelmetarms"), gond("head", "tierthreechain", "gondorhelmetarms", last="chain"),
                                   gond("head", "tierfourplate", "gondorlevyhelmet"))]
                   + [[helmet, gond("body", "tierthreechain", "gondorarmorarmsblacktwo"), gond("legs", "tierthreechain", "gondorleggingsmedium", "gondorleggingsmediumblack")]
                      for helmet in (gond("head", "tierthreechain", "gondorhelmetarms"), gond("head", "tierthreechain", "gondorhelmetarms", last="chain"),
                                     gond("head", "tierfourplate", "gondorlevyhelmet"))],
        "warrior": [[gond("head", "tierfourplate", "gondorhelmetinfantry"),
                     gond("body", "tierfourplate", "gondorarmorheavy", last=cloak), GONDOR_INFANTRY_LEGS]
                    for cloak in ("none", "cloaked")],
        "captain": [[gond("head", "tierfiveplate", "gondorhelmetinfantry", "gondorhelmetfountain"),
                     gond("body", "tierfiveplate", "gondorarmorfountain", last="cloaked"), GONDOR_INFANTRY_LEGS]],
    },
    # Rohan: Man-at-Arms for recruits, Eorling for warriors (green or red, matching legs), Huscarl for captains.
    # 0.0.82 (owner's list): "Rohirric Warrior: Warrior set only" (the Man-at-Arms set: the mod has none called warrior;
    # my reading), "Eorling: Eorling set only", "Huscarl: Huscarl only". Unchanged.
    "rohan": {
        "recruit": [[roha("head", "tierthreechain", "rohanhelmetmedium"),
                     roha("body", "tierthreechain", "rohanarmormedium", "rohanarmormedium" + c),
                     roha("legs", "tierthreechain", "rohanleggingsmedium", "rohanleggingsmedium" + c)] for c in ("green", "red")],
        "warrior": [[roha("head", "tierfourscale", "rohanhelmetheavy"),
                     roha("body", "tierfourscale", "rohanarmorheavy", "rohanarmorheavy" + c),
                     roha("legs", "tierfourscale", "rohanleggingsheavy", "rohanleggingsheavy" + c)] for c in ("green", "red")],
        "captain": [trta_set("roha", "tierfivescale", "rohanhelmetheavier", "rohanarmorheavier", "rohanleggingsheavier")],
    },
    # Dale (Dale and Dorwinion mod): Lake Guard, Dale Soldier, Dale Captain.
    "dale": {
        "recruit": [trta_set("dale", "tierthreechain", "dalehelmetmedium", "dalearmormedium", "daleleggingsmedium")],
        "warrior": [trta_set("dale", "tierfourscale", "dalehelmetheavy", "dalearmorheavy", "daleleggingsheavy")],
        "captain": [trta_set("dale", "tierfivescale", "dalehelmetheavier", "dalearmorheavier", "daleleggingsheavier")],
    },
    # Arnor: Arnorian Infantry, Soldier, Lord. 0.0.82 (owner's list: "Levy: Full levy set, always helmed"; "Arnor
    # warrior: Full set, can be unhelmed and wear levy helmet"; owner: "use the infantry helm"): the Levy is the
    # Arnorian Infantry set, and warriors may wear its helmet.
    "arnor": {
        "recruit": [trta_set("arnor", "tierthreechain", "arnorhelmetmedium", "arnorarmormedium", "arnorleggingsmedium")],
        "warrior": [trta_set("arnor", "tierfourplate", "arnorhelmetheavy", "arnorarmorheavy", "arnorleggingsheavy"),
                    [trta("arnor", "head", "tierthreechain", "arnorhelmetmedium"), trta("arnor", "body", "tierfourplate", "arnorarmorheavy"),
                     trta("arnor", "legs", "tierfourplate", "arnorleggingsheavy")]],
        "captain": [trta_set("arnor", "tierfiveplate", "arnorhelmetheavier", "arnorarmorheavier", "arnorleggingsheavier")],
    },
    # Dunland (Isengard mod): Dunlending Raider, Warrior, Warlord (either helmet).
    "dunlending": {
        "recruit": [trta_set("iseng", "tierthreechain", "dunlendinghelmetmedium", "dunlendingarmormedium",
                             "dunlendingleggingsmedium", "clean")],
        # 0.0.82 (owner's list): "Dunlending Warrior: Warrior only for armor, can wear raider helmet and unhelmed."
        "warrior": [trta_set("iseng", "tierfourchain", "dunlendinghelmetheavy", "dunlendingarmorheavy",
                             "dunlendingleggingsheavy", "clean"),
                    [trta("iseng", "head", "tierthreechain", "dunlendinghelmetmedium", last="clean"),
                     trta("iseng", "body", "tierfourchain", "dunlendingarmorheavy", last="clean"),
                     trta("iseng", "legs", "tierfourchain", "dunlendingleggingsheavy", last="clean")]],
        "captain": [trta_set("iseng", "tierfiveplate", helmet, "dunlendingarmorheavier", "dunlendingleggingsheavier", "clean")
                    for helmet in ("dunlendinghelmetheavier", "dunlendinghelmetheavier2")],
    },
    # Black Numenoreans (Mordor mod's human armor): Black Numenorean (brown or black) for recruits, black Servant of
    # the Eye (bare or black mask) for warriors, Black Numenorean Lord (with or without the cloak) for captains.
    # 0.0.82 (owner's list, read as this faction's units since factions don't mix races): "Black Numenorean: BN set
    # only, not Lord." "Servant of the Eye: Servant only." Recruits the Black Numenorean set (brown or black), warriors
    # the black Servant of the Eye (bare or black mask), captains the red one with the red mask (my split, so the ranks
    # still look different). No Lord set.
    "blacknumenorean": {
        "recruit": [[bnum("head", "tierthreescale", "servanthelmethumanmedium"),
                     bnum("body", "tierthreescale", "servantarmorhumanmedium", "servantarmorhumanmedium" + c),
                     bnum("legs", "tierthreescale", "servantleggingshumanmedium")] for c in ("brown", "black")],
        "warrior": [[bnum("head", "tierfourplate", "servanthelmethumanheavy", last=mask),
                     bnum("body", "tierfourplate", "servantarmorhumanheavy", "servantarmorhumanheavyblack"),
                     bnum("legs", "tierfourplate", "servantleggingshumanheavy")] for mask in ("none", "maskblack")],
        "captain": [[bnum("head", "tierfourplate", "servanthelmethumanheavy", last="maskred"),
                     bnum("body", "tierfourplate", "servantarmorhumanheavy", "servantarmorhumanheavyred"),
                     bnum("legs", "tierfourplate", "servantleggingshumanheavy")]],
    },
    # Dwarves (Dwarves mod): Militia (brown), Warrior, Lord.
    "dwarf": {
        "recruit": [[trta("dwarf", "head", "tierthreechain", "dwarfhelmetmedium"),
                     trta("dwarf", "body", "tierthreechain", "dwarfarmormedium", "dwarfarmormediumbrown"),
                     trta("dwarf", "legs", "tierthreechain", "dwarfleggingsmedium")]],
        "warrior": [trta_set("dwarf", "tierfourplate", "dwarfhelmetheavy", "dwarfarmorheavy", "dwarfleggingsheavy")],
        "captain": [trta_set("dwarf", "tierfiveplate", "dwarfhelmetheavier", "dwarfarmorheavier", "dwarfleggingsheavier")],
    },
    # Wicked dwarves (Dwarves mod): Footman, Warrior, Lord.
    "wickeddwarf": {
        "recruit": [trta_set("dwarf", "tierthreechain", "evildwarfhelmetmedium", "evildwarfarmormedium", "evildwarfleggingsmedium")],
        "warrior": [trta_set("dwarf", "tierfourplate", "evildwarfhelmetheavy", "evildwarfarmorheavy", "evildwarfleggingsheavy")],
        "captain": [trta_set("dwarf", "tierfiveplate", "evildwarfhelmetheavier", "evildwarfarmorheavier", "evildwarfleggingsheavier")],
    },
    # Haradrim, Easterlings and tormented elves have no armor mod of their own. 0.0.41 (user, 2026-11-03) gives each the
    # outfit the user picked, mostly vanilla clothing and armor. An item code may end in "@slot" (a gear slot such as
    # face or neck) to wear it in that slot instead of its usual one, so an NPC can wear two pieces meant for the same
    # slot; the gear renderer draws every slot.
    # Haradrim, every rank alike (the user named no rank differences): Nadiyan guard boots, Nadiyan guard or hunter
    # pants, any Nadiyan cape, a black or red tailored gambeson (body and legs), the Forgotten noble robe, a sturdy
    # leather belt, the fur wrap (worn in the neck slot, since the cape takes the shoulder slot) and the miner mask.
    # 0.0.78 (owner, 2026-11-04: "Use these as skins for the haradrim, mostly as a temporary placeholder"): the owner's
    # three armor models as a separate armor mod, haradrimarmor (tools/haradrim_armor_mod.py, built to
    # build-inputs/haradrim-armor-<version>.zip): infantry for recruits (and archers), heavy infantry for warriors,
    # warlord for captains. Without that mod they wear the outfits above (FALLBACK_SETS), never a mix of the two.
    "haradrim": {"recruit": [HARADRIM_ARMOR["infantry"]], "warrior": [HARADRIM_ARMOR["heavy"]],
                 "captain": [HARADRIM_ARMOR["warlord"]]},
    # Easterlings (user): tin bronze brigandine for recruits, tin bronze plate for warriors, and for captains tin bronze
    # plate with the Woodland Sentinel helmet over an iron chain coif covering the face (worn in the face slot).
    "easterling": {
        "recruit": [[van(part, "brigandine-tinbronze") for part in ("head", "body", "legs")]],
        "warrior": [[van(part, "plate-tinbronze") for part in ("head", "body", "legs")]],
        "captain": [[trta("wlel", "head", "tierthreescale", "woodlandhelmetmedium"), van("head", "chain-iron") + "@face",
                     van("body", "plate-tinbronze"), van("legs", "plate-tinbronze")]],
    },
    # Tormented elves (user): the Angmar Ranger tunic and leggings, bareheaded, and the captain in the Rotten king crown.
    "tormentedelf": {
        "recruit": [ANGMAR_RANGER],
        "warrior": [ANGMAR_RANGER],
        "captain": [ANGMAR_RANGER + ["game:clothes-head-rottenking"]],
    },
    # Elves (Woodland Realm mod, the only elf armor): Woodland Sentinel, Wood-elf Warrior, Mirkwood Gate Warden.
    "elf": {
        "recruit": [trta_set("wlel", "tierthreescale", "woodlandhelmetmedium", "woodlandarmormedium", "woodlandleggingsmedium")],
        "warrior": [trta_set("wlel", "tierfourscale", "woodlandhelmetheavy", "woodlandarmorheavy", "woodlandleggingsheavy")],
        "captain": [trta_set("wlel", "tierfivescale", "woodlandhelmetheavier", "woodlandarmorheavier", "woodlandleggingsheavier")],
    },
})


# Hobbits (0.0.55, user, 2026-11-03: "give them the clothes similar to vanilla traders, minus any helmets. instead
# maybe a hat on rare occasion"). Vanilla traders' outfits are drawn onto their model, not worn items, so these are
# vanilla clothes in the same plain, homely trader style: a shirt, trousers or breeches, and a belt, waistcoat, apron
# or scarf. All vanilla, so they work on both servers. Hobbits stay barefoot. One outfit per hobbit at random; a hat
# one time in eight.
def _c(*codes):
    return ["game:clothes-" + c for c in codes]
HOBBIT_SETS = [
    _c("upperbody-peasent-shirt", "lowerbody-braiesbrown", "waist-peasant-strap"),
    _c("upperbody-homespun-shirt", "lowerbody-homespun-pants", "upperbodyover-quilteddoublet"),
    _c("upperbody-commoner-shirt", "lowerbody-commoner-trousers", "waist-sturdy-leather-belt"),
    _c("upperbody-pastoral-shirt", "lowerbody-pastoral-pants", "shoulder-apronbrown"),
    _c("upperbody-merchant-shirt", "lowerbody-merchant-pants", "waist-merchant-belt"),
    _c("upperbody-farmhand", "lowerbody-farmhand", "waist-farmhand"),
    _c("upperbody-ruralfarmer", "lowerbody-braiesplain", "waist-ruralfarmer"),
    _c("upperbody-linen-tunic", "lowerbody-workmans-gown", "shoulder-woolen-scarf"),
    _c("upperbody-woolen-shirt", "lowerbody-shepherd-pants", "upperbodyover-commoner-coat"),
    _c("upperbody-tailor-blouse", "lowerbody-tailor-trousers", "upperbodyover-tailor-jacket"),
    _c("upperbody-messenger-shirt", "lowerbody-messenger-trousers", "waist-messenger-belt"),
    _c("upperbody-marketeer", "lowerbody-marketeer", "waist-marketeer"),
]
HOBBIT_GEAR = HOBBIT_SETS[0]
HOBBIT_HATS = {"chance": 0.125, "items": _c("head-straw-hat", "head-roll-hat", "head-broadbrim", "head-thrumcap",
                                              "head-capotain", "head-merchant-hat")}
HATS = {"hobbit": HOBBIT_HATS}
FACTION_SETS["hobbit"] = {"recruit": HOBBIT_SETS}
# Archers wear their faction's recruit armor, with the same helmets: Mordor's light Orc Raider mail since 0.0.55, the
# other factions' recruit sets since 0.0.69/0.0.70 (Gundabad's bone armor is the same for every rank).
for _f in ARCHER_RACES:
    FACTION_SETS[_f]["archer"] = [list(st) for st in FACTION_SETS[_f]["recruit"]]
# 0.0.82 (owner's list): "Gondorian Ranger: Ranger set only, not chainmail variant."
FACTION_SETS["gondor"]["archer"] = [[gond("head", "tiertwogambeson", "gondorrangerhelmet", last="gondorrangermask"),
                                     gond("body", "tiertwogambeson", "gondorrangerarmor"), gond("legs", "tiertwogambeson", "gondorrangerleggings")]]

# Spawning bare-headed (0.0.82, owner's list: "All orcs and uruks can be unhelmed unless otherwise noted", plus each
# unit marked "can be unhelmed"; owner: "all units should still have the option to be unhelmeted", so every unit keeps
# "No helmet" in the looks menu). A unit listed False always spawns with its helmet; True rolls HelmetChance. Factions
# the list doesn't cover keep rolling as before.
ORC_RACES = {"mordororc", "angmarorc", "goblin", "uruk", "gundabadorc"}
HELMET_ALWAYS = {
    "gondor": {"recruit", "warrior", "captain", "archer"},
    "rohan": {"recruit", "archer"},
    "dunlending": {"captain"},
    "elf": {"recruit", "warrior", "captain", "archer"},
    "dwarf": {"captain"}, "wickeddwarf": {"captain"},
    "dale": {"recruit", "warrior", "archer"},
    "arnor": {"recruit", "archer"},
    "blacknumenorean": {"recruit", "warrior", "captain", "archer"},
}
def helmet_optional(faction, rank):
    if faction in ORC_RACES:
        return True
    return rank not in HELMET_ALWAYS.get(faction, set())


# Sets worn only when none of a type's FACTION_SETS is complete (0.0.78): the Haradrim's 0.0.41 outfits stand in when
# the haradrimarmor mod isn't installed. Archers wear the recruit ones, as with FACTION_SETS.
FALLBACK_SETS = {"haradrim": {rank: HARADRIM_OUTFITS for rank in ("recruit", "warrior", "captain", "archer")}}


def ranks(race, people, model, skin):
    """Recruit, warrior and captain test NPCs of one faction."""
    return [(f"test-{race}-{rank}", f"{people} {rank}", model, skin, gear)
            for rank, gear in (("recruit", RECRUIT_GEAR), ("warrior", WARRIOR_GEAR), ("captain", CAPTAIN_GEAR), ("archer", RECRUIT_GEAR))]


TEST_NPCS = [
    ("test-goblin", "Goblin", "lotrnpcs:goblin", SKINS + "orc/orcskin6", []),
    ("test-mordororc-recruit", "Mordor orc recruit", "lotrnpcs:mordororc", ORC_SKIN, RECRUIT_GEAR),
    ("test-mordororc-warrior", "Mordor orc warrior", "lotrnpcs:mordororc", ORC_SKIN, WARRIOR_GEAR),
    ("test-mordororc-captain", "Mordor orc captain", "lotrnpcs:mordororc", ORC_SKIN, CAPTAIN_GEAR),
    ("test-mordororc-archer", "Mordor orc archer", "lotrnpcs:mordororc", ORC_SKIN, RECRUIT_GEAR),
    # Angmar orcs (user): their own faction, same race model and stats as Mordor orcs, Angmar armor.
    ("test-angmarorc-recruit", "Angmar orc recruit", "lotrnpcs:mordororc", ORC_SKIN, RECRUIT_GEAR),
    ("test-angmarorc-warrior", "Angmar orc warrior", "lotrnpcs:mordororc", ORC_SKIN, WARRIOR_GEAR),
    ("test-angmarorc-captain", "Angmar orc captain", "lotrnpcs:mordororc", ORC_SKIN, CAPTAIN_GEAR),
    ("test-angmarorc-archer", "Angmar orc archer", "lotrnpcs:mordororc", ORC_SKIN, RECRUIT_GEAR),
    ("test-uruk", "Uruk-hai", "lotrnpcs:uruk", SKINS + "orc/orcskin9", []),
    # Misty Mountains orcs and Isengard Uruk-hai (user): same ranks and stats as Mordor orcs, their own armor.
    ("test-goblin-recruit", "Misty Mountains orc recruit", "lotrnpcs:goblin", SKINS + "orc/orcskin6", RECRUIT_GEAR),
    ("test-goblin-warrior", "Misty Mountains orc warrior", "lotrnpcs:goblin", SKINS + "orc/orcskin6", WARRIOR_GEAR),
    ("test-goblin-captain", "Misty Mountains orc captain", "lotrnpcs:goblin", SKINS + "orc/orcskin6", CAPTAIN_GEAR),
    ("test-goblin-archer", "Misty Mountains orc archer", "lotrnpcs:goblin", SKINS + "orc/orcskin6", RECRUIT_GEAR),
    ("test-uruk-recruit", "Isengard Uruk-hai recruit", "lotrnpcs:uruk", SKINS + "orc/orcskin9", RECRUIT_GEAR),
    ("test-uruk-warrior", "Isengard Uruk-hai warrior", "lotrnpcs:uruk", SKINS + "orc/orcskin9", WARRIOR_GEAR),
    ("test-uruk-captain", "Isengard Uruk-hai captain", "lotrnpcs:uruk", SKINS + "orc/orcskin9", CAPTAIN_GEAR),
    ("test-uruk-archer", "Isengard Uruk-hai archer", "lotrnpcs:uruk", SKINS + "orc/orcskin9", RECRUIT_GEAR),
    # Gundabad (0.0.62, work in progress): the Mordor orc body at 0.9 size (user's choice), the bone armor, no helmets.
    # 0.0.63: their own race model, lotrnpcs:gundabadorc (bundle_lrc.py), the Mordor orc with lighter skins.
    *[(f"test-gundabadorc-{rank}", f"Gundabad orc {rank}", "lotrnpcs:gundabadorc", SKINS + "orc/orcskin1", GUNDABAD_FALLBACK[rank])
      for rank in ("recruit", "warrior", "captain", "archer")],
    ("test-hobbit", "Hobbit", "lotrnpcs:hobbit", SKINS + "hobbit/hobbitskin1", HOBBIT_GEAR),
    # 0.0.35 (user, 2026-11-02): every faction has recruits, warriors and captains. These replace the unarmored
    # Gondor, Rohan, Dunlending, Easterling and Dwarf test NPCs.
    *ranks("gondor", "Gondor", "lotrnpcs:gondor", SKINS + "human/westernmen/humanskin1"),
    *ranks("rohan", "Rohan", "lotrnpcs:rohan", SKINS + "human/westernmen/humanskin1"),
    *ranks("dale", "Dale", "lotrnpcs:dale", SKINS + "human/westernmen/humanskin2"),
    *ranks("arnor", "Arnor", "lotrnpcs:arnor", SKINS + "human/westernmen/humanskin27"),
    *ranks("haradrim", "Haradrim", "lotrnpcs:haradrim", SKINS + "human/haradrim/humanskin23"),
    *ranks("dunlending", "Dunlending", "lotrnpcs:dunlending", SKINS + "human/westernmen/humanskin1"),
    *ranks("blacknumenorean", "Black Númenórean", "lotrnpcs:blacknumenorean", SKINS + "human/blacknumenor/humanskin17"),
    *ranks("easterling", "Easterling", "lotrnpcs:easterling", SKINS + "human/easterling/humanskin10"),
    *ranks("dwarf", "Dwarf", "lotrnpcs:dwarf", SKINS + "dwarf/dwarfskin1"),
    *ranks("wickeddwarf", "Wicked dwarf", "lotrnpcs:wickeddwarf", SKINS + "dwarf/dwarfskin3"),
    *ranks("elf", "Elf", "lotrnpcs:elf", SKINS + "elf/elfskin1"),
    *ranks("tormentedelf", "Tormented elf", "lotrnpcs:tormentedelf", SKINS + "elf/elfskin11"),
]
# Natural spawning (user, 2026-11-02): recruits, warriors and rare captains spawn on the surface inside admin
# zones (/recruits zone) that list their faction, one group every 3 to 5 minutes near a player. SpawnZones.cs does all
# of it, finding the NPC types by these lotrnpcsFaction and lotrnpcsRank attributes; there are no vanilla spawn
# rules, because a vanilla spawn chance can't promise a steady rate (0.0.29 used them).
# Every NPC's faction id (the code's race part -> faction), read by the faction system from lotrnpcsFaction.
FACTION_OF = {"mordororc": "mordor", "angmarorc": "angmar", "goblin": "mistymountains", "uruk": "isengard", "gundabadorc": "gundabad",
              "gondor": "gondor", "rohan": "rohan", "dale": "dale", "arnor": "arnor", "haradrim": "haradrim",
              "dunlending": "dunlendings", "blacknumenorean": "blacknumenoreans", "easterling": "easterlings",
              "dwarf": "dwarves", "wickeddwarf": "wickeddwarves", "hobbit": "shire", "elf": "elves",
              "tormentedelf": "tormentedelves"}
# NPCs whose code doesn't end in their rank. Hobbits have one rank, farmers, which counts as the Shire's recruit rank
# (user, 2026-11-02: "make them recruit equivalent"), so they get recruit stats and Shire zones spawn them.
RANK_OF = {"test-hobbit": "recruit"}
# Left out of the creative menu (user, 2026-11-03: "in the menu there is still basic npcs for uruk hai, goblin, and
# hobbit"). Their entity types stay, so NPCs already in a world keep loading. Hobbits came back in 0.0.42 (user: "put
# hobbits in the spawn menu anyway").
HIDDEN = {"test-goblin", "test-uruk"}


# Names on hire (0.0.54, user, 2026-11-03: "when hired have their name be randomized anyway to names befitting the
# race"). Written in the style of each people, avoiding the names of well-known characters from the books and films.
# Edit freely: Looks.cs picks one at random (hobbits add a family name from HOBBIT_SURNAMES).
def _n(text):
    return [w for w in text.split()]
NAMES = {
    "orc": _n("""Grushnak Mauzgul Ughrath Skabrak Narzug Gorbuth Lugmakh Rathgul Krimpul Ushnar Dumbag Shagrol Brogga
        Yagrish Gazmog Thrakul Ograth Snurk Zogbad Ghashnak Rudluk Morgrub Drabshak Krulg Burzak Ulgrod Garluk Skrith
        Nargath Grishmuk Shagbur Mugrot Dushnak Kragor Uftak Gorgash Bolgrim Snagrut Zarbuk Hurlug Grakhul Muzrak"""),
    "goblin": _n("""Snikrit Gribble Nazzik Skritch Gobbrak Ruknit Zitgob Yikrak Snatchgut Mogwit Pikgrub Grittle Skabnit
        Ruzgob Fizzak Gnashtooth Splitnail Krikkit Nibbluk Zagrat Skulk Ratchit Grubnose Wizzug Snarlik Tiktak Muckrot
        Blitgrub Snivvek Kribnak Gnasher Squiggot Rotfang Glimrit Yarnik Skreek Durnak Zibbit Grizzlak Pizgut"""),
    "uruk": _n("""Ugrath Mazrûkh Thrakdur Gorzhak Durbag Krazgul Mogrûsh Ulgrath Shazbur Barzog Ruknar Grôdush Uzbrak
        Hragmor Zograth Drûbak Narghul Vragash Burzûk Gashnar Mûrzag Throkul Ozgrim Kardûk Rûgash Skorbag Grimûk Lûthrak
        Dagrûm Krûdash Morzag Ulbrûk Thrazûl Bagrûsh Zûrgath Harkûn Gûlmok Raznûk Draugûl Vûrzak"""),
    "gondor": _n("""Arthondir Belegorn Caladhir Dorhalion Ecthelorn Galdrion Hallanor Imlorn Lossorin Mardion Nimrion
        Orodhir Pelendir Rhovanor Silmandir Tarcalion Ulbarad Valandor Anardion Berendir Calimbor Dúinor Eranor Fendil
        Gwinoth Herumir Ithilmar Lothiran Mithrion Narmion Ostorion Pelarion Romendil Seregorn Thalorn Turmir Valarion
        Hirvorn Duinmar Elphion Morthil Lindorin"""),
    "rohan": _n("""Aldhelm Beorhtric Cynric Dernwine Eadric Ealdwine Eofor Folcred Garmund Godric Heoruwine Hunwald Leofric
        Osric Sigebert Wiglaf Wulfhere Aethelmund Brytmund Ceolric Deorhelm Ecgbert Eormen Frithwulf Gunthere Hereward
        Ingwald Leodwine Merewald Ordlaf Raedwald Saewine Thurstan Wigmund Wynstan Aelfric Beornwulf Cuthred Eadwulf
        Guthmund Oswulf Sigewine Wulfstan Eadgifu Hildeburh Leofrun Wynflaed Osgyth"""),
    "dale": _n("""Arnulf Bjarki Dagfinn Eilif Finnvard Geirmund Hallvard Ingolf Jorund Ketil Leif Mord Oddvar Ragnvald
        Sveinbjorn Thorvald Ulfar Vigfus Arnbjorn Bersi Dalgrim Egil Floki Grimkel Hrafn Ingvar Kolbein Ljot Orm Ragnar
        Starkad Thorkel Ulf Vali Asgaut Bodvar Eyvind Gunnar Halldor Hrolf Skuli Torfi Yngvar Asa Bergthora Gudrun Ingrid
        Sigrun"""),
    "arnor": _n("""Arandil Beleghir Caranhir Dirion Elathorn Faramund Gilrion Hallorn Ithildor Lothron Mallorin Nardil
        Ornedhel Tarondil Ulmorn Vorandir Adaneth Barandir Celebrion Dorlion Eradil Fornion Galadir Hiriel Imladir Lindorn
        Morandir Nimeth Ostrion Rhudion Sarnion Tharion Dúnhir Annúndir Fornedhel Calenor Eriadon Hathaldir Maelorn Thorondir"""),
    "haradrim": _n("""Azrabek Bahrun Dasharim Fazzad Haruzan Imradek Jharam Kasrul Malikar Qadirun Rashkar Sahrim Tazmir
        Umbarek Yazdar Zahruk Amrasad Barzim Dhurak Fahrun Ghazir Hassad Ilkhar Jamzir Karrash Mazrun Nurhad Orzam Qasrim
        Rahzar Sukhrad Tahrin Urzah Zafrim Azhara Dalima Jahrisa Nazira Samira Kharuzad"""),
    "dunlending": _n("""Bran Cadoc Dyfan Eogan Fergal Gwrach Hywel Ithel Lugaid Madoc Nechtan Owain Rhodri Seisyll Tegid
        Urien Brychan Cynan Drust Einion Fiach Garbh Iorwerth Llew Morcant Nudd Peredur Rhun Talorc Uaithne Cormac Donal
        Feidlim Gartnait Loarn Maelgwn Ninian Ruadh Tadhg Aine Brigid Morfudd Nest"""),
    "easterling": _n("""Altan Borjin Chagar Dorgun Erdan Ghulkan Hulagar Ishbek Jochir Kaidar Mönkar Nogai Ogdar Qutlug
        Sartan Temur Ulagan Yesun Zhurgen Arslan Batur Chinbat Daruga Ebugen Gansukh Khasar Kuchum Mergen Nairan Orluk
        Sübar Tolui Ulzan Yeke Bortai Khulan Oyuun Sorgan Tsetseg Alaqa"""),
    "blacknumenorean": _n("""Azrubêl Belzagar Dûnzirak Ghazulâth Hûrzimir Izrakhôr Kalimzôr Luthârzan Mazgûthar Nâzrimil
        Phârazul Rûthimôr Sakalthûn Thârzimil Ulbazôr Zimrakhôr Adrazân Bêlthazôr Dâgnimil Gimrazôr Hazrubân Imrakhûl
        Karanzôr Lôkhazir Mûrazân Nimruzîr Ôrzagal Rakhalzôr Sûrimbêl Tûrzagar Uzrakhôn Zâbarmil Azrabêth Inzilrâ
        Nimruzêth Zimrabêth Mîrazên Bêlzimrâ Khôrazân Thûlzagar"""),
    "dwarf": _n("""Alfr Andvari Austri Brokkr Eitri Fjalar Frosti Ginnar Hanarr Jari Litr Nyr Reginn Sviurr Skirvir Virvir
        Vestri Vigg Barzûn Durgrim Khazrik Thrandar Grimbur Hrundar Kôrnin Brodrin Fenrik Dolgrin Hamrin Brokkvar Thurbin
        Galdur Ragdur Skorri Ulfrin Vargrim Drogni Hjalmrin Kragni Thorgni Bari Hlorin Daggar"""),
    "elf": _n("""Amdirion Belegcam Caladwen Celebrin Daerion Edhelion Faelon Galadhorn Gilwen Hithaeron Ithilwen Laurion
        Lómedhel Mithwen Nimroth Orophel Rínon Sarnel Tathariel Uialwen Aerandil Belthanor Calenglad Duilinor Elenroth
        Faerwen Galathorn Hiriel Ilmendir Lossëwen Meludir Nenharma Onodrion Rochendil Silivren Thalanor Uinéniel Varnion
        Calaerwen Lothmir"""),
}
HOBBIT_NAMES = _n("""Aldo Basso Barnabas Bartholo Cobbin Cotman Dandry Elbo Emmet Falmo Fenno Gundo Hob Hobson Ivo Jasper
    Kipper Linden Lindo Milton Nedd Oddo Perry Quill Robin Tobble Willum Barley Clover Fern Hazel Myrtle Nell Petunia
    Primrose Tansy Violet Winnie Bramble Tilly""")
HOBBIT_SURNAMES = _n("""Applethorn Barleycorn Brackenbury Bunce Burrowell Copperkettle Dimbleby Fairmead Greenhill
    Hedgecroft Honeycomb Longacre Mossfoot Oakbottom Puddlewick Rushmead Sandyfoot Thistlewood Tunnelly Whitbrook
    Woodruff Elderberry Buttercup Bramblebank Hollowell Pebbleton Mallowby Cobblestone Hayward Furrowfield""")
# Which list each people uses (the code's race part).
NAMES_OF = {"mordororc": "orc", "angmarorc": "orc", "gundabadorc": "orc", "goblin": "goblin", "uruk": "uruk", "gondor": "gondor", "rohan": "rohan",
            "dale": "dale", "arnor": "arnor", "haradrim": "haradrim", "dunlending": "dunlending",
            "blacknumenorean": "blacknumenorean", "easterling": "easterling", "dwarf": "dwarf", "wickeddwarf": "dwarf",
            "elf": "elf", "tormentedelf": "elf"}


# Spawn item portraits (user, 2026-11-03: "villager esque item profile pictures of the head, some shoulder, and the
# armor the entity is wearing"): Portrait.cs builds them from these specs. Evil factions scowl, the others look resolute,
# hobbits are kind; a race model without that expression shows its first one.
EVIL = {"mordor", "angmar", "mistymountains", "gundabad", "isengard", "dunlendings", "blacknumenoreans", "easterlings", "haradrim",
        "wickeddwarves", "tormentedelves"}
PORTRAIT_FACE = {f: "angry" for f in EVIL} | {"shire": "kind"}
# Skin parts a race's portrait shows instead of the model's first variant. Without an entry the portrait takes the
# first hair style that isn't bald (Portrait.cs), since a bald head with the model's default pale features read as
# faceless (user, 2026-11-03). Dwarves get a full beard.
PORTRAIT_PARTS = {"dwarf": {"beard": "brd-box20-full"}, "wickeddwarf": {"beard": "brd-box20-full"}}
PORTRAITS = {}

for code, name, model, skin_texture, gear in TEST_NPCS:
    preset = {"code": "lotrnpcs:presetskinmodel", "skinModel": model}
    skin = [preset, {"code": "PlayerModelLib:ExtraSkinnable"}]
    # The gear inventory goes after Player Model Lib, which swaps in the race model that the gear is added onto.
    # 0.0.80 (owner: "when the helmet is on, hide the hair options underneath"): after the gear, so it can see a helmet.
    # 0.0.81 (owner: "beards cannot be worn with these helmets either"): the Haradrim helmets hide beards and mustaches too.
    client_gear = [{"code": "seraphinventory"}, {"code": "lotrnpcs:helmethidehair", "hideBeardUnder": BEARD_HIDING_HELMETS, "hideEarsUnder": EAR_HIDING_HELMETS}] if gear else []
    faction, _, rank = code.removeprefix("test-").partition("-")
    rank = RANK_OF.get(code, rank)
    sets = FACTION_SETS.get(faction, {}).get(rank, [])
    # 0.0.78: sets worn only when no set above is complete (the owner's armor mod isn't installed).
    fallback_sets = FALLBACK_SETS.get(faction, {}).get(rank, [])
    server_gear = [{"code": "seraphinventory"}, {"code": "lotrnpcs:startinggear", "sets": sets, "items": gear}
                   | ({"fallbackSets": fallback_sets} if fallback_sets else {})
                   | ({"hats": HATS[faction]} if faction in HATS else {})] if gear else []
    # Gear textures go before Player Model Lib, whose texture lookup only knows the race model's own textures.
    gear_textures = [{"code": "lotrnpcs:geartextures"}] if gear else []
    health, armor_tier, melee = RANK_STATS.get(rank, RANKLESS_STATS)
    attributes = {"lotrnpcsFaction": FACTION_OF[faction]}
    if code in HIDDEN:
        attributes["inCreativeInventory"] = False
    if rank in RANK_STATS:
        attributes["lotrnpcsRank"] = rank
    if faction == "hobbit":
        attributes["lotrnpcsNames"], attributes["lotrnpcsSurnames"] = HOBBIT_NAMES, HOBBIT_SURNAMES
    elif faction in NAMES_OF:
        attributes["lotrnpcsNames"] = NAMES[NAMES_OF[faction]]
    # The helmets its owner may choose in the looks menu (0.0.58, user, 2026-11-03: "the helmets only have the switch
    # on and off, not the option to choose the helmet"): every helmet in this type's armor sets. Not the vanilla
    # fallback outfit's, which would sit beside the faction helmets on a server with the armor mods; an NPC in the
    # fallback outfit still gets its own helmet as a choice (Looks.HelmetChoices adds the one worn or kept). Pieces with
    # an @slot (the Easterling captain's coif on the face) aren't helmets.
    # 0.0.76 (user: "Each rank of a faction should have access to the helmets of the ranks beneath it as well as its
    # own rank"): its own first, then the lower ranks'. Archers are promoted recruits and wear recruit sets already.
    lower = {"warrior": ["recruit"], "captain": ["recruit", "warrior"]}.get(rank, [])
    all_sets = sets + [st for r in lower for st in FACTION_SETS.get(faction, {}).get(r, [])]
    helmets = list(dict.fromkeys(c for s in all_sets for c in s if "armor-head-" in c and "@" not in c))
    if helmets:
        attributes["lotrnpcsHelmets"] = helmets
        # 0.0.82: whether it may spawn bare-headed (HELMET_ALWAYS); read by Looks.RollHelmet.
        attributes["lotrnpcsHelmetOptional"] = helmet_optional(faction, rank)
    PORTRAITS[code] = {"model": model, "skin": skin_texture,
                       "parts": {"facialexpression": PORTRAIT_FACE.get(FACTION_OF[faction], "determined")} | PORTRAIT_PARTS.get(faction, {}),
                       "sets": sets + fallback_sets, "items": gear}
    weapons = [held_weapon(faction, rank)] if rank == "archer" or ((faction in WEAPONS_OF or faction in SHIELD_OF or faction in WEAPON_PREFS) and rank in ("recruit", "warrior", "captain")) else []
    write(f"entities/{code}.json", entity(
        code, name, attributes,
        [preset] + gear_textures + skin[1:] + [{"code": "PlayerModelLib:EntityTextureSourceBehavior"}] + client_gear + weapons,
        skin + server_gear + weapons,
        health, armor_tier, melee, archer=rank == "archer",
    ))

write_weapons()

# The creative tab's order (0.0.71): free peoples, then the Shadow, each faction's units together, recruit to archer.
CREATIVE_FACTIONS = ["shire", "gondor", "rohan", "arnor", "dale", "dwarves", "elves",
                     "mordor", "angmar", "mistymountains", "gundabad", "isengard", "haradrim", "easterlings", "dunlendings",
                     "blacknumenoreans", "wickeddwarves", "tormentedelves"]
CREATIVE_RANKS = ["recruit", "warrior", "captain", "archer"]
def _creative_key(code):
    race, _, rank = code.removeprefix("test-").partition("-")
    return CREATIVE_FACTIONS.index(FACTION_OF[race]), CREATIVE_RANKS.index(RANK_OF.get(code, rank))
CREATIVE_ORDER = sorted((c for c, *_ in TEST_NPCS if c not in HIDDEN), key=_creative_key)
assert set(FACTION_OF.values()) == set(CREATIVE_FACTIONS)

write("itemtypes/creature.json", {
    "code": "creature",
    "class": "ItemCreature",
    "maxstacksize": 64,
    "variantgroups": [{"code": "type", "states": [c for c, *_ in TEST_NPCS]}],
    # The plain full-body mesh, shown only if a portrait can't be built.
    "shape": {"base": FA_BODY},
    "texturesByType": {f"*-{c}": {"seraph": {"base": skin}} for c, _, _, skin, _ in TEST_NPCS},
    "behaviors": [{"name": "lotrnpcs:npcportrait"}],
    "attributesByType": {f"*-{c}": {"lotrnpcsPortrait": spec} for c, spec in PORTRAITS.items()},
    # Own creative tab (user): "LOTR NPCs", plus the general tab so the search finds them. The hidden NPCs' items
    # stay registered, so ones already in an inventory don't turn into unknown items.
    # 0.0.71 (user, 2026-11-04: "re-order the creative inventory to have all the units of a faction next to each
    # other, not scattered about"): the game lists a tab by item id, and a world hands out ids in the order variants
    # first appeared (new archers landed at the end). So one variant (the hidden goblin, never shown itself) carries
    # the whole list as explicit stacks, which the tab shows in the order given: CREATIVE_ORDER.
    "creativeinventoryStacksByType": {"*-test-goblin": [{"tabs": ["general", "lotrnpcs"],
                                                         "stacks": [{"type": "item", "code": f"lotrnpcs:creature-{c}"} for c in CREATIVE_ORDER]}]},
    "materialDensity": 600,
    # The portrait mesh is a bust centred in the 1x1x1 item space. The angles are Fantasy Creatures' (whose humanoid
    # spawn items face the same way as ours), turned 25 degrees for a three-quarter view.
    # 0.0.38's bust filled about 60% of the slot at scale 1; 0.0.39 crops it to the head and shoulders and scales up.
    "guiTransform": {"rotation": {"x": 0, "y": -65, "z": -180}, "origin": {"x": 0.5, "y": 0.5, "z": 0.5}, "scale": 1.5},
    "fpHandTransform": {"rotation": {"x": 0, "y": -90, "z": 0}, "origin": {"x": 0.5, "y": 0.5, "z": 0.5}, "scale": 0.6},
    "groundTransform": {"rotation": {"x": 0, "y": -90, "z": 0}, "origin": {"x": 0.5, "y": 0, "z": 0.5}, "scale": 0.6},
})

# How the game names an NPC in death messages and the damage log, such as "killed by a Mordor orc captain".
# Without a "prefixandcreature-" entry it says "a wild animal".
PEOPLE = {"Goblin": "goblin", "Hobbit": "hobbit"}


def prefix_and_creature(name):
    who = PEOPLE.get(name, name)
    return ("an " if who[0] in "AEIOUaeiou" else "a ") + who


# Talk menu lines (user, 2026-11-02: hire, and "a brief random dialogue when they ask a question"). Original text.
# GuiDialogTalk picks a random numbered line of a kind for the NPC's faction, falling back to the general ones.
# In menu lines {0} is the price, {1} the coin's name, {2} the most recruits a player may lead, {3} the owner,
# {4} how many the player leads now, {5} the most that may follow at a time.
TALK = {
    "talk-opt-hire": "I'd like to hire you. ({0} × {1})",
    "talk-opt-hire-free": "I'd like to hire you.",
    "talk-opt-ask": "I have a question.",
    "talk-opt-follow": "Follow me.",
    "talk-opt-hold": "Hold your position here.",
    "talk-opt-guard": "Guard this area.",
    "talk-opt-guard-shared": "Guard this area for our people.",
    "talk-opt-return": "Go back on watch here.",
    "talk-opt-borrow": "Come with me.",
    "talk-opt-looks": "Let me change how you look.",
    "talk-opt-dismiss": "I don't need you any more.",
    "talk-opt-dismiss-yes": "Yes, you're free to go. (The coins aren't refunded.)",
    "talk-opt-dismiss-no": "On second thought, stay.",
    "talk-opt-bye": "Goodbye.",
    "talk-greet-1": "Well met.",
    "talk-stranger-1": "I don't know you. Move along.",
    "talk-stranger-2": "We don't take orders from strangers.",
    "talk-own-1": "Your orders?",
    "talk-shared-1": "I keep watch here for {3}'s people. Do you need me?",
    "talk-shared-2": "{3} said any of their people may call on me.",
    "talk-own-2": "I'm listening.",
    "talk-others-1": "I serve {3}. Take it up with them.",
    "talk-others-2": "{3} pays me, not you.",
    "talk-stay-1": "As you wish.",
    # The appearance dialog (0.0.54).
    "talk-looks-done-1": "Very well. I'll wear it proudly.",
    "talk-looks-done-2": "Do I look the part now?",
    "talk-looks-done-3": "If it pleases you, it pleases me.",
    "talk-looks-cancel-1": "Back as I was, then.",
    "talk-looks-cancel-2": "No changes? Fine by me.",
    "looks-title": "How {0} looks",
    "looks-name": "Name",
    "looks-rename": "Rename",
    "looks-helmet": "Helmet",
    "looks-toggle": "Switch",
    "looks-on": "On",
    "looks-off": "Off",
    "looks-weapon": "Weapon",
    "looks-weapon-none": "None",
    "looks-variant": "{0} ({1} of {2})",
    "looks-done": "Done",
    "looks-cancel": "Cancel",
    "looks-part-baseskin": "Skin",
    "looks-part-face": "Face",
    "looks-part-eyecolor": "Eyes",
    "looks-part-facialexpression": "Expression",
    "looks-part-ears": "Ears",
    "looks-part-hairbase": "Hair",
    "looks-part-hairextra": "More hair",
    "looks-part-haircolor": "Hair color",
    "looks-part-brows": "Brows",
    "looks-part-mustache": "Mustache",
    "looks-part-beard": "Beard",
    "looks-notyours": "Only the one who hired this NPC can change how it looks.",
    "looks-badname": "A name needs 1 to 24 letters, and may also use spaces, apostrophes and hyphens.",
    "looks-badpart": "That look isn't available for this NPC.",
    "looks-badweapon": "This NPC can't carry that weapon.",
    "looks-nohelmet": "This NPC has no helmet.",
    "looks-badhelmet": "This NPC can't wear that helmet.",
    "looks-helmet-none": "No helmet",
    "looks-shield": "Shield",
    "looks-badshield": "This NPC has no shield.",
    "looks-shieldtwohanded": "The poleaxe takes both hands, so the shield can't go with it.",
    "talk-stay-2": "Good. I'd rather stay.",
    "talk-dismiss-ask": "You're sending me away? The coin you paid stays with me.",
    "talk-hired": "Your coin is good. I'm yours to command.",
    "talk-hired-guard": "Your coin is good. Your band is full, so I'll keep watch here until you call for me.",
    "talk-followfull": "Your band is full. {6} Send someone off first.",
    "talk-full": "You lead as many recruits as anyone can keep in line. {7}",
    "talk-captains-full": "Your band already has as many captains as it can keep. Another would only quarrel with them.",
    "talk-poor": "That's not enough. I want {0} × {1}.",
    "talk-nocoin": "I'd want {0} × {1}, but nobody here has seen such a coin. (An admin can change HireCoin in lotrnpcs-factions.json.)",
    "talk-follow": "Lead the way.",
    "talk-hold": "I'll hold here.",
    "talk-guard": "I'll keep watch over this place.",
    "talk-guard-shared": "I'll keep watch here. Any of your people may call on me.",
    "talk-borrowed": "{3} trusts you, so I'll come. Lead the way.",
    "talk-dismissed": "Farewell, then.",
    # Hired NPCs go by rank, then name (user, 2026-11-03: call hired NPCs by their rank, then
    # their name). {3} here (and {1}/{2} below where noted) is "warrior Gorbag"; Shire hobbits
    # are farmers. Rankless NPCs go by their name alone.
    "title-recruit": "Recruit",
    "title-warrior": "Warrior",
    "title-captain": "Captain",
    "title-archer": "Archer",
    "title-shire-recruit": "Farmer",
    "recruit-yours": "Your {3}: {0} in {2} formation, {1} stance",
    "recruit-yours-guard": "Your {1}: guarding their post, {0} stance",
    "recruit-of": "{1}, in the service of {0}",
    "recruit-borrowed": "Following {0} for now",
    "recruit-shared": "Guarding for its owner's group",
    "recruit-yours-borrowed": "Your {1}: lent to {0}",
    "recruit-yours-guard-shared": "Your {1}: guarding their post for your group, {0} stance",
    "order-follow": "following",
    "order-hold": "holding position",
    "order-guard": "guarding",
    # What the owner sees when looking at their recruit. The game draws the orders key next to help-orders, as it
    # is bound (R unless changed).
    "help-talk": "Talk",
    "help-orders": "Hold for orders",
    "recruit-died": "Your {0} has died.",
    # Promotion (0.0.76). {0} the recruit's title and name before, {1} after; in the menu {0} the rank, {1} the price, {2}
    # the coin. talk-promote-* use the talk menu's placeholders ({0} price, {1} coin).
    "recruit-xp": "XP: ({0}/{1})",
    "promote-ready": "Your {0} has earned enough experience to be promoted. Talk to them to promote them.",
    "promote-done": "Your {0} is now {1}.",
    "talk-opt-promote": "Promote to {0} ({1} × {2})",
    "talk-opt-promote-free": "Promote to {0}",
    "talk-promote-poor": "A promotion costs {0} × {1}, and you don't have that much.",
    "talk-promote-nocoin": "A promotion would cost {0} × {1}, but nobody here has seen such a coin. (An admin can change HireCoin in lotrnpcs-factions.json.)",
    # The whitelist menu (0.0.76), opened by /recruits whitelist. {0} in whitelist-more is how many aren't shown.
    "whitelist-title": "Recruit whitelist",
    "whitelist-explain": "Your recruits never attack the players on this list on sight, even when guarding or on the raid stance. They still fight back if one of them hurts you or your recruits.",
    "whitelist-listed": "On your whitelist",
    "whitelist-none": "Nobody yet.",
    "whitelist-remove": "Remove",
    "whitelist-more": "And {0} more. Type /recruits whitelist list to see everyone.",
    "whitelist-add-label": "Add a player",
    "whitelist-add": "Add",
    "whitelist-online": "Or pick a player who is online now:",
    "whitelist-close": "Close",
    # Orders menu (0.0.38, hold R). {0} in orders-title is how many recruits hear the player. Since 0.0.58 the R
    # controls call hired NPCs "recruits" (user, 2026-11-03), and since 0.0.74 everything does (user, 2026-11-04: the
    # mod should not use the old word anywhere).
    "hotkey-orders": "LOTR NPCs: recruit orders (hold)",
    "orders-none": "No recruits of yours are near enough to hear you.",
    "orders-title": "Recruits near you: {0} ({1}, {2})",
    "orders-page-movement": "Movement",
    "orders-page-combat": "Combat",
    "orders-follow": "1. Follow me",
    "orders-hold": "2. Hold position",
    "orders-move": "3. Move there",
    "orders-guard": "4. Guard here",
    "orders-guard-shared": "5. Guard here for my group",
    "orders-formation": "Formation",
    "orders-hint": "Scroll or Tab: switch page. Let go of {0} or press Esc to close.",
    "orders-sharedkey": "The recruit orders key {0} is also bound to: {1}. Holding {0} opens the orders menu first, so that won't react to it any more; to keep both, give one of them another key in Settings > Controls.",
    "formation-loose": "Loose",
    "formation-line": "Line",
    "formation-column": "Column",
    "formation-wedge": "Wedge",
    "formation-square": "Square",
    "formation-circle": "Circle",
    "stance-passive": "Passive",
    "stance-defensive": "Defensive",
    "stance-aggressive": "Aggressive",
    "stance-raid": "Raid",
    "stance-desc-passive": "never fight, not even when struck",
    "stance-desc-defensive": "fight only what attacks you or them",
    "stance-desc-aggressive": "attack every enemy they see",
    "stance-desc-raid": "attack every player and creature that isn't allied",
    "cmd-none": "No recruits of yours are near enough to hear you.",
    "cmd-follow": "Recruits following you: {0}",
    "cmd-follow-full": "Recruits following you: {0}. {1} stayed behind because your band is full. {2}",
    "cmd-hold": "Recruits holding position: {0}",
    "cmd-move": "Recruits on the move: {0}",
    # Guard here (0.0.44). {1} is GuardRadius from lotrnpcs-factions.json.
    "cmd-guard": "Recruits guarding this spot: {0}. They patrol {1} blocks around it, and on the aggressive or raid stance they attack any player there who isn't a friend of yours (see /recruits whitelist).",
    "cmd-guard-shared": "Recruits guarding this spot for your group: {0}. They patrol {1} blocks around it, and anyone in your groups may talk to them to borrow one.",
    "cmd-notarget": "Look at the ground where they should go.",
    "cmd-toofar": "That's too far away to send them.",
    "cmd-formation": "Formation: {0}",
    "cmd-stance": "Stance: {0} (recruits: {1})",
    # /recruits whitelist (0.0.44): players the owner's recruits never attack on sight. {0} is the player's name.
    # Chat reads text as VTML, so no angle brackets.
    "whitelist-unknown": "Nobody called {0} has played on this server.",
    "whitelist-self": "Your recruits never attack you anyway.",
    "whitelist-already": "{0} is already on your recruits' whitelist.",
    "whitelist-added": "{0} is on your recruits' whitelist now. They won't attack {0} on sight, but they still fight back if {0} hurts you or them.",
    "whitelist-notlisted": "{0} isn't on your recruits' whitelist.",
    "whitelist-removed": "{0} is off your recruits' whitelist.",
    "whitelist-empty": "Your recruits' whitelist is empty. Add a player with /recruits whitelist add [name].",
    "whitelist-list": "Your recruits never attack these players on sight: {0}",
    # /recruits dismissall (0.0.73, user: "make an option in the chat to fire all your recruits"). {0} counts recruits.
    "dismissall-none": "You have no recruits to dismiss.",
    "dismissall-ask": "You have {0} recruits. To dismiss all of them, type /recruits dismissall confirm. Their hire fees aren't refunded.",
    "dismissall-done": "You dismissed all {0} of your recruits. They go back to their own people where they stand.",
    "dismissall-done-later": "You dismissed all {0} of your recruits. {1} of them are far away and will be free the next time someone comes near them.",
    # 0.0.90 follow shares (owner: "Chat would explain it, like room for 1 more orc or 0 more men").
    "room-line": "Room in your band: {0}.",
    # 0.0.91 (owner: players "should be notified how many recruits they have left in their capacity").
    "roster-line": "Room on your roster: {0}.",
    # 0.0.92 join menu (owner: players pick their faction after making a character; once; then admins change it).
    "join-title": "Choose your faction",
    "join-explain": "Pick the people you fight for. Their NPCs treat you as one of their own, you can hire their recruits, and their enemies become yours. You choose once; after that only an admin can change it.",
    "join-followers": "{0} followers",
    "join-follower": "{0} follower",
    "join-total": "{0} total",
    "join-legend": "Followers: how many recruits can walk with you at once. Total: how many recruits you can have in all.",
    "join-bloc-free": "Free Peoples",
    "join-bloc-shadow": "Shadow",
    "join-bloc-other": "Others",
    "join-stance-aggressive": "aggressive",
    "join-stance-defensive": "defensive",
    "join-stance-neutral": "neutral",
    "join-confirm": "Join the {0}? This is permanent: only an admin can change it.",
    "join-yes": "Join",
    "join-back": "Back",
    "join-later": "Decide later",
    "join-none": "There are no factions to join. Ask an admin.",
    "join-done": "You joined the {0}. Their NPCs now treat you as one of their own.",
    "join-reminder": "You haven't joined a faction yet, so every faction's NPCs treat you as a stranger and aggressive ones attack you. Type /recruits join to choose your faction.",
    "join-already": "You are a member of the {0}. Only an admin can change your faction.",
    "join-closed": "The {0} can't be joined from this menu. Ask an admin.",
    "join-unknown": "That faction doesn't exist any more. Open the menu again with /recruits join.",
    "hired-room": "You hired {0}. {1} {2}",
    "limits-dismissed": "Your recruits were over the new limits, so {0} left your service: {1}.",
    "limits-away": "all of them far away",
    "limits-dismissed-later": "{0} more will leave when you next come near them.",
    "limits-returned": "Your band was over the new limits, so {0} borrowed guards went back to keep watch.",
}
# Per faction: greetings for members, what they say when hired, and questions with their answers.
FACTION_TALK = {
    "gondor": (["Well met. The White City needs every sword it can get.", "Hail, friend of Gondor. What brings you here?"],
               "My sword is yours, for Gondor's sake.",
               [("How goes the watch on the eastern border?", "Badly. Fires burn in the mountains at night, and the patrols come back smaller every season."),
                ("What do you think of the horsemen of Rohan?", "Proud, and quick to anger. Still, I'd rather have them beside me than across from me."),
                ("Is the city safe?", "Safer than the fields. The walls have held for an age. Pray they hold another.")]),
    "rohan": (["Hail. The plains are wide, but we see who crosses them.", "You're no stranger to the Mark. Speak."],
              "I'll ride with you, as long as you ride against our foes.",
              [("Where is your horse?", "Grazing where the orcs can't find it. A rider without a horse is half a rider."),
               ("Have you seen orcs on the plains?", "Tracks, mostly. They move by night and burn what they can't carry."),
               ("What worries you most?", "The west. Too many farms near the hills have gone quiet this year.")]),
    "dale": (["Welcome to the lands of Dale. Mind the market, it's busier than it looks.", "A friend of Dale is welcome at our fires."],
             "Pay well and I'll fight well. That's how we do things in Dale.",
             [("How is trade with the dwarves?", "Good, when they're not haggling over every copper. Their toys sell well in the south."),
              ("Is the lake safe?", "Safe enough for fishermen. My grandfather's stories say otherwise, but he liked a tale."),
              ("What news from the east?", "Fewer traders every year, and the ones who come keep looking over their shoulders.")]),
    "arnor": (["Few walk these old roads. Fewer still are friends.", "The North remembers its own. Welcome."],
              "I'll walk your road. I've walked worse.",
              [("Where is the kingdom of Arnor?", "Under your feet. Ruins and barrows now, but we still keep the watch."),
               ("Why do you live in the wilds?", "Someone has to guard the quiet places, and the quiet folk in them. It's thankless work."),
               ("What haunts the old barrows?", "Things that should have stayed buried. Don't sleep near the standing stones.")]),
    "haradrim": (["The sun is hot and my patience is short. But you are one of us. Speak.", "Peace, cousin of the south."],
                 "Gold buys my spear, and your gold is good.",
                 [("What is the desert like?", "Endless. Water is worth more than gold there, and a friend is worth more than water."),
                  ("Have you seen the great war beasts?", "I marched beside one once. The ground shook for a mile around it."),
                  ("Why come so far north?", "Because we were told to. And because the north has rich land for the taking.")]),
    "dunlendings": (["You've the look of the hills. Sit, if you like.", "The hill folk don't forget a friend. Or an enemy."],
                    "I'll fight. The horse-lords owe us blood anyway.",
                    [("Why do you hate the horsemen?", "They took our grazing lands long ago, and they call us wild for wanting them back."),
                     ("Where do your people live?", "In the hills and valleys, in houses of turf and stone. We were here first."),
                     ("Who leads you?", "Whoever the clans follow this year. Lately a voice from a tower has been making promises.")]),
    "blacknumenoreans": (["You stand before the old blood of the West. Be brief.", "A servant of the true lords. Good. Speak."],
                         "My blade and my counsel, at a price. You have paid it.",
                         [("Where do your people come from?", "From the drowned island, long ago. We remember what the others chose to forget."),
                          ("Why do you serve the dark lord?", "Power is power. The kings of the West grew weak, so we chose the stronger master."),
                          ("What do you think of Gondor?", "Our cousins, grown soft and proud. One day they will kneel.")]),
    "easterlings": (["Kin of the east. The road was long, but we're here now.", "Welcome, brother. Share the fire."],
                    "I'll follow you. The east will remember your name.",
                    [("Why did you come west?", "For land, for plunder, for the lord who calls us. Mostly for land."),
                     ("What is your homeland like?", "Wide grass and cold winds. Horse clans and wagon towns. Nothing like these crowded hills."),
                     ("Do you trust the orcs?", "Nobody trusts orcs. We march beside them because we're told to.")]),
    "dwarves": (["Hail, friend of the mountain halls. Mind your step, the floor's new.", "A friend of the dwarves! Come, have a drink."],
                "Right then. My axe goes where you go, so long as the pay holds.",
                [("What do you dig for?", "Iron, gold, gems. And the old halls we lost when the deep things woke."),
                 ("Do you get along with elves?", "We trade. We don't share a table. Old grudges run as deep as mines."),
                 ("What's the best ale?", "Whatever's in my mug. Second best is whatever's in yours.")]),
    "wickeddwarves": (["You've a strong arm and a weak conscience. Good. Speak.", "Hah! Another one who knows where the real gold is."],
                      "Coin first, questions never. Lead on.",
                      [("Why did you leave your kin?", "They hoard for the sake of hoarding. We take what we need, from whoever has it."),
                       ("Who do you serve?", "Whoever pays in gold, not promises."),
                       ("Do you miss the great halls?", "Every day. That's why we'll take them back, from whoever sits in them.")]),
    "shire": (["Oh! Good day to you. Mind the garden.", "Well met, neighbour. Lovely weather for it."],
              "Well, I suppose a bit of adventure won't kill me. Probably.",
              [("Where's the best inn around here?", "Depends what you're after: good beer, good gossip or a quiet corner. Every village claims the first."),
               ("Have you seen anything strange lately?", "More big folk on the roads than usual. And riders in dark cloaks asking questions, so they say."),
               ("What do hobbits do all day?", "Garden, eat, visit, eat again. It's a very busy life, thank you.")]),
    "elves": (["Well met, friend. The stars are bright tonight.", "You walk among the elves. Be welcome."],
              "I will go with you a while. Our roads lie together, for now.",
              [("How old are you?", "Older than the oaks in this wood, and younger than the hills. Why do mortals always ask that?"),
               ("Will you sail west one day?", "One day. Not yet. There is still beauty here worth guarding."),
               ("What do your songs tell of?", "Of the light that was, and the friends we have lost. Our songs are long, and mostly sad.")]),
    "tormentedelves": (["You... you see clearly. Not like the others. Speak.", "The dark has a voice. You hear it too, don't you?"],
                       "Yes. I'll follow. Better to serve you than the voice in the dark.",
                       [("What happened to you?", "Taken, long ago. Broken, then mended wrong. I don't remember the light any more."),
                        ("Do you remember your kin?", "Faces in water. When I reach for them, they're gone."),
                        ("Why do you fight?", "Because the pain stops while I fight. Only then.")]),
    "mordor": (["What do you want? Make it quick, the boss is watching.", "Another one sent by the Eye. Fine. Talk."],
               "Fine. Point me at something soft and I'll stick it.",
               [("Who do you serve?", "The Eye. Everyone serves the Eye sooner or later. Better to do it willingly."),
                ("What do you eat?", "Whatever isn't fast enough. Don't ask what's in the stew."),
                ("Do you like the Isengard lot?", "The white-hand boys? Too proud by half. They think they're better than us because the sun doesn't bother them.")]),
    "angmar": (["Cold up here, eh? You get used to it. Or you die.", "The iron north has room for you. Speak."],
               "I'll come. Better than freezing in these ruins.",
               [("Who rules here?", "A king in iron, cold as the hills. Don't say his name too loud."),
                ("What happened to the old kingdoms here?", "We happened. Now they're barrows and broken towers, and the dead don't rest."),
                ("Why is it always so cold?", "The wind comes off the northern ice. And something else. It keeps the men away.")]),
    "mistymountains": (["Hss... a friend, is it? Don't get lost in the tunnels.", "You're not dinner, then. Pity. What do you want?"],
                       "Gold and meat, and I'll come. More meat than gold.",
                       [("Where do you live?", "Under the mountains. Miles of tunnels, all dark. We like it dark."),
                        ("Who is your king?", "The big one in the great cave. He's fat, but he's mean."),
                        ("Do you go out in daylight?", "Not if we can help it. The sun burns. Night's for hunting.")]),
    "gundabad": (["You walk the north road and live. Speak, before that changes.", "Gundabad remembers its friends. Not many of them."],
                 "Bones and blood, and I'm yours. Point me at something.",
                 [("Why the bone armor?", "Iron rusts in the snow. Bone doesn't, and it shows what we've done to the ones who wore it."),
                  ("What is Gundabad?", "The mountain in the far north. The dwarves call it theirs. They're wrong."),
                  ("Do you fear the cold?", "The cold fears us. We were born in it.")]),
    "isengard": (["State your business. Isengard doesn't wait.", "Uruk-hai of the White Hand. What do you want?"],
                 "Good. I'll march day or night. Uruks don't tire.",
                 [("Who made you?", "The wizard, in the pits under his tower. We're better than the old orcs. Bigger, stronger."),
                  ("What does the white hand mean?", "That we belong to him. Wear it proud."),
                  ("Why cut down the forest?", "Fuel for the forges. Trees don't fight back. Mostly.")]),
}
# Strangers the Shire won't serve, but won't fight either (the Shire is the only defensive faction by default).
FACTION_STRANGER = {
    "shire": ["I don't know you, and I'll thank you to keep off my lawn.", "Big folk. Hmph. Mind where you put your boots.",
              "Good day. I've nothing to say to strangers, I'm afraid."],
}
for faction, (greets, hired, questions) in FACTION_TALK.items():
    for i, line in enumerate(greets, 1):
        TALK[f"talk-greet-{faction}-{i}"] = line
    TALK[f"talk-hired-{faction}"] = hired
    for i, (question, answer) in enumerate(questions, 1):
        TALK[f"talk-ask-{faction}-{i}"] = question
        TALK[f"talk-answer-{faction}-{i}"] = answer
for faction, lines in FACTION_STRANGER.items():
    for i, line in enumerate(lines, 1):
        TALK[f"talk-stranger-{faction}-{i}"] = line
assert set(FACTION_TALK) == set(FACTION_OF.values()), "talk lines for every faction"
# 0.0.90: each faction's people for the room line ("Room in your band: Mordor orcs 1, Men of Gondor 0.").
FOLK = {
    "gondor": "Men of Gondor", "rohan": "Riders of Rohan", "dale": "Men of Dale", "arnor": "Dunedain of Arnor",
    "haradrim": "Haradrim", "easterlings": "Easterlings", "blacknumenoreans": "Black Numenoreans", "dunlendings": "Dunlendings",
    "isengard": "Uruk-hai", "mordor": "Mordor orcs", "angmar": "Angmar orcs", "gundabad": "Gundabad orcs",
    "mistymountains": "Goblins of the Misty Mountains", "elves": "Elves", "tormentedelves": "Tormented elves",
    "dwarves": "Dwarves", "wickeddwarves": "Wicked dwarves", "shire": "Hobbits",
}
assert set(FOLK) == set(FACTION_OF.values()), "a folk name for every faction"
for faction, folk in FOLK.items():
    TALK[f"folk-{faction}"] = folk

# The creative tab's name is looked up in the game domain (user, 2026-11-03: the tab read "tabname-lotrnpcs").
lang = {"game:tabname-lotrnpcs": "LOTR NPCs", "tabname-lotrnpcs": "LOTR NPCs"}
for code, name, *_ in TEST_NPCS:
    lang[f"item-creature-{code}"] = name
    lang[f"entity-{code}"] = name
    lang[f"prefixandcreature-{code}"] = prefix_and_creature(name)
for weapon, w in {**{k: w for k, w in ALL_WEAPONS.items() if "item" not in w}, **SHIELDS, "bow": BOW, "bow-drawn": BOW_DRAWN, **BOW_PULL_ITEMS, "mmbow": MM_BOW, "mmbow-drawn": MM_BOW_DRAWN, **MM_BOW_PULL_ITEMS}.items():
    lang[f"item-orcweapon-{weapon}"] = w["name"]
lang["entity-npcarrow"] = "Arrow"
lang.update(TALK)
write("lang/en.json", lang)

with open(os.path.join(out, "modinfo.json"), "w") as f:
    json.dump({
        "type": "code",
        "modid": "lotrnpcs",
        "name": "LOTR NPCs",
        "version": "0.0.109",
        "description": "Recruits, warriors and captains of every Middle-earth faction. They spawn in admin zones (/recruits zone), "
                       "fight by faction, and can be hired and commanded (talk to them; hold R for orders). Race models from "
                       "LOTR Races and Classes by Dango Dango Dango, Shakgriig and Buggoi, used with their permission.",
        "authors": ["Emperorcharizar"],
        "contributors": ["Dango Dango Dango", "Shakgriig", "Buggoi"],
        "dependencies": {"playermodellib": "1.23.8"},
        "side": "Universal",
        "requiredOnClient": True,
        "requiredOnServer": True,
    }, f, indent=2)
    f.write("\n")
