import json, sys, collections as C, statistics as st
D = json.load(open(sys.argv[1]))
BODY, rows, items, weap = D["body"], D["rows"], D["items"], D["weapons"]
def el(i): return (items.get(i) or weap.get(i) or {}).get("elements", 0)
out = []
P = out.append
# 1) heaviest
worn = sorted({i for r in rows for i in r["items"] + r["helmets"] + r["shields"]} | set(weap))
heavy = sorted(((el(i), i) for i in worn), reverse=True)[:15]
P("| # | item | mod | elements | tiny (<0.5 voxel) |\n| --- | --- | --- | --- | --- |")
for k, (n, i) in enumerate(heavy, 1):
    t = (items.get(i) or weap.get(i) or {}).get("tiny", 0)
    P(f"| {k} | `{i.split(':',1)[1]}` | {i.split(':')[0]} | {n} | {t} |")
tiny_sum = sum((items.get(i) or weap.get(i) or {}).get("tiny", 0) for _, i in heavy); el_sum = sum(n for n, _ in heavy)
P(f"\nTiny decorations in these 15: {tiny_sum} of {el_sum} elements ({100*tiny_sum/max(1,el_sum):.0f}%).")
# 2) per faction totals: body + outfit set + heaviest/lightest weapon + shield (worst: heaviest weapon + heaviest shield)
P("\n| faction | NPC types | outfits | typical total | worst total | worst NPC / outfit |\n| --- | --- | --- | --- | --- | --- |")
byf = C.defaultdict(list)
for r in rows:
    wl = [el(w) for w in r["weapons"]] or [0]
    sl = [el(s) for s in r["shields"]]
    gear = sum(el(i) for i in r["items"])
    typical = BODY + gear + round(st.mean(wl)) + (round(st.mean(sl)) // 2 if sl else 0)  # shield carried about half the time
    worst = BODY + gear + max(wl) + (max(sl) if sl else 0)
    byf[r["faction"] or "-"].append((typical, worst, r["npc"], r["set"]))
for f, l in sorted(byf.items(), key=lambda kv: -max(x[1] for x in kv[1])):
    w = max(l, key=lambda x: x[1])
    P(f"| {f} | {len({x[2] for x in l})} | {len(l)} | {round(st.median(x[0] for x in l))} | {w[1]} | {w[2]} set {w[3]} |")
# 3) coverage: same category twice in a set, and pieces whose elements another piece disables
P("\n### Covered pieces\n")
found = 0
for r in rows:
    its = [i for i in r["items"] if items.get(i)]
    cats = C.Counter(items[i]["cat"] for i in its if items[i]["cat"])
    for c, n in cats.items():
        if n > 1:
            P(f"- {r['npc']} set {r['set']}: {n} pieces in category `{c}`: " + ", ".join(f"`{i}` ({el(i)})" for i in its if items[i]['cat'] == c)); found += 1
    for a in its:
        dis = set(items[a]["disable"] or [])
        for b in its:
            if a == b or not dis: continue
            names = [x for x in items[b]["names"] if x]
            hit = [x for x in names if x in dis]
            if hit and len(hit) == len(names):
                P(f"- {r['npc']} set {r['set']}: `{b}` is entirely hidden by `{a}` (disableElements)"); found += 1
            elif hit:
                P(f"- {r['npc']} set {r['set']}: `{a}` hides {len(hit)} of {len(names)} elements of `{b}`"); found += 1
if not found: P("None found by category or disableElements.")
P("\n### Per-outfit detail\n\n| NPC | set | pieces (elements) | gear total |\n| --- | --- | --- | --- |")
for r in rows:
    P(f"| {r['npc']} | {r['set']} | " + ", ".join(f"{i.split(':',1)[1]} ({el(i)})" for i in r["items"]) + f" | {sum(el(i) for i in r['items'])} |")
print("\n".join(out))
