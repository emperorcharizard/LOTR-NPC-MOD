# lotrnpcs

LOTR NPC factions for Vintage Story 1.22.7: wild faction NPCs in admin spawn zones, hiring, orders and formations, battle lines, per-faction balance tiers.

Current version: 0.0.109 (see `tools/gen_testnpcs.py` and `docs/test-checklist.md`, sections 98-109). Builds 0.0.98 to 0.0.109 are not yet tested in game.

## Layout

- `src/` the mod DLL (C#, net10.0). Build: `cd src && dotnet build -c Release -o out`.
- `tools/gen_testnpcs.py` writes the entity, item, lang and modinfo JSON; `tools/bundle_lrc.py` copies the LRC race models used into `assets/lotrnpcs`.
- `tools/refs/` reference DLLs (VS API and VSEssentials 1.22.7 built from the open-source repos, Harmony 2.4.2).
- `tools/tests/factions`, `tools/tests/spawnzones` offline tests that load the generated entities.
- `tools/balance/` combat simulator and the fitting scripts behind the faction stats and hire prices.
- `docs/` design doc, config guide, test checklist, handoff notes.

## Build inputs not in this repo

Third-party and owner asset zips live in the Claude project files, not here: LRC 0.4.1 (bundled with the authors' permission, credits in `tools/CREDITS.txt`), FA Races 1.3.3 (seraph-faceless shape for the tests), haradrim-armor 0.1.3, gundabad-bone-armor 0.3.0.

## Packaging

1. `python3 -I tools/gen_testnpcs.py <gen>` then `python3 -I tools/bundle_lrc.py <lrc>/assets/lrc <gen>/assets/lotrnpcs`.
2. Copy `src/out/lotrnpcs.dll` and `tools/CREDITS.txt` next to `<gen>/modinfo.json`.
3. Zip `modinfo.json lotrnpcs.dll CREDITS.txt assets`.
