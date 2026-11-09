"""Turns a Model Creator armor file whose pieces sit in groups named after seraph body parts (UpperTorso, UpperArmR...)
into a step parented wearable shape (made gundabad-bone-armor 0.2.0's chest piece from the owner's
gundabad_bone_armor.json, 2026-11-04).

The owner models each group's children in that body part's own frame (the group's from is the part's corner), so each
group becomes a top-level piece "GB_<part>" with stepParentName <part> and from/to/rotationOrigin 0; children are kept
as they are. Pieces filed under the wrong arm (the right arm's horns, tusks and forearm spikes sat in the left arm's
groups, 8 px to the side) are moved to the right arm's group: a child of a ...L group whose centre is beyond the part's
opposite side by more than --stray px moves to the ...R group, shifted by the two groups' offset. Every face's texture
becomes #material. Editor-only elements ("not exported") and empty child stepParentName keys are dropped.
Usage:
  python3 -I convert_partgroup_armor.py <in.json> <out.json> <seraph.json> --texture <asset path for #material>"""
import argparse, json

SIDES = {"UpperArmL": "UpperArmR", "LowerArmL": "LowerArmR", "UpperFootL": "UpperFootR", "LowerFootL": "LowerFootR"}


def find_groups(elements, parts):
    """The elements named after body parts, wherever they are nested."""
    out = []
    for e in elements:
        if e["name"] in parts:
            out.append(e)
        else:
            out += find_groups(e.get("children") or [], parts)
    return out


def all_names(elements, acc):
    for e in elements:
        acc.add(e["name"]); all_names(e.get("children") or [], acc)
    return acc


def shift(e, dz):
    for key in ("from", "to", "rotationOrigin"):
        if e.get(key) is not None:
            e[key] = [e[key][0], e[key][1], round(e[key][2] + dz, 5)]


def clean(elements):
    for e in elements:
        if e.get("stepParentName", None) == "":
            del e["stepParentName"]
        for f in (e.get("faces") or {}).values():
            if isinstance(f, dict) and f.get("texture"):
                f["texture"] = "#material"
        clean(e.get("children") or [])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src"); ap.add_argument("dst"); ap.add_argument("seraph")
    ap.add_argument("--texture", required=True); ap.add_argument("--stray", type=float, default=3.0)
    a = ap.parse_args()
    shape = json.load(open(a.src, encoding="utf-8-sig"))
    body = json.load(open(a.seraph, encoding="utf-8-sig"))
    parts = {}
    def walk(es):
        for e in es:
            parts[e["name"]] = e; walk(e.get("children") or [])
    walk(body["elements"])
    groups = {g["name"]: g for g in find_groups(shape["elements"], parts)}
    # Strays: right-side pieces filed in the left group.
    for left, right in SIDES.items():
        if left not in groups or right not in groups:
            continue
        gl, gr = groups[left], groups[right]
        width = parts[left]["to"][2] - parts[left]["from"][2]
        dz = gl["from"][2] - gr["from"][2]
        keep, moved = [], []
        for c in gl.get("children") or []:
            mid = (c["from"][2] + c["to"][2]) / 2
            (moved if mid < -a.stray or mid > width + a.stray + abs(dz) else keep).append(c)
        for c in moved:
            shift(c, dz)
        gl["children"] = keep
        gr["children"] = (gr.get("children") or []) + moved
        if moved:
            print(f"{len(moved)} pieces moved from {left} to {right} (z {dz:+g})")
    tops = []
    for name, g in groups.items():
        top = {"name": "GB_" + name, "from": [0, 0, 0], "to": [0, 0, 0], "rotationOrigin": [0, 0, 0],
               "stepParentName": name, "children": g.get("children") or []}
        tops.append(top)
        print(f"{top['name']}: {len(top['children'])} pieces on {name}")
    clean(tops)
    clash = all_names(tops, set()) & set(parts)
    if clash:
        raise SystemExit(f"armor element names clash with body parts (the body's animations would move them): {clash}")
    out = {"editor": shape.get("editor", {}), "textureWidth": shape.get("textureWidth", 64),
           "textureHeight": shape.get("textureHeight", 64),
           "textureSizes": {"material": [shape.get("textureWidth", 64), shape.get("textureHeight", 64)]},
           "textures": {"material": a.texture}, "elements": tops}
    if shape.get("animations"):
        out["animations"] = shape["animations"]
    json.dump(out, open(a.dst, "w", encoding="utf-8"), indent="\t")


if __name__ == "__main__":
    main()
