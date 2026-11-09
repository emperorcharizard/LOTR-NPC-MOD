"""0.0.108: per-rank hire prices for equal coin. For each rank, armies of only that rank bought with the same coins
(no roster or captain limit) should win about half their fights averaged over the other six tiers. Men's prices fixed;
whole-coin prices; coordinate search. Usage: python3 fitrankprice.py <Factions.cs> RUNS"""
import sys, random, json
SRC, RUNS = sys.argv[1], int(sys.argv[2]); sys.argv = [sys.argv[0], SRC, "1"]
import sim105 as S
from multiprocessing import Pool
names = list(S.TIERS)
BUD = {"recruit": range(25, 81, 5), "warrior": range(45, 241, 15), "archer": range(45, 241, 15), "captain": range(40, 201, 10)}
def rate(args):
    r, a, pa, b, pb, seed = args; rnd = random.Random(seed); w = n = 0
    for B in BUD[r]:
        na, nb = B // pa, B // pb
        if na == 0 or nb == 0: w += 0.5 * RUNS * (na == nb) + RUNS * (na > nb); n += RUNS; continue
        for _ in range(RUNS): w += S.fight([S.U(0, S.TIERS[a], r)] * 0 + [S.U(0, S.TIERS[a], r) for _ in range(na)], [S.U(1, S.TIERS[b], r) for _ in range(nb)], rnd)[0]; n += 1
    return w / n
pairs = [(a, b) for i, a in enumerate(names) for b in names[i + 1:]]
if __name__ == "__main__":
    out = {}
    with Pool() as pool:
        for r in S.RANKS:
            P = {t: S.TIERS[t]["price"][r] for t in names}
            def avgs(P, salt=0):
                res = dict(zip(pairs, pool.map(rate, [(r, a, P[a], b, P[b], hash((r, a, b, salt)) & 0xffffff) for a, b in pairs])))
                return {t: sum(res[(t, b)] if (t, b) in res else 1 - res[(b, t)] for b in names if b != t) / 6 for t in names}
            av = avgs(P); obj = sum((v - .5) ** 2 for v in av.values())
            for sweep in range(3):
                changed = False
                for t in names:
                    if t == "Men": continue
                    for d in (-1, 1):
                        T = dict(P); T[t] = max(1, P[t] + d); a2 = avgs(T); o2 = sum((v - .5) ** 2 for v in a2.values())
                        if o2 < obj: P, av, obj, changed = T, a2, o2, True
                if not changed: break
            chk = avgs(P, 1)
            print(r, {t: (P[t], round(chk[t], 2)) for t in names}, flush=True)
            out[r] = {t: {"price": P[t], "average": round(chk[t], 3)} for t in names}
    json.dump(out, open("fit-rankprices.json", "w"), indent=1)
