"""Blockbench armor project (Vintage Story format, pieces in groups named after the player body's parts) -> one step
parented wearable shape + one texture per armor piece (head, body, legs).

0.0.78 (owner, 2026-11-04: "Use these as skins for the haradrim, mostly as a temporary placeholder", three .bbmodel
files: haradrim_infantry_armor, heavy_infantry, warlord; built as a separate armor mod, owner's choice "B").

The owner's files have a "player body (not exported)" preview group and one armor group holding three piece groups
(head, body, legs). Each piece group holds groups named after body parts (Head, Neck, UpperTorso, LowerTorso,
UpperArmR/L, LowerArmR/L, UpperFootR/L, LowerFootR/L) whose origin is that part's corner in the preview body. Each part
group becomes a top-level element "<prefix>_<part>" with stepParentName <part> and from/to/rotationOrigin 0, its cubes
relative to the part's corner (bbmodel_to_vs.convert does the nesting). Each piece keeps only the textures its faces
use, packed onto one sheet named #material.

Checks and fixes:
- Cubes filed under one arm or leg group but centred on the other side of the body (warlord: two "hand_guard mirrored"
  under LowerArmL at z -4.9) move to the other side's group. Blockbench cube coordinates are absolute, so they stay put
  and follow the right part.
- Part anchors (0.0.78, the owner's corrected files): each part group is placed by the preview body's own part corner,
  not the group's origin, since a group's origin is only Blockbench's pivot and can be moved by accident (the corrected
  infantry file's leggings UpperFootR group had its pivot at y 0 instead of the thigh's corner at y 7, which would have
  lifted the robe 7 px). Part groups must have no rotation.
- Leg fit: the preview body's legs are shorter than the game body's (preview shin 7 px and thigh 7 px, 4 x 3 thick;
  the game's LowerFoot shows 9 px below the thigh, and its UpperFoot is 9 px long, 4.2 x 3.2 thick, its top 3 px inside
  the torso). Pieces on LowerFoot parts are stretched 9/7 in Y; pieces on UpperFoot parts 9/7 in Y and 4.2/4, 3.2/3
  across, about the part's corner, so they cover the whole thigh, the inner side and the top that shows when the leg
  swings (owner, 0.0.78 test: "on the inner side of the legs, the clothes dont cover the skin"). 0.0.78 squeezed the
  thigh 6/7, which left its top bare when walking. Head, neck, torso and arms match the game body and are unchanged.
- Waist pieces (waist_keywords, unused since haradrim-armor 0.1.3): cubes on a thigh whose name has one of the
  keywords hang from LowerTorso instead, unscaled, so they stay still while the legs swing. 0.1.2 did this for the
  robes, skirts, sashes and tassets (my reading of the owner's 0.0.78 note "portion of the leg that is like a part of the
  coat, is made part of the actual leg instead of overclothes"); the owner's 0.0.80 test: "The armor around the legs
  isnt tracking the movements of the legs and they phase through", so they are back on the thighs, where the owner's
  files put them.
- Hanging pieces (hang_below, haradrim-armor 0.1.3, owner: "the belts should also move with the legs"): cubes on
  LowerTorso whose bottom is more than hang_below px under the preview waist (the heavy infantry's sash tails, which
  reach the knee) move to the thigh on their side (by their centre's z) so they swing with it. They keep their size
  and their height under the waist (the game's waist is 6 px up its thigh, HANG_WAIST; the preview's is 7, the thigh's
  top), and get the thigh pieces' widening in x and z, so at rest they sit where they hung from the waist.
- Veils (veil_top): on the head piece, veil cubes in front of the face whose top edge crosses the eyes are cut down to
  veil_top px above the head's bottom (the game face's eyes are 1.75 to 2.55 px up; the preview body's were higher),
  their side faces' textures cut to match rather than squashed; veil hems just above are moved down with them (owner,
  0.0.78 test: "The eyes arent lined up").

Usage:
  python3 -I bbmodel_armor_to_vs.py <in.bbmodel> <outdir> <prefix> <texture path prefix>
writes <outdir>/<piece>.json and <outdir>/<piece>.png for each piece (head, body, legs), the shape's #material pointing
at "<texture path prefix>/<piece>".
"""
import copy, json, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bbmodel_to_vs import convert, load  # noqa: E402

PARTS = {"Head", "Neck", "UpperTorso", "LowerTorso", "UpperArmR", "UpperArmL", "LowerArmR", "LowerArmL",
         "UpperFootR", "UpperFootL", "LowerFootR", "LowerFootL"}
OTHER_SIDE = {"UpperArmL": "UpperArmR", "LowerArmL": "LowerArmR", "UpperFootL": "UpperFootR", "LowerFootL": "LowerFootR"}
OTHER_SIDE.update({v: k for k, v in list(OTHER_SIDE.items())})
LEG_SCALE = {"LowerFootR": (1, 9 / 7, 1), "LowerFootL": (1, 9 / 7, 1),
             "UpperFootR": (4.2 / 4, 9 / 7, 3.2 / 3), "UpperFootL": (4.2 / 4, 9 / 7, 3.2 / 3)}
# Hanging pieces: the game's waist (LowerTorso's bottom) is 6 px above its thigh's corner; the preview's is 7.
HANG_WAIST = 6.0
HANG = "~hang"
SIDE_FACES = ("north", "south", "east", "west")
PIECES = ("head", "body", "legs")


def lower_veils(project, head_corner, veil_top, log):
    """Cuts veil cubes in front of the face down to veil_top px above the head's bottom (see the module notes)."""
    hx, hy = head_corner[0], head_corner[1]
    for e in project["elements"]:
        n = e["name"].lower()
        if "veil" not in n or e["from"][0] > hx + 0.15:
            continue
        bottom, top = e["from"][1] - hy, e["to"][1] - hy
        if top > 2.8 or top <= veil_top + 1e-6:
            continue
        if "hem" in n and bottom >= veil_top - 0.05:
            dy = veil_top - top
            e["from"][1] += dy; e["to"][1] += dy
            if e.get("origin"): e["origin"][1] += dy
            log.append(f"head: {e['name']} moved down {-dy:.2f} px")
        elif bottom < veil_top:
            frac = (veil_top - bottom) / (top - bottom)
            e["to"][1] = round(hy + veil_top, 5)
            for side, f in (e.get("faces") or {}).items():
                if side in SIDE_FACES and f.get("uv") and not f.get("rotation"):
                    u1, v1, u2, v2 = f["uv"]
                    f["uv"] = [u1, round(v2 + (v1 - v2) * frac, 5), u2, v2]
            log.append(f"head: {e['name']} top cut from {top:.2f} to {veil_top} px")


def piece_projects(project, waist_keywords=(), veil_top=None, hang_below=None):
    """{piece name: a copy of the project holding only that piece group, its strays moved, its own textures}."""
    project = copy.deepcopy(project)
    groups = {g["uuid"]: g for g in project["groups"]}
    elements = {e["uuid"]: e for e in project["elements"]}
    preview = [x for x in project["outliner"] if isinstance(x, dict) and groups[x["uuid"]]["name"].lower().startswith("player body")]
    corners = {}
    for x in preview:
        for c in x["children"]:
            if isinstance(c, str) and elements[c]["name"] in PARTS:
                corners[elements[c]["name"]] = elements[c]["from"]
    if set(corners) != PARTS:
        raise SystemExit(f"the player body preview lacks {sorted(PARTS - set(corners))}")
    armor = [x for x in project["outliner"] if isinstance(x, dict) and not groups[x["uuid"]]["name"].lower().startswith("player body")]
    if len(armor) != 1:
        raise SystemExit(f"expected one armor group besides the player body, found {len(armor)}")
    out, moved = {}, []
    if veil_top is not None:
        lower_veils(project, corners["Head"], veil_top, moved)
    for entry in armor[0]["children"]:
        if not isinstance(entry, dict):
            raise SystemExit("a loose cube sits outside the head, body and legs groups")
        name = groups[entry["uuid"]]["name"].lower()
        if name not in PIECES:
            raise SystemExit(f"unexpected piece group {name}")
        entry = copy.deepcopy(entry)
        # A piece may hold two groups for one part (the corrected files moved the body's thigh groups into legs), so
        # parts are a list, and to_step_parented merges same-named ones.
        part_list = [(groups[c["uuid"]]["name"], c) for c in entry["children"] if isinstance(c, dict)]
        parts = {}
        for part, node in part_list:
            parts.setdefault(part, node)
        if set(parts) - PARTS or any(not isinstance(c, dict) for c in entry["children"]):
            raise SystemExit(f"{name}: only body-part groups expected, got {sorted(set(parts) - PARTS)}")
        for part, node in part_list:
            g = groups[node["uuid"]]
            if any(g.get("rotation") or [0, 0, 0]):
                raise SystemExit(f"{name}: the {part} group is rotated")
            if list(g.get("origin", [0, 0, 0])) != list(corners[part]):
                moved.append(f"{name}: {part} group anchored at the body's corner {corners[part]}, not its pivot {g.get('origin')}")
        for part, node in part_list:
            other = OTHER_SIDE.get(part)
            if other is None:
                continue
            for c in list(node["children"]):
                if not isinstance(c, str):
                    continue
                e = elements[c]
                z = (e["from"][2] + e["to"][2]) / 2
                if (part.endswith("L") and z < -0.5) or (part.endswith("R") and z > 0.5):
                    if other not in parts:
                        raise SystemExit(f"{e['name']} sits on the {other} side, which has no group in {name}")
                    node["children"].remove(c)
                    parts[other]["children"].append(c)
                    moved.append(f"{name}: {e['name']} {part} -> {other}")
        # Coat-like cubes on the thighs hang from the waist instead (see "Waist pieces").
        if waist_keywords:
            waist = []
            for part, node in part_list:
                if not part.startswith("UpperFoot"):
                    continue
                for c in list(node["children"]):
                    if isinstance(c, str) and any(k in elements[c]["name"].lower() for k in waist_keywords):
                        node["children"].remove(c)
                        waist.append(c)
            if waist:
                uuid = f"waist-{name}"
                g = {"uuid": uuid, "name": "LowerTorso", "origin": list(corners["LowerTorso"]), "rotation": [0, 0, 0]}
                groups[uuid] = g
                project["groups"].append(g)
                entry["children"].append({"uuid": uuid, "children": waist})
                moved.append(f"{name}: {len(waist)} cubes hang from LowerTorso: " + ", ".join(sorted({elements[c]['name'] for c in waist})))
        # Torso cubes reaching down the legs swing with the thigh on their side (see "Hanging pieces").
        if hang_below is not None:
            waist_y = corners["LowerTorso"][1]
            hang = {"UpperFootR": [], "UpperFootL": []}
            for part, node in part_list:
                if part != "LowerTorso":
                    continue
                for c in list(node["children"]):
                    if isinstance(c, str) and elements[c]["from"][1] < waist_y - hang_below:
                        e = elements[c]
                        node["children"].remove(c)
                        hang["UpperFootR" if (e["from"][2] + e["to"][2]) / 2 < 0 else "UpperFootL"].append(c)
            for leg, cubes in hang.items():
                if not cubes:
                    continue
                uuid = f"hang-{name}-{leg}"
                g = {"uuid": uuid, "name": leg + HANG, "origin": list(corners[leg]), "rotation": [0, 0, 0]}
                groups[uuid] = g
                project["groups"].append(g)
                entry["children"].append({"uuid": uuid, "children": cubes})
                moved.append(f"{name}: " + ", ".join(elements[c]["name"] for c in cubes) + f" LowerTorso -> {leg} (hanging)")
        # Only the textures this piece's faces use.
        used = []

        def collect(x):
            for c in x["children"]:
                if isinstance(c, str):
                    for f in (elements[c].get("faces") or {}).values():
                        t = f.get("texture")
                        if t is not None and t is not False and t not in used:
                            used.append(t)
                else:
                    collect(c)
        collect(entry)
        sub = copy.deepcopy(project)
        sub["outliner"] = [entry]
        # Each part group's origin = the preview body's part corner (see "Part anchors").
        for g in sub["groups"]:
            if g["name"] in PARTS and any(groups[c["uuid"]] is not None and c["uuid"] == g["uuid"] for c in entry["children"]):
                g["origin"] = list(corners[g["name"]])
        remap = {}
        textures = []
        for t in used:
            idx = t if isinstance(t, int) else next(i for i, tt in enumerate(project["textures"])
                                                   if tt.get("uuid") == t or str(tt.get("id")) == str(t))
            remap[t] = len(textures)
            textures.append(project["textures"][idx])
        sub["textures"] = textures
        for e in sub["elements"]:
            for f in (e.get("faces") or {}).values():
                if f.get("texture") in remap:
                    f["texture"] = remap[f["texture"]]
        out[name] = sub
    return out, moved


def scale_xyz(e, k):
    for key in ("from", "to", "rotationOrigin"):
        if e.get(key) is not None:
            e[key] = [round(e[key][i] * k[i], 5) for i in range(3)]
    for c in e.get("children") or []:
        scale_xyz(c, k)


THIGH_PREVIEW = 7.0  # the preview body's thigh height (its waist above the thigh's corner)


def move_hanging(e, sx, sz, dy):
    """A hanging cube relative to the thigh's corner: x and z positions widened like the thigh, y moved by dy, size kept
    (only top-level positions move; children are relative to their parent and keep their size)."""
    f, t = e["from"], e["to"]
    cx, cz = (f[0] + t[0]) / 2, (f[2] + t[2]) / 2
    ddx, ddz = cx * (sx - 1), cz * (sz - 1)
    for key in ("from", "to", "rotationOrigin"):
        if e.get(key) is not None:
            e[key] = [round(e[key][0] + ddx, 5), round(e[key][1] + dy, 5), round(e[key][2] + ddz, 5)]


def to_step_parented(shape, prefix, texture_path):
    """bbmodel_to_vs output (piece group -> part groups -> cubes) -> step parented top-level pieces, one per body part
    (same-named part groups merged: all are anchored at the same body corner)."""
    (piece,) = shape["elements"]
    top, by_part = [], {}
    for part in piece["children"]:
        kids = part["children"]
        name = part["name"]
        if name.endswith(HANG):
            # Hanging pieces: widened like the thigh pieces, not stretched; their height under the waist kept.
            name = name[:-len(HANG)]
            sx, _, sz = LEG_SCALE[name]
            for c in kids:
                move_hanging(c, sx, sz, HANG_WAIST - THIGH_PREVIEW)
        elif name in LEG_SCALE:
            for c in kids:
                scale_xyz(c, LEG_SCALE[name])
        if name in by_part:
            by_part[name]["children"].extend(kids)
            continue
        el = {"name": f"{prefix}_{name}", "from": [0, 0, 0], "to": [0, 0, 0], "rotationOrigin": [0, 0, 0],
              "stepParentName": name, "children": kids}
        by_part[name] = el
        top.append(el)

    def retexture(es):
        for e in es:
            for f in (e.get("faces") or {}).values():
                f["texture"] = "#material"
            retexture(e.get("children") or [])
    retexture(top)
    return {"textureWidth": shape["textureWidth"], "textureHeight": shape["textureHeight"],
            "textureSizes": {"material": [shape["textureWidth"], shape["textureHeight"]]},
            "textures": {"material": texture_path}, "elements": top, "animations": []}


def main(argv):
    src, outdir, prefix, tex_prefix = argv[:4]
    os.makedirs(outdir, exist_ok=True)
    pieces, moved = piece_projects(load(src))
    for m in moved:
        print("moved", m)
    for name, sub in pieces.items():
        shape, sheet = convert(sub)
        result = to_step_parented(shape, prefix, f"{tex_prefix}/{name}")
        with open(os.path.join(outdir, name + ".json"), "w", encoding="utf-8") as f:
            json.dump(result, f, indent=1)
        sheet.save(os.path.join(outdir, name + ".png"), optimize=True)
        n = sum(1 for _ in walk(result["elements"]))
        print(f"{name}: {n} elements, {len(sub['textures'])} texture(s), sheet {sheet.width} x {sheet.height} px")


def walk(elements):
    for e in elements:
        yield e
        yield from walk(e.get("children") or [])


if __name__ == "__main__":
    main(sys.argv[1:])
