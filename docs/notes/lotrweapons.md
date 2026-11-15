---
name: lotrweapons
description: LOTR Weapons, the second mod - the owner's custom NPC weapons as player items (iron/steel), decisions and progress
metadata:
  type: project
  modified: 2026-11-14
---

Owner, 2026-11-14: "take all the weapons so far that are custom made then make them into items for players as a
different mod, same rigs and animations. And to eventually begin creating recipes and forge items on anvil."
Full plan: `prompts/player-weapons-mod.md`.

## Decisions (owner, 2026-11-14)
- Mod id `lotrweapons`, "LOTR Weapons".
- Vanilla item classes plus a Combat Overhaul JSON patch (`dependsOn`), "so it can still be used without the mod".
- Stats copy the nearest game weapon.
- Every melee weapon in two variants, iron and steel ("the skin difference can be solved later"). The three pairs the
  owner already made (Gondorian sword, dwarven warhammer, dwarven shorthammer) become one weapon each, steel variant
  on the steel model: 29 melee weapons, 58 items.
- Shields and bows: iron only.
- Order: melee weapons first ("Do melee weapons first"), then shields, then bows.
- Where it lives (2026-11-14, owner gave full permission to proceed; my recommendation taken): this repo. Generator
  `tools/gen_lotrweapons.py`, checks `tools/tests/lotrweapons/check.py`, both run by `tools/build.py`, which also
  writes `releases/lotrweapons_<version>-test.zip`; `tools/game/restart.ps1` installs both mods.

## Progress
- Phase 1 done (2026-11-14): the owner's 32 melee entries, `hand_transform`, the texture tables and the shape writer
  (`weapon_shape`, `copy_own_textures`) moved from gen_testnpcs.py to `tools/weapon_catalog.py` (`MELEE` = all 32).
  gen loads it by path and writes lotrnpcs' weapons through it with domain `lotrnpcs`, folder `orcweapon`. Proof: gen
  output (282 files) byte-identical before and after (`diff -r`). Shields and bows stay in gen until their phase.
  Not built with dotnet here (no SDK in the cloud container); the change touches only gen's Python.
- 0.0.1 built (2026-11-14), not yet in game. Content-only mod, vanilla classes, 29 weapons x iron/steel = 58 items.
  - Sources read: vanilla 1.22.7 assets from the public mirror github.com/caffeineLover/Vintage-Story-Reference
    (versions/1.22.7/Assets), Combat Overhaul from github.com/maltiez2/CombatOverhaul (the server runs a fork,
    0.18.9; same layout). mods.vintagestory.at is blocked from the cloud container; dot.net too (no dotnet here, so
    the C# tests run only in GitHub Actions).
  - Stats (vanilla JSON): swords = falx (iron 5 / tier 4 / 850, steel 5.25 / 5 / 2125, reach 2.5, Sharpenable);
    knives (Orc dagger, Orc knife, Dwarven dagger) = knife-generic (3.5/900, 4/2250, ItemKnife); one-handed axes =
    felling axe (3/4/900, 4/5/1800, ItemAxe, chops wood); spears = spear-generic (4.2/400, 4.4/700, reach 3.5,
    thrown 6.8 tier 1 / 7.0 tier 2, ItemSpear with our own thrown entities). Vanilla has no iron or steel mace, war
    hammer, battle axe, poleaxe, halberd or great sword (only "ruined", 4.5 for every type), so those ("heavy": both
    hammers, poleaxe, battleaxe, halberd, warhammer, elven longsword, captain's sword) copy the falx numbers.
    Owner may want two-handed weapons to hit harder: ask after the in-game test.
  - Hand: tpHandTransform is the NPC item's (checked equal by the test). Vanilla 1.22 tools have no fpHandTransform;
    first person uses the same one.
  - Animations: swing = the NPC's attack animation (heldTpHitAnimation), hold = the NPC's hold (heldTpIdleAnimation
    and heldRightReadyAnimation). The vanilla ones (falx, axehit, hammerhit, knifestab, spearhit, spearidle) are on
    the player already. poleaxechop, poleaxehold, greatswordhold are added by patches/player-animations.json to the
    game's seraph-faceless and LRC's seraph/dwarf/elf-faceless shapes (LRC ones dependsOn lrc), with player.json
    settings copied from axehit (chop: blow at frame 14, speed 1) and holdbothhands (holds), plus "-fp" entries.
  - Icons and ground: rotation = vanilla falx's icon rotation applied after the turn that takes our weapon's in-hand
    lie to the falx's; origin = the model's centre; scale gives every weapon the falx's on-screen length. Offline
    sheet previews/lotrweapons-icons-0.0.1.png (renderer centred on the origin, which is how vanilla's falx icon
    reads). Tune in game with .tfedit.
  - Combat Overhaul: CO replaces vanilla weapons with its own definitions (game/itemtypes/combatoverhaul/sword.json:
    OneHandedStanceByType with Attack/DamageTypes per metal, its own animations "combatoverhaul:sword-falx-slash-1"
    etc., CollidersLib colliders). So under CO the swing is CO's animation, not the NPC's: "same animations" holds only
    without CO. Next build: the CO patch, copying CO's falx/spear/club/axe stances per kind (CO metal sets tier only:
    falx iron tier 5, steel 7, damage 4).
- 0.0.2 built (2026-11-14, owner: "build as much as you can without need for ingame testing", "increase the damage
  to be slightly better than vanilla", and sent Not Only Spear Expansion 0.4.1 and Maltiez Swords 1.1.0 "as a
  comparison for stats"). Not in game yet. Maltiez Swords is client-side sword models only (no stats), unused.
  - Damage x1.1 (BETTER) on every attack power, thrown spear damage, shield absorption and bow damage, and on CO damage.
  - Stats: war axes (Orc, Dwarven, Rohirric axe) and the Dwarven battleaxe now copy NOSE's bartka (5/850, 6/2150),
    hammers its mace (5.5/1000, 6/2350), the warhammer its polehammer (5/1100, 6/2000, reach 3.5), poleaxe and
    halberd its halberd (5/1100, 6/2000, reach 3.5), the great swords its claymore (5.5/1200, 6.2/2400, reach 3).
    Swords, knives and spears stay the game's. NOSE is not a dependency: numbers only (credited).
  - CO patch (patches/combatoverhaul.json, dependsOn combatoverhaul, side Server, as NOSE's): class
    CombatOverhaul:MeleeWeapon, OneHandedStance and TwoHandedStance (two-handed weapons use their two-handed swing in
    both), damage per metal x1.1 with "Tier" (CO source) and "Strength" (NOSE's name) both set, hit line from the grip
    to the end of the head. First person: CO's own animations (falx slash; club 1h for axes and maces, CO has no axe
    swings; club 2h for polearms, battleaxe, warhammer; great-sword slash; spear thrusts 1h/2h). Third person:
    AttackTpAnimation = the NPC swing. Knives, shields and bows: no CO patch (CO leaves the game's knife alone, its
    own shield patch is disabled, and its bow class needs its animated bow model).
    Risk: the server runs a CO fork 0.18.9; the stance format was read from maltiez2/CombatOverhaul (2026-04) and
    NOSE 0.4.1. Untested.
  - Shields (12, iron only): ItemShield as the game's iron metal shield (450 + 60 durability, absorb 5 / projectiles
    10, x1.1), the NPC off-hand transform, icon from the shape (face plate toward the viewer). Recipe: iron boss, hoop
    and plate (the game's metal shield).
  - Bows (2, iron only): ItemBow as the game's recurve (damage 4 x1.1, accuracy +0.3, 600), the NPCs' full-draw
    transform, aim animation bowaim, the three pulled-string stages as draw shapes. Recipe: the game's recurve bow
    recipe without the bowyer trait.
  - Forging: head-<code>-<metal> items (the whole weapon model, as the game's falx blade; Quenchable, forgable),
    anvil recipe = the head's outline (boxes past the grip on the heavier side, shaft pieces left out for hafted
    weapons) fitted to 16 x 8 and <= 42 voxels (one ingot, like every game and NOSE head), grid: head over one stick
    (two for spears, polearms, battleaxe, warhammer, great swords), applyquenchablebuffs. Outlines are drafts:
    previews/lotrweapons-anvil-0.0.2.txt.
  - Shield and bow data moved into tools/weapon_catalog.py (lotrnpcs output byte-identical).
- After 0.0.2 (same day, "take this as far as you can"): tools/tests/lotrweapons/patches.py applies every patch to
  its target (our items, LRC's bodies from build-inputs; the game's player.json and seraph-faceless when given a game
  assets folder: checked once against the 1.22.7 mirror, all 70 apply) and checks the three custom animations only
  move parts each body has. Run by build.py. Handbook lines (itemdesc-*) for every weapon, head, shield and bow.
  Under CO, spears are CO melee weapons (thrust, no throw): CO's own throwing needs its projectile behaviour and
  entity, not copied.
  Ground storage (GroundStorable WallHalves, Ctrl + right click) and tool racks (rackable): transforms are the falx's
  (blade.json "*") applied after hand(falx)^-1 * hand(ours), so each weapon's grip lands where the falx's does
  (self-check: the falx maps back onto its own transforms). Long weapons may poke out of a rack.
  Shields and bows too, from the game's round shield and recurve bow (same method, their hand transforms).
- Steel skins, looked into (not built): the owner's three iron/steel pairs change warm brown-grey iron (hue 0.04-0.12,
  saturation 0.04-0.16) to near-neutral lighter grey (lightness 0.389 l + 0.348, saturation 0.017); wood, leather
  and cloth stay. Applied blindly to the other atlases it fits the owner's pairs (mean colour error 6-14 vs 23-29
  untouched) but changes nothing on about half the weapons (already neutral grey) and greys some wood (Easterling
  scimitar handle). Proposal sheet: previews/lotrweapons-steel-skins-proposal.png (each pair: iron, auto steel).
  Owner to decide: own steel skins, auto rule for chosen weapons only, or leave both metals alike.
- Owner, 2026-11-15: in-game testing allowed (but Remote Control still won't serve the PC folder to cloud sessions:
  "not_available", app version or folder location on DESKTOP-JR5ODSO); "Apply the automatic rule, can revise it
  later"; leave the bowyer trait out; NPCs don't drop these; "It will be its own mod, I just need the zip".
- 0.0.3: the steel rule applied (STEEL_RULE in gen_lotrweapons.py): 15 weapons get atlas/<key>-steel.png on their
  steel variant (texturesByType, also on their heads and thrown spears), the boar spear the game's steel ingot,
  3 keep the owner's steel models, 11 are unchanged (no warm iron in their texture: Dwarven axe, battleaxe, dagger,
  Rohirric axe, sword, spear, Haradrim curved sword, sword, spear, Misty Mountains sword, Elven longsword).
  build.py --weapons-only builds only this mod with Python (no .NET); the zip is sent to the owner directly.
- In game 2026-11-14 on 0.0.3 (with Combat Overhaul, the owner's modpack): loads clean (no lotrweapons errors or
  warnings; the 33 patch errors are the armour mods' own co.json). Creative tab lists 130 items (58 weapons, 58 heads,
  12 shields, 2 bows), icons inside their slots, names right ("Iron/Steel Dwarven battleaxe"), heads use the whole
  weapon model as designed. Tab label read "tabname-lotrweapons": the key must be in the game domain, as lotrnpcs
  learned (fix = 0.0.4). Under CO the steel Gondorian sword shows 4.4, tier 7 (matches W6.3); battleaxe 5.3 / 5.8.
  First person the steel sword is visible, not through the camera, and swings with CO's slash. Ctrl+Shift+right
  click on the ground did not place it (computer-use click may not count; owner to try). Not checked: third-person
  hold vs NPC, damage, throw, racks, shields, bows, forging. Left in the world: hotbar slot 6 holds the steel
  Gondorian sword (was Medium fertility soil), and a creative left click dug a small hole at about 1501 3 142.
- 0.0.4: tab name in the game domain.
- In game 2026-11-14 on 0.0.4: tab reads "LOTR Weapons" (W1.2 done). Ctrl+Shift+right click on open ground says
  "Requires a solid wall to lean at" (the falx's WallHalves layout, so a wall is needed; not tried against one).
  Haradrim heavy shield: durability 510, CO block tier 2 / parry tier 4, hangs on the left arm and raises to block
  on right click. Misty Mountains orc bow: 4.4 piercing, +30% accuracy, held in the right hand; draw not seen (no
  arrows; the hotbar is full). Still unchecked: steel skins by eye, third-person hold vs NPC, damage on an animal,
  spear throw, racks, bow draw, forging (W3), CO W6.2. Driving the inventory by computer use stalls (the game's
  pointer stops following the mouse), so these are better done by the owner. Left in the world: hotbar slot 6 holds
  the Misty Mountains orc bow, the off-hand the Haradrim heavy shield (slot 6 was Medium fertility soil); a termite
  mound block stands at about 1500 4 141. tools/game/placerc.ps1: held right click with Ctrl/Shift held.
