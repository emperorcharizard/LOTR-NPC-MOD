"""Copies the LOTR Races and Classes (LRC) race models the NPCs use into the lotrnpcs asset domain.

Used with the LRC authors' permission (Dango Dango Dango, Shakgriig, Buggoi), with credit.
The copies are registered as Player Model Lib models "lotrnpcs:<race>" and are disabled, so players
never see them in character creation. LRC itself is not needed.

Usage: python3 -I bundle_lrc.py <lrc assets/lrc folder> <output assets/lotrnpcs folder>
"""
import json
import os
import re
import shutil
import sys

LRC = sys.argv[1]
OUT = sys.argv[2]


def orc_skins(*numbers):
    return [{"code": f"orcskin{n}", "texture": f"lrc:entity/humanoid/skinparts/orc/orcskin{n}"} for n in numbers]


def shapes_named(prefix, codes):
    return [{"code": code, "shape": {"base": prefix + code}} for code in codes]


DARK_HAIR = ["eerieblack", "liver", "indigodye"]
FAIR_HAIR = ["wheat", "ecru", "sanddune", "harvestgold", "russet"]
RED_BROWN_HAIR = ["russet", "rosewood", "cordovan", "liver", "eerieblack", "oldburgundy"]
DWARF_HAIR = ["eerieblack", "liver", "cordovan", "rosewood", "russet", "oldburgundy", "slategray", "silverchalice"]
HOBBIT_HAIR = ["liver", "cordovan", "rosewood", "russet", "wheat", "eerieblack"]
HOBBIT_SKINS = [f"hobbitskin{n}" for n in (1, 2, 3, 4, 5, 6, 8)]
DALE_HAIR = ["cordovan", "rosewood", "russet", "liver", "sanddune"]
ARNOR_HAIR = ["eerieblack", "liver", "slategray", "cordovan"]
HARAD_HAIR = ["eerieblack", "liver", "oldburgundy", "indigodye"]
NUMENOREAN_HAIR = ["eerieblack", "slategray", "indigodye", "silverchalice"]
WICKED_DWARF_HAIR = ["lightgray", "silverchalice", "slategray", "eerieblack"]
ELF_HAIR = ["silverchalice", "wheat", "ecru", "harvestgold", "eerieblack", "liver", "cordovan"]
TORMENTED_ELF_HAIR = ["eerieblack", "slategray", "darkpurple", "liver"]
MEN_HAIR_STYLES = ["balding", "combed", "classic", "layered", "longflowing", "longwithstrands", "mediumlength-bangs",
                   "messy", "short", "short-parted", "short-trimmed"]
DWARF_HAIR_STYLES = ["bald", "balding", "messy", "longflowing", "longwithstrands", "layered", "classic", "short"]
# Player Model Lib picks beard "none" 70% of the time, so for dwarves "none" is itself a dwarf beard.
DWARF_BEARDS = ([{"code": "none", "shape": {"base": "lrc:entity/humanoid/hair-face/dwarf-1"}}]
                + shapes_named("lrc:entity/humanoid/hair-face/dwarf-", ["2", "3", "4", "5"])
                + shapes_named("game:entity/humanoid/seraphskinparts/hair-face/",
                               ["brd-box20-full", "brd-braid10-double", "brd-braid15-triple", "brd-full10-shaggy"]))

# Extra hair styles for every orc body (0.0.67, user, 2026-11-04: "I dont like the last hair added, instead add
# dreadlocks, and messy"). Both are the game's own hair-base shapes, found through each body's shapeTemplate; they
# replace 0.0.64's long balding hair. Uruks already have dreadlocks from LRC, so they only gain messy.
ORC_EXTRA_HAIR = [{"code": "dreadlocks"}, {"code": "messy"}]

ORC_EARS = [
    {"code": "plumpears", "shape": {"base": "lrc:entity/humanoid/ears/orc_earsplump"}},
    {"code": "arrowears", "shape": {"base": "lrc:entity/humanoid/ears/orc_earsarrow"}},
]

# Our model code -> the LRC config file and model it starts from, plus changes:
#   "settings" overrides top-level values, "variants" replaces a part's variants, "append" adds variants,
#   "keep" limits a part to the listed variant codes.
# Goblins, Mordor orcs and Uruk-hai are three races built from LRC's two orc bodies. Each gets its
# own skin shades, so body and face skins (picked separately) stay close.
MODELS = {
    "goblin": ("orc-char.json", "orc", {
        "variants": {
            "baseskin": orc_skins(5, 6, 7),
            "face": orc_skins(5, 6, 7),
            # LRC ships these faces but none of its races use them yet.
            "facialexpression": shapes_named("lrc:entity/humanoid/face/goblin/goblin_",
                                             ["angry", "grin", "neutral", "serious", "upset"]),
        },
        "append": {"hairbase": ORC_EXTRA_HAIR},
        "keep": {
            "underwear": ["breeches"],
            "eyecolor": ["bitter-lemon", "citrine"],
            "haircolor": ["slategray", "eerieblack"],
            "brows": ["slategray", "eerieblack"],
        },
    }),
    "mordororc": ("orc-char.json", "orc", {
        "settings": {"ModelSizeFactor": 0.9, "GuiModelScale": 0.9},
        "variants": {
            "baseskin": orc_skins(2, 3, 8),
            "face": orc_skins(2, 3, 8),
            "ears": ORC_EARS,
            # LRC's own orc faces, without tusks (user, 2026-11-03: "FA has the tusks. lotr chars does not."). Until
            # 0.0.56 these were LRC's unused tusked orc_angry/orc_determined; the codes stay, so old orcs keep their look
            # minus the tusks.
            "facialexpression": shapes_named("lrc:seraphfaces/", ["angry", "serious", "determined", "fierce"]),
        },
        "append": {"hairbase": ORC_EXTRA_HAIR},
        "keep": {
            "underwear": ["breeches"],
            "eyecolor": ["topaz", "ruby", "citrine"],
            "haircolor": ["eerieblack", "slategray"],
            "brows": ["eerieblack", "slategray"],
        },
    }),
}
# Gundabad orcs (0.0.63, user, 2026-11-03: "gundabad orcs have an average lighter skintone"): the Mordor orc in every
# way (the owner chose the Mordor orc body at 0.9 for them in 0.0.62) except the skins: LRC orcskin 2, 1 and 7, the
# three lightest (average brightness 122, Mordor's 2, 3 and 8 average 95). orcskin 7 is the pale blue-grey the Misty
# Mountains also use; orcskin 1 is a warm one no other race uses.
MODELS["gundabadorc"] = ("orc-char.json", "orc", {
    **MODELS["mordororc"][2],
    "variants": {**MODELS["mordororc"][2]["variants"], "baseskin": orc_skins(2, 1, 7), "face": orc_skins(2, 1, 7)},
})
MODELS |= {
    "uruk": ("uruk-char.json", "uruk", {
        "variants": {
            "baseskin": orc_skins(4, 9),
            "face": orc_skins(4, 9),
        },
        "keep": {
            "underwear": ["breeches"],
            "hairbase": ["dreadlocks", "longflowing", "longwithstrands"],
            "hairextra": ["none", "tieddreads", "unkempt"],
            "haircolor": ["eerieblack"],
            "brows": ["eerieblack"],
            "eyecolor": ["ruby", "topaz"],
            "facialexpression": ["angry", "determined", "fierce", "serious"],
        },
        "append": {"hairbase": [{"code": "messy"}]},
    }),
    # Gondor and Rohan share LRC's Men of the West, told apart by hair: dark for Gondor, fair for Rohan.
    "gondor": ("freepeople-char.json", "freepeople", {
        "keep": {
            "underwear": ["breeches"],
            "haircolor": DARK_HAIR,
            "brows": DARK_HAIR,
            "hairbase": ["balding", "combed", "classic", "layered", "mediumlength-bangs", "short", "short-parted",
                         "short-trimmed", "longwithstrands"],
        },
    }),
    "rohan": ("freepeople-char.json", "freepeople", {
        "keep": {
            "underwear": ["breeches"],
            "haircolor": FAIR_HAIR,
            "brows": FAIR_HAIR,
            "hairbase": ["longflowing", "longwithstrands", "layered", "classic", "messy", "combed"],
        },
    }),
    "dunlending": ("wildmen.json", "wildmen", {
        "keep": {
            "underwear": ["breeches"],
            "haircolor": RED_BROWN_HAIR,
            "brows": RED_BROWN_HAIR,
            "hairbase": ["messy", "messy2", "longflowing", "longwithstrands", "dreadlocks", "balding", "layered"],
        },
    }),
    "easterling": ("easterling-char.json", "easterling", {
        "keep": {
            "underwear": ["breeches"],
            "haircolor": ["eerieblack", "indigodye", "liver", "oldburgundy"],
            "brows": ["eerieblack", "indigodye", "liver", "oldburgundy"],
            # No "kniaz": its crown and side locks came out grey and untinted in game (0.0.7 shots).
            "hairbase": ["bald", "balding", "short", "short-trimmed", "combed", "classic", "longwithstrands", "layered"],
        },
    }),
    "dwarf": ("longbeard-char.json", "longbeard", {
        "variants": {
            # Player Model Lib picks beard "none" 70% of the time, so here "none" is itself a dwarf beard.
            "beard": DWARF_BEARDS,
        },
        "keep": {
            "underwear": ["breeches"],
            "haircolor": DWARF_HAIR,
            "brows": DWARF_HAIR,
            "hairbase": DWARF_HAIR_STYLES,
        },
    }),
    "hobbit": ("harfoot-char.json", "harfoot", {
        "keep": {
            "underwear": ["breeches"],
            "haircolor": HOBBIT_HAIR,
            "brows": HOBBIT_HAIR,
            "hairbase": ["afro", "messy", "messy2", "short", "layered", "classic", "combed", "mediumlength-bangs"],
            # No hobbitskin7: it is nearly colourless and made hobbits look grey in game (0.0.7 shots).
            "baseskin": HOBBIT_SKINS,
            "face": HOBBIT_SKINS,
        },
    }),
    # 0.0.35: Dale and Arnor are Men of the West too: brown and red hair for Dale, dark and grey for the Dunedain.
    "dale": ("freepeople-char.json", "freepeople", {
        "keep": {
            "underwear": ["breeches"],
            "haircolor": DALE_HAIR,
            "brows": DALE_HAIR,
            "hairbase": MEN_HAIR_STYLES,
        },
    }),
    "arnor": ("freepeople-char.json", "freepeople", {
        "keep": {
            "underwear": ["breeches"],
            "haircolor": ARNOR_HAIR,
            "brows": ARNOR_HAIR,
            "hairbase": ["combed", "classic", "layered", "longflowing", "longwithstrands", "messy", "short-trimmed"],
        },
    }),
    # No "kniaz" for any race: it came out grey on Easterlings.
    "haradrim": ("haradrim.json", "haradrim", {
        "keep": {
            "underwear": ["breeches"],
            "haircolor": HARAD_HAIR,
            "brows": HARAD_HAIR,
            "hairbase": MEN_HAIR_STYLES + ["bald", "dreadlocks"],
        },
    }),
    "blacknumenorean": ("bnumenorean-char.json", "bnumenorean", {
        "keep": {
            "underwear": ["breeches"],
            "haircolor": NUMENOREAN_HAIR,
            "brows": NUMENOREAN_HAIR,
            "hairbase": MEN_HAIR_STYLES + ["bald"],
        },
    }),
    "wickeddwarf": ("wickeddwarf.json", "wickeddwarf", {
        "variants": {
            "beard": DWARF_BEARDS,
        },
        "keep": {
            "underwear": ["breeches"],
            "haircolor": WICKED_DWARF_HAIR,
            "brows": WICKED_DWARF_HAIR,
            "hairbase": DWARF_HAIR_STYLES,
        },
    }),
    # Noldor, Sindar and Avari are one faction for now (user, 2026-11-02); the Sindar body stands for all three.
    "elf": ("sindar-char.json", "sindar", {
        "keep": {
            "underwear": ["breeches"],
            "haircolor": ELF_HAIR,
            "brows": ELF_HAIR,
        },
    }),
    "tormentedelf": ("tormentedelf-char.json", "tormentedelf", {
        "keep": {
            "underwear": ["breeches"],
            "haircolor": TORMENTED_ELF_HAIR,
            "brows": TORMENTED_ELF_HAIR,
        },
    }),
}

# Inside the braces: the game's JSON parser rejects a comment before the first token.
CREDIT = ("  // Race model from LOTR Races and Classes by Dango Dango Dango, Shakgriig and Buggoi,\n"
          "  // bundled in LOTR NPCs with their permission.\n")


def load_lenient(path):
    """Reads Vintage Story JSON, which allows // comments and trailing commas."""
    text = open(path, encoding="utf-8-sig").read()
    text = re.sub(r'(?m)(?<!:)//.*$', "", text)
    text = re.sub(r",(\s*[}\]])", r"\1", text)
    return json.loads(text)


def lrc_file(kind, path):
    """Returns the LRC file for an asset path, or None if LRC doesn't have it."""
    ext = ".json" if kind == "shapes" else ".png"
    full = os.path.join(LRC, kind, path + ("" if path.endswith(ext) else ext))
    return full if os.path.isfile(full) else None


shapes, textures, missing = set(), set(), set()


def add_ref(path):
    """Records an lrc-domain asset path as a shape or texture to copy."""
    if lrc_file("shapes", path):
        shapes.add(path)
    elif lrc_file("textures", path):
        textures.add(path)
    else:
        missing.add(path)


def collect(node, variant_codes=None):
    if isinstance(node, dict):
        codes = [v["code"] for v in node.get("variants", []) if isinstance(v, dict) and "code" in v] or variant_codes
        for value in node.values():
            collect(value, codes)
    elif isinstance(node, list):
        for value in node:
            collect(value, variant_codes)
    elif isinstance(node, str) and node.startswith("lrc:"):
        path = node[4:]
        if "{code}" in path:
            for code in variant_codes or []:
                add_ref(path.replace("{code}", code))
        else:
            add_ref(path)


# Every hair colour the game's own skin randomizer rules can ask for.
ALL_HAIR_COLORS = ["lightgray", "silverchalice", "slategray", "eerieblack", "mossgreen", "seagreen", "wintergreendream",
                   "steelblue", "queenblue", "indigodye", "heliotropegray", "palatinatepurple", "darkpurple", "wheat",
                   "ecru", "sanddune", "harvestgold", "cordovan", "rosewood", "russet", "liver", "oldburgundy"]


def limit_colors(part, allowed):
    """Limits a hair or brow colour part to the allowed colours.

    The game's randomizer rules can pick a colour by name. A name the part lacks leaves the hair with no
    colour, which shows as grey, so every colour name stays and the unwanted ones draw an allowed colour.
    """
    template = part.pop("textureTemplate")
    part["variants"] = [
        {"code": color, "texture": template.replace("{code}", color if color in allowed else allowed[i % len(allowed)])}
        for i, color in enumerate(ALL_HAIR_COLORS)
    ]


def apply_changes(code, config, changes):
    config.update(changes.get("settings", {}))
    parts = {part["code"]: part for part in config["SkinnableParts"]}
    for part_code, variants in changes.get("variants", {}).items():
        parts[part_code]["variants"] = variants
    for part_code, keep in changes.get("keep", {}).items():
        part = parts[part_code]
        if part_code in ("haircolor", "brows"):
            limit_colors(part, keep)
            continue
        unknown = set(keep) - {v["code"] for v in part["variants"]}
        if unknown:
            sys.exit(f"{code}: part {part_code} has no variants {sorted(unknown)}")
        part["variants"] = [v for v in part["variants"] if v["code"] in keep]
    for part_code, extra in changes.get("append", {}).items():
        parts[part_code]["variants"] = parts[part_code]["variants"] + [dict(v) for v in extra]


configs = {}
for code, (filename, lrc_key, changes) in MODELS.items():
    config = load_lenient(os.path.join(LRC, "config", "customplayermodels", filename))[lrc_key]
    apply_changes(code, config, changes)
    collect(config)
    configs[code] = config

# Shapes name their own textures. Paths without a domain resolve to the shape's domain, so any that
# LRC has must come along at the same path.
for shape in sorted(shapes):
    for value in (load_lenient(lrc_file("shapes", shape)).get("textures") or {}).values():
        if not isinstance(value, str):
            continue
        if value.startswith("lrc:"):
            add_ref(value[4:])
        elif ":" not in value and lrc_file("textures", value):
            textures.add(value)

# Clear what an earlier run bundled, so races or files dropped since don't linger. Only the shape and texture folders
# LRC's files go into are cleared: gen_testnpcs.py writes the weapon models to shapes/item (0.0.45) and their textures
# to textures/item (0.0.46).
if os.path.isdir(OUT):
    # Two folder levels deep (0.0.69): LRC's bodies are in shapes/entity/humanoid, and gen_testnpcs.py writes the
    # archers' arrow to shapes/entity/npcarrow.json, which clearing all of shapes/entity used to delete (the build had
    # to save and restore it around this script).
    lrc_dirs = sorted({os.path.join("shapes", *shape.split("/")[:-1][:2]) for shape in shapes}
                      | {os.path.join("textures", *texture.split("/")[:-1][:2]) for texture in textures})
    assert not {os.path.join("shapes", "item"), os.path.join("textures", "item")} & set(lrc_dirs), \
        "LRC files would replace the weapon models or their textures"
    for sub in lrc_dirs + [os.path.join("config", "customplayermodels")]:
        shutil.rmtree(os.path.join(OUT, sub), ignore_errors=True)


def retarget(text):
    return text.replace('"lrc:', '"lotrnpcs:')


# Animations of our own, added to the body every bundled race uses (0.0.68, user, 2026-11-04: the poleaxe "has spear
# animations instead of poleaxe animations", then "just do a swing similar to an axe chop but for a poleaxe"). Each
# file in tools/anims is one shape animation. poleaxechop is the game's axehit (windup at 9, blow at 14) with the left
# arm re-solved so the left hand stays on the shaft near its butt the whole swing, starting and ending in the
# poleaxe's holdbothhands guard pose; made with the offline preview tools.
BODY = "entity/humanoid/seraph-faceless"
# 0.0.76: the dwarf and elf bodies get them too, since dwarves now swing the battleaxe with poleaxechop and hold it with
# poleaxehold (the poleaxe's two-handed guard with the right hand higher up the shaft).
BODIES = {BODY, "entity/humanoid/dwarf-faceless", "entity/humanoid/elf-faceless"}
ANIM_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "anims")


def add_animations(text):
    marker = '"animations": ['
    if text.count(marker) != 1:
        sys.exit("the body shape's animation list was not found")
    ours = [json.load(open(os.path.join(ANIM_DIR, name))) for name in sorted(os.listdir(ANIM_DIR)) if name.endswith(".json")]
    existing = set(re.findall(r'"code":\s*"([^"]+)"', text))
    clash = {a["code"] for a in ours} & existing
    if clash:
        sys.exit(f"body already has animations {sorted(clash)}")
    return text.replace(marker, marker + "".join("\n" + json.dumps(a, separators=(",", ":")) + "," for a in ours), 1)


for shape in sorted(shapes):
    dest = os.path.join(OUT, "shapes", shape + ".json")
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    with open(lrc_file("shapes", shape), encoding="utf-8-sig") as src, open(dest, "w", encoding="utf-8") as f:
        text = retarget(src.read())
        f.write(add_animations(text) if shape in BODIES else text)

def shrink_png(src, dest):
    """
    0.0.76 (user, 2026-11-04: "Shrink the bundled race textures"): the PNG re-saved with the best zlib settings and
    without its metadata chunks, kept only if every pixel is the same and the file is smaller (about 31% off the race
    textures). Otherwise the original is copied.
    """
    import io
    from PIL import Image
    raw = open(src, "rb").read()
    try:
        image = Image.open(io.BytesIO(raw))
        image.load()
        buf = io.BytesIO()
        image.save(buf, "PNG", optimize=True)
        out = buf.getvalue()
        same = Image.open(io.BytesIO(raw)).convert("RGBA").tobytes() == Image.open(io.BytesIO(out)).convert("RGBA").tobytes()
    except Exception:
        out, same = raw, True
    with open(dest, "wb") as f:
        f.write(out if same and len(out) < len(raw) else raw)


for texture in sorted(textures):
    dest = os.path.join(OUT, "textures", texture + ".png")
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    shrink_png(lrc_file("textures", texture), dest)

for race, config in configs.items():
    config["Enabled"] = False  # NPC-only: hidden from character creation.
    config["ExtraTraits"] = []  # LRC's traits and classes are player features and need LRC.
    config["AvailableClasses"] = []
    dest = os.path.join(OUT, "config", "customplayermodels", race + ".json")
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    with open(dest, "w", encoding="utf-8") as f:
        f.write("{\n" + CREDIT + retarget(json.dumps({race: config}, indent=2))[2:])
        f.write("\n")

print(f"{len(configs)} races, {len(shapes)} shapes, {len(textures)} textures")
for path in sorted(missing):
    print("not in LRC:", path)
