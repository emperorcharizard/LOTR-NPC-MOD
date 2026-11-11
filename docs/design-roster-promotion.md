# Design: roster screen and branching promotion

Draft, 2026-11-10. Built in 0.0.113 (both steps) with default answers to Q1-Q6; see notes/lotrnpcs-0113.md. Owner picks from the army-games gap review (docs/notes/lotrnpcs-review-answers.md):
"a roster screen is a good idea, branching promotion is good". Build after the 0.0.112 in-game test. Open questions for
the owner are marked **Q** and collected at the end.

---

## 1. Roster screen

A list of every recruit the player has hired, wherever they are, so the player can see their army at a glance and
find lost NPCs.

### How it opens

- `/recruits roster` (a new player command next to `dismissall`, `whitelist` and `orders`).
- A **Roster** button at the bottom of the orders menu (the R menu), so no new key is needed.
- A key of its own, unbound by default, that the player can set under Controls. **Q1**: or bind it to a key by default?

### What it looks like

```
+---------------------------------------------------------------------------------------------+
|  Your recruits                                                                        [x]   |
|  11 of 16 roster places used  ·  4 of 5 following  ·  1 of 2 captain places                 |
|  Room on your roster: Men of Gondor 5                                                       |
|                                                                                             |
|  Show: [All 11] [Following 4] [Guarding 6] [Ready to promote 2] [Not loaded 3]              |
|                                                                                             |
|  Name ▲            Rank        XP       Order        Stance      Where                      |
|  ---------------------------------------------------------------------------------------    |
|  Beregond          Captain     -        Follow       Defensive   12 blocks NE               |
|  Hirgon            Warrior     40/40 ★  Follow       Aggressive  8 blocks N         [Promote]|
|  Ingold            Guard       12/40    Guard here   Defensive   at the gate, 140 blocks W  |
|  Damrod            Archer      -        Guard here   Aggressive  at the gate, 141 blocks W  |
|  Mablung           Recruit     15/15 ★  Hold         Passive     30 blocks S        [Promote]|
|  Anborn            Recruit     6/15     Guard here   Defensive   not loaded: last seen      |
|                                                                  2,140 / -880, 2 days ago   |
|  ...                                                                                        |
|                                                                                             |
|  Selected: Hirgon (Warrior, Men of Gondor)                                                  |
|  [Show on map]   [Call to me]   [Promote...]   [Dismiss...]                                 |
+---------------------------------------------------------------------------------------------+
```

- **Header**: the same numbers the talk menu already shows (roster share used, following, captain places, room line),
  so they can be found without talking to an NPC.
- **Filters**: All, Following, Guarding (posted or guard here), Ready to promote (XP full), Not loaded.
- **Columns**, any of which sorts the list on click:
  - **Name**: the NPC's name (Looks), or its rank title if unnamed.
  - **Rank**: the faction's title for the rank (`title-<rank>`).
  - **XP**: `x/need`, ★ when full; `-` for ranks that earn none.
  - **Order** and **Stance**: as the orders menu names them. A borrowed shared guard shows "Borrowed by <name>".
  - **Where**: for a loaded NPC, distance and compass direction from the player. For a posted guard, "at <post>" when
    the post has a name. **Q2**: should posts get names? Without them it's just the distance. For an NPC that isn't
    loaded: "not loaded: last seen x / z, <time> ago".
- **Row actions** (on the selected row):
  - **Show on map** adds a temporary waypoint at the NPC (or where it was last seen), using the game's world map
    waypoints, and removes it again after 5 minutes.
  - **Call to me** sets it to Follow, if it's loaded and within reach of the follow limit, the same way the orders
    menu does. Greyed out when it's not loaded.
  - **Promote...** opens the promotion screen (section 2). Only when XP is full and the NPC is loaded.
  - **Dismiss...** asks for confirmation, then dismisses it as the talk menu does. An unloaded NPC goes on the same
    pending list `/recruits dismissall` uses.
- Shift-click on a column header sorts it in reverse. The list scrolls past about 12 rows.

### What the server needs (the main work)

Today the server only knows two things about a recruit it hasn't loaded: its **faction** and whether it's a
**captain** (`HiringOwner.RecruitFactions`, `HiringOwner.Captains`). Name, rank, XP, order, stance and position
are on the entity, so they're gone while its chunk is unloaded.

- Add a saved summary per recruit to `HiringOwner`: name, rank, XP, order, stance, position and dimension, and when
  it was last updated.
- Refresh it when a recruit loads or unloads, when an order, stance or name changes, on promotion, and every
  30 seconds for loaded recruits (position only). It's a few hundred bytes per player.
- When the screen opens, the server sends one JSON packet to the player, as it already does for the talk menu
  (`TalkState`). The screen asks again every 2 seconds while it's open, for distances.
- Recruits hired before this version get a summary the first time they load. Until then their row shows the faction
  and "not seen since the update".

### What it doesn't do

It doesn't change gear, rename NPCs (that stays in the talk menu's Looks), or give orders to a whole filtered group
(that stays in the orders menu).

---

## 2. Branching promotion

### Today

- Recruit at 15 XP → **Warrior** or **Archer**.
- Warrior at 40 XP → **Captain**.
- Archers and captains earn no XP.
- Each promotion costs the new rank's hire price less 3 coins (less 4 for a captain). It's offered as chat options
  in the talk menu.

### What the balance check found (tools/balance/branches.py, 2026-11-10)

1. **A rank that's simply stronger and pricier doesn't pay off.** In this mod's fights, numbers beat quality,
   because up to 4 attackers can gang up on one target. A "veteran" costing 1.67× a warrior needs 2.3× the warrior's
   health to break even coin for coin. A "marksman" archer loses even with 2.6× arrow damage, because big arrows
   overkill and get lost in the half-second invulnerability after each hit. Captains get away with it only because
   captain places are limited.
2. **Same-price sidegrades are fair** as long as health × damage stays the same. A "guard" with health ×1.25 and
   damage ×0.8 wins 0.48–0.51 against plain warriors, coin for coin, in every faction. A "shock" fighter with health
   ×0.8 and damage ×1.25 wins 0.48–0.53. Both are well inside the 0.45–0.55 target. Even small mismatches swing it:
   ×1.3 / ×0.8 (4% more health × damage) wins 0.74–0.85.
3. **Promotion costs more than hiring the rank outright**, from your 0.0.93 rule (new rank's price less 3–4 coins).
   Men: recruit 5 + promotion 12 = 17 coins for a warrior that hires for 15. A captain grown from a recruit costs
   53 coins against 40 to hire (+5 for goblins, up to +18 for elves and dwarves), plus 55 kills. The only gain today
   is keeping the NPC's name and looks.

So branches should be **sidegrades that change how an NPC fights, not how strong it is**, and **only reachable by
promotion**, which gives promotion a reason to exist.

### Proposed paths

```
                       +--> Warrior (as now) ----+
Recruit (15 XP) -------+--> Guard   (new) -------+--(40 XP)--> Captain
                       +--> Shock   (new) -------+
                       +--> Archer  (as now)
```

| Rank | Health | Damage | Weapon and shield | How you get it | Promotion price |
| --- | --- | --- | --- | --- | --- |
| Warrior | as now | as now | as now | hire or promote | as now |
| **Guard** | warrior × 1.25 | warrior × 0.8 | one-handed weapon, **always** a shield | promotion only | same as to warrior |
| **Shock** | warrior × 0.8 | warrior × 1.25 | the faction's **two-handed** weapon (halberd, greatsword, warhammer, battleaxe); factions without one get a one-handed weapon and **no** shield | promotion only | same as to warrior |
| Captain | as now | as now | as now | hire or promote from any of the three | as now |

- Guard and Shock earn XP and promote to Captain at 40 XP, like warriors.
- They count as warriors for everything else: roster share, follow share, battle lines and the captain limit (they
  aren't captains).
- A Guard is told apart from a Warrior by its shield, which it always carries, and a Shock trooper by its two-handed
  weapon. **Q3**: should Guards also wear the captain's helmet (no captain plume), or keep the warrior set?

Starting numbers per faction (warrior × 1.25 / 0.8, rounded; the build refits them with the fit105 method, since one
health point can move a fight a lot):

| Faction tier | Warrior health / damage | Guard health / damage | Shock health / damage |
| --- | --- | --- | --- |
| Men | 34 / 3.95 | 43 / 3.16 | 27 / 4.94 |
| Uruk-hai | 32 / 4.3 | 40 / 3.44 | 26 / 5.38 |
| Levy | 28 / 3.6 | 35 / 2.88 | 22 / 4.5 |
| Orcs | 30 / 3.35 | 38 / 2.68 | 24 / 4.19 |
| Goblins | 22 / 2.8 | 28 / 2.24 | 18 / 3.5 |
| Elves | 37 / 5.3 | 46 / 4.24 | 30 / 6.63 |
| Dwarves | 47 / 4.15 | 59 / 3.32 | 38 / 5.19 |

Faction titles (lang `title-<faction>-guard` and so on, falling back to "Guard" and "Shock trooper"), as examples
for you to replace (**Q4**):

| Faction | Guard | Shock |
| --- | --- | --- |
| Gondor | Fountain Guard | Swordsman |
| Rohan | Royal Guard | Axeman |
| Dwarves | Iron Guard | Hammerer |
| Mordor orcs | Shieldbearer | Black Uruk |
| Easterlings | Golden Guard | Halberdier |

### The promotion screen

When an NPC's XP is full, its talk menu option becomes **Promote...**, which opens this screen (also reachable from
the roster):

```
+-----------------------------------------------------------------------------+
|  Promote Recruit Mablung (Men of Gondor)                                     |
|  XP 15/15  ·  You have 31 rusty gears                                        |
|                                                                             |
|  +----------------+ +----------------+ +----------------+ +----------------+ |
|  |   [portrait]   | |   [portrait]   | |   [portrait]   | |   [portrait]   | |
|  |    Warrior     | | Fountain Guard | |   Swordsman    | |     Archer     | |
|  |  Health  34    | |  Health  43    | |  Health  27    | |  Health  35    | |
|  |  Damage  3.95  | |  Damage  3.16  | |  Damage  4.94  | |  Arrows  3.05  | |
|  |  Spear/sword   | |  Sword+shield  | |  Halberd       | |  Longbow       | |
|  |  can lead to   | |  can lead to   | |  can lead to   | |  last rank     | |
|  |  Captain       | |  Captain       | |  Captain       | |                | |
|  |   12 gears     | |   12 gears     | |   12 gears     | |   12 gears     | |
|  |  [ Promote ]   | |  [ Promote ]   | |  [ Promote ]   | |  [ Promote ]   | |
|  +----------------+ +----------------+ +----------------+ +----------------+ |
|                                                                             |
|  The name, looks and orders stay. The armor becomes the new rank's.          |
+-----------------------------------------------------------------------------+
```

- One card per path. A path whose entity type is missing (a faction without that rank) isn't shown, as now.
- Cards the player can't afford show the price in red with the Promote button greyed out. Captain shows "captain
  places full" when it is.
- The portrait uses the item portrait code from 0.0.112 (`CollectibleBehaviorNpcPortrait`) to draw the NPC in the
  new rank's armor. If that turns out too slow for a dialog, the card shows the rank's armor icon instead.
- Promote asks the server, which re-checks XP, coins and places, then replaces the NPC exactly as `Promote()` does
  now (everything the owner chose carries over).

### What changes in the code

- `Promotion.Targets`: recruit → warrior, guard, shock, archer; warrior, guard and shock → captain.
  `Promotion.Need`: guard and shock 40.
- `tools/gen_testnpcs.py`: two more entity types per armed race (`-guard`, `-shock`), with the warrior's armor and
  the loadout rules above; `RANK_STATS` and `FactionTiers` get the two ranks.
- `FactionTiers` and `lotrnpcs-factions.json`: health and damage per faction for the new ranks; promote prices
  default to the warrior's. They get no hire price, since they're promotion only.
- Wild spawn groups: **Q5**: leave guards and shock troops out of wild groups (only players get them), or mix a few
  in for variety?
- Existing warriors stay warriors. Nothing in a saved world changes.

---

## Order of work (after the 0.0.112 test)

1. **Roster screen**, version 0.0.113: the summary store, `/recruits roster`, the screen and the Roster button.
   Promote on the roster opens the old chat options until step 2.
2. **Branching promotion**, version 0.0.114: the new ranks, the stats refit, the promotion screen.

## Open questions for the owner

- **Q1** Roster key: unbound by default (set under Controls), or a default key?
- **Q2** Should guard posts get names, so the roster can say "at the gate"? Owner, 2026-11-10: "No".
- **Q3** Guards: warrior armor, or the captain's helmet with the warrior body?
- **Q4** Faction names for the Guard and Shock paths (the examples above are placeholders).
- **Q5** Guards and shock troops in wild spawn groups, or players only?
- **Q6** Keep Warrior as a path next to Guard and Shock (four choices at 15 XP), or replace it with the two (three
  choices)?
