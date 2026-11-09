---
name: lotrnpcs-base-0063
description: lotrnpcs 0.0.63 handoff base (from the user's other account): where it lives, source hash, how a thread rebuilds it, verified build results, Gundabad armor mod conflict
metadata:
  type: project
---

- 2026-11-04: user brought the 0.0.63 handoff bundle from their other Claude account. It is newer than this project's own 0.0.57 in /mnt/project-files/lotrnpcs/ (left untouched, now stale).
- Base: /mnt/project-files/lotrnpcs-base-0.0.63/lotrnpcs-handoff.zip (11.7 MB). Inside: HANDOFF.md (full briefing, section 3 Summary = current state, section 9 = open questions), THREADS.md (thread procedure + round log), lotrnpcs/ source + dist/lotrnpcs_0.0.63-test.zip, docs/, memory/, build-inputs/ (lrc-0.4.1, FA Races 1.3.3, gundabad-bone-armor 0.1.0 and fixed 0.1.1).
- Source hash at base: e390cc2f6860143e (THREADS.md method). Later changes go in a source.patch against this base (none yet).
- Verified 2026-11-04 in this sandbox with no changes: plain `apt-get install dotnet-sdk-10.0` works (10.0.114, no proxy tweak needed). DLL builds 0 warnings; factions 367 PASS 0 FAIL; spawn zones ALL PASSED; regenerated assets match the shipped 0.0.63 zip except PNG bytes (none differ here).
- seraph-faceless.json in the 0.0.63 zip is NOT a slip: bundle_lrc copies LRC's body, and every bundled race uses ShapePath lotrnpcs:entity/humanoid/seraph-faceless. HANDOFF section 7's factions-test step unzips FA's copy over it and says to delete it: that would ship orcs with no body. Put FA's copy somewhere else or restore LRC's before packaging.
- GUNDABAD CONFLICT: 0.0.62/0.0.63 reference the other account's mod `gundabad` (gundabad-bone-armor 0.1.1, body + legs, codes gundabad:armor-body-tierthreebrig-gundabadbonearmor-..., gundabad:armor-legs-tierthreebrig-gundabadboneleggings-...). This project's own `gundabadarmor` 0.1.0 (/mnt/project-files/gundabadarmor/, body only, tierthreechain) is a different mod; lotrnpcs does not reference it. RESOLVED (user, 2026-11-04, re-sent gundabad-bone-armor-0.1.1.zip and lotrnpcs_0.0.63-test.zip, both byte-identical to the bundle's): "this is the latest version". gundabad 0.1.1 is the real armor mod; this project's gundabadarmor 0.1.0 is superseded.
- Next open question per HANDOFF: in-game test of 0.0.63 Gundabad (bone armor fit on all ranks with 0.1.1, lighter skins orcskin 2/1/7; respawn old Gundabad orcs).

Related: [[lotrnpcs-0058-0063]], [[lotrnpcs-build]], [[lotrnpcs-looks]]


- 0.0.65 onward: see [[lotrnpcs-0065-0071]].
- Gundabad armor 0.1.2-0.2.0, 0.0.64, render tool notes: see [[gundabad-armor-fixes]].
