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

## Build inputs

`build-inputs/` holds the asset zips the build reads: LRC 0.4.1 (bundled with the authors' permission, credits in `tools/CREDITS.txt`; this repo is private), FA Races 1.3.3 (seraph-faceless shape for the tests), and the owner's haradrim-armor 0.1.3 and gundabad-bone-armor 0.3.0 with their original files.

## Test builds

`releases/` holds the latest test zip (now 0.0.109). Older zips stay in the Claude project files.

## Notes and decisions

See `docs/README.md` for where everything is: decisions and their reasons (`docs/notes/`), the design review, balance results, per-version history and the test checklist.

## Packaging

1. `python3 -I tools/gen_testnpcs.py <gen>` then `python3 -I tools/bundle_lrc.py <lrc>/assets/lrc <gen>/assets/lotrnpcs`.
2. Copy `src/out/lotrnpcs.dll` and `tools/CREDITS.txt` next to `<gen>/modinfo.json`.
3. Zip `modinfo.json lotrnpcs.dll CREDITS.txt assets`.
