"""Blockbench project (.bbmodel, Vintage Story format) -> one Vintage Story shape JSON and one texture PNG.

0.0.76 (user, 2026-11-04: "read block bench files and their textures and put that in vintage story as a single file and
single texture"). Usage:

    python3 -I bbmodel_to_vs.py <in.bbmodel> <out.json> [<out.png>] [--untextured V:TEXTURE,...,else:TEXTURE]

- Groups become zero-size parent elements at the group's origin, carrying its rotation; their children's from, to and
  rotationOrigin are made relative to the parent's from (the game's rule for nested elements). Blockbench rotates a
  group's children about the group origin and each cube about its own origin, which is exactly what that nesting does.
- Zero-size cubes (Blockbench's "_geo" placeholders) are dropped.
- Embedded textures are decoded. One texture is written as it is. Several are packed side by side onto one sheet, scaled
  to the densest pixels-per-UV-unit, and every face's UV is moved to its texture's place. A face whose UV runs past its
  texture is an error (it would need tiling).
- --untextured: for a project saved without textures (the boar spear). Faces get a texture path by their UV's top edge:
  "2:game:item/tool/handle,else:game:block/metal/ingot/iron" gives faces with v1 < 2 the handle texture and the rest
  iron. No PNG is written then; the model names those textures.
"""
import base64, io, json, sys

from PIL import Image


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def sub(a, b):
    return [round(x - y, 5) for x, y in zip(a, b)]


def convert(project, untextured=None):
    elements = {e["uuid"]: e for e in project["elements"]}
    groups = {g["uuid"]: g for g in project.get("groups", [])}
    textures = project.get("textures") or []
    res = project.get("resolution", {"width": 16, "height": 16})

    # ---- texture sheet
    sheet, placement, uv_size = None, {}, (res["width"], res["height"])
    images = []
    for i, t in enumerate(textures):
        src = t.get("source", "")
        if not src.startswith("data:image/png;base64,"):
            raise SystemExit(f"texture {t.get('name')} is not embedded in the project")
        img = Image.open(io.BytesIO(base64.b64decode(src.split(",", 1)[1]))).convert("RGBA")
        uvw, uvh = t.get("uv_width", res["width"]), t.get("uv_height", res["height"])
        images.append((i, t, img, uvw, uvh))
    if images:
        density = max(img.width / uvw for _, _, img, uvw, _ in images)
        x_uv, height_uv = 0.0, 0.0
        scaled = []
        for i, t, img, uvw, uvh in images:
            w, h = round(uvw * density), round(uvh * density)
            if (w, h) != img.size:
                img = img.resize((w, h), Image.NEAREST)
            scaled.append((i, img, x_uv, uvw, uvh))
            placement[i] = (x_uv, uvw, uvh)
            x_uv += uvw
            height_uv = max(height_uv, uvh)
        sheet = Image.new("RGBA", (round(x_uv * density), round(height_uv * density)), (0, 0, 0, 0))
        for i, img, xo, _, _ in scaled:
            sheet.paste(img, (round(xo * density), 0))
        uv_size = (x_uv, height_uv)

    def texture_index(face):
        tex = face.get("texture")
        if tex is None or tex is False:
            return None
        if isinstance(tex, int):
            return tex
        for i, t in enumerate(textures):
            if t.get("uuid") == tex or str(t.get("id")) == str(tex):
                return i
        return int(tex)

    used_paths = {}

    def faces_of(e):
        out = {}
        for side, f in (e.get("faces") or {}).items():
            uv = list(f.get("uv") or [0, 0, 0, 0])
            if untextured is not None:
                path = next((p for limit, p in untextured if limit is None or min(uv[1], uv[3]) < limit), None)
                key = used_paths.setdefault(path, f"t{len(used_paths)}")
                face = {"texture": "#" + key, "uv": uv}
            else:
                idx = texture_index(f)
                if idx is None:
                    continue  # no texture: Blockbench doesn't draw the face, nor will the game
                xo, uvw, uvh = placement[idx]
                if max(uv[0], uv[2]) > uvw + 1e-6 or max(uv[1], uv[3]) > uvh + 1e-6 or min(uv) < -1e-6:
                    raise SystemExit(f"{e['name']} {side}: UV {uv} runs past its texture ({uvw} x {uvh})")
                face = {"texture": "#atlas", "uv": [uv[0] + xo, uv[1], uv[2] + xo, uv[3]]}
            if f.get("rotation"):
                face["rotation"] = f["rotation"]
            out[side] = face
        return out

    def rot_fields(rotation):
        rx, ry, rz = (rotation or [0, 0, 0])
        return {k: v for k, v in (("rotationX", rx), ("rotationY", ry), ("rotationZ", rz)) if v}

    def cube(e, offset):
        size = [e["to"][i] - e["from"][i] for i in range(3)]
        if any(abs(s) < 1e-6 for s in size):
            return None
        out = {"name": e["name"], "from": sub(e["from"], offset), "to": sub(e["to"], offset),
               "rotationOrigin": sub(e.get("origin", e["from"]), offset)}
        out.update(rot_fields(e.get("rotation")))
        out["faces"] = faces_of(e)
        return out

    def node(entry, offset):
        if isinstance(entry, str):
            e = elements.get(entry)
            if e is None or e.get("export") is False or e.get("type", "cube") != "cube":
                return None
            return cube(e, offset)
        g = groups[entry["uuid"]]
        if g.get("export") is False:
            return None
        origin = g.get("origin", [0, 0, 0])
        kids = [k for k in (node(c, origin) for c in entry.get("children", [])) if k is not None]
        if not kids:
            return None
        out = {"name": g["name"], "from": sub(origin, offset), "to": sub(origin, offset),
               "rotationOrigin": sub(origin, offset)}
        out.update(rot_fields(g.get("rotation")))
        out["children"] = kids
        return out

    top = [k for k in (node(c, [0, 0, 0]) for c in project.get("outliner", [])) if k is not None]
    whole = lambda v: int(v) if float(v).is_integer() else v
    shape = {"textureWidth": whole(uv_size[0]), "textureHeight": whole(uv_size[1])}
    if untextured is not None:
        shape["textureWidth"], shape["textureHeight"] = res["width"], res["height"]
        shape["textures"] = {key: path for path, key in used_paths.items()}
    else:
        shape["textures"] = {"atlas": "atlas"}
    shape["elements"] = top
    return shape, sheet


def parse_untextured(arg):
    rules = []
    for part in arg.split(","):
        limit, path = part.split(":", 1)
        rules.append((None if limit == "else" else float(limit), path))
    return rules


def main(argv):
    args = [a for a in argv if not a.startswith("--")]
    untextured = None
    if "--untextured" in argv:
        untextured = parse_untextured(argv[argv.index("--untextured") + 1])
        args = [a for a in args if a != argv[argv.index("--untextured") + 1]]
    project = load(args[0])
    shape, sheet = convert(project, untextured)
    with open(args[1], "w", encoding="utf-8") as f:
        json.dump(shape, f, indent=1)
    if sheet is not None:
        sheet.save(args[2], optimize=True)
    n = sum(1 for _ in walk(shape["elements"]))
    print(f"{args[1]}: {n} elements, texture {shape['textureWidth']} x {shape['textureHeight']} UV"
          + (f", sheet {sheet.width} x {sheet.height} px" if sheet is not None else ""))


def walk(elements):
    for e in elements:
        yield e
        yield from walk(e.get("children") or [])


if __name__ == "__main__":
    main(sys.argv[1:])
