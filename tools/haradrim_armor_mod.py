"""Builds the owner's Haradrim armor mod (mod id haradrimarmor), a placeholder armor mod like gundabad-bone-armor.

0.0.78 (owner, 2026-11-04: "Use these as skins for the haradrim, mostly as a temporary placeholder"; chose a separate
mod so players can wear it too). Three sets from the owner's Blockbench files (build-inputs/haradrim-armor-owner-
originals): infantry (Haradrim recruits and archers), heavy infantry (warriors), warlord (captains), each a helmet,
body armor and leggings. Converted by bbmodel_armor_to_vs.py.

Item codes follow the "To Rule Them All" armor mods' pattern, so lotrnpcs treats them like any faction armor:
    haradrimarmor:armor-<head|body|legs>-<tier>-<construct>-<construct>-none
Protection numbers, durability and walk speed are placeholders of my choosing (told to the owner): infantry like
chain, heavy and warlord like plate. No recipes: creative inventory only (my call; easy to add).

Usage: python3 -I haradrim_armor_mod.py <build-inputs/haradrim-armor-owner-originals> <out.zip> [version]
"""
import io, json, os, sys, tempfile, zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bbmodel_armor_to_vs as conv  # noqa: E402
from bbmodel_to_vs import convert, load  # noqa: E402

MODID = "haradrimarmor"
VERSION = "0.1.3"
# 0.1.2 (owner's 0.0.78 test): the robe, skirt, sash, coin and tasset cubes hung from the waist, not the thighs; veils
# cut down below the game face's eyes (top 1.6 px up the head; the warlord's mask already cleared them).
# 0.1.3 (owner's 0.0.80 test: "The armor around the legs isnt tracking the movements of the legs and they phase
# through", "the belts should also move with the legs"): no waist pieces, everything the owner put on a thigh stays on
# it; torso cubes reaching more than HANG_BELOW px below the waist (the heavy infantry's sash tails) swing with the thigh
# on their side.
WAIST_KEYWORDS = ()
HANG_BELOW = 1.0
VEIL_TOP = 1.6
SETS = {  # rank set -> (source file, tier, display name, protection (relative, flat, tier), durability, walk speed)
    "infantry": ("haradrim_infantry_armor.bbmodel", "tierthreechain", "Haradrim Infantry", (0.75, 1.0, 2), 900, -0.02),
    "heavy": ("heavy_infantry.bbmodel", "tierfourplate", "Haradrim Heavy Infantry", (0.85, 1.5, 3), 1300, -0.04),
    "warlord": ("warlord.bbmodel", "tierfiveplate", "Haradrim Warlord", (0.88, 2.0, 3), 1600, -0.05),
}
PIECE = {"head": ("helmet", "Helmet", "armorhead"), "body": ("armor", "Armor", "armorbody"),
         "legs": ("leggings", "Leggings", "armorlegs")}
# Body parts the worn piece hides on the wearer's own body and clothes (as gundabad-bone-armor does).
DISABLE = {
    "head": [],
    "body": ["Waist", "hideupperarm", "hidelowerarm", "mittenfrill-l", "mittenfrill-r", "glovefrill-l", "glovefrill-r",
             "ShirtFrontTile", "ShirtBackTile", "coatFrontTile", "coatBackTile", "shouldertextureFrontTile",
             "shouldertextureBackTile"],
    "legs": ["hideshoeL", "hideshoeR", "shoefluffL", "shoefluffR"],
}


def construct(rank, part):
    return f"haradrim{rank}{PIECE[part][0]}"


def code(rank, part):
    c = construct(rank, part)
    return f"armor-{part}-{SETS[rank][1]}-{c}-{c}-none"


def item_codes():
    """Every item code, with the mod's domain, by rank set and piece."""
    return {rank: {part: f"{MODID}:{code(rank, part)}" for part in PIECE} for rank in SETS}


def itemtype():
    all_codes = [code(r, p) for r in SETS for p in PIECE]
    by = lambda f: {code(r, p): f(r, p) for r in SETS for p in PIECE}
    return {
        "code": "armor",
        "behaviors": [{"name": "Wearable"}],
        "heldTpIdleAnimation": "holdunderarm",
        "variantgroups": [
            {"code": "bodypart", "states": list(PIECE)},
            {"code": "tier", "states": sorted({s[1] for s in SETS.values()})},
            {"code": "construct", "states": [construct(r, p) for r in SETS for p in PIECE]},
            {"code": "material", "states": [construct(r, p) for r in SETS for p in PIECE]},
            {"code": "cloak", "states": ["none"]},
        ],
        "allowedVariants": all_codes,
        "shapeByType": {"*": {"base": f"entity/humanoid/seraph/{MODID}/armor/{{bodypart}}/{{construct}}/{{construct}}"}},
        "texturesByType": {"*": {"seraph": {"base": "game:block/transparent"},
                                 "material": {"base": f"entity/humanoid/{MODID}/{{bodypart}}/{{construct}}/{{material}}"}}},
        "storageFlags": 128,
        "attributes": {
            "visibleDamageEffect": True,
            "wearableAttachment": True,
            "clothescategoryByType": by(lambda r, p: PIECE[p][2]),
            "attachableToEntity": {
                "texturePrefixCode": f"{MODID}-{{bodypart}}-{{construct}}-{{material}}",
                "categoryCodeByType": by(lambda r, p: PIECE[p][2]),
                "disableElementsByType": by(lambda r, p: DISABLE[p]),
            },
            "defaultProtLoss": {"perTierRelativeProtectionLoss": [0.03, 0.15], "perTierFlatDamageReductionLoss": [0.1, 0.2]},
            "protectionModifiersByType": by(lambda r, p: {"relativeProtection": SETS[r][3][0], "flatDamageReduction": SETS[r][3][1],
                                                          "highDamageTierResistant": SETS[r][3][2] >= 3, "protectionTier": SETS[r][3][2]}),
            "statModifiersByType": by(lambda r, p: {"walkSpeed": SETS[r][5]}),
        },
        "creativeinventory": {"general": ["*"], "items": ["*"], "clothing": ["*"]},
        "renderAlphaTest": 0.25,
        "durabilityByType": by(lambda r, p: SETS[r][4]),
        "tpHandTransformByType": {"*": {"translation": {"x": -1, "y": -1.9, "z": -0.5}, "rotation": {"x": 97, "y": -168, "z": 94},
                                        "origin": {"x": 0.5, "y": 1.5, "z": 0.5}, "scale": 0.67}},
        "guiTransformByType": {"*": {"translation": {"x": 3, "y": 0, "z": 0}, "rotation": {"x": -26, "y": -46, "z": -180},
                                     "origin": {"x": 0.5, "y": 1.11, "z": 0.5}, "scale": 1.37}},
        "groundTransformByType": {"*": {"translation": {"x": 0, "y": 0, "z": 0}, "rotation": {"x": 0, "y": 0, "z": 90},
                                        "origin": {"x": 0.34, "y": 1.12, "z": 0.5}, "scale": 3.1}},
    }


def build(src_dir, out_zip, version=VERSION):
    files = {}
    lang = {}
    for rank, (src, tier, title, *_rest) in SETS.items():
        pieces, moved = conv.piece_projects(load(os.path.join(src_dir, src)), WAIST_KEYWORDS, VEIL_TOP, HANG_BELOW)
        for m in moved:
            print(f"{rank}: moved {m}")
        if set(pieces) != set(PIECE):
            raise SystemExit(f"{src}: expected head, body and legs, got {sorted(pieces)}")
        for part, sub in pieces.items():
            c = construct(rank, part)
            shape, sheet = convert(sub)
            tex = f"entity/humanoid/{MODID}/{part}/{c}/{c}"
            result = conv.to_step_parented(shape, f"HR{rank[0].upper()}", tex)
            files[f"assets/{MODID}/shapes/entity/humanoid/seraph/{MODID}/armor/{part}/{c}/{c}.json"] = json.dumps(result, indent=1).encode()
            buf = io.BytesIO(); sheet.save(buf, "PNG", optimize=True)
            files[f"assets/{MODID}/textures/{tex}.png"] = buf.getvalue()
            lang[f"item-{code(rank, part)}"] = f"{title} {PIECE[part][1]}"
            lang[f"itemdesc-{code(rank, part)}"] = f"{title} {PIECE[part][1].lower()}, a placeholder model until the final Haradrim armor."
    files[f"assets/{MODID}/itemtypes/wearable/{MODID}/armor.json"] = json.dumps(itemtype(), indent=2).encode()
    files[f"assets/{MODID}/lang/en.json"] = json.dumps(lang, indent=2, ensure_ascii=False).encode()
    files["modinfo.json"] = json.dumps({
        "type": "content", "modid": MODID, "name": "Haradrim Armor (placeholder)",
        "authors": ["Haradrim armor model creator (unspecified)"],
        "description": "Placeholder Haradrim armor: infantry, heavy infantry and warlord sets (helmet, armor, leggings). Worn by LOTR NPCs' Haradrim and wearable by players.",
        "version": version, "requiredOnClient": True, "requiredOnServer": True}, indent=2).encode()
    with zipfile.ZipFile(out_zip, "w", zipfile.ZIP_DEFLATED) as z:
        for name in sorted(files):
            z.writestr(name, files[name])
    print(f"{out_zip}: {len(files)} files")


if __name__ == "__main__":
    build(sys.argv[1], sys.argv[2], *(sys.argv[3:4] or []))
