"""Event-driven fight simulator for lotrnpcs faction balance (0.0.90 design).

Models what the mod does in an NPC-on-NPC fight (0.0.89 values):
- rank health and damage (gen RANK_STATS), times the faction's health and damage multipliers;
  health rounded to a whole number, damage left exact (the planned rule);
- melee swings every 1.95 to 2.35 s (gen meleeattack), MELEE_HIT of them landing, each times a random 1 +- SPREAD;
- archers: arrow every 3.1 to 3.9 s (1.1 s draw + 2 to 2.8 s, gen shootattack), ARROW_HIT landing,
  shooting from the start while melee walks in (CONTACT seconds);
- up to INNER (5) melee attackers on one target (inner ring, 0.0.87), the rest wait;
- a melee fighter takes an enemy with the fewest attackers first (0.0.87 seek tiers);
- after its target dies, a 0.8 to 1.5 s walk to the next.
Left out: terrain, formations, the player, knockback, Combat Overhaul (NPC-on-NPC damage is raw).
"""
import heapq, random

RANKS = {"recruit": (22, 3.0), "warrior": (34, 4.0), "captain": (48, 7.0), "archer": (34, 3.0)}
MELEE_CD = (1.95, 2.35)
ARROW_CD = (3.1, 3.9)
MELEE_HIT, ARROW_HIT = 0.9, 0.7
INNER = 5
WALK = (0.8, 1.5)
CONTACT = (3.0, 5.0)   # melee reaches the enemy after this many seconds (archers shoot meanwhile)
ARCHER_SHOVE = 3.0     # an archer in melee shoves for a recruit's damage (gen: "a recruit's shove")
SPREAD = 0.0           # 0.0.90 follow-up (owner, 2026-11-05: "add a +-20% spread on hits"): each hit x U(1-SPREAD, 1+SPREAD)


class Unit:
    __slots__ = ("side", "hp", "dmg", "shove", "archer", "target", "alive")

    def __init__(self, side, rank, hpm, dmgm):
        hp, d = RANKS[rank]
        self.side, self.archer = side, rank == "archer"
        self.hp = max(1, round(hp * hpm))
        self.dmg = d * dmgm
        self.shove = ARCHER_SHOVE * dmgm
        self.target, self.alive = None, True


def army(side, ranks, hpm, dmgm):
    return [Unit(side, r, hpm, dmgm) for r in ranks]


def fight(a, b, rnd):
    """True if side a wins (has units left)."""
    units = a + b
    enemies = {0: b, 1: a}
    attackers = {id(u): 0 for u in units}  # melee attackers on each unit
    q = []
    for u in units:
        t = rnd.uniform(0, 1.0) if u.archer else rnd.uniform(*CONTACT)
        heapq.heappush(q, (t, rnd.random(), u))
    alive = [len(a), len(b)]

    def pick_melee(u):
        foes = [e for e in enemies[u.side] if e.alive and attackers[id(e)] < INNER]
        if not foes:
            return None
        low = min(attackers[id(e)] for e in foes)
        e = rnd.choice([e for e in foes if attackers[id(e)] == low])
        attackers[id(e)] += 1
        return e

    while q and alive[0] and alive[1]:
        t, _, u = heapq.heappop(q)
        if not u.alive:
            continue
        if u.archer:
            foes = [e for e in enemies[u.side] if e.alive]
            e = rnd.choice(foes)
            dmg = u.dmg if rnd.random() < ARROW_HIT else 0
            nxt = t + rnd.uniform(*ARROW_CD)
        else:
            if u.target is None or not u.target.alive:
                if u.target is not None:
                    u.target = None
                    e = pick_melee(u)
                    u.target = e
                    heapq.heappush(q, (t + rnd.uniform(*WALK), rnd.random(), u))
                    continue
                u.target = pick_melee(u)
                if u.target is None:  # waiting in the outer ring
                    heapq.heappush(q, (t + 0.5, rnd.random(), u))
                    continue
            e = u.target
            dmg = u.dmg if rnd.random() < MELEE_HIT else 0
            nxt = t + rnd.uniform(*MELEE_CD)
        if dmg:
            if SPREAD: dmg *= rnd.uniform(1 - SPREAD, 1 + SPREAD)
            e.hp -= dmg
            if e.hp <= 0 and e.alive:
                e.alive = False
                alive[e.side] -= 1
                if e.target is not None:
                    attackers[id(e.target)] -= 1
                # its attackers free their place on it
                for o in enemies[e.side]:
                    if o.alive and o.target is e:
                        attackers[id(e)] -= 1
        heapq.heappush(q, (nxt, rnd.random(), u))
    return alive[0] > 0


def winrate(make_a, make_b, runs=2000, seed=1):
    rnd = random.Random(seed)
    wins = 0
    for _ in range(runs):
        wins += fight(make_a(rnd), make_b(rnd), rnd)
    return wins / runs
