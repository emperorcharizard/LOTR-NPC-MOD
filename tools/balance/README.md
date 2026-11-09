# Faction balance simulator (0.0.90, 0.0.91)

How the 0.0.90 faction numbers were found (job script `prompts/0.0.90-faction-balance.md`, section 4).

- `sim2.py`: event-driven NPC-on-NPC fight model. Rank health and damage times the faction multipliers, melee every 1.95 to 2.35 s (90% landing), arrows every 3.1 to 3.9 s (70%), at most 5 attackers per target, a free enemy first, a short walk after a kill. The hit rates are guesses.
- `analyse.py`: per-rank values, hits to kill, threat to a lone player, win-rate tables for full bands (four rank makeups), wild groups made like `SpawnZoneSystem.GroupRanks` (scaled) and mixed armies under follow shares.

Run (any Python 3, no packages):

    python3 analyse.py 2000 all                    # every table, 2000 fights per pairing (about a minute)
    python3 analyse.py 1000 bands Orc=0.70,0.85    # try other multipliers: Tier=health,damage
    python3 analyse.py 500 ranks                   # just the per-rank and hit tables

Read win rates with care: fights are nearly decided by the makeup, so one health point that adds a hit to kill moves a tier a lot (goblin recruits at 12 health lose, at 13 they are even).

## 0.0.91: per-rank numbers with a hit spread

After the owner's 5 against 7 warrior test (Men won losing one), the balance moved from two multipliers per faction to health and damage per rank, with every hit varied by `DamageSpread` 0.2 (`sim2.SPREAD`). The goal (owner): at each rank, a full band of each faction wins half its fights averaged over every other faction's full band (Uruk-hai a little more, about 0.55).

- `fitspread.py RANK RUNS STEPS START.json`: hill climbing on single steps (health +-1, damage +-0.05) for one rank, every pair with a fixed seed, Men free but pulled to the base values, a small pull toward each faction's 0.0.90 flavour; then a check on fresh fights. Writes `spread-RANK.json`.
- `fitjoint.py`: the same without the spread (how 0.0.91 found that per-rank numbers alone leave single pairs lopsided).
- `ranks-0.0.91.json`: the shipped result (the tables in `FactionTiers`, src/Factions.cs), with each faction's average and every pair. Averages 0.45 to 0.57, every single pair 0.37 to 0.63.

To refit from the shipped values: `for r in recruit warrior captain archer; do python3 fitspread.py $r 400 200 ranks-0.0.91.json; done` (about 10 minutes on 2 cores). `analyse.py` still uses the 0.0.90 multipliers.
