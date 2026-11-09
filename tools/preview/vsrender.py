"""Offline preview of a VS seraph shape posed by an animation, holding an item shape with a tpHandTransform.
Matrix math follows vsapi ShapeElement.GetLocalTransformMatrix (version 0), Animation lerp, and
EntityShapeRenderer.RenderItem. Orthographic side/front/top views, flat colors, painter's sort."""
import json, math, re, sys
import numpy as np
from PIL import Image, ImageDraw

def load(p):
    t = open(p, encoding='utf-8-sig').read()
    return json.loads(t)

def T(x, y, z):
    m = np.eye(4); m[:3, 3] = (x, y, z); return m
def S(x, y, z):
    return np.diag([x, y, z, 1.0])
def RX(a):
    c, s = math.cos(a), math.sin(a); m = np.eye(4); m[1, 1] = c; m[1, 2] = -s; m[2, 1] = s; m[2, 2] = c; return m
def RY(a):
    c, s = math.cos(a), math.sin(a); m = np.eye(4); m[0, 0] = c; m[0, 2] = s; m[2, 0] = -s; m[2, 2] = c; return m
def RZ(a):
    c, s = math.cos(a), math.sin(a); m = np.eye(4); m[0, 0] = c; m[0, 1] = -s; m[1, 0] = s; m[1, 1] = c; return m
def RXYZ(x, y, z):  # Mat4f.RotateByXYZ: M * Rx * Ry * Rz
    return RX(x) @ RY(y) @ RZ(z)
D = math.pi / 180

def local(e, pose):
    o = [v / 16 for v in (e.get('rotationOrigin') or [0, 0, 0])]
    rx = (e.get('rotationX') or 0) + pose.get('degX', 0)
    ry = (e.get('rotationY') or 0) + pose.get('degY', 0)
    rz = (e.get('rotationZ') or 0) + pose.get('degZ', 0)
    f = e['from']
    return (T(*o) @ RXYZ(rx * D, ry * D, rz * D) @ S(pose.get('sx', 1), pose.get('sy', 1), pose.get('sz', 1))
            @ T(f[0] / 16 + pose.get('tx', 0) - o[0], f[1] / 16 + pose.get('ty', 0) - o[1], f[2] / 16 + pose.get('tz', 0) - o[2]))

def anim_pose(anim, frame):
    """element name -> pose dict at the given frame, lerped per channel like vsapi Animation."""
    if anim is None: return {}
    n = anim.get('quantityframes', 1)
    kfs = anim['keyframes']
    poses = {}
    names = {name for k in kfs for name in k['elements']}
    groups = {'pos': ('offsetX', 'offsetY', 'offsetZ'), 'rot': ('rotationX', 'rotationY', 'rotationZ'), 'str': ('stretchX', 'stretchY', 'stretchZ')}
    for name in names:
        p = {}
        for g, keys in groups.items():
            have = [k for k in kfs if name in k['elements'] and any(kk in k['elements'][name] for kk in keys)]
            if not have: continue
            prev = [k for k in have if k['frame'] <= frame]
            nxt = [k for k in have if k['frame'] > frame]
            a = prev[-1] if prev else have[-1]
            b = nxt[0] if nxt else have[0]
            fa, fb = a['frame'], b['frame']
            if b is a: t = 0
            else:
                span = (fb - fa) % n if fb != fa else n
                t = ((frame - fa) % n) / span if span else 0
            ea, eb = a['elements'][name], b['elements'][name]
            dflt = 1 if g == 'str' else 0
            vals = [ea.get(k, dflt) + (eb.get(k, dflt) - ea.get(k, dflt)) * t for k in keys]
            if g == 'pos': p['tx'], p['ty'], p['tz'] = (v / 16 for v in vals)
            if g == 'rot': p['degX'], p['degY'], p['degZ'] = vals
            if g == 'str': p['sx'], p['sy'], p['sz'] = vals
        poses[name] = p
    return poses

def boxes(elements, parent, poses, out, aps, color_of, skip=()):
    for e in elements:
        if e['name'] in skip: continue
        m = parent @ local(e, poses.get(e['name'], {}))
        size = [(e['to'][i] - e['from'][i]) / 16 for i in range(3)]
        faces = e.get('faces') or {}
        if any(isinstance(f, dict) and f.get('texture') and f.get('enabled', True) is not False for f in faces.values()):
            tex = next((f['texture'] for f in faces.values() if isinstance(f, dict) and f.get('texture')), None)
            out.append((m, size, color_of(e, tex)))
        for ap in e.get('attachmentpoints') or []:
            aps[ap['code']] = (m, ap)
        boxes(e.get('children') or [], m, poses, out, aps, color_of, skip)

CUBE_FACES = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
def corners(m, size):
    pts = []
    for i in range(8):
        v = np.array([size[0] if i & 4 else 0, size[1] if i & 2 else 0, size[2] if i & 1 else 0, 1.0])
        pts.append((m @ v)[:3])
    return pts

def render(polys_boxes, view, path, scale=160, W=560, H=560, center=(0.5, 1.0, 0.5), marks=(), zbuffer=False):
    """zbuffer (haradrim-armor 0.1.3): per-pixel depth instead of the painter's sort, which can draw a large body face
    over a thin armor plate just in front of it when a limb is tilted (the 0.1.2/0.1.3 walk previews showed bare shins
    that the armor covers). Faces get a dark edge where they meet a face at another depth or the background."""
    img = Image.new('RGB', (W, H), (235, 235, 240)); dr = ImageDraw.Draw(img)
    # view: 'side' looks along -Z (x right, y up), 'front' looks along -X (z right... ), 'top' looks down -Y
    def proj(p):
        x, y, z = p[0] - center[0], p[1] - center[1], p[2] - center[2]
        if view == 'side': u, v, d = x, y, z
        elif view == 'front': u, v, d = -z, y, x
        elif view == 'back': u, v, d = z, y, -x
        else: u, v, d = x, -z, y
        return (W / 2 + u * scale, H / 2 - v * scale, d)
    quads = []
    for m, size, col in polys_boxes:
        c = [proj(p) for p in corners(m, size)]
        for f in CUBE_FACES:
            pts = [c[i] for i in f]
            depth = sum(p[2] for p in pts) / 4
            # shade by face normal direction in view space
            a, b, cc = np.array(pts[0]), np.array(pts[1]), np.array(pts[2])
            n = np.cross(b - a, cc - a); nn = np.linalg.norm(n)
            shade = 0.65 + 0.35 * (abs(n[2]) / nn if nn else 1)
            quads.append((depth, [(p[0], p[1]) for p in pts], tuple(int(ch * shade) for ch in col)))
    if zbuffer:
        zb = np.full((H, W), -np.inf); cb = np.zeros((H, W, 3), np.uint8); cb[:] = (235, 235, 240)
        ib = np.full((H, W), -1)
        k = 0
        for m, size, col in polys_boxes:
            c = [proj(p) for p in corners(m, size)]
            for f in CUBE_FACES:
                pts = [np.array(c[i]) for i in f]
                n = np.cross(pts[1] - pts[0], pts[2] - pts[0])
                if abs(n[2]) < 1e-9:
                    k += 1; continue
                xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
                x0, x1 = max(int(min(xs)), 0), min(int(max(xs)) + 2, W); y0, y1 = max(int(min(ys)), 0), min(int(max(ys)) + 2, H)
                if x0 >= x1 or y0 >= y1:
                    k += 1; continue
                mask = Image.new('1', (x1 - x0, y1 - y0), 0)
                ImageDraw.Draw(mask).polygon([(p[0] - x0, p[1] - y0) for p in pts], fill=1)
                mk = np.array(mask, bool)
                yy, xx = np.mgrid[y0:y1, x0:x1]
                # plane: n . (q - p0) = 0 -> depth at (x, y)
                d = pts[0][2] - (n[0] * (xx - pts[0][0]) + n[1] * (yy - pts[0][1])) / n[2]
                upd = mk & (d > zb[y0:y1, x0:x1] + 1e-6)
                zb[y0:y1, x0:x1][upd] = d[upd]; cb[y0:y1, x0:x1][upd] = quads[k][2]; ib[y0:y1, x0:x1][upd] = k
                k += 1
        # Outline only where the depth jumps (a face edge over something farther, or the background), not between
        # overlapping armor layers a fraction of a pixel apart.
        zf = np.where(np.isfinite(zb), zb, -1e3)
        jump = 0.03
        edge = np.zeros((H, W), bool)
        edge[:, 1:] |= np.abs(zf[:, 1:] - zf[:, :-1]) > jump; edge[1:, :] |= np.abs(zf[1:, :] - zf[:-1, :]) > jump
        cb[edge & (ib >= 0)] = (40, 40, 40)
        img.paste(Image.fromarray(cb)); dr = ImageDraw.Draw(img)
    else:
        quads.sort(key=lambda q: q[0])
        for _, pts, col in quads:
            dr.polygon(pts, fill=col, outline=(40, 40, 40))
    # grid: ground at y=0 and a 1-block ruler
    g = proj((center[0] - 2, 0, center[2]))
    dr.line([(0, g[1]), (W, g[1])], fill=(150, 150, 150))
    for mk in marks:
        q = proj(mk); dr.ellipse([q[0] - 4, q[1] - 4, q[0] + 4, q[1] + 4], outline=(255, 0, 0))
    dr.text((6, 6), view, fill=(0, 0, 0))
    img.save(path)
