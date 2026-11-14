"""2026-11-14 balance check under 0.0.137 rules: equal stats (every faction the Men's), 3 followers, NPC v NPC
damage x0.5, hits in the back x1.5 (modelled: a blow from an attacker the target isn't fighting comes from its rear
cone 120/360 of the time). Uses matchups2's fight (no hit turns, cap 4). python tools/balance/check137.py"""
import os, random, statistics, sys, heapq
sys.argv = sys.argv[:1]
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import matchups2 as M2  # noqa: E402

_m = M2.M.G["TIERS"]["Men"]
T = dict(_m, hp=dict(_m["hp"], rider=76), dmg=dict(_m["dmg"], rider=8.83))  # 0.0.135 riders: warrior x 2.24
G = M2.M.fight.__globals__  # sim105's constants (CONTACT, SPREAD, ...)
NPC, BACK, ARC = 0.5, 1.5, 120


last = [0.0]
_pop = heapq.heappop
def pop(q):
    it = _pop(q); last[0] = it[0]; return it
M2.heapq.heappop = pop

def fight(a, b, rnd, back):
    # Same as matchups2.fight, with the back-hit chance on blows from attackers the target isn't fighting.
    units = a + b; foes = {0: b, 1: a}; att = {id(u): 0 for u in units}; alive = [len(a), len(b)]; q = []
    for u in units: heapq.heappush(q, (rnd.uniform(0, 1) if u.archer else rnd.uniform(*G["CONTACT"]), rnd.random(), u))
    def pick(u):
        fs = [e for e in foes[u.side] if e.alive and att[id(e)] < M2.M.CAP]
        if not fs: return None
        low = min(att[id(e)] for e in fs); e = rnd.choice([e for e in fs if att[id(e)] == low]); att[id(e)] += 1; return e
    def hurt(e, dmg):
        e.hp -= dmg * rnd.uniform(1 - G["SPREAD"], 1 + G["SPREAD"])
        if e.hp <= 0 and e.alive:
            e.alive = False; alive[e.side] -= 1
            if e.target is not None: att[id(e.target)] -= 1
    while q and alive[0] and alive[1]:
        t, _, u = pop(q)
        if not u.alive: continue
        if u.archer:
            e = rnd.choice([x for x in foes[u.side] if x.alive])
            if rnd.random() < G["ARROW_HIT"]: hurt(e, u.dmg * NPC * (BACK if back and e.target is not u and rnd.random() < ARC / 360 else 1))
            heapq.heappush(q, (t + rnd.uniform(*G["ARROW_CD"]), rnd.random(), u)); continue
        if u.target is None or not u.target.alive:
            had = u.target is not None
            u.target = pick(u)
            heapq.heappush(q, (t + (rnd.uniform(*G["WALK"]) if had and u.target else 0.5 if u.target is None else 0), rnd.random(), u))
            if u.target is None or had: continue
        e = u.target
        if rnd.random() < G["MELEE_HIT"]:
            hurt(e, u.dmg * NPC * (BACK if back and e.target is not u and rnd.random() < ARC / 360 else 1))
        heapq.heappush(q, (t + rnd.uniform(*G["MELEE_CD"]), rnd.random(), u))
    return alive[0] > 0, alive[0], alive[1]

def run(ra, na, rb, nb, back, n=300):
    w = 0; ts = []; left = []
    for i in range(n):
        r = random.Random(i)
        won, x, y = fight([M2.R(0, T, ra) for _ in range(na)], [M2.R(1, T, rb) for _ in range(nb)], r, back)
        w += won; ts.append(last[0]); left.append(x if won else -y)
    return w / n, statistics.mean(ts)

print("## Same rank, same numbers (every faction alike now): win rate of side A, fight length")
for n in (3, 9, 30):
    for back in (False, True):
        w, t = run("warrior", n, "warrior", n, back); print(f"warriors {n} v {n}, back hits {'on ' if back else 'off'}: {w:.2f}, {t:.0f} s")
print("\n## Outnumbered (how much one or a few extra NPCs decide)")
for na, nb in ((3, 4), (9, 10), (9, 12), (27, 30)):
    for back in (False, True):
        w, t = run("warrior", na, "warrior", nb, back); print(f"warriors {na} v {nb}, back hits {'on ' if back else 'off'}: smaller side wins {w:.2f}")
print("\n## Rank against rank at equal coin (hire 5/15/40/15; riders a warrior plus the captain's promotion, 51)")
cost = {"recruit": 5, "warrior": 15, "captain": 40, "archer": 15, "rider": 51}
for ra, rb, coins in (("recruit", "warrior", 45), ("recruit", "warrior", 135), ("captain", "warrior", 120), ("archer", "warrior", 45), ("rider", "warrior", 102), ("rider", "captain", 120)):
    w, t = run(ra, round(coins / cost[ra]), rb, round(coins / cost[rb]), True)
    print(f"{round(coins / cost[ra])} {ra} v {round(coins / cost[rb])} {rb} ({coins} coin each): {ra} wins {w:.2f}")
print("\n## A full PvP side: 10 players' followings, 30 warriors v 30, and mixes")
mix = lambda: ["warrior"] * 18 + ["recruit"] * 6 + ["archer"] * 4 + ["captain"] * 2
w = 0; ts = []
for i in range(300):
    r = random.Random(i); a = [M2.R(0, T, k) for k in mix()]; b = [M2.R(1, T, k) for k in ["warrior"] * 30]
    w += fight(a, b, r, True)[0]; ts.append(last[0])
print(f"30 mixed (18 warriors, 6 recruits, 4 archers, 2 captains) v 30 warriors: mixed wins {w / 300:.2f}, {statistics.mean(ts):.0f} s")
print("\n## Per follower slot (each follower is 1/3 of a band whatever its rank): 3 v 3 and 30 v 30, back hits on")
ranks = ["recruit", "warrior", "archer", "captain", "rider"]
for n in (3, 30):
    print(f"{n} v {n}: row's win rate against the column")
    print("| | " + " | ".join(ranks) + " |\n" + "| --- " * (len(ranks) + 1) + "|")
    for ra in ranks:
        print(f"| {ra} | " + " | ".join("-" if ra == rb else f"{run(ra, n, rb, n, True, 200)[0]:.2f}" for rb in ranks) + " |")
