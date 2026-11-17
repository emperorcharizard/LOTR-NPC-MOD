# LOTR NPCs in-game test pass (0.0.117)

Updated 2026-11-12 for 0.0.115.

Everything built since 0.0.44 that nobody has seen in game yet, in one play session. Install **lotrnpcs 0.0.117** (only one lotrnpcs zip in Mods, full game restart) and **gundabad-bone-armor 0.3.0**. Spawn fresh NPCs from the LOTR NPCs creative tab: older ones keep their old weapon, hair and looks. For anything wrong, note the step number and send a screenshot plus client-main.log and server-main.log.

## 1. Weapons and animations (0.0.65 to 0.0.68)

- [ ] 1.1 Spawn 6 or more Mordor orc warriors. Between them you should see a scimitar, axe, hammer, dagger, knife and poleaxe, all in your new textures.
- [ ] 1.2 The scimitar's curve sweeps up at the tip, and the knife's edge faces the same way as the dagger's.
- [ ] 1.3 Let a poleaxe orc fight you. It should raise the poleaxe over its right shoulder and chop down and forward, with its left hand on the shaft. If it just stands still when it attacks, that's the most important thing to report.
- [ ] 1.4 Between swings the poleaxe orc holds the pole slanted across its body in both hands.

## 2. Archers for every faction except the Shire (0.0.69, 0.0.70, bow 0.0.71)

- [ ] 2.1 Spawn an archer from several factions (Mordor, Angmar, Misty Mountains, Isengard, Gundabad, Gondor, a dwarf, an elf). Each holds your orc bow level at its side and wears its faction's recruit armor.
- [ ] 2.2 When it draws, the bow comes up and the string is pulled back; at rest it goes back to the side.
- [ ] 2.3 Each one shoots you from range, stops about 16 blocks away, and steps back if you rush in.
- [ ] 2.4 In a zone for one of those factions (/lotrzone), groups sometimes include archers.
- [ ] 2.5 Hire one: it costs 15 planchets and stands in the back row of a formation.

## 3. Shields and the creative tab (0.0.71, 0.0.72)

- [ ] 3.1 About half of new Mordor melee orcs carry your morc_shield on the left forearm, never with the poleaxe.
- [ ] 3.2 About half of new Misty Mountains recruits, warriors and captains carry your Misty Mountains shield (single Moria texture). They still have no weapon.
- [ ] 3.3 In the LOTR NPCs creative tab, each faction's recruit, warrior, captain and archer sit next to each other.

## 4. Orc looks (0.0.57 and 0.0.67)

- [ ] 4.1 Hire an orc of each body (goblin, Mordor, Gundabad, Uruk). In the looks menu, the hair list offers Dreadlocks and Messy, and the long balding style is gone.
- [ ] 4.2 Mordor orcs have no tusks.
- [ ] 4.3 The looks menu changes the name, helmet, face parts and weapon, and Cancel puts everything back.
- [ ] 4.4 Hired NPCs are called by rank then name, for example "Warrior Gorbag", in the name tag, talk menu and death message.

## 5. Gundabad (0.0.62/0.0.63 and armor 0.2.0)

- [ ] 5.1 Spawn a Gundabad recruit, warrior and captain. Check the bone armor from front, side and back: the new chest piece, gauntlets and collar should hug the body.
- [ ] 5.2 Their skin is lighter than Mordor orcs'.
- [ ] 5.3 Note whether the old leggings' waist band looks wrong under the new chest piece. That's the open question about removing it.

## 6. Guards and formations (0.0.44 to 0.0.51)

- [ ] 6.1 Order a recruit to Guard here. It patrols within 16 blocks of the spot and fights enemies that come inside.
- [ ] 6.2 A player who isn't on your side and walks into the area gets attacked. A player you added with /recruits whitelist add does not.
- [ ] 6.3 Log off. Guards keep guarding while you're gone.
- [ ] 6.4 Hire more than 5. Followers stop at 5, and the rest guard where they were hired. The cap is 16.
- [ ] 6.5 Turn on Guard this area for our people. A /group mate can borrow the guard, and it returns to its post when they log off.
- [ ] 6.6 Put guards in each formation (line, column, wedge, square, circle). They should stand still in neat spots facing out, not spin in place.

## 7. Spawning and rivals (0.0.56)

- [ ] 7.1 Make two overlapping zones for factions at war, such as Mordor and Gondor. Now and then an enemy group should spawn 16 to 32 blocks from the first group, and the two should fight.

## 8. Hobbits (0.0.55)

- [ ] 8.1 Hobbits wear trader-style clothes, go barefoot, and now and then wear a hat.

## 0.0.73 additions

- [ ] 73.1 Watch an archer draw: as its hand pulls back, the string bends back into a V that follows the hand in three steps, and stays fully pulled while it aims.
- [ ] 73.2 When it shoots, the string snaps straight, then the bow goes back to its side.
- [ ] 73.3 With a close-range shove, the bow is upright with a straight string.
- [ ] 73.4 Type `/recruits dismissall`: it tells you how many recruits you have and asks for confirm. Type `/recruits dismissall confirm`: every recruit near you stops following and goes back to its own people.
- [ ] 73.5 Go to where a recruit was left far away (a guard post). It's free when you arrive (server log: "dismissed on load").
- [ ] 73.6 `/recruits fireall` works the same as dismissall.

## 74. Recruits and archer range (0.0.74)

- [ ] 74.1 `/hirelings` no longer exists. `/recruits`, `/recruits whitelist list` and `/recruits dismissall` work, and their help text says recruits.
- [ ] 74.2 Look at a hired NPC, hire past the cap, and let one die: none of the messages say "hireling".
- [ ] 74.3 Open `ModConfig/lotrnpcs-factions.json` after the first start: `Version` is 15, `MaxHirelings` is gone and `MaxRecruits` has the value you had before.
- [ ] 74.4 Walk at an archer: it keeps shooting until you're about 2 blocks away, then steps back (it used to back off at 4).
- [ ] 74.5 Archers shoot you from 24 blocks away on flat ground, and from up to 20 blocks above or below (a cliff or a tower).
- [ ] 74.6 Your R key for orders still works without rebinding it.

## 75. Misty Mountains weapons (0.0.75)

- [ ] 75.1 Spawn several Misty Mountains recruits, warriors and captains. Each carries your Moria sword or your Moria spear, in your textures, and keeps its shield.
- [ ] 75.2 The sword's straight edge leads the swing (the hooks on its back trail), as with the Mordor scimitar.
- [ ] 75.3 A spear orc walks and runs with the spear held level at its side; the butt end doesn't dig into the ground. It attacks with a forward thrust.
- [ ] 75.4 A Misty Mountains archer carries your Moria bow level at its side, and when drawing, the string bends back to its hand and stays there while it aims.
- [ ] 75.5 Misty Mountains orcs spawned before 0.0.75 pick up a sword or spear (and their archers the Moria bow) the next time their area loads.
- [ ] 75.6 In a hired Misty Mountains orc's looks menu, the weapon row offers Orc sword, Orc spear and None.

## 76. Weapons, promotion, whitelist menu (0.0.76)

Spawn fresh NPCs for the weapon checks; old ones pick up a weapon when they next load, but keep their old shield choice.

- [ ] 76.1 Dwarves (recruit, warrior, captain) carry the walking axe, battleaxe, dagger or boar spear, in your textures (the boar spear in plain wood and iron until its texture arrives). Some carry the tower shield or the iron shield, never with the battleaxe.
- [ ] 76.2 The dwarven battleaxe is held in both hands, the right hand above the left, and chops like the poleaxe.
- [ ] 76.3 Elves carry the elven spear, about as long as the Moria spear, and thrust with it.
- [ ] 76.4 Black Numenoreans carry the iron falx and sometimes the Numenorean kite shield across the forearm.
- [ ] 76.5 Gondor, Rohan, Dale, Arnor, Haradrim, Easterlings, Dunlendings, tormented elves, wicked dwarves and Isengard hold the game's iron falx. If a hand is empty instead, the client log says "weapon item game:blade-falx-iron does not exist": tell me.
- [ ] 76.6 Angmar and Gundabad orcs carry the orc dagger or axe; Misty Mountains orcs the sword, spear, dagger or axe.
- [ ] 76.7 Archers of every faction except Mordor and Misty Mountains carry the game's longbow (client log warning "game:bow-long does not exist" if not).
- [ ] 76.8 A Mordor poleaxe orc standing between swings: the right hand is higher up the shaft than the left.
- [ ] 76.9 Gundabad: no "(WIP)" in names; warriors wear the norc Gundabad helmet and armor, recruits and captains the bone armor.
- [ ] 76.10 Mordor and Isengard NPCs fight each other now (no default alliance). Check `ModConfig/lotrnpcs-factions.json`: Version 16, no Mordor/Isengard entry, `PromotePrices` under each faction.
- [ ] 76.11 Looks menu: a captain can pick its own helmet and the recruit and warrior helmets; a warrior its own and the recruit ones; a recruit only its own.
- [ ] 76.12 Hire a recruit and look at it: under its name and health it shows `XP: (0/15)`. A group mate sees it too; a stranger doesn't.
- [ ] 76.13 Let it fight NPCs of this mod: each kill it took part in within 10 seconds adds 1. Drifters and animals add nothing. At 15 you get a chat message.
- [ ] 76.14 Talk to it: "Promote to Warrior (10 × ...)" and "Promote to Archer (10 × ...)". Without enough coins it says so. With them, the menu closes, chat says it is now a warrior, it keeps its name, face, orders and post, wears warrior armor, and wears a helmet only if it wore one before.
- [ ] 76.15 Promote a recruit to archer: it shoots with its faction's bow. A warrior at 40 XP can become a captain for 30.
- [ ] 76.16 A guard on Loose formation chases an enemy out of its post and walks back to its post when the fight ends.
- [ ] 76.17 `/recruits whitelist` opens the whitelist menu: add a player by name or with an online player's button, remove one, and the list updates. The typed `add`, `remove` and `list` still work.


## 77. Two-handed holds, follow-and-fight, crowding, archers (0.0.77)

Restart the game fully with only the 0.0.77 zip. Respawn NPCs for the weapon and helmet checks.

- [x] 77.1 (confirmed in game 2026-11-04: "the dwarves and orcs now swing the axe properly. and hold it properly") A Mordor poleaxe orc and a dwarf with the battleaxe hold the weapon in both hands between swings, right hand above the left, and chop with both hands. The server log no longer says "attempted to play an animation code which its shape does not have: poleaxehold" (or poleaxechop). This is the first time the poleaxe chop can actually play.
- [ ] 77.2 Every other NPC still looks and moves as before (all NPCs now use the mod's own copy of the body, which is the game's body plus the two animations).
- [ ] 77.3 The dwarven walking axe hangs with its blade down at rest, like the Mordor axe.
- [ ] 77.4 (changed in 0.0.79) The boar spear points forward with its blade hanging toward the ground at rest, and thrusts head first.
- [ ] 77.5 Easterling recruits, warriors and captains sometimes carry the Easterling shield on the left forearm, not clipping the arm. Archers never do.
- [ ] 77.6 Misty Mountains warriors wear the Moria helmet (the recruits' one) with the Gundabad armor. Gundabad faction warriors still wear the Gundabad helmet. A Misty Mountains warrior's looks menu has no Gundabad helmet.
- [ ] 77.7 Followers on Line (or any formation but Loose), Aggressive, with an enemy group: they fight without running back to you between blows. Walk more than 12 blocks away: they leave the fight and come to you, and don't fight again until they're within 4 blocks of you. They ignore enemies more than 12 blocks from you.
- [ ] 77.8 Same on Loose: they keep fighting however far you go, and come back once no enemy is left. They aren't pulled to you at 48 blocks while fighting.
- [ ] 77.9 Passive followers still just follow.
- [ ] 77.10 Four or more melee NPCs against one enemy: three fight it, and the others join in too when there's no other enemy around (Rathgul's case). With several enemies, the extra NPCs still spread to the other enemies.
- [ ] 77.11 An archer behind your melee recruits keeps shooting through them; its arrows don't hurt them.

## 78. Haradrim armor mod (0.0.78 to 0.0.80, haradrim-armor 0.1.2)

Install `haradrim-armor-0.1.2.zip` (replaces 0.1.0 and 0.1.1) (mod id `haradrimarmor`) next to lotrnpcs 0.0.78 on the client and the server. Haradrim already spawned keep the outfit they have; spawn new ones.

- [ ] 78.1 Haradrim recruits and archers wear the infantry set, warriors the heavy infantry set, captains the warlord set (helmet, armor, leggings), and nothing of the old Nadiyan outfit.
- [ ] 78.2 Fit: no floating pieces; shin guards meet the thigh pieces at the knee and the thigh pieces meet the body at the waist (the legs were stretched to the game body, see `previews/haradrim-armor-0.0.78.png`). The warlord's right hand guard sits on the right forearm.
- [ ] 78.3 Looks menu: a Haradrim captain can choose the warlord, heavy or infantry helmet.
- [ ] 78.4 Players: the nine pieces are in the creative inventory (Clothing tab), can be worn, and show on the player. There are no recipes yet.
- [ ] 78.5 Without the armor mod: Haradrim spawn in the Nadiyan outfits as before, and the client log has no lotrnpcs errors.
- [ ] 78.6 (haradrim-armor 0.1.1) The robe, skirt and thigh pieces come with the leggings, not the chest armor: a player wearing only the chest armor has no skirt.
- [ ] 78.7 (0.1.2) Infantry and heavy infantry: the veil stops just below the eyes, so both eyes show between veil and turban or helmet. The captain's mask is unchanged.
- [ ] 78.8 (0.1.2, replaced by 81.1: owner, the leg armor phased through the legs) Walking: the robe, skirt, sash, coins and thigh scales hang from the waist and stay still while the legs swing; the trousers and greaves move with the legs. No skin shows on the inner thighs or at the top of the thighs.
- [ ] 78.9 (0.0.80) Any NPC wearing a helmet (any faction) shows no hair under or through it; take the helmet off in the looks menu and the hair comes back. Beards and mustaches stay (except under the Haradrim helmets since 0.0.81, see 81.2). The client log has no "could not hide the hair under its helmet" warning.

## 81. Haradrim leg armor follows the legs, beards under Haradrim helmets (0.0.81, haradrim-armor 0.1.3)

Install `lotrnpcs_0.0.81-test.zip` and `haradrim-armor-0.1.3.zip` (replaces 0.1.2) on the client and the server, and restart the game fully. Item codes are unchanged, so Haradrim already spawned pick up the new armor shapes; the beard change needs no respawn either.

- [ ] 81.1 Walking and running Haradrim of every rank: the robe, skirt, sash ends, coins and thigh scales move with the thigh they are on, and no leg passes through them. The heavy infantry's two red sash tails swing with the legs (right tail with the right leg, left with the left). At rest the outfit looks as in 0.1.2. Compare `previews/haradrim-armor-0.1.3-walk.png`.
- [ ] 81.2 A bearded Haradrim (or one with a mustache) wearing any of the three Haradrim helmets shows no beard or mustache through the veil or mask.
- [ ] 81.3 Take the helmet off in the looks menu: the same hair, beard and mustache come back. Put it back on: they are hidden again. Other factions' helmets still hide only the hair (a dwarf or a Gondor captain keeps his beard).
- [ ] 81.4 The client log has no "could not hide the hair under its helmet" warning.

## 82. Loadouts, placeholder weapons, fight movement, battle lines (0.0.82)

Install `lotrnpcs_0.0.82-test.zip` (one lotrnpcs zip only) and restart the game fully. The placeholder weapons need the (Not Only) Spear Expansion mod on client and server; without it those NPCs' hands stay empty and the client log names the missing items. New loadouts and weapons show on newly spawned NPCs (already spawned ones keep their outfit; their weapon is picked again only if it's no longer listed). The config file moves to version 17 on first start.

- [ ] 82.1 Loadouts, spawn a few of each: Gondor Men-at-Arms in one colour head to toe (blue, black, green, yellow, or the White Tree with black legs), with the Man-at-Arms, coif or Levy helmet; Fountain Guards always cloaked; Gondor archers in the Ranger set (not chainmail). Mordor recruits in raider or warrior armor, warriors (Black Uruks) in warrior armor, black or red, legs matching. Uruk-hai warriors with infantry, berserker or archer helmets, captains general ones. Misty Mountains warriors in the Gundabad or Mordor warrior helmet (no longer the Moria one). Angmar soldiers with either soldier helmet. Arnor warriors sometimes in the infantry helmet. Black Numenorean captains in the red Servant of the Eye armor with the red mask (no Lord set).
- [ ] 82.2 Helmets at spawn: Gondor, elves, Rohan Men-at-Arms, Dale Lake Guard and Watch, Arnor Levy, Black Numenoreans, Dwarf Lords and Dunlending Warlords always spawn helmeted; orcs, Uruk-hai, Eorlings, Huscarls, dwarf militia and warriors, Arnor warriors and lords, Dale lords sometimes bare-headed. Every unit's looks menu still offers No helmet.
- [ ] 82.3 Weapons (with the Spear Expansion): Gondor Men-at-Arms, Lake Guard, Arnor Levy and Easterling recruits mostly carry a pike (held like the Moria spear); Dalian Watch and Lords mostly the falx; dwarves axes, battleaxes and the war pick, Lords mostly the war pick; Easterling warriors halberds (two-handed, poleaxe swing, never with a shield) and khopeshes; the Easterling captain a flanged mace, bartka or falx. Check how each placeholder sits in the hand.
- [ ] 82.4 Two wild groups of different factions (say 4 Mordor orcs and 3 Gondor men, spawned 15 to 20 blocks apart): each forms a line facing the other, about 1.5 blocks between neighbours, and marches to meet it. They meet as pairs fighting 1 to 1.6 blocks apart, extras two on one at the ends, rather than one blob.
- [ ] 82.5 Fighting NPCs face their opponent between swings (no turning to and fro) and don't step in and out without swinging.
- [ ] 82.6 With more NPCs than twice the enemy (say 7 against 3), the extra one waits a step behind its line and steps in when a front fighter falls.
- [ ] 82.7 Your recruits following in Line (or any formation but Loose), Aggressive: they form a line against an enemy group and march; on Defensive they hold the line until enemies come within 6 blocks. On Loose they fight one to one without a line.
- [ ] 82.8 NPCs fighting you (a player) or drifters behave as before (no lines).
- [ ] 82.9 ModConfig/lotrnpcs-factions.json shows Version 17, MaxAttackersPerTarget 2, CombatSpacing 1.5, CaptainsAlwaysHelmeted false, BattleLines true, DuelDistanceMin 1.0, DuelDistanceMax 1.6, LineHoldRange 6 (values you changed yourself stay).
- [ ] 82.10 No lotrnpcs errors in the server or client logs during a battle.

## 83. Recruits fight without waiting to be hit (0.0.83)

Install `lotrnpcs_0.0.84-test.zip` (0.0.83 plus the 12-block limit removed; instead of 0.0.82 or 0.0.83) and restart the game fully.

- [ ] 83.1 Aggressive recruits following in Line (or another formation but Loose), with you within 12 blocks of a wild enemy group: they line up, march, and the moment one of them reaches an enemy the whole front rank attacks, without waiting to be hit.
- [ ] 83.2 Same on Loose: they go for the enemies straight away (no line).
- [ ] 83.3 (0.0.84) Stand more than 12 blocks from an enemy with formation followers: they go for it anyway (the 0.0.77 limit is gone). If one gets more than 12 blocks from you it still breaks off and comes back.

## 85. Fight first, formation second (0.0.85)

Install `lotrnpcs_0.0.85-test.zip` (instead of 0.0.82 to 0.0.84) and restart the game fully. ModConfig/lotrnpcs-factions.json should show Version 18, MaxAttackersPerTarget 0 and BattleLines false (unless you changed them yourself). Sections 82.4, 82.6, 82.7 and 83.1 now only apply with BattleLines set to true.

- [ ] 85.1 Aggressive recruits following you in any formation go straight for every hostile NPC they see, without waiting to be hit, and any number of them may go for one enemy.
- [ ] 85.2 With several enemies, they still spread out over them (each takes an enemy nobody is fighting first) instead of all piling onto one.
- [ ] 85.3 When the fight is over, followers go back to their places in the formation.
- [ ] 85.4 Fighters still stand 1 to 1.6 blocks from their enemy, face it, and don't step in and out.

## 86. Mixed weapon sets (0.0.86)

Install `lotrnpcs_0.0.86-test.zip` (instead of 0.0.85), with the Spear Expansion mod. Spawn new NPCs: existing ones keep their weapon if it is still in their set.

- [ ] 86.1 Every faction shows a mix of weapons across a dozen spawns, like the dwarves: Haradrim khopesh, pike, bartka or seax (no falx); Rohan pike, falx, bartka or seax; Dunlendings bartka, club, pike or seax; Uruk-hai falx, pike, bartka or seax; Angmar and Gundabad orcs the orc dagger, axe, pike or flanged mace; elves the elven spear, falx or seax; wicked dwarves the dwarven weapons and the war pick.
- [ ] 86.2 Each stand-in sits in the hand sensibly and swings with a fitting animation (seax stabs, club and maces hammer, bartka chops, halberd two-handed). Note any that look wrong.

## 87. Rings instead of a pile (0.0.87)

Install `lotrnpcs_0.0.87-test.zip` (instead of 0.0.86).

- [ ] 87.1 A big group (10 or more) against one or two enemy NPCs: about five fight each enemy from close in; the rest stand in a wider ring around them, facing in, and one steps in whenever a fighter falls. No pile.
- [ ] 87.2 With more enemies, the group spreads over them first: nobody waits in an outer ring while some enemy has room.
- [ ] 87.3 Same with your recruits against wild NPCs and wild NPCs against each other.

## 88. Edge guard (0.0.88)

Install `lotrnpcs_0.0.88-test.zip` (instead of 0.0.87).

- [ ] 88.1 Lead followers along the edge of a deep pit, and across a narrow bridge over one: none walk off. Across a gap with no way round, they stop at the edge (and teleport to you if you go more than 48 blocks away).
- [ ] 88.2 Put an enemy across a deep pit: NPCs going for it stop at the edge instead of jumping in.
- [ ] 88.3 Drops of up to 4 blocks and jumps into water still work (they take the step down), and NPCs still walk normally up and down hills and stairs.

## 89. One command: /recruits (0.0.89)

Install `lotrnpcs_0.0.89-test.zip` (instead of 0.0.88). The old commands /lotrfaction, /lotrzone and /lotrorders are gone (earlier sections that name them now mean /recruits faction, /recruits zone and /recruits orders).

- [ ] 89.1 `/help recruits` lists dismissall, whitelist, orders, faction and zone; `/help recruits zone` lists the zone commands.
- [ ] 89.2 `/recruits orders` opens the orders menu; `/recruits whitelist` opens the whitelist menu.
- [ ] 89.3 As an admin, `/recruits faction list` shows each faction's stance, friendly races and groups and the alliances, with no unit prices or coin; `/recruits zone list` and `/recruits zone spawnnow` work.
- [ ] 89.4 A player without admin rights gets "Only server admins can use /recruits faction and /recruits zone." (or the game's own refusal) for those two.

## 90. Factions "unequally equal" (0.0.90)

Install `lotrnpcs_0.0.90-test.zip` (instead of 0.0.89). The faction file moves to version 19 on the first start (see the config guide, "Faction balance").

- [ ] 90.1 `/recruits faction list` shows each faction's follow limit, roster and multipliers (Mordor: follow 7, roster 22, health x0.66, damage x0.85). The hover shows an orc recruit at 15 health, a goblin recruit at 13, a dwarf captain at 64; a Gondor recruit still 22.
- [ ] 90.2 Hire 7 Mordor orcs: all follow. The 8th guards where it was hired. Its talk menu says "Room in your band: Mordor orcs 0."
- [ ] 90.3 Mixed band: 3 Gondor men and 2 orcs follow (needs a player in both factions, or an ally with ShareFriends). Telling a third orc or a goblin to follow fails with the room line; the orders menu's Follow says how many stayed behind.
- [ ] 90.4 Prices: a Mordor recruit costs 4, a warrior 11, a captain 29; a Gondor recruit still 5; an elf recruit 6.
- [ ] 90.5 Over the limits: in a world with more than 13 dwarves (or lower an orc faction's MaxRecruits and run `/recruits faction reload`), the most recently hired go with a chat line naming them; far-away ones go when you come near them. Nobody else is touched.
- [ ] 90.6 Promoting an orc keeps it an orc (same shares, its new rank's health times 0.66).
- [ ] 90.7 `/recruits faction reload` after setting Mordor's HealthMultiplier to 1: loaded orcs take full type health, wounded ones keep their wounds. Set it back.
- [ ] 90.8 Wild groups: orc zones bring groups of up to 7, goblin zones up to 8, elf zones up to 4. With 8 goblins nearby, another group may still come.
- [ ] 90.9 Damage log: an orc recruit's hit on an unarmored player is about 2.55 (a Men recruit's 3); an elf captain's about 9.45.
- [ ] 90.10 Allied hiring: set Mordor and Isengard to ally with ShareFriends true and reload. A player in a Mordor group can now hire Uruk-hai (6 coins for a recruit), and the two mix in one band.
- [ ] 90.11 Fights between full bands (for example 5 Gondor men against 7 Mordor orcs, both mixed ranks): neither side should win almost every time.

## 91. Per-rank balance, hit spread, lifetime despawn, roster line (0.0.91)

Install `lotrnpcs_0.0.91-test.zip` (instead of 0.0.90). The faction file moves to version 20 on the first start.

- [ ] 91.1 `/recruits faction list` shows each faction's per-rank health and damage (Mordor: health 15/27/31/22, damage 2.5/2.8/5.95/2.45) and "Hits vary by 20% either way." The hover shows an orc warrior at 27 health, a dwarf captain at 63.
- [ ] 91.2 Same-rank fights: 5 Rohan warriors against 7 Mordor warriors, several times. Each side should win some (about half each in the simulator). Then try another pair, for example 4 elf warriors against 8 goblin warriors.
- [ ] 91.3 The damage log shows hits varying (a Men recruit's hit on an unarmored player between about 2.4 and 3.7).
- [ ] 91.4 Talk to an NPC you could hire: under its greeting a grey line says the room in your band and on your roster. Hire it: the same line after its answer, and in chat "You hired ... Room in your band: ... Room on your roster: ...".
- [ ] 91.5 Fill your roster with one faction (lower its MaxRecruits for a quick test and reload): the next hire is refused with "Room on your roster: ... 0."
- [ ] 91.6 Lifetime despawn: stay in a zone for 20 minutes or more. Old wild NPCs more than 64 blocks away disappear (not while fighting, never right next to you); new groups keep coming. Your recruits and creative-spawned NPCs stay.
- [ ] 91.7 A world from 0.0.90: the config upgrades (no HealthMultiplier left in the file), and NPCs take the new per-rank health when they load.

## 92. Joining a faction (0.0.92)

Install `lotrnpcs_0.0.92-test.zip` (instead of 0.0.91). The faction file moves to version 21 on the first start.

- [ ] 92.1 A new player: make a character. When the character screen closes, the "Choose your faction" menu opens, with 18 factions in two columns (name, stance, follow and roster).
- [ ] 92.2 Click a faction: it asks "Join the ...? This is permanent". Back returns to the list; Join closes the menu and chat says "You joined the ...". `/recruits faction members` lists you.
- [ ] 92.3 Its NPCs leave you alone and offer Hire; an enemy faction's NPCs attack you.
- [ ] 92.4 `/recruits join` after picking says "You are a member of the ... Only an admin can change your faction."
- [ ] 92.5 A second player: press Escape (or Decide later) in the menu. Chat shows the reminder with /recruits join. `/recruits join` opens the menu again. Rejoining the world without a faction opens it again.
- [ ] 92.6 An existing player whose faction came from their race (an elf, a dwarf, a hobbit): after the update they have no faction and get the menu on joining.
- [ ] 92.7 Admin: `/recruits faction set <player> gondor` moves a player; `/recruits faction clear <player>` lets them pick again from the menu.
- [ ] 92.8 Set a faction's `Joinable` to false and reload: it is gone from the menu (`/recruits join` on a player without a faction).
- [x] 92.9 A player with the character screen open for a long time: the menu waits until it closes. (Owner, 2026-11-07: "yes the join menu waits".)

## 93. Join menu cards and promotion prices (0.0.93)

Install `lotrnpcs_0.0.93-test.zip` (instead of 0.0.92). The faction file moves to version 22 on the first start. Preview of the menu's layout: `previews/join-menu-0.0.93.png` (offline font; the faces are drawn by the game).

- [ ] 93.1 Open the menu (`/recruits join` on a player without a faction, or `/recruits faction clear <player>` first). Factions are cards in three columns under Free Peoples (7), Shadow (8, with Gundabad) and Others (Dunlendings, Wicked dwarves, Tormented elves).
- [ ] 93.2 Every card shows the faction's portrait (its warrior; the hobbit for the Shire), sitting inside the dark box on the left. Note any card without a face or a face spilling out of the box.
- [ ] 93.3 Names fit: "Goblins of the Misty Mountains" takes two lines and stays inside its card; no text runs into the next card. Each card says "N followers · M total"; only Hobbits have a green "defensive" tag.
- [ ] 93.4 A card lights up under the mouse. Clicking one shows that card next to "Join the ...? This is permanent", with Join and Back. Pressing on one card and releasing elsewhere picks nothing.
- [ ] 93.5 The line under the cards explains followers and total; Decide later sits at the bottom right. Try GUI scale 1 and a larger one (Settings > Interface): the menu still fits on screen.
- [ ] 93.6 Promotion: a hired Men recruit with 15 XP offers warrior or archer for 12 coins; a warrior with 40 XP offers captain for 36. Mordor: 8 and 25. Elves or dwarves: 16 and 46. Goblins: 6 and 21.
- [ ] 93.7 An existing server's faction file: promotion prices you never changed move to the new ones; ones you changed stay (check `ModConfig/lotrnpcs-factions.json`).

## 94. Menu text, spawn cap, weapons and shields, sight, cliff archers, hiding, rusty gears (0.0.94)

Install `lotrnpcs_0.0.94-test.zip` (instead of 0.0.93), on the server and your game; restart fully (the DLL changed). Job script `prompts/0.0.94-menu-fix-spawncap-spear-sight.md`. Previews: `previews/join-menu-0.0.94.png`, `previews/shields-spears-0.0.94.png`, `previews/dwarf-weapons-0.0.94.png` (offline, flat colours).

- [ ] 94.1 Join menu (`/recruits join` on a player without a faction): every card's "N followers · M total" is small, light and inside its card; Hobbits just say "Hobbits" (no tag).
- [ ] 94.2 Spawn cap: `ModConfig/lotrnpcs-spawning.json` now has `"Version": 1` and `"MaxNearby": 24` (unless you had set your own). In a zone, `/recruits zone spawnnow` keeps working until about three times as many NPCs are around as before.
- [ ] 94.3 Rusty gears: `ModConfig/lotrnpcs-factions.json` is version 23 and every `HireCoin` is `game:gear-rusty`. Hiring a Men warrior takes 15 rusty gears; the talk menu names the item. Promotion takes rusty gears too.
- [ ] 94.4 Weapons: elves' spear is as long as the Moria spear (2.25 blocks) and held at the right place; the dwarven walking axe and battleaxe are 20% bigger, the boar spear 10%; the dwarven dagger is knife-sized. The battleaxe's left hand is still on the handle.
- [ ] 94.5 Haradrim carry the owner's spear (no Spear Expansion pike any more), thrust it, and hold it level between thrusts.
- [ ] 94.6 Shields: Gondor recruits, warriors and captains carry the curved shield or Boromir's round shield (about half each); elves the elven shield; nobody else. Shields sit on the forearm like the others and don't cut into the body. The elven shield is the biggest (1.5 blocks): say whether it needs to be smaller.
- [ ] 94.7 Sight: an enemy NPC behind a wall (no gap) is not picked as a target; one in the open is. A target that ducks round a corner is followed for about 5 seconds, then let go.
- [ ] 94.8 Calls for help: an NPC hit by someone it can't see doesn't summon allies who can't see the attacker either.
- [ ] 94.9 Cliff archers: stand 10 blocks below an enemy archer on a cliff, 5 to 15 blocks out. It walks to the edge and shoots down at you.
- [ ] 94.10 Hiding: shoot a wild melee NPC with a bow from a ledge it can't climb (3+ blocks up). It runs to a spot you can't see (behind a tree, rock or wall within about 16 blocks), or away from you if there's none, and comes back out about 10 seconds after your last hit. Come down to it: it fights.
- [ ] 94.11 Hiding, recruits: your followers hit by an archer they can't reach hide no further than 12 blocks from you; guards stay inside their guard area.

## 95. Gundabad helmets, wicked dwarves under Shadow, captain's sword, dwarf hammers (0.0.95)

Install `lotrnpcs_0.0.95-test.zip` (instead of 0.0.94) and `gundabad-bone-armor-0.3.0.zip` (instead of 0.2.0), on the server and your game; restart fully. Job script `prompts/0.0.95-gundabad-helmets-wicked-dwarves.md`. Preview: `previews/weapons-helmet-0.0.95.png` (offline, flat colours; the hammers on the dwarf body).

- [ ] 95.1 Gundabad warriors wear the Gundabad orc set with either the Gundabad warrior helmet or the tusked helmet (some bare-headed, like all orcs). Recruits stay bare-headed in bone armor.
- [ ] 95.2 Gundabad captains wear the Gundabad Warlord body and legs with the warlord helmet, the tusked helmet or your new Gundabad Helmet; some still wear the bone armor. Misty Mountains captains are unchanged (warlord helmet or Gundabad warrior helmet).
- [ ] 95.3 Your Gundabad Helmet (creative inventory, "Gundabad Helmet") sits on the head as in Blockbench, on an NPC and on a player; hair doesn't poke through. Its stats are placeholders; it has no recipe.
- [ ] 95.4 The looks menu of a Gundabad captain lists all its helmets (and the warrior's) plus "No helmet".
- [ ] 95.5 Join menu: Shadow has nine cards, the wicked dwarves last; Others has the Dunlendings and tormented elves.
- [ ] 95.6 Gondor captains carry the pike or the Captain's sword (about evenly), sometimes the seax or halberd, never the falx. The sword is two-handed (no shield), 1.35 blocks, held with the right hand at the top of the handle and the left on the pommel. Say if the hold looks wrong.
- [ ] 95.7 Dwarves and wicked dwarves carry your warhammer (two-handed, no shield) and shorthammer (one-handed, with a shield) instead of the war pick. Say if either hammer's face points the wrong way when swinging (it can be turned over), or if the 2.1-block warhammer is too long.

## 96. No Spear Expansion, Gundabad spear, shields closer, bigger captain's sword, shoulder carry, Gundabad captain and ears (0.0.96)

Install `lotrnpcs_0.0.96-test.zip` (instead of 0.0.95); keep `gundabad-bone-armor-0.3.0.zip`. The Spear Expansion mod is no longer needed (sections 82.3 and 86.2 no longer apply). Restart fully. Job script `prompts/0.0.96-no-spear-expansion-carry-shields.md`. Previews: `previews/shoulder-carry-dwarf-0.0.96.png`, `previews/shoulder-carry-human-0.0.96.png`.

- [ ] 96.1 No NPC holds a Spear Expansion item, and the client log has no "item does not exist" warning for `spearexpantion:`. NPCs that held one before now carry another weapon.
- [ ] 96.2 Most Men, Uruks, tormented elves and Easterlings carry the game's iron falx (if their hands are empty, the falx code is wrong: say so). Haradrim carry only their spear; Gondor captains only the Captain's sword.
- [ ] 96.3 Gundabad orcs (all ranks) carry the orc dagger, orc axe or the Moria spear; the spear is thrust and held level like the Misty Mountains orcs'.
- [ ] 96.4 New Gundabad captains never wear the bone armor (already spawned ones keep theirs).
- [ ] 96.5 Under your Gundabad Helmet the ears are hidden; take the helmet off in the looks menu and they come back. Other helmets leave the ears alone.
- [ ] 96.6 The dwarven tower and iron shields, the elven shield and both Gondor shields sit closer to the arm, with no visible gap. Say if any now clips through the sleeve.
- [ ] 96.7 The Captain's sword is about 2 blocks long (1.5x), still held in both hands upright between swings.
- [ ] 96.8 Poleaxe (Mordor), dwarven battleaxe and warhammer are carried on the right shoulder between swings, head behind, the left arm swinging while walking. The chop still lands, and the move into it looks acceptable.

## 97. Haradrim sword, curved sword, light and heavy shields (0.0.97)

Install `lotrnpcs_0.0.97-test.zip` (instead of 0.0.96); restart fully. Job script `prompts/0.0.97-haradrim-swords-shields.md`. Preview: `previews/haradrim-weapons-shields-0.0.97.png` (offline, beside the Mordor scimitar).

- [ ] 97.1 Haradrim recruits, warriors and captains carry the spear, the sword or the curved sword (about a third each). Both swords are held by the middle of the grip and slash like the Mordor scimitar. Say if an edge faces the wrong way (each can be turned over) or if either is too long (sword 1.54 blocks, curved sword 1.28).
- [ ] 97.2 Haradrim recruits with a shield carry the light shield; warriors and captains the heavy wicker shield. Both stand along the forearm, face out, strap side toward the elbow, resting on the sleeve. Archers carry none.
- [ ] 97.3 The heavy shield (1.3 blocks tall; offline its lowest point stays 1.1 px above the ground while walking) doesn't drag on the ground. Say if it should be smaller.
- [ ] 97.4 Shields go with every Haradrim weapon (all three are one-handed): check the spear and a shield together look right.

## 98-109. Combat, battles, balance and the battle log (0.0.98 to 0.0.109)

Install `lotrnpcs_0.0.109-test.zip` (instead of any older lotrnpcs zip); restart fully. Spawn fresh NPCs. Before the battle steps, as admin run `/recruits battlelog on`; afterwards `/recruits battlelog off` prints the totals and the CSV is in the game's `Logs` folder. Send the totals, the CSV and server-main.log.

- [ ] 98.1 Three of your recruits on one enemy NPC don't all swing at the same moment, and each blow shows damage (0.0.98 hit turns).
- [ ] 99.1 Each weapon's blow lands when the swing visibly connects, also right after the NPC turns to face its target (axe and hammer slower to land, knife and spear quicker).
- [ ] 100.1 Kite, Easterling and Gondor shields touch the hand with no gap.
- [ ] 101.1 Gondor/Arnor NPCs carry a steel spear or falx, Rohan/Dale iron spear or falx with a wood-and-metal shield, Dunlendings clubs, flint spears, ruined axes and the crude shield; Uruks iron falx, spear or ruined battle axe. Nobody is empty-handed. Say if a game shield shows the wrong texture.
- [ ] 102.1 With two players on enemy factions, one player's aggressive recruits attack the other player and their recruits on sight. A player with no faction is left alone.
- [x] 102.2 No more than 4 NPCs attack one NPC at once; the rest find another enemy or wait. (2026-11-16 `215004`: distinct attackers on one target within 3 s: 1 in 178 blows, 2 in 85, 3 in 20, 4 in 10, 5-6 in 2 (a swap inside the window))
- [ ] 104.1 A marching column of your recruits doesn't walk through each other, and NPCs walking past a standing group steer round it.
- [ ] 104.2 Two groups of wild NPCs meeting form lines and fight one-on-one along the front; the side with more then wraps round the enemy.
- [ ] 104.3 Hiring or promoting past the captain limit (Men 2, orcs and goblins 3, elves and dwarves 1, Uruks and Dunlendings 2) is refused with a message.
- [ ] 105.1 An archer on a wall more than 2 blocks above its target keeps shooting instead of jumping down into melee.
- [ ] 106.1 In a follow formation, captains stand front-middle, then warriors, then recruits; archers at the back or the ends.
- [ ] 107.1 Mordor/Angmar/Gundabad orcs follow up to 6 at a time.
- [x] 108.1 Melee NPCs swing about every 2.6 to 3 s. Fighting a group of 3 or more: say if it now feels too fast or too slow. (2026-11-16 `215004` 9 v 9: swings every 2.95 s (Gondor) and 3.05 s (Mordor) median, 2.8-3.5 s middle half; the feel question is the owner's)
- [ ] 108.2 Hire prices match the new table (for example Mordor 5/12/29/12, Elves 6/19/50/20); promotion costs the new rank's price minus 3 (captain minus 4).
- [x] 109.1 The big one: 100 Mordor orcs against 50 Haradrim (a shared zone or creative spawns), battle log on. Send the totals. Then set `NpcHitsIgnoreHurtWindow` to true in lotrnpcs-factions.json, `/recruits faction reload`, and repeat with the log on again. (2026-11-16 on 0.0.147 (`221011`, NpcHitsIgnoreHurtWindow already true by default since 0.0.112, so run once): 100 Mordor orcs v 50 Haradrim in batches of 5 within ~30 blocks, Synergy on. 93 s: mordor 99 NPCs, 691/839 landed (103 out of reach), 1362 damage, 49 kills, 0 deaths, 81 back hits; haradrim 49 NPCs, 266/313 (47), 528 damage, 0 kills, 49 deaths, 243 back hits (surrounded). The 50th Haradrim was dead at cleanup)
- [x] 109.2 During that fight, note the server's lag (any "Server overloaded" lines in server-main.log) and whether many NPCs stand around not fighting; the log's "idle while fighting" counts those. (2026-11-16: no "Server overloaded" or running-behind lines in any server log during the fight, no lotrnpcs errors; idle while fighting: mordor 76 NPC-seconds, haradrim 3)

## 110. Extras on the flanks (0.0.110)

- [ ] 110.1 Rerun 38 Misty Mountains orc warriors against 26 Dunlending warriors with the battle log on. Before contact the orcs form one long rank (no rank behind), wider than the Dunlendings' line at both ends. At contact the middle pairs off one-on-one and the extra orcs wrap round the Dunlendings' end men instead of swarming the middle. Send the CSV: the win margin should be closer than 26 to 2.

## 111. Why NPCs stand idle (0.0.111)

- [x] 111.1 Same fight as 110.1 (38 orc warriors v 26 Dunlending warriors, log on). The chat totals now say what idle NPCs were doing, e.g. "idle while fighting 477 NPC-seconds (FactionSeek 60%, none 25%, ...)". Send the CSV: its new "idle" rows show each idle second with the NPC's running tasks, target and distance, so the next build can fix the biggest cause.

## 112. Reserve captains, safe spawn, bodies, patrols (0.0.112)

- [x] 112.1 Battle lines with captains: before contact the captains stand one step behind the middle of their line; at contact they join the fight.
- [ ] 112.2 Within 64 blocks of the world spawn, aggressive and guarding recruits of an enemy side don't attack you on sight. Hit one: it and its group fight back.
- [x] 112.3 Kill a wild NPC and a recruit of each rank. Each body opens to a knife like a drifter's (with Combat Overhaul, hold Shift and right-click with the knife): rusty gears (recruit 0-1, warrior/archer 1-2, captain 3-5) and maybe one cloth or leather, no armor or weapons. The body rots away after about 3 in-game hours. Change `HiredBodyLoot` in lotrnpcs-factions.json, `/recruits faction reload`, and check a recruit's new body follows it.
- [x] 112.4 Give a zone 3 points with `/recruits zone patrol <n>` at three spots, then `/recruits zone spawnnow` inside it. The group walks to the nearest point, then round the points in order, pausing at each; it still fights anything hostile on the way. `/recruits zone list` shows the points.
- [x] 112.5 After the update, lotrnpcs-factions.json says `"Version": 29` and `"NpcHitsIgnoreHurtWindow": true`, and `/recruits battlelog on` reports "NPC hits ignore the hurt window: yes".
- [ ] 112.6 New weapons in hand (spawn several of each): Gondor recruits/warriors the Gondorian sword, captains the Captain's sword or the steel sword; Rohan the Rohirric spear and sword; Black Numenorean recruits/warriors the Numenorean spear, captains the steel sword, Morgul blade or spear; elves the two-handed elven longsword (no shield with it); Easterlings the two-handed Easterling axe (no shield). Each sits in the hand by its handle, blade forward like the Haradrim sword, and swings or thrusts.
- [x] 112.7 Steel only for captains: dwarf and wicked-dwarf captains carry the steel warhammer or shorthammer, recruits and warriors only the iron ones.
- [ ] 112.8 Shields: Black Numenoreans carry their new black shield (red emblem facing out); Mordor orcs carry their own shield or the black one; Arnor carries the Numenorean kite shield. Each sits on the forearm without floating or sinking in, in idle, walk and attack.
- [x] 112.9 Orc helmets in the looks menu: each orc and Uruk unit offers only its own helmets (Black Uruk: Orc Warrior helmet; Mordor captain: red mask; Uruk captain: General helmets; Gundabad captain: warlord, tusked or Gundabad Helmet), plus "No helmet". Other factions still offer lower ranks' helmets.

## 113. New weapons, ranks, roster and promotion screens (0.0.113)

- [x] 113.1 Rohan NPCs carry the Rohirric axe as well as the spear and sword: held by the wrap at the bottom of the handle, blade forward like the orc axe, swung like it, with a shield. It is 1.56 blocks long, the same scale as your other models; say if you want it shorter (a real one-handed axe is about 0.9 blocks).
- [x] 113.2 Easterlings carry the Easterling scimitar instead of the game's Forlorn blade: held by the handle, the edge (the curve's outer side) leading the swing like the Haradrim curved sword, with the Easterling shield. It is 0.93 blocks long.
- [x] 113.3 Promotion screen: get a recruit to 15 XP, talk to it and pick "Promote...". A screen opens with four cards (Warrior, Guard, Shock trooper, Archer), each with a portrait, health, damage, weapons, what it can become and the price. A card you can't afford says "Not enough coins". Promote one: the coins go, the screen closes, chat says it was promoted, and its name, looks and orders stay.
- [x] 113.4 Guard: a promoted guard has warrior health x1.25 (Men 43) and always carries its faction's shield. Since 0.0.115 its looks menu has a shield switch: turning the shield off (or picking a two-handed weapon) makes it a warrior (or shock trooper), see 115.1. Factions without a shield of their own carry a stand-in: Uruk-hai and tormented elves the game's wood-and-metal shield, Angmar and Gundabad the crude shield, wicked dwarves the dwarves' shields. Their warriors still carry none.
- [ ] 113.5 Shock trooper: health x0.8 (Men 27), no shield; dwarves carry the battleaxe or warhammer, elves the longsword, Easterlings the axe, Mordor orcs the poleaxe; every other faction the game's ruined bardiche (a stand-in until its own two-hander comes). Check the bardiche is held and swung with both hands.
- [x] 113.6 Guards and shock troops earn XP and become captains at 40 XP (the captain card shows "Captain places full" when it is). They never spawn wild.
- [x] 113.7 Roster: `/recruits roster`, or the Roster button at the bottom of the orders menu (hold R, click it). Every recruit is listed with name, rank, XP, order, stance and where it is. The filters and column sorting work; the list refreshes every 2 seconds.
- [ ] 113.8 Roster buttons: Map puts a marker on the world map (gone when you rejoin); Call makes a loaded recruit follow you (refused when your band is full); Promote opens the promotion screen from any distance; Dismiss asks "Sure?" first. Walk far away so one unloads: its row says "Not loaded: last seen x / z, n min ago", and Dismiss still works (it leaves when its area loads).
- [ ] 113.9 Recruits hired before 0.0.113 show "Not seen since the update" until they first load.
- [x] 113.10 Old config: an existing lotrnpcs-factions.json gains guard and shock health and damage from its own warrior values, and PromotePrices guard and shock at the warrior's price.

## 114. Fewer model rebuilds, quieter logs, line fixes, no FA Races (0.0.114)

- [x] 114.1 Followers in a battle line (any formation but Loose) hold their place between swings instead of walking back to you. They go back to following when the line breaks up, when you are more than 12 blocks away, or on a move order.
- [x] 114.2 Stragglers: start a wild fight, then spawn one more NPC of one side about 17 blocks behind it. It runs straight to its side's line and joins it, instead of walking in alone over about 20 seconds.
- [x] 114.3 Frame rate: turn on `.debug logticks 30` and watch a fight of 20 or more NPCs. In client-main.log, `esr-tesseleateshape` should show up only when an NPC spawns, is promoted or changes gear, not several times a second. Giving move and formation orders to a big band, and the game's autosave, cause no stutter.
- [x] 114.4 Orders still reach your screen: switch recruits between Hold, Guard and Follow, and between a formation and Loose. Each takes effect at once, and again after you leave and rejoin the world.
- [x] 114.5 Gear still updates: take a recruit's helmet off and put it back in the looks menu, and change its weapon. You (and a second player, if one is on) see each change at once, and it is still right after you rejoin.
- [x] 114.6 Logs: client-main.log has no "duplicated attachment point code 'Eyes'" warnings and no "Step parented shape angmar:..." warnings, and no "Failed to apply patch" naming lotrnpcs:patches/angmar-stepparent.json. NPCs in Angmar armor look exactly as before, with no pieces missing.
- [x] 114.7 Config: after the update, lotrnpcs-factions.json says `"Version": 30`, and no faction's FriendlyRaces lists a `faraces:` race.
- [ ] 114.8 Admin XP: look at your hired recruit and type `/recruits xp 15`. Its XP line shows 15/15, you get the "ready to promote" message, and Promote is offered in its talk menu. `/recruits xp -100` takes it back to 0. Looking at an archer, a captain, a wild NPC, a dead recruit or nothing gives an error. A player without admin rights is refused.
- [x] 114.9 Bench: on open flat ground type `/recruits bench 100` and keep looking at the fight. Two armies appear about 16 blocks ahead and fight. About 35 s later chat shows a "bench client" line (FPS, 1% low, worst frame, vsync, view distance) and a "bench server" line (ticks), and both are in the logs. `/recruits bench clear` removes every bench NPC and body. Run it at 50, 100 and 200 with vsync off and send the three client lines.

## 115. A warrior's gear picks its class, helmet row, promotion screen, Moria helmet (0.0.115)

- [x] 115.1 Class from gear: in a hired warrior's looks menu pick a two-handed weapon. Chat says it is now a shock trooper; turn its shield on with a one-handed weapon and it becomes a guard; neither makes a warrior again. Its XP stays.
- [x] 115.2 Cancel: open the looks menu of a warrior with its shield on and press Cancel. It stays a warrior.
- [x] 115.3 Helmet row: the looks menu of any recruit that can wear helmets has a Helmet row, and the helmet you pick shows on the model.
- [x] 115.4 Promotion screen: a warrior with 40 XP picks "Promote...". The single captain card sits in a screen wide enough for the title, the XP and coins line and the whole last sentence.
- [x] 115.5 Moria helmet: Misty Mountains warriors, guards and shock troopers offer the Moria Orc Helmet in the looks menu, and some spawn wearing it.
- [x] 115.6 Testing commands off: on a new or updated config, `/recruits xp 15`, `/recruits bench 20`, `/recruits battlelog on` and `/recruits zone spawnnow` each refuse with a message naming DebugCommands. Set `"DebugCommands": true` in ModConfig/lotrnpcs-factions.json, run `/recruits faction reload`, and they work again.

## 116. Far NPCs animate less often (0.0.116)

- [x] 116.1 FPS: with `DebugCommands` on, `.npcanimlod off` then `/recruits bench 200`, then `.npcanimlod 10` and the bench again. The second run has clearly higher FPS (owner's PC: 64.7 against 103.2); the server line is the same.
- [ ] 116.2 Looks: at the default (10 blocks), watch a battle from close and from 40 to 80 blocks. Up close nothing changes; far NPCs may move a little less smoothly but never freeze, slide or snap. Say if 10 should be lower (more FPS) or higher.
- [x] 116.3 ModConfig/lotrnpcs-client.json appears after the first start with FarAnimationLod true and AnimationFullRateWithin 10; `.npcanimlod off` writes false, and the setting holds after a restart.

## 117. Mounted riders (0.0.117)

- [x] 117.1 With Feverstone Wilds installed, a Rohan rider (creative tab, next to the captain) spawns already sitting on a saddled horse.
- [x] 117.2 Promotion: a hired warrior with 40 XP shows a Rider card beside Captain (the captain's price). Promoted, it comes on its own horse; on Follow its horse carries it after you and stops beside you.
- [x] 117.3 Fights: a rider fights an enemy from the saddle and lands hits. Watch that it doesn't stand off out of reach.
- [x] 117.4 Kill a rider: its mount gallops away and is gone after a few seconds. Kill a rider's mount: the rider fights on foot.
- [x] 117.5 Leave and rejoin the world: riders are still on their mounts.
- [ ] 117.6 Orc riders on dire wolves, elves on elk, dwarves on giant boars (needs Teapot's Giant Boar), each saddled and moving.
- [x] 117.7 In a spawn zone, now and then a wild group has one rider.
- [ ] 117.8 Without Feverstone Wilds, no Rider card is offered and no riders spawn wild.


## 118. Lines that meet together (0.0.118)

Install `lotrnpcs_0.0.118-test.zip`; restart fully.

- [x] 118.1 Rerun 110.1 (38 Misty Mountains orc warriors v 26 Dunlending warriors, battle log on), standing within about 40 blocks of everyone (Synergy freezes NPCs past 48). Once any part of a side's line has met the enemy, every other line of that side within 24 blocks fights too: after contact, the CSV's idle rows show almost no "line-forming" for either side.

## 119. Whole armies form up (0.0.119)

Install `lotrnpcs_0.0.119-test.zip`; restart fully.

- [x] 119.1 Spawn two armies wider than 24 blocks facing each other (e.g. batches of 5 down two columns 40 blocks apart). Before contact each side forms one long line of all its warriors, not just those near an enemy; nearly every NPC fights.
- [ ] 119.2 Your recruits following in a formation join a battle line that forms near them only if they're set to the same formation as the recruit that started it (Line with Line, Wedge with Wedge); Loose followers never do.

## 120. Lines pair off at contact (0.0.120)

Install `lotrnpcs_0.0.120-test.zip`; restart fully.

- [ ] 120.1 Staged 38 goblin warriors v 26 Dunlending warriors as in 110.1 (batches of 5, columns 40 blocks apart, player at about 1500 15 300, battle log on). For at least 20 s after contact the two lines stay two lines: each front fighter fights the enemy across from it, and the extras come in at the ends instead of the middle folding into one blob. In the CSV, no Dunlending is hit by more than 2 different orcs in any 5 s stretch (dead targets aside), and the result is closer than 0.0.119's 24-4.
- [ ] 120.2 Your recruits following in Line with a Passive stance next to a forming line of yours aren't pulled into it (they stay with you, and the line's march doesn't stall).

## 125. Riders sit still, prefer spears, reach farther (0.0.125)

Install `lotrnpcs_0.0.125-test.zip`; restart fully. Needs Feverstone Wilds.

- [x] 125.1 A Rohan rider (creative tab) walking, running and standing on its horse: its legs hold the riding pose (rideelk) and never play the walk or run cycle. On foot after its horse dies, it walks normally again.
- [x] 125.2 Spawn about ten Rohan or Gondor riders: most hold a spear (spears weigh 3 times their foot weight).
- [ ] 125.3 A rider fights a goblin warrior on foot from the saddle and lands hits without riding on top of it (reach is now a block past the foot reach, plus the seat height).

## 126. Riders walk when wandering and close in to fight (0.0.126)

Install `lotrnpcs_0.0.126-test.zip`; restart fully. Clear leftover test NPCs first (explicit types and a range).

- [x] 126.1 Spawn 5 Rohan riders with no enemies around: they wander at a walk and stay within about 20 blocks for a minute (in 0.0.125 they galloped 30-50 blocks off). (passes on 0.0.147, see 147.1)
  - 2026-11-16 on 0.0.146: after ~70 s, 0 of 5 riders within 30 blocks, 1 within 50, all 5 within 100. Failed; 0.0.147 puts riders' wander on a 12-block leash (147.1).
- [x] 126.2 Spawn 3 goblin warriors 15 blocks from them: the riders ride in, the horses walk up to each goblin and the riders land hits; no rider stands off out of reach for more than a few seconds.
- [ ] 126.3 A hired rider on Follow still keeps up when you sprint away (its horse runs when the rider runs).

## 127. Tiny gear pieces left out (0.0.127)

Install `lotrnpcs_0.0.127-test.zip`; restart fully.

- [ ] 127.1 ModConfig/lotrnpcs-client.json now has `StripTinyGearDetail: true` and `TinyGearSize: 0.5`, and client-debug.log has "[lotrnpcs] gear ...: left out N tiny pieces" lines (once per item).
- [ ] 127.2 Angmar and Haradrim warriors up close look the same with `.npcgeardetail on` and `.npcgeardetail off` (the command rebuilds them at once); your own worn armor never changes.
- [ ] 127.3 `/recruits bench 200` with `.npcgeardetail off`, then `on`, then `1.5`: note the FPS of each, and whether 1.5 visibly loses studs or buckles.


## 128. Archers hold their range when allies call them in (0.0.128)

Install `lotrnpcs_0.0.128-test.zip`; restart fully.

- [ ] 128.1 Spawn 5 Gondor archers and 5 Gondor warriors together, and 5 goblin warriors about 20 blocks away: the archers walk in only until about 16 blocks, then shoot from there while their warriors charge. They don't follow the warriors into the melee unless an enemy comes within about 2 blocks.

## 130. Riders worth their price, solid NPCs (0.0.130)

Install `lotrnpcs_0.0.130-test.zip`; restart fully. Stage each side on one spot, battle log on.

- [ ] 130.1 A Rohan rider (creative tab) has about 76 health (a warrior 34; promotion card or `/entity` info). 3 Rohan riders against 120 coins of enemy warriors of similar strength win about half the time, and riders no longer die about 14 s into a crowd.
- [ ] 130.2 In a 38 v 26 blob no two NPCs stand inside each other: walking in or fighting, their bodies stay about 0.7 blocks apart (look from above), and none are pushed into walls or off ledges.
- [ ] 130.3 Riders still sit on their mounts (rider and mount aren't shoved apart), and the player can still walk among NPCs (the game's push).
- [x] 130.4 `/recruits bench 200`: the server still holds about 29 ticks/s. (2026-11-16 on 0.0.147: `/recruits bench 200`: server avg 29.3 ticks/s, 1% low 27.2, worst tick 39 ms)

## 131. Riders hold still to fight (0.0.131)

Install `lotrnpcs_0.0.131-test.zip`; restart fully. Battle log on, each side on one spot.

- [ ] 131.1 3 Rohan riders v 10 Dunlending warriors (14-20 blocks apart), twice: the horses stop when they reach the enemy and turn to face it instead of walking around; riders swing about every 3 s (about as often as a Dunlending) and win or come close (0.0.130: 0-3 and 4-3).
- [ ] 131.2 The CSV has few `nocontact` melee rows for riders, and the `/recruits battlelog` summary shows "out of reach when the blow fell" only rarely.
  - 2026-11-16 (`214310`, `214600`): rider out of reach 17 of 83 and 25 of 84 (20-30%): misses are targets 13-122 degrees off the rider's facing while the horse moves. Not rare, but riders already win 10-0; left for the owner's rider-strength decision.
- [ ] 131.3 Riders still chase an enemy that backs off (beyond 3.5 blocks they follow the path again).

## 132. Riders finish one enemy at a time (0.0.132)

Install `lotrnpcs_0.0.132-test.zip`; restart fully. Battle log on, each side on one spot.

- [x] 132.1 3 Rohan riders v 10 Dunlending warriors (riders at z 126, Dunlendings at z 142), twice: in the CSV each rider's hits go to one Dunlending until it dies, then the next; more kills than 0.0.131's 1 (the simulator expects about 8 of 10 with focus).
- [ ] 132.2 A rider whose target runs more than 6 blocks off takes a new one; a rider never stands still for long while enemies hit it.

## 133. Riders charge and wheel away (0.0.133)

Install `lotrnpcs_0.0.133-test.zip`; restart fully. Battle log on, each side on one spot.

- [ ] 133.1 3 Rohan riders v 10 Dunlending warriors (riders at z 126, Dunlendings at z 142), twice: the riders gallop in, strike, ride on several blocks, wheel and charge the same enemy again; the `/recruits battlelog` summary shows charge hits for rohan. *2026-11-14 on 0.0.138: failed, riders never swung (0 rider rows in 146 s) and ended 100+ blocks away; fix in 0.0.139, see 139.*
- [ ] 133.2 A charge hit visibly shoves the Dunlending back about a block or two (not flung far).
- [ ] 133.3 No rider stays stuck: one boxed in by enemies stands and fights, then charges again when there's room. Riders stay within about 20 blocks of the fight.
- [ ] 133.4 A hired rider on Guard stays inside its guard area while charging; one on Follow doesn't ride more than about 12 blocks from you.

## 134: smaller mod file (packaging only)
- [x] 134.1 Every race still shows its skins (dwarves, elves, hobbits, Men of each faction, orcs): the 25 duplicate skin pictures now point at one shared copy. *2026-11-14: Haradrim, Black Númenórean, Easterling, dwarf, elf and tormented elf recruits, Gondor, Mordor and Dunlendings all showed their skins.*
- [x] 134.2 No new "texture not found" or JSON parse lines for lotrnpcs in client-main.log / server-main.log. (2026-11-16 on 0.0.147, owner's PC: no lotrnpcs error/exception/failed lines and no [lotrnpcs] warnings in client-main.log or server-main.log after a fresh start; the server without LRC still to check before release)

## 135. Equal stats, 3 followers, longer NPC fights (0.0.135)

Install `lotrnpcs_0.0.135-test.zip`; restart fully. Battle log on, each side on one spot.

- [x] 135.1 `/recruits faction list`: every faction shows follow 3, roster 16, health 22/34/48/35/43/27, damage 3.15/3.95/7/3.05/3.16/4.94; ModConfig/lotrnpcs-factions.json is Version 31 with NpcVsNpcDamageMultiplier 0.5. (2026-11-16 on 0.0.147: every faction follow 3, roster 16, health 22/34/48/35/43/27, damage 3.15/3.95/7/3.05/3.16/4.94; the file is now Version 32 (0.0.138) with NpcVsNpcDamageMultiplier 0.5)
- [ ] 135.2 9 Gondor warriors v 9 Mordor warriors, twice: about even (each side wins some), and the fight lasts roughly twice as long as a 0.0.134 fight of the same size (battle log damage per NPC hit about half the warrior's 3.95, so about 1.6-2.4). *2026-11-14 on 0.0.138: not even, Gondor won 3 of 3 (9-2, 9-7, 9-3); 1.96-1.97 per hit; 71-114 s from first blow (pre-0.0.135 about 25 s), so 3-4x longer. Same swing rate both sides; orc weapons land later (mean 462 v 367 ms). Owner deciding.*
- [ ] 135.3 A warrior hitting you does its full damage (not halved), and your hits on NPCs are unchanged.
- [ ] 135.4 The hire menu shows 3 followers for every faction, and a third follower of any faction fills the band.
- [ ] 135.5 Wild zone groups of orcs or goblins are no bigger than men's.

## 136. Retreat and Charge orders (0.0.136)

Install `lotrnpcs_0.0.136-test.zip`; restart fully. Hire 3 followers (any factions), spawn 6 enemy warriors about 20 blocks off.

- [ ] 136.1 Hold R, Tab to Combat: "5. Retreat" and "6. Charge (what you look at)" show under the stances, and the keys 5 and 6 work.
- [ ] 136.2 Charge while looking at one enemy: the followers break formation and run at it, then fight the others nearby; the chat line says how many charge. Looking at empty ground near enemies, or at nothing with enemies within 24 blocks, still finds one; with no enemy around you get the "no enemy there" line. Defensive followers charge too; passive ones don't.
- [ ] 136.3 The charge ends by itself: 5 s after the last enemy near them dies (or after 30 s) they come back to their follow places.
- [ ] 136.4 Retreat in the middle of the fight: they stop swinging, come back to you at a run (a rider gallops) and don't start new fights for 10 s even when hit; then they fight again as usual.
- [ ] 136.5 A hired rider on Charge gallops at the target and lands charge hits.

## 137. Hits in the back, facing the enemy, stepping back (0.0.137)

Install `lotrnpcs_0.0.137-test.zip`; restart fully. Battle log on.

- [ ] 137.1 Hit a standing NPC from behind and from the front with the same weapon: the hit from behind does 1.5x (watch its health or the battle log); `/recruits battlelog` shows "hits in the back" in an NPC fight. *2026-11-14: "hits in the back" shows (22-35 per side per 9 v 9, about 20-25% of hits); the player hit test not done yet.*
- [ ] 137.2 In a 9 v 9 warrior fight, NPCs keep their faces toward the nearest enemy; when one backs off (spacing, hiding) it steps back facing the enemy with the walk-back animation, at a walk, not moonwalking in place or sliding.
- [ ] 137.3 On Retreat, followers turn and run (no stepping back); riders never step back.
- [x] 137.4 lotrnpcs-factions.json has BackHitMultiplier 1.5 and BackArcDegrees 120, and #Instructions explain them. (2026-11-16: BackHitMultiplier 1.5, BackArcDegrees 120, one #Instructions entry explains both)

## 138. One captain, recruit-only places (0.0.138; 1 recruit place since the owner's change)

Install `lotrnpcs_0.0.138-test.zip`; restart fully.

- [ ] 138.1 With one captain hired, hiring or promoting a second captain is refused ("captains full"); riders don't count.
- [ ] 138.2 With 3 warriors following, 1 recruit can still follow (Follow me in the orders menu or the talk menu), a 2nd recruit or a 4th warrior can't; 4 recruits can all follow. The roster header shows "Following: 3 of 3 ranked places, 1 of 1 recruit places". (Owner 2026-11-14: "maybe just 1 recruit place"; RecruitExtraFollowers 1.)
- [ ] 138.3 Promote a following recruit while 3 ranked followers follow: it is promoted, stays on the roster, guards where it stands, and the chat says why; dismissing a warrior then lets you call it from the roster.
- [x] 138.4 lotrnpcs-factions.json is version 32 with MaxCaptains 1 and RecruitExtraFollowers 1, no RosterPerCaptain, and #Instructions explain both. (2026-11-16: Version 32, MaxCaptains 1, RecruitExtraFollowers 1, no RosterPerCaptain key (only named in #Instructions as replaced), both explained)

## 139. Riders strike while charging (0.0.139)

Install `lotrnpcs_0.0.139-test.zip`; restart fully. DebugCommands on, battle log on.

- [x] 139.1 3 Rohan riders v 10 Dunlending warriors (riders at z 126, Dunlendings at z 142), twice: the CSV has rider melee rows (landed, some nocontact), the summary shows rohan charge hits, and riders kill some Dunlendings. *2026-11-15 on 0.0.142 (0.0.139-0.0.141 still failed): 47/72 and 61/82 rider rows landed, 14 and 19 charge hits, 6 and 9 kills, 0 rider deaths.*
- [x] 139.2 server-main.log has "[lotrnpcs] rider ... melee:" lines: most swings end in "blow", few "ended without a blow" or "cancelled before the blow". *0.0.142: 77 and 88 swings, every one reached its blow (52+25 and 66+22 out of reach), none ended without one.*
- [x] 139.3 Riders stay with the fight (2026-11-16: reworded from "within about 20 blocks of where it started"; the fight itself drifts). *0.0.142: farthest 56 and 53 blocks from the centre, while still fighting (chasing Dunlendings), not loose; 141 kept the horses from running off.*

## 140. One blow time for every melee weapon (0.0.140)

Install `lotrnpcs_0.0.140-test.zip`; restart fully. Battle log on.

- [x] 140.1 9 Gondor warriors v 9 Mordor orc warriors, three times (as on 2026-11-14: Gondor at z 128 / Mordor at z 142, swapped, side by side): about even (Mordor wins at least one), not 3 of 3 to Gondor. *2026-11-15: Mordor 9-5, Gondor 9-4, Gondor 9-3 (was 3 of 3 to Gondor); Mordor still took more back hits (26-35 v 20-38).*
- [ ] 140.2 Axes and hammers swing visibly faster than on 0.0.139 and spears and knives a little slower, but the blade still meets the target when the hit lands (no hit before the blow).

## 141. Mounts stop when their rider stops steering (0.0.141)

Install `lotrnpcs_0.0.141-test.zip`; restart fully. DebugCommands on.

- [x] 141.1 After the 139 rider tests, `/entity cmd e[type=lotrnpcs:test-rohan-rider,range=60] kill` finds all the riders still alive (none ran off beyond 60 blocks); no riderless-looking horse gallops on in a straight line. *2026-11-15: every rider found within 40-60 blocks after each run.*
- [x] 141.2 Fly about 60 blocks away from a rider fight (past Synergy's 48-block freeze) for 20 s and come back: the riders' horses stand where they were frozen instead of having galloped off. (2026-11-16 on 0.0.147 with Synergy on: 3 riders v 10 Dunlendings, player teleported 84 blocks off for 22 s and back: all 3 riders within 60 blocks, 2 within 40 (the fight moved, no runaway horses))

## 142. Mounted riders' blows fall on time (0.0.142)

Install `lotrnpcs_0.0.142-test.zip`; restart fully. DebugCommands on, battle log on.

- [x] 142.1 3 Rohan riders v 10 Dunlending warriors, twice: server-main.log "rider N melee:" lines mostly "blow" (not "ended without a blow"); the battle log shows rider melee rows several times the 4 of 2026-11-15 and some charge hits; riders kill Dunlendings. *2026-11-15: see 139.1/139.2.*
- [x] 142.2 A rider swinging while standing next to its foe stays put until the blow (no ride-out mid-swing). *2026-11-15: 16 and 29 swings begun standing reached their blow still standing.*

## 143. Foot soldiers hold swings at galloping riders; MountSpike removed (0.0.143)

Install `lotrnpcs_0.0.143-test.zip`; restart fully. DebugCommands on, battle log on.

- [x] 143.1 3 Rohan riders v 10 Dunlending warriors, twice: the Dunlendings' "out of reach when the blow fell" is well under the 85 of 108 and 100 of 140 of 2026-11-15, without their landed blows dropping much (they still strike riders who stand or charge at them).
  - 2026-11-16 (`211710`, `212122`, both stalled by Synergy after 1-2 min): out of reach 28 of 43 and 36 of 65 (55-65%, was 71-79%), landed about as often as on 0.0.142. Partly met; superseded by 146.1 (pass).
- [x] 143.2 `/recruits mountspike` no longer exists. (2026-11-16: the subcommand list no longer has it.)

## 144. Foot soldiers don't swing at riders who'll be gone when the blow falls (0.0.144)

Install `lotrnpcs_0.0.144-test.zip`; restart fully. DebugCommands on, battle log on. Turn Synergy's `EntityActivationRangeEnabled` off for this test (rider fights drift past 48 blocks) and back on afterwards.

- [x] 144.1 3 Rohan riders v 10 Dunlending warriors, twice, 150 s each: the Dunlendings' "out of reach when the blow fell" is under a third of their melee swings (0.0.142: 71-79%, 0.0.143: 55-65%), and they still land blows on riders.
  - 2026-11-16 (`212951`, Synergy off, 208 s, fight ran to the end): Dunlendings 41/88 landed, 47 out of reach (53%), 0 kills, 10 deaths; rohan 70/106 (36 out of reach), 383 damage, 10 kills, 0 deaths, 17 charge hits. Superseded by 146.1 (pass): the prediction barely changed it, so the cause isn't (only) horse speed. 0.0.145 logs why each blow missed (145.1).

## 145. Out-of-reach blows say why (0.0.145, debug)

- [x] 145.1 Same rider test with the battle log on: every `nocontact` row ends with `ms= swing= d=dist/minDist v=gap/minVerDist spd= yaw= mounted=`. Read the rows to find the cause.
  - [x] 2026-11-16 (`213618`, 171 s, ran to the end): every Dunlending miss (22 of 45) was at a horse at 8.5-8.9 blocks a second with the soldier facing it (yaw 0), 0-4.9 blocks past reach (most under 1.5); blow 417-1300 ms after the decision. Rohan misses (26 of 86): yaw 13-122 degrees off, 0-2.9 past reach, the mount at 0-4 blocks a second. Cause for foot soldiers: the rider rides through the swing. 0.0.146.

## 146. A rider who rides through a foot soldier's swing is struck (0.0.146)

- [x] 146.1 Rider test (3 Rohan riders v 10 Dunlendings), twice: Dunlendings' out of reach well under 47 of 88 / 22 of 45, more landed blows on riders; riders still win clearly (owner: riders stay as they are).
  - 2026-11-16 (Synergy off, both ran to the end): `214310` 147 s: Dunlendings 49/66 landed (17 out of reach, 26%), 96 damage, 10 deaths; rohan 65/83, 375 damage, 10 kills, 0 deaths, 20 charge hits. `214600` 158 s: Dunlendings 46/56 (10 out of reach, 18%), 93 damage, 10 deaths; rohan 59/84, 328 damage, 10 kills, 0 deaths, 18 charge hits. Out of reach down from 53-79% to 18-26%; Dunlending damage about doubled; riders still win 3 v 10 with no losses. Pass.

## 147. Riders wander on a leash (0.0.147)

Install `lotrnpcs_0.0.147-test.zip`; restart fully.

- [x] 147.1 Spawn 5 Rohan riders with no enemies around: after a minute and after two, all 5 are within about 20 blocks of where they spawned (`/entity list e[type=lotrnpcs:test-rohan-rider,range=20,alive=true]` from the spawn point). (2026-11-16: 5 of 5 within 20 blocks after ~65 s, all 5 within 12 after ~2 min)

## 148. NPC riders' mounts play their walk and gallop (0.0.148)

Install `lotrnpcs_0.0.148-test.zip`; restart fully.

- [x] 148.1 Owner, 2026-11-16: "The animals ride around without animations." Spawn 3 Rohan riders near goblins or Dunlendings: the horses play their walk and gallop animations while they move and stand idle when they stop (no sliding).
- [ ] 148.2 Ride a horse yourself: walking, galloping and stopping still work with your keys as before.

## 149. Riders 1.5x a warrior, for about 50/50 at equal coin (0.0.149)

Install `lotrnpcs_0.0.149-test.zip`; restart fully. Synergy's activation range off for the test, back on after.

- [ ] 149.1 Owner, 2026-11-16: "isnt the warriors supposed to be 50/50 with the riders?" A Rohan rider has 51 health and hits for about 5.9 (warrior 34 and 3.95). 3 Rohan riders v 13 Dunlending warriors (equal coin), a few runs: each side wins about half (on 0.0.147, 2.24x: riders 13-0 with no losses).
  - 2026-11-16 on 0.0.149, owner's target changed to "3 riders should beat 5-7 warriors": 3 v 6 Dunlendings 6-0 (155 s, 0 rider deaths, riders 221 damage, Dunlendings 63); 3 v 8 8-0 (290 s, 0 rider deaths, 268 v 68 damage, 26 charge hits). Still too strong: superseded by 150.

## 150. Riders at a warrior's numbers: 3 riders beat 5-7 warriors (0.0.150)

Install `lotrnpcs_0.0.150-test.zip`; restart fully. Synergy's activation range off for the test, back on after.

- [ ] 150.1 Owner, 2026-11-16: "3 riders should beat 5-7 warriors". A Rohan rider has a warrior's 34 health and 3.95 damage (x2 on charge hits). 3 Rohan riders beat 6 Dunlending warriors with losses, and 3 v 8 or 9 is about even.

## 151. Every faction's units against each other, in game (owner, 2026-11-16)

Owner, 2026-11-16: "On the checklist I want all the factions units to be tested against each other." "In game". Since 0.0.135 every faction has the same numbers per rank, so what still differs is weapons, reach, body size and armor tiers. Two parts: every faction pair with a rank pairing (136 fights), and the riders' role.

How: Synergy's activation range off (back on after), battle log on, a flat open area within 40 blocks of the player. Spawn each side in batches of 5 about 16 blocks apart, e.g. `/entity spawnat lotrnpcs:test-gondor-warrior 5 x 4 z 2`. Read `/recruits battlelog` at the end, then kill both types with explicit types and a range. Save the CSVs to docs/handover/battle-logs/ and note the score next to the item.

### 151A. Faction v faction, rank v rank: equal coin, 3 runs

Owner, 2026-11-16: "Actually I guess it doesnt really matter if they have equal stats", "But do it anyway", "It'll be testing the different ranks against each other". Every faction pair once, each with a different rank pairing, so all 28 rank pairings (recruit, warrior, guard, shock, captain, archer, rider) come up about 5 times across all factions. Riders only on factions that have them.

Each side spends about 60 coins at its live hire prices (`/recruits faction list`; guard and shock at their promotion price), at least 1 NPC (e.g. 1 captain at 51 v 5 warriors at 12). Archers start 16 blocks off. Pass: neither side wins all 3, or the winner never keeps more than a third of its NPCs standing; or, where a rank is meant to win (captain v warriors, riders v archers), it wins. Note the score and the CSV next to the item. Run several at once only when each fight is 30+ blocks from the next.

- [ ] 151.1 gondor recruit v rohan recruit
- [ ] 151.2 gondor recruit v arnor warrior
- [ ] 151.3 gondor recruit v dale guard
- [ ] 151.4 gondor recruit v elf shock
- [ ] 151.5 gondor recruit v dwarf captain
- [ ] 151.6 gondor recruit v mordororc archer
- [ ] 151.7 gondor recruit v angmarorc rider
- [ ] 151.8 gondor warrior v gundabadorc warrior
- [ ] 151.9 gondor warrior v goblin guard
- [ ] 151.10 gondor warrior v uruk shock
- [ ] 151.11 gondor warrior v dunlending captain
- [ ] 151.12 gondor warrior v haradrim archer
- [ ] 151.13 gondor rider v easterling warrior
- [ ] 151.14 gondor guard v blacknumenorean guard
- [ ] 151.15 gondor guard v tormentedelf shock
- [ ] 151.16 gondor guard v wickeddwarf captain
- [ ] 151.17 rohan guard v arnor archer
- [ ] 151.18 rohan guard v dale rider
- [ ] 151.19 rohan shock v elf shock
- [ ] 151.20 rohan shock v dwarf captain
- [ ] 151.21 rohan shock v mordororc archer
- [ ] 151.22 rohan shock v angmarorc rider
- [ ] 151.23 rohan captain v gundabadorc captain
- [ ] 151.24 rohan captain v goblin archer
- [ ] 151.25 rohan captain v uruk rider
- [ ] 151.26 rohan archer v dunlending archer
- [ ] 151.27 rohan rider v haradrim archer
- [ ] 151.28 rohan recruit v easterling recruit
- [ ] 151.29 rohan recruit v blacknumenorean warrior
- [ ] 151.30 rohan recruit v tormentedelf guard
- [ ] 151.31 rohan recruit v wickeddwarf shock
- [ ] 151.32 arnor recruit v dale captain
- [ ] 151.33 arnor recruit v elf archer
- [ ] 151.34 arnor recruit v dwarf rider
- [ ] 151.35 arnor warrior v mordororc warrior
- [ ] 151.36 arnor warrior v angmarorc guard
- [ ] 151.37 arnor warrior v gundabadorc shock
- [ ] 151.38 arnor warrior v goblin captain
- [ ] 151.39 arnor warrior v uruk archer
- [ ] 151.40 arnor rider v dunlending warrior
- [ ] 151.41 arnor guard v haradrim guard
- [ ] 151.42 arnor guard v easterling shock
- [ ] 151.43 arnor guard v blacknumenorean captain
- [ ] 151.44 arnor guard v tormentedelf archer
- [ ] 151.45 arnor rider v wickeddwarf guard
- [ ] 151.46 dale shock v elf shock
- [ ] 151.47 dale shock v dwarf captain
- [ ] 151.48 dale shock v mordororc archer
- [ ] 151.49 dale shock v angmarorc rider
- [ ] 151.50 dale captain v gundabadorc captain
- [ ] 151.51 dale captain v goblin archer
- [ ] 151.52 dale captain v uruk rider
- [ ] 151.53 dale archer v dunlending archer
- [ ] 151.54 dale rider v haradrim archer
- [ ] 151.55 dale recruit v easterling recruit
- [ ] 151.56 dale recruit v blacknumenorean warrior
- [ ] 151.57 dale recruit v tormentedelf guard
- [ ] 151.58 dale recruit v wickeddwarf shock
- [ ] 151.59 elf recruit v dwarf captain
- [ ] 151.60 elf recruit v mordororc archer
- [ ] 151.61 elf recruit v angmarorc rider
- [ ] 151.62 elf warrior v gundabadorc warrior
- [ ] 151.63 elf warrior v goblin guard
- [ ] 151.64 elf warrior v uruk shock
- [ ] 151.65 elf warrior v dunlending captain
- [ ] 151.66 elf warrior v haradrim archer
- [ ] 151.67 elf rider v easterling warrior
- [ ] 151.68 elf guard v blacknumenorean guard
- [ ] 151.69 elf guard v tormentedelf shock
- [ ] 151.70 elf guard v wickeddwarf captain
- [ ] 151.71 dwarf guard v mordororc archer
- [ ] 151.72 dwarf guard v angmarorc rider
- [ ] 151.73 dwarf shock v gundabadorc shock
- [ ] 151.74 dwarf shock v goblin captain
- [ ] 151.75 dwarf shock v uruk archer
- [ ] 151.76 dwarf rider v dunlending shock
- [ ] 151.77 dwarf captain v haradrim captain
- [ ] 151.78 dwarf captain v easterling archer
- [ ] 151.79 dwarf rider v blacknumenorean captain
- [ ] 151.80 dwarf archer v tormentedelf archer
- [ ] 151.81 dwarf rider v wickeddwarf archer
- [ ] 151.82 mordororc rider v angmarorc rider
- [ ] 151.83 mordororc recruit v gundabadorc recruit
- [ ] 151.84 mordororc recruit v goblin warrior
- [ ] 151.85 mordororc recruit v uruk guard
- [ ] 151.86 mordororc recruit v dunlending shock
- [ ] 151.87 mordororc recruit v haradrim captain
- [ ] 151.88 mordororc recruit v easterling archer
- [ ] 151.89 mordororc rider v blacknumenorean recruit
- [ ] 151.90 mordororc warrior v tormentedelf warrior
- [ ] 151.91 mordororc warrior v wickeddwarf guard
- [ ] 151.92 angmarorc warrior v gundabadorc shock
- [ ] 151.93 angmarorc warrior v goblin captain
- [ ] 151.94 angmarorc warrior v uruk archer
- [ ] 151.95 angmarorc rider v dunlending warrior
- [ ] 151.96 angmarorc guard v haradrim guard
- [ ] 151.97 angmarorc guard v easterling shock
- [ ] 151.98 angmarorc guard v blacknumenorean captain
- [ ] 151.99 angmarorc guard v tormentedelf archer
- [ ] 151.100 angmarorc rider v wickeddwarf guard
- [ ] 151.101 gundabadorc shock v goblin shock
- [ ] 151.102 gundabadorc shock v uruk captain
- [ ] 151.103 gundabadorc shock v dunlending archer
- [ ] 151.104 gundabadorc rider v haradrim shock
- [ ] 151.105 gundabadorc captain v easterling captain
- [ ] 151.106 gundabadorc captain v blacknumenorean archer
- [ ] 151.107 gundabadorc rider v tormentedelf captain
- [ ] 151.108 gundabadorc archer v wickeddwarf archer
- [ ] 151.109 goblin archer v uruk rider
- [ ] 151.110 goblin recruit v dunlending recruit
- [ ] 151.111 goblin recruit v haradrim warrior
- [ ] 151.112 goblin recruit v easterling guard
- [ ] 151.113 goblin recruit v blacknumenorean shock
- [ ] 151.114 goblin recruit v tormentedelf captain
- [ ] 151.115 goblin recruit v wickeddwarf archer
- [ ] 151.116 uruk rider v dunlending recruit
- [ ] 151.117 uruk warrior v haradrim warrior
- [ ] 151.118 uruk warrior v easterling guard
- [ ] 151.119 uruk warrior v blacknumenorean shock
- [ ] 151.120 uruk warrior v tormentedelf captain
- [ ] 151.121 uruk warrior v wickeddwarf archer
- [ ] 151.122 dunlending guard v haradrim guard
- [ ] 151.123 dunlending guard v easterling shock
- [ ] 151.124 dunlending guard v blacknumenorean captain
- [ ] 151.125 dunlending guard v tormentedelf archer
- [ ] 151.126 dunlending shock v wickeddwarf shock
- [ ] 151.127 haradrim shock v easterling captain
- [ ] 151.128 haradrim shock v blacknumenorean archer
- [ ] 151.129 haradrim captain v tormentedelf captain
- [ ] 151.130 haradrim captain v wickeddwarf archer
- [ ] 151.131 easterling archer v blacknumenorean archer
- [ ] 151.132 easterling recruit v tormentedelf recruit
- [ ] 151.133 easterling recruit v wickeddwarf warrior
- [ ] 151.134 blacknumenorean recruit v tormentedelf guard
- [ ] 151.135 blacknumenorean recruit v wickeddwarf shock
- [ ] 151.136 tormentedelf recruit v wickeddwarf captain

### 151C. Riders' role (owner, 2026-11-16: riders should "beat 2 warriors, less than one captain, and be a harassing unit for archers")

- [x] 151.C1 1 Rohan rider v 2 Dunlending warriors: the rider wins.
  - 2026-11-16 on 0.0.150: rider won in 276 s with 20 of 34 health left (73 v 14 damage; warriors chased 77% of the time).
- [ ] 151.C2 1 Rohan rider v 1 Dunlending captain: the captain wins.
  - 2026-11-16 on 0.0.150: FAILED, the rider was winning when stopped at 218 s (52 v 17 damage, captain landed 5 of 6 swings but swung only 6 times). By stats a captain (48/7) is weaker than 2 warriors (34/3.95 each), so no rider strength gives 'beats 2 warriors, loses to a captain'; owner decision asked.
- [ ] 151.C3 3 Rohan riders v 5 Dunlending warriors + 3 archers behind them: the riders ride past the warriors to the archers and kill them first (0.0.151).

## 153. Stronger captains (0.0.153)

Owner, 2026-11-16, chose "Stronger captains overall" (so a captain beats a rider that beats 2 warriors). Captains have 96 health and 10.5 damage (were 48 and 7); config version 33 moves a captain still at 48 or 7. The captain x2.5 on riders from 0.0.152 is gone; riders keep x2 on archers.

- [x] 153.1 1 Rohan rider v 1 Dunlending captain: the captain wins.
  - 2026-11-16 on 0.0.153: captain dealt 32 (rider left on 2 of 34, then stood off), rider dealt 46 of the captain's 96, by 172 s (log 104729).
- [ ] 153.2 1 Dunlending captain v 2 Dunlending warriors: the captain wins.
- [ ] 153.3 0.0.155 (owner: "keep this within reason for players to be able to kill in equivalent armor and weapons", "Aka for captains, iron and steel"): captains are back to 48 health and 7 damage against players; config version 34 moves 96/10.5 back. A player in iron or steel armor with an iron or steel weapon can kill a captain one on one. Against NPCs the captain still hits x1.5 and takes x0.5 (redo 153.1 and 153.2 on 0.0.155).
  - 2026-11-16 on 0.0.155, 1 rider v 1 captain: captain dealt 37 (rider 34 health) and took 19 of 48 by 153 s, captain wins (log 105929). Player side not tested yet.

## 154. Guard and shock recruits (0.0.154)

Owner, 2026-11-16: "Do the same for recruit", chose "Pick it when hiring", then "They should also still be able to be changed after hire". Guard recruit: 28 health, 2.4 damage, one-handed weapon and always a shield. Shock recruit: 18 health, 3.75 damage, two-handed weapon. Same price, XP and promotions as a recruit; wild recruits stay plain.

- [x] 154.1 (passed on 0.0.155, owner 2026-11-16: "It works"; replaced by 156.1) Talking to a wild recruit of your faction offers "hire you" plus "hire you as a guard recruit" and "as a shock recruit", all at the recruit's price. Hiring as one closes the menu and the chat says the recruit became that class.
- [x] 154.2 (passed on 0.0.155, owner 2026-11-16; menu reworked in 156.2) Your own recruit's talk menu offers "Train as a ..." for the two other classes; picking one swaps it for free and keeps its XP, name, looks and orders.
- [ ] 154.3 Guard and shock recruits count as recruits for the 2 recruit-only follower places, and promote to warrior, guard, shock or archer at 15 XP.
- [ ] 154.4 The creative tab lists recruit, guard recruit, shock recruit before warrior in every faction.

## 156. One hire option, class change behind its own menu (0.0.156)

Owner, 2026-11-16, after testing 154.1-154.2: "I think though it should just be, let me hire you, then in the menu after they are hired it should say let me change your class".

- [ ] 156.1 A wild recruit of your faction offers only "I'd like to hire you" (no guard/shock hire lines); hired, it is a plain recruit.
- [ ] 156.2 Your own recruit's menu has one line "Let me change your class."; it asks "What should I train as?" and lists "Train as a ..." for the two other classes plus "Never mind, stay as you are." Picking a class swaps it for free (XP, name, looks, orders kept) and closes the menu; "Never mind" goes back to the normal menu.
- [ ] 156.3 Warriors, guards, shock troops, captains and other ranks show no class line.
- [ ] 156.4 Misty Mountains have a rider (Misty Mountains orc rider on a Feverstone dire wolf): in the creative tab after the captain, hireable/promotable like the other riders, and spawning wild in zones.
- [x] 156.5 (0.0.157; owner 2026-11-16: "the caribou size is good now"; elf seen seated by computer use) Elf riders ride a male caribou (game:tameddeer-caribou-male-adult, added by tools/patches/caribou-mount.json), about a horse's size (drawn 1.45x, hitbox 1.8 x 1.65), with antlers nearly all year; the elf sits in the saddle on its back (not floating or sunk), and the caribou walks, trots and runs with its rider. No patch errors for caribou-mount.json or tameddeer in server-main.log.
- [ ] 156.6 (0.0.158, 2026-11-17, battle log 181603: 3 Haradrim riders v 5 Arnor warriors, a wild elf rider joined: after ~140 s riders 0 losses, 2 kills, warriors' melee 27/41 landed (14 out of reach), riders 41/59; riders still win, as the owner's rider role allows (beat 2 warriors each)) Rider reach is half a block from the saddle again (was a block): recruits and warriors on foot get swings off at a charging rider before it hits them, instead of being knocked back first every time. Compare 3 riders v 5 warriors with 0.0.152's 5-0.
- [x] 156.7 (2026-11-17, 0.0.158: Haradrim, Dunlending, Easterling, Black Numenorean, wicked dwarf and tormented elf riders all spawn mounted on Feverstone horses with their names) Every armed faction has a rider: Dunlendings, Haradrim, Easterlings, Black Numenoreans, wicked dwarves and tormented elves on Feverstone horses (owner: "horses are the default unless otherwise stated"); the Shire has none (no warriors).
- [ ] 156.8 (0.0.158, 2026-11-17: much better, the seven stars arc, crown and trunk read as the tree, but the trunk shows as two thin lines with black between; screenshot /mnt/project-files/lotrnpcs/0.0.158/kite-shield-ingame.jpg; owner to judge) The Arnor kite shield shows the white tree and seven gold stars cleanly (its down faces' UVs are flipped top to bottom at build time; if it's still scrambled, try rotate 180 instead).
- [ ] 160.1 (0.0.160) The Arnor kite shield shows the white tree as one solid trunk under a full crown with the seven gold stars above it (down faces now turned 180 degrees; 0.0.158's v-only flip drew the trunk as two lines; owner said 2026-11-17 "still messed up").
- [ ] 159.1 Dunlending captains always show their Warlord helmet (none bareheaded).
