# lotrnpcs

LOTR NPC factions for Vintage Story 1.22.7: wild faction NPCs in admin spawn zones, hiring, orders and formations, battle lines, per-faction balance tiers.

Current version: 0.0.112 (see `tools/gen_testnpcs.py` and `docs/test-checklist.md`, sections 98-112). Last in-game test: 0.0.109 on 2026-11-09 (results in `docs/notes/lotrnpcs-0109.md`).

## Layout

- `src/` the mod DLL (C#, net10.0). Build: `cd src && dotnet build -c Release -o out`.
- `tools/gen_testnpcs.py` writes the entity, item, lang and modinfo JSON; `tools/bundle_lrc.py` copies the LRC race models used into `assets/lotrnpcs`.
- `tools/refs/` reference DLLs (VS API and VSEssentials 1.22.7 built from the open-source repos, Harmony 2.4.2).
- `tools/tests/factions`, `tools/tests/spawnzones` offline tests that load the generated entities.
- `tools/balance/` combat simulator and the fitting scripts behind the faction stats and hire prices.
- `docs/` design doc, config guide, test checklist, handoff notes.

## Build inputs

`build-inputs/` holds the asset zips the build reads: LRC 0.4.1 (bundled with the authors' permission, credits in `tools/CREDITS.txt`), and the owner's haradrim-armor 0.1.3 and gundabad-bone-armor 0.3.0 with their original files.

## Test builds

Test zips are GitHub Releases (`v<version>`), built into `releases/` locally (ignored by git since 0.0.112; zips up to 0.0.111 are in git history).

## Notes and decisions

See `docs/README.md` for where everything is: decisions and their reasons (`docs/notes/`), the design review, balance results, per-version history and the test checklist.

## Building

Needs the .NET 10 SDK and Python 3 with Pillow (`pip install pillow`).

- `python tools/build.py` builds the DLL, generates and bundles the assets into `build/`, runs both offline test suites and writes `releases/lotrnpcs_<version>-test.zip`.
- `--no-zip` stops after the tests (what GitHub Actions runs on every push, `.github/workflows/test.yml`).
- `--publish` also uploads the zip as GitHub release `v<version>` (run `gh auth login` once first).

Use plain `python`, not `python -I` (it hides user site-packages, where Pillow may be). The version is in `tools/gen_testnpcs.py` (`"version"` in modinfo).
