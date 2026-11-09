"""Faction balance checks for the 0.0.90 design (run: python3 analyse.py [runs])."""
import sys, math, random, itertools
from sim2 import army, winrate, RANKS

RUNS = int(sys.argv[1]) if len(sys.argv) > 1 else 1000
WHAT = sys.argv[2] if len(sys.argv) > 2 else "all"

# tier: (max followers, roster, health x, damage x)
# The 0.0.90 shipped values (FactionTiers in src/Factions.cs). Override on the command line: Orc=0.66,0.85
TIERS = {
    "Men":    (5, 16, 1.00, 1.00),
    "Uruk":   (5, 15, 1.05, 1.03),
    "Levy":   (6, 19, 0.85, 0.85),
    "Orc":    (7, 22, 0.66, 0.85),
    "Goblin": (8, 26, 0.57, 0.73),
    "Elf":    (4, 13, 1.14, 1.35),
    "Dwarf":  (4, 13, 1.33, 1.15),
}
if len(sys.argv) > 3:  # overrides like Orc=0.70,0.85
    for spec in sys.argv[3:]:
        k, v = spec.split("=")
        h, d = map(float, v.split(","))
        n, r, _, _ = TIERS[k]
        TIERS[k] = (n, r, h, d)


def split(n, shares):
    """n units over ranks by largest remainder, deterministic."""
    counts = {k: int(n * f) for k, f in shares.items()}
    order = sorted(shares, key=lambda k: -(n * shares[k] - counts[k]))
    i = 0
    while sum(counts.values()) < n:
        counts[order[i % len(order)]] += 1
        i += 1
    return [k for k, c in counts.items() for _ in range(c)]


COMPS = {
    "mixed":   {"recruit": 0.4, "warrior": 0.4, "captain": 0.2},
    "recruits": {"recruit": 1.0},
    "veterans": {"warrior": 0.6, "captain": 0.4},
    "archers": {"recruit": 0.3, "warrior": 0.3, "captain": 0.15, "archer": 0.25},
}


def draw(rnd, n, comp):
    """n ranks drawn at random from the makeup's proportions (so every faction gets the same mix on average)."""
    ks, ws = zip(*COMPS[comp].items())
    return rnd.choices(ks, ws, k=n)


def band(tier, comp):
    n, _, h, d = TIERS[tier]
    return lambda rnd, side: army(side, draw(rnd, n, comp), h, d)


def full_band_matrix(comp):
    names = list(TIERS)
    print(f"\n== Full warbands, composition '{comp}' ({RUNS} fights each): row's win rate against column")
    print("        " + "".join(f"{c:>8}" for c in names))
    worst = []
    for a in names:
        row = []
        for b in names:
            if a == b:
                row.append("    -   ")
                continue
            ra, rb = band(a, comp), band(b, comp)
            w = winrate(lambda r: ra(r, 0), lambda r: rb(r, 1), RUNS, seed=hash((a, b, comp)) & 0xffff)
            row.append(f"{w:8.2f}")
            if a < b:
                worst.append((abs(w - 0.5), a, b, w))
        print(f"{a:>8}" + "".join(row))
    worst.sort(reverse=True)
    print("  furthest from even:", ", ".join(f"{a} v {b} {w:.2f}" for _, a, b, w in worst[:4]))


def wild_group(tier, rnd, side):
    """A wild group as SpawnZones.GroupRanks makes it, sizes scaled by MaxFollowers/5 (decision 4)."""
    n, _, h, d = TIERS[tier]
    s = n / 5
    avg, var, mx = 3 * s, 2 * s, round(5 * s)
    if rnd.random() < 0.12:
        lo, hi = 1, max(1, round(3 * s))
        ranks = ["captain"] + [("archer" if rnd.random() < 0.2 else ("warrior" if rnd.random() < 1 / 3 else "recruit")) for _ in range(rnd.randint(lo, hi))]
    else:
        # NatFloat gaussian over avg +- var: mean of 3 uniforms, as vanilla's GAUSSIAN
        size = avg + var * (sum(rnd.uniform(-1, 1) for _ in range(3)) / 3)
        count = int(size) + (1 if size - int(size) > rnd.random() else 0)
        count = max(1, min(mx, count))
        ranks = ["recruit"] + [("archer" if rnd.random() < 0.2 else ("warrior" if rnd.random() < 0.35 else "recruit")) for _ in range(count - 1)]
    return army(side, ranks, h, d)


def wild_matrix():
    names = list(TIERS)
    print(f"\n== Wild groups (sizes scaled, captain groups 12%), rival-group fights ({RUNS} each)")
    print("        " + "".join(f"{c:>8}" for c in names))
    for a in names:
        row = []
        for b in names:
            if a == b:
                row.append("    -   ")
                continue
            w = winrate(lambda r: wild_group(a, r, 0), lambda r: wild_group(b, r, 1), RUNS, seed=hash((a, b, "w")) & 0xffff)
            row.append(f"{w:8.2f}")
        print(f"{a:>8}" + "".join(row))


def mixes():
    print(f"\n== Mixed armies under follow shares ('mixed' ranks), against a full Men band ({RUNS} each)")
    men = band("Men", "mixed")
    cases = [
        ("3 Men + 2 Orc", [("Men", 3), ("Orc", 2)]),
        ("2 Elf + 2 Dwarf", [("Elf", 2), ("Dwarf", 2)]),
        ("2 Elf + 3 Orc", [("Elf", 2), ("Orc", 3)]),
        ("2 Dwarf + 4 Goblin", [("Dwarf", 2), ("Goblin", 4)]),
        ("1 Elf + 6 Goblin", [("Elf", 1), ("Goblin", 6)]),
        ("2 Uruk + 3 Men", [("Uruk", 2), ("Men", 3)]),
        ("3 Uruk + 2 Levy", [("Uruk", 3), ("Levy", 2)]),
        ("5 Men (control)", [("Men", 5)]),
        ("7 Orc (control)", [("Orc", 7)]),
        ("4 Men + 1 Elf", [("Men", 4), ("Elf", 1)]),
        ("3 Orc + 4 Goblin", [("Orc", 3), ("Goblin", 4)]),
    ]
    for name, parts in cases:
        share = sum(c / TIERS[t][0] for t, c in parts)
        def make(rnd, parts=parts):
            units = []
            for t, c in parts:
                n, _, h, d = TIERS[t]
                units += army(0, draw(rnd, c, "mixed"), h, d)
            return units
        w = winrate(make, lambda r: men(r, 1), RUNS, seed=hash(name) & 0xffff)
        print(f"  {name:<20} share {share:.2f}: wins {w:.2f}")


def per_rank():
    print("\n== Per-rank values (health / melee damage; archers: health / arrow)")
    for t, (n, r, h, d) in TIERS.items():
        cells = [f"{k} {max(1, round(hp * h))}/{round(dm * d, 1)}" for k, (hp, dm) in RANKS.items()]
        print(f"  {t:<7}" + "  ".join(cells))
    print("\n== Hits to kill, recruit attacker (row) on recruit / warrior / captain defender (column)")
    for a in TIERS:
        da = round(RANKS["recruit"][1] * TIERS[a][3], 1)
        cells = []
        for b in TIERS:
            hb = TIERS[b][2]
            cells.append(f"{b}:" + "/".join(str(math.ceil(max(1, round(RANKS[k][0] * hb)) / da)) for k in ("recruit", "warrior", "captain")))
        print(f"  {a:<7}" + " ".join(cells))


def player_threat():
    """A lone player fighting a group: damage taken ~ d * h * n(n+1)/2 (kills one at a time, the rest keep
    swinging). Relative to Men, for full bands and for the average wild group."""
    print("\n== Threat to a lone player (damage they take killing the whole group, Men = 1.00)")
    base = TIERS["Men"]
    def threat(n, h, d):
        return d * h * n * (n + 1) / 2
    m = threat(base[0], 1, 1)
    for t, (n, r, h, d) in TIERS.items():
        print(f"  {t:<7} full band {threat(n, h, d) / m:.2f}   incoming damage per second {n * d / base[0]:.2f}   total health {n * h / base[0]:.2f}")


if __name__ == "__main__":
    if WHAT in ("all", "ranks"):
        per_rank()
        player_threat()
    if WHAT in ("all", "bands"):
        for comp in COMPS:
            full_band_matrix(comp)
    if WHAT in ("all", "wild"):
        wild_matrix()
    if WHAT in ("all", "mix"):
        mixes()
