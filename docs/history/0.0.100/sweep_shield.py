import sys
sys.path.insert(0, sys.argv[1]); G = sys.argv[2]; names = sys.argv[3].split(',')
sys.argv = ['gaps.py', sys.argv[1], G]
exec(open('gaps.py').read().split('seen = set()')[0])
def sink(points, arm_m, arm_e):
    inv = np.linalg.inv(arm_m); p = (inv @ np.hstack([points, np.ones((len(points), 1))]).T).T[:, :3] * 16
    size = np.array([(arm_e['to'][i] - arm_e['from'][i]) for i in range(3)])
    inside = np.all((p > 0) & (p < size), 1)
    if not inside.any(): return 0.0
    q = p[inside]; return float(np.minimum(q, size - q).min(1).max())
for n in names:
    shape, tf = item_tf('lotrnpcs:orcweapon-' + n, 'L')
    for bn, body in bodies.items():
        worst_sink = {}
        g0 = None
        for code, fr in [('rest', 0), ('idle1', 0), ('walk', 8), ('sprint', 4), ('falx', 12)]:
            poses = anim_pose(own[bn].get(code), fr) if code != 'rest' else {}
            ms = {}; mats(body['elements'], np.eye(4), poses, ms)
            apm, ap = next(((m, a) for m, e in ms.values() for a in (e.get('attachmentpoints') or []) if a['code'] == 'LeftHand'))
            pts = item_points(shape, item_matrix(apm, ap, tf))
            if g0 is None: g0 = gap(pts, *ms['LowerArmL'])
            for el in ('LowerArmL', 'UpperArmL', 'UpperTorso', 'LowerTorso'):
                if el in ms: worst_sink[el] = max(worst_sink.get(el, 0), sink(pts, *ms[el]))
        print(f"{n:14} {bn:7} hand gap {g0:.2f}px  sink " + ' '.join(f"{k} {v:.2f}" for k, v in worst_sink.items()))
