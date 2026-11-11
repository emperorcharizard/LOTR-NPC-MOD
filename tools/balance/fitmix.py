"""0.0.108: per-rank whole-coin hire prices so equal coin is even however it is spent. Objective: every tier's average
win rate (over the six others) near 0.5 in each spending style of evalcoin.py (quarter/half/all-allowed on captains then
recruit-warrior-archer mix, no captains, recruits only, warriors only), budgets 50-250 with roster and captain limits.
Men fixed. Coordinate search on each tier's four prices. Usage: python3 fitmix.py <Factions.cs> RUNS SWEEPS"""
import sys, random, json
import os; sys.argv = os.environ.setdefault("LOTRNPCS_BALANCE_ARGV", "\n".join(sys.argv)).split("\n")  # Windows pool workers re-run this with the argv edited below; keep the real one
SRC, RUNS, SWEEPS = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]); sys.argv = [sys.argv[0], SRC, str(RUNS)]
import evalcoin as E
S = E.S
from multiprocessing import Pool
names = E.names; STRATS = ("quarter", "half", "maxcap", "nocap", "recruits", "warriors")
pairs = [(a, b) for i, a in enumerate(names) for b in names[i + 1:]]
def rate(args):
    st, a, pa, b, pb, seed = args
    S.TIERS[a]["price"] = pa; S.TIERS[b]["price"] = pb
    return E.rate((st, a, b, seed))
if __name__ == "__main__":
    E.RUNS = RUNS
    P = {t: dict(S.TIERS[t]["price"]) for t in names}
    with Pool() as pool:
        def run(P, only=None, salt=0):
            jobs = [(st, a, P[a], b, P[b], hash((st, a, b, salt)) & 0xffffff) for st in STRATS for a, b in pairs if only is None or only in (a, b)]
            return dict(zip([(j[0], j[1], j[3]) for j in jobs], pool.map(rate, jobs)))
        def avgs(res):
            return {(st, t): sum(res[(st, t, b)] if (st, t, b) in res else 1 - res[(st, b, t)] for b in names if b != t) / 6 for st in STRATS for t in names}
        def obj(res): return sum((v - .5) ** 2 for v in avgs(res).values())
        res = run(P); o = obj(res); print("start", round(o, 3), flush=True)
        for sw in range(SWEEPS):
            for t in names:
                if t == "Men": continue
                for r in S.RANKS:
                    for d in (-1, 1) if r != "captain" else (-2, 2):
                        T = {k: dict(v) for k, v in P.items()}; T[t][r] = max(1, P[t][r] + d)
                        new = dict(res); new.update(run(T, only=t)); o2 = obj(new)
                        if o2 < o: P, res, o = T, new, o2; print(" ", t, r, T[t][r], round(o, 3), flush=True)
            print("sweep", sw, round(o, 3), flush=True)
        chk = avgs(run(P, salt=1))
        for st in STRATS: print(f"{st:9}", "  ".join(f"{t} {chk[(st, t)]:.2f}" for t in names), flush=True)
        print("prices", P, flush=True)
        json.dump({"prices": P, "check": {f"{st} {t}": round(v, 3) for (st, t), v in chk.items()}}, open("fit-mixprices.json", "w"), indent=1)
