# Usage: python3 -I shieldmeasure.py <tools/preview> <seraph-faceless.json> <orc_shield.json> <tools/preview> '<tpOffHandTransform json>'
import json, sys
sys.path.insert(0, sys.argv[4])
from shieldfit import *
tf = json.loads(sys.argv[5])
for mt, ma in ((0, 0), (1.0, 0.5), (1.5, 0.5)):
    d = depth(tf, mt / 16, ma / 16)
    print(f"armor margin torso/legs {mt}px arm {ma}px: worst {max(d):.2f}px; poses over 0.2px:",
          [f"{POSES[i][0]}@{POSES[i][1]}:{v:.1f}" for i, v in enumerate(d) if v > 0.2][:14])
