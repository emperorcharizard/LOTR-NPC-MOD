"""Offline sheet of every lotrweapons item's inventory icon (flat colours: shape and angle only), next to the
vanilla falx and round shield they are aligned to. Centred on each icon's origin, which is how vanilla's icons read.

    python tools/preview/lotrweapons_icons.py <lotrweapons out dir> <vanilla falx shape> <vanilla round shield shape> <out.png>
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import vsrender as V

out_dir, falx, round_shield, dest = sys.argv[1:5]
A = os.path.join(out_dir, "assets", "lotrweapons")


def tmat(t):
    o, r, s = t["origin"], t["rotation"], t["scale"]
    O = V.T(o["x"], o["y"], o["z"])
    return O @ V.S(s, s, s) @ V.RXYZ(r.get("x", 0) * V.D, r.get("y", 0) * V.D, r.get("z", 0) * V.D) @ np.linalg.inv(O)


def colour(e, tex):
    h = abs(hash(e.get("name", ""))) % 200
    return (80 + h % 120, 90 + (h * 7) % 100, 110 + (h * 13) % 120)


items = [("vanilla falx", V.load(falx), {"rotation": {"x": -53, "y": 118, "z": -5}, "origin": {"x": .65, "y": .1, "z": .37}, "scale": 1.65}),
         ("vanilla round shield", V.load(round_shield), {"rotation": {"x": 34, "y": 32, "z": -163}, "origin": {"x": .21, "y": -.21, "z": .49}, "scale": 1.8})]
for f in sorted(os.listdir(os.path.join(A, "itemtypes"))):
    it = json.load(open(os.path.join(A, "itemtypes", f)))
    base = (it.get("shape") or it["shapeByType"]["*"])["base"].split(":")[1]
    items.append((it["code"], json.load(open(os.path.join(A, "shapes", base + ".json"))), it["guiTransform"]))
cell, cols = 150, 7
rows = (len(items) + cols - 1) // cols
sheet = Image.new("RGB", (cols * cell, rows * (cell + 16)), (40, 40, 40))
draw = ImageDraw.Draw(sheet)
tmp = dest + ".cell.png"
for i, (name, shape, t) in enumerate(items):
    boxes = []
    V.boxes(shape["elements"], tmat(t), {}, boxes, {}, colour)
    o = t["origin"]
    V.render(boxes, "side", tmp, scale=cell * 0.4, W=cell, H=cell, center=(o["x"], o["y"], o["z"]))
    x, y = (i % cols) * cell, (i // cols) * (cell + 16)
    sheet.paste(Image.open(tmp).convert("RGB"), (x, y))
    draw.rectangle([x, y, x + cell - 1, y + cell - 1], outline=(90, 90, 90))
    draw.text((x + 3, y + cell + 2), name[:24], fill=(220, 220, 220))
os.remove(tmp)
sheet.save(dest)
print(dest)
