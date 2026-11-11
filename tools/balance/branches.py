"""Branching promotion design check (2026-11-10, docs/design-roster-promotion.md): what stats make two proposed ranks
worth their price, per faction, under sim105's fight rules.
- veteran: the warrior's other path (warrior -> captain or veteran). Damage = warrior's x VET_DMG; finds the health
  multiplier on the warrior's health at which veterans bought with the same coin as warriors win half (price = warrior
  price x PRICE_MULT, rounded).
- marksman: the archer's next rank. Health = archer's; finds the arrow damage multiplier at which marksmen bought with
  the same coin as archers win half, both behind an equal-coin screen of the faction's warriors.
Budgets 60 to 240 coins (no roster or captain limit), common random numbers, bisection.
Usage: python tools/balance/branches.py src/Factions.cs RUNS"""
import sys, random
import os; sys.argv = os.environ.setdefault("LOTRNPCS_BALANCE_ARGV", "\n".join(sys.argv)).split("\n")  # Windows pool workers re-run this with the argv edited below; keep the real one
SRC, RUNS = sys.argv[1], int(sys.argv[2]); sys.argv = [sys.argv[0], SRC, "1"]
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim105 as S
from multiprocessing import Pool

PRICE_MULT, VET_DMG = 5 / 3, 1.15
BUDGETS = tuple(range(60, 241, 30))

def units(side, n, hp, dmg, archer=False):
    out = []
    for _ in range(n):
        u = S.U(side, {"hp": {"x": hp}, "dmg": {"x": dmg}}, "x"); u.archer = archer; out.append(u)
    return out

def veteran_rate(t, m, seed):
    T = S.TIERS[t]; p = T["price"]["warrior"]; vp = round(p * PRICE_MULT); hp, dmg = T["hp"]["warrior"], T["dmg"]["warrior"]
    rnd = random.Random(seed); w = n = 0
    for B in BUDGETS:
        for _ in range(RUNS):
            w += S.fight(units(0, B // vp, hp * m, dmg * VET_DMG), units(1, B // p, hp, dmg), rnd)[0]; n += 1
    return w / n

def marksman_rate(t, m, seed):
    T = S.TIERS[t]; p = T["price"]["archer"]; mp = round(p * PRICE_MULT); wp = T["price"]["warrior"]
    hp, dmg, whp, wdmg = T["hp"]["archer"], T["dmg"]["archer"], T["hp"]["warrior"], T["dmg"]["warrior"]
    rnd = random.Random(seed); w = n = 0
    for B in BUDGETS:
        screen = (B // 2) // wp
        for _ in range(RUNS):
            a = units(0, screen, whp, wdmg) + units(0, (B - B // 2) // mp, hp, dmg * m, True)
            b = units(1, screen, whp, wdmg) + units(1, (B - B // 2) // p, hp, dmg, True)
            w += S.fight(a, b, rnd)[0]; n += 1
    return w / n

SIDEGRADES = {"guard (health x1.25, damage x0.8)": (1.25, 0.8), "shock (health x0.8, damage x1.25)": (0.8, 1.25), "guard (health x1.15, damage x0.87)": (1.15, 0.87), "shock (health x0.87, damage x1.15)": (0.87, 1.15)}

def sidegrade_rate(args):
    """A same-price sidegrade of the warrior (health x h, damage x d) against plain warriors, equal coin."""
    t, h, d = args; T = S.TIERS[t]; p = T["price"]["warrior"]; hp, dmg = T["hp"]["warrior"], T["dmg"]["warrior"]
    rnd = random.Random(777); w = n = 0
    for B in BUDGETS:
        for _ in range(RUNS):
            w += S.fight(units(0, B // p, hp * h, dmg * d), units(1, B // p, hp, dmg), rnd)[0]; n += 1
    return w / n

def solve(args):
    kind, t = args; f = veteran_rate if kind == "veteran" else marksman_rate; lo, hi = 1.0, 2.6
    for _ in range(9):
        mid = (lo + hi) / 2
        if f(t, mid, 12345) < 0.5: lo = mid
        else: hi = mid
    m = (lo + hi) / 2
    return kind, t, m, f(t, m, 999)

if __name__ == "__main__":
    names = list(S.TIERS)
    with Pool() as pool: res = pool.map(solve, [(k, t) for k in ("veteran", "marksman") for t in names])
    for kind in ("veteran", "marksman"):
        print(f"\n== {kind}: price = {'warrior' if kind == 'veteran' else 'archer'} price x {PRICE_MULT:.2f}, rounded; "
              + ("damage x %.2f, health x m" % VET_DMG if kind == "veteran" else "health = archer's, arrow damage x m")
              + f"; win rate at m on fresh fights ({RUNS} per budget)")
        for k, t, m, check in res:
            if k != kind: continue
            T = S.TIERS[t]; base = "warrior" if kind == "veteran" else "archer"; price = round(T["price"][base] * PRICE_MULT)
            hp = T["hp"][base] * (m if kind == "veteran" else 1); dmg = T["dmg"][base] * (VET_DMG if kind == "veteran" else m)
            print(f"  {t:7} m {m:.2f}  health {hp:5.1f}  damage {dmg:5.2f}  price {price:3}   check {check:.2f}")
    with Pool() as pool:
        side = pool.map(sidegrade_rate, [(t, h, d) for h, d in SIDEGRADES.values() for t in names])
    print(f"\n== same-price warrior sidegrades against plain warriors, equal coin ({RUNS} per budget)")
    for i, label in enumerate(SIDEGRADES):
        print(f"  {label:34}" + "  ".join(f"{t} {side[i * len(names) + j]:.2f}" for j, t in enumerate(names)))
