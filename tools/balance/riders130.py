"""0.0.130 rider stats: riders at (captain/warrior price)^exponent times a warrior's numbers against the warriors the same coin buys."""
import sys, os, random, runpy
HERE = os.path.dirname(os.path.abspath(__file__))
sys.argv = ["s", os.path.join(HERE, "..", "..", "src", "Factions.cs"), "1"]
g = runpy.run_path(os.path.join(HERE, "sim105.py"), run_name="sim")
U,fight,T=g["U"],g["fight"],g["TIERS"]
for ex in (0.82,):
  print("exponent",ex)
  for t in T:
    cp,wp=T[t]["price"]["captain"],T[t]["price"]["warrior"]; f=(cp/wp)**ex
    hp=round(T[t]["hp"]["warrior"]*f); dm=round(T[t]["dmg"]["warrior"]*f,2)
    X=dict(T[t],hp=dict(T[t]["hp"],captain=hp),dmg=dict(T[t]["dmg"],captain=dm))
    res=[]
    for nr in (2,4,8):
        nw=nr*cp/wp; lo=int(nw); fr=nw-lo; w=0;n=300
        for i in range(n):
            r=random.Random(i); k=lo+(1 if r.random()<fr else 0)
            w+=fight([U(0,X,"captain") for _ in range(nr)],[U(1,T[t],"warrior") for _ in range(k)],r,4)[0]
        res.append(round(w/n,2))
    print(f"  {t:7} price {cp}/{wp} f {f:.2f} rider {hp}/{dm} (warrior {T[t]['hp']['warrior']}/{T[t]['dmg']['warrior']}) win v equal coin 2/4/8 riders: {res}")
