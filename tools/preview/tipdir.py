import json, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vsrender import *
body = load(sys.argv[1]); weapon = load(sys.argv[2]); tf = json.loads(sys.argv[3]); anim = sys.argv[4]; frames = [float(f) for f in sys.argv[5].split(',')]
by = {a['code']: a for a in body['animations']}
for fr in frames:
    poses = anim_pose(by.get(anim), fr) if anim != 'rest' else {}
    out, aps = [], {}
    boxes(body['elements'], np.eye(4), poses, out, aps, lambda e, t: (0, 0, 0))
    m, ap = aps['RightHand']
    o = tf.get('origin', {'x': .5, 'y': .5, 'z': .5}); o = (o.get('x', 0), o.get('y', 0), o.get('z', 0)); tr = tf.get('translation', {}); rot = tf.get('rotation', {}); sc = tf.get('scale', 1)
    im = (m @ T(*o) @ S(sc, sc, sc) @ T(float(ap['posX'])/16 + tr.get('x', 0), float(ap['posY'])/16 + tr.get('y', 0), float(ap['posZ'])/16 + tr.get('z', 0))
          @ RXYZ((float(ap['rotationX']) + rot.get('x', 0)) * D, (float(ap['rotationY']) + rot.get('y', 0)) * D, (float(ap['rotationZ']) + rot.get('z', 0)) * D) @ T(-o[0], -o[1], -o[2]))
    w = []; boxes(weapon['elements'], im, {}, w, {}, lambda e, t: (0, 0, 0))
    hand = (m @ np.array([float(ap['posX'])/16, float(ap['posY'])/16, float(ap['posZ'])/16, 1]))[:3]
    pts = np.array([p for mm, size, c in w for p in corners(mm, size)])
    dist = np.linalg.norm(pts - hand, axis=1); tip = pts[dist.argmax()]; near = dist.min()
    d = (tip - hand) / np.linalg.norm(tip - hand)
    print(f'{anim}@{fr:>4}: tip fwd={-d[0]:+.2f} up={d[1]:+.2f} side={d[2]:+.2f}  reach {dist.max():.2f}  nearest-to-hand {near*16:.1f}u  hand=({-hand[0]+0.5:+.2f},{hand[1]:.2f},{hand[2]-0.5:+.2f})')
