import runpy, sys, json
sys.argv = ["x", sys.argv[1]]
g = runpy.run_path(__file__.replace("gear_strip_counts.py", "gear_pieces.py"))
items = g["item_info"]; weap = g["wi"]
def strip_count(shape_ref, dom, size=0.5):
    els = g["elements_of"](shape_ref, dom) or []
    targets = {e.get("stepParentName") for e in g["walk"](els) if e.get("stepParentName")}
    n = 0
    def rec(lst):
        nonlocal n
        for e in lst or []:
            ch = e.get("children") or []
            if not ch and not e.get("attachmentpoints") and not e.get("attachmentPoints") and not e.get("stepParentName") and e.get("name") not in targets:
                f, t = e.get("from", [0,0,0]), e.get("to", [0,0,0])
                if all(abs(t[k]-f[k]) < size for k in range(3)): n += 1; continue
            rec(ch)
    for root in els: rec(root.get("children"))
    return n
allc = {**{k: v for k, v in items.items() if v}, **{k: v for k, v in weap.items() if v}}
rows = sorted(((v["elements"], k) for k, v in allc.items()), reverse=True)[:15]
print("| item | elements | stripped at 0.5 | after |\n| --- | --- | --- | --- |")
tot = tots = 0
for n, k in rows:
    v = allc[k]; s = strip_count(v["shape"], k.split(":")[0])
    tot += n; tots += s
    print(f"| `{k}` | {n} | {s} | {n-s} |")
print(f"\nTotal for these 15: {tots} of {tot} elements left out ({100*tots/tot:.0f}%).")
# whole catalogue
al = sum(v["elements"] for v in allc.values()); st = sum(strip_count(v["shape"], k.split(":")[0]) for k, v in allc.items())
print(f"All {len(allc)} worn items: {st} of {al} elements ({100*st/al:.0f}%).")
for size in (1.0, 1.5, 2.0):
    st = sum(strip_count(v["shape"], k.split(":")[0], size) for k, v in allc.items())
    print(f"size {size}: {st} of {al} ({100*st/al:.0f}%)")
# how many elements are leaves at all / thin-only
import collections as C
leaf=thin=0
for k,v in allc.items():
    for e in g["walk"](g["elements_of"](v["shape"], k.split(":")[0]) or []):
        if not e.get("children"): leaf+=1
        f,t=e.get("from",[0,0,0]),e.get("to",[0,0,0]); d=sorted(abs(t[i]-f[i]) for i in range(3))
        if d[0]<0.5 and d[1]>=0.5: thin+=1
print("leaves",leaf,"thin plates (one side <0.5, others bigger)",thin)
