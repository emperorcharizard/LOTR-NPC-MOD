"""Offline checks for the generated lotrweapons mod (run by tools/build.py).

    python check.py <lotrweapons out dir> <lotrnpcs gen dir>

Every weapon: iron and steel variants with stats for both, its shapes and their textures in the mod, a name per metal,
swing and hold animations that exist on every player body, and the same hand transform as the NPC's copy of the weapon
(lotrnpcs' orcweapon-<catalog key>). Spears have their thrown entity. Shields and bows are iron only and held as the NPC's are. Every weapon but the knives has a Combat Overhaul
patch (class and both stances, gated on combatoverhaul) using CO's own first-person animations and our third-person
swing. Neither mod names the other's domain.
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

# Combat Overhaul's own first-person animations (its config/animations/melee/*.json, source read 2026-11-14).
CO_ANIMS = {"combatoverhaul:" + a for a in (
    "sword-falx-slash-1", "sword-falx-slash-2", "sword-falx-ready", "sword-falx-idle", "club-strike-1h-1", "club-strike-1h-2",
    "club-ready-1h", "club-idle-1h", "club-strike-2h-1", "club-strike-2h-2", "club-ready-2h", "club-idle-2h",
    "sword-great-slash", "sword-long-ready", "sword-great-idle-1h", "spear-thrust-1h", "spear-ready-1h", "spear-idle-1h",
    "spear-thrust-2h-1", "spear-thrust-2h-2", "spear-thrust-2h-3", "spear-ready-2h", "spear-idle-2h")}
co_class, co_attr = set(), {}
for p in load(os.path.join(A, "patches", "combatoverhaul.json")):
    code = p["file"].rsplit("/", 1)[1]
    if p.get("dependsOn") != [{"modid": "combatoverhaul"}]:
        errors.append(f"CO patch on {code} must depend on combatoverhaul")
    if p["path"] == "/class":
        co_class.add(code) if p["value"] == "CombatOverhaul:MeleeWeapon" else errors.append(f"{code}: CO class {p['value']}")
    else:
        co_attr[code] = p["value"]

lang = load(os.path.join(A, "lang", "en.json"))
items = 0
for name in sorted(os.listdir(os.path.join(A, "itemtypes"))):
    item = load(os.path.join(A, "itemtypes", name))
    code = item["code"]
    if item["class"] in ("ItemShield", "ItemBow"):
        # Shields and bows: iron only (no metal variants), held as the NPCs hold them (a shield's off-hand transform,
        # a bow's full-draw item), no Combat Overhaul patch.
        items += 1
        catalog = item["attributes"]["lotrweapons"]["catalog"]
        shield = item["class"] == "ItemShield"
        npc_item = load(os.path.join(NA, "itemtypes", f"orcweapon-{catalog if shield else catalog + '-drawn'}.json"))
        held = "tpOffHandTransform" if shield else "tpHandTransform"
        if "variantgroups" in item or f"item-{code}" not in lang or item.get("durability", 0) <= 0:
            errors.append(f"{code}: variants, name or durability wrong")
        if npc_item[held] != item[held]:
            errors.append(f"{code}: {held} differs from the NPC's {catalog}")
        if code in co_class or code in co_attr:
            errors.append(f"{code}: shields and bows get no Combat Overhaul patch")
        shapes = [item["shape"]["base"]] + [a["base"] for a in item["shape"].get("alternates", [])]
        if not shield and len(shapes) != 4:
            errors.append(f"{code}: a bow needs its three draw shapes")
        for base in shapes:
            path = asset(base, "shapes", ".json")
            if not os.path.exists(path) or any(t.startswith("lotrweapons:") and not os.path.exists(asset(t, "textures", ".png"))
                                               for t in load(path)["textures"].values()):
                errors.append(f"{code}: shape {base} or its texture missing")
        continue
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
    power = item["attackpowerByType"]
    if power["*-steel"] < power["*-iron"]:
        errors.append(f"{code}: steel hits softer than iron")
    knife = item["class"] == "ItemKnife"
    if knife == (code in co_class or code in co_attr):
        errors.append(f"{code}: {'knives keep the game knife under CO' if knife else 'no Combat Overhaul patch'}")
    for name, st in ((n, co_attr.get(code, {}).get(n)) for n in ("OneHandedStance", "TwoHandedStance")):
        if st is None:
            if not knife:
                errors.append(f"{code}: CO {name} missing")
            continue
        hits = st["Attack"]["DamageTypes"][0]
        if set(hits["DamageByType"]) != {"*-iron", "*-steel"} or len(hits["Collider"]) != 6:
            errors.append(f"{code}: CO {name} damage per metal or collider wrong")
        if st["AttackTpAnimation"] not in anims_ok:
            errors.append(f"{code}: CO {name} third-person animation {st['AttackTpAnimation']} is on no player body")
        for a in st["AttackAnimation"]["Main"] + [st["ReadyAnimation"], st["IdleAnimation"]]:
            if a not in CO_ANIMS:
                errors.append(f"{code}: CO {name} animation {a} is not one of Combat Overhaul's")
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
