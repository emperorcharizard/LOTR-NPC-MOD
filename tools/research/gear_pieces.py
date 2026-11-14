"""Counts shape elements per worn item for every lotrnpcs NPC type (read only)."""
import json, os, re, sys, zipfile, glob, fnmatch, collections as C

APP = os.environ["APPDATA"]
REPO = r"C:\Users\Mason\Documents\GitHub\LOTR-NPC-MOD"
GEN = os.path.join(REPO, "build", "gen", "assets")


def json5(text):
    """Tolerant parser for VS asset JSON: comments, unquoted keys, single quotes, trailing commas."""
    i, n = 0, len(text)

    def ws():
        nonlocal i
        while i < n:
            c = text[i]
            if c in " \t\r\n\ufeff":
                i += 1
            elif text.startswith("//", i):
                j = text.find("\n", i); i = n if j < 0 else j
            elif text.startswith("/*", i):
                j = text.find("*/", i); i = n if j < 0 else j + 2
            else:
                break

    def string():
        nonlocal i
        q = text[i]; i += 1; out = []
        while i < n and text[i] != q:
            if text[i] == "\\":
                i += 1; e = text[i]
                out.append({"n": "\n", "t": "\t", "r": "\r", "b": "\b", "f": "\f"}.get(e, e))
                if e == "u": out[-1] = chr(int(text[i + 1:i + 5], 16)); i += 4
            else:
                out.append(text[i])
            i += 1
        i += 1
        return "".join(out)

    def value():
        nonlocal i
        ws(); c = text[i]
        if c == "{":
            i += 1; d = {}
            while True:
                ws()
                if text[i] == "}": i += 1; return d
                if text[i] in "\"'": k = string()
                else:
                    j = i
                    while text[i] not in ":": i += 1
                    k = text[j:i].strip()
                ws(); i += 1  # ':'
                d[k] = value(); ws()
                if text[i] == ",": i += 1
        if c == "[":
            i += 1; a = []
            while True:
                ws()
                if text[i] == "]": i += 1; return a
                a.append(value()); ws()
                if text[i] == ",": i += 1
        if c in "\"'": return string()
        j = i
        while i < n and text[i] not in ",}] \t\r\n/": i += 1
        tok = text[j:i]
        if tok == "true": return True
        if tok == "false": return False
        if tok == "null": return None
        try: return int(tok)
        except ValueError:
            try: return float(tok)
            except ValueError: return tok

    return value()


# ---- asset sources: domain -> {path: loader} ----
files = {}  # (domain, path) -> callable returning text
def add_zip(zp):
    try: z = zipfile.ZipFile(zp)
    except Exception: return
    for name in z.namelist():
        m = re.match(r"assets/([^/]+)/(.+)$", name)
        if m and name.endswith(".json"):
            files.setdefault((m[1], m[2].lower()), (lambda z=z, name=name: z.read(name).decode("utf-8-sig", "replace")))
for zp in glob.glob(os.path.join(APP, "VintagestoryData", "Mods", "*.zip")):
    add_zip(zp)
for base, dom in ((os.path.join(APP, "Vintagestory", "assets", "survival"), "game"), (os.path.join(APP, "Vintagestory", "assets", "game"), "game"),
                  (os.path.join(GEN, "lotrnpcs"), "lotrnpcs")):
    for root, _, fs in os.walk(base):
        for f in fs:
            if f.endswith(".json"):
                p = os.path.join(root, f); rel = os.path.relpath(p, base).replace("\\", "/").lower()
                files.setdefault((dom, rel), (lambda p=p: open(p, encoding="utf-8-sig", errors="replace").read()))

cache = {}
def load(dom, path):
    k = (dom, path.lower())
    if k not in cache:
        f = files.get(k)
        try: cache[k] = json5(f()) if f else None
        except Exception as e: cache[k] = None
    return cache[k]

# itemtype index: domain -> list of (code, json)
itemtypes = C.defaultdict(list)
for (dom, path) in list(files):
    if path.startswith("itemtypes/"):
        d = load(dom, path)
        for it in (d if isinstance(d, list) else [d]):
            if isinstance(it, dict) and isinstance(it.get("code"), str): itemtypes[dom].append(it)


def wild(pattern, code):
    return fnmatch.fnmatchcase(code, pattern.split(":")[-1])


def match_variants(it, rest):
    groups = [g for g in it.get("variantgroups", []) if isinstance(g, dict) and isinstance(g.get("states"), list)]
    def rec(gi, s):
        if gi == len(groups): return {} if s == "" else None
        for st in sorted(groups[gi]["states"], key=len, reverse=True):
            st = str(st)
            if s == st or s.startswith(st + "-"):
                r = rec(gi + 1, s[len(st) + 1:] if s != st else "")
                if r is not None: r[groups[gi]["code"]] = st; return r
        return None
    return rec(0, rest)


def by_type(it, key, code):
    if key in it: return it[key]
    bt = it.get(key + "ByType") or it.get(key + "bytype")
    if isinstance(bt, dict):
        for k, v in bt.items():
            if wild(k, code): return v
    return None


def resolve(full):
    dom, code = full.split(":", 1) if ":" in full else ("game", full)
    for it in itemtypes.get(dom, []):
        base = it["code"]
        if code == base or code.startswith(base + "-"):
            vars_ = match_variants(it, code[len(base) + 1:] if code != base else "")
            if vars_ is None: continue
            attrs = it.get("attributes") or {}
            shape = attrs.get("attachShape") or by_type(attrs, "attachShape", code) or by_type(it, "shape", code)
            if isinstance(shape, dict): shape = shape.get("base")
            if not isinstance(shape, str): return None, vars_, it
            shape = re.sub(r"\{(\w+)\}", lambda m: vars_.get(m[1], m[0]), shape)
            ate = attrs.get("attachableToEntity") or {}
            disable = ate.get("disableElements") or by_type(ate, "disableElements", code) or attrs.get("disableElements") or by_type(attrs, "disableElements", code) or []
            cat = by_type(attrs, "clothescategory", code) or ate.get("categoryCode") or by_type(ate, "categoryCode", code)
            return (shape, disable, cat), vars_, it
    return None, None, None


def elements_of(shape_ref, dom):
    sdom, spath = shape_ref.split(":", 1) if ":" in shape_ref else (dom, shape_ref)
    d = load(sdom, "shapes/" + spath + ".json")
    return None if d is None else d.get("elements", [])


def walk(els):
    for e in els or []:
        yield e
        yield from walk(e.get("children"))


def tiny(e):
    f, t = e.get("from", [0, 0, 0]), e.get("to", [0, 0, 0])
    return any(abs(t[k] - f[k]) < 0.5 for k in range(3))


item_info = {}
def info(full):
    if full in item_info: return item_info[full]
    r, _, it = resolve(full)
    res = None
    if r:
        shape, disable, cat = r
        els = elements_of(shape, full.split(":")[0] if ":" in full else "game")
        if els is not None:
            all_ = list(walk(els))
            res = {"elements": len(all_), "tiny": sum(map(tiny, all_)), "shape": shape, "disable": disable, "cat": cat,
                   "names": [e.get("name") for e in all_]}
    item_info[full] = res
    return res


def weapon_info(full):
    d = load("lotrnpcs", "itemtypes/" + full.split(":")[1] + ".json") if full.startswith("lotrnpcs:") else None
    if d and d.get("shape"):
        els = elements_of(d["shape"]["base"], "lotrnpcs")
        if els is not None:
            a = list(walk(els)); return {"elements": len(a), "tiny": sum(map(tiny, a)), "shape": d["shape"]["base"], "disable": [], "cat": "held", "names": []}
    return info(full)


BODY = len(list(walk(load("lotrnpcs", "shapes/entity/humanoid/seraph-faceless.json")["elements"])))
rows = []  # (npc, faction, set_index, pieces)
for path in sorted(glob.glob(os.path.join(GEN, "lotrnpcs", "entities", "test-*.json"))):
    e = json.load(open(path, encoding="utf-8"))
    code = e["code"]; fac = e.get("attributes", {}).get("lotrnpcsFaction")
    beh = {b["code"]: b for b in e["server"]["behaviors"]}
    sg = beh.get("lotrnpcs:startinggear", {})
    sets = sg.get("sets") or ([sg["items"]] if sg.get("items") else [[]])
    hw = beh.get("lotrnpcs:heldweapon", {})
    weapons = [w["item"] for w in hw.get("weapons", [])]
    shields = hw.get("shields", [])
    helmets = e.get("attributes", {}).get("lotrnpcsHelmets") or []
    for si, s in enumerate(sets):
        rows.append({"npc": code, "faction": fac, "set": si, "items": list(s), "weapons": weapons, "shields": shields, "helmets": helmets, "fallback": sg.get("items", [])})

json.dump({"body": BODY, "rows": rows, "items": {k: v for k, v in item_info.items()}}, open(sys.argv[1], "w"), indent=0)
# resolve everything referenced
allitems = sorted({i for r in rows for i in r["items"] + r["fallback"] + r["helmets"] + r["shields"]})
allweap = sorted({w for r in rows for w in r["weapons"]})
for i in allitems: info(i)
wi = {w: weapon_info(w) for w in allweap}
missing = [i for i in allitems if item_info.get(i) is None]
print("body", BODY, "npc types", len({r['npc'] for r in rows}), "outfit sets", len(rows), "items", len(allitems), "unresolved", len(missing))
for m in missing[:40]: print("  unresolved", m)
json.dump({"body": BODY, "rows": rows, "items": item_info, "weapons": wi}, open(sys.argv[1], "w"), indent=0)
