import json, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vsrender import *
from PIL import Image, ImageDraw
body = load(sys.argv[1]); weapon = load(sys.argv[2]); out = sys.argv[3]; tf = json.loads(sys.argv[4]); specs = sys.argv[5].split(',')
view = sys.argv[6] if len(sys.argv) > 6 else 'side'
edge = set(sys.argv[7].split('|')) if len(sys.argv) > 7 else set()
by = {a['code']: a for a in body['animations']}
def wcolor(e, tex):
    if e['name'] in edge: return (230, 30, 30)
    t = (tex or '').lower()
    if 'rust' in t: return (190, 110, 60)
    if 'iron' in t or 'metal' in t: return (165, 170, 185)
    if 'plank' in t or 'handle' in t or 'wood' in t: return (130, 85, 45)
    if 'leather' in t: return (90, 55, 30)
    if 'cloth' in t: return (40, 40, 45)
    if 'pelt' in t: return (180, 150, 110)
    return (200, 180, 130)
def bcolor(e, tex):
    n = e['name']
    if 'ArmR' in n: return (90, 150, 220)
    if 'ArmL' in n: return (90, 200, 120)
    if 'Head' in n: return (230, 200, 160)
    return (200, 190, 180)
tiles = []
for spec in specs:
    code, fr = spec.split('@')
    poses = anim_pose(by.get(code), float(fr)) if code != 'rest' else {}
    o_, aps = [], {}
    boxes(body['elements'], np.eye(4), poses, o_, aps, bcolor)
    m, ap = aps['RightHand']
    o = tf.get('origin', {}); o = (o.get('x', 0), o.get('y', 0), o.get('z', 0)); tr = tf.get('translation', {}); rot = tf.get('rotation', {}); sc = tf.get('scale', 1)
    item = (m @ T(*o) @ S(sc, sc, sc) @ T(float(ap['posX'])/16 + tr.get('x', 0), float(ap['posY'])/16 + tr.get('y', 0), float(ap['posZ'])/16 + tr.get('z', 0))
            @ RXYZ((float(ap['rotationX']) + rot.get('x', 0)) * D, (float(ap['rotationY']) + rot.get('y', 0)) * D, (float(ap['rotationZ']) + rot.get('z', 0)) * D) @ T(-o[0], -o[1], -o[2]))
    w = []; boxes(weapon['elements'], item, {}, w, {}, wcolor)
    p = f'{out}-{code}{fr}.png'
    render(o_ + w, view, p, scale=260, W=520, H=620, center=(0.5, 1.15, 0.5))
    im = Image.open(p); ImageDraw.Draw(im).text((60, 6), spec, fill=(200, 0, 0)); tiles.append(im)
sheet = Image.new('RGB', (sum(t.width for t in tiles), tiles[0].height), (255, 255, 255)); x = 0
for t in tiles: sheet.paste(t, (x, 0)); x += t.width
sheet.save(out + '-zoom.png'); print(out + '-zoom.png')
