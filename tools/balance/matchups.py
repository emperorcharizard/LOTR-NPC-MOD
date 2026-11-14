"""2026-11-14 matchup analysis (owner: "do another mathematical analysis of all the different matchups that can happen
for testing balance"). sim105's fight rules on the owner's ModConfig/lotrnpcs-factions.json numbers (every faction,
every rank: recruit, warrior, captain, archer, guard, shock, rider), with NpcHitsIgnoreHurtWindow as configured.
Usage: python tools/balance/matchups.py [config.json] [runs] > out.md
"""
import json, os, random, runpy, sys, statistics
from multiprocessing import Pool

HERE = os.path.dirname(os.path.abspath(__file__))
ARGS, _argv = sys.argv[1:], sys.argv
sys.argv = ["sim105.py", os.path.join(HERE, "..", "..", "src", "Factions.cs"), "1"]
G = runpy.run_path(os.path.join(HERE, "sim105.py"), run_name="sim")
sys.argv = _argv  # Windows' multiprocessing children start from sys.argv again
U, fight = G["U"], G["fight"]
CONFIG = ARGS[0] if ARGS else os.path.expandvars(r"%APPDATA%\VintagestoryData\ModConfig\lotrnpcs-factions.json")
RUNS = int(ARGS[1]) if len(ARGS) > 1 else 200
CFG = json.load(open(CONFIG, encoding="utf-8-sig"))
TURNS = not CFG.get("NpcHitsIgnoreHurtWindow", False)
if not TURNS: fight.__globals__["INVULN"] = 0.0  # hits on NPCs ignore the hurt window: every melee hit lands
CAP = int(CFG.get("MaxAttackersPerTarget", 4))
fight.__globals__["SPREAD"] = float(CFG.get("DamageSpread", 0.2))
RANKS = ["recruit", "warrior", "captain", "archer", "guard", "shock", "rider"]
RIDER_EXP = 0.82  # FactionConfig.RiderStatExponent (0.0.130)

FACTIONS = {}
for name, f in CFG["Factions"].items():
    hp, dm, hire, promo = f.get("Health") or {}, f.get("Damage") or {}, f.get("HirePrices") or {}, f.get("PromotePrices") or {}
    if "warrior" not in hp: continue
    scale = (hire["captain"] / hire["warrior"]) ** RIDER_EXP if hire.get("captain") and hire.get("warrior") else 2.2
    hp = dict(hp); dm = dict(dm)
    hp.setdefault("rider", round(hp["warrior"] * scale)); dm.setdefault("rider", round(dm["warrior"] * scale, 2))
    cost = {r: hire[r] for r in ("recruit", "warrior", "captain", "archer") if r in hire}
    # Promoted ranks: a warrior's hire plus the promotion (a rider costs a captain's promotion, Promotion.PriceRank).
    for r, via in (("guard", "guard"), ("shock", "shock"), ("rider", "captain")):
        if "warrior" in hire and via in promo: cost[r] = hire["warrior"] + promo[via]
    FACTIONS[name] = dict(hp=hp, dmg=dm, cost=cost, followers=f.get("MaxFollowers", 5))

def army(fac, rank, coins, rnd, side):
    t = FACTIONS[fac]; n = coins / t["cost"][rank]
    k = int(n) + (1 if rnd.random() < n - int(n) else 0)
    return [U(side, t, rank) for _ in range(max(1, k))]

def duel(args):
    fa, ra, fb, rb, coins, seed = args
    w = left = 0
    for i in range(RUNS):
        rnd = random.Random(seed * 100003 + i)
        a, b = army(fa, ra, coins, rnd, 0), army(fb, rb, coins, rnd, 1)
        won, x, y = fight(a, b, rnd, CAP, TURNS)
        w += won
    return (fa, ra, fb, rb, coins, w / RUNS)

def table(rows, title):
    print(f"\n### {title}\n")
    print("| | " + " | ".join(r[0] for r in rows[0][1]) + " |")
    print("| --- " * (len(rows[0][1]) + 1) + "|")
    for name, cells in rows: print(f"| {name} | " + " | ".join(c[1] for c in cells) + " |")

def main():
    facs = sorted(FACTIONS)
    print(f"Config: {CONFIG}\nRuns per pairing: {RUNS}; attacker cap {CAP}; hurt window ignored: {not TURNS}; spread {fight.__globals__['SPREAD']}.\n")
    print("## Units (health / damage / cost in coin)\n")
    print("| faction | " + " | ".join(RANKS) + " |\n| --- " * 1 + "| --- " * len(RANKS) + "|")
    for f in facs:
        t = FACTIONS[f]
        print(f"| {f} | " + " | ".join(f"{t['hp'][r]:g} / {t['dmg'][r]:g} / {t['cost'].get(r, '-')}" if r in t["hp"] and r in t["cost"] else "-" for r in RANKS) + " |")
    jobs = []
    seed = 1
    for coins in (120, 360):
        for rank in RANKS:
            for i, a in enumerate(facs):
                for b in facs[i + 1:]:
                    if rank in FACTIONS[a]["cost"] and rank in FACTIONS[b]["cost"]: jobs.append((a, rank, b, rank, coins, seed)); seed += 1
    for f in facs:
        for i, ra in enumerate(RANKS):
            for rb in RANKS[i + 1:]:
                if ra in FACTIONS[f]["cost"] and rb in FACTIONS[f]["cost"]: jobs.append((f, ra, f, rb, 240, seed)); seed += 1
    with Pool() as p: res = p.map(duel, jobs, chunksize=8)
    same = {}
    for fa, ra, fb, rb, coins, w in res:
        if fa != fb: same[(coins, ra, fa, fb)] = w; same[(coins, ra, fb, fa)] = 1 - w
    print("\n## 1. Same rank, equal coin: each faction's average win rate against every other faction\n")
    print("Coin per side: 120 (a band) and 360 (a battle). 0.5 is even; bold is outside 0.35-0.65.\n")
    print("| faction | " + " | ".join(f"{r} 120 | {r} 360" for r in RANKS) + " |\n" + "| --- " * (1 + 2 * len(RANKS)) + "|")
    avgs = {}
    for f in facs:
        cells = []
        for r in RANKS:
            for coins in (120, 360):
                v = [same[(coins, r, f, o)] for o in facs if o != f and (coins, r, f, o) in same]
                m = statistics.mean(v) if v else None; avgs[(f, r, coins)] = m
                cells.append("-" if m is None else (f"**{m:.2f}**" if m < 0.35 or m > 0.65 else f"{m:.2f}"))
        print(f"| {f} | " + " | ".join(cells) + " |")
    print("\n## 2. The most lopsided single pairings (360 coin each, same rank)\n")
    worst = sorted(((abs(w - 0.5), k, w) for k, w in same.items() if k[0] == 360 and w >= 0.5), reverse=True)[:25]
    print("| rank | winner | loser | win rate |\n| --- | --- | --- | --- |")
    for _, (coins, r, a, b), w in worst: print(f"| {r} | {a} | {b} | {w:.2f} |")
    print("\n## 3. Which rank is worth its coin: rank against rank inside one faction, 240 coin each\n")
    print("Row rank's win rate against the column rank (same faction), averaged over all factions.\n")
    cross = {}
    for fa, ra, fb, rb, coins, w in res:
        if fa == fb: cross.setdefault((ra, rb), []).append(w); cross.setdefault((rb, ra), []).append(1 - w)
    print("| | " + " | ".join(RANKS) + " | average |\n" + "| --- " * (len(RANKS) + 2) + "|")
    for ra in RANKS:
        vals = [statistics.mean(cross[(ra, rb)]) if (ra, rb) in cross else None for rb in RANKS]
        av = statistics.mean(v for v in vals if v is not None)
        print(f"| {ra} | " + " | ".join("-" if v is None else f"{v:.2f}" for v in vals) + f" | {av:.2f} |")

if __name__ == "__main__":
    main()
