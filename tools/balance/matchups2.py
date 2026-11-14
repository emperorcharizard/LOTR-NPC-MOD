"""2026-11-14 matchup analysis, part 2 (see matchups.py for part 1 and the rules): full followings at x1/x2/x4,
riders standing and with a 0.0.133 charge model, wild zone groups and big battles. Same config and rules.
Usage: python tools/balance/matchups2.py [config.json] [runs] > out.md

Charge model (0.0.133 is untested in game, so these are assumptions): a charging rider's blow is a charge hit
(x2 damage, Riders.ChargeDamageMultiplier) CHARGE_SHARE of the time (the rest it is boxed in and stands); while it
rides out it is out of reach, so a blow at a rider lands EXPOSED of the time. Swing rate is unchanged.
"""
import heapq, os, random, statistics, sys
from multiprocessing import Pool

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import matchups as M  # noqa: E402  (loads the config and sim105)

G = M.fight.__globals__
CHARGE_SHARE, EXPOSED = 0.7, 0.5
RUNS = M.RUNS
FACS = sorted(f for f in M.FACTIONS if f != "shire")


class R(M.U):
    __slots__ = ("charge",)

    def __init__(s, side, tier, rank, charge=False):
        M.U.__init__(s, side, tier, rank); s.charge = charge


def fight(a, b, rnd, cap=M.CAP):
    """sim105.fight without hit turns (NpcHitsIgnoreHurtWindow), plus the charge model."""
    units = a + b; foes = {0: b, 1: a}; att = {id(u): 0 for u in units}; alive = [len(a), len(b)]; q = []
    CONTACT, WALK, MELEE_CD, ARROW_CD = G["CONTACT"], G["WALK"], G["MELEE_CD"], G["ARROW_CD"]
    MELEE_HIT, ARROW_HIT, SPREAD = G["MELEE_HIT"], G["ARROW_HIT"], G["SPREAD"]
    for u in units: heapq.heappush(q, (rnd.uniform(0, 1) if u.archer else rnd.uniform(*CONTACT), rnd.random(), u))
    def pick(u):
        fs = [e for e in foes[u.side] if e.alive and (cap <= 0 or att[id(e)] < cap)]
        if not fs: return None
        low = min(att[id(e)] for e in fs); e = rnd.choice([e for e in fs if att[id(e)] == low]); att[id(e)] += 1; return e
    def hurt(e, dmg):
        e.hp -= dmg * rnd.uniform(1 - SPREAD, 1 + SPREAD)
        if e.hp <= 0 and e.alive:
            e.alive = False; alive[e.side] -= 1
            if e.target is not None: att[id(e.target)] -= 1
    while q and alive[0] and alive[1]:
        t, _, u = heapq.heappop(q)
        if not u.alive: continue
        if u.archer:
            e = rnd.choice([e for e in foes[u.side] if e.alive])
            if rnd.random() < ARROW_HIT and not (getattr(e, "charge", False) and rnd.random() > EXPOSED): hurt(e, u.dmg)
            heapq.heappush(q, (t + rnd.uniform(*ARROW_CD), rnd.random(), u)); continue
        if u.target is None or not u.target.alive:
            had = u.target is not None
            u.target = pick(u)
            heapq.heappush(q, (t + (rnd.uniform(*WALK) if had and u.target else 0.5 if u.target is None else 0), rnd.random(), u))
            if u.target is None or had: continue
        e = u.target
        if rnd.random() < MELEE_HIT and not (getattr(e, "charge", False) and rnd.random() > EXPOSED):
            hurt(e, u.dmg * (2 if getattr(u, "charge", False) and rnd.random() < CHARGE_SHARE else 1))
        heapq.heappush(q, (t + rnd.uniform(*MELEE_CD), rnd.random(), u))
    return alive[0] > 0, alive[0], alive[1]


def band(fac, rank, n, side, charge=False):
    return [R(side, M.FACTIONS[fac], rank, charge) for _ in range(n)]


def group(fac, rnd, side, scale_mult=1):
    """SpawnZoneSystem.GroupRanks with the owner's spawning file (avg 3 +- 2, max 5, warriors 0.35, archers 0.2,
    captain groups 0.12 with 1 to 3 escort, a rider at 0.06), scaled by MaxFollowers / 5."""
    sc = M.FACTIONS[fac]["followers"] / 5
    def member(ws):
        if rnd.random() < 0.2: return "archer"
        return "warrior" if rnd.random() < ws else "recruit"
    if rnd.random() < 0.12:
        ranks = ["captain"] + [member(1 / 3) for _ in range(rnd.randint(1, max(1, round(3 * sc))))]
    else:
        size = max(1.0, rnd.gauss(3 * sc, 2 * sc / 2))
        n = min(max(1, int(size) + (1 if size - int(size) > rnd.random() else 0)), max(1, round(5 * sc)))
        ranks = ["recruit"] + [member(0.35) for _ in range(n - 1)]
    if "warrior" in ranks and rnd.random() < 0.06: ranks[ranks.index("warrior")] = "rider"
    return [R(side, M.FACTIONS[fac], r, r == "rider") for r in ranks]


def job(args):
    kind, fa, fb, rank, mult, charge, seed = args
    w = 0
    for i in range(RUNS):
        rnd = random.Random(seed * 1000003 + i)
        if kind == "band":
            a = band(fa, rank, M.FACTIONS[fa]["followers"] * mult, 0, charge); b = band(fb, rank, M.FACTIONS[fb]["followers"] * mult, 1, charge)
        elif kind == "group":
            a = sum((group(fa, rnd, 0) for _ in range(mult)), []); b = sum((group(fb, rnd, 1) for _ in range(mult)), [])
        else:  # riders v warriors at equal coin
            coins = mult
            na = coins / M.FACTIONS[fa]["cost"]["rider"]; nb = coins / M.FACTIONS[fb]["cost"]["warrior"]
            ka = max(1, int(na) + (rnd.random() < na - int(na))); kb = max(1, int(nb) + (rnd.random() < nb - int(nb)))
            a = band(fa, "rider", ka, 0, charge); b = band(fb, "warrior", kb, 1)
        w += fight(a, b, rnd)[0]
    return (kind, fa, fb, rank, mult, charge, w / RUNS)


def main():
    jobs = []; seed = 1
    pairs = [(a, b) for i, a in enumerate(FACS) for b in FACS[i + 1:]]
    for rank in ("recruit", "warrior", "captain", "archer", "rider"):
        for mult in (1, 2, 4):
            for a, b in pairs: jobs.append(("band", a, b, rank, mult, False, seed)); seed += 1
    for mult in (1, 2, 4):
        for a, b in pairs: jobs.append(("band", a, b, "rider", mult, True, seed)); seed += 1
    for mult in (1, 4):  # one wild group each, and big battles of 4 groups
        for a, b in pairs: jobs.append(("group", a, b, None, mult, False, seed)); seed += 1
    for coins in (102, 255):
        for a in FACS:
            for charge in (False, True): jobs.append(("rvw", a, a, None, coins, charge, seed)); seed += 1
    with Pool() as p: res = p.map(job, jobs, chunksize=8)
    def avg(kind, rank, mult, charge):
        d = {}
        for k, fa, fb, r, m, c, w in res:
            if k == kind and r == rank and m == mult and c == charge:
                d.setdefault(fa, []).append(w); d.setdefault(fb, []).append(1 - w)
        return {f: statistics.mean(v) for f, v in d.items()}
    def mark(v): return f"**{v:.2f}**" if v < 0.4 or v > 0.6 else f"{v:.2f}"
    print(f"Runs per pairing: {RUNS}. Factions: {len(FACS)} (the Shire left out: hobbits don't fight in bands). Bold: outside 0.40-0.60.\n")
    print("## 4. Full followings, same rank, at x1, x2 and x4 size: each faction's average win rate\n")
    cols = [(r, m, False) for r in ("recruit", "warrior", "captain", "archer", "rider") for m in (1, 2, 4)] + [("rider", m, True) for m in (1, 2, 4)]
    A = {c: avg("band", *c) for c in cols}
    print("| faction | " + " | ".join(f"{r}{' charge' if c else ''} x{m}" for r, m, c in cols) + " |\n" + "| --- " * (len(cols) + 1) + "|")
    for f in FACS: print(f"| {f} | " + " | ".join(mark(A[c][f]) for c in cols) + " |")
    print("\n## 5. Wild zone groups: one group against one, and big battles of 4 groups a side\n")
    g1, g4 = avg("group", None, 1, False), avg("group", None, 4, False)
    print("| faction | one group | 4 groups |\n| --- | --- | --- |")
    for f in sorted(FACS, key=lambda f: -g4[f]): print(f"| {f} | {mark(g1[f])} | {mark(g4[f])} |")
    print("\n## 6. Riders against warriors of their own faction at equal coin (2 and 5 riders' worth), standing and with the charge model\n")
    print("| faction | rider cost | 2 riders' coin, standing | with charge | 5 riders' coin, standing | with charge |\n| --- | --- | --- | --- | --- | --- |")
    rv = {(fa, m, c): w for k, fa, fb, r, m, c, w in res if k == "rvw"}
    for f in FACS:
        cost = M.FACTIONS[f]["cost"]["rider"]
        print(f"| {f} | {cost} | " + " | ".join(mark(rv[(f, coins, c)]) for coins in (102, 255) for c in (False, True)) + " |")
    print("\n(The coin for 6 is 102 and 255: 2 and 5 Men riders at 51 each; other factions get as many riders and warriors as that coin buys, fractions rounded at random.)")


if __name__ == "__main__":
    main()
