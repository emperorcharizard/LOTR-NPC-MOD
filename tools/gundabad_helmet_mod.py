"""Builds gundabad-bone-armor 0.3.0 (the owner's armor mod, mod id gundabad): 0.2.0 plus the owner's Gundabad helmet.

0.0.95 (owner, 2026-11-06, with Gundabad_Helmet_Variant_1.bbmodel: "give the gundabad captain this helmet option"; chose
"A": add it to their gundabad-bone-armor mod, next to the bone armor; "Armor is never bundled").

The owner's file holds a reference body (the seraph in its rest pose: LowerTorso z 3, UpperTorso z -6, Neck z 12, Head z
-7, the game's own rest rotations) and the helmet group "Gundabad Helmet" (Light Metal, Dark Metal, Nasal Bridge) in
absolute coordinates over the posed head. The helmet is moved into the head's own frame: each point goes through the
inverse of the reference head's world matrix (the Head group's chain of rotations, then the corner of Head_geo, which is
the game Head element's box: both 5.5 x 5 x 5). That inverse is a 2 degree turn about Z and a shift, written as a parent
element turned -2 degrees about the head's centre, so the cubes keep the owner's exact sizes and angles.

Item code in the mod's pattern: gundabad:armor-head-tierfourplate-gundabadhelmetone-gundabadhelmetone-none. Protection,
durability and walk speed are placeholders of my choosing (like the Haradrim heavy helmet). No recipe: creative
inventory only (my call; the bone pieces keep theirs).

Usage: python3 -I gundabad_helmet_mod.py <gundabad-bone-armor-0.2.0.zip> <Gundabad_Helmet_Variant_1.bbmodel> <out.zip>
"""
import copy, io, json, math, os, sys, zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bbmodel_to_vs import convert, load  # noqa: E402

VERSION = "0.3.0"
MODID = "gundabad"
TIER = "tierfourplate"
CONSTRUCT = "gundabadhelmetone"
CODE = f"armor-head-{TIER}-{CONSTRUCT}-{CONSTRUCT}-none"
ITEM = f"{MODID}:{CODE}"
HELMET_GROUP = "Gundabad Helmet"
CHAIN = ("LowerTorso", "UpperTorso", "Neck", "Head")
TEX = f"entity/humanoid/{MODID}/head/{CONSTRUCT}/{CONSTRUCT}"


def _rot(axis, deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    if axis == 0: return [[1, 0, 0], [0, c, -s], [0, s, c]]
    if axis == 1: return [[c, 0, s], [0, 1, 0], [-s, 0, c]]
    return [[c, -s, 0], [s, c, 0], [0, 0, 1]]


def _mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def _app(m, v):
    return [sum(m[i][k] * v[k] for k in range(3)) for i in range(3)]


def _xyz(rx, ry, rz):  # vsapi Mat4f.RotateByXYZ: Rx * Ry * Rz (as tools/preview/vsrender.py)
    return _mul(_mul(_rot(0, rx), _rot(1, ry)), _rot(2, rz))


def head_frame(project):
    """World matrix (rotation R, translation t, px) of the reference head's corner frame: world = R p + t."""
    groups = {g["uuid"]: g for g in project["groups"]}
    elements = {e["uuid"]: e for e in project["elements"]}
    path = []

    def find(node, trail):
        if isinstance(node, str):
            return None
        g = groups[node["uuid"]]
        trail = trail + [g]
        if g["name"] == "Head":
            return trail, node
        for c in node["children"]:
            r = find(c, trail)
            if r: return r
        return None
    for n in project["outliner"]:
        r = find(n, [])
        if r: path, head_node = r; break
    if [g["name"] for g in path] != list(CHAIN):
        raise SystemExit(f"reference body chain is {[g['name'] for g in path]}, expected {list(CHAIN)}")
    geo = next(elements[c] for c in head_node["children"] if isinstance(c, str) and elements[c]["name"] == "Head_geo")
    # Blockbench (VS format) group: rotate its contents about its origin; nested, outermost first.
    R = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]; t = [0.0, 0.0, 0.0]
    for g in path:
        Rg = _xyz(*(g.get("rotation") or [0, 0, 0])); o = g["origin"]
        # world = M_prev(Rg (p - o) + o): R' = R Rg, t' = R (o - Rg o) + t
        t = [a + b for a, b in zip(_app(R, [x - y for x, y in zip(o, _app(Rg, o))]), t)]
        R = _mul(R, Rg)
    t = [a + b for a, b in zip(_app(R, geo["from"]), t)]  # corner frame: p_world = R (p + geo.from) + t
    size = [b - a for a, b in zip(geo["from"], geo["to"])]
    return R, t, size, [g.get("rotation") or [0, 0, 0] for g in path]


def helmet_shape(project):
    R, t, size, rots = head_frame(project)
    if any(r[0] or r[1] for r in rots):
        raise SystemExit("reference chain turns about X or Y; only Z turns are handled")
    net_z = sum(r[2] for r in rots)
    only = copy.deepcopy(project)
    node = next(n for n in project["outliner"] if not isinstance(n, str) and
                next(g for g in project["groups"] if g["uuid"] == n["uuid"])["name"] == HELMET_GROUP)
    only["outliner"] = [node]
    shape, sheet = convert(only)
    (helmet,) = shape["elements"]
    centre = [s / 2 for s in size]                      # head centre in the head frame
    P = [a + b for a, b in zip(_app(R, centre), t)]     # the same point on the posed reference head
    off = [round(o - p, 5) for o, p in zip(helmet["from"], P)]
    helmet.update({"from": off, "to": off, "rotationOrigin": off})
    for k in ("rotationX", "rotationY", "rotationZ"): helmet.pop(k, None)
    turn = {"name": "GBH_HeadTurn", "from": centre, "to": centre, "rotationOrigin": centre, "children": [helmet]}
    if abs(net_z) > 1e-9: turn["rotationZ"] = -net_z
    top = {"name": "GBH_Head", "from": [0, 0, 0], "to": [0, 0, 0], "rotationOrigin": [0, 0, 0], "stepParentName": "Head",
           "children": [turn]}

    def retexture(es):
        for e in es:
            for f in (e.get("faces") or {}).values(): f["texture"] = "#material"
            retexture(e.get("children") or [])
    retexture([top])
    out = {"textureWidth": shape["textureWidth"], "textureHeight": shape["textureHeight"],
           "textureSizes": {"material": [shape["textureWidth"], shape["textureHeight"]]},
           "textures": {"material": TEX}, "elements": [top], "animations": []}
    return out, sheet


def itemtype():
    return {
        "code": "armor",
        "behaviors": [{"name": "Wearable"}],
        "heldTpIdleAnimation": "holdunderarm",
        "variantgroups": [{"code": "bodypart", "states": ["head"]}, {"code": "tier", "states": [TIER]},
                          {"code": "construct", "states": [CONSTRUCT]}, {"code": "material", "states": [CONSTRUCT]},
                          {"code": "cloak", "states": ["none"]}],
        "skipVariants": [],
        "allowedVariants": [CODE],
        "shapeByType": {"*": {"base": "entity/humanoid/seraph/gundabad/armor/{bodypart}/{construct}/{construct}"}},
        "texturesByType": {"*": {"seraph": {"base": "game:block/transparent"},
                                 "material": {"base": "entity/humanoid/gundabad/{bodypart}/{construct}/{material}"}}},
        "storageFlags": 128,
        "attributes": {
            "visibleDamageEffect": True,
            "wearableAttachment": True,
            "clothescategoryByType": {"*": "armorhead"},
            "attachableToEntity": {"texturePrefixCode": "gundabad-{bodypart}-{construct}-{material}",
                                   "categoryCodeByType": {"*": "armorhead"}, "disableElementsByType": {"*": []}},
            "defaultProtLoss": {"perTierRelativeProtectionLoss": [0.03, 0.15], "perTierFlatDamageReductionLoss": [0.1, 0.2]},
            "protectionModifiersByType": {"*": {"relativeProtection": 0.85, "flatDamageReduction": 1.5,
                                                "highDamageTierResistant": True, "protectionTier": 3}},
            "statModifiersByType": {"*": {"walkSpeed": -0.02}},
            "footStepSoundByType": {},
        },
        "creativeinventory": {"general": ["*"], "items": ["*"], "clothing": ["*"]},
        "renderAlphaTest": 0.25,
        "durabilityByType": {"*": 1300},
        "tpHandTransformByType": {"*": {"translation": {"x": -1, "y": -1.9, "z": -0.5}, "rotation": {"x": 97, "y": -168, "z": 94},
                                        "origin": {"x": 0.5, "y": 1.5, "z": 0.5}, "scale": 0.67}},
        # Held, GUI and ground transforms as the Haradrim helmets (the bone armor's).
        "guiTransformByType": {"*": {"translation": {"x": 3, "y": 0, "z": 0}, "rotation": {"x": -26, "y": -46, "z": -180},
                                     "origin": {"x": 0.5, "y": 1.11, "z": 0.5}, "scale": 1.37}},
        "groundTransformByType": {"*": {"translation": {"x": 0, "y": 0, "z": 0}, "rotation": {"x": 0, "y": 0, "z": 90},
                                        "origin": {"x": 0.34, "y": 1.12, "z": 0.5}, "scale": 3.1}},
    }


def build(base_zip, bbmodel, out_zip):
    with zipfile.ZipFile(base_zip) as z:
        files = {n: z.read(n) for n in z.namelist() if not n.endswith("/")}
    shape, sheet = helmet_shape(load(bbmodel))
    files[f"assets/{MODID}/shapes/entity/humanoid/seraph/{MODID}/armor/head/{CONSTRUCT}/{CONSTRUCT}.json"] = \
        json.dumps(shape, indent=1).encode()
    buf = io.BytesIO(); sheet.save(buf, "PNG", optimize=True)
    files[f"assets/{MODID}/textures/{TEX}.png"] = buf.getvalue()
    files[f"assets/{MODID}/itemtypes/wearable/{MODID}/helmet.json"] = json.dumps(itemtype(), indent=2).encode()
    lang = json.loads(files[f"assets/{MODID}/lang/en.json"])
    lang[f"item-{CODE}"] = "Gundabad Helmet"
    lang[f"itemdesc-{CODE}"] = "A tall Gundabad war helm of dark and light iron with a nasal bridge."
    files[f"assets/{MODID}/lang/en.json"] = json.dumps(lang, indent=2, ensure_ascii=False).encode()
    info = json.loads(files["modinfo.json"])
    info["version"] = VERSION
    info["description"] = "Gundabad bone armor (chestplate with gauntlets, leggings with boots) and a Gundabad helmet."
    files["modinfo.json"] = json.dumps(info, indent=2).encode()
    with zipfile.ZipFile(out_zip, "w", zipfile.ZIP_DEFLATED) as z:
        for name in sorted(files):
            z.writestr(name, files[name])
    print(f"{out_zip}: {len(files)} files, helmet {ITEM}")


if __name__ == "__main__":
    build(*sys.argv[1:4])
