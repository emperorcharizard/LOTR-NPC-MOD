"""Measure gaps between held items and the hands. usage: gaps.py <vsrender dir> <gen assets/lotrnpcs> <entity json>..."""
import json, sys, os, glob
sys.path.insert(0, sys.argv[1])
from vsrender import load, local, anim_pose, T, S, RXYZ, D
import numpy as np
G = sys.argv[2]
bodies = {n: load(f"{G}/shapes/entity/humanoid/{n}-faceless.json") for n in ("seraph", "dwarf", "elf")}
anims = {a['code']: a for a in bodies['seraph']['animations']}
own = {bn: {a["code"]: a for a in b["animations"]} for bn, b in bodies.items()}
ent_anims = {}
def mats(elements, parent, poses, out):
    for e in elements:
        m = parent @ local(e, poses.get(e['name'], {}))
        out[e['name']] = (m, e)
        mats(e.get('children') or [], m, poses, out)
def item_matrix(apm, ap, tf):
    o = tf.get('origin', {'x': 0, 'y': 0, 'z': 0}); o = (o['x'], o['y'], o['z']); tr = tf['translation']; rot = tf['rotation']; sc = tf['scale']
    return (apm @ T(*o) @ S(sc, sc, sc) @ T(float(ap['posX']) / 16 + tr['x'], float(ap['posY']) / 16 + tr['y'], float(ap['posZ']) / 16 + tr['z'])
            @ RXYZ((float(ap['rotationX']) + rot.get('x', 0)) * D, (float(ap['rotationY']) + rot.get('y', 0)) * D, (float(ap['rotationZ']) + rot.get('z', 0)) * D)
            @ T(-o[0], -o[1], -o[2]))
def item_points(shape, im):
    pts = []
    def walk(es, parent):
        for e in es:
            m = parent @ local(e, {})
            size = [(e['to'][i] - e['from'][i]) / 16 for i in range(3)]
            faces = e.get('faces') or {}
            if any(isinstance(f, dict) and f.get('texture') and f.get('enabled', True) is not False for f in faces.values()):
                n = [max(2, int(abs(s) * 16 / 0.25) + 1) for s in size]
                g = np.stack(np.meshgrid(*[np.linspace(0, size[i], n[i]) for i in range(3)], indexing='ij'), -1).reshape(-1, 3)
                g = np.hstack([g, np.ones((len(g), 1))])
                pts.append((m @ g.T).T[:, :3])
            walk(e.get('children') or [], m)
    walk(shape['elements'], im)
    return np.vstack(pts) if pts else np.zeros((0, 3))
def gap(points, arm_m, arm_e, hand_px=3.0):
    """Min distance in px from item points to the hand (bottom hand_px of the lower arm box)."""
    inv = np.linalg.inv(arm_m)
    p = (inv @ np.hstack([points, np.ones((len(points), 1))]).T).T[:, :3] * 16
    size = [(arm_e['to'][i] - arm_e['from'][i]) for i in range(3)]
    lo = np.array([0, 0, 0]); hi = np.array([size[0], hand_px, size[2]])
    d = np.maximum(np.maximum(lo - p, p - hi), 0)
    return float(np.sqrt((d ** 2).sum(1)).min())
def item_tf(code, hand):
    it = load(f"{G}/itemtypes/{code.split(':')[1]}.json")
    shape = it['shape']['base'].split(':')[1]
    tf = it.get('tpOffHandTransform') if hand == 'L' and it.get('tpOffHandTransform') else it['tpHandTransform']
    return load(f"{G}/shapes/{shape}.json"), tf
def anim_for(cfgcode):
    a = ent_anims.get(cfgcode)
    return (a['animation'], a) if a else (None, None)
seen = set(); rows = []
for ef in sys.argv[3:]:
    ent = load(ef)
    for a in ent['client'].get('animations', []): ent_anims[a['code']] = a
    hw = next((b for b in ent['client']['behaviors'] if b.get('code') == 'lotrnpcs:heldweapon'), None)
    if not hw: continue
    for w in hw['weapons']:
        if not w['item'].startswith('lotrnpcs:'): continue
        key = ('R', w['item'], w.get('holdAnimation'), w['attackAnimation'])
        if key in seen: continue
        seen.add(key)
        shape, tf = item_tf(w['item'], 'R')
        hold = anim_for(w.get('holdAnimation'))[0] if w.get('holdAnimation') else None
        atk, atkcfg = anim_for(w['attackAnimation'])
        states = [("hold" if hold else "idle", hold or 'idle1', 0)]
        if atk in anims:
            states.append(("strike", atk, None))
        for label, code, fr in states:
            if fr is None:
                # impact frame from gen: speed = impact/30/(hit/1000)
                fr = atkcfg['animationSpeed'] * w.get('hitMs', 400) / 1000 * 30
            for bn, body in bodies.items():
                poses = anim_pose(anims.get(code), fr)
                ms = {}; mats(body['elements'], np.eye(4), poses, ms)
                apm, ap = next(((m, a) for m, e in ms.values() for a in (e.get('attachmentpoints') or []) if a['code'] == 'RightHand'), (None, None))
                pts = item_points(shape, item_matrix(apm, ap, tf))
                gR = gap(pts, *ms['LowerArmR'])
                gL = gap(pts, *ms['LowerArmL']) if w.get('twoHanded') and label != 'strike' or (w.get('twoHanded') and code == 'poleaxechop') else None
                rows.append((w['item'].split('-', 1)[1], label, f"{code}@{fr:.1f}", bn, gR, gL))
    for s in hw.get('shields') or []:
        if ('L', s) in seen: continue
        seen.add(('L', s))
        shape, tf = item_tf(s, 'L')
        for bn, body in bodies.items():
            ms = {}; mats(body['elements'], np.eye(4), {}, ms)
            apm, ap = next(((m, a) for m, e in ms.values() for a in (e.get('attachmentpoints') or []) if a['code'] == 'LeftHand'))
            pts = item_points(shape, item_matrix(apm, ap, tf))
            rows.append((s.split('-', 1)[1], 'shield', 'rest', bn, None, gap(pts, *ms['LowerArmL'])))
for r in rows:
    print(f"{r[0]:16} {r[1]:7} {r[2]:18} {r[3]:7} R={'-' if r[4] is None else f'{r[4]:.2f}'} L={'-' if r[5] is None else f'{r[5]:.2f}'}")
