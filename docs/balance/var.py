import sys, random
sys.argv=["x","../work/lotrnpcs-handoff/lotrnpcs/src/Factions.cs","600"]
import sim105 as S
from multiprocessing import Pool
names=list(S.TIERS)
def j(a):
    x,y,orc,kind=a
    rnd=random.Random(hash((x,y,kind))&0xffff); w=0
    tx = orc if x=="Orc" else S.TIERS[x]; ty = orc if y=="Orc" else S.TIERS[y]
    for _ in range(600):
        if kind=="mixed": ra=S.mixed(tx,tx["followers"],rnd); rb=S.mixed(ty,ty["followers"],rnd)
        else: ra=[kind]*tx["followers"]; rb=[kind]*ty["followers"]
        w+=S.fight([S.U(0,tx,k) for k in ra],[S.U(1,ty,k) for k in rb],rnd)[0]
    return w/600
O=S.TIERS["Orc"]
mid=dict(O, hp=dict(zip(S.RANKS,(17,29,36,26))), dmg=dict(zip(S.RANKS,(2.6,3.2,6.1,2.6))))
for label,orc in (("now, 7 followers",O),("0.0.103 stats, 6 followers",dict(O,followers=6)),("0.0.103 stats, 5 followers",dict(O,followers=5)),("halfway stats (~0.67), 7 followers",mid),("0.0.91 stats, 7 followers",S.OLD_ORC)):
    res=[]
    for kind in ("mixed","recruit","warrior","captain"):
        jobs=[("Orc",b,orc,kind) for b in names if b!="Orc"]
        with Pool() as p: r=p.map(j,jobs)
        res.append(f"{kind} {sum(r)/6:.2f}")
    print(f"Orc {label}: avg win " + ", ".join(res))
