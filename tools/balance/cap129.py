"""2026-11-14 balance check (docs/notes/balance-2026-11-14.md), sim105 rules on the shipped FactionTiers."""
import sys, os, random, runpy
HERE = os.path.dirname(os.path.abspath(__file__))
sys.argv = ["sim105.py", os.path.join(HERE, "..", "..", "src", "Factions.cs"), "1"]
g = runpy.run_path(os.path.join(HERE, "sim105.py"), run_name="sim")
U,fight,T=g["U"],g["fight"],g["TIERS"]
def run(na,ta,nb,tb,cap,n=300):
    w=sa=sb=0
    for i in range(n):
        r=random.Random(i); won,x,y=fight([U(0,T[ta],"warrior") for _ in range(na)],[U(1,T[tb],"warrior") for _ in range(nb)],r,cap); w+=won; sa+=x; sb+=y
    return round(w/n,2), round(sa/n,1), round(sb/n,1)
for cap in (1,2,3,4,0):
    print("cap",cap, {na: run(na,"Goblin",26,"Levy",cap) for na in (24,30,35,38,42)})
