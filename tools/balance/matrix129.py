"""2026-11-14 balance check (docs/notes/balance-2026-11-14.md), sim105 rules on the shipped FactionTiers."""
import sys, os, random, runpy
HERE = os.path.dirname(os.path.abspath(__file__))
sys.argv = ["sim105.py", os.path.join(HERE, "..", "..", "src", "Factions.cs"), "1"]
g = runpy.run_path(os.path.join(HERE, "sim105.py"), run_name="sim")
U,fight,T=g["U"],g["fight"],g["TIERS"]
def run(na,ta,nb,tb,rank="warrior",n=400,cap=4):
    w=0;sa=sb=0
    for i in range(n):
        r=random.Random(i); a=[U(0,T[ta],rank) for _ in range(na)]; b=[U(1,T[tb],rank) for _ in range(nb)]
        won,x,y=fight(a,b,r,cap); w+=won; sa+=x; sb+=y
    return w/n, sa/n, sb/n
for na in (24,28,32,38,42):
    print(f"{na} goblin v 26 levy: goblin wins {run(na,'Goblin',26,'Levy')[0]:.2f}, survivors {run(na,'Goblin',26,'Levy')[1:]}")
for na in (8,16): print(f"{na} goblin v {na*5//8 if na>8 else 5} levy:",run(na,'Goblin',na*5//8 if na>8 else 5,'Levy'))
print("--- equal coins (warriors only, 180 coins), row win rate vs column")
names=[k for k in ("Men","Uruk","Levy","Orc","Goblin","Elf","Dwarf") if k in T]
cnt={k:max(1,round(180/T[k]["price"]["warrior"])) for k in names}
print("counts",cnt)
print("      "+" ".join(f"{k[:5]:>6}" for k in names)+"   avg")
for a in names:
    rs=[run(cnt[a],a,cnt[b],b,n=200)[0] for b in names if b!=a]
    print(f"{a[:6]:6}"+" ".join(f"{(run(cnt[a],a,cnt[b],b,n=200)[0] if b!=a else float('nan')):6.2f}" for b in names)+f"  {sum(rs)/len(rs):.2f}")
for mult in (1,4):
    print(f"--- same-rank warriors, following sizes x{mult}")
    cnt={k:T[k]["followers"]*mult for k in names}
    print("counts",cnt)
    for a in names:
        rs=[run(cnt[a],a,cnt[b],b,n=300)[0] for b in names if b!=a]
        print(f"{a[:6]:6}"+" ".join(f"{(run(cnt[a],a,cnt[b],b,n=300)[0] if b!=a else float('nan')):6.2f}" for b in names)+f"  {sum(rs)/len(rs):.2f}")
