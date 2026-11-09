"""0.0.108 (owner: "balance combat for equal coin spendage"): hire prices so that armies bought with the same coins
win about half their fights. Stats: fit-RANK.json (the 0.0.108 per-rank refit). Each tier's prices are Men's
(5/15/40/15) times a multiplier m, rounded; Men fixed. A buyer spends up to a quarter on captains (within the captain
limit), then recruit, warrior, recruit, warrior, archer in turn while affordable, then recruits, up to the roster.
Budgets 50 to 250 coins in steps of 10, averaged (one budget alone is lumpy: a unit more or less decides it). Hill climbing on m in steps of 0.02.
Usage: python3 fitprice.py <Factions.cs> RUNS STEPS"""
import sys, json, random
SRC, RUNS, STEPS = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
sys.argv = [sys.argv[0], SRC, str(RUNS)]
import sim105 as S
from multiprocessing import Pool
names = list(S.TIERS); RANKS = S.RANKS
FIT = {r: json.load(open(f"fit-{r}.json")) for r in RANKS}
STATS = {t: dict(S.TIERS[t], followers=6 if t == "Orc" else S.TIERS[t]["followers"], hp={r: FIT[r][t]["health"] for r in RANKS}, dmg={r: FIT[r][t]["damage"] for r in RANKS}) for t in names}
MEN = {"recruit": 5, "warrior": 15, "captain": 40, "archer": 15}
BUDGETS = tuple(range(50, 251, 10))
CYCLE = ("recruit", "warrior", "recruit", "warrior", "archer")
def prices(m): return {r: max(1, round(MEN[r] * m)) for r in RANKS}
def buy(t, p, coins):
    roster = STATS[t]["roster"]; caps = S.captains_for(roster); r = []; left = coins
    while r.count("captain") < caps and coins - left + p["captain"] <= coins / 4: r.append("captain"); left -= p["captain"]
    i = 0
    while len(r) < roster:
        k = CYCLE[i % len(CYCLE)]; i += 1
        if left >= p[k]: r.append(k); left -= p[k]
        elif left >= p["recruit"]: r.append("recruit"); left -= p["recruit"]
        else: break
    return r
def rate(args):
    a, ma, b, mb, seed = args
    rnd = random.Random(seed); pa, pb = prices(ma), prices(mb); w = 0; n = 0
    for B in BUDGETS:
        ra, rb = buy(a, pa, B), buy(b, pb, B)
        for _ in range(RUNS):
            w += S.fight([S.U(0, STATS[a], k) for k in ra], [S.U(1, STATS[b], k) for k in rb], rnd)[0]; n += 1
    return w / n
pairs = [(a, b) for i, a in enumerate(names) for b in names[i + 1:]]
def avgs(mat): return {t: sum(mat[(t, b)] if (t, b) in mat else 1 - mat[(b, t)] for b in names if b != t) / 6 for t in names}
TARGET = {t: 0.5 for t in names}
if __name__ == "__main__":
    m = {"Men": 1.0, "Uruk": 1.05, "Levy": 0.83, "Orc": 0.8, "Goblin": 0.65, "Elf": 1.29, "Dwarf": 1.29}
    rng = random.Random(3)
    with Pool() as pool:
        def matrix(m, salt=0, runs=None, only=None):
            ps = [p for p in pairs if only is None or only in p]
            return dict(zip(ps, pool.map(rate, [(a, m[a], b, m[b], hash((a, b, salt)) & 0xffffff) for a, b in ps])))
        mat = matrix(m); obj = sum((v - 0.5) ** 2 for v in avgs(mat).values())
        print("start", {t: round(v, 2) for t, v in avgs(mat).items()}, flush=True)
        for step in range(STEPS):
            t = rng.choice([n for n in names if n != "Men"])
            trial = dict(m); trial[t] = round(max(0.3, m[t] + rng.choice((-0.04, -0.02, 0.02, 0.04))), 2)
            if prices(trial[t]) == prices(m[t]): m = trial; continue
            new = dict(mat); new.update(matrix(trial, only=t))
            o = sum((v - 0.5) ** 2 for v in avgs(new).values())
            if o < obj: m, mat, obj = trial, new, o
        check = matrix(m, salt=1); av = avgs(check)
        print("end", {t: (m[t], prices(m[t]), round(av[t], 2)) for t in names}, flush=True)
        json.dump({t: {"multiplier": m[t], "prices": prices(m[t]), "average": round(av[t], 3), "vs": {b: round(check[(t, b)] if (t, b) in check else 1 - check[(b, t)], 2) for b in names if b != t}} for t in names}, open("fit-prices.json", "w"), indent=1)
