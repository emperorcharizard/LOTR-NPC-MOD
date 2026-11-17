# lotrnpcs

LOTR NPC factions for Vintage Story 1.22: wild faction NPCs in admin spawn zones, joining a faction, hiring, orders and formations, promotion, riders, equal-stat battles.

Current version: 1.0.3

**Guides:** players `docs/player-guide.md`, admins `docs/config-guide.md`, release notes `docs/release-notes.md`.

## Layout

- `src/` the mod DLL (C#, net10.0). Build: `cd src && dotnet build -c Release -o out`.
- `tools/gen_testnpcs.py` writes the entity, item, lang and modinfo JSON; `tools/bundle_lrc.py` copies the LRC race models used into `assets/lotrnpcs`.
- `tools/refs/` reference DLLs (VS API and VSEssentials 1.22.7 built from the open-source repos, Harmony 2.4.2).
- `tools/tests/factions`, `tools/tests/spawnzones` offline tests that load the generated entities.
- `tools/balance/` combat simulator and the fitting scripts behind the faction stats and hire prices.
- `docs/` design doc, config guide, test checklist, handoff notes.

## Build inputs

`build-inputs/` holds the asset zips the build reads: LRC 0.4.1 (bundled with the authors' permission, credits in `tools/CREDITS.txt`), and the owner's haradrim-armor 0.1.3 and gundabad-bone-armor 0.3.0 with their original files.

## Builds

Test zips (`lotrnpcs_<version>-test.zip`) and the official release zip (`lotrnpcs_<version>.zip`, `--release`) are built into `releases/` locally (ignored by git). Test builds up to 0.0.119 were also published as GitHub Releases.

## Notes and decisions

See `docs/README.md` for where everything is: decisions and their reasons (`docs/notes/`), the design review, balance results, per-version history and the test checklist.

## Building

Needs the .NET 10 SDK and Python 3 with Pillow (`pip install pillow`).

- `python tools/build.py` builds the DLL, generates and bundles the assets into `build/`, runs both offline test suites and writes `releases/lotrnpcs_<version>-test.zip`.
- `--no-zip` stops after the tests (what GitHub Actions runs on every push, `.github/workflows/test.yml`).
- `--publish` also uploads the zip as GitHub release `v<version>` (run `gh auth login` once first).
- `--release` names the zip without `-test` and adds `tools/modicon.png` and `LICENSE`; with `--publish` the release text is `docs/release-notes.md`.

Use plain `python`, not `python -I` (it hides user site-packages, where Pillow may be). The version is in `tools/gen_testnpcs.py` (`"version"` in modinfo).
