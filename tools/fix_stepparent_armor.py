"""Fixes armor shapes whose step-parented pieces were modelled in whole-body coordinates (0.0.63, the owner's
gundabad-bone-armor 0.1.0). The game hangs each top-level piece on the body part it names (stepParentName), so its
from, to and rotationOrigin must be relative to that part. This shifts them: += FRAME minus the part's absolute rest
position on the seraph body. Children are untouched. Every other file is copied byte for byte; modinfo's version goes
to --version. Usage:
  python3 -I fix_stepparent_armor.py <mod.zip> <out.zip> <seraph shape .json> [--version 0.1.1] [--frame 8.5,0,8]"""
import json, sys, zipfile, argparse


def absolute_froms(body):
    out = {}
    def walk(elements, offset):
        for e in elements:
            here = [offset[i] + e["from"][i] for i in range(3)]
            out[e["name"]] = here
            walk(e.get("children") or [], here)
    walk(body["elements"], [0, 0, 0])
    return out


def fix_shape(shape, parts, frame):
    """Returns the number of pieces moved. Raises if a piece names a body part the seraph lacks."""
    moved = 0
    for e in shape["elements"]:
        parent = e.get("stepParentName")
        if not parent:
            continue
        if parent not in parts:
            raise SystemExit(f"{e['name']}: body part {parent} is not on the seraph body")
        delta = [frame[i] - parts[parent][i] for i in range(3)]
        for key in ("from", "to", "rotationOrigin"):
            if key in e and e[key] is not None:
                e[key] = [round(e[key][i] + delta[i], 4) for i in range(3)]
        moved += 1
    return moved


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src"); ap.add_argument("dst"); ap.add_argument("seraph")
    ap.add_argument("--version", default=None); ap.add_argument("--frame", default="8.5,0,8")
    a = ap.parse_args()
    frame = [float(x) for x in a.frame.split(",")]
    parts = absolute_froms(json.load(open(a.seraph, encoding="utf-8-sig")))
    with zipfile.ZipFile(a.src) as zin, zipfile.ZipFile(a.dst, "w", zipfile.ZIP_DEFLATED) as zout:
        for info in zin.infolist():
            data = zin.read(info.filename)
            name = info.filename
            if name.endswith(".json") and "/shapes/" in name:
                shape = json.loads(data.decode("utf-8-sig"))
                n = fix_shape(shape, parts, frame)
                if n:
                    print(f"{name}: {n} pieces moved onto their body parts")
                    data = json.dumps(shape, indent="\t").encode("utf-8")
            elif name == "modinfo.json" and a.version:
                info_json = json.loads(data.decode("utf-8-sig"))
                print(f"modinfo version {info_json.get('version')} -> {a.version}")
                info_json["version"] = a.version
                data = json.dumps(info_json, indent=2).encode("utf-8")
            zout.writestr(info, data)


if __name__ == "__main__":
    main()
