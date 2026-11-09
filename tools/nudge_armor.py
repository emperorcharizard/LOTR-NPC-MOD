"""Moves armor pieces (or single elements inside them) by a few voxels in their body part's frame, for fitting step
parented armor by hand after fix_stepparent_armor.py (made gundabad-bone-armor 0.1.3 from 0.1.1). Also drops empty
stepParentName keys from child elements (each one logged a "no such element" warning in game). Every other file is
copied byte for byte; modinfo's version goes to --version.
Usage:
  python3 -I nudge_armor.py <mod.zip> <out.zip> [--version 0.1.3] --move <Part>[/<element>]=dx,dy,dz ...
<Part> is a body part named by a top-level piece's stepParentName (moves the whole piece); <Part>/<element> moves
every element of that name inside that piece (with its children). Deltas are in voxels in the part's own frame."""
import argparse, json, zipfile


def shift(e, d):
    for key in ("from", "to", "rotationOrigin"):
        if e.get(key) is not None:
            v = [round(e[key][i] + d[i], 4) for i in range(3)]
            e[key] = [0.0 if x == 0 else x for x in v]


def strip_empty_parents(elements):
    n = 0
    for e in elements:
        if e.get("stepParentName", None) == "":
            del e["stepParentName"]; n += 1
        n += strip_empty_parents(e.get("children") or [])
    return n


def apply(shape, moves, used):
    for e in shape["elements"]:
        part = e.get("stepParentName")
        for (p, name), d in moves.items():
            if p != part:
                continue
            if name is None:
                shift(e, d); used.add((p, name))
            else:
                def walk(es):
                    for c in es:
                        if c["name"] == name:
                            shift(c, d); used.add((p, name))
                        else:
                            walk(c.get("children") or [])
                walk(e.get("children") or [])
    return sum(strip_empty_parents(e.get("children") or []) for e in shape["elements"])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src"); ap.add_argument("dst"); ap.add_argument("--version")
    ap.add_argument("--move", action="append", default=[])
    a = ap.parse_args()
    moves = {}
    for m in a.move:
        key, val = m.split("=")
        p, _, name = key.partition("/")
        moves[(p, name or None)] = [float(x) for x in val.split(",")]
    used = set()
    with zipfile.ZipFile(a.src) as zin, zipfile.ZipFile(a.dst, "w", zipfile.ZIP_DEFLATED) as zout:
        for info in zin.infolist():
            data = zin.read(info.filename); name = info.filename
            if name.endswith(".json") and "/shapes/" in name:
                shape = json.loads(data.decode("utf-8-sig"))
                before = json.dumps(shape)
                stripped = apply(shape, moves, used)
                if json.dumps(shape) != before:
                    print(f"{name}: moved, {stripped} empty stepParentName removed")
                    data = json.dumps(shape, indent="\t").encode("utf-8")
            elif name == "modinfo.json" and a.version:
                j = json.loads(data.decode("utf-8-sig")); j["version"] = a.version
                data = json.dumps(j, indent=2).encode("utf-8")
            zout.writestr(info, data)
    missing = set(moves) - used
    if missing:
        raise SystemExit(f"nothing matched: {sorted(missing, key=str)}")


if __name__ == "__main__":
    main()
