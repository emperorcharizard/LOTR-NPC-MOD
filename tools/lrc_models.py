"""Writes the NPCs' Player Model Lib models and the patch that gives LOTR Races and Classes' bodies our weapon animations.

1.0.4 (outside review; owner: "Make it a dependency"): LOTR Races and Classes (LRC, by Dango Dango Dango, Shakgriig and
Buggoi) is a required mod and nothing of it is copied any more. Most races use LRC's own model (tools/data/races.json
names it, with the looks each faction may have: dark hair for Gondor on LRC's shared Men of the West, and so on). Only
the orc bodies get a model of ours, since they add looks LRC doesn't have; those are written here from LRC's own
model files, every shape and texture still LRC's (lrc:...), disabled so players never see them in character creation.

Usage: python3 -I lrc_models.py <lrc assets/lrc folder> <output assets/lotrnpcs folder>
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from jsonfmt import dumps  # noqa: E402

LRC = sys.argv[1]
OUT = sys.argv[2]
TOOLS = os.path.dirname(os.path.abspath(__file__))
RACES = os.path.join(TOOLS, "data", "races.json")


def orc_skins(*numbers):
    return [{"code": f"orcskin{n}", "texture": f"lrc:entity/humanoid/skinparts/orc/orcskin{n}"} for n in numbers]


def shapes_named(prefix, codes):
    return [{"code": code, "shape": {"base": prefix + code}} for code in codes]


# Extra hair styles for every orc body (0.0.67, user, 2026-11-04: "I dont like the last hair added, instead add
# dreadlocks, and messy"). Both are the game's own hair-base shapes, found through each body's shapeTemplate. Uruks
# already have dreadlocks from LRC, so they only gain messy.
ORC_EXTRA_HAIR = [{"code": "dreadlocks"}, {"code": "messy"}]

ORC_EARS = [
    {"code": "plumpears", "shape": {"base": "lrc:entity/humanoid/ears/orc_earsplump"}},
    {"code": "arrowears", "shape": {"base": "lrc:entity/humanoid/ears/orc_earsarrow"}},
]

# Our own models: code -> the LRC model file and key they start from, plus what they add. "settings" overrides
# top-level values, "variants" replaces a part's variants, "append" adds variants. Which of them each faction may
# have is in tools/data/races.json, like every other race's.
MODELS = {
    # Misty Mountains goblins: LRC's orc with the goblin faces LRC ships but none of its races use yet.
    "goblin": ("orc-char.json", "orc", {
        "variants": {
            "baseskin": orc_skins(5, 6, 7),
            "face": orc_skins(5, 6, 7),
            "facialexpression": shapes_named("lrc:entity/humanoid/face/goblin/goblin_",
                                             ["angry", "grin", "neutral", "serious", "upset"]),
        },
        "append": {"hairbase": ORC_EXTRA_HAIR},
    }),
    # Mordor, Angmar and Gundabad orcs: LRC's orc at 0.9 size (owner's choice, 0.0.62) with the Uruk-hai's ears and
    # LRC's faces without tusks (user, 2026-11-03: "FA has the tusks. lotr chars does not."). Skins: Mordor's 2, 3
    # and 8, Gundabad's lighter 1, 2 and 7 (0.0.63), picked per faction in races.json.
    "mordororc": ("orc-char.json", "orc", {
        "settings": {"ModelSizeFactor": 0.9, "GuiModelScale": 0.9},
        "variants": {
            "baseskin": orc_skins(1, 2, 3, 7, 8),
            "face": orc_skins(1, 2, 3, 7, 8),
            "ears": ORC_EARS,
            "facialexpression": shapes_named("lrc:seraphfaces/", ["angry", "serious", "determined", "fierce"]),
        },
        "append": {"hairbase": ORC_EXTRA_HAIR},
    }),
    # Isengard Uruk-hai: LRC's Uruk with the game's messy hair too.
    "uruk": ("uruk-char.json", "uruk", {
        "append": {"hairbase": [{"code": "messy"}]},
    }),
}

# Inside the braces: the game's JSON parser rejects a comment before the first token.
CREDIT = ("  // NPC-only model made from LOTR Races and Classes' own (by Dango Dango Dango, Shakgriig and Buggoi).\n"
          "  // Its shapes and textures are LRC's, so LRC must be installed.\n")


def load_lenient(path):
    """Reads Vintage Story JSON, which allows // comments and trailing commas."""
    text = open(path, encoding="utf-8-sig").read()
    text = re.sub(r'(?m)(?<!:)//.*$', "", text)
    text = re.sub(r",(\s*[}\]])", r"\1", text)
    return json.loads(text)


def lrc_models():
    """Every model LRC defines, by code (lrc:freepeople), from its config/customplayermodels files."""
    found = {}
    folder = os.path.join(LRC, "config", "customplayermodels")
    for name in sorted(os.listdir(folder)):
        if name.endswith(".json"):
            for key, model in load_lenient(os.path.join(folder, name)).items():
                found["lrc:" + key] = model
    return found


def apply_changes(config, changes):
    config.update(changes.get("settings", {}))
    parts = {part["code"]: part for part in config["SkinnableParts"]}
    for part_code, variants in changes.get("variants", {}).items():
        parts[part_code]["variants"] = variants
    for part_code, extra in changes.get("append", {}).items():
        parts[part_code]["variants"] = parts[part_code]["variants"] + [dict(v) for v in extra]


def asset_exists(code):
    """Whether an lrc: shape or texture code names a file in LRC."""
    path = code.split(":", 1)[1]
    return any(os.path.isfile(os.path.join(LRC, kind, path + ext)) for kind, ext in (("shapes", ".json"), ("textures", ".png")))


def lrc_refs(node, codes=None):
    """Every lrc: asset code a model names, with {code} templates filled from the part's variants."""
    if isinstance(node, dict):
        codes = [v["code"] for v in node.get("variants", []) if isinstance(v, dict) and "code" in v] or codes
        for value in node.values():
            yield from lrc_refs(value, codes)
    elif isinstance(node, list):
        for value in node:
            yield from lrc_refs(value, codes)
    elif isinstance(node, str) and node.startswith("lrc:"):
        yield from ((node.replace("{code}", c) for c in codes or []) if "{code}" in node else [node])


lrc = lrc_models()
models = {}
for code, (filename, key, changes) in MODELS.items():
    config = load_lenient(os.path.join(LRC, "config", "customplayermodels", filename))[key]
    apply_changes(config, changes)
    config["Enabled"] = False  # NPC-only: hidden from character creation.
    config["ExtraTraits"] = []  # LRC's traits and classes are player features.
    config["AvailableClasses"] = []
    missing = sorted(r for r in set(lrc_refs(config)) if not asset_exists(r))
    if missing:
        sys.exit(f"{code}: not in LRC: {missing}")
    models["lotrnpcs:" + code] = config
    dest = os.path.join(OUT, "config", "customplayermodels", code + ".json")
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    with open(dest, "w", encoding="utf-8") as f:
        f.write("{\n" + CREDIT + dumps({code: config})[2:])

# Every race's model must exist and offer every look races.json allows it.
races = json.load(open(RACES, encoding="utf-8"))
for race, entry in races.items():
    model = models.get(entry["model"]) or lrc.get(entry["model"])
    if model is None:
        sys.exit(f"races.json: {race} names model {entry['model']}, which neither LRC nor we define")
    offered = {p["code"]: {v["code"] for v in p.get("variants", [])} for p in model["SkinnableParts"]}
    for part, allowed in entry["parts"].items():
        bad = [v for v in allowed if v not in offered.get(part, ())]
        if bad:
            sys.exit(f"races.json: {race}'s {part} lists {bad}, which {entry['model']} doesn't have")

# Animations of our own, added to the LRC bodies (0.0.68, user, 2026-11-04: the poleaxe "has spear animations instead
# of poleaxe animations", then "just do a swing similar to an axe chop but for a poleaxe"; 0.0.76: the dwarf and elf
# bodies too, for the dwarves' battleaxe). Each file in tools/anims is one shape animation. Since 1.0.4 they go in by a
# JSON patch on LRC's own body shapes instead of our copies of them; players on those bodies gain unused animations.
BODIES = ["entity/humanoid/seraph-faceless", "entity/humanoid/dwarf-faceless", "entity/humanoid/elf-faceless"]
ANIM_DIR = os.path.join(TOOLS, "anims")
ours = [json.load(open(os.path.join(ANIM_DIR, name))) for name in sorted(os.listdir(ANIM_DIR)) if name.endswith(".json")]
patches = []
for body in BODIES:
    shape = load_lenient(os.path.join(LRC, "shapes", body + ".json"))
    clash = {a["code"] for a in ours} & {a.get("code") for a in shape.get("animations", [])}
    if clash:
        sys.exit(f"LRC's {body} already has animations {sorted(clash)}")
    patches += [{"op": "add", "path": "/animations/-", "value": anim, "file": f"lrc:shapes/{body}.json"} for anim in ours]
dest = os.path.join(OUT, "patches", "lrc-body-animations.json")
os.makedirs(os.path.dirname(dest), exist_ok=True)
with open(dest, "w", encoding="utf-8") as f:
    f.write(dumps(patches))

print(f"{len(models)} models of ours, {len(races)} races checked, {len(patches)} animation patches")
