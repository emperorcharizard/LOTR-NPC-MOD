"""Joint per-rank fit (owner: "balance it for all factions on an average, not just men"; "should still be a 50/50
chance one side defeats the other for same rank fights"). For one rank at a time, every tier's full same-rank band
plays every other's; the objective is the sum over tiers of (average win rate - target)^2 plus a small pull toward
the tier's flavour multipliers. Hill climbing on single steps (health +-1, damage +-0.05), each pair with a fixed
seed (common random numbers), so a step is judged on the same fights. Men may move too, pulled to the base."""
import sys, random, json
from multiprocessing import Pool
from sim2 import Unit, fight
RANK = sys.argv[1]; RUNS = int(sys.argv[2]); STEPS = int(sys.argv[3]); START = sys.argv[4]
N = {"Men": 5, "Uruk": 5, "Levy": 6, "Orc": 7, "Goblin": 8, "Elf": 4, "Dwarf": 4}
FLAV = {"Men": (1, 1), "Uruk": (1.05, 1.03), "Levy": (0.85, 0.85), "Orc": (0.66, 0.85), "Goblin": (0.57, 0.73), "Elf": (1.14, 1.35), "Dwarf": (1.33, 1.15)}
BASE = {"recruit": (22, 3.0), "warrior": (34, 4.0), "captain": (48, 7.0), "archer": (34, 3.0)}
UR = 0.55; OTHER = (3.5 - UR) / 6
TARGET = {t: (UR if t == "Uruk" else OTHER) for t in N}
names = list(N); pairs = [(a, b) for i, a in enumerate(names) for b in names[i + 1:]]
def band(side, n, hp, dmg):
    out = []
    for _ in range(n):
        u = Unit(side, RANK, 1, 1); u.hp = hp; u.dmg = dmg; out.append(u)
    return out
def rate(args):
    a, ha, da, b, hb, db, runs, seed = args
    rnd = random.Random(seed)
    return sum(fight(band(0, N[a], ha, da), band(1, N[b], hb, db), rnd) for _ in range(runs)) / runs
def seed_of(a, b, salt=0): return (hash((RANK, a, b, salt)) & 0xffffff)
def objective(vals, mat):
    bh, bd = BASE[RANK]; s = 0
    for t in names:
        avg = sum(mat[(t, b)] if (t, b) in mat else 1 - mat[(b, t)] for b in names if b != t) / 6
        s += (avg - TARGET[t]) ** 2
        s += 0.002 * ((vals[t][0] / (bh * FLAV[t][0]) - 1) ** 2 + (vals[t][1] / (bd * FLAV[t][1]) - 1) ** 2)
    return s
def avgs(mat): return {t: sum(mat[(t, b)] if (t, b) in mat else 1 - mat[(b, t)] for b in names if b != t) / 6 for t in names}
if __name__ == "__main__":
    start = json.load(open(START))[RANK]
    vals = {t: (start[t]["health"], start[t]["damage"]) for t in names}
    rng = random.Random(5)
    with Pool() as pool:
        def matrix(vals, salt=0, runs=RUNS):
            jobs = [(a, vals[a][0], vals[a][1], b, vals[b][0], vals[b][1], runs, seed_of(a, b, salt)) for a, b in pairs]
            return dict(zip(pairs, pool.map(rate, jobs)))
        mat = matrix(vals); obj = objective(vals, mat)
        for step in range(STEPS):
            t = rng.choice(names)
            h, d = vals[t]
            if rng.random() < 0.5: h = max(1, h + rng.choice((-1, 1)))
            else: d = round(d + rng.choice((-0.05, 0.05)), 2)
            trial = dict(vals); trial[t] = (h, d)
            jobs = [(a, trial[a][0], trial[a][1], b, trial[b][0], trial[b][1], RUNS, seed_of(a, b)) for a, b in pairs if t in (a, b)]
            new = dict(mat); new.update(dict(zip([p for p in pairs if t in p], pool.map(rate, jobs))))
            o = objective(trial, new)
            if o < obj: vals, mat, obj = trial, new, o
        # check on fresh fights
        check = matrix(vals, salt=1, runs=RUNS * 4)
        av = avgs(check)
        worst = max(abs(check[p] - 0.5) for p in pairs if "Uruk" not in p)
        print(f"== {RANK}: " + ", ".join(f"{t} {vals[t][0]}/{vals[t][1]:.2f} avg {av[t]:.2f}" for t in names) + f"; worst single pair (Uruk aside) {0.5 + worst:.2f}", flush=True)
        json.dump({t: {"health": vals[t][0], "damage": vals[t][1], "average": round(av[t], 3), "vs": {b: round(check[(t, b)] if (t, b) in check else 1 - check[(b, t)], 2) for b in names if b != t}} for t in names}, open(f"joint-{RANK}.json", "w"), indent=1)
