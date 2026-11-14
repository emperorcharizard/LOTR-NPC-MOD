# Balance check under 0.0.137 rules (2026-11-14)

Owner: "Check the tests and balance". Offline tests: both suites pass (653 checks). Made with `tools/balance/check137.py` (sim105 rules without hit turns, cap 4; equal stats; NPC v NPC x0.5; back hits modelled as 1/3 of blows from attackers the target isn't fighting, x1.5). The simulator has no positioning, so archers can't kite and riders don't charge (0.0.133 untested).

```
## Same rank, same numbers (every faction alike now): win rate of side A, fight length
warriors 3 v 3, back hits off: 0.52, 30 s
warriors 3 v 3, back hits on : 0.55, 28 s
warriors 9 v 9, back hits off: 0.49, 32 s
warriors 9 v 9, back hits on : 0.48, 29 s
warriors 30 v 30, back hits off: 0.49, 34 s
warriors 30 v 30, back hits on : 0.52, 30 s

## Outnumbered (how much one or a few extra NPCs decide)
warriors 3 v 4, back hits off: smaller side wins 0.00
warriors 3 v 4, back hits on : smaller side wins 0.00
warriors 9 v 10, back hits off: smaller side wins 0.00
warriors 9 v 10, back hits on : smaller side wins 0.00
warriors 9 v 12, back hits off: smaller side wins 0.00
warriors 9 v 12, back hits on : smaller side wins 0.00
warriors 27 v 30, back hits off: smaller side wins 0.00
warriors 27 v 30, back hits on : smaller side wins 0.00

## Rank against rank at equal coin (hire 5/15/40/15; riders a warrior plus the captain's promotion, 51)
9 recruit v 3 warrior (45 coin each): recruit wins 1.00
27 recruit v 9 warrior (135 coin each): recruit wins 1.00
3 captain v 8 warrior (120 coin each): captain wins 0.00
3 archer v 3 warrior (45 coin each): archer wins 0.00
2 rider v 7 warrior (102 coin each): rider wins 0.00
2 rider v 3 captain (120 coin each): rider wins 0.43

## A full PvP side: 10 players' followings, 30 warriors v 30, and mixes
30 mixed (18 warriors, 6 recruits, 4 archers, 2 captains) v 30 warriors: mixed wins 0.01, 31 s

## Per follower slot (each follower is 1/3 of a band whatever its rank): 3 v 3 and 30 v 30, back hits on
3 v 3: row's win rate against the column
| | recruit | warrior | archer | captain | rider |
| --- | --- | --- | --- | --- | --- |
| recruit | - | 0.00 | 1.00 | 0.00 | 0.00 |
| warrior | 1.00 | - | 1.00 | 0.00 | 0.00 |
| archer | 0.00 | 0.00 | - | 0.00 | 0.00 |
| captain | 1.00 | 1.00 | 1.00 | - | 0.00 |
| rider | 1.00 | 1.00 | 1.00 | 1.00 | - |
30 v 30: row's win rate against the column
| | recruit | warrior | archer | captain | rider |
| --- | --- | --- | --- | --- | --- |
| recruit | - | 0.00 | 1.00 | 0.00 | 0.00 |
| warrior | 1.00 | - | 1.00 | 0.00 | 0.00 |
| archer | 0.00 | 0.00 | - | 0.00 | 0.00 |
| captain | 1.00 | 1.00 | 1.00 | - | 0.00 |
| rider | 1.00 | 1.00 | 1.00 | 1.00 | - |
```

## Reading it
- Every faction is now the same, so equal numbers of the same rank are even (0.48-0.55) and the factions differ only in gear and looks. Hits in the back barely change even fights; they shorten them a little (about 34 to 30 s from contact at 30 v 30).
- One extra NPC decides a fight (3 v 4, 9 v 10, 27 v 30: the smaller side never wins in the simulator). In PvP with 3 followers each, equal player counts give equal numbers, so this is fine; a missing follower matters a lot.
- Per follower slot (a follower is a third of a band whatever its rank) the ranks are a strict ladder: rider > captain > warrior > recruit, and archers lose to everything in the simulator (no kiting). Coin and the captain limit (RosterPerCaptain: 2 captains on a roster of 16) are the only brakes, so in PvP every player will field the strongest followers they can pay for: riders (76 / 8.83, about twice a captain's health x damage) and captains.
- Equal coin by rank is far from even (27 recruits beat 9 warriors, 3 captains lose to 8 warriors), but follower slots, not coin, limit a PvP band.

## Options (not built)
1. Count riders against the captain limit (or make a rider take 2 follower slots), so a band can't be three riders.
2. Lower the shared rider scale from 2.24 to about 1.4 (between a warrior and a captain), and let the charge carry them.
3. Leave the ladder (better units cost more) and accept that rich players field riders and captains.
