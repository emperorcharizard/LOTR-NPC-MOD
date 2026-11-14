"""Generates the LOTR Weapons mod (lotrweapons) into the folder given as argv[1]: the owner's custom melee weapons, the
ones lotrnpcs' NPCs carry, as player items in iron and steel (owner, 2026-11-14). Same models, textures and hand
transforms as the NPCs (tools/weapon_catalog.py), the same swing and hold animations.

Stats copy the nearest game weapon in the same metal, 10% stronger (KINDS below names each source). With Combat
Overhaul installed, a patch turns them into its melee weapons (CO_KINDS). Notes: docs/notes/lotrweapons.md.
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


# What each kind of weapon copies, per metal: attack power, tool tier, durability, reach. Swords, knives and spears
# copy the game's own (1.22.7 survival/itemtypes/tool/*.json). The game makes no iron or steel war axe, mace, war
# hammer, halberd or great sword (only rusty "ruined" ones), so those copy Not Only Spear Expansion 0.4.1 (Kanahaku;
# the owner sent it for comparison, 2026-11-14): its bartka (a battle axe), mace, polehammer, halberd and claymore.
# Owner, 2026-11-14: "increase the damage to be slightly better than vanilla": every attack power (and the thrown
# spear's damage) is the copied number times BETTER, here and in the Combat Overhaul patch.
BETTER = 1.1
_HIT = [{"name": "AnimationAuthoritative"}, {"name": "Buffable"}]
_KIT = {"damagedby": ["blockbreaking", "attacking"], "behaviors": _HIT}
KINDS = {
    "sword": _KIT | {"copies": "game:blade-falx", "class": "Item", "tool": "sword", "attackRange": 2.5,
                     "power": {"iron": 5, "steel": 5.25}, "tier": {"iron": 4, "steel": 5}, "durability": {"iron": 850, "steel": 2125},
                     "behaviors": _HIT + [{"name": "Sharpenable"}]},
    "knife": _KIT | {"copies": "game:knife-generic", "class": "ItemKnife", "tool": "knife",
                     "power": {"iron": 3.5, "steel": 4}, "durability": {"iron": 900, "steel": 2250}},
    "spear": {"copies": "game:spear-generic", "class": "ItemSpear", "tool": "spear", "damagedby": ["attacking"],
              "attackRange": 3.5, "power": {"iron": 4.2, "steel": 4.4}, "durability": {"iron": 400, "steel": 700},
              "thrown": {"iron": 6.8, "steel": 7.0}, "thrownTier": {"iron": 1, "steel": 2}, "behaviors": _HIT},
    # A war axe still fells trees: the game's felling axe's speeds.
    "axe": _KIT | {"copies": "spearexpantion:bartka", "class": "ItemAxe", "tool": "axe", "attackRange": 2.5,
                   "power": {"iron": 5, "steel": 6}, "tier": {"iron": 4, "steel": 5}, "durability": {"iron": 850, "steel": 2150},
                   "miningspeed": {"iron": {"wood": 8.4, "plant": 5, "leaves": 3.2}, "steel": {"wood": 11, "plant": 6, "leaves": 4}}},
    "battleaxe": _KIT | {"copies": "spearexpantion:bartka", "class": "Item", "attackRange": 2.5,
                         "power": {"iron": 5, "steel": 6}, "tier": {"iron": 4, "steel": 5}, "durability": {"iron": 850, "steel": 2150}},
    "mace": _KIT | {"copies": "spearexpantion:mace-common", "class": "Item", "attackRange": 2.5,
                    "power": {"iron": 5.5, "steel": 6}, "tier": {"iron": 4, "steel": 5}, "durability": {"iron": 1000, "steel": 2350}},
    "polehammer": _KIT | {"copies": "spearexpantion:polehammer", "class": "Item", "attackRange": 3.5,
                          "power": {"iron": 5, "steel": 6}, "tier": {"iron": 4, "steel": 6}, "durability": {"iron": 1100, "steel": 2000}},
    "halberd": _KIT | {"copies": "spearexpantion:halberd", "class": "Item", "attackRange": 3.5,
                       "power": {"iron": 5, "steel": 6}, "tier": {"iron": 4, "steel": 5}, "durability": {"iron": 1100, "steel": 2000}},
    "claymore": _KIT | {"copies": "spearexpantion:claymore", "class": "Item", "attackRange": 3,
                        "power": {"iron": 5.5, "steel": 6.2}, "tier": {"iron": 4, "steel": 5}, "durability": {"iron": 1200, "steel": 2400}},
}


def better(value):
    return round(value * BETTER, 2)


# Catalog key -> (item code, kind, name). Codes are <weapon>-<people>, plus the metal: lotrweapons:sword-gondor-steel.
# Gondor's sword and the dwarven war and short hammers are one weapon each with the owner's iron and steel models
# (STEEL_MODEL); every other weapon uses its one model for both metals until the owner's steel skins arrive.
WEAPONS = {
    "scimitar": ("scimitar-mordor", "sword", "Orc scimitar"),
    "axe": ("axe-mordor", "axe", "Orc axe"),
    "hammer": ("hammer-mordor", "mace", "Orc hammer"),
    "dagger": ("dagger-mordor", "knife", "Orc dagger"),
    "knife": ("knife-mordor", "knife", "Orc knife"),
    "poleaxe": ("poleaxe-mordor", "halberd", "Orc poleaxe"),
    "mmsword": ("sword-mistymountains", "sword", "Misty Mountains orc sword"),
    "mmspear": ("spear-mistymountains", "spear", "Misty Mountains orc spear"),
    "elfspear": ("spear-elven", "spear", "Elven spear"),
    "elflongsword": ("longsword-elven", "claymore", "Elven longsword"),
    "dwaxe": ("axe-dwarven", "axe", "Dwarven axe"),
    "dwbattleaxe": ("battleaxe-dwarven", "battleaxe", "Dwarven battleaxe"),
    "dwdagger": ("dagger-dwarven", "knife", "Dwarven dagger"),
    "dwspear": ("boarspear-dwarven", "spear", "Dwarven boar spear"),
    "dwwarhammer": ("warhammer-dwarven", "polehammer", "Dwarven warhammer"),
    "dwshorthammer": ("shorthammer-dwarven", "mace", "Dwarven shorthammer"),
    "gdsword": ("sword-gondor", "sword", "Gondorian sword"),
    "gdcaptsword": ("captainsword-gondor", "claymore", "Captain's sword"),
    "rhsword": ("sword-rohan", "sword", "Rohirric sword"),
    "rhaxe": ("axe-rohan", "axe", "Rohirric axe"),
    "rhspear": ("spear-rohan", "spear", "Rohirric spear"),
    "bnsteelsword": ("sword-blacknumenorean", "sword", "Black Numenorean sword"),
    "bnspear": ("spear-numenorean", "spear", "Numenorean spear"),
    "morgulblade": ("morgulblade", "sword", "Morgul blade"),
    "eastaxe": ("halberd-easterling", "halberd", "Easterling halberd"),
    "eastscimitar": ("scimitar-easterling", "sword", "Easterling scimitar"),
    "hdspear": ("spear-haradrim", "spear", "Haradrim spear"),
    "hdsword": ("sword-haradrim", "sword", "Haradrim sword"),
    "hdcurvedsword": ("curvedsword-haradrim", "sword", "Haradrim curved sword"),
}
STEEL_MODEL = {"gdsword": "gdsteelsword", "dwwarhammer": "dwwarhammersteel", "dwshorthammer": "dwshorthammersteel"}
assert set(WEAPONS) | set(STEEL_MODEL.values()) == set(C.MELEE), sorted(set(C.MELEE) ^ (set(WEAPONS) | set(STEEL_MODEL.values())))

# The owner's shields, iron only (owner, 2026-11-14), as the game's iron metal shield (roundshield.json shield-metal:
# 450 durability plus 60 for iron, absorbs 5 damage and 10 from projectiles), absorption times BETTER. They hang on
# the left arm exactly as the NPCs carry them (the catalog's off-hand transform). Combat Overhaul leaves the game's
# shields as they are (its shield patch is switched off), so these get no CO patch.
SHIELDS = {
    "shield": ("shield-mordor", "Orc shield"),
    "mmshield": ("shield-mistymountains", "Misty Mountains orc shield"),
    "kiteshield": ("kiteshield-numenorean", "Numenorean shield"),
    "bnshield": ("shield-blacknumenorean", "Black Numenorean shield"),
    "dwtowershield": ("towershield-dwarven", "Dwarven tower shield"),
    "dwshield": ("shield-dwarven", "Dwarven shield"),
    "eastshield": ("shield-easterling", "Easterling shield"),
    "gondorshield": ("shield-gondor", "Gondorian shield"),
    "boromirshield": ("roundshield-gondor", "Gondorian round shield"),
    "elfshield": ("shield-elven", "Elven shield"),
    "hdlightshield": ("lightshield-haradrim", "Haradrim light shield"),
    "hdheavyshield": ("heavyshield-haradrim", "Haradrim heavy shield"),
}
assert set(SHIELDS) == set(C.SHIELDS), sorted(set(SHIELDS) ^ set(C.SHIELDS))
SHIELD_STATS = {"copies": "game:shield-metal (iron)", "durability": 450 + 60, "absorb": 5, "absorbProjectile": 10,
                "protectionChance": {"passive-projectile": 0.2, "active-projectile": 1, "passive": 0.2, "active": 0.9}}

# The owner's two bows, iron tier only (owner, 2026-11-14), as the game's recurve bow, its best (bow.json: arrow damage
# 4 times BETTER, accuracy +0.3, durability 600). Held as the NPCs hold a drawn bow (the catalog's full-draw transform,
# solved against the game's bowaim, which is the aim animation here too); the three pulled-string stages the NPCs show
# are the bow's draw shapes ("alternates", as the game's charge1-3). Combat Overhaul's bow class needs its own animated
# bow model, so with CO these stay the game's bow (no CO patch).
BOWS = {
    "bow": ("bow-mordor", "Orc bow", C.BOW, C.BOW_DRAWN, C.BOW_PULL_ITEMS),
    "mmbow": ("bow-mistymountains", "Misty Mountains orc bow", C.MM_BOW, C.MM_BOW_DRAWN, C.MM_BOW_PULL_ITEMS),
}
BOW_STATS = {"copies": "game:bow-recurve", "durability": 600, "damage": 4, "damageTier": 0, "accuracy": 0.3}

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


# With Combat Overhaul (CO) installed, a patch makes every weapon but the knives one of its melee weapons
# ("CombatOverhaul:MeleeWeapon", as Not Only Spear Expansion's CO patches do). Damage per metal comes from CO's own
# falx (game/itemtypes/combatoverhaul/sword.json in CO's source) for swords, and from Not Only Spear Expansion's CO
# patches for the rest (its numbers on CO's scale), all times BETTER. "Tier" is what CO's source reads; "Strength" is
# the same number under the name Not Only Spear Expansion's patches use, for CO versions that read that. First person
# uses CO's own animations (it has none for axes, so axes swing like its clubs); third person (AttackTpAnimation)
# is the NPC's swing, so other players see the weapon used as the NPCs use it. Knives keep the game's knife.
_FP = {
    "falx": {"attack": ["combatoverhaul:sword-falx-slash-1", "combatoverhaul:sword-falx-slash-2"],
             "ready": "combatoverhaul:sword-falx-ready", "idle": "combatoverhaul:sword-falx-idle"},
    "club1h": {"attack": ["combatoverhaul:club-strike-1h-1", "combatoverhaul:club-strike-1h-2"],
               "ready": "combatoverhaul:club-ready-1h", "idle": "combatoverhaul:club-idle-1h"},
    "club2h": {"attack": ["combatoverhaul:club-strike-2h-1", "combatoverhaul:club-strike-2h-2"],
               "ready": "combatoverhaul:club-ready-2h", "idle": "combatoverhaul:club-idle-2h"},
    "greatsword": {"attack": ["combatoverhaul:sword-great-slash"],
                   "ready": "combatoverhaul:sword-long-ready", "idle": "combatoverhaul:sword-great-idle-1h"},
    "spear1h": {"attack": ["combatoverhaul:spear-thrust-1h"],
                "ready": "combatoverhaul:spear-ready-1h", "idle": "combatoverhaul:spear-idle-1h"},
    "spear2h": {"attack": ["combatoverhaul:spear-thrust-2h-1", "combatoverhaul:spear-thrust-2h-2", "combatoverhaul:spear-thrust-2h-3"],
                "ready": "combatoverhaul:spear-ready-2h", "idle": "combatoverhaul:spear-idle-2h"},
}
CO_KINDS = {
    "sword": {"copies": "combatoverhaul:blade-falx", "type": "SlashingAttack", "damage": {"iron": 4, "steel": 4},
              "tier": {"iron": 5, "steel": 7}, "knockback": -0.5, "one": "falx", "skill": "oneHandedSwordsProficiency"},
    "axe": {"copies": "spearexpantion:bartka", "type": "SlashingAttack", "damage": {"iron": 4.8, "steel": 5.3},
            "tier": {"iron": 5, "steel": 7}, "knockback": -0.2, "one": "club1h", "skill": "axesProficiency"},
    "battleaxe": {"copies": "spearexpantion:bartka", "type": "SlashingAttack", "damage": {"iron": 4.8, "steel": 5.3},
                  "tier": {"iron": 5, "steel": 7}, "knockback": 0.5, "two": "club2h", "skill": "axesProficiency"},
    "mace": {"copies": "spearexpantion:mace-common", "type": "BluntAttack", "damage": {"iron": 4.9, "steel": 5.2},
             "tier": {"iron": 5, "steel": 6}, "knockback": 0, "one": "club1h", "skill": "macesProficiency"},
    "polehammer": {"copies": "spearexpantion:polehammer", "type": "BluntAttack", "damage": {"iron": 5.1, "steel": 7.1},
                   "tier": {"iron": 5, "steel": 7}, "knockback": 0.5, "two": "club2h", "skill": "halberdsProficiency"},
    "halberd": {"copies": "spearexpantion:halberd", "type": "SlashingAttack", "damage": {"iron": 5.1, "steel": 7.1},
                "tier": {"iron": 5, "steel": 7}, "knockback": 0.5, "two": "club2h", "skill": "halberdsProficiency"},
    "claymore": {"copies": "spearexpantion:claymore", "type": "SlashingAttack", "damage": {"iron": 5, "steel": 6},
                 "tier": {"iron": 4, "steel": 6}, "knockback": 0, "two": "greatsword", "skill": "twoHandedSwordsProficiency"},
    "spear": {"copies": "spearexpantion:spear", "type": "PiercingAttack", "damage": {"iron": 4.5, "steel": 5},
              "damage2h": {"iron": 5.5, "steel": 6}, "tier": {"iron": 4, "steel": 6}, "knockback": 0.5,
              "one": "spear1h", "two": "spear2h", "reach2h": 15, "skill": "spearsProficiency"},
}
assert set(CO_KINDS) == set(KINDS) - {"knife"}
CO_FP_ANIMS = {a for f in _FP.values() for a in f["attack"] + [f["ready"], f["idle"]]}


def blade_line(w, shape):
    """CO's hit line for a weapon, in model blocks: from the grip to the far end of the weapon along its longest
    side (CO's falx: [0, 0, 0.5, 1.2, 0, 0.05], along its blade)."""
    lo, hi = bounds(shape)
    grip = [w["grip"][0] / 16, w["grip"][1] / 16, w["grip"][2] / 16 if len(w["grip"]) > 2 else 0.5]
    axis = max(range(3), key=lambda i: hi[i] - lo[i])
    tip = list(grip)
    tip[axis] = hi[axis] if hi[axis] - grip[axis] >= grip[axis] - lo[axis] else lo[axis]
    return [round(v, 3) for v in grip + tip]


def co_stance(ck, w, line, anims, damage, reach):
    fp = _FP[anims]
    return {"CanAttack": True, "CanParry": False, "CanBlock": False, "CanSprint": True, "SpeedPenalty": 0,
            "Attack": {"StopOnTerrainHit": False, "StopOnEntityHit": True, "CollideWithTerrain": True, "MaxReach": reach,
                       "DamageTypes": [{"DamageByType": {f"*-{m}": {"DamageType": ck["type"], "Tier": ck["tier"][m],
                                                                    "Strength": ck["tier"][m], "Damage": better(damage[m])}
                                                         for m in METALS},
                                        "Knockback": ck["knockback"], "DurabilityDamage": 1, "Collider": line}]},
            "AttackHitSound": "game:sounds/thud", "AttackAnimation": {"Main": fp["attack"]},
            "ReadyAnimation": fp["ready"], "IdleAnimation": fp["idle"], "AttackTpAnimation": w["attack"]}


def write_co_patch(shapes):
    patches = []
    depends = [{"modid": "combatoverhaul"}]
    for key, (code, kind, _) in WEAPONS.items():
        ck = CO_KINDS.get(kind)
        if not ck:
            continue
        w = C.MELEE[key]
        line = blade_line(w, shapes[key])
        # A two-handed weapon gets its two-handed swing in both stances (one hand when the other holds something).
        one = ck.get("one") or ck["two"]
        two = ck.get("two") or ck["one"]
        attributes = {"ProficiencyStats": ["meleeProficiency", ck["skill"]], "RenderingOffset": False,
                      "lotrweapons": {"catalog": key, "copies": KINDS[kind]["copies"], "coCopies": ck["copies"]},
                      "OneHandedStance": co_stance(ck, w, line, one, ck["damage"], 9),
                      "TwoHandedStance": co_stance(ck, w, line, two, ck.get("damage2h", ck["damage"]), ck.get("reach2h", 9))}
        file = f"{DOMAIN}:itemtypes/{code}"
        patches.append({"side": "Server", "dependsOn": depends, "file": file, "op": "replace", "path": "/class",
                        "value": "CombatOverhaul:MeleeWeapon"})
        patches.append({"side": "Server", "dependsOn": depends, "file": file, "op": "addmerge", "path": "/attributes",
                        "value": attributes})
    write("patches/combatoverhaul.json", patches)


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


def visible_boxes(shape):
    """The shape's visible boxes (a texture on some face) as (min corner, max corner) in model blocks, each turned by
    its own and its parents' rotations as the game does (ShapeElement.GetLocalTransformMatrix)."""
    def matmul(a, b):
        return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]

    def move(x, y, z):
        return [[1, 0, 0, x], [0, 1, 0, y], [0, 0, 1, z], [0, 0, 0, 1]]

    def turn(e):
        r = rot({"x": e.get("rotationX") or 0, "y": e.get("rotationY") or 0, "z": e.get("rotationZ") or 0})
        return [r[0] + [0], r[1] + [0], r[2] + [0], [0, 0, 0, 1]]
    out = []

    def walk(elements, parent):
        for e in elements:
            o = [v / 16 for v in (e.get("rotationOrigin") or [0, 0, 0])]
            f, t = [v / 16 for v in e["from"]], [v / 16 for v in e["to"]]
            m = matmul(matmul(matmul(parent, move(*o)), turn(e)), move(*(f[i] - o[i] for i in range(3))))
            faces = e.get("faces") or {}
            if any(isinstance(fc, dict) and fc.get("texture") and fc.get("enabled", True) is not False for fc in faces.values()):
                size = [t[i] - f[i] for i in range(3)]
                pts = [[sum(m[i][k] * v[k] for k in range(4)) for i in range(3)]
                       for v in ([size[0] if c & 4 else 0, size[1] if c & 2 else 0, size[2] if c & 1 else 0, 1] for c in range(8))]
                out.append(([min(p[i] for p in pts) for i in range(3)], [max(p[i] for p in pts) for i in range(3)]))
            walk(e.get("children") or [], m)
    walk(shape["elements"], move(0, 0, 0))
    return out


def bounds(shape):
    """Min and max corner, in blocks, of the shape's visible boxes."""
    boxes = visible_boxes(shape)
    return [min(b[0][i] for b in boxes) for i in range(3)], [max(b[1][i] for b in boxes) for i in range(3)]


REFERENCES = {
    # The game's recurve bow, held with rotation (107, 11, 10); 1.91 blocks long; icon (-129, 25, 41) at scale 1.36,
    # ground scale 4.8 (bow.json).
    "recurve": {"hand": {"x": 107, "y": 11 - 180, "z": 10}, "gui": {"x": -129, "y": 25, "z": 41}, "ground": {},
                "length": 1.91, "guiScale": 1.36, "groundScale": 4.8},
    "falx": {"hand": FALX_HAND, "gui": FALX_GUI, "ground": {}, "length": FALX_LENGTH, "guiScale": FALX_GUI_SCALE,
             "groundScale": FALX_GROUND_SCALE},
}


def display_transforms(rotation, shape, ref="falx"):
    """Icon and ground transforms for a model held with this in-hand rotation, laid as the reference item lies."""
    r = REFERENCES[ref]
    lo, hi = bounds(shape)
    centre = {a: round((lo[i] + hi[i]) / 2, 3) for i, a in enumerate("xyz")}
    # The icon and the ground show the model unscaled, so each item gets the reference's on-screen size.
    size = r["length"] / max(max(hi[i] - lo[i] for i in range(3)), 0.1)
    ours = dict(rotation, y=rotation.get("y", 0) - 180)
    align = mul(transpose(rot(r["hand"])), rot(ours))
    gui = {"translation": {"x": 0, "y": 0, "z": 0}, "rotation": euler_xyz(mul(rot(r["gui"]), align)),
           "origin": centre, "scale": round(r["guiScale"] * size, 3)}
    ground = {"translation": {"x": 0, "y": 0, "z": 0}, "rotation": euler_xyz(mul(rot(r["ground"]), align)), "origin": centre,
              "scale": round(r["groundScale"] * size, 3)}
    return gui, ground, centre


def write_items():
    own = C.own_weapon_textures()
    bundled = set()
    shapes = {}
    for key in list(WEAPONS) + list(STEEL_MODEL.values()):
        shapes[key] = C.weapon_shape(key, C.MELEE[key], own, bundled, DOMAIN, A, FOLDER)
        write(f"shapes/item/{FOLDER}/{key}.json", shapes[key])
    lang = {"tabname-lotrweapons": "LOTR Weapons"}
    write_shields(own, bundled, lang)
    write_bows(own, bundled, lang)
    C.copy_own_textures(bundled, own, A, FOLDER)
    for key, (code, kind, name) in WEAPONS.items():
        w, k = C.MELEE[key], KINDS[kind]
        gui, ground, centre = display_transforms(w["rotation"], shapes[key])
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
            "attackpowerByType": {f"*-{m}": better(k["power"][m]) for m in METALS},
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
            item["attributes"] |= {"damageByType": {f"*-{m}": better(k["thrown"][m]) for m in METALS},
                                   "damageTierByType": {f"*-{m}": k["thrownTier"][m] for m in METALS},
                                   "spearEntityCodeByType": {"*": f"{DOMAIN}:thrown-{code}-{{metal}}"}}
            write_thrown_spear(code, key, w, centre)
        write(f"itemtypes/{code}.json", {k2: v for k2, v in item.items() if v is not None})
        for m in METALS:
            lang[f"item-{code}-{m}"] = f"{m.capitalize()} {name[0].lower() + name[1:] if name.startswith(('Orc', 'Captain')) else name}"
    write("lang/en.json", lang)
    write_co_patch(shapes)
    return lang


def shield_icon(shape):
    """Icon and ground transforms for a shield, from its shape: the face plate (the box with the largest area across
    the shield's thinnest side) toward the viewer, the grip side away, the shield's length upright tipped 15 degrees,
    as big on screen as the game's round shield (0.69 blocks across at icon scale 1.8, ground scale 5)."""
    boxes = visible_boxes(shape)
    lo, hi = bounds(shape)
    ext = [hi[i] - lo[i] for i in range(3)]
    thin = min(range(3), key=lambda i: ext[i])
    along = max((i for i in range(3) if i != thin), key=lambda i: ext[i])
    side = 3 - thin - along
    area = lambda b: (b[1][along] - b[0][along]) * (b[1][side] - b[0][side])
    plate = max(boxes, key=area)
    plate_mid = (plate[0][thin] + plate[1][thin]) / 2
    rest = [(b[0][thin] + b[1][thin]) / 2 for b in boxes if b is not plate] or [plate_mid]
    front = 1 if plate_mid >= sum(rest) / len(rest) else -1
    tip = 15 * D
    # Columns: where the model's axes go on screen (x right, y up, z toward the viewer).
    to_screen = {thin: [0, 0, front], along: [-math.sin(tip), math.cos(tip), 0]}
    a, b = to_screen[thin], to_screen[along]
    cross = [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
    # The third axis completes a right-handed turn whichever order the axes come in.
    to_screen[side] = cross if (thin, along, side) in ((0, 1, 2), (1, 2, 0), (2, 0, 1)) else [-v for v in cross]
    m = [[to_screen[j][i] for j in range(3)] for i in range(3)]
    if abs(sum(m[0][i] * (m[1][(i + 1) % 3] * m[2][(i + 2) % 3] - m[1][(i + 2) % 3] * m[2][(i + 1) % 3]) for i in range(3)) - 1) > 1e-6:
        raise SystemExit("shield icon rotation is not a rotation")
    centre = {a2: round((lo[i] + hi[i]) / 2, 3) for i, a2 in enumerate("xyz")}
    size = 0.69 / max(ext[along], ext[side], 0.1)
    gui = {"translation": {"x": 0, "y": 0, "z": 0}, "rotation": euler_xyz(m), "origin": centre, "scale": round(1.8 * size, 3)}
    # On the ground it lies face up: the face toward +Y.
    flat = mul([[1, 0, 0], [0, 0, -1], [0, 1, 0]], m)
    ground = {"translation": {"x": 0, "y": 0, "z": 0}, "rotation": euler_xyz(flat), "origin": centre, "scale": round(5 * size, 3)}
    return gui, ground


def write_shields(own, bundled, lang):
    for key, (code, name) in SHIELDS.items():
        w = C.SHIELDS[key]
        shape = C.weapon_shape(key, w, own, bundled, DOMAIN, A, FOLDER)
        write(f"shapes/item/{FOLDER}/{key}.json", shape)
        gui, ground = shield_icon(shape)
        k = SHIELD_STATS
        write(f"itemtypes/{code}.json", {
            "code": code, "class": "ItemShield", "storageFlags": 257, "maxstacksize": 1,
            "tags": ["weapon", "shield"],
            "shape": {"base": f"{DOMAIN}:item/{FOLDER}/{key}"},
            "durability": k["durability"],
            "attributes": {
                "shield": {"blockSoundLight": "held/shieldblock-metal-light", "blockSoundHeavy": "held/shieldblock-metal-heavy",
                           "protectionChance": k["protectionChance"],
                           "damageAbsorption": better(k["absorb"]), "projectileDamageAbsorption": better(k["absorbProjectile"])},
                "handbook": {"groupBy": ["shield-*"]},
                "lotrweapons": {"catalog": key, "copies": k["copies"]}},
            "creativeinventory": {"general": ["*"], "items": ["*"], "tools": ["*"], "lotrweapons": ["*"]},
            "tpHandTransform": w["hand"], "tpOffHandTransform": w["hand"],
            "guiTransform": gui, "groundTransform": ground,
        })
        lang[f"item-{code}"] = name


def write_bows(own, bundled, lang):
    for key, (code, name, bow, drawn, pulls) in BOWS.items():
        base = f"{DOMAIN}:item/{FOLDER}/{key}"
        shape = C.weapon_shape(key, bow, own, bundled, DOMAIN, A, FOLDER)
        write(f"shapes/item/{FOLDER}/{key}.json", shape)
        for pull, item in pulls.items():
            stage = C.weapon_shape(pull, item, own, bundled, DOMAIN, A, FOLDER)
            C.pull_string(stage, item["pull"], item.get("apexX"))
            write(f"shapes/item/{FOLDER}/{pull}.json", stage)
        gui, ground, _ = display_transforms(drawn["hand"]["rotation"], shape, "recurve")
        k = BOW_STATS
        write(f"itemtypes/{code}.json", {
            "code": code, "class": "ItemBow", "tags": ["weapon", "weapon-ranged", "weapon-bow"], "maxstacksize": 1,
            "tool": "bow", "durability": k["durability"],
            "shape": {"base": base, "alternates": [{"base": f"{DOMAIN}:item/{FOLDER}/{pull}"} for pull in pulls]},
            "attributes": {"heldItemPitchFollow": 1, "aimAnimation": "bowaim", "damage": better(k["damage"]),
                           "damageTier": k["damageTier"], "statModifier": {"rangedWeaponsAcc": k["accuracy"]},
                           "handbook": {"groupBy": ["bow-*"]}, "lotrweapons": {"catalog": key, "copies": k["copies"]}},
            "creativeinventory": {"general": ["*"], "items": ["*"], "tools": ["*"], "lotrweapons": ["*"]},
            "tpHandTransform": drawn["hand"], "guiTransform": gui, "groundTransform": ground,
        })
        lang[f"item-{code}"] = name


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
