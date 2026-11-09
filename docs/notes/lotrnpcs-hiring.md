---
name: lotrnpcs-hiring
description: Hiring in lotrnpcs: talk menu + hire (0.0.37), hold-R orders menu, formations, stances (0.0.38), Guard here + /hirelings whitelist (0.0.44), what is still left out
metadata:
  type: project
  modified: 2026-11-03T06:14:29.174Z
---

Design inputs (user, 2026-11-02): right-click menu "similar to the traders" with hire and a brief random Q&A; commands "like the minecraft recruits mod"; any faction member may hire (race, group or /lotrfaction set).

0.0.37 (src/Hiring.cs): talk menu (entity packets 46301-46305); pays HireCoin x HirePrices[rank] from hotbar+backpack; MaxHirelings 5; save key lotrnpcs:hirelings; origin "hired" (never despawns); followers teleport past 48 blocks; no friendly fire; attacks what would attack the owner, monsters, anything hitting the team; grudges. OPEN: default HireCoin coinage:coin-silver-lg is missing in the user's world (they use planchets); user hasn't picked a coin. Silver planchet code is coinage:planchet-silver-lg (coordinator, 2026-11-03), the likely candidate.

0.0.38+ orders menu (src/Orders.cs): HOLD R (rebindable, RegisterHotKeyFirst because Claims Radar also binds R; .lotrorders opens it too), CONFIRMED working by the user in 0.0.40. Movement page + combat page, number keys pick, 96-block range. Formations loose/line/column/wedge/square/circle. Movement orders break off fights 5 s. Stances (user's words): passive never; defensive only defends; aggressive = all enemies, no players unless provoked; raid = all non-allied players and creatures (allies = /group mates, allied-faction members, their hirelings, allied NPCs, animals ownedby an ally; creative/spectator spared). Followers walk 0.025, run 0.055 past 6 blocks.

0.0.44 (2026-11-03, NOT yet tested in game) Guard here, my interpretations, told the user:
- Order "guard" (lotrnpcsOrder), post = where the owner stands, stored in the hold coords. Orders menu "4. Guard here" (formations now keys 5-9, 0); talk menu "Guard this area". Follow clears the post.
- Patrol: random spot within 0.8 x radius every 6-16 s, walking; outside the area they return (may run). No teleport to owner.
- Area: GuardRadius (lotrnpcs-factions.json, default 16, clamped 4-64, config v7), cylinder r across and r up/down. Leash GuardRadius+8 in seek search, ShouldExecute, ContinueExecute, Notify.
- Aggressive guards: usual enemies but only inside the area, plus intruders = players not Friendly (owner, /group mate, allied-faction member, whitelist) and their hirelings. Raid: raid targets inside the area. Defensive/passive unchanged. Fighting back works anywhere.
- Owner offline: judged by the owner record (race, groups, assigned faction, whitelist).
- /hirelings whitelist add|remove|list <player> (privilege chat), online or last known name; one-way, per owner, HiringOwner.Whitelist (uid -> name). Counts like a group mate: never attacked on sight, still fought back.

0.0.48 (user 2026-11-03, NOT yet tested in game): MaxHirelings default 15 (old default 5 migrated, config v8), new MaxFollowers 5. Hiring past 5 followers -> new hireling guards where hired; talk "Follow me" refused when full; orders-menu Follow keeps current followers, nearest others fill free places, rest keep orders. Followers counted from loaded entities only.

0.0.49 shared guards (user 2026-11-03: "a new option", "do the /group"; NOT yet tested in game): talk "Guard this area for our people" / R menu "5. Guard here for my group" (formations now 6-9,0; circle has no number key). Watched attrs lotrnpcsShared, lotrnpcsBorrowerUid/Name. A player sharing one of the owner's /groups (HiringOwner.Groups, last seen) talks to a shared, unborrowed guard -> "Come with me" borrows free; counts toward borrower's MaxFollowers; borrower gets follow/hold/"go back on watch"; posting (any guard order) or logging off returns it as a shared guard. Owner keeps it (dismiss, 15 cap); any owner order takes it back and sets shared only for guard-shared. Owner's R menu skips lent-out ones. Leader = borrower ?? owner (follow AI, teleport, R menu, OnTeamHurt). Combat friendliness still by owner.

0.0.50 (user 2026-11-03 "when I press an R formation, if they are tasked to guard here, they actually go into that formation"): guards get a formation place (attrs lotrnpcsSpotX/Y/Z) around their post, facing the player's look; they hold it like Hold, still fight intruders within GuardRadius of the post, then return. Loose = patrol as before. R-menu Guard here uses the current formation; picking a formation re-forms guards per post. Talk-menu Guard (single NPC) patrols. Untested in game.

User 2026-11-03 chose "Keep as is": guards in a non-loose formation STAND STILL at their spots; only Loose patrols (don't re-offer marching formations).
0.0.51 (user screenshots: formations ragged, square worst, column guards spun in place; "make hireling cap 16"): formation spots (Place, holds and guards) store facing lotrnpcsFaceYaw = atan2(fx,fz); in formation start 0.8 / stop 0.35 / nav 0.25 (was holdDistance 2 / 0.9 / 0.6); idle ones turn to the facing; AiTaskLookAround blocked for them. MaxHirelings default 16, config v9 migrates 5 or 15 -> 16. Untested in game.

Still left out: groups, wander, back-to-pos, forget targets, upkeep, shields/ranged, mounts, loyalty, whitelist menu. Non-orc factions still copy orc stats. Design doc "Hiring and hirelings" ([[npc-mod-maker-goals]]). Factions: [[lotrnpcs-factions]]. Build/perf: [[lotrnpcs-build]], [[lotrnpcs-performance]].
