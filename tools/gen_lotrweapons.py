"""Generates the LOTR Weapons mod (lotrweapons) into the folder given as argv[1]: the owner's custom melee weapons, the
ones lotrnpcs' NPCs carry, as player items in iron and steel (owner, 2026-11-14). Same models, textures and hand
transforms as the NPCs (tools/weapon_catalog.py), the same swing and hold animations.

Stats copy the nearest game weapon in the same metal (vanilla 1.22.7 itemtypes; KINDS below names each source). The
mod is vanilla-only so far; the Combat Overhaul patch comes next (docs/notes/lotrweapons.md).
"""
import json
import math
import os
import shutil
import sys

VERSION = "0.0.1"
DOMAIN = "lotrweapons"
FOLDER = "weapon"  # assets/lotrweapons/shapes/item/weapon, textures/item/weapon
METALS = ["iron", "steel"]
TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(TOOLS)

out = sys.argv[1]
A = os.path.join(out, "assets", DOMAIN)


def _load_tool(name):
    import importlib.util
    spec = importlib.util.spec_from_file_location(name, os.path.join(TOOLS, name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


C = _load_tool("weapon_catalog")


def write(rel, data):
    path = os.path.join(A, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        json.dump(data, f, indent=2)
        f.write("\n")


# What each kind of weapon copies, read from the game's 1.22.7 itemtypes (survival/itemtypes/tool/*.json), per metal:
# attack power, tool tier, durability. Vanilla has no iron or steel mace, war hammer, battle axe, poleaxe, halberd or
# great sword (only rusty "ruined" ones, 4.5 damage whatever their type), so those ("heavy") copy the iron and steel
# falx, the nearest weapon the game makes in those metals.
FALX = {"copies": "blade-falx", "class": "Item", "tool": "sword", "damagedby": ["blockbreaking", "attacking"],
        "attackRange": 2.5, "power": {"iron": 5, "steel": 5.25}, "tier": {"iron": 4, "steel": 5},
        "durability": {"iron": 850, "steel": 2125},
        "behaviors": [{"name": "AnimationAuthoritative"}, {"name": "Buffable"}, {"name": "Sharpenable"}]}
KINDS = {
    "sword": FALX,
    "heavy": dict(FALX, tool=None, behaviors=[{"name": "AnimationAuthoritative"}, {"name": "Buffable"}]),
    "knife": {"copies": "knife-generic", "class": "ItemKnife", "tool": "knife", "damagedby": ["blockbreaking", "attacking"],
              "power": {"iron": 3.5, "steel": 4}, "durability": {"iron": 900, "steel": 2250},
              "behaviors": [{"name": "AnimationAuthoritative"}]},
    "axe": {"copies": "axe-felling", "class": "ItemAxe", "tool": "axe", "damagedby": ["blockbreaking", "attacking"],
            "attackRange": 2, "power": {"iron": 3, "steel": 4}, "tier": {"iron": 4, "steel": 5},
            "durability": {"iron": 900, "steel": 1800},
            "miningspeed": {"iron": {"wood": 8.4, "plant": 5, "leaves": 3.2}, "steel": {"wood": 11, "plant": 6, "leaves": 4}},
            "behaviors": [{"name": "AnimationAuthoritative"}]},
    "spear": {"copies": "spear-generic", "class": "ItemSpear", "tool": "spear", "damagedby": ["attacking"],
              "attackRange": 3.5, "power": {"iron": 4.2, "steel": 4.4}, "durability": {"iron": 400, "steel": 700},
              "thrown": {"iron": 6.8, "steel": 7.0}, "thrownTier": {"iron": 1, "steel": 2},
              "behaviors": [{"name": "Buffable"}, {"name": "AnimationAuthoritative"}]},
}
# Catalog key -> (item code, kind, name). Codes are <weapon>-<people>, plus the metal: lotrweapons:sword-gondor-steel.
# Gondor's sword and the dwarven war and short hammers are one weapon each with the owner's iron and steel models
# (STEEL_MODEL); every other weapon uses its one model for both metals until the owner's steel skins arrive.
WEAPONS = {
    "scimitar": ("scimitar-mordor", "sword", "Orc scimitar"),
    "axe": ("axe-mordor", "axe", "Orc axe"),
    "hammer": ("hammer-mordor", "heavy", "Orc hammer"),
    "dagger": ("dagger-mordor", "knife", "Orc dagger"),
    "knife": ("knife-mordor", "knife", "Orc knife"),
    "poleaxe": ("poleaxe-mordor", "heavy", "Orc poleaxe"),
    "mmsword": ("sword-mistymountains", "sword", "Misty Mountains orc sword"),
    "mmspear": ("spear-mistymountains", "spear", "Misty Mountains orc spear"),
    "elfspear": ("spear-elven", "spear", "Elven spear"),
    "elflongsword": ("longsword-elven", "heavy", "Elven longsword"),
    "dwaxe": ("axe-dwarven", "axe", "Dwarven axe"),
    "dwbattleaxe": ("battleaxe-dwarven", "heavy", "Dwarven battleaxe"),
    "dwdagger": ("dagger-dwarven", "knife", "Dwarven dagger"),
    "dwspear": ("boarspear-dwarven", "spear", "Dwarven boar spear"),
    "dwwarhammer": ("warhammer-dwarven", "heavy", "Dwarven warhammer"),
    "dwshorthammer": ("shorthammer-dwarven", "heavy", "Dwarven shorthammer"),
    "gdsword": ("sword-gondor", "sword", "Gondorian sword"),
    "gdcaptsword": ("captainsword-gondor", "heavy", "Captain's sword"),
    "rhsword": ("sword-rohan", "sword", "Rohirric sword"),
    "rhaxe": ("axe-rohan", "axe", "Rohirric axe"),
    "rhspear": ("spear-rohan", "spear", "Rohirric spear"),
    "bnsteelsword": ("sword-blacknumenorean", "sword", "Black Numenorean sword"),
    "bnspear": ("spear-numenorean", "spear", "Numenorean spear"),
    "morgulblade": ("morgulblade", "sword", "Morgul blade"),
    "eastaxe": ("halberd-easterling", "heavy", "Easterling halberd"),
    "eastscimitar": ("scimitar-easterling", "sword", "Easterling scimitar"),
    "hdspear": ("spear-haradrim", "spear", "Haradrim spear"),
    "hdsword": ("sword-haradrim", "sword", "Haradrim sword"),
    "hdcurvedsword": ("curvedsword-haradrim", "sword", "Haradrim curved sword"),
}
STEEL_MODEL = {"gdsword": "gdsteelsword", "dwwarhammer": "dwwarhammersteel", "dwshorthammer": "dwshorthammersteel"}
assert set(WEAPONS) | set(STEEL_MODEL.values()) == set(C.MELEE), sorted(set(C.MELEE) ^ (set(WEAPONS) | set(STEEL_MODEL.values())))


# The three animations of our own (tools/anims) go onto every body a player can have: the game's seraph and LOTR Races
# and Classes' seraph, dwarf and elf bodies (its customplayermodels use those three). Their settings copy the vanilla
# animation each was made from (player.json): the chop is the axe's hit with the blow at our frame 14, the holds are
# holdbothhands. The "-fp" entries are what first person plays; they use the same animation.
OWN_ANIMS = ["poleaxechop", "poleaxehold", "greatswordhold"]
PLAYER_BODIES = [("game:shapes/entity/humanoid/seraph-faceless.json", None)] + [
    (f"lrc:shapes/entity/humanoid/{b}-faceless.json", "lrc") for b in ("seraph", "dwarf", "elf")]
ARMS = ["UpperArmR", "LowerArmR", "UpperArmL", "LowerArmL"]
ANIM_META = {
    "poleaxechop": {"blendMode": "Add", "animationSpeed": 1, "holdEyePosAfterEasein": 0.3,
                    "attributes": {"soundAtFrame": 10, "damageAtFrame": 14, "authorative": True},
                    "elementWeight": {e: 20 for e in ARMS}, "elementBlendMode": {e: "AddAverage" for e in ARMS}},
    "poleaxehold": {"blendMode": "Add", "elementWeight": {e: 100 for e in ["UpperArmR", "LowerArmR", "ItemAnchor"]},
                    "elementBlendMode": {e: "AddAverage" for e in ["UpperArmR", "LowerArmR", "ItemAnchor"]}},
    "greatswordhold": {"blendMode": "Add", "elementWeight": {e: 100 for e in ARMS},
                       "elementBlendMode": {e: "AddAverage" for e in ARMS}},
}
assert set(ANIM_META) == set(OWN_ANIMS)
VANILLA_ANIMS = {"falx", "axehit", "hammerhit", "knifestab", "spearhit", "spearidle"}  # all in player.json


def write_animation_patches():
    patches = []
    for name in OWN_ANIMS:
        anim = json.load(open(os.path.join(TOOLS, "anims", name + ".json"), encoding="utf-8"))
        for file, mod in PLAYER_BODIES:
            patches.append({"op": "add", "path": "/animations/-", "value": anim, "file": file}
                           | ({"dependsOn": [{"modid": mod}]} if mod else {}))
        for code in (name, name + "-fp"):
            patches.append({"op": "add", "path": "/client/animations/-", "file": "game:entities/humanoid/player.json",
                            "value": {"code": code, "animation": name} | ANIM_META[name]})
    write("patches/player-animations.json", patches)


# Rotation helpers for the icon and ground transforms (the tpHandTransform is the NPC's, from the catalog).
D = math.pi / 180


def rot(angles):
    """Mat4f.RotateByXYZ as a 3x3 matrix: Rx * Ry * Rz (angles in degrees)."""
    x, y, z = (angles.get(a, 0) * D for a in "xyz")
    rx = [[1, 0, 0], [0, math.cos(x), -math.sin(x)], [0, math.sin(x), math.cos(x)]]
    ry = [[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]]
    rz = [[math.cos(z), -math.sin(z), 0], [math.sin(z), math.cos(z), 0], [0, 0, 1]]
    return mul(mul(rx, ry), rz)


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def transpose(a):
    return [list(r) for r in zip(*a)]


def euler_xyz(m):
    """Angles (degrees) with rot(angles) == m."""
    y = math.asin(max(-1, min(1, m[0][2])))
    if abs(m[0][2]) < 0.9999:
        x = math.atan2(-m[1][2], m[2][2])
        z = math.atan2(-m[0][1], m[0][0])
    else:
        x, z = math.atan2(m[2][1], m[1][1]), 0
    return {"x": round(x / D, 1), "y": round(y / D, 1), "z": round(z / D, 1)}


# The vanilla falx, held in the hand with rotation (85, 0, 2) plus the hand's own -180 about Y. Our weapons are turned
# in the hand so they sit as it does (catalog), so the turn that takes one of ours to the falx's own lie is
# falx_hand^-1 * our_hand; the falx's icon and ground rotations are then applied on top. Sizes keep the falx's
# on-screen length (its model is 1.33 blocks long; icon scale 1.65, ground 3.4 at its in-hand scale 0.8).
FALX_HAND = {"x": 85, "y": -180, "z": 2}
FALX_GUI = {"x": -53, "y": 118, "z": -5}
FALX_LENGTH, FALX_GUI_SCALE, FALX_GROUND_SCALE, FALX_HAND_SCALE = 1.33, 1.65, 3.4, 0.8


def bounds(shape):
    """Min and max corner, in blocks, of the shape's visible boxes (with a texture on some face), each turned by its
    own and its parents' rotations as the game does (ShapeElement.GetLocalTransformMatrix)."""
    def matmul(a, b):
        return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]

    def move(x, y, z):
        return [[1, 0, 0, x], [0, 1, 0, y], [0, 0, 1, z], [0, 0, 0, 1]]

    def turn(e):
        r = rot({"x": e.get("rotationX") or 0, "y": e.get("rotationY") or 0, "z": e.get("rotationZ") or 0})
        return [r[0] + [0], r[1] + [0], r[2] + [0], [0, 0, 0, 1]]
    lo, hi = [1e9] * 3, [-1e9] * 3

    def walk(elements, parent):
        for e in elements:
            o = [v / 16 for v in (e.get("rotationOrigin") or [0, 0, 0])]
            f, t = [v / 16 for v in e["from"]], [v / 16 for v in e["to"]]
            m = matmul(matmul(matmul(parent, move(*o)), turn(e)), move(*(f[i] - o[i] for i in range(3))))
            faces = e.get("faces") or {}
            if any(isinstance(fc, dict) and fc.get("texture") and fc.get("enabled", True) is not False for fc in faces.values()):
                size = [t[i] - f[i] for i in range(3)]
                for c in range(8):
                    v = [size[0] if c & 4 else 0, size[1] if c & 2 else 0, size[2] if c & 1 else 0, 1]
                    p = [sum(m[i][k] * v[k] for k in range(4)) for i in range(3)]
                    for i in range(3):
                        lo[i], hi[i] = min(lo[i], p[i]), max(hi[i], p[i])
            walk(e.get("children") or [], m)
    walk(shape["elements"], move(0, 0, 0))
    return lo, hi


def display_transforms(w, shape):
    lo, hi = bounds(shape)
    centre = {a: round((lo[i] + hi[i]) / 2, 3) for i, a in enumerate("xyz")}
    # The icon and the ground show the model unscaled, so each weapon gets the falx's on-screen length.
    size = FALX_LENGTH / max(max(hi[i] - lo[i] for i in range(3)), 0.1)
    ours = dict(w["rotation"], y=w["rotation"].get("y", 0) - 180)
    align = mul(transpose(rot(FALX_HAND)), rot(ours))
    gui = {"translation": {"x": 0, "y": 0, "z": 0}, "rotation": euler_xyz(mul(rot(FALX_GUI), align)),
           "origin": centre, "scale": round(FALX_GUI_SCALE * size, 3)}
    ground = {"translation": {"x": 0, "y": 0, "z": 0}, "rotation": euler_xyz(align), "origin": centre,
              "scale": round(FALX_GROUND_SCALE * size, 3)}
    return gui, ground, centre


def write_items():
    own = C.own_weapon_textures()
    bundled = set()
    shapes = {}
    for key in list(WEAPONS) + list(STEEL_MODEL.values()):
        shapes[key] = C.weapon_shape(key, C.MELEE[key], own, bundled, DOMAIN, A, FOLDER)
        write(f"shapes/item/{FOLDER}/{key}.json", shapes[key])
    C.copy_own_textures(bundled, own, A, FOLDER)
    lang = {"tabname-lotrweapons": "LOTR Weapons"}
    for key, (code, kind, name) in WEAPONS.items():
        w, k = C.MELEE[key], KINDS[kind]
        gui, ground, centre = display_transforms(w, shapes[key])
        shape = {"base": f"{DOMAIN}:item/{FOLDER}/{key}"}
        item = {
            "code": code, "class": k["class"], "tags": ["weapon", "weapon-melee"], "damagedby": k["damagedby"],
            "variantgroups": [{"code": "metal", "states": METALS}],
            "shapeByType": {f"*-steel": {"base": f"{DOMAIN}:item/{FOLDER}/{STEEL_MODEL[key]}"}, "*": shape} if key in STEEL_MODEL else None,
            "shape": None if key in STEEL_MODEL else shape,
            "maxstacksize": 1,
            "attributes": {"heldItemPitchFollow": 0.9, "handbook": {"groupBy": [code + "-*"]},
                           "lotrweapons": {"catalog": key, "copies": k["copies"]}},
            "heldTpHitAnimation": w["attack"],
            "attackpowerByType": {f"*-{m}": k["power"][m] for m in METALS},
            "durabilityByType": {f"*-{m}": k["durability"][m] for m in METALS},
            "creativeinventory": {"general": ["*"], "items": ["*"], "tools": ["*"], "lotrweapons": ["*"]},
            "behaviors": k["behaviors"],
            "tpHandTransform": C.hand_transform(w["grip"], w["rotation"], w.get("scale", C.WEAPON_SCALE)),
            "guiTransform": gui,
            "groundTransform": ground,
        }
        if k.get("tool"):
            item["tool"] = k["tool"]
        if k.get("tier"):
            item["tooltierByType"] = {f"*-{m}": k["tier"][m] for m in METALS}
        if k.get("attackRange"):
            item["attackRange"] = k["attackRange"]
        if k.get("miningspeed"):
            item["miningspeedByType"] = {f"*-{m}": k["miningspeed"][m] for m in METALS}
        if w.get("hold"):
            item["heldTpIdleAnimation"] = w["hold"]
            item["heldRightReadyAnimation"] = w["hold"]
        if kind == "spear":
            item["attributes"] |= {"damageByType": {f"*-{m}": k["thrown"][m] for m in METALS},
                                   "damageTierByType": {f"*-{m}": k["thrownTier"][m] for m in METALS},
                                   "spearEntityCodeByType": {"*": f"{DOMAIN}:thrown-{code}-{{metal}}"}}
            write_thrown_spear(code, key, w, centre)
        write(f"itemtypes/{code}.json", {k2: v for k2, v in item.items() if v is not None})
        for m in METALS:
            lang[f"item-{code}-{m}"] = f"{m.capitalize()} {name[0].lower() + name[1:] if name.startswith(('Orc', 'Captain')) else name}"
    write("lang/en.json", lang)
    return lang


def write_thrown_spear(code, key, w, centre):
    """A thrown spear, as the game's (survival/entities/nonliving/projectile/spear.json): the same shape, flying tip
    first along +X. The game draws its thrown spear at full size, 1.25 times its in-hand size; ours keeps that ratio."""
    physics = {"code": "passivephysics", "groundDragFactor": 1, "airDragFactor": 0.25, "gravityFactor": 0.75}
    turn = w["rotation"]
    write(f"entities/thrown-{code}.json", {
        "code": f"thrown-{code}", "class": "EntityProjectile", "tags": ["inanimate", "projectile"],
        "variantgroups": [{"code": "metal", "states": METALS}],
        "hitboxSize": {"x": 0.125, "y": 0.125}, "attributes": {"isProjectile": True},
        "client": {"renderer": "Shape", "size": round(w.get("scale", C.WEAPON_SCALE) / FALX_HAND_SCALE, 3),
                   "shape": {"base": f"{DOMAIN}:item/{FOLDER}/{key}", "offsetX": -centre["x"]}
                   | ({"rotateY": turn["y"]} if turn.get("y") else {}),
                   "behaviors": [physics, {"code": "interpolateposition"}]},
        "server": {"behaviors": [physics, {"code": "despawn", "minSeconds": 600}, {"code": "entitystatetags"}]},
        "sounds": {},
    })


def main():
    shutil.rmtree(out, ignore_errors=True)
    write_items()
    write_animation_patches()
    with open(os.path.join(out, "modinfo.json"), "w") as f:
        json.dump({
            "type": "content", "modid": DOMAIN, "name": "LOTR Weapons", "version": VERSION,
            "description": "Middle-earth weapons for players, in iron and steel: the swords, axes, hammers, spears and "
                           "polearms of Mordor, the Misty Mountains, Gondor, Rohan, the dwarves, the elves, Harad, the "
                           "Easterlings and the Black Numenoreans.",
            "authors": ["Emperorcharizar"],
            "side": "Universal", "requiredOnClient": True, "requiredOnServer": True,
        }, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()
