"""Hangs armor shapes on the seraph body the way the game does (each top-level piece becomes a child of the body part
its stepParentName names, so its coordinates are relative to that part) and renders front and side views in a pose.
Usage: python3 -I armorpreview.py <tools/preview> <seraph.json> <out.png> <label> <pose e.g. rest@0> <armor.json>..."""
import copy, json, sys
sys.path.insert(0, sys.argv[1])
from vsrender import load, boxes, anim_pose, render
import numpy as np
from PIL import Image, ImageDraw
body = load(sys.argv[2]); out = sys.argv[3]; label = sys.argv[4]; code, fr = sys.argv[5].split('@'); armors = sys.argv[6:]
tree = copy.deepcopy(body['elements'])
index = {}
def walk(es):
    for e in es: index[e['name']] = e; walk(e.get('children') or [])
walk(tree)
ARMOR = set()
for path in armors:
    for e in load(path)['elements']:
        p = e.get('stepParentName')
        if p in index:
            index[p].setdefault('children', []).append(e)
            def mark(x):
                ARMOR.add(id(x)); [mark(c) for c in x.get('children') or []]
            mark(e)
names = {}
def collect(es):
    for e in es: names[e['name']] = names.get(e['name'], 0) + 1; collect(e.get('children') or [])
by = {a['code']: a for a in body['animations']}
poses = anim_pose(by.get(code), float(fr)) if code != 'rest' else {}
def color(e, tex):
    if id(e) in ARMOR: return (200, 120, 60)
    n = e['name']
    if 'Head' in n: return (230, 200, 160)
    return (190, 190, 200)
# boxes() passes the element dict to the colour callback, so armor is told apart by identity.
o, aps = [], {}
boxes(tree, np.eye(4), poses, o, aps, color)
tiles = []
for view in ('front', 'side'):
    p = f"{out}-{view}.png"; render(o, view, p, scale=200, W=420, H=560, center=(0.5, 1.0, 0.5), zbuffer=True); tiles.append(Image.open(p))
sheet = Image.new('RGB', (sum(t.width for t in tiles), tiles[0].height + 30), 'white'); x = 0
for t in tiles: sheet.paste(t, (x, 30)); x += t.width
ImageDraw.Draw(sheet).text((8, 8), label, fill=(200, 0, 0)); sheet.save(out); print(out)
