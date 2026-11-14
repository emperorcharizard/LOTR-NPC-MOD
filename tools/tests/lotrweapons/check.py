"""Offline checks for the generated lotrweapons mod (run by tools/build.py).

    python check.py <lotrweapons out dir> <lotrnpcs gen dir>

Every item: iron and steel variants with stats for both, its shapes and their textures in the mod, a name per metal,
swing and hold animations that exist on every player body, and the same hand transform as the NPC's copy of the weapon
(lotrnpcs' orcweapon-<catalog key>). Spears have their thrown entity. Neither mod names the other's domain.
"""
import json
import os
import sys

out, npc = sys.argv[1], sys.argv[2]
A = os.path.join(out, "assets", "lotrweapons")
NA = os.path.join(npc, "assets", "lotrnpcs")
VANILLA_ANIMS = {"falx", "axehit", "hammerhit", "knifestab", "spearhit", "spearidle"}
GAME_TEXTURES_OK = {"game:block/metal/ingot/iron", "game:block/metal/tarnished/rusty-iron", "game:item/tool/handle",
                    "game:block/leather/plain", "game:block/cloth/burlap/string", "game:item/resource/hide/pelt"}
BODIES = {"game:shapes/entity/humanoid/seraph-faceless.json"} | {f"lrc:shapes/entity/humanoid/{b}-faceless.json" for b in ("seraph", "dwarf", "elf")}
errors = []


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def asset(code, kind, ext):
    domain, path = code.split(":", 1)
    assert domain == "lotrweapons", code
    return os.path.join(A, kind, path + ext)


patches = load(os.path.join(A, "patches", "player-animations.json"))
on_body = {}
player_meta = set()
for p in patches:
    if p["path"] == "/animations/-":
        on_body.setdefault(p["value"]["code"], set()).add(p["file"])
        if p["file"].startswith("lrc:") and p.get("dependsOn") != [{"modid": "lrc"}]:
            errors.append(f"patch on {p['file']} must depend on lrc")
    elif p["file"] == "game:entities/humanoid/player.json":
        player_meta.add(p["value"]["code"])
for code, files in on_body.items():
    if files != BODIES:
        errors.append(f"animation {code} is on {sorted(files)}, not on every player body")
    if not {code, code + "-fp"} <= player_meta:
        errors.append(f"animation {code} has no player.json settings (or no -fp)")
anims_ok = VANILLA_ANIMS | set(on_body)

lang = load(os.path.join(A, "lang", "en.json"))
items = 0
for name in sorted(os.listdir(os.path.join(A, "itemtypes"))):
    item = load(os.path.join(A, "itemtypes", name))
    code = item["code"]
    if item["variantgroups"] != [{"code": "metal", "states": ["iron", "steel"]}]:
        errors.append(f"{code}: metals {item['variantgroups']}")
    for stat in ("attackpowerByType", "durabilityByType"):
        if set(item.get(stat, {})) != {"*-iron", "*-steel"}:
            errors.append(f"{code}: {stat} not set for iron and steel")
    for metal in ("iron", "steel"):
        items += 1
        if f"item-{code}-{metal}" not in lang:
            errors.append(f"{code}-{metal}: no name")
    shapes = [s["base"] for s in (item.get("shapeByType") or {}).values()] or [item["shape"]["base"]]
    for base in shapes:
        path = asset(base, "shapes", ".json")
        if not os.path.exists(path):
            errors.append(f"{code}: shape {base} missing")
            continue
        for tex in load(path)["textures"].values():
            if tex.startswith("lotrweapons:"):
                if not os.path.exists(asset(tex, "textures", ".png")):
                    errors.append(f"{code}: texture {tex} missing")
            elif tex not in GAME_TEXTURES_OK:
                errors.append(f"{code}: texture {tex} is not one of ours nor a known game texture")
    for key in ("heldTpHitAnimation", "heldTpIdleAnimation", "heldRightReadyAnimation"):
        if key in item and item[key] not in anims_ok:
            errors.append(f"{code}: {key} {item[key]} is on no player body")
    catalog = item["attributes"]["lotrweapons"]["catalog"]
    npc_item = os.path.join(NA, "itemtypes", f"orcweapon-{catalog}.json")
    if load(npc_item)["tpHandTransform"] != item["tpHandTransform"]:
        errors.append(f"{code}: hand transform differs from the NPC's {catalog}")
    entity = item["attributes"].get("spearEntityCodeByType", {}).get("*")
    if (item["class"] == "ItemSpear") != bool(entity):
        errors.append(f"{code}: spear class and thrown entity don't match")
    if entity and not os.path.exists(os.path.join(A, "entities", entity.split(":", 1)[1].replace("-{metal}", "") + ".json")):
        errors.append(f"{code}: thrown entity {entity} missing")

for root, label, other in ((A, "lotrweapons", "lotrnpcs"), (NA, "lotrnpcs", "lotrweapons")):
    for folder, _, files in os.walk(root):
        for f in files:
            if f.endswith(".json") and f'"{other}:' in open(os.path.join(folder, f), encoding="utf-8-sig").read():
                errors.append(f"{label} names {other}: {os.path.relpath(os.path.join(folder, f), root)}")

if errors:
    sys.exit("lotrweapons checks failed:\n  " + "\n  ".join(errors))
print(f"lotrweapons: {items} items checked")
