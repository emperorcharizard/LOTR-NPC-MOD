# LOTR NPC mod (lotrnpcs) for Vintage Story

The owner works with Claude from more than one Claude account. Everything a session needs lives in this repo, not in one account's private memory, so any account can pick up where the last one stopped.

## Start here
1. `docs/notes/resume.md`: where the work stands and what's next. Read it first, and update it before you stop.
2. `docs/test-checklist.md`: the in-game test list. Tick items (`- [x]`) as they pass.
3. `docs/handover/`: battle-log CSVs, the owner's mod configs (DebugCommands on), and the permissions a new account needs (README).
4. `docs/notes/MEMORY.md`: the index of decision and per-version notes. Append test results and build notes to the running version note, `docs/notes/lotrnpcs-0113.md` (it covers 0.0.113 to 0.0.120).

## Claude plugins
`.claude/settings.json` turns on the Ponytail plugin (github DietrichGebert/ponytail: smallest change that works, plus `/ponytail-review` and `/ponytail-audit`). Claude Code asks to trust the marketplace the first time; `/ponytail off` or `/ponytail lite` changes it for a session.

## Build
`python tools/build.py` builds the DLL, generates assets, runs the offline tests (factions, spawnzones) and writes `releases/lotrnpcs_<version>-test.zip`. `--publish` also makes GitHub release v<version>. The version is set in `tools/gen_testnpcs.py` (`"version"`). Run the Python tools with plain `python` (Pillow is in user site-packages). Details in `docs/notes/lotrnpcs-build.md`.

## Testing in the owner's game (Windows, computer use)
Scripts in `tools/game/`:
- `chat.ps1`: types chat commands into the game (`-cmds @("/cmd", "wait:500", "key:20")`).
- `restart.ps1`: `/stop`, quit, installs the newest zip from `releases/`, relaunches and opens the world.
- `focus.ps1`, `click.ps1` (held click, 1456x819 screenshot coordinates), `look.ps1` (turns the camera), `rc.ps1`, `holdrc.ps1`, `tpclick.ps1`, `hire.ps1`.
- `follow.ps1`: keeps the player over the middle of the test NPCs.

Command replies appear in `%APPDATA%\VintagestoryData\Logs\client-chat.log`. Battle CSVs go to the same Logs folder (`/recruits battlelog on|off`, needs `"DebugCommands": true` in `ModConfig/lotrnpcs-factions.json`).

## Rules from the owner
- Never send Escape to the game: computer use treats it as the stop key and the turn ends. Close menus with R, number keys (8 = Goodbye) or by clicking buttons.
- The owner pre-authorizes routine work: code edits, builds, commits and pushes to main, in-game test setup (spawning and killing test NPCs, config values, restarts). Do it and report. Ask before publishing a GitHub release, deleting worlds or saves, installing third-party mods, or anything involving accounts or money.
- Testing commands and debug behaviour go behind `DebugCommands` (`ModCommands.DebugOnly` in src/Hiring.cs), off by default.
- Synergy (in the modpack) freezes NPC AI more than 48 blocks from the player (`EntityActivationRangeEnabled` in `ModConfig/Synergy.json`). Keep battle tests within about 40 blocks, or turn it off for the test and back on afterwards.
- `/entity spawn <type> 38` scatters an army up to ~36 blocks; spawn in batches of 5 (about 8 blocks of scatter) to place armies.
- Use explicit types and a range in `/entity` selectors: an unfiltered kill once killed a hired captain.
