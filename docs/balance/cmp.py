import sys, random
sys.argv=["x","../work/lotrnpcs-handoff/lotrnpcs/src/Factions.cs","1000"]
import sim105 as S
from multiprocessing import Pool
names=list(S.TIERS)
def j(a):
    x,y,mode,orc=a
    rnd=random.Random(hash((x,y,mode))&0xffff); w=0
    tx = S.OLD_ORC if (x=="Orc" and orc=="old") else S.TIERS[x]; ty = S.OLD_ORC if (y=="Orc" and orc=="old") else S.TIERS[y]
    for _ in range(1000):
        ra=S.mixed(tx,tx["followers"],rnd); rb=S.mixed(ty,ty["followers"],rnd)
        r=S.fight([S.U(0,tx,k) for k in ra],[S.U(1,ty,k) for k in rb],rnd,cap=4 if mode=="now" else 5,turns=mode=="now")
        w+=r[0]
    return w/1000
for mode,orc,label in (("old","old","0.0.97 rules (no hit turns, cap 5), 0.0.91 orc stats"),("now","old","0.0.105 rules, 0.0.91 orc stats"),("now","new","0.0.105 rules, 0.0.103 orc stats (current)")):
    jobs=[(a,b,mode,orc) for a in names for b in names if a!=b]
    with Pool() as p: r=dict(zip([(a,b) for a,b,_,_ in jobs], p.map(j,jobs)))
    print(label+": average win rate of full mixed followings: "+", ".join(f"{a} {sum(r[(a,b)] for b in names if b!=a)/6:.2f}" for a in names))
