"""Hangs armor modelled in whole-body coordinates on the seraph body parts it names, keeping every piece exactly where
the modeller put it in the rest pose (0.0.64, the owner's gundabad-bone-armor 0.1.2).

fix_stepparent_armor.py (0.1.1) only subtracted each body part's rest position. The seraph's arms, legs and upper torso
are also rotated in the rest pose (upper arms 10 and 7 degrees, lower arms back by 8 and 6, and so on), and a step
parented piece turns with its part, so 0.1.1 hung the gauntlets and pauldrons about 1 px out to the side and tilted the
collar with the torso. This tool gives each top-level piece the inverse of its part's whole rest transform: from and
rotationOrigin at the piece's spot in the part's frame, plus rotationX/Y/Z undoing the part's turn. In the rest pose
the armor then sits exactly as modelled; in animations it follows its part as before.

It also drops empty stepParentName keys from child elements (the game logged a "no such element" warning for each one,
3,456 lines in one short test). Every other file is copied byte for byte; modinfo's version goes to --version.
Usage:
  python3 -I realign_stepparent_armor.py <whole-body mod.zip> <out.zip> <seraph shape .json> [--version 0.1.2]
         [--frame 8.5,0,8]
--frame is where the modeller's origin sits on the body (0.1.0 was modelled centred on x 8.5, z 8, feet at y 0)."""
import argparse, json, math, sys, zipfile
import numpy as np

D = math.pi / 180


def T(x, y, z):
    m = np.eye(4); m[:3, 3] = (x, y, z); return m


def R(rx, ry, rz):  # vsapi Mat4f.RotateXYZ order: Rx * Ry * Rz
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    X = np.array([[1, 0, 0, 0], [0, cx, -sx, 0], [0, sx, cx, 0], [0, 0, 0, 1]])
    Y = np.array([[cy, 0, sy, 0], [0, 1, 0, 0], [-sy, 0, cy, 0], [0, 0, 0, 1]])
    Z = np.array([[cz, -sz, 0, 0], [sz, cz, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]])
    return X @ Y @ Z


def local(e):
    """vsapi ShapeElement.GetLocalTransformMatrix, in voxels: T(origin) * Rxyz * T(from - origin)."""
    o = e.get("rotationOrigin") or [0, 0, 0]
    f = e["from"]
    return T(*o) @ R((e.get("rotationX") or 0) * D, (e.get("rotationY") or 0) * D, (e.get("rotationZ") or 0) * D) \
        @ T(f[0] - o[0], f[1] - o[1], f[2] - o[2])


def part_matrices(body):
    out = {}
    def walk(elements, parent):
        for e in elements:
            m = parent @ local(e)
            out[e["name"]] = m
            walk(e.get("children") or [], m)
    walk(body["elements"], np.eye(4))
    return out


def euler_xyz(m):
    """Angles (degrees) with Rx*Ry*Rz equal to the rotation part of m."""
    ry = math.asin(max(-1.0, min(1.0, m[0, 2])))
    rx = math.atan2(-m[1, 2], m[2, 2])
    rz = math.atan2(-m[0, 1], m[0, 0])
    return [rx / D, ry / D, rz / D]


def r4(v):
    v = round(v, 4)
    return 0.0 if v == 0 else v


def strip_empty_parents(elements):
    n = 0
    for e in elements:
        if e.get("stepParentName", None) == "":
            del e["stepParentName"]; n += 1
        n += strip_empty_parents(e.get("children") or [])
    return n


def realign(shape, parts, frame):
    moved = 0
    for e in shape["elements"]:
        parent = e.get("stepParentName")
        if not parent:
            continue
        if parent not in parts:
            raise SystemExit(f"{e['name']}: body part {parent} is not on the seraph body")
        if any(e.get(k) for k in ("rotationX", "rotationY", "rotationZ")):
            raise SystemExit(f"{e['name']}: top-level piece is already rotated; this tool expects unrotated pieces")
        size = [e["to"][i] - e["from"][i] for i in range(3)]
        # Where the modeller put the piece, in body coordinates.
        want = local(e) @ np.eye(4)
        want = T(*frame) @ want
        need = np.linalg.inv(parts[parent]) @ want
        f = [r4(need[i, 3]) for i in range(3)]
        rx, ry, rz = (r4(a) for a in euler_xyz(need))
        e["from"] = f
        e["to"] = [r4(f[i] + size[i]) for i in range(3)]
        e["rotationOrigin"] = list(f)
        for key, val in (("rotationX", rx), ("rotationY", ry), ("rotationZ", rz)):
            if val: e[key] = val
            else: e.pop(key, None)
        # Children stay relative to the piece's from, so only from/to/rotationOrigin/rotations above change.
        moved += 1
    stripped = 0
    for e in shape["elements"]:
        stripped += strip_empty_parents(e.get("children") or [])
    return moved, stripped


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("src"); ap.add_argument("dst"); ap.add_argument("seraph")
    ap.add_argument("--version", default=None); ap.add_argument("--frame", default="8.5,0,8")
    a = ap.parse_args()
    frame = [float(x) for x in a.frame.split(",")]
    parts = part_matrices(json.load(open(a.seraph, encoding="utf-8-sig")))
    with zipfile.ZipFile(a.src) as zin, zipfile.ZipFile(a.dst, "w", zipfile.ZIP_DEFLATED) as zout:
        for info in zin.infolist():
            data = zin.read(info.filename)
            name = info.filename
            if name.endswith(".json") and "/shapes/" in name:
                shape = json.loads(data.decode("utf-8-sig"))
                moved, stripped = realign(shape, parts, frame)
                if moved or stripped:
                    print(f"{name}: {moved} pieces hung on their body parts, {stripped} empty stepParentName removed")
                    data = json.dumps(shape, indent="\t").encode("utf-8")
            elif name == "modinfo.json" and a.version:
                info_json = json.loads(data.decode("utf-8-sig"))
                print(f"modinfo version {info_json.get('version')} -> {a.version}")
                info_json["version"] = a.version
                data = json.dumps(info_json, indent=2).encode("utf-8")
            zout.writestr(info, data)


if __name__ == "__main__":
    main()
