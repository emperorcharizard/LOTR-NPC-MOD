"""0.0.138 bands (3 ranked places + 2 recruit places, 1 captain): 10 players a side, check137's rules (equal stats,
NPC v NPC x0.5, back hits). python tools/balance/bands138.py"""
import io, contextlib, os, random, statistics, sys
sys.argv = sys.argv[:1]; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
with contextlib.redirect_stdout(io.StringIO()):
    import check137 as C
BANDS = {
    "captain+2 warriors+2 recruits": [("captain", 1), ("warrior", 2), ("recruit", 2)],
    "3 warriors+2 recruits": [("warrior", 3), ("recruit", 2)],
    "3 riders+2 recruits": [("rider", 3), ("recruit", 2)],
    "captain+2 riders+2 recruits": [("captain", 1), ("rider", 2), ("recruit", 2)],
    "5 recruits": [("recruit", 5)],
    "old: captain+2 warriors": [("captain", 1), ("warrior", 2)],
    "old: 3 warriors": [("warrior", 3)],
    "old: 3 riders": [("rider", 3)],
}
def army(spec, side, players): return [C.M2.R(side, C.T, r) for r, n in spec for _ in range(n * players)]
def run(a, b, players=10, n=200):
    w = 0; ts = []
    for i in range(n):
        rnd = random.Random(i); w += C.fight(army(BANDS[a], 0, players), army(BANDS[b], 1, players), rnd, True)[0]; ts.append(C.last[0])
    return w / n, statistics.mean(ts)
names = list(BANDS)
print("Row's win rate against the column, 10 players a side (average fight length in brackets, from contact)\n")
print("| | " + " | ".join(names) + " |\n" + "| --- " * (len(names) + 1) + "|")
for a in names:
    cells = []
    for b in names:
        if a == b: w, t = run(a, b); cells.append(f"{w:.2f} ({t:.0f} s)")
        else: w, t = run(a, b); cells.append(f"{w:.2f}")
    print(f"| {a} | " + " | ".join(cells) + " |")
