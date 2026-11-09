"""0.0.107 refit (owner chose "Orcs to 6 followers + refit all"): per rank, every tier's full same-rank following
plays every other's under the 0.0.105 rules (sim105.fight: spread, hit turns, cap 4, arrows lost in i-frames).
Objective: sum over tiers of (average win rate - target)^2 (Uruk 0.55, others even) plus a pull toward the tier's
current (0.0.103) values so the flavour stays. Hill climbing, health +-1 / damage +-0.05, common random numbers.
Usage: python3 fit105.py <Factions.cs> RANK RUNS STEPS"""
import sys, random, json
SRC, RANK, RUNS, STEPS = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
sys.argv = [sys.argv[0], SRC, str(RUNS)]
import sim105 as S
from multiprocessing import Pool
names = list(S.TIERS)
N = {t: S.TIERS[t]["followers"] for t in names}; N["Orc"] = 6
START = {t: (S.TIERS[t]["hp"][RANK], S.TIERS[t]["dmg"][RANK]) for t in names}
UR = 0.55; OTHER = (3.5 - UR) / 6
TARGET = {t: (UR if t == "Uruk" else OTHER) for t in names}
pairs = [(a, b) for i, a in enumerate(names) for b in names[i + 1:]]
def band(side, n, hp, dmg):
    t = {"hp": {RANK: hp}, "dmg": {RANK: dmg}}
    return [S.U(side, t, RANK) for _ in range(n)]
def rate(args):
    a, ha, da, b, hb, db, runs, seed = args
    rnd = random.Random(seed)
    return sum(S.fight(band(0, N[a], ha, da), band(1, N[b], hb, db), rnd)[0] for _ in range(runs)) / runs
def seed_of(a, b, salt=0): return hash((RANK, a, b, salt)) & 0xffffff
def avgs(mat): return {t: sum(mat[(t, b)] if (t, b) in mat else 1 - mat[(b, t)] for b in names if b != t) / 6 for t in names}
def objective(vals, mat):
    av = avgs(mat)
    return sum((av[t] - TARGET[t]) ** 2 + 0.003 * ((vals[t][0] / START[t][0] - 1) ** 2 + (vals[t][1] / START[t][1] - 1) ** 2) for t in names)
if __name__ == "__main__":
    vals = dict(START); rng = random.Random(7)
    with Pool() as pool:
        def matrix(vals, salt=0, runs=RUNS, only=None):
            ps = [p for p in pairs if only is None or only in p]
            jobs = [(a, vals[a][0], vals[a][1], b, vals[b][0], vals[b][1], runs, seed_of(a, b, salt)) for a, b in ps]
            return dict(zip(ps, pool.map(rate, jobs)))
        mat = matrix(vals); obj = objective(vals, mat)
        for step in range(STEPS):
            t = rng.choice(names); h, d = vals[t]
            if rng.random() < 0.5: h = max(1, h + rng.choice((-1, 1)))
            else: d = round(max(0.1, d + rng.choice((-0.05, 0.05))), 2)
            trial = dict(vals); trial[t] = (h, d)
            new = dict(mat); new.update(matrix(trial, only=t))
            o = objective(trial, new)
            if o < obj: vals, mat, obj = trial, new, o
        check = matrix(vals, salt=1, runs=RUNS * 4); av = avgs(check)
        print(f"== {RANK}: " + ", ".join(f"{t} {vals[t][0]:g}/{vals[t][1]:.2f} avg {av[t]:.2f}" for t in names), flush=True)
        json.dump({t: {"health": vals[t][0], "damage": vals[t][1], "average": round(av[t], 3)} for t in names}, open(f"fit-{RANK}.json", "w"), indent=1)
