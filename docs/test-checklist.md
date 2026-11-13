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
- [ ] 102.2 No more than 4 NPCs attack one NPC at once; the rest find another enemy or wait.
- [ ] 104.1 A marching column of your recruits doesn't walk through each other, and NPCs walking past a standing group steer round it.
- [ ] 104.2 Two groups of wild NPCs meeting form lines and fight one-on-one along the front; the side with more then wraps round the enemy.
- [ ] 104.3 Hiring or promoting past the captain limit (Men 2, orcs and goblins 3, elves and dwarves 1, Uruks and Dunlendings 2) is refused with a message.
- [ ] 105.1 An archer on a wall more than 2 blocks above its target keeps shooting instead of jumping down into melee.
- [ ] 106.1 In a follow formation, captains stand front-middle, then warriors, then recruits; archers at the back or the ends.
- [ ] 107.1 Mordor/Angmar/Gundabad orcs follow up to 6 at a time.
- [ ] 108.1 Melee NPCs swing about every 2.6 to 3 s. Fighting a group of 3 or more: say if it now feels too fast or too slow.
- [ ] 108.2 Hire prices match the new table (for example Mordor 5/12/29/12, Elves 6/19/50/20); promotion costs the new rank's price minus 3 (captain minus 4).
- [ ] 109.1 The big one: 100 Mordor orcs against 50 Haradrim (a shared zone or creative spawns), battle log on. Send the totals. Then set `NpcHitsIgnoreHurtWindow` to true in lotrnpcs-factions.json, `/recruits faction reload`, and repeat with the log on again.
- [ ] 109.2 During that fight, note the server's lag (any "Server overloaded" lines in server-main.log) and whether many NPCs stand around not fighting; the log's "idle while fighting" counts those.

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

