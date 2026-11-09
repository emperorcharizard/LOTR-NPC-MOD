"""Measures how deep the Mordor shield sinks into the (armor-inflated) body over the idle, walk, sprint and attack poses."""
import json, sys, math
import numpy as np
sys.path.insert(0, sys.argv[1])            # tools/preview
from vsrender import load, boxes, anim_pose, T, S, RXYZ, D
body = load(sys.argv[2]); shield = load(sys.argv[3])
by = {a['code']: a for a in body['animations']}

def frames(code, step):
    a = by[code]; n = a.get('quantityframes', 1)
    return [(code, f) for f in range(0, n, step)]
POSES = [('rest', 0)] + frames('idle1', 10) + frames('walk', 4) + frames('sprint', 4) + frames('falx', 4) + frames('axehit', 4) + frames('hit', 4)

OBST = ('UpperTorso', 'LowerTorso', 'UpperFootL', 'LowerFootL', 'UpperFootR', 'LowerFootR', 'LowerArmL', 'UpperArmL')
def body_state(code, fr):
    poses = anim_pose(by.get(code), fr) if code != 'rest' else {}
    out, aps = [], {}
    names = []
    def cb(e, tex): names.append(e['name']); return (0, 0, 0)
    boxes(body['elements'], np.eye(4), poses, out, aps, cb)
    obs = [(m, size, n) for (m, size, _), n in zip(out, names) if n in OBST]
    return obs, aps['LeftHand']
STATES = [body_state(c, f) for c, f in POSES]

# Sample points on every shield box (in shield model space, blocks), skipping the grip bar the hand holds.
def shield_points():
    pts = []
    def walk(es, parent):
        from vsrender import local
        for e in es:
            m = parent @ local(e, {})
            size = [(e['to'][i] - e['from'][i]) / 16 for i in range(3)]
            grip = 'grip' in e['name'].lower() or 'handle' in e['name'].lower()
            if not grip:
                for a in np.linspace(0, 1, 3):
                    for b in np.linspace(0, 1, 3):
                        for c in np.linspace(0, 1, 2):
                            pts.append((m @ np.array([a * size[0], b * size[1], c * size[2], 1.0]))[:4])
            walk(e.get('children') or [], m)
    walk(shield['elements'], np.eye(4))
    return np.array(pts)
PTS = shield_points()

def item_matrix(ap_m, ap, tf):
    o = tf['origin']; o = (o['x'], o['y'], o['z']); tr = tf['translation']; rot = tf['rotation']; sc = tf['scale']
    return (ap_m @ T(*o) @ S(sc, sc, sc) @ T(float(ap['posX'])/16 + tr['x'], float(ap['posY'])/16 + tr['y'], float(ap['posZ'])/16 + tr['z'])
            @ RXYZ((float(ap['rotationX']) + rot['x']) * D, (float(ap['rotationY']) + rot['y']) * D, (float(ap['rotationZ']) + rot['z']) * D) @ T(-o[0], -o[1], -o[2]))

def depth(tf, margin_torso, margin_arm):
    """Per pose: deepest penetration (px) of any shield point into an inflated obstacle box."""
    res = []
    for (obs, (apm, ap)) in STATES:
        wp = (item_matrix(apm, ap, tf) @ PTS.T).T
        worst = 0.0
        for m, size, name in obs:
            mg = margin_arm if 'Arm' in name else margin_torso
            inv = np.linalg.inv(m)
            lp = (inv @ wp.T).T[:, :3]
            lo = -mg; hi = np.array(size) + mg
            inside = np.all((lp > lo) & (lp < hi), axis=1)
            if inside.any():
                d = np.minimum(lp - lo, hi - lp).min(axis=1)[inside].max() * 16
                worst = max(worst, d)
        res.append(worst)
    return res

def grip_world(tf, state=0):
    obs, (apm, ap) = STATES[state]
    return (item_matrix(apm, ap, tf) @ np.array([3.7/16, 0.75/16, 6/16, 1]))[:3]
