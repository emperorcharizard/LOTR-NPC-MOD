# Prompt: lotrnpcs 0.0.73, the bowstring pulls back, and dismissing every hireling from chat

The owner's words (2026-11-04), with a screenshot of a Mordor orc archer at full draw (the string still straight between the tips) and logs from a 0.0.72 session: *"whenever the bow is drawn back, can the string on it change to a pulled back state? also make an option in the chat to fire all your recruits."* Then: *"keep the gundabad armor as it is as well."*

**Settled by this message:** gundabad-bone-armor 0.2.0 stays as it is, so the open question about dropping the leggings' waist piece is answered no. The 0.0.72 logs show no lotrnpcs errors (16 hirelings and the zone loaded).

## Part 1: The string pulls back while drawing

Items can't run their own animations in an NPC's hand, so the string is shown by swapping bow items, as the drawn bow already is (0.0.59/0.0.60).

**Measured offline** (the owner's bow on the seraph body, its full-draw hold, vanilla `bowaim` at the 1100 ms draw): the draw hand (LeftHand) reaches the string about 620 ms in (frame 9), then pulls it straight back to 16 px behind the grip's side of the bow (model y -16.0, string at rest y -5.84) at about 1030 ms (frame 15), where the pose holds. The hand stays in the string's plane (z 8.4 to 9.4 against the string's 8) and near the grip's middle (x 1.7 to 2.5).

**Build:**
- Three pulled bows, `lotrnpcs:orcweapon-bow-pull1`, `-pull2`, `-pull3`: the owner's bow with its `bowstring` element replaced by two string segments from the two tips to an apex at x 2.2, z 8 and y -8.5, -12.3 and -16.0 (a third, two thirds and the full draw). Same textures, same full-draw hand transform as `orcweapon-bow-drawn`. Generated from `tools/weapons/orc_bow.json`, so a new bow model gets them too.
- Weapons get an ordered list of **poses** (`Weapons.cs`): each an item and the animations it shows during, as a window in milliseconds after the animation starts (`[from, until]`, until 0 = while it runs). The first pose that matches wins, else the weapon's own item. The old `drawnItem`/`drawnWhile` still read (as one pose). Animation codes are looked up as the shape animation names the game tracks (0.0.60 rule).
- The archer: pull3 from 1031 ms of the draw, pull2 from 894, pull1 from 756, then the straight drawn bow for the rest of the draw, the first 200 ms of the release (the string snaps back when the arrow goes) and the shove; the rest bow otherwise.
- Tests: the pose order and windows, the three shapes (two segments meeting at the apex, no straight string), the pose picker over a whole draw and release.
- Preview: `previews/bowstring-0.0.73.png` (draw frames 9, 11, 13, 15 with each pulled bow).

## Part 2: Dismiss all your hirelings from chat

The owner says "fire" (let go). Read as **dismiss**, the same as "Dismiss" in a hireling's talk menu (coins not refunded), not shooting.

- `/hirelings dismissall` (alias `fireall`) tells you how many hirelings you have and asks you to confirm with `/hirelings dismissall confirm`. Confirming dismisses every one: loaded ones at once (each turns back into a wild NPC of its faction where it stands, as a talk-menu dismissal does); ones in unloaded chunks are marked and dismissed when their chunk next loads (`HiringOwner.PendingDismiss`, saved with the world). Shared guards that someone borrowed are yours, so they go too. NPCs you borrowed from someone else aren't yours and stay with their owner.
- Logged in the server log like single dismissals. Plain-English chat lines.
- Tests: the confirm step, loaded and unloaded hirelings, the pending list surviving save and load, borrowed guards.

## Also, since C# changes this round

- Fix the spawning `#Instructions` line that still says archers are Mordor only (0.0.69 gave every faction but the Shire archers).

## Build and check

0.0.73: regenerate, bundle, build, both suites, package, end the round per `THREADS.md`. The owner checks: an archer's string pulls back with the hand while drawing, snaps straight on the shot, and the bow goes back to the side; `/hirelings dismissall`, then `confirm`, frees every hireling, including ones far away (they're free when you next go near them).
