# Handover files (2026-11-13)

Copies of things that normally live only on the owner's PC, so another Claude account (or a different computer) has them.

## battle-logs/
Every non-empty battle log so far (`/recruits battlelog on|off` writes them to `%APPDATA%\VintagestoryData\Logs`). Columns: `ms,event,attacker,attackerFaction,attackerRank,target,targetFaction,targetRank,damage,result,targetHealth`. Events: `melee`, `arrow`, `death`, `idle` (one row per idle NPC-second; `result` = why, `damage` = distance to its target, -1 for none). What each run was is in `docs/notes/lotrnpcs-0109.md` and `lotrnpcs-0113.md`. The latest:
- `20261113-015951`: 0.0.117, 38 orcs v 26 Dunlendings, scattered spawn, Synergy stall (Dunlendings won 35-8).
- `20261113-095243` / `-095825`: 0.0.118, mixed spawn (orcs 26-18) and 70 blocks apart (waves).
- `20261113-102538`: 0.0.118, staged in columns 40 apart (24 of 64 never fought).
- `20261113-104001` / `-104613`: 0.0.119 first builds, both armies stalled in forming lines (the bugs fixed before release).
- `20261113-105114`: 0.0.119 release, whole armies in lines, orcs 24-4.

## game-config/
The owner's `%APPDATA%\VintagestoryData\ModConfig` files as of 2026-11-13:
- `lotrnpcs-factions.json`: has `"DebugCommands": true` (needed for `/recruits battlelog`, `xp`, `bench`, `zone spawnnow`). Copy it into ModConfig, then `/recruits faction reload` in game (or restart).
- `lotrnpcs-spawning.json`, `lotrnpcs-client.json`: the mod's other configs.
- `Synergy.json`: `EntityActivationRangeEnabled` is true (the normal setting). Set it to false only for a battle test, and back afterwards.

## Permissions the next account needs
Claude Code doesn't let Claude write its own permission files, so the owner sets these:

1. **Permission mode:** the sessions here ran in auto mode. Pick it in the Code tab's mode menu.
2. **Computer use:** approve "Vintage Story" when the session asks (it's asked once per session; driving the game needs it).
3. **Fewer prompts (optional):** save this as `.claude/settings.json` in the repo (or `.claude/settings.local.json` to keep it on this PC only):

```json
{
  "permissions": {
    "allow": [
      "PowerShell(& \"C:\\Users\\Mason\\Documents\\GitHub\\LOTR-NPC-MOD\\tools\\game\\*)",
      "PowerShell(& .\\tools\\game\\*)",
      "Bash(python tools/build.py)",
      "Bash(git status *)",
      "Bash(git log *)",
      "Bash(git diff *)",
      "Bash(git add *)",
      "Bash(git commit *)",
      "Bash(git push)",
      "Bash(git push -q)",
      "Bash(gh release view *)",
      "Bash(gh release list *)",
      "Read(~/AppData/Roaming/VintagestoryData/**)",
      "Edit(~/AppData/Roaming/VintagestoryData/ModConfig/**)",
      "Edit(~/AppData/Roaming/VintagestoryData/Mods/**)"
    ],
    "ask": [
      "Bash(python tools/build.py --publish*)",
      "Bash(gh release create *)",
      "Bash(git push --force*)"
    ]
  }
}
```

4. **GitHub:** `gh auth login` once on the PC for releases (already done for this Windows user).
