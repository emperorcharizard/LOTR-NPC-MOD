"""Equal-coin win rates of the shipped prices under several ways of spending. Usage: python3 -I evalcoin.py <Factions.cs> RUNS"""
import sys, random
SRC, RUNS = sys.argv[1], int(sys.argv[2]); sys.argv = [sys.argv[0], SRC, "1"]
import sim105 as S
from multiprocessing import Pool
names = list(S.TIERS); RANKS = S.RANKS; BUDGETS = tuple(range(50, 251, 20))
def strat(name, t, coins, rnd):
    T = S.TIERS[t]; p = T["price"]; roster = T["roster"]; caps = S.captains_for(roster); r = []; left = coins
    share = {"quarter": 0.25, "half": 0.5, "nocap": 0.0, "recruits": 0.0, "warriors": 0.0, "maxcap": 1.0}[name]
    while r.count("captain") < caps and coins - left + p["captain"] <= coins * share: r.append("captain"); left -= p["captain"]
    cyc = {"recruits": ("recruit",), "warriors": ("warrior",)}.get(name, ("recruit", "warrior", "recruit", "warrior", "archer")); i = 0
    while len(r) < roster:
        k = cyc[i % len(cyc)]; i += 1
        if left >= p[k]: r.append(k); left -= p[k]
        elif left >= p["recruit"]: r.append("recruit"); left -= p["recruit"]
        else: break
    return r
def rate(args):
    st, a, b, seed = args; rnd = random.Random(seed); w = n = 0
    for B in BUDGETS:
        ra, rb = strat(st, a, B, rnd), strat(st, b, B, rnd)
        for _ in range(RUNS): w += S.fight([S.U(0, S.TIERS[a], k) for k in ra], [S.U(1, S.TIERS[b], k) for k in rb], rnd)[0]; n += 1
    return w / n
if __name__ == "__main__":
    pairs = [(a, b) for i, a in enumerate(names) for b in names[i + 1:]]
    with Pool() as pool:
        for st in ("quarter", "half", "maxcap", "nocap", "recruits", "warriors"):
            res = dict(zip(pairs, pool.map(rate, [(st, a, b, hash((st, a, b)) & 0xffffff) for a, b in pairs])))
            av = {t: sum(res[(t, b)] if (t, b) in res else 1 - res[(b, t)] for b in names if b != t) / 6 for t in names}
            print(f"{st:9}", "  ".join(f"{t} {av[t]:.2f}" for t in names), flush=True)
