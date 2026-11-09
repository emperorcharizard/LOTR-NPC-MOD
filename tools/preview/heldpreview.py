"""Side and front views of the body holding items in either hand, in a few poses (0.0.94: the new shields and spears).
usage: python3 -I heldpreview.py <body.json> <out.png> <poses, e.g. rest@0,walk@8> <spec.json>
spec.json: [{"label": "...", "items": [{"hand": "RightHand"|"LeftHand", "model": "<shape.json>", "tf": {...}}]}, ...]
Body in grey, left arm green, right arm blue, items tan. One row per spec, one column per pose and view."""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vsrender import *
from PIL import Image, ImageDraw

body = load(sys.argv[1]); out = sys.argv[2]; specs_poses = sys.argv[3].split(','); rows = json.load(open(sys.argv[4]))
by = {a['code']: a for a in body['animations']}


def bcolor(e, tex):
    n = e['name']
    if 'ArmR' in n: return (90, 150, 220)
    if 'ArmL' in n: return (90, 200, 120)
    if 'Head' in n: return (230, 200, 160)
    return (200, 190, 180)


def item_boxes(shape, apm, ap, tf, color):
    o = tf.get('origin', {'x': 0, 'y': 0, 'z': 0}); o = (o['x'], o['y'], o['z']); tr = tf['translation']; rot = tf['rotation']; sc = tf['scale']
    im = (apm @ T(*o) @ S(sc, sc, sc) @ T(float(ap['posX']) / 16 + tr['x'], float(ap['posY']) / 16 + tr['y'], float(ap['posZ']) / 16 + tr['z'])
          @ RXYZ((float(ap['rotationX']) + rot.get('x', 0)) * D, (float(ap['rotationY']) + rot.get('y', 0)) * D, (float(ap['rotationZ']) + rot.get('z', 0)) * D)
          @ T(-o[0], -o[1], -o[2]))
    res = []
    boxes(shape['elements'], im, {}, res, {}, lambda e, t: color)
    return res


tiles = []
for row in rows:
    line = []
    for spec in specs_poses:
        code, fr = spec.split('@')
        poses = anim_pose(by.get(code), float(fr)) if code != 'rest' else {}
        bx, aps = [], {}
        boxes(body['elements'], np.eye(4), poses, bx, aps, bcolor)
        for it in row['items']:
            apm, ap = aps[it['hand']]
            bx += item_boxes(load(it['model']), apm, ap, it['tf'], (205, 175, 120) if it['hand'] == 'LeftHand' else (150, 110, 70))
        for view in ('side', 'front'):
            path = f"/tmp/heldpreview_{len(tiles)}_{len(line)}.png"
            render(bx, view, path, scale=150, W=420, H=480, center=(0.5, 1.05, 0.5), zbuffer=True)
            im = Image.open(path)
            ImageDraw.Draw(im).text((8, 8), f"{row['label']}  {spec}  {view}", fill=(0, 0, 0))
            line.append(im)
    tiles.append(line)
W = max(len(l) for l in tiles) * 420; H = len(tiles) * 480
sheet = Image.new('RGB', (W, H), (255, 255, 255))
for r, line in enumerate(tiles):
    for c, im in enumerate(line):
        sheet.paste(im, (c * 420, r * 480))
sheet.save(out, optimize=True)
print(out, sheet.size)
