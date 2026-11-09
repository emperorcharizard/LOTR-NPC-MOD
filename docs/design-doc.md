# LOTR NPC mod: v1 design

Oct 30, 2026 · @Emperorcharizar

## Summary

One content mod for a Lord of the Rings Vintage Story server. It adds faction NPCs of the Middle-earth races that players fight in the wild or hire to follow them and guard their base. Every decision below was agreed in the design interview on 2026-10-30. Test builds have proven the NPC looks, rank stats, faction armor and zone spawning. Faction fights are built but not yet seen in game. Hiring part 1 (talk menu, hiring, Follow me, Hold your position, Dismiss) is built in 0.0.37, not yet tested in game.

**The three goals**

1. Hired NPCs live at and defend the player's base.
2. Players command their hirelings in battle against other players and their NPCs.
3. Wild NPCs dot the map as enemies, more believable than drifters or skeletons.

**Out of scope for v1:** creatures (wargs, spiders, trolls), structures or worldgen, equippable gear, patrol routes and formations, wages, mercenary factions, changes to any other mod's spawning.

## Factions and hostility

A player's allegiance comes from their race and from their membership in vanilla `/group` groups that admins create and own. Every NPC faction is hostile to every player by default. For each faction, the server config lists the player races and the groups it treats as friendly, and either one is enough (decided 2026-10-30). Races are Player Model Lib race codes such as lrc:orc or faraces:dwarf, so each server lists the race mods it actually runs. Orc players join through groups only, so no faction lists the orc races (decided 2026-11-02). Admins choose which factions are allies and which are enemies. Factions without an entry in the config are enemies, so every faction fights every other, orc factions included, until admins ally them (decided 2026-11-02). Allied factions' NPCs, wild or hired, never fight each other. A player is safe only from the factions that list their race or group, or the faction an admin set for them with /lotrfaction set, which replaces their race while their groups still count (built in 0.0.35). Admins can extend that to an ally by switching on ShareFriends for that alliance in the config, and then both factions spare each other's friends (decided 2026-11-02).

- **Admins own the faction groups.** Players can't switch sides on their own. Admins can also set a player's faction directly with /lotrfaction set and undo it with /lotrfaction clear (0.0.35).
- **A player can be a friend of several factions.** For example, a player in both Gondor's and Mordor's groups is a friend of both, so neither side's NPCs attack them. This replaces the earlier rule that conflicting membership made a player unaligned.
- **Each faction has a stance** toward non-friendly players:

| Stance | Behaviour toward non-friendly players | Example races |
| --- | --- | --- |
| Aggressive | Attacks on sight | Every faction except the Shire (decided 2026-11-02) |
| Defensive | Only fights players who attack it or its allies | The Shire |
| Passive | Runs away | None in v1 |

- **NPC factions fight each other** according to a faction-versus-faction relation table in the config. Fights only happen within 128 blocks of an online player, because vanilla stops running AI beyond that range.
- **Every faction fights drifters, undead and other mods' monsters.** The mod doesn't change vanilla spawning or any other mod's spawning.

### v1 factions

Seventeen factions since 0.0.35 (user, 2026-11-02). Men are Gondor, Rohan, Dale, Arnor, Haradrim, Dunlendings, Black Númenóreans and Easterlings. Hobbits are one faction whatever the sub-race. Longbeards and Firebeards are the Dwarves, and wicked dwarves are their own faction. Noldor, Sindar and Avari are one Elf faction for now, since there is only one set of elf armor, and tormented elves are their own faction. The orc factions are Mordor, Angmar, Misty Mountains and Isengard, and orc players join by region through groups. Gondor, Rohan, Dale and Arnor share one Men of the West race, so they list no race either, and their players join through groups or /lotrfaction set. The blocs only describe the lore. They don't make factions allies, so every faction fights every other unless the config allies them (decided 2026-11-02). The default config ships one alliance, Mordor with Isengard, as the example entry the user asked for (0.0.34).

| Faction | Race | Bloc | Default stance |
| --- | --- | --- | --- |
| Gondor | Men | Free peoples | Aggressive (changed 2026-11-02) |
| Rohan | Men | Free peoples | Aggressive (changed 2026-11-02) |
| Dwarves | Dwarves | Free peoples | Aggressive (changed 2026-11-02) |
| The Shire | Hobbits | Free peoples | Defensive (changed from passive) |
| Mordor orcs | Orcs | Shadow | Aggressive |
| Angmar orcs | Orcs | Shadow (decided 2026-11-02) | Aggressive |
| Goblins | Goblins | Shadow | Aggressive |
| Easterlings | Men | Shadow | Aggressive |
| Isengard | Uruk-hai | Shadow | Aggressive |
| Dunlendings | Men | Independent | Aggressive (changed 2026-11-02) |
| Dale | Men | Free peoples | Aggressive (added 0.0.35) |
| Arnor | Men | Free peoples | Aggressive (added 0.0.35) |
| Haradrim | Men | Shadow | Aggressive (added 0.0.35) |
| Black Númenóreans | Men | Shadow | Aggressive (added 0.0.35) |
| Wicked dwarves | Dwarves | Not set | Aggressive (added 0.0.35) |
| Elves | Elves (Noldor, Sindar, Avari) | Free peoples | Aggressive (added 0.0.35) |
| Tormented elves | Elves | Not set | Aggressive (added 0.0.35) |

## Races, ranks and looks

All NPCs use race models from LOTR Races and Classes (LRC 0.4.1), drawn through Player Model Lib, so an NPC looks like a player of that race. The models are bundled inside this mod with the LRC authors' permission and credit, so neither LRC nor Fourth Age Races is needed. They're hidden from character creation, so players can't pick them. Gear is cosmetic only. It's fixed by race and rank, and players can't change it.

| Race | LRC model | Notes |
| --- | --- | --- |
| Men | Men of the West for Gondor, Rohan, Dale and Arnor (told apart by hair colour), Haradrim, Black Númenórean, wildmen for Dunlendings, Easterling for Easterlings | All built by 0.0.35 |
| Dwarves | Longbeard | Its own dwarf body on the player skeleton |
| Hobbits | Harfoot only (one hobbit type, decided 2026-10-30) | 0.7 scale |
| Goblins | LRC's small orc body at 0.77 scale, with goblin ears and faces and pale grey-green skins | Its own race. Shares a body shape with Mordor orcs |
| Mordor orcs | LRC's small orc body at 0.9 scale, with tusked orc faces, orc ears and dark grey skins | Its own race, separate from Uruk-hai. The tusked faces ship unused in LRC and are being tested in build 0.0.4 |
| Uruk-hai | LRC's Uruk-hai body at full size, with the darkest skins and long or dreadlocked hair | Isengard faction |
| Wicked dwarves | LRC wicked dwarf | Own faction, dwarf beards like the Longbeards, grey and black hair (0.0.35) |
| Elves | Sindar body for Noldor, Sindar and Avari | One faction for now (0.0.35) |
| Tormented elves | LRC tormented elf | Own faction (0.0.35) |

**Ranks.** Every fighting faction has three ranks, recruit, warrior and captain, each its own spawn. Hobbits have one rank: farmers who fight with a hoe, scythe or shovel. Since 0.0.36 it counts as the Shire's recruit rank, with recruit health and damage, so Shire zones spawn groups of hobbits (user, 2026-11-02). Health follows drifters and there is no rank armor. Numbers below were tuned in game under Combat Overhaul (test builds 0.0.14 to 0.0.21) against iron faction armor.

| Rank | Health | Melee (slashing) | Per hit through iron faction armor | Per hit, unarmored |
| --- | --- | --- | --- | --- |
| Recruit | 28 | 3 at tier 8 | 0.75 | 3 |
| Warrior | 40 | 4 at tier 9 | 2 | 4 |
| Captain | 54 | 7 at tier 9 | 3.5 | 7 |

Health is 22, 34 and 48 since 0.0.43. **Since 0.0.90 these are the Men's** (owner: factions "unequally equal", even per full warband). In 0.0.90 each faction's NPCs had them times its health and damage multipliers; **since 0.0.91 each faction has its own health and damage per rank** (fitted so each faction's same-rank band is even on average against every other's) and every hit varies by 20% (`DamageSpread`). Follow limits and rosters differ: Uruk-hai x1.05/x1.03 (5 follow, 15 roster), Dunlendings x0.85/x0.85 (6, 19), Mordor, Angmar and Gundabad orcs x0.66/x0.85 (7, 22), Misty Mountains goblins x0.57/x0.73 (8, 26), elves x1.14/x1.35 (4, 13), dwarves x1.33/x1.15 (4, 13). Mixed bands use shares. Full table and rules: config guide, "Faction balance".

- **Hits:** always on the chest (no head shots), one swing every 2.85 to 3.25 seconds (0.0.45, user 2026-11-03: attack 0.25 s more often; cooldown 1950 to 2350 ms, was 2200 to 2600).
- **Mordor orc weapons (0.0.45):** Mordor orcs only (user, 2026-11-03) carry one of the user's five test models, a scimitar, axe, hammer, dagger or poleaxe, picked at random per orc and kept for life. Looks only: the client draws it in the hand and the server never sees it, so damage, tier and timing stay the rank's own. Each swings its own vanilla animation (scimitar slash, axe chop, hammer blow, dagger stab, poleaxe thrust) timed to land with the damage, and the poleaxe is held upright in both hands between attacks. Since 0.0.46 the models use the user's own textures (only the dagger's cord, which the set lacks, keeps a vanilla stand-in), and the dagger is rolled over in the hand after the user saw it upside down in game. The user's 2026-11-03 screenshots show the weapons in hand; the other grips and the swings are still to be checked.
- **Movement:** chase speed 0.03, faster than a walking player but slower than a sprinting one. They spot players from 24 blocks (about 14 when sneaking) and look every second.
- **Orc armor:** each orc faction wears its own To Rule Them All armor, read from the installed armor mods by item code and never bundled: Mordor (Orc Raider, Orc Warrior), Angmar (Raider, Soldier), Misty Mountains (Moria and Gundabad Orc), Isengard (Uruk-hai Scout, Infantry, General helmet). Without the armor mod they wear a vanilla outfit. Armor is looks only and never drops.
- **Captain armor:** captains wear the heavier set where a faction has one (user, 2026-11-02): Angmar the Wight Guard, Misty Mountains the Gundabad Warlord. Mordor captains wear the red Orc Warrior armor with the red-masked Servant of the Eye helmet.
- **Other factions' armor (0.0.35):** medium set for recruits, heavy for warriors, heaviest for captains, from the installed To Rule Them All mods. Gondor (Man-at-Arms, Infantry, Fountain Guard with Infantry leggings), Rohan (Man-at-Arms, Eorling, Huscarl), Dale (Lake Guard, Soldier, Captain), Arnor (Infantry, Soldier, Lord), Dunland from the Isengard mod (Raider, Warrior, Warlord), Black Númenóreans from the Mordor mod (Black Númenórean, Servant of the Eye, Lord), Dwarves (Militia, Warrior, Lord), wicked dwarves (Footman, Warrior, Lord) and Elves from the Woodland Realm mod (Sentinel, Warrior, Gate Warden). Haradrim, Easterlings and tormented elves have no armor mod of their own, so since 0.0.38 they borrow the closest sets (user, 2026-11-03): Haradrim wear the Mordor mod's brown Black Númenórean and red Servant of the Eye armor, the captain with the red mask; Easterlings wear Angmar's Raider, Soldier and heavier armor; tormented elves wear the Angmar ranger set, the black masked Servant of the Eye, and the Black Númenórean Lord. Non-orc ranks copy the orc rank stats for now.

## Wild NPCs and spawn zones

Admins mark spawn zones on the map. A zone is a rectangle between two corners covering every height, and it lists one or more factions. Zones may overlap (decided 2026-11-02). Recruits and warriors spawn naturally inside their faction's zones, rarer than animals, and captains are very rare (decided 2026-11-02). Zones replace the earlier camp anchors: nothing is ever cleared, and a zone refills over time. The mod ships no structures, and worldgen is ruled out because the map is fully generated and hand painted.

- **Spawn rules (test build 0.0.30):** our own zone timer, not vanilla's spawn chance, because the user wants at least one group every 3 to 5 minutes near a player in a zone (decided 2026-11-02). When a player's timer is due, one group of a faction the zone lists spawns on the ground surface 40 to 80 blocks away, inside the zone. Groups are 1 to 5 orcs, about 3 recruits per warrior, and about 1 group in 8 is instead a captain with 1 to 3 escorts. Nothing spawns on water, on tree tops or where block light is above 7, so not next to torches. Spawning pauses while 8 zone NPCs are within 128 blocks, and at most 1 captain is nearby. Zone NPCs that no player comes within 128 blocks of for 5 minutes despawn. All numbers live in the server's ModConfig/lotrnpcs-spawning.json.
- **Zone check:** our code vetoes any spawn of our NPCs outside a zone of their faction, through vanilla's spawn event. Other mods' spawning is untouched.
- **Marking zones (built in 0.0.29):** admins with the controlserver privilege use `/lotrzone add x1 z1 x2 z2 faction [more factions]`, plus `list`, `here`, `remove <zone>`, `addfaction <zone> <faction>` and `removefaction <zone> <faction>`, plus spawnnow to spawn a group near the admin for testing. Coordinates are the ones shown on screen, and `~` means relative to the admin. Zones are saved with the world. Since 0.0.36 every faction can have zones, the Shire included. Shire groups are all hobbit farmers.
- **Creative menu:** every race and rank gets a spawn item in the creative inventory, like vanilla creatures. An NPC spawned this way is a wild NPC of its faction, regardless of zones.

## Hiring and hirelings

A player can hire up to 5 NPCs (the cap is a config setting) from any faction they are a member of. The hirelings at your base and the ones you take into battle are the same NPCs.

- **Cost:** a one-time, expensive fee paid in Coinage coins, priced by rank. Since 0.0.35 the prices sit in the faction config: 5, 15 and 40 coins for recruit, warrior and captain in every faction for now, with a coin type per faction (default coinage:coin-silver-lg). Coinage coins don't carry the value tag vanilla trading uses, so the mod patches in a value for each metal and coin size.
- **Who can hire:** any member of the faction, by race, by group or set by an admin with /lotrfaction set (agreed 2026-11-02). There's no mercenary faction in v1.
- **Interaction:** right-clicking an NPC opens a talk menu like the traders' (user, 2026-11-02). Members get a greeting, Hire with its price, Ask a question (a random question and answer for the faction) and Goodbye. Your own hireling offers Follow me, Hold your position, Guard this area (0.0.44), Ask a question and Dismiss. NPCs that don't attack you but won't serve you brush you off, and NPCs hostile to you don't talk. Orders for all your hirelings at once come from a small menu shown while you hold R (rebindable in Settings > Controls), modeled on the Minecraft Recruits mod.
- **Side:** a hireling fights for its owner's side, and wild NPCs treat it the way they treat its owner. It stays loyal if the owner later changes faction group.
- **Friends** never targeted: the owner, the owner's faction group, and the owner's whitelist.
- **Death is permanent,** and the fee is lost. Out of combat, hirelings slowly regenerate health.

| Order | Where the hireling stays | Which players it attacks |
| --- | --- | --- |
| Follow me | Near the owner | Only those who attack the owner or the hirelings |
| Guard here | Wanders within a set radius of the spot where the order was given | Any non-friendly player inside the radius, even while the owner is offline |

Under both orders, hirelings also fight NPCs that are hostile to the owner's side.

- **Whitelist:** managed with `/hirelings whitelist add/remove/list <player>`. A menu can come later.
- **Guard radius:** 16 blocks by default, set in the config.
- **Owner logs off:** hirelings stay where they are. Escorts are still only defensive, so if they get attacked while the owner is away, they fight back and can die.
- **Owner too far away:** an escort more than about 64 blocks from its owner is teleported to them, which also happens after the owner logs back in. The check runs on the owner's side, so it still works when the escort's chunk is about to unload.

**Built in 0.0.37 (part 1, not yet tested in game):** the talk menu with 2 greetings, a hired line and 3 questions per faction; hiring that takes the faction's coins from the hotbar and backpack (rankless NPCs cost a recruit's price, 0 makes it free), with MaxHirelings in lotrnpcs-factions.json; Follow me (walks, runs when far behind, teleports to the owner past 48 blocks while its chunk is loaded); Hold your position (the spot where the order was given, without the guard radius yet); Dismiss with a confirm, coins not refunded. Hirelings never wander or zone-despawn, take no damage from their owner or fellow hirelings, and the owner is told in chat when one dies. They attack on sight whatever would attack their owner on sight, monsters, and anything chasing or hitting the owner or a hireling, and they fight anyone who hurt the owner or a hireling for GrudgeSeconds. Wild NPCs treat them as they treat the owner, using the owner's last seen race and groups while the owner is offline.

**Built in 0.0.38 (orders menu, not yet tested in game):** holding R shows a compact menu with a movement page and a combat page (user, 2026-11-03), switched with the scroll wheel or Tab; number keys pick, and letting go of R closes it. It commands every hireling of yours within 96 blocks. Movement: Follow me, Hold position, and Move there (the block under the cursor, up to 96 blocks away), with formations loose, line, column, wedge, square and circle. Following hirelings keep their formation behind you, and a circle surrounds you. Movement orders make hirelings break off a fight for 5 seconds. Combat stances: Passive never fights; Defensive only defends you and your hirelings; Aggressive attacks all enemies (0.0.37's behavior, no players unless they start it); Raid attacks every player and creature not allied to you, sparing group mates, allied factions, their hirelings and animals tamed by allies. Followers run to catch up past 6 blocks. Spawn items show a head-and-shoulders portrait in the rank's armor, and names no longer say Test. Left out for now: groups, wander, back to position, forget targets. Still later: whitelist, guard radius, regeneration.

**Built in 0.0.44 (Guard here and the whitelist, not yet tested in game):** Guard here is the fourth movement order in the hold-R menu (key 4, so the formations moved to 5 to 9 and 0), and "Guard this area" in your hireling's talk menu does the same for one hireling. The post is where you stand. Guards walk to a random spot in the area every 6 to 16 seconds and head back whenever a fight takes them outside it. The area reaches GuardRadius blocks from the post (16 by default, 4 to 64, in lotrnpcs-factions.json), as far up and down as across. On the aggressive stance they attack, inside the area only, whatever aggressive hirelings fight anyway, plus every player there who isn't you, a /group mate, a member of a faction allied to yours or on your whitelist, and those players' hirelings. Creative and spectator players are left alone. Raiding guards attack every creature in the area that isn't allied, defensive ones only fight back, and passive ones never fight. Fighting back works anywhere, but a guard never chases anything more than 8 blocks past its area. While you're offline they keep guarding and judge players by your last known race, groups, faction and whitelist. The whitelist is /hirelings whitelist add, remove or list, for any player, found by name even when they're offline. It's one-way and covers only your own hirelings, which still fight back if a whitelisted player hurts you or them. Also in 0.0.44: targeting checks are about 4 times faster than in 0.0.43 and no longer allocate memory, spawn item portraits are built a few at a time once you're in creative mode, healing a hireling no longer counts as an attack, Follow me clears the old post, damaged zone and hireling saves load safely, and the hint on your hireling shows the orders key as you bound it.

## Technical approach

The mod is JSON content plus one small C# DLL. That's the same split Hirelings uses, which is the cleanest example among the uploaded mods. The DLL is needed because vanilla AI picks targets by entity code, and every player shares the code `player`, so it can't tell a Gondor player from a Mordor player.

| Component | Built as | Based on |
| --- | --- | --- |
| NPC types, one per race and rank | JSON entities with outfit variants | Vanilla humanoid NPC classes, plus LRC race models bundled in the mod |
| Creative-menu spawn items | Our own creature spawn item | The way Fantasy Creatures adds its spawn items |
| Faction config and lookup | C# mod system plus a server config file (built in 0.0.31 to 0.0.35: ModConfig/lotrnpcs-factions.json, and /lotrfaction check, list, reload, set, clear and members for admins; both config files start with #Instructions) | Vanilla player groups: `IPlayer.GetGroups()`, `IGroupManager.GetPlayerGroupByName` |
| Faction-aware targeting | C# AI tasks that subclass vanilla's classic seek and melee tasks, which already carry the tuned chase and swing values (built in test build 0.0.31) | Vanilla `AiTaskSeekEntity` and `AiTaskMeleeAttack` |
| Follow me and Guard here | Two short C# AI tasks | Hirelings' follow task, built on vanilla `AiTaskStayCloseToEntityR` |
| Hireling state (owner, order, guard spot, whitelist, cap) | C# entity behavior | Hirelings' core behavior |
| Hiring dialogue | JSON dialogue plus C# dialogue triggers | Hirelings' dialogue-as-commands pattern |
| Order menu on right-click | C# interaction and GUI | Pet AI's command menu |
| Spawn zones and admin commands | JSON runtime spawn conditions, a C# zone store saved with the world, a spawn veto and chat commands | Vanilla `OnTrySpawnEntity` event, as vanilla temporal stability uses it to block spawns |
| Out-of-combat regeneration | C# behavior, unless vanilla health settings already cover it | Still to check |
| Coinage prices | JSON patch giving coins a value | Coinage item types |

**Dependencies:** Player Model Lib (required) and Coinage (required for hiring). LRC's race models are bundled with its authors' permission, so neither LRC nor Fourth Age Races is needed. The mod has to run on two servers, and only one of them has LRC.

**Checked in the vanilla 1.22.7 source:** NPCs stop running their AI beyond 128 blocks from every online player. NPC melee damage is a fixed number in the AI config, which is how ranks will set damage. AI tasks have to be registered on the server side.

## Risks and open questions

Proven in game on 2026-10-30: NPCs show real race models through Player Model Lib, with our DLL choosing the race. Build 0.0.3 moved the models into the mod itself, and every race rendered from those copies in game.

**Risks, highest impact first**

1. **Skins on NPCs (checked against Player Model Lib 1.24.0).** The library's wearables renderer refuses to attach to anything except a player, and its skin system is player-centric. So NPCs can't inherit Fourth Age Races looks through it. Decompiled code (ingestion report, section 9) shows the library's skin behavior probably does work on NPCs, and it randomizes a skin on spawn. The race is chosen by a watched attribute called skinModel, and there's no JSON key for it, so our DLL sets it when the NPC spawns. That's the planned route. The fallback, with no library involved, is to point the NPC's JSON straight at Fourth Age Races' body shapes and layer its skins with vanilla texture and shape overlays. Tested in game on 2026-10-30: Player Model Lib does render a randomized skinned body on an NPC, and the vanilla-skinning route rendered invisible, so it's dropped. The first test named the race without its mod prefix ("orc" instead of "lrc:orc"), so it most likely showed the library's fallback model. Build 0.0.2 used the full codes and showed the real LRC and Fourth Age orcs. What's left is the randomizer, which picks unsuitable parts such as leotards, long hair and light Uruk-hai skin. The next build will preset the parts per faction and rank. Dwarf armor won't be swapped automatically for NPCs either, so dwarf outfits have to reference the dwarf armor models directly.
2. **Combat Overhaul is on the server.** Player Model Lib requires Combat Overhaul's library, and a Combat Overhaul fork has been uploaded. It changes how players deal and take damage, so rank stats need balancing against it, and its effect on NPC melee is still unknown.
3. **Guards as griefing tools.** "Guard here" attacks any non-friendly player in range, even while the owner is offline. Someone could post guards on a road. Decided 2026-10-30: left open for v1. The guard radius is a config value, so admins can tighten it later without a code change.
4. **Hirelings left behind.** If the owner teleports or rides off, a hireling can end up in a chunk that unloads with it still inside. This is handled by teleporting escorts that fall more than about 64 blocks behind, but it needs testing.
5. **Version coupling.** The bundled LRC models are frozen copies, so LRC updates can't break them, but a Player Model Lib update can. Pin its version per server release.

**Open questions**

1. Hire prices are 5, 15 and 40 coins for now (0.0.35). The non-orc factions copy the orc rank stats for now and are still to tune.
2.

Everything else from the interview was decided on 2026-10-30: the factions, the cultures of Men (Gondor, Rohan, Easterlings, Dunlendings), the whitelist, the guard radius, what happens on logoff, and no loot.

**Needed from you:** the server's full mod list. Player Model Lib and the Combat Overhaul fork are now uploaded.
