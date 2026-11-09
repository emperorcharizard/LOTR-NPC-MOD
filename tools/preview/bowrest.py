"""Solves the archers' rest bow (0.0.59): held in the right hand with the arm hanging (idle1), grip in the hand, stave
upright and tipped forward by TILT degrees, string toward the back, then checks it against the legs and torso over the
idle, walk and sprint frames. Usage: python3 -I bowrest.py <tools/preview> <seraph-faceless.json> <orc_bow.json> [tilt] [upright|flat|trail] [yaw: back end out, trail only]"""
import json, math, sys
import numpy as np
sys.path.insert(0, sys.argv[1])
from vsrender import load, boxes, anim_pose, local, T, S, RXYZ, D

body = load(sys.argv[2]); bow = load(sys.argv[3]); TILT = float(sys.argv[4]) if len(sys.argv) > 4 else 12
import os
SCALE = float(os.environ.get('BOW_SCALE', 0.6))
by = {a['code']: a for a in body['animations']}
# grip centre (model px) and which way the string lies from the stave (+1 = +Y, -1 = -Y). 0.0.55's stand-in bow:
# grip 0,0,8 with the string on +Y; the owner's orc_bow (0.0.71): BOW_GRIP=2.6,0.9,8 BOW_STRING=-1.
GRIP = np.array([float(v) for v in os.environ.get('BOW_GRIP', '0,0,8').split(',')]) / 16
STAVE, STRING = np.array([1.0, 0, 0]), np.array([0, float(os.environ.get('BOW_STRING', 1)), 0])

def state(code, fr):
    poses = anim_pose(by.get(code), fr) if code != 'rest' else {}
    out, aps, names = [], {}, []
    boxes(body['elements'], np.eye(4), poses, out, aps, lambda e, t: names.append(e['name']) or (0, 0, 0))
    return [(m, s, n) for (m, s, _), n in zip(out, names)], aps

def hand_pos(aps, code):
    m, ap = aps[code]
    return (m @ np.array([float(ap['posX']) / 16, float(ap['posY']) / 16, float(ap['posZ']) / 16, 1]))[:3]

# Forward: where the left hand goes when the bow is raised (bowaim), flattened.
_, rest_aps = state('rest', 0)
_, aim_aps = state('bowaim', 15)
fwd = hand_pos(aim_aps, 'LeftHand') - hand_pos(rest_aps, 'LeftHand'); fwd[1] = 0; fwd /= np.linalg.norm(fwd)
up = np.array([0, 1.0, 0]); t = TILT * D
# out: away from the body on the bow hand's side (the right hand is on the -side of the body's centre line).
_, rest_aps0 = state('rest', 0)
side = hand_pos(rest_aps0, 'RightHand') - hand_pos(rest_aps0, 'LeftHand'); side[1] = 0; side -= fwd * (side @ fwd); out = side / np.linalg.norm(side)
STYLE = sys.argv[5] if len(sys.argv) > 5 else 'upright'
if STYLE == 'upright':      # 0.0.59: stave up along the leg (tipped forward), string toward the back
    stave_w = math.cos(t) * up + math.sin(t) * fwd
    string_w = -(math.cos(t) * fwd - math.sin(t) * up)
elif STYLE == 'flat':       # bow face outward: stave up (tipped forward), string toward the leg, the curve seen from the front
    stave_w = math.cos(t) * up + math.sin(t) * fwd
    string_w = -out
elif STYLE == 'trail':      # carried level at the side: stave pointing forward (tipped down by TILT), string up,
    # turned by YAW degrees so the back end swings out, clear of the leg (the front end comes in a little).
    yaw = (float(sys.argv[6]) if len(sys.argv) > 6 else 0) * D
    ahead = math.cos(yaw) * fwd - math.sin(yaw) * out
    stave_w = math.cos(t) * ahead - math.sin(t) * up
    string_w = math.cos(t) * up + math.sin(t) * ahead
string_w = string_w - stave_w * (string_w @ stave_w); string_w /= np.linalg.norm(string_w)
model_y = string_w * STRING[1]   # model +Y in the world: toward the string, or away from it for a -Y string
want = np.column_stack([stave_w, model_y, np.cross(stave_w, model_y)])   # model X, Y, Z -> world

_, idle_aps = state('idle1', 0)
M, ap = idle_aps['RightHand']
aprot = np.array([float(ap['rotationX']), float(ap['rotationY']), float(ap['rotationZ'])])
M3 = M[:3, :3] / np.cbrt(np.linalg.det(M[:3, :3]))
Rt = np.linalg.inv(M3) @ want
b = math.asin(max(-1, min(1, Rt[0, 2]))); c = math.atan2(-Rt[0, 1], Rt[0, 0]); a = math.atan2(-Rt[1, 2], Rt[2, 2])
rot = np.degrees([a, b, c]) - aprot
rot = (rot + 180) % 360 - 180
R = RXYZ(*((aprot + rot) * D))[:3, :3]
assert np.allclose(R, Rt, atol=1e-6), "euler decomposition"
tr = -(R @ GRIP)
tf = {"translation": dict(zip('xyz', [round(float(v), 4) for v in tr])), "rotation": dict(zip('xyz', [round(float(v), 1) for v in rot])),
      "origin": {"x": 0, "y": 0, "z": 0}, "scale": SCALE}
print(json.dumps(tf))

def item_matrix(apm, ap, tf):
    tr = tf['translation']; r = tf['rotation']; sc = tf['scale']
    return (apm @ S(sc, sc, sc) @ T(float(ap['posX'])/16 + tr['x'], float(ap['posY'])/16 + tr['y'], float(ap['posZ'])/16 + tr['z'])
            @ RXYZ((float(ap['rotationX']) + r['x']) * D, (float(ap['rotationY']) + r['y']) * D, (float(ap['rotationZ']) + r['z']) * D))

pts = []
def walk(es, parent):
    for e in es:
        m = parent @ local(e, {}); size = [(e['to'][i] - e['from'][i]) / 16 for i in range(3)]
        if 'grip' not in e['name']:
            for x in np.linspace(0, 1, 3):
                for y in np.linspace(0, 1, 2):
                    for z in np.linspace(0, 1, 2): pts.append(m @ np.array([x * size[0], y * size[1], z * size[2], 1]))
        walk(e.get('children') or [], m)
walk(bow['elements'], np.eye(4)); PTS = np.array(pts)
OBST = ('UpperTorso', 'LowerTorso', 'UpperFootL', 'LowerFootL', 'UpperFootR', 'LowerFootR')
worst, where, lowest = 0, None, 9
for code in ('rest', 'idle1', 'walk', 'sprint'):
    n = by[code].get('quantityframes', 1) if code != 'rest' else 1
    for fr in range(0, n, 3):
        els, aps = state(code, fr); apm, ap = aps['RightHand']
        wp = (item_matrix(apm, ap, tf) @ PTS.T).T
        lowest = min(lowest, wp[:, 1].min())
        for m, size, name in els:
            if name not in OBST: continue
            lp = (np.linalg.inv(m) @ wp.T).T[:, :3]
            inside = np.all((lp > 0) & (lp < np.array(size)), axis=1)
            if inside.any():
                d = np.minimum(lp, np.array(size) - lp).min(axis=1)[inside].max() * 16
                if d > worst: worst, where = d, f"{code}@{fr} {name}"
print(f"tilt {TILT}: deepest into legs/torso {worst:.2f} px at {where}; lowest point {lowest:.2f} blocks above the feet")
