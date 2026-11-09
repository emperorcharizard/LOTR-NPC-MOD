"""lotrnpcs 0.0.105 combat balance simulator. Event-driven NPC-on-NPC fights with the current rules:
per-rank health/damage tables (FactionTiers, parsed from src/Factions.cs), +-20% hit spread, melee every 2.6-3.0 s (0.0.108; was 2.85-3.25)
(90% land), arrows every 3.1-3.9 s (70% land, shooting from the start while melee closes in 3-5 s), hit turns (0.0.98:
melee hits on one target at least 0.55-0.75 s apart, later swings wait their turn), vanilla 0.5 s invulnerability after
a hit (arrows that land inside it are lost; melee never does since turns are booked), at most CAP melee attackers per
target (0.0.102: 4), free enemies first, 0.8-1.5 s walk after a kill. Left out: terrain, formations/lines geometry,
knockback, armor (NPC damage tier 8-9 passes every rank armor), Combat Overhaul, player.
Usage: python3 -I sim105.py <Factions.cs> [runs]"""
import heapq, random, re, sys, json
from multiprocessing import Pool

SRC = sys.argv[1]; RUNS = int(sys.argv[2]) if len(sys.argv) > 2 else 1000
RANKS = ("recruit", "warrior", "captain", "archer")
TIERS = {}
for m in re.finditer(r'FactionTier (\w+) = T\("\w+", (\d+), (\d+), [\d.]+f, [\d.]+f, R\(([^)]*)\), R\(([^)]*)\), (\d+), (\d+), (\d+), (\d+)', open(SRC).read()):
    name, fol, ros, hp, dm, *price = m.groups()
    hp = [float(x.strip().rstrip("f")) for x in hp.split(",")]; dm = [float(x.strip().rstrip("f")) for x in dm.split(",")]
    TIERS[name] = dict(followers=int(fol), roster=int(ros), hp=dict(zip(RANKS, hp)), dmg=dict(zip(RANKS, dm)), price=dict(zip(RANKS, map(int, price))))
OLD_ORC = dict(TIERS["Orc"], hp=dict(zip(RANKS, (15, 27, 31, 22))), dmg=dict(zip(RANKS, (2.5, 2.8, 5.95, 2.45))))
# 0.0.108: melee cycle = cooldown 1.7-2.1 s + the 0.9 s swing (the 0.0.105/0.0.107 runs used 1.95-2.35 as the whole cycle, too fast)
MELEE_CD = (2.6, 3.0); ARROW_CD = (3.1, 3.9); MELEE_HIT, ARROW_HIT = 0.9, 0.7
WALK = (0.8, 1.5); CONTACT = (3.0, 5.0); SPREAD = 0.2; GAP = (0.55, 0.75); INVULN = 0.5; SHOVE = 3.0

def captains_for(roster): return max(1, int(roster / 8 + 0.25))

class U:
    __slots__ = ("side", "hp", "dmg", "archer", "target", "alive", "free_at", "hurt_at")
    def __init__(s, side, tier, rank):
        s.side = side; s.hp = tier["hp"][rank]; s.dmg = tier["dmg"][rank]; s.archer = rank == "archer"
        s.target = None; s.alive = True; s.free_at = 0.0; s.hurt_at = -9.0

def fight(a, b, rnd, cap=4, turns=True):
    units = a + b; foes = {0: b, 1: a}; att = {id(u): 0 for u in units}; alive = [len(a), len(b)]; q = []
    for u in units: heapq.heappush(q, (rnd.uniform(0, 1) if u.archer else rnd.uniform(*CONTACT), rnd.random(), u))
    def pick(u):
        fs = [e for e in foes[u.side] if e.alive and (cap <= 0 or att[id(e)] < cap)]
        if not fs: return None
        low = min(att[id(e)] for e in fs); e = rnd.choice([e for e in fs if att[id(e)] == low]); att[id(e)] += 1; return e
    def hurt(e, dmg, t):
        e.hp -= dmg * rnd.uniform(1 - SPREAD, 1 + SPREAD); e.hurt_at = t
        if e.hp <= 0 and e.alive:
            e.alive = False; alive[e.side] -= 1
            if e.target is not None: att[id(e.target)] -= 1
    while q and alive[0] and alive[1]:
        t, _, u = heapq.heappop(q)
        if not u.alive: continue
        if u.archer:
            e = rnd.choice([e for e in foes[u.side] if e.alive])
            if rnd.random() < ARROW_HIT and t - e.hurt_at >= INVULN: hurt(e, u.dmg, t)
            heapq.heappush(q, (t + rnd.uniform(*ARROW_CD), rnd.random(), u)); continue
        if u.target is None or not u.target.alive:
            had = u.target is not None
            if had: u.target = None
            u.target = pick(u)
            heapq.heappush(q, (t + (rnd.uniform(*WALK) if had and u.target else 0.5 if u.target is None else 0), rnd.random(), u))
            if u.target is None or had: continue
        e = u.target
        if turns and e.free_at > t:
            heapq.heappush(q, (e.free_at, rnd.random(), u)); continue
        if turns: e.free_at = t + rnd.uniform(*GAP)
        if rnd.random() < MELEE_HIT and (turns or t - e.hurt_at >= INVULN): hurt(e, u.dmg, t)
        heapq.heappush(q, (t + rnd.uniform(*MELEE_CD), rnd.random(), u))
    return alive[0] > 0, alive[0], alive[1]

def mixed(tier, n, rnd, archers=0.2):
    """A warband: captains up to the captain limit share (one per CaptainsFor/followers place, at least 1), then
    archers by share, the rest half warriors, half recruits."""
    caps = max(1, round(n * captains_for(tier["roster"]) / tier["roster"])) if n >= 3 else 0
    r = ["captain"] * caps
    for _ in range(n - caps): r.append("archer" if rnd.random() < archers else ("warrior" if rnd.random() < 0.5 else "recruit"))
    return r

def wild(tier, n, rnd):
    """Zone spawns: recruits, 35% warriors, 20% archers, captains about 1 in 25."""
    return ["captain" if rnd.random() < 0.04 else "archer" if rnd.random() < 0.2 else "warrior" if rnd.random() < 0.35 else "recruit" for _ in range(n)]

def buy(tier, coins, rnd):
    """Spend coins like a player would: the captain limit's worth of captains, then warriors/recruits/archers
    (20% archers) until the money runs out (roster capped)."""
    r = []; left = coins; caps = captains_for(tier["roster"])
    for _ in range(caps):
        if left - tier["price"]["captain"] >= coins / 2: r.append("captain"); left -= tier["price"]["captain"]
    while len(r) < tier["roster"]:
        k = "archer" if rnd.random() < 0.2 else ("warrior" if rnd.random() < 0.5 else "recruit")
        if left >= tier["price"][k]: r.append(k); left -= tier["price"][k]
        elif left >= tier["price"]["recruit"]: r.append("recruit"); left -= tier["price"]["recruit"]
        else: break
    return r

def job(args):
    kind, a, b, na, nb, seed, extra = args
    rnd = random.Random(seed); ta = OLD_ORC if a == "OldOrc" else TIERS[a]; tb = OLD_ORC if b == "OldOrc" else TIERS[b]
    wins = 0; left = 0
    for _ in range(RUNS):
        if kind == "rank": ra, rb = [extra] * na, [extra] * nb
        elif kind == "mixed": ra, rb = mixed(ta, na, rnd), mixed(tb, nb, rnd)
        elif kind == "coin": ra, rb = buy(ta, extra, rnd), buy(tb, extra, rnd)
        elif kind == "wild": ra, rb = wild(ta, na, rnd), wild(tb, nb, rnd)
        cap = 4 if kind != "nocap" else 0
        w, la, lb = fight([U(0, ta, r) for r in ra], [U(1, tb, r) for r in rb], rnd, cap=extra if kind == "wild" else 4)
        wins += w; left += la if w else -lb
    return wins / RUNS, left / RUNS

if __name__ == "__main__":
    names = list(TIERS); out = {}
    def matrix(kind, sizes, extra, label):
        jobs = [(kind, a, b, sizes(a), sizes(b), hash((label, a, b)) & 0xffffff, extra) for a in names for b in names if a != b]
        with Pool() as p: res = p.map(job, jobs)
        m = {(j[1], j[2]): r for j, r in zip(jobs, res)}
        print(f"\n== {label} ({RUNS} fights each): row's win rate against column; avg = average over the other six")
        print("         " + "".join(f"{c:>7}" for c in names) + "    avg")
        for a in names:
            row = [m[(a, b)][0] if a != b else None for b in names]
            avg = sum(x for x in row if x is not None) / (len(names) - 1)
            print(f"{a:>8} " + "".join("      -" if x is None else f"{x:7.2f}" for x in row) + f"  {avg:5.2f}")
        out[label] = {f"{a} v {b}": m[(a, b)][0] for a, b in m}
    F = lambda t: TIERS[t]["followers"]
    for r in RANKS: matrix("rank", F, r, f"Full followings, all {r}s")
    matrix("mixed", F, None, "Full followings, mixed (captain limit share, 20% archers)")
    matrix("mixed", lambda t: 10, None, "Equal headcount 10 v 10, mixed")
    matrix("coin", F, 150, "Equal coin: 150 coins each, bought like a player (captain limit, roster cap)")
    # The reported battle.
    print("\n== 100 wild Mordor orcs v 50 wild Haradrim (Men tier), zone-spawn mix")
    for lab, orc, cap in (("0.0.101 orc stats, no attacker cap", "OldOrc", 0), ("0.0.101 orc stats, cap 4", "OldOrc", 4), ("0.0.103+ orc stats, cap 4 (now)", "Orc", 4)):
        with Pool() as p: res = p.map(job, [("wild", orc, "Men", 100, 50, s, cap) for s in range(4)])
        w = sum(r[0] for r in res) / 4; l = sum(r[1] for r in res) / 4
        print(f"  {lab:<36} orcs win {w:.2f}   average survivors (+ orcs, - Haradrim) {l:+.1f}")
    for n in (50, 70, 85):
        with Pool() as p: res = p.map(job, [("wild", "Orc", "Men", n, 50, s, 4) for s in range(4)])
        print(f"  {n} orcs v 50 Haradrim (now)            orcs win {sum(r[0] for r in res)/4:.2f}")
    json.dump(out, open("results-0.0.105.json", "w"), indent=1)
