"""Generates the test NPC mod assets into the folder given as argv[1].

Run bundle_lrc.py into the same folder for the race models these NPCs use.
"""
import json
import os
import sys

out = sys.argv[1]
A = os.path.join(out, "assets", "lotrnpcs")


def write(rel, data):
    path = os.path.join(A, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        json.dump(data, f, indent=2)
        f.write("\n")


ANIMS = [
    {"code": "walk", "animation": "walk", "blendMode": "Average", "mulWithWalkSpeed": True, "animationSpeed": 1.1,
     "triggeredBy": {"onControls": ["Move"], "matchExact": True}},
    {"code": "idle", "animation": "idle1", "blendMode": "Average",
     "triggeredBy": {"onControls": ["Idle"], "matchExact": True, "defaultAnim": True}},
    {"code": "sprint", "animation": "sprint", "animationSpeed": 0.6, "mulWithWalkSpeed": True, "blendMode": "Average",
     "triggeredBy": {"onControls": ["Move", "SprintMode"], "matchExact": True}},
    {"code": "swim", "animation": "swim", "blendMode": "Average",
     "triggeredBy": {"onControls": ["Move", "Swim"], "matchExact": True}},
    {"code": "swimidle", "animation": "swimidle", "blendMode": "Average",
     "triggeredBy": {"onControls": ["Idle", "Swim"], "matchExact": True}},
    {"code": "land", "animation": "land", "blendMode": "Average", "triggeredBy": {"onControls": ["Fall"]}},
    {"code": "die", "animation": "die", "blendMode": "Average", "animationSpeed": 1.5,
     "triggeredBy": {"onControls": ["Dead"]}},
    {"code": "jump", "animation": "newjump", "blendMode": "Average",
     "triggeredBy": {"onControls": ["Idle", "Jump"], "matchExact": True}},
    {"code": "hurt", "animation": "hurt", "blendMode": "Average"},
    # 0.0.137: stepping back while facing an enemy (Factions.cs Face), weighted over the controls' walk.
    {"code": "lotrnpcs-walkback", "animation": "walkback", "blendMode": "Average", "weight": 10, "animationSpeed": 1.1},
    {"code": "hit", "animation": "hit", "blendMode": "Add",
     "elementWeight": {"UpperArmR": 4, "LowerArmR": 4, "UpperArmL": 4, "LowerArmL": 4},
     "elementBlendMode": {"UpperArmR": "AddAverage", "LowerArmR": "AddAverage", "UpperArmL": "AddAverage", "LowerArmL": "AddAverage"}},
]

# When a melee hit lands, in ms after the attack starts. The unarmed punch and the archers' shove strike at this moment.
DAMAGE_AT_MS = 400
# 0.0.99 (owner, 2026-11-08: "Review the animations and try to improve them"; chose "Swing sync + weapon wind-ups"):
# until 0.0.98 every weapon struck at DAMAGE_AT_MS, so the game's swings were sped up or slowed down to fit (axehit
# 1.9x, hammerhit 1.4x, knifestab and spearhit 0.8x). Now each swing strikes when it naturally would, close to its own
# speed: light thrusts first, heavy chops last. Only the first blow of a fight comes later; the time between blows
# (attack plus cooldown) is unchanged, so damage per second stays the same. A swing not listed keeps DAMAGE_AT_MS.
HIT_MS = {"knifestab": 350, "spearhit": 350, "falx": 420, "poleaxechop": 500, "hammerhit": 550, "axehit": 600}


def hit_ms(w):
    return HIT_MS.get(w["attack"], DAMAGE_AT_MS)

AI_TASKS = [
    # Our melee task: always hits a player's chest under Combat Overhaul (user: no head shots).
    # 0.0.45 (user, 2026-11-03: "increase how often they attack by .25 s"): the cooldown after each swing is 250 ms
    # shorter, 1950 to 2350 ms (was 2200 to 2600), so one swing every 2.85 to 3.25 s (was 3.1 to 3.5 s). 0.0.108 (owner,
    # 2026-11-09: "increase attack frequency up a tick or two"): 0.25 s shorter again, 1700 to 2100 ms, one swing every 2.6 to 3.0 s.
    {"code": "lotrnpcs:meleeattack", "entityCodes": ["player"], "priority": 3, "slot": 1, "damage": 3, "damageTier": 1,
     "mincooldown": 1700, "maxcooldown": 2100, "attackDurationMs": 900, "damagePlayerAtMs": DAMAGE_AT_MS,
     "animation": "hit", "animationSpeed": 1},
    # Our seek task: vanilla seek, but targets are chosen by faction (Factions.cs): non-friendly players for
    # aggressive factions, enemy factions' NPCs, monsters, and anyone who attacked the faction or its allies.
    # 0.0.94 (owner: "for units that are being damaged but cannot reach their attacker, have them flee and hide behind
    # cover"; recruits too): above seek, shooting and following (Hiding.cs).
    {"code": "lotrnpcs:hide", "priority": 2.8, "movespeed": 0.04, "searchRadius": 16, "stayMs": 10000,
     "animation": "sprint", "animationSpeed": 0.45},
    {"code": "lotrnpcs:seekentity", "entityCodes": ["player"], "priority": 2.5, "movespeed": 0.03, "seekingRange": 24, "searchWaitMs": 1000,
     "animation": "sprint", "animationSpeed": 0.45},
    # Recruits (0.0.37): follow the owner or go back to the spot they hold. Below seek, so fights come first.
    # Recruits never wander (EntityBehaviorHireable blocks it). 0.0.38 (user: "they move at a walking speed"): a
    # little faster than a walking player, and running a little faster than a sprinting one whenever more than 6
    # blocks behind, back to walking within 3.
    {"code": "lotrnpcs:followowner", "priority": 2.0, "walkSpeed": 0.027, "runSpeed": 0.055, "runAnimationSpeed": 0.75,
     "followDistance": 4, "holdDistance": 2},
    # 0.0.112: wild groups from a zone with patrol points walk them (ZonePatrol.cs); above wander, below everything else.
    {"code": "lotrnpcs:zonepatrol", "priority": 1.5, "movespeed": 0.02, "animation": "walk"},
    {"code": "wander", "priority": 1.0, "movespeed": 0.012, "animation": "walk", "wanderChance": 0.02},
    {"code": "lookaround", "priority": 0.5},
]

# Vanilla body as the base shape: Player Model Lib swaps in the race model at render time.
FA_BODY = "game:entity/humanoid/seraph-faceless"
# 0.0.77: NPCs use our own copy of that body (bundled by bundle_lrc.py: the game's body plus our shape animations
# poleaxechop and poleaxehold). The server builds an NPC's animations from its type's base shape, never from the Player
# Model Lib race model, so with the game's body it refused both ("attempted to play an animation code which its shape
# does not have", owner's 0.0.76 server-debug.log) and the poleaxe and battleaxe hung at the side, one-handed.
# Same elements as the game's body; only the animation list differs.
NPC_BODY = "lotrnpcs:entity/humanoid/seraph-faceless"
FA_BASE_TEXTURE = "game:entity/humanoid/seraph-naked-hairless"


# The owner's weapons, shields and bows (models, grips, holds, swings) and the shape writer live in
# tools/weapon_catalog.py, shared with the lotrweapons player mod. gen runs with python -I, so tools/ is put on the path.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import weapon_catalog as CATALOG  # noqa: E402
from weapon_catalog import (WEAPON_SCALE, SPEAR_HOLD, WEAPONS, MM_WEAPONS, ELF_WEAPONS, DWARF_WEAPONS, MELEE,
                            hand_transform, own_weapon_textures, SHIELDS, BOW, BOW_DRAWN, DRAW_MS_FOR_POSES,
                            BOW_PULLS, BOW_PULL_ITEMS, MM_BOW, MM_BOW_DRAWN, MM_BOW_PULLS, MM_BOW_PULL_ITEMS,
                            pull_string)  # noqa: E402
# The game's iron falx (0.0.76, user: "the other factions should just have an iron falx placeholder until I can get the
# actual model"), held with the game's own hand transform and swung like the scimitar. Code from the game's item list
# (wiki image Blade-falx-iron); a wrong code only leaves the hand empty, with a warning in the client log.
FALX = {"falx": {"item": "game:blade-falx-iron", "attack": "falx", "impact": 12.5}}
# 0.0.101 (owner, 2026-11-08: "with the removal of not only spears expansion, what base game equivalents can be given";
# then "build it that way"): the game's own weapons as stand-ins until the owner's models arrive, held with the game's
# hand transforms. Codes read from the server's Combat Overhaul fork 0.18.9 (assets/game/itemtypes/combatoverhaul),
# which redefines the game's weapons under the same codes; "ruined" ones are rusty. Spears thrust and are held level
# like the owner's spears, swords slash like the falx, clubs and maces swing like the hammer, axes like the axe. The
# bardiche (a polearm) is swung one-handed like the axe, with no shield; the game's item has no two-handed hold of ours.
_GAME_SPEAR = {"attack": "spearhit", "impact": 10} | SPEAR_HOLD
_GAME_SWORD = {"attack": "falx", "impact": 12.5}
_GAME_CLUB = {"attack": "hammerhit", "impact": 17}
_GAME_AXE = {"attack": "axehit", "impact": 22.5}
GAME_WEAPONS = {
    "steelspear": {"item": "game:spear-generic-steel"} | _GAME_SPEAR,
    "ironspear": {"item": "game:spear-generic-iron"} | _GAME_SPEAR,
    "bbronzespear": {"item": "game:spear-generic-blackbronze"} | _GAME_SPEAR,
    "flintspear": {"item": "game:spear-generic-flint"} | _GAME_SPEAR,
    "silverspear": {"item": "game:spear-generic-ornatesilver"} | _GAME_SPEAR,
    "scrapspear": {"item": "game:spear-scrap-scrap"} | _GAME_SPEAR,
    "steelfalx": {"item": "game:blade-falx-steel"} | _GAME_SWORD,
    "blackguard": {"item": "game:blade-blackguard-iron"} | _GAME_SWORD,
    "forlorn": {"item": "game:blade-forlorn-iron"} | _GAME_SWORD,
    "ruinedsabre": {"item": "game:blade-sabre-ruined"} | _GAME_SWORD,
    "ruinedgladius": {"item": "game:blade-gladius-ruined"} | _GAME_SWORD,
    "woodclub": {"item": "game:club-generic-wood"} | _GAME_CLUB,
    "scrapmace": {"item": "game:club-scrapmace-scrap"} | _GAME_CLUB,
    "ruinedmace": {"item": "game:club-flanged-ruined"} | _GAME_CLUB,
    "ruinedbeardedaxe": {"item": "game:axe-bearded-ruined"} | _GAME_AXE,
    "ruinedbattleaxe": {"item": "game:axe-battle-ruined"} | _GAME_AXE,
    "ruinedbardiche": {"item": "game:axe-bardiche-ruined", "twoHanded": True} | _GAME_AXE,
}
# Every orc faction but Isengard may carry the Mordor dagger and axe (0.0.76, user: "the orc dagger and the orc axe
# should now be eligible accessories for all orc factions except isengard").
ORC_SHARED = {k: WEAPONS[k] for k in ("dagger", "axe")}
FALX_RACES = ["gondor", "rohan", "dale", "arnor", "haradrim", "easterling", "dunlending", "blacknumenorean",
              "tormentedelf", "wickeddwarf", "uruk"]
# Each armed race's weapons (Mordor since 0.0.45, Misty Mountains 0.0.75, everyone but the Shire 0.0.76).
# 0.0.96 (owner, 2026-11-07: "Let the gundabad orcs use the moria spear"): the Moria spear joins Gundabad's orc dagger
# and axe (held as the Misty Mountains orcs hold it; Gundabad carry no shield).
WEAPONS_OF = {"mordororc": WEAPONS, "goblin": {**MM_WEAPONS, **ORC_SHARED}, "angmarorc": ORC_SHARED,
              "gundabadorc": {**ORC_SHARED, "mmspear": MM_WEAPONS["mmspear"]},
              "elf": ELF_WEAPONS, "dwarf": DWARF_WEAPONS} | {r: FALX for r in FALX_RACES}
# 0.0.82 to 0.0.95 used items of the (Not Only) Spear Expansion mod (domain spearexpantion) as stand-ins: pike, halberd,
# war pick, flanged mace, common mace, khopesh, bartka, seax. 0.0.96 (owner, 2026-11-07: "remove the not only spears
# expansion dependancy, as it wont be implemented into the server"): all gone. Each faction now carries only the
# owner's models and the game's own falx; a rank left with nothing falls back to WEAPONS_OF (the falx for Men, Uruks,
# tormented elves). The weapons each faction still needs are in docs/model-checklist.md, with the owner's preferences
# from the 0.0.82 loadout list ("prefer spears" and the like), so they can be weighted again when the models come.
# NPCs already holding a Spear Expansion item pick again when they load (it is no longer in their list).
ALL_WEAPONS = {**MELEE, **FALX, **GAME_WEAPONS}
# Weapons by rank, with weights (0.0.82, owner's list: "Prefer spears" and the like). "Prefer" = weight 3 against 1
# for the faction's other weapons (my default). Races and ranks not listed carry WEAPONS_OF's list, all weight 1.
PREFER = 3
_DWARF_RANKS = {"recruit": {"dwaxe": PREFER, "dwbattleaxe": PREFER, "dwwarhammer": PREFER, "dwshorthammer": PREFER, "dwdagger": 1, "dwspear": 1},
                "warrior": {"dwaxe": PREFER, "dwbattleaxe": PREFER, "dwwarhammer": PREFER, "dwshorthammer": PREFER, "dwdagger": 1, "dwspear": 1},
                # 0.0.112: captains carry the steel hammers instead of the iron ones (owner: steel for captains only).
                "captain": {"dwwarhammersteel": PREFER, "dwshorthammersteel": PREFER, "dwaxe": 1, "dwbattleaxe": 1, "dwdagger": 1, "dwspear": 1}}
# 0.0.101 game stand-ins by faction (owner: "build it that way"), every rank alike; PREFER marks the owner's 0.0.82
# preferences (Gondor, Arnor: spears). Replaced by the owner's models as they come (docs/model-checklist.md).
_RANKS = ("recruit", "warrior", "captain")
GAME_LOADOUTS = {
    # 0.0.112: the owner's models replace Gondor's falx, Rohan's spear and sword, the Black Numenorean spear and the
    # Easterlings' bardiche (NEW_WEAPONS).
    "gondor": {"steelspear": PREFER, "gdsword": 1},
    "arnor": {"steelspear": PREFER, "steelfalx": 1},
    "rohan": {"rhspear": 1, "rhsword": 1, "rhaxe": 1},
    "dale": {"ironspear": 1, "falx": 1},
    "dunlending": {"woodclub": 1, "flintspear": 1, "ruinedbeardedaxe": 1},
    "blacknumenorean": {"blackguard": 1, "ruinedmace": 1, "bnspear": 1},
    # 0.0.113: the owner's scimitar replaces the game's Forlorn blade.
    "easterling": {"bbronzespear": 1, "eastscimitar": 1, "eastaxe": 1},
    "uruk": {"falx": 1, "ironspear": 1, "ruinedbattleaxe": 1},
    "tormentedelf": {"ruinedsabre": 1, "ruinedgladius": 1, "silverspear": 1},
    "angmarorc": {"dagger": 1, "axe": 1, "scrapspear": 1, "scrapmace": 1},
    "gundabadorc": {"dagger": 1, "axe": 1, "mmspear": 1, "scrapmace": 1},
}
WEAPON_PREFS = {
    **{f: {r: dict(w) for r in _RANKS} for f, w in GAME_LOADOUTS.items()},
    # Gondor captains: the owner's captain's sword (0.0.95) and, 0.0.112, the steel sword (steel for captains only).
    # Recruits and warriors: the game's steel spear and the owner's iron sword.
    "gondor": {"recruit": dict(GAME_LOADOUTS["gondor"]), "warrior": dict(GAME_LOADOUTS["gondor"]),
               "captain": {"gdcaptsword": 1, "gdsteelsword": 1}},
    # Black Numenorean captains (0.0.112): the owner's steel sword and Morgul blade, and their spear; the game's
    # Blackguard blade and ruined mace stay with recruits and warriors.
    "blacknumenorean": {"recruit": dict(GAME_LOADOUTS["blacknumenorean"]), "warrior": dict(GAME_LOADOUTS["blacknumenorean"]),
                        "captain": {"bnsteelsword": 1, "morgulblade": 1, "bnspear": 1}},
    # Haradrim (owner, 0.0.86: no falx): the owner's spear (0.0.94), sword and curved sword (0.0.97), evenly.
    "haradrim": {r: {"hdspear": 1, "hdsword": 1, "hdcurvedsword": 1} for r in ("recruit", "warrior", "captain")},
    # Elves: the owner's elven spear and, 0.0.112, longsword (was the game's falx).
    "elf": {r: {"elfspear": 1, "elflongsword": 1} for r in ("recruit", "warrior", "captain")},
    # Dwarves: "Prefer axes and warhammers"; Lords "Prefer warhammers" (0.0.95: the owner's warhammer and shorthammer).
    "dwarf": _DWARF_RANKS,
    # Wicked dwarves: the dwarves' rules and models.
    "wickeddwarf": _DWARF_RANKS,
}


# Races whose melee NPCs may carry a shield, and which. Several: each NPC that rolls a shield picks one at random
# (0.0.76, dwarves). Mordor since 0.0.55, Misty Mountains 0.0.71, Black Numenoreans and dwarves 0.0.76.
# 0.0.101: a full "domain:code" is a game shield (stand-ins: Rohan and Dale the wood-and-metal shield, Dunlendings the
# crude one), drawn as the game draws it.
SHIELD_OF = {"rohan": ["game:shield-woodmetal"], "dale": ["game:shield-woodmetal"], "dunlending": ["game:shield-crude"],
             # 0.0.112 (owner): Black Numenoreans carry their new shield, which Mordor orcs may also carry beside their
             # own ("the black numenorean shield can be used by orcs"; chose Mordor), and the Numenorean kite shield
             # goes to Arnor (owner's choice).
             "mordororc": ["shield", "bnshield"], "goblin": ["mmshield"], "blacknumenorean": ["bnshield"], "arnor": ["kiteshield"],
             "dwarf": ["dwtowershield", "dwshield"], "easterling": ["eastshield"],
             # 0.0.94: Gondor (owner: "gondor only"; both shields at random, "A") and elves (owner: "elves only").
             "gondor": ["gondorshield", "boromirshield"], "elf": ["elfshield"],
             # 0.0.97 (my call, the owner sent the two Haradrim shields without notes): by rank, as their armor goes
             # (infantry, heavy infantry, warlord): the light shield for recruits, the heavy one for warriors and
             # captains. A dict by rank instead of a list.
             "haradrim": {"recruit": ["hdlightshield"], "warrior": ["hdheavyshield"], "captain": ["hdheavyshield"]}}
# 0.0.113 (owner: "Guards should just be shielded units, and shock troops should be the two handed units"; chose game
# stand-ins): a guard of a faction without a shield carries one of these, a shock trooper of a faction without a
# two-handed weapon the game's ruined bardiche. Their other ranks stay as they were. Replaced as the models come.
GUARD_SHIELD_STANDIN = {"uruk": ["game:shield-woodmetal"], "angmarorc": ["game:shield-crude"],
                        "gundabadorc": ["game:shield-crude"], "tormentedelf": ["game:shield-woodmetal"],
                        "wickeddwarf": ["dwtowershield", "dwshield"]}
SHOCK_STANDIN = "ruinedbardiche"
# When the drawn bow shows: the whole draw, the first 200 ms of the 450 ms release, while the arm comes down from the
# aim (after that the rest bow fits the lowered arm), and the shove when an enemy is too close to shoot (0.0.60: an
# upright bow bash, where the rest bow pointed forward like a stick). Milliseconds after the animation starts; 0 = while
# it runs. Weapons.cs looks these codes up as the shape animations the game tracks (bowaim, bowhit, hit).
DRAWN_WHILE = {"lotrnpcs-bow-draw": 0, "lotrnpcs-bow-release": 200, "lotrnpcs-bow-attack": 0}
BOW_POSES = ([{"item": f"lotrnpcs:orcweapon-{name}", "while": {"lotrnpcs-bow-draw": [round(frame * DRAW_MS_FOR_POSES / 16), 0]}}
              for name, _, frame in reversed(BOW_PULLS)]
             + [{"item": "lotrnpcs:orcweapon-bow-drawn", "while": {a: [0, ms] for a, ms in DRAWN_WHILE.items()}}])


MM_BOW_POSES = ([{"item": f"lotrnpcs:orcweapon-{name}", "while": {"lotrnpcs-bow-draw": [round(frame * DRAW_MS_FOR_POSES / 16), 0]}}
                 for name, _, frame in reversed(MM_BOW_PULLS)]
                + [{"item": "lotrnpcs:orcweapon-mmbow-drawn", "while": {a: [0, ms] for a, ms in DRAWN_WHILE.items()}}])
# Each archer race's bow, by item and poses. 0.0.76 (user: "the default bow should be the vanilla longbow, not the orc
# bow"): every race not listed carries the game's longbow, with the game's own hand transform and no string stages.
BOW_OF = {"mordororc": ("lotrnpcs:orcweapon-bow", BOW_POSES), "goblin": ("lotrnpcs:orcweapon-mmbow", MM_BOW_POSES),
          # 0.0.101 game stand-ins: the recurve bow for elves, Easterlings and Haradrim, the simple bow for Dunlendings.
          "elf": ("game:bow-recurve", []), "easterling": ("game:bow-recurve", []), "haradrim": ("game:bow-recurve", []),
          "dunlending": ("game:bow-simple", [])}
DEFAULT_BOW = ("game:bow-long", [])


# Archer animations: draw over DRAW_MS (vanilla bowaim, 16 frames, held at the end), loose (bowhit), and a shove with
# the bow when cornered (the unarmed hit, timed like the weapons). No pose between shots since 0.0.59: the arms idle.
DRAW_MS = 1100
assert DRAW_MS == DRAW_MS_FOR_POSES, "keep the pulled-string timing in step with the draw"
BOW_ANIMS = [
    {"code": "lotrnpcs-bow-draw", "animation": "bowaim", "animationSpeed": round(16 / 30 / (DRAW_MS / 1000), 3),
     "blendMode": "Average", "weight": 0.01, "elementWeight": None},
    {"code": "lotrnpcs-bow-release", "animation": "bowhit", "animationSpeed": 1, "blendMode": "Average", "weight": 0.01,
     "elementWeight": None},
    {"code": "lotrnpcs-bow-attack", "animation": "hit", "animationSpeed": round(7 / 30 / (400 / 1000), 3),
     "blendMode": "Average", "weight": 0.01, "elementWeight": None},
]
# Factions whose NPCs carry these weapons (user: "to be used by the mordor orcs only").
ARMED_FACTIONS = set(WEAPONS_OF)  # Mordor since 0.0.45, Misty Mountains since 0.0.75
# Factions with an archer unit (0.0.55 Mordor; 0.0.69 every orc faction; 0.0.70 user, 2026-11-04: "every faction
# (except hobbits) gets archers."). Same unit, stats, bow and arrows as Mordor's, in the faction's recruit armor; the
# bow is the one stand-in model for everyone until the owner's own bows arrive.
ARCHER_RACES = ["mordororc", "angmarorc", "goblin", "uruk", "gundabadorc", "gondor", "rohan", "dale", "arnor", "haradrim",
                "dunlending", "blacknumenorean", "easterling", "dwarf", "wickeddwarf", "elf", "tormentedelf"]
# A swing moves the arms, torso and head, but hardly the legs, so an orc that attacks while running keeps running.
UPPER_BODY = ["UpperArmR", "LowerArmR", "ItemAnchor", "UpperArmL", "LowerArmL", "ItemAnchorL", "UpperTorso", "Neck", "Head"]
LEGS = ["UpperFootL", "LowerFootL", "UpperFootR", "LowerFootR"]
ATTACK_WEIGHTS = {e: 10 for e in UPPER_BODY} | {"LowerTorso": 3} | {e: 0.5 for e in LEGS}
HOLD_WEIGHTS = {e: 10 for e in ["UpperArmR", "LowerArmR", "UpperArmL", "LowerArmL"]}
def weapon_anims():
    anims = []
    for a in BOW_ANIMS:
        anims.append(dict(a, elementWeight=HOLD_WEIGHTS if a["elementWeight"] == "hold" else ATTACK_WEIGHTS))
    for name, w in ALL_WEAPONS.items():
        anims.append({"code": f"lotrnpcs-{name}-attack", "animation": w["attack"],
                      "animationSpeed": round(w["impact"] / 30 / (hit_ms(w) / 1000), 3),
                      "blendMode": "Average", "weight": 0.01, "elementWeight": ATTACK_WEIGHTS})
        if w.get("hold"):
            anims.append({"code": f"lotrnpcs-{name}-hold", "animation": w["hold"], "blendMode": "Average", "weight": 0.01,
                          "elementWeight": {e: 10 for e in w["holdElements"]} if w.get("holdElements") else HOLD_WEIGHTS})
    return anims


def weapon_list(weapons=WEAPONS, weights=None):
    """The lotrnpcs:heldweapon behavior's list (weights: name -> weight, 0.0.82; 1 is left out)."""
    return [{"item": w.get("item", f"lotrnpcs:orcweapon-{name}"), "attackAnimation": f"lotrnpcs-{name}-attack"}
            | ({"hitMs": hit_ms(w)} if hit_ms(w) != DAMAGE_AT_MS else {})
            | ({"holdAnimation": f"lotrnpcs-{name}-hold"} if w.get("hold") else {})
            | ({"twoHanded": True} if w.get("twoHanded") else {})
            | ({"weight": weights[name]} if weights and weights.get(name, 1) != 1 else {}) for name, w in weapons.items()]


def held_weapon(faction, rank):
    """The lotrnpcs:heldweapon behavior for an NPC: archers keep their bow; other Mordor orcs pick a weapon and may
    carry the shield. 0.0.113 branches pick from the warrior's list: guards its one-handed weapons and always the
    shield (GUARD_SHIELD_STANDIN when the faction has none), shock troops its two-handed weapons (the ruined bardiche
    when it has none), never with a shield."""
    if rank == "archer":
        bow, poses = BOW_OF.get(faction, DEFAULT_BOW)
        return {"code": "lotrnpcs:heldweapon", "fixed": True,
                "weapons": [{"item": bow, "attackAnimation": "lotrnpcs-bow-attack"} | ({"poses": poses} if poses else {})]}
    base = BASE_RANK.get(rank, rank)
    prefs = WEAPON_PREFS.get(faction, {}).get(base)
    listed = {name: ALL_WEAPONS[name] for name in prefs} if prefs else WEAPONS_OF.get(faction, {})
    shields = SHIELD_OF.get(faction, [])
    shields = shields.get(base, []) if isinstance(shields, dict) else shields
    if rank == "guard":
        listed = {n: w for n, w in listed.items() if not w.get("twoHanded")} or listed
        shields = shields or GUARD_SHIELD_STANDIN.get(faction, [])
    elif rank == "shock":
        listed = {n: w for n, w in listed.items() if w.get("twoHanded")} or {SHOCK_STANDIN: ALL_WEAPONS[SHOCK_STANDIN]}
        shields = []
    if rank == "rider" and any(w.get("attack") == "spearhit" for w in listed.values()):
        # 0.0.125 (owner, 2026-11-13: riders "prefer spears"): each spear weighs PREFER times what it would on foot.
        prefs = {n: (prefs or {}).get(n, 1) * (PREFER if w.get("attack") == "spearhit" else 1) for n, w in listed.items()}
    behavior = {"code": "lotrnpcs:heldweapon", "weapons": weapon_list(listed, prefs)}
    if shields:
        behavior["shields"] = [s if ":" in s else f"lotrnpcs:orcweapon-{s}" for s in shields]
        if rank == "guard":
            behavior["alwaysShield"] = True
    return behavior


def write_weapons():
    own = own_weapon_textures()
    bundled = set()
    items = {**{k: w for k, w in ALL_WEAPONS.items() if "item" not in w}, **SHIELDS, "bow": BOW, "bow-drawn": BOW_DRAWN, **BOW_PULL_ITEMS, "mmbow": MM_BOW, "mmbow-drawn": MM_BOW_DRAWN, **MM_BOW_PULL_ITEMS}
    for name, w in items.items():
        if w.get("shape"):
            # Another pose of an item already written (the drawn bow): only the item, on that item's shape.
            write(f"itemtypes/orcweapon-{name}.json", {
                "code": f"orcweapon-{name}", "class": "Item", "maxstacksize": 1,
                "shape": {"base": f"lotrnpcs:item/orcweapon/{w['shape']}"},
                "attributes": {"handbook": {"exclude": True}},
                "tpHandTransform": w["hand"],
                "guiTransform": {"rotation": {"x": 0, "y": 90, "z": -45}, "origin": {"x": 0.5, "y": 0.1, "z": 0.5}, "scale": 1.2},
            })
            continue
        shape = CATALOG.weapon_shape(name, w, own, bundled, "lotrnpcs", A, "orcweapon")
        if "pull" in w:
            pull_string(shape, w["pull"], w.get("apexX"))
        write(f"shapes/item/orcweapon/{name}.json", shape)
        write(f"itemtypes/orcweapon-{name}.json", {
            "code": f"orcweapon-{name}",
            "class": "Item",
            "maxstacksize": 1,
            "shape": {"base": f"lotrnpcs:item/orcweapon/{name}"},
            # Only ever shown in an NPC's hand: not in the creative inventory, not in the handbook.
            "attributes": {"handbook": {"exclude": True}},
            "tpHandTransform": w["hand"] if "hand" in w else hand_transform(w["grip"], w["rotation"], w.get("scale", WEAPON_SCALE)),
            "guiTransform": {"rotation": {"x": 0, "y": 90, "z": -45}, "origin": {"x": 0.5, "y": 0.1, "z": 0.5}, "scale": 1.2},
        } | ({"tpOffHandTransform": w["hand"]} if name in SHIELDS else {}))
    # The arrow's shaft, head and fletching (write_arrow) are the owner's textures too. Until 0.0.65 the old weapon
    # models also used them, so they came along with those.
    bundled |= {"game:planks/walnut1", "game:mord/rustiron", "game:cloth/black"} & set(own)
    CATALOG.copy_own_textures(bundled, own, A, "orcweapon")
    write_arrow(bundled)
    unused = sorted(set(own) - bundled)
    if unused:
        sys.exit(f"weapon textures no model uses: {unused}")


def write_arrow(bundled):
    """The archers' arrow (0.0.55): a vanilla-style projectile of our own class (Archery.cs), drawn with the same
    bundled textures as the weapons: a walnut shaft, a rusty iron head and black fletching. It flies tip first along
    +X, as the game turns projectiles."""
    tex = {"shaft": "lotrnpcs:item/orcweapon/planks/walnut1", "head": "lotrnpcs:item/orcweapon/mord/rustiron",
           "fletch": "lotrnpcs:item/orcweapon/cloth/black"}
    for path in ("game:planks/walnut1", "game:mord/rustiron", "game:cloth/black"):
        assert path in bundled, path

    def box(name, f, t, texture):
        return {"name": name, "from": f, "to": t,
                "faces": {k: {"texture": "#" + texture, "uv": [0, 0, 2, 2]} for k in ("north", "east", "south", "west", "up", "down")}}
    write("shapes/entity/npcarrow.json", {"textureWidth": 16, "textureHeight": 16, "textures": tex, "elements": [
        box("shaft", [0, 0.85, 7.5], [14, 1.85, 8.5], "shaft"),
        box("head", [14, 0.6, 7.25], [16.5, 2.1, 8.75], "head"),
        box("fletching up", [0.2, 1.85, 7.95], [3.2, 3.0, 8.05], "fletch"),
        box("fletching down", [0.2, -0.3, 7.95], [3.2, 0.85, 8.05], "fletch"),
        box("fletching side", [0.2, 1.3, 6.8], [3.2, 1.4, 9.2], "fletch"),
    ]})
    physics = {"code": "passivephysics", "groundDragFactor": 1, "airDragFactor": 0.25, "gravityFactor": 0.75}
    write("entities/projectile/npcarrow.json", {
        "code": "npcarrow",
        "class": "LotrNpcsArrow",
        "hitboxSize": {"x": 0.125, "y": 0.125},
        "attributes": {"handbook": {"exclude": True}},
        "client": {"renderer": "Shape", "size": 0.75, "shape": {"base": "lotrnpcs:entity/npcarrow"},
                   "behaviors": [physics, {"code": "interpolateposition"}]},
        # Gone 20 seconds after it was shot, wherever it landed.
        "server": {"behaviors": [physics, {"code": "despawn", "minSeconds": 20}]},
        "sounds": {},
    })


ANIMS += weapon_anims()


def nametag(name):
    return {"code": "nametag", "showtagonlywhentargeted": True, "selectFromRandomName": [name]}


# Archers (0.0.55, user, 2026-11-03: "bring up archer units, primarily for mordor, up to the current spec that the npcs
# are at right now"; "The archers are just an additional unit, they don't have ranks"). Every faction but the Shire since 0.0.70. Same
# seek, melee and hiring as everyone; they shoot from up to 24 blocks out and 20 above or below (0.0.74, user,
# 2026-11-04; was 20 out and 12 up or down), one arrow every 3.1 to 3.9 seconds (a
# 1.1 second draw, then 2 to 2.8 seconds), 3 piercing damage at tier 8 (a warrior's sword hits for 4 at tier 9),
# stop 16 blocks from a target they can see instead of walking up to it, and step back from enemies within 2 blocks (0.0.74, user, 2026-11-04: "step back when they are within 2 blocks"; was 4).
ARCHER_TASK = {"code": "lotrnpcs:shootattack", "priority": 2.6, "slot": 0, "range": 24, "verticalRange": 20, "minRange": 2,
               "damage": 3, "damageTier": 8, "damageType": "PiercingAttack", "drawMs": DRAW_MS, "releaseMs": 450,
               "mincooldown": 2000, "maxcooldown": 2800, "arrowSpeed": 28, "gravityFactor": 0.75, "dispersionDeg": 2.5,
               "projectileCode": "lotrnpcs:npcarrow", "shootSound": "game:sounds/bow-release",
               "animation": "lotrnpcs-bow-draw", "releaseAnimation": "lotrnpcs-bow-release"}
ARCHER_STAND_OFF = 16


def entity(code, name, extra_attributes, skin_client, skin_server, health=25, armor_tier=0, melee=(3, 1), archer=False):
    ai_tasks = [dict(t, damage=melee[0], damageTier=melee[1], damageType="SlashingAttack") if t["code"] == "lotrnpcs:meleeattack" else t
                for t in AI_TASKS]
    if archer:
        ai_tasks = [dict(t, standOff=ARCHER_STAND_OFF) if t["code"] == "lotrnpcs:seekentity" else t for t in ai_tasks]
        ai_tasks.insert(1, ARCHER_TASK)
    return {
        "code": code,
        "class": "EntityAgent",
        "tags": ["humanoid", "habitat-land"],
        "canClimb": True,
        "hitboxSize": {"x": 0.6, "y": 1.85},
        "eyeHeight": 1.7,
        "attributes": extra_attributes,
        "behaviorConfigs": {"controlledphysics": {"stepHeight": 1.1}},
        "client": {
            "renderer": "Shape",
            "size": 1.05,
            "pitchStep": False,
            "shape": {"base": NPC_BODY},
            "texture": {"base": FA_BASE_TEXTURE},
            "behaviors": [
                {"code": "repulseagents"},
                nametag(name),
                # Right-click talk menu and recruit info (Hiring.cs).
                {"code": "lotrnpcs:hireable"},
                {"code": "controlledphysics"},
                {"code": "interpolateposition"},
                # 0.0.112: the dead body opens to a knife, like a drifter's (BodyLoot.cs).
                {"code": "harvestable", "duration": 2},
            ] + skin_client,
            "animations": ANIMS,
        },
        "server": {
            "behaviors": [
                {"code": "repulseagents"},
                nametag(name),
                {"code": "controlledphysics"},
            ] + ([{"code": "lotrnpcs:rankarmor", "tier": armor_tier}] if armor_tier else []) + [
                # Talk menu, hiring and recruit upkeep (Hiring.cs). Before health, so it can cancel friendly fire.
                {"code": "lotrnpcs:hireable"},
                {"code": "health", "currenthealth": health, "maxhealth": health},
                {"code": "taskai", "aiCreatureType": "Humanoid", "aitasks": ai_tasks},
                # 0.0.88 (owner: NPCs "will fling themselves off the edge" of deep pits): right after the AI, so it can
                # stop a step the AI just set (EdgeGuard.cs).
                {"code": "lotrnpcs:edgeguard"},
                # Fights back with its allies when hurt (Factions.cs). 0.0.137: after the AI, so its facing (Face) wins
                # over the path follower's turn toward where it walks.
                {"code": "lotrnpcs:factionmember"},
                # 0.0.112 (owner, review Q10): a drifter-style body. The drops list stays empty: lotrnpcs:bodyloot fills it
                # from the config (WildBodyLoot, HiredBodyLoot), so admins can change it without new assets.
                {"code": "harvestable", "duration": 2, "fixedweight": True, "drops": []},
                {"code": "lotrnpcs:bodyloot"},
                {"code": "deaddecay", "hoursToDecay": 3},
                {"code": "floatupwhenstuck", "onlyWhenDead": True},
            ] + skin_server,
        },
        "sounds": {"hurt": "game:player/hurt", "death": "game:player/death", "swim": "game:player/swim"},
    }


# Player Model Lib draws a race model. Model codes are "domain:key"; ours are bundled by bundle_lrc.py.
# Each entry: entity code, display name, model code, skin texture for the creative item icon, starting gear.
SKINS = "lotrnpcs:entity/humanoid/skinparts/"
ORC_SKIN = SKINS + "orc/orcskin8"
# Rank outfits are vanilla armor items, worn through the vanilla gear inventory. Looks only for now:
# armor on NPCs doesn't reduce damage in vanilla, and it isn't dropped on death.
RECRUIT_GEAR = ["game:armor-body-jerkin-leather", "game:armor-legs-jerkin-leather"]
WARRIOR_GEAR = ["game:armor-head-chain-iron", "game:armor-body-chain-iron", "game:armor-legs-chain-iron"]
# Rank strength (user): health scales like drifters (tainted 22, nightmare 40, double-headed 54), no rank armor.
# Melee was tuned in game under Combat Overhaul: slashing, about 0.75, 2 and 3.5 damage per torso hit through
# iron LOTR faction armor (13 slashing resist), and 3, 4 and 7 on an unarmored player.
# 0.0.43 (user, 2026-11-03: "reduce the health of all the npcs by 6"): 22, 34 and 48 (were 28, 40 and 54), and 19
# for the two rankless leftovers (was 25). NPCs already in a world are brought down on load (Factions.cs).
# Each entry is (health, rank armor tier, (melee damage, damage tier)); armor tier 0 leaves out lotrnpcs:rankarmor.
RANK_STATS = {"recruit": (22, 0, (3, 8)), "warrior": (34, 0, (4, 9)), "captain": (48, 0, (7, 9)),
              # 0.0.55 archers (a unit, not a rank): a warrior's health, a recruit's shove when cornered.
              "archer": (34, 0, (3, 8)),
              # 0.0.113 promotion-only warrior branches (Promotion.Branches): guard health x1.25 and damage x0.8, shock
              # the other way round. The faction config's tables replace these (FactionTiers.WithBranches).
              "guard": (43, 0, (3.2, 9)), "shock": (27, 0, (5, 9)),
              # 0.0.117 riders (promoted from the warrior line next to captain): a warrior's numbers; the mount is the gain.
              "rider": (34, 0, (4, 9))}
# Ranks that wear, carry and offer what another rank does (0.0.113: the warrior branches; 0.0.117 riders, for gear only:
# in the code a rider is its own rank, Promotion.StatRank).
BASE_RANK = {"guard": "warrior", "shock": "warrior", "rider": "warrior"}
BRANCH_NAMES = {"guard": "guard", "shock": "shock trooper"}
RANKLESS_STATS = (19, 0, (3, 1))
CAPTAIN_GEAR = ["game:armor-head-antique-blackguard-damaged", "game:armor-body-antique-blackguard-damaged",
                "game:armor-legs-antique-blackguard-damaged"]
# Gundabad's fallback when its armor mod is missing: the rank outfits without helmets (user: "No helmets").
GUNDABAD_FALLBACK = {rank: [c for c in gear if "armor-head-" not in c]
                     for rank, gear in (("recruit", RECRUIT_GEAR), ("warrior", WARRIOR_GEAR), ("captain", CAPTAIN_GEAR),
                                        ("archer", RECRUIT_GEAR))}
# Faction armor (user): each orc faction wears its own "To Rule Them All" armor mod. lotrnpcs:startinggear picks
# one whole set at random and falls back to the vanilla outfit above when the armor mod isn't installed.
def mord(part, tier, construct, material, last="none"):
    return f"mord:armor-{part}-{tier}-{construct}-{material}-{last}"
def angmar(part, tier, construct):
    return f"angmar:armor-{part}-{tier}-{construct}-{construct}-none"
MORD_RAIDER_HEAD = mord("head", "tierthreechain", "servanthelmetorcmedium", "servanthelmetorcmedium")
MORD_RAIDER_LEGS = mord("legs", "tierthreechain", "servantleggingsorcmedium", "servantleggingsorcmedium")
MORD_WARRIOR_HEAD = mord("head", "tierfourplate", "servanthelmetorcheavy", "servanthelmetorcheavy")
# Captain helmet (user): Servant of the Eye Helmet (Red Mask), worn with the red Orc Warrior armor.
MORD_CAPTAIN_HEAD = mord("head", "tierfourplate", "servanthelmethumanheavy", "servanthelmethumanheavy", "maskred")
def mord_warrior(colour, head=MORD_WARRIOR_HEAD):
    return [head, mord("body", "tierfourplate", "servantarmororcheavy", "servantarmororcheavy" + colour),
            mord("legs", "tierfourplate", "servantleggingsorcheavy", "servantleggingsorcheavy" + colour)]
def norc(part, tier, construct):
    return f"norc:armor-{part}-{tier}-{construct}-{construct}-none"
def uruk(part, tier, construct, finish):
    return f"iseng:armor-{part}-{tier}-{construct}-{construct}-{finish}"
GUNDABAD_BODY = norc("body", "tierfourplate", "norcarmorheavy")
GUNDABAD_LEGS = norc("legs", "tierfourplate", "norcleggingsheavy")
URUK_INFANTRY_LEGS = uruk("legs", "tierfourplate", "urukhaileggingsheavy", "clean")
ANGMAR_SOLDIER_BODY = angmar("body", "tierfourplate", "angmararmorheavy")
ANGMAR_SOLDIER_LEGS = angmar("legs", "tierfourplate", "angmarleggingsheavy")
# 0.0.82 loadouts (owner's list, 2026-11-04, prompts/0.0.82-loadouts-combat.md): "All orcs and uruks can be unhelmed
# unless otherwise noted. If there is a color variant, make sure the whole set is the same color." Each set below is
# one whole outfit; a set per helmet a unit may wear.
ANGMAR_RAIDER_HEAD = angmar("head", "tierthreescale", "angmarhelmetmedium")
GUNDABAD_HEAD = norc("head", "tierfourplate", "norchelmetheavy")
GUNDABAD_TUSKED_HEAD = norc("head", "tierfourplate", "norchelmetheavytwo")
def mord_raider(colour, head=MORD_RAIDER_HEAD):
    return [head, mord("body", "tierthreechain", "servantarmororcmedium", "servantarmororcmedium" + colour), MORD_RAIDER_LEGS]
FACTION_SETS = {
    # Mordor (user, 2026-11-02): Orc Raider (black or brown) for recruits, Orc Warrior for warriors, Orc Warrior red
    # with the red-masked Servant of the Eye helmet for captains. 0.0.82 (owner's list): "Mordor Orc: Raider or warrior
    # set. Helmet can also be angmar orc helmet." "Black Uruk: Mordor orc warrior set only." (black or red, legs to
    # match). Captains unchanged.
    "mordororc": {
        "recruit": [mord_raider(c, head) for c in ("black", "brown") for head in (MORD_RAIDER_HEAD, ANGMAR_RAIDER_HEAD)]
                   + [mord_warrior(c, head) for c in ("black", "red") for head in (MORD_WARRIOR_HEAD, ANGMAR_RAIDER_HEAD)],
        "warrior": [mord_warrior("black"), mord_warrior("red")],
        "captain": [mord_warrior("red", MORD_CAPTAIN_HEAD)],
    },
    # Angmar: Raider for recruits, Soldier for warriors, Wight Guard (either helmet) for captains (user). 0.0.82
    # (owner's list): "Angmar Orc: Full set, can wear both mordor orc helmets, gundabad tusked helmet, or angmar orc
    # helmet." "Angmar Soldier: Full set only, either helmet." Every rank stays an orc (owner: "Factions should not mix
    # races").
    "angmarorc": {
        "recruit": [[head, angmar("body", "tierthreechain", "angmararmormedium"), angmar("legs", "tierthreechain", "angmarleggingsmedium")]
                    for head in (ANGMAR_RAIDER_HEAD, MORD_RAIDER_HEAD, MORD_WARRIOR_HEAD, GUNDABAD_TUSKED_HEAD)],
        "warrior": [[angmar("head", "tierfourplate", helmet), ANGMAR_SOLDIER_BODY, ANGMAR_SOLDIER_LEGS]
                    for helmet in ("angmarhelmetheavy", "angmarhelmetheavy2")],
        "captain": [[angmar("head", "tierfiveplate", helmet), angmar("body", "tierfiveplate", "angmararmorheavier"),
                     angmar("legs", "tierfiveplate", "angmarleggingsheavier")]
                    for helmet in ("angmarhelmetheavier", "angmarhelmetheavier2")],
    },
    # Misty Mountains (norc mod): Moria Orc for recruits, Gundabad Orc for warriors, Gundabad Warlord for captains (user).
    # 0.0.82 (owner's list, "Northern Orcs", and "give the new option listed"): "Moria orc: Full moria set, can wear
    # gundabad warrior helmet, orc snaga helmet" (snaga = the Mordor Orc Raider helmet, my guess: no item has that
    # name); "Gundabad orc: Full gundabad plate, can wear either gundabad orc warrior helmet or black uruk helmet"
    # (= Mordor's Orc Warrior helmet; this replaces 0.0.77's Moria helmet); "Gundabad warlord: Full gundabad warlod
    # plate. Can also have Gundabad orc warrior helmet."
    "goblin": {
        "recruit": [[head, norc("body", "tierthreechain", "norcarmormedium"), norc("legs", "tierthreechain", "norcleggingsmedium")]
                    for head in (norc("head", "tierthreechain", "norchelmetmedium"), GUNDABAD_HEAD, MORD_RAIDER_HEAD)],
        # 0.0.115 (owner, 2026-11-12: "the misty mountains orc warriors should also get the moria orc helmet option").
        "warrior": [[head, GUNDABAD_BODY, GUNDABAD_LEGS] for head in (GUNDABAD_HEAD, MORD_WARRIOR_HEAD, norc("head", "tierthreechain", "norchelmetmedium"))],
        "captain": [[head, norc("body", "tierfiveplate", "norcarmorheavier"), norc("legs", "tierfiveplate", "norcleggingsheavier")]
                    for head in (norc("head", "tierfiveplate", "norchelmetheavier"), GUNDABAD_HEAD)],
    },
    # Isengard (iseng mod): Uruk-hai Scout for recruits, Infantry (clean or painted) for warriors,
    # painted Infantry with the General helmet for captains.
    "uruk": {
        "recruit": [[uruk("head", "tierthreechain", "urukhaihelmetscout", "clean"),
                     uruk("body", "tierthreechain", "urukhaiarmormedium", "clean"),
                     uruk("legs", "tierthreechain", "urukhaileggingsmedium", "clean")]],
        # 0.0.82 (owner's list): "Uruk-hai Warrior: Uruk warrior only. Any Uruk helmet except for scout or general."
        # (infantry, berserker, archer; clean or painted like the body; the legs exist only clean). "Uruk-hai Captain:
        # Same as warrior but only General helmets."
        "warrior": [[uruk("head", "tierfourplate", helmet, f), uruk("body", "tierfourplate", "urukhaiarmorheavy", f), URUK_INFANTRY_LEGS]
                    for helmet in ("urukhaihelmetinfantry", "urukhaihelmetberserker", "urukhaihelmetarcher") for f in ("clean", "painted")],
        "captain": [[uruk("head", "tierfourplate", "urukhaihelmetgeneral", f), uruk("body", "tierfourplate", "urukhaiarmorheavy", f), URUK_INFANTRY_LEGS]
                    for f in ("clean", "painted")],
    },
}

# Gundabad (0.0.62, work in progress; user, 2026-11-03: "I added a mod called gundabadarmor_0.1.0. call on it for a
# gundabad faction for a work in progress"): the owner's Gundabad Bone Armor mod (mod id gundabad) has one set, a
# chestplate with gauntlets and leggings with boots, and no helmet. Every rank wears it, bare-headed (user: "No
# helmets"). Without the mod they wear the vanilla rank outfit, minus its helmets (GUNDABAD_FALLBACK).
GUNDABAD_BONE = ["gundabad:armor-body-tierthreebrig-gundabadbonearmor-gundabadbonearmor-none",
                 "gundabad:armor-legs-tierthreebrig-gundabadboneleggings-gundabadboneleggings-none"]
FACTION_SETS["gundabadorc"] = {rank: [GUNDABAD_BONE] for rank in ("recruit", "warrior", "captain")}
# 0.0.76 (user: "remove wip from gundabad. and give the warrior the gundabad helmet / armor from the angmar misty
# mountains mod"): warriors wear the norc mod's Gundabad Orc set, as Misty Mountains warriors do. Recruits and
# captains keep the bone armor.
FACTION_SETS["gundabadorc"]["warrior"] = [[norc("head", "tierfourplate", "norchelmetheavy"), GUNDABAD_BODY, GUNDABAD_LEGS]]
# 0.0.95 (owner, 2026-11-06: "make the gundabad tusked orc helmet an option for the gundabad orcs. and give the gundabad
# captain the norc warlord options" ... "give the gundabad captain this helmet option" (Gundabad_Helmet_Variant_1);
# "make the tusked helmet, warrior +. have the misty mountains captain keep the gundabad helmet, and the gundabad
# captain, get this variant"): warriors also wear the tusked helmet; captains wear the Gundabad Warlord body and legs
# with the warlord helmet, the tusked helmet or the owner's helmet (gundabad-bone-armor 0.3.0,
# tools/gundabad_helmet_mod.py), or the bone armor as before. Recruits stay bare-headed in bone armor.
GUNDABAD_HELMET_ONE = "gundabad:armor-head-tierfourplate-gundabadhelmetone-gundabadhelmetone-none"
GUNDABAD_WARLORD = [norc("body", "tierfiveplate", "norcarmorheavier"), norc("legs", "tierfiveplate", "norcleggingsheavier")]
FACTION_SETS["gundabadorc"]["warrior"] = [[head, GUNDABAD_BODY, GUNDABAD_LEGS] for head in (GUNDABAD_HEAD, GUNDABAD_TUSKED_HEAD)]
FACTION_SETS["gundabadorc"]["captain"] = ([[head] + GUNDABAD_WARLORD for head in
                                          (norc("head", "tierfiveplate", "norchelmetheavier"), GUNDABAD_TUSKED_HEAD, GUNDABAD_HELMET_ONE)])
# 0.0.96 (owner, 2026-11-07: "the gundabad captain should not spawn in the bone armor like it does"): the bone armor
# set is gone from captains. Captains already spawned keep what they wear (gear is handed out at spawn).

# Shared orc helmets (0.0.55, user, 2026-11-03: "for all the orc factions except isengard, their helmets should be
# interchangable, for the raider helmet, soldier helmets, and warrior helmets. the moria and gundabad orc helmets
# should remain independent for their faction. as well as commander helmets remaining independent"). Mordor, Angmar
# and Misty Mountains recruits and warriors also get each shared helmet with their own body and legs, as extra whole
# sets after their own (portraits show the first). A set whose helmet mod isn't installed is skipped at spawn.
ANGMAR_SOLDIER_HEAD = angmar("head", "tierfourplate", "angmarhelmetheavy")
SHARED_ORC_HELMETS = {"recruit": [MORD_RAIDER_HEAD, ANGMAR_RAIDER_HEAD], "warrior": [MORD_WARRIOR_HEAD, ANGMAR_SOLDIER_HEAD]}
SHARED_HELMET_FACTIONS = ("mordororc", "angmarorc", "goblin")


def is_head(code):
    return code.split(":", 1)[-1].startswith("armor-head-")


def share_orc_helmets():
    for faction in SHARED_HELMET_FACTIONS:
        for rank, helmets in SHARED_ORC_HELMETS.items():
            sets = FACTION_SETS[faction][rank]
            extra = []
            for own in list(sets):
                rest = [c for c in own if not is_head(c)]
                for helmet in helmets:
                    candidate = [helmet] + rest
                    if candidate not in sets and candidate not in extra:
                        extra.append(candidate)
            sets.extend(extra)


# 0.0.82: the owner's loadout list names every helmet each orc unit may wear, so the shared helmets are no longer added
# (share_orc_helmets is kept for reference, not called).

# 0.0.35 (user, 2026-11-02): the other factions wear their own "To Rule Them All" sets where a mod has one, with
# the medium set for recruits, the heavy set for warriors and the heaviest for captains. Haradrim, Easterlings and
# Tormented Elves have no armor mod of their own, so 0.0.38 (user, 2026-11-03: "the most appropriate armor sets in
# regard to similarity to their intention") borrowed the closest evil-aligned sets, mixed with vanilla pieces in
# 0.0.39; since 0.0.41 they wear the outfits the user picked, at the end of FACTION_SETS.
def trta(domain, part, tier, construct, material=None, last="none"):
    return f"{domain}:armor-{part}-{tier}-{construct}-{material or construct}-{last}"
def trta_set(domain, tier, head, body, legs, last="none"):
    """A whole set whose items repeat their construct as the material."""
    return [trta(domain, "head", tier, head, last=last), trta(domain, "body", tier, body, last=last),
            trta(domain, "legs", tier, legs, last=last)]
def gond(part, tier, construct, material=None, last="none"):
    return trta("gond", part, tier, construct, material, last)
GONDOR_INFANTRY_LEGS = gond("legs", "tierfourplate", "gondorleggingsheavy")
def roha(part, tier, construct, material=None):
    return trta("roha", part, tier, construct, material)
def bnum(part, tier, construct, material=None, last="none"):
    return trta("mord", part, tier, construct, material, last)
def van(part, kind):
    """A vanilla armor piece, such as van("body", "lamellar-copper")."""
    return f"game:armor-{part}-{kind}"
ANGMAR_RANGER = [trta("angmar", "body", "tiertwogambeson", "angmararmorranger"),
                 trta("angmar", "legs", "tiertwogambeson", "angmarleggingsranger")]
# Helmets that also hide beards and mustaches (0.0.81, owner: "beards cannot be worn with these helmets either", the
# Haradrim helmets: veils and a mask over the face). Wildcards, read by lotrnpcs:helmethidehair.
BEARD_HIDING_HELMETS = ["haradrimarmor:armor-head-*"]
# 0.0.96 (owner, 2026-11-07: "with the new gundabad helmet, the ears stick out from the sides"): helmets that also hide
# the ears skin part (orc ears), so they don't poke out of the helmet's sides. Only the owner's Gundabad Helmet.
EAR_HIDING_HELMETS = [GUNDABAD_HELMET_ONE]

# The haradrimarmor mod's items (tools/haradrim_armor_mod.py: armor-<part>-<tier>-haradrim<set><piece>-...-none).
HARADRIM_ARMOR = {rank: [f"haradrimarmor:armor-{part}-{tier}-haradrim{rank}{piece}-haradrim{rank}{piece}-none"
                         for part, piece in (("head", "helmet"), ("body", "armor"), ("legs", "leggings"))]
                  for rank, tier in (("infantry", "tierthreechain"), ("heavy", "tierfourplate"), ("warlord", "tierfiveplate"))}
NADIYAN_CAPES = ["hunter", "peasant1", "peasant2", "peasant3", "shepherd"]
HARADRIM_OUTFITS = [["game:clothes-nadiya-foot-guard", f"game:clothes-nadiya-lowerbody-{pants}", f"game:clothes-nadiya-shoulder-{cape}",
                     van("body", f"tailored-{colour}-linen"), van("legs", f"tailored-{colour}-linen"),
                     "game:clothes-upperbodyover-forgotten", "game:clothes-waist-sturdy-leather-belt",
                     "game:clothes-shoulder-furwrap@neck", "game:clothes-face-miner"]
                    for colour in ("red", "black") for pants in ("guard", "hunter") for cape in NADIYAN_CAPES]
FACTION_SETS.update({
    # Gondor: Man-at-Arms (black, or the White Tree, with or without the coif) for recruits, Infantry (with or
    # without the cloak) for warriors, Fountain Guard with Infantry leggings for captains (the mod has no Fountain
    # Guard leggings).
    # 0.0.82 (owner's list): "Gondorian Man-At-Arms: Either MAA set. MAA or Levy helmet." Every Man-at-Arms colour with
    # its own legs (the White Tree breastplate has no legs of its own: black). "Gondorian Infantry: Full plate only."
    # "Gondorian Fountain Guard: Fountain Guard with cloak only ... Legs should be infantry." Rangers: the archers.
    "gondor": {
        "recruit": [[helmet, gond("body", "tierthreechain", *body), gond("legs", "tierthreechain", "gondorleggingsmedium", "gondorleggingsmedium" + legs)]
                    for body, legs in ((("gondorarmorarms", "gondorarmorarms" + c), c) for c in ("blue", "black", "green", "yellow"))
                    for helmet in (gond("head", "tierthreechain", "gondorhelmetarms"), gond("head", "tierthreechain", "gondorhelmetarms", last="chain"),
                                   gond("head", "tierfourplate", "gondorlevyhelmet"))]
                   + [[helmet, gond("body", "tierthreechain", "gondorarmorarmsblacktwo"), gond("legs", "tierthreechain", "gondorleggingsmedium", "gondorleggingsmediumblack")]
                      for helmet in (gond("head", "tierthreechain", "gondorhelmetarms"), gond("head", "tierthreechain", "gondorhelmetarms", last="chain"),
                                     gond("head", "tierfourplate", "gondorlevyhelmet"))],
        "warrior": [[gond("head", "tierfourplate", "gondorhelmetinfantry"),
                     gond("body", "tierfourplate", "gondorarmorheavy", last=cloak), GONDOR_INFANTRY_LEGS]
                    for cloak in ("none", "cloaked")],
        "captain": [[gond("head", "tierfiveplate", "gondorhelmetinfantry", "gondorhelmetfountain"),
                     gond("body", "tierfiveplate", "gondorarmorfountain", last="cloaked"), GONDOR_INFANTRY_LEGS]],
    },
    # Rohan: Man-at-Arms for recruits, Eorling for warriors (green or red, matching legs), Huscarl for captains.
    # 0.0.82 (owner's list): "Rohirric Warrior: Warrior set only" (the Man-at-Arms set: the mod has none called warrior;
    # my reading), "Eorling: Eorling set only", "Huscarl: Huscarl only". Unchanged.
    "rohan": {
        "recruit": [[roha("head", "tierthreechain", "rohanhelmetmedium"),
                     roha("body", "tierthreechain", "rohanarmormedium", "rohanarmormedium" + c),
                     roha("legs", "tierthreechain", "rohanleggingsmedium", "rohanleggingsmedium" + c)] for c in ("green", "red")],
        "warrior": [[roha("head", "tierfourscale", "rohanhelmetheavy"),
                     roha("body", "tierfourscale", "rohanarmorheavy", "rohanarmorheavy" + c),
                     roha("legs", "tierfourscale", "rohanleggingsheavy", "rohanleggingsheavy" + c)] for c in ("green", "red")],
        "captain": [trta_set("roha", "tierfivescale", "rohanhelmetheavier", "rohanarmorheavier", "rohanleggingsheavier")],
    },
    # Dale (Dale and Dorwinion mod): Lake Guard, Dale Soldier, Dale Captain.
    "dale": {
        "recruit": [trta_set("dale", "tierthreechain", "dalehelmetmedium", "dalearmormedium", "daleleggingsmedium")],
        "warrior": [trta_set("dale", "tierfourscale", "dalehelmetheavy", "dalearmorheavy", "daleleggingsheavy")],
        "captain": [trta_set("dale", "tierfivescale", "dalehelmetheavier", "dalearmorheavier", "daleleggingsheavier")],
    },
    # Arnor: Arnorian Infantry, Soldier, Lord. 0.0.82 (owner's list: "Levy: Full levy set, always helmed"; "Arnor
    # warrior: Full set, can be unhelmed and wear levy helmet"; owner: "use the infantry helm"): the Levy is the
    # Arnorian Infantry set, and warriors may wear its helmet.
    "arnor": {
        "recruit": [trta_set("arnor", "tierthreechain", "arnorhelmetmedium", "arnorarmormedium", "arnorleggingsmedium")],
        "warrior": [trta_set("arnor", "tierfourplate", "arnorhelmetheavy", "arnorarmorheavy", "arnorleggingsheavy"),
                    [trta("arnor", "head", "tierthreechain", "arnorhelmetmedium"), trta("arnor", "body", "tierfourplate", "arnorarmorheavy"),
                     trta("arnor", "legs", "tierfourplate", "arnorleggingsheavy")]],
        "captain": [trta_set("arnor", "tierfiveplate", "arnorhelmetheavier", "arnorarmorheavier", "arnorleggingsheavier")],
    },
    # Dunland (Isengard mod): Dunlending Raider, Warrior, Warlord (either helmet).
    "dunlending": {
        "recruit": [trta_set("iseng", "tierthreechain", "dunlendinghelmetmedium", "dunlendingarmormedium",
                             "dunlendingleggingsmedium", "clean")],
        # 0.0.82 (owner's list): "Dunlending Warrior: Warrior only for armor, can wear raider helmet and unhelmed."
        "warrior": [trta_set("iseng", "tierfourchain", "dunlendinghelmetheavy", "dunlendingarmorheavy",
                             "dunlendingleggingsheavy", "clean"),
                    [trta("iseng", "head", "tierthreechain", "dunlendinghelmetmedium", last="clean"),
                     trta("iseng", "body", "tierfourchain", "dunlendingarmorheavy", last="clean"),
                     trta("iseng", "legs", "tierfourchain", "dunlendingleggingsheavy", last="clean")]],
        "captain": [trta_set("iseng", "tierfiveplate", helmet, "dunlendingarmorheavier", "dunlendingleggingsheavier", "clean")
                    for helmet in ("dunlendinghelmetheavier", "dunlendinghelmetheavier2")],
    },
    # Black Numenoreans (Mordor mod's human armor): Black Numenorean (brown or black) for recruits, black Servant of
    # the Eye (bare or black mask) for warriors, Black Numenorean Lord (with or without the cloak) for captains.
    # 0.0.82 (owner's list, read as this faction's units since factions don't mix races): "Black Numenorean: BN set
    # only, not Lord." "Servant of the Eye: Servant only." Recruits the Black Numenorean set (brown or black), warriors
    # the black Servant of the Eye (bare or black mask), captains the red one with the red mask (my split, so the ranks
    # still look different). No Lord set.
    "blacknumenorean": {
        "recruit": [[bnum("head", "tierthreescale", "servanthelmethumanmedium"),
                     bnum("body", "tierthreescale", "servantarmorhumanmedium", "servantarmorhumanmedium" + c),
                     bnum("legs", "tierthreescale", "servantleggingshumanmedium")] for c in ("brown", "black")],
        "warrior": [[bnum("head", "tierfourplate", "servanthelmethumanheavy", last=mask),
                     bnum("body", "tierfourplate", "servantarmorhumanheavy", "servantarmorhumanheavyblack"),
                     bnum("legs", "tierfourplate", "servantleggingshumanheavy")] for mask in ("none", "maskblack")],
        "captain": [[bnum("head", "tierfourplate", "servanthelmethumanheavy", last="maskred"),
                     bnum("body", "tierfourplate", "servantarmorhumanheavy", "servantarmorhumanheavyred"),
                     bnum("legs", "tierfourplate", "servantleggingshumanheavy")]],
    },
    # Dwarves (Dwarves mod): Militia (brown), Warrior, Lord.
    "dwarf": {
        "recruit": [[trta("dwarf", "head", "tierthreechain", "dwarfhelmetmedium"),
                     trta("dwarf", "body", "tierthreechain", "dwarfarmormedium", "dwarfarmormediumbrown"),
                     trta("dwarf", "legs", "tierthreechain", "dwarfleggingsmedium")]],
        "warrior": [trta_set("dwarf", "tierfourplate", "dwarfhelmetheavy", "dwarfarmorheavy", "dwarfleggingsheavy")],
        "captain": [trta_set("dwarf", "tierfiveplate", "dwarfhelmetheavier", "dwarfarmorheavier", "dwarfleggingsheavier")],
    },
    # Wicked dwarves (Dwarves mod): Footman, Warrior, Lord.
    "wickeddwarf": {
        "recruit": [trta_set("dwarf", "tierthreechain", "evildwarfhelmetmedium", "evildwarfarmormedium", "evildwarfleggingsmedium")],
        "warrior": [trta_set("dwarf", "tierfourplate", "evildwarfhelmetheavy", "evildwarfarmorheavy", "evildwarfleggingsheavy")],
        "captain": [trta_set("dwarf", "tierfiveplate", "evildwarfhelmetheavier", "evildwarfarmorheavier", "evildwarfleggingsheavier")],
    },
    # Haradrim, Easterlings and tormented elves have no armor mod of their own. 0.0.41 (user, 2026-11-03) gives each the
    # outfit the user picked, mostly vanilla clothing and armor. An item code may end in "@slot" (a gear slot such as
    # face or neck) to wear it in that slot instead of its usual one, so an NPC can wear two pieces meant for the same
    # slot; the gear renderer draws every slot.
    # Haradrim, every rank alike (the user named no rank differences): Nadiyan guard boots, Nadiyan guard or hunter
    # pants, any Nadiyan cape, a black or red tailored gambeson (body and legs), the Forgotten noble robe, a sturdy
    # leather belt, the fur wrap (worn in the neck slot, since the cape takes the shoulder slot) and the miner mask.
    # 0.0.78 (owner, 2026-11-04: "Use these as skins for the haradrim, mostly as a temporary placeholder"): the owner's
    # three armor models as a separate armor mod, haradrimarmor (tools/haradrim_armor_mod.py, built to
    # build-inputs/haradrim-armor-<version>.zip): infantry for recruits (and archers), heavy infantry for warriors,
    # warlord for captains. Without that mod they wear the outfits above (FALLBACK_SETS), never a mix of the two.
    "haradrim": {"recruit": [HARADRIM_ARMOR["infantry"]], "warrior": [HARADRIM_ARMOR["heavy"]],
                 "captain": [HARADRIM_ARMOR["warlord"]]},
    # Easterlings (user): tin bronze brigandine for recruits, tin bronze plate for warriors, and for captains tin bronze
    # plate with the Woodland Sentinel helmet over an iron chain coif covering the face (worn in the face slot).
    "easterling": {
        "recruit": [[van(part, "brigandine-tinbronze") for part in ("head", "body", "legs")]],
        "warrior": [[van(part, "plate-tinbronze") for part in ("head", "body", "legs")]],
        "captain": [[trta("wlel", "head", "tierthreescale", "woodlandhelmetmedium"), van("head", "chain-iron") + "@face",
                     van("body", "plate-tinbronze"), van("legs", "plate-tinbronze")]],
    },
    # Tormented elves (user): the Angmar Ranger tunic and leggings, bareheaded, and the captain in the Rotten king crown.
    "tormentedelf": {
        "recruit": [ANGMAR_RANGER],
        "warrior": [ANGMAR_RANGER],
        "captain": [ANGMAR_RANGER + ["game:clothes-head-rottenking"]],
    },
    # Elves (Woodland Realm mod, the only elf armor): Woodland Sentinel, Wood-elf Warrior, Mirkwood Gate Warden.
    "elf": {
        "recruit": [trta_set("wlel", "tierthreescale", "woodlandhelmetmedium", "woodlandarmormedium", "woodlandleggingsmedium")],
        "warrior": [trta_set("wlel", "tierfourscale", "woodlandhelmetheavy", "woodlandarmorheavy", "woodlandleggingsheavy")],
        "captain": [trta_set("wlel", "tierfivescale", "woodlandhelmetheavier", "woodlandarmorheavier", "woodlandleggingsheavier")],
    },
})


# Hobbits (0.0.55, user, 2026-11-03: "give them the clothes similar to vanilla traders, minus any helmets. instead
# maybe a hat on rare occasion"). Vanilla traders' outfits are drawn onto their model, not worn items, so these are
# vanilla clothes in the same plain, homely trader style: a shirt, trousers or breeches, and a belt, waistcoat, apron
# or scarf. All vanilla, so they work on both servers. Hobbits stay barefoot. One outfit per hobbit at random; a hat
# one time in eight.
def _c(*codes):
    return ["game:clothes-" + c for c in codes]
HOBBIT_SETS = [
    _c("upperbody-peasent-shirt", "lowerbody-braiesbrown", "waist-peasant-strap"),
    _c("upperbody-homespun-shirt", "lowerbody-homespun-pants", "upperbodyover-quilteddoublet"),
    _c("upperbody-commoner-shirt", "lowerbody-commoner-trousers", "waist-sturdy-leather-belt"),
    _c("upperbody-pastoral-shirt", "lowerbody-pastoral-pants", "shoulder-apronbrown"),
    _c("upperbody-merchant-shirt", "lowerbody-merchant-pants", "waist-merchant-belt"),
    _c("upperbody-farmhand", "lowerbody-farmhand", "waist-farmhand"),
    _c("upperbody-ruralfarmer", "lowerbody-braiesplain", "waist-ruralfarmer"),
    _c("upperbody-linen-tunic", "lowerbody-workmans-gown", "shoulder-woolen-scarf"),
    _c("upperbody-woolen-shirt", "lowerbody-shepherd-pants", "upperbodyover-commoner-coat"),
    _c("upperbody-tailor-blouse", "lowerbody-tailor-trousers", "upperbodyover-tailor-jacket"),
    _c("upperbody-messenger-shirt", "lowerbody-messenger-trousers", "waist-messenger-belt"),
    _c("upperbody-marketeer", "lowerbody-marketeer", "waist-marketeer"),
]
HOBBIT_GEAR = HOBBIT_SETS[0]
HOBBIT_HATS = {"chance": 0.125, "items": _c("head-straw-hat", "head-roll-hat", "head-broadbrim", "head-thrumcap",
                                              "head-capotain", "head-merchant-hat")}
HATS = {"hobbit": HOBBIT_HATS}
FACTION_SETS["hobbit"] = {"recruit": HOBBIT_SETS}
# Archers wear their faction's recruit armor, with the same helmets: Mordor's light Orc Raider mail since 0.0.55, the
# other factions' recruit sets since 0.0.69/0.0.70 (Gundabad's bone armor is the same for every rank).
for _f in ARCHER_RACES:
    FACTION_SETS[_f]["archer"] = [list(st) for st in FACTION_SETS[_f]["recruit"]]
# 0.0.82 (owner's list): "Gondorian Ranger: Ranger set only, not chainmail variant."
FACTION_SETS["gondor"]["archer"] = [[gond("head", "tiertwogambeson", "gondorrangerhelmet", last="gondorrangermask"),
                                     gond("body", "tiertwogambeson", "gondorrangerarmor"), gond("legs", "tiertwogambeson", "gondorrangerleggings")]]

# Spawning bare-headed (0.0.82, owner's list: "All orcs and uruks can be unhelmed unless otherwise noted", plus each
# unit marked "can be unhelmed"; owner: "all units should still have the option to be unhelmeted", so every unit keeps
# "No helmet" in the looks menu). A unit listed False always spawns with its helmet; True rolls HelmetChance. Factions
# the list doesn't cover keep rolling as before.
ORC_RACES = {"mordororc", "angmarorc", "goblin", "uruk", "gundabadorc"}
HELMET_ALWAYS = {
    "gondor": {"recruit", "warrior", "captain", "archer"},
    "rohan": {"recruit", "archer"},
    "dunlending": {"captain"},
    "elf": {"recruit", "warrior", "captain", "archer"},
    "dwarf": {"captain"}, "wickeddwarf": {"captain"},
    "dale": {"recruit", "warrior", "archer"},
    "arnor": {"recruit", "archer"},
    "blacknumenorean": {"recruit", "warrior", "captain", "archer"},
}
def helmet_optional(faction, rank):
    if faction in ORC_RACES:
        return True
    return rank not in HELMET_ALWAYS.get(faction, set())


# Sets worn only when none of a type's FACTION_SETS is complete (0.0.78): the Haradrim's 0.0.41 outfits stand in when
# the haradrimarmor mod isn't installed. Archers wear the recruit ones, as with FACTION_SETS.
FALLBACK_SETS = {"haradrim": {rank: HARADRIM_OUTFITS for rank in ("recruit", "warrior", "captain", "archer")}}


def ranks(race, people, model, skin):
    """Recruit, warrior and captain test NPCs of one faction."""
    return [(f"test-{race}-{rank}", f"{people} {BRANCH_NAMES.get(rank, rank)}", model, skin, gear)
            for rank, gear in (("recruit", RECRUIT_GEAR), ("warrior", WARRIOR_GEAR), ("captain", CAPTAIN_GEAR), ("archer", RECRUIT_GEAR),
                               ("guard", WARRIOR_GEAR), ("shock", WARRIOR_GEAR))]


TEST_NPCS = [
    ("test-goblin", "Goblin", "lotrnpcs:goblin", SKINS + "orc/orcskin6", []),
    ("test-mordororc-recruit", "Mordor orc recruit", "lotrnpcs:mordororc", ORC_SKIN, RECRUIT_GEAR),
    ("test-mordororc-warrior", "Mordor orc warrior", "lotrnpcs:mordororc", ORC_SKIN, WARRIOR_GEAR),
    ("test-mordororc-captain", "Mordor orc captain", "lotrnpcs:mordororc", ORC_SKIN, CAPTAIN_GEAR),
    ("test-mordororc-archer", "Mordor orc archer", "lotrnpcs:mordororc", ORC_SKIN, RECRUIT_GEAR),
    ("test-mordororc-guard", "Mordor orc guard", "lotrnpcs:mordororc", ORC_SKIN, WARRIOR_GEAR),
    ("test-mordororc-shock", "Mordor orc shock trooper", "lotrnpcs:mordororc", ORC_SKIN, WARRIOR_GEAR),
    # Angmar orcs (user): their own faction, same race model and stats as Mordor orcs, Angmar armor.
    ("test-angmarorc-recruit", "Angmar orc recruit", "lotrnpcs:mordororc", ORC_SKIN, RECRUIT_GEAR),
    ("test-angmarorc-warrior", "Angmar orc warrior", "lotrnpcs:mordororc", ORC_SKIN, WARRIOR_GEAR),
    ("test-angmarorc-captain", "Angmar orc captain", "lotrnpcs:mordororc", ORC_SKIN, CAPTAIN_GEAR),
    ("test-angmarorc-archer", "Angmar orc archer", "lotrnpcs:mordororc", ORC_SKIN, RECRUIT_GEAR),
    ("test-angmarorc-guard", "Angmar orc guard", "lotrnpcs:mordororc", ORC_SKIN, WARRIOR_GEAR),
    ("test-angmarorc-shock", "Angmar orc shock trooper", "lotrnpcs:mordororc", ORC_SKIN, WARRIOR_GEAR),
    ("test-uruk", "Uruk-hai", "lotrnpcs:uruk", SKINS + "orc/orcskin9", []),
    # Misty Mountains orcs and Isengard Uruk-hai (user): same ranks and stats as Mordor orcs, their own armor.
    ("test-goblin-recruit", "Misty Mountains orc recruit", "lotrnpcs:goblin", SKINS + "orc/orcskin6", RECRUIT_GEAR),
    ("test-goblin-warrior", "Misty Mountains orc warrior", "lotrnpcs:goblin", SKINS + "orc/orcskin6", WARRIOR_GEAR),
    ("test-goblin-captain", "Misty Mountains orc captain", "lotrnpcs:goblin", SKINS + "orc/orcskin6", CAPTAIN_GEAR),
    ("test-goblin-archer", "Misty Mountains orc archer", "lotrnpcs:goblin", SKINS + "orc/orcskin6", RECRUIT_GEAR),
    ("test-goblin-guard", "Misty Mountains orc guard", "lotrnpcs:goblin", SKINS + "orc/orcskin6", WARRIOR_GEAR),
    ("test-goblin-shock", "Misty Mountains orc shock trooper", "lotrnpcs:goblin", SKINS + "orc/orcskin6", WARRIOR_GEAR),
    ("test-uruk-recruit", "Isengard Uruk-hai recruit", "lotrnpcs:uruk", SKINS + "orc/orcskin9", RECRUIT_GEAR),
    ("test-uruk-warrior", "Isengard Uruk-hai warrior", "lotrnpcs:uruk", SKINS + "orc/orcskin9", WARRIOR_GEAR),
    ("test-uruk-captain", "Isengard Uruk-hai captain", "lotrnpcs:uruk", SKINS + "orc/orcskin9", CAPTAIN_GEAR),
    ("test-uruk-archer", "Isengard Uruk-hai archer", "lotrnpcs:uruk", SKINS + "orc/orcskin9", RECRUIT_GEAR),
    ("test-uruk-guard", "Isengard Uruk-hai guard", "lotrnpcs:uruk", SKINS + "orc/orcskin9", WARRIOR_GEAR),
    ("test-uruk-shock", "Isengard Uruk-hai shock trooper", "lotrnpcs:uruk", SKINS + "orc/orcskin9", WARRIOR_GEAR),
    # Gundabad (0.0.62, work in progress): the Mordor orc body at 0.9 size (user's choice), the bone armor, no helmets.
    # 0.0.63: their own race model, lotrnpcs:gundabadorc (bundle_lrc.py), the Mordor orc with lighter skins.
    *[(f"test-gundabadorc-{rank}", f"Gundabad orc {BRANCH_NAMES.get(rank, rank)}", "lotrnpcs:gundabadorc", SKINS + "orc/orcskin1", GUNDABAD_FALLBACK[BASE_RANK.get(rank, rank)])
      for rank in ("recruit", "warrior", "captain", "archer", "guard", "shock")],
    ("test-hobbit", "Hobbit", "lotrnpcs:hobbit", SKINS + "hobbit/hobbitskin1", HOBBIT_GEAR),
    # 0.0.35 (user, 2026-11-02): every faction has recruits, warriors and captains. These replace the unarmored
    # Gondor, Rohan, Dunlending, Easterling and Dwarf test NPCs.
    *ranks("gondor", "Gondor", "lotrnpcs:gondor", SKINS + "human/westernmen/humanskin1"),
    *ranks("rohan", "Rohan", "lotrnpcs:rohan", SKINS + "human/westernmen/humanskin1"),
    *ranks("dale", "Dale", "lotrnpcs:dale", SKINS + "human/westernmen/humanskin2"),
    *ranks("arnor", "Arnor", "lotrnpcs:arnor", SKINS + "human/westernmen/humanskin27"),
    *ranks("haradrim", "Haradrim", "lotrnpcs:haradrim", SKINS + "human/haradrim/humanskin23"),
    *ranks("dunlending", "Dunlending", "lotrnpcs:dunlending", SKINS + "human/westernmen/humanskin1"),
    *ranks("blacknumenorean", "Black Númenórean", "lotrnpcs:blacknumenorean", SKINS + "human/blacknumenor/humanskin17"),
    *ranks("easterling", "Easterling", "lotrnpcs:easterling", SKINS + "human/easterling/humanskin10"),
    *ranks("dwarf", "Dwarf", "lotrnpcs:dwarf", SKINS + "dwarf/dwarfskin1"),
    *ranks("wickeddwarf", "Wicked dwarf", "lotrnpcs:wickeddwarf", SKINS + "dwarf/dwarfskin3"),
    *ranks("elf", "Elf", "lotrnpcs:elf", SKINS + "elf/elfskin1"),
    *ranks("tormentedelf", "Tormented elf", "lotrnpcs:tormentedelf", SKINS + "elf/elfskin11"),
]

# 0.0.117 mounted riders (docs/notes/feverstone-mounts.md; owner, 2026-11-12: Rohan, Gondor, Dale and Arnor on horses,
# Isengard, Mordor, Gundabad and Angmar orcs on dire wolves, elves on elk, dwarves on Teapot's giant boars; the mount
# spawns as part of the unit, and the mod works without the mounts' mods). Each rider type lists the mounts it may get;
# Riders.cs uses the first ones that exist in the world, and offers no rider rank when none do.
HORSES = [f"feverstonewilds:horse-tamed-{g}-{t}" for t in ("quarter", "shire") for g in ("male", "female")]
DIREWOLVES = [f"feverstonewilds:tame-direwolf-{g}" for g in ("male", "female")]
ELK = [f"game:tameddeer-elk-{g}-adult" for g in ("male", "female")]
GIANT_BOARS = [f"teapotsgiantboar:tame-giantboar-giant-{t}-male" for t in ("01", "02", "03", "04", "05")]     + [f"teapotsgiantboar:tame-giantboar-giant-{t}-female" for t in ("01", "02")]
MOUNTS_OF = {"gondor": HORSES, "rohan": HORSES, "dale": HORSES, "arnor": HORSES,
             "uruk": DIREWOLVES, "mordororc": DIREWOLVES, "gundabadorc": DIREWOLVES, "angmarorc": DIREWOLVES,
             "elf": ELK, "dwarf": GIANT_BOARS}
TEST_NPCS += [(code.removesuffix("-warrior") + "-rider", name.removesuffix(" warrior") + " rider", model, skin, gear)
              for code, name, model, skin, gear in list(TEST_NPCS)
              if code.endswith("-warrior") and code.removeprefix("test-").removesuffix("-warrior") in MOUNTS_OF]
# Natural spawning (user, 2026-11-02): recruits, warriors and rare captains spawn on the surface inside admin
# zones (/recruits zone) that list their faction, one group every 3 to 5 minutes near a player. SpawnZones.cs does all
# of it, finding the NPC types by these lotrnpcsFaction and lotrnpcsRank attributes; there are no vanilla spawn
# rules, because a vanilla spawn chance can't promise a steady rate (0.0.29 used them).
# Every NPC's faction id (the code's race part -> faction), read by the faction system from lotrnpcsFaction.
FACTION_OF = {"mordororc": "mordor", "angmarorc": "angmar", "goblin": "mistymountains", "uruk": "isengard", "gundabadorc": "gundabad",
              "gondor": "gondor", "rohan": "rohan", "dale": "dale", "arnor": "arnor", "haradrim": "haradrim",
              "dunlending": "dunlendings", "blacknumenorean": "blacknumenoreans", "easterling": "easterlings",
              "dwarf": "dwarves", "wickeddwarf": "wickeddwarves", "hobbit": "shire", "elf": "elves",
              "tormentedelf": "tormentedelves"}
# NPCs whose code doesn't end in their rank. Hobbits have one rank, farmers, which counts as the Shire's recruit rank
# (user, 2026-11-02: "make them recruit equivalent"), so they get recruit stats and Shire zones spawn them.
RANK_OF = {"test-hobbit": "recruit"}
# Left out of the creative menu (user, 2026-11-03: "in the menu there is still basic npcs for uruk hai, goblin, and
# hobbit"). Their entity types stay, so NPCs already in a world keep loading. Hobbits came back in 0.0.42 (user: "put
# hobbits in the spawn menu anyway").
HIDDEN = {"test-goblin", "test-uruk"}


# Names on hire (0.0.54, user, 2026-11-03: "when hired have their name be randomized anyway to names befitting the
# race"). Written in the style of each people, avoiding the names of well-known characters from the books and films.
# Edit freely: Looks.cs picks one at random (hobbits add a family name from HOBBIT_SURNAMES).
def _n(text):
    return [w for w in text.split()]
NAMES = {
    "orc": _n("""Grushnak Mauzgul Ughrath Skabrak Narzug Gorbuth Lugmakh Rathgul Krimpul Ushnar Dumbag Shagrol Brogga
        Yagrish Gazmog Thrakul Ograth Snurk Zogbad Ghashnak Rudluk Morgrub Drabshak Krulg Burzak Ulgrod Garluk Skrith
        Nargath Grishmuk Shagbur Mugrot Dushnak Kragor Uftak Gorgash Bolgrim Snagrut Zarbuk Hurlug Grakhul Muzrak"""),
    "goblin": _n("""Snikrit Gribble Nazzik Skritch Gobbrak Ruknit Zitgob Yikrak Snatchgut Mogwit Pikgrub Grittle Skabnit
        Ruzgob Fizzak Gnashtooth Splitnail Krikkit Nibbluk Zagrat Skulk Ratchit Grubnose Wizzug Snarlik Tiktak Muckrot
        Blitgrub Snivvek Kribnak Gnasher Squiggot Rotfang Glimrit Yarnik Skreek Durnak Zibbit Grizzlak Pizgut"""),
    "uruk": _n("""Ugrath Mazrûkh Thrakdur Gorzhak Durbag Krazgul Mogrûsh Ulgrath Shazbur Barzog Ruknar Grôdush Uzbrak
        Hragmor Zograth Drûbak Narghul Vragash Burzûk Gashnar Mûrzag Throkul Ozgrim Kardûk Rûgash Skorbag Grimûk Lûthrak
        Dagrûm Krûdash Morzag Ulbrûk Thrazûl Bagrûsh Zûrgath Harkûn Gûlmok Raznûk Draugûl Vûrzak"""),
    "gondor": _n("""Arthondir Belegorn Caladhir Dorhalion Ecthelorn Galdrion Hallanor Imlorn Lossorin Mardion Nimrion
        Orodhir Pelendir Rhovanor Silmandir Tarcalion Ulbarad Valandor Anardion Berendir Calimbor Dúinor Eranor Fendil
        Gwinoth Herumir Ithilmar Lothiran Mithrion Narmion Ostorion Pelarion Romendil Seregorn Thalorn Turmir Valarion
        Hirvorn Duinmar Elphion Morthil Lindorin"""),
    "rohan": _n("""Aldhelm Beorhtric Cynric Dernwine Eadric Ealdwine Eofor Folcred Garmund Godric Heoruwine Hunwald Leofric
        Osric Sigebert Wiglaf Wulfhere Aethelmund Brytmund Ceolric Deorhelm Ecgbert Eormen Frithwulf Gunthere Hereward
        Ingwald Leodwine Merewald Ordlaf Raedwald Saewine Thurstan Wigmund Wynstan Aelfric Beornwulf Cuthred Eadwulf
        Guthmund Oswulf Sigewine Wulfstan Eadgifu Hildeburh Leofrun Wynflaed Osgyth"""),
    "dale": _n("""Arnulf Bjarki Dagfinn Eilif Finnvard Geirmund Hallvard Ingolf Jorund Ketil Leif Mord Oddvar Ragnvald
        Sveinbjorn Thorvald Ulfar Vigfus Arnbjorn Bersi Dalgrim Egil Floki Grimkel Hrafn Ingvar Kolbein Ljot Orm Ragnar
        Starkad Thorkel Ulf Vali Asgaut Bodvar Eyvind Gunnar Halldor Hrolf Skuli Torfi Yngvar Asa Bergthora Gudrun Ingrid
        Sigrun"""),
    "arnor": _n("""Arandil Beleghir Caranhir Dirion Elathorn Faramund Gilrion Hallorn Ithildor Lothron Mallorin Nardil
        Ornedhel Tarondil Ulmorn Vorandir Adaneth Barandir Celebrion Dorlion Eradil Fornion Galadir Hiriel Imladir Lindorn
        Morandir Nimeth Ostrion Rhudion Sarnion Tharion Dúnhir Annúndir Fornedhel Calenor Eriadon Hathaldir Maelorn Thorondir"""),
    "haradrim": _n("""Azrabek Bahrun Dasharim Fazzad Haruzan Imradek Jharam Kasrul Malikar Qadirun Rashkar Sahrim Tazmir
        Umbarek Yazdar Zahruk Amrasad Barzim Dhurak Fahrun Ghazir Hassad Ilkhar Jamzir Karrash Mazrun Nurhad Orzam Qasrim
        Rahzar Sukhrad Tahrin Urzah Zafrim Azhara Dalima Jahrisa Nazira Samira Kharuzad"""),
    "dunlending": _n("""Bran Cadoc Dyfan Eogan Fergal Gwrach Hywel Ithel Lugaid Madoc Nechtan Owain Rhodri Seisyll Tegid
        Urien Brychan Cynan Drust Einion Fiach Garbh Iorwerth Llew Morcant Nudd Peredur Rhun Talorc Uaithne Cormac Donal
        Feidlim Gartnait Loarn Maelgwn Ninian Ruadh Tadhg Aine Brigid Morfudd Nest"""),
    "easterling": _n("""Altan Borjin Chagar Dorgun Erdan Ghulkan Hulagar Ishbek Jochir Kaidar Mönkar Nogai Ogdar Qutlug
        Sartan Temur Ulagan Yesun Zhurgen Arslan Batur Chinbat Daruga Ebugen Gansukh Khasar Kuchum Mergen Nairan Orluk
        Sübar Tolui Ulzan Yeke Bortai Khulan Oyuun Sorgan Tsetseg Alaqa"""),
    "blacknumenorean": _n("""Azrubêl Belzagar Dûnzirak Ghazulâth Hûrzimir Izrakhôr Kalimzôr Luthârzan Mazgûthar Nâzrimil
        Phârazul Rûthimôr Sakalthûn Thârzimil Ulbazôr Zimrakhôr Adrazân Bêlthazôr Dâgnimil Gimrazôr Hazrubân Imrakhûl
        Karanzôr Lôkhazir Mûrazân Nimruzîr Ôrzagal Rakhalzôr Sûrimbêl Tûrzagar Uzrakhôn Zâbarmil Azrabêth Inzilrâ
        Nimruzêth Zimrabêth Mîrazên Bêlzimrâ Khôrazân Thûlzagar"""),
    "dwarf": _n("""Alfr Andvari Austri Brokkr Eitri Fjalar Frosti Ginnar Hanarr Jari Litr Nyr Reginn Sviurr Skirvir Virvir
        Vestri Vigg Barzûn Durgrim Khazrik Thrandar Grimbur Hrundar Kôrnin Brodrin Fenrik Dolgrin Hamrin Brokkvar Thurbin
        Galdur Ragdur Skorri Ulfrin Vargrim Drogni Hjalmrin Kragni Thorgni Bari Hlorin Daggar"""),
    "elf": _n("""Amdirion Belegcam Caladwen Celebrin Daerion Edhelion Faelon Galadhorn Gilwen Hithaeron Ithilwen Laurion
        Lómedhel Mithwen Nimroth Orophel Rínon Sarnel Tathariel Uialwen Aerandil Belthanor Calenglad Duilinor Elenroth
        Faerwen Galathorn Hiriel Ilmendir Lossëwen Meludir Nenharma Onodrion Rochendil Silivren Thalanor Uinéniel Varnion
        Calaerwen Lothmir"""),
}
HOBBIT_NAMES = _n("""Aldo Basso Barnabas Bartholo Cobbin Cotman Dandry Elbo Emmet Falmo Fenno Gundo Hob Hobson Ivo Jasper
    Kipper Linden Lindo Milton Nedd Oddo Perry Quill Robin Tobble Willum Barley Clover Fern Hazel Myrtle Nell Petunia
    Primrose Tansy Violet Winnie Bramble Tilly""")
HOBBIT_SURNAMES = _n("""Applethorn Barleycorn Brackenbury Bunce Burrowell Copperkettle Dimbleby Fairmead Greenhill
    Hedgecroft Honeycomb Longacre Mossfoot Oakbottom Puddlewick Rushmead Sandyfoot Thistlewood Tunnelly Whitbrook
    Woodruff Elderberry Buttercup Bramblebank Hollowell Pebbleton Mallowby Cobblestone Hayward Furrowfield""")
# Which list each people uses (the code's race part).
NAMES_OF = {"mordororc": "orc", "angmarorc": "orc", "gundabadorc": "orc", "goblin": "goblin", "uruk": "uruk", "gondor": "gondor", "rohan": "rohan",
            "dale": "dale", "arnor": "arnor", "haradrim": "haradrim", "dunlending": "dunlending",
            "blacknumenorean": "blacknumenorean", "easterling": "easterling", "dwarf": "dwarf", "wickeddwarf": "dwarf",
            "elf": "elf", "tormentedelf": "elf"}


# Spawn item portraits (user, 2026-11-03: "villager esque item profile pictures of the head, some shoulder, and the
# armor the entity is wearing"): Portrait.cs builds them from these specs. Evil factions scowl, the others look resolute,
# hobbits are kind; a race model without that expression shows its first one.
EVIL = {"mordor", "angmar", "mistymountains", "gundabad", "isengard", "dunlendings", "blacknumenoreans", "easterlings", "haradrim",
        "wickeddwarves", "tormentedelves"}
PORTRAIT_FACE = {f: "angry" for f in EVIL} | {"shire": "kind"}
# Skin parts a race's portrait shows instead of the model's first variant. Without an entry the portrait takes the
# first hair style that isn't bald (Portrait.cs), since a bald head with the model's default pale features read as
# faceless (user, 2026-11-03). Dwarves get a full beard.
PORTRAIT_PARTS = {"dwarf": {"beard": "brd-box20-full"}, "wickeddwarf": {"beard": "brd-box20-full"}}
PORTRAITS = {}

for code, name, model, skin_texture, gear in TEST_NPCS:
    preset = {"code": "lotrnpcs:presetskinmodel", "skinModel": model}
    skin = [preset, {"code": "PlayerModelLib:ExtraSkinnable"}]
    # The gear inventory goes after Player Model Lib, which swaps in the race model that the gear is added onto.
    # 0.0.80 (owner: "when the helmet is on, hide the hair options underneath"): after the gear, so it can see a helmet.
    # 0.0.81 (owner: "beards cannot be worn with these helmets either"): the Haradrim helmets hide beards and mustaches too.
    client_gear = [{"code": "seraphinventory"}, {"code": "lotrnpcs:helmethidehair", "hideBeardUnder": BEARD_HIDING_HELMETS, "hideEarsUnder": EAR_HIDING_HELMETS}] if gear else []
    faction, _, rank = code.removeprefix("test-").partition("-")
    rank = RANK_OF.get(code, rank)
    # 0.0.113: guards and shock troops wear the warrior's armor and offer its helmets.
    base = BASE_RANK.get(rank, rank)
    sets = FACTION_SETS.get(faction, {}).get(base, [])
    # 0.0.78: sets worn only when no set above is complete (the owner's armor mod isn't installed).
    fallback_sets = FALLBACK_SETS.get(faction, {}).get(base, [])
    server_gear = [{"code": "seraphinventory"}, {"code": "lotrnpcs:startinggear", "sets": sets, "items": gear}
                   | ({"fallbackSets": fallback_sets} if fallback_sets else {})
                   | ({"hats": HATS[faction]} if faction in HATS else {})] if gear else []
    # Gear textures go before Player Model Lib, whose texture lookup only knows the race model's own textures.
    gear_textures = [{"code": "lotrnpcs:geartextures"}] if gear else []
    health, armor_tier, melee = RANK_STATS.get(rank, RANKLESS_STATS)
    attributes = {"lotrnpcsFaction": FACTION_OF[faction]}
    if code in HIDDEN:
        attributes["inCreativeInventory"] = False
    if rank in RANK_STATS:
        attributes["lotrnpcsRank"] = rank
    if rank == "rider":
        attributes["lotrnpcsMounts"] = MOUNTS_OF[faction]
    if faction == "hobbit":
        attributes["lotrnpcsNames"], attributes["lotrnpcsSurnames"] = HOBBIT_NAMES, HOBBIT_SURNAMES
    elif faction in NAMES_OF:
        attributes["lotrnpcsNames"] = NAMES[NAMES_OF[faction]]
    # The helmets its owner may choose in the looks menu (0.0.58, user, 2026-11-03: "the helmets only have the switch
    # on and off, not the option to choose the helmet"): every helmet in this type's armor sets. Not the vanilla
    # fallback outfit's, which would sit beside the faction helmets on a server with the armor mods; an NPC in the
    # fallback outfit still gets its own helmet as a choice (Looks.HelmetChoices adds the one worn or kept). Pieces with
    # an @slot (the Easterling captain's coif on the face) aren't helmets.
    # 0.0.76 (user: "Each rank of a faction should have access to the helmets of the ranks beneath it as well as its
    # own rank"): its own first, then the lower ranks'. Archers are promoted recruits and wear recruit sets already.
    # 0.0.112 (owner, 2026-11-09: "follow the prompt for armors to make sure they have the correct helmets whitelisted",
    # "specifically orcs"): orcs and Uruk-hai offer only the helmets the 0.0.82 loadout list gives their own unit (a
    # Black Uruk no longer picks the Raider helmets, an Uruk captain only the General helmets).
    lower = [] if faction in ORC_RACES else {"warrior": ["recruit"], "captain": ["recruit", "warrior"]}.get(base, [])
    all_sets = sets + [st for r in lower for st in FACTION_SETS.get(faction, {}).get(r, [])]
    helmets = list(dict.fromkeys(c for s in all_sets for c in s if "armor-head-" in c and "@" not in c))
    if helmets:
        attributes["lotrnpcsHelmets"] = helmets
        # 0.0.82: whether it may spawn bare-headed (HELMET_ALWAYS); read by Looks.RollHelmet.
        attributes["lotrnpcsHelmetOptional"] = helmet_optional(faction, base)
    PORTRAITS[code] = {"model": model, "skin": skin_texture,
                       "parts": {"facialexpression": PORTRAIT_FACE.get(FACTION_OF[faction], "determined")} | PORTRAIT_PARTS.get(faction, {}),
                       "sets": sets + fallback_sets, "items": gear}
    weapons = [held_weapon(faction, rank)] if rank == "archer" or ((faction in WEAPONS_OF or faction in SHIELD_OF or faction in WEAPON_PREFS) and base in ("recruit", "warrior", "captain")) else []
    write(f"entities/{code}.json", entity(
        code, name, attributes,
        [preset] + gear_textures + skin[1:] + [{"code": "PlayerModelLib:EntityTextureSourceBehavior"}] + client_gear + weapons,
        skin + server_gear + weapons + ([{"code": "lotrnpcs:rider"}] if rank == "rider" else []),
        health, armor_tier, melee, archer=rank == "archer",
    ))

write_weapons()

# The creative tab's order (0.0.71): free peoples, then the Shadow, each faction's units together, recruit to archer.
CREATIVE_FACTIONS = ["shire", "gondor", "rohan", "arnor", "dale", "dwarves", "elves",
                     "mordor", "angmar", "mistymountains", "gundabad", "isengard", "haradrim", "easterlings", "dunlendings",
                     "blacknumenoreans", "wickeddwarves", "tormentedelves"]
CREATIVE_RANKS = ["recruit", "warrior", "guard", "shock", "captain", "rider", "archer"]
def _creative_key(code):
    race, _, rank = code.removeprefix("test-").partition("-")
    return CREATIVE_FACTIONS.index(FACTION_OF[race]), CREATIVE_RANKS.index(RANK_OF.get(code, rank))
CREATIVE_ORDER = sorted((c for c, *_ in TEST_NPCS if c not in HIDDEN), key=_creative_key)
assert set(FACTION_OF.values()) == set(CREATIVE_FACTIONS)

write("itemtypes/creature.json", {
    "code": "creature",
    "class": "ItemCreature",
    "maxstacksize": 64,
    "variantgroups": [{"code": "type", "states": [c for c, *_ in TEST_NPCS]}],
    # The plain full-body mesh, shown only if a portrait can't be built.
    "shape": {"base": FA_BODY},
    "texturesByType": {f"*-{c}": {"seraph": {"base": skin}} for c, _, _, skin, _ in TEST_NPCS},
    "behaviors": [{"name": "lotrnpcs:npcportrait"}],
    "attributesByType": {f"*-{c}": {"lotrnpcsPortrait": spec} for c, spec in PORTRAITS.items()},
    # Own creative tab (user): "LOTR NPCs", plus the general tab so the search finds them. The hidden NPCs' items
    # stay registered, so ones already in an inventory don't turn into unknown items.
    # 0.0.71 (user, 2026-11-04: "re-order the creative inventory to have all the units of a faction next to each
    # other, not scattered about"): the game lists a tab by item id, and a world hands out ids in the order variants
    # first appeared (new archers landed at the end). So one variant (the hidden goblin, never shown itself) carries
    # the whole list as explicit stacks, which the tab shows in the order given: CREATIVE_ORDER.
    "creativeinventoryStacksByType": {"*-test-goblin": [{"tabs": ["general", "lotrnpcs"],
                                                         "stacks": [{"type": "item", "code": f"lotrnpcs:creature-{c}"} for c in CREATIVE_ORDER]}]},
    "materialDensity": 600,
    # The portrait mesh is a bust centred in the 1x1x1 item space. The angles are Fantasy Creatures' (whose humanoid
    # spawn items face the same way as ours), turned 25 degrees for a three-quarter view.
    # 0.0.38's bust filled about 60% of the slot at scale 1; 0.0.39 crops it to the head and shoulders and scales up.
    "guiTransform": {"rotation": {"x": 0, "y": -65, "z": -180}, "origin": {"x": 0.5, "y": 0.5, "z": 0.5}, "scale": 1.5},
    "fpHandTransform": {"rotation": {"x": 0, "y": -90, "z": 0}, "origin": {"x": 0.5, "y": 0.5, "z": 0.5}, "scale": 0.6},
    "groundTransform": {"rotation": {"x": 0, "y": -90, "z": 0}, "origin": {"x": 0.5, "y": 0, "z": 0.5}, "scale": 0.6},
})

# How the game names an NPC in death messages and the damage log, such as "killed by a Mordor orc captain".
# Without a "prefixandcreature-" entry it says "a wild animal".
PEOPLE = {"Goblin": "goblin", "Hobbit": "hobbit"}


def prefix_and_creature(name):
    who = PEOPLE.get(name, name)
    return ("an " if who[0] in "AEIOUaeiou" else "a ") + who


# Talk menu lines (user, 2026-11-02: hire, and "a brief random dialogue when they ask a question"). Original text.
# GuiDialogTalk picks a random numbered line of a kind for the NPC's faction, falling back to the general ones.
# In menu lines {0} is the price, {1} the coin's name, {2} the most recruits a player may lead, {3} the owner,
# {4} how many the player leads now, {5} the most that may follow at a time.
TALK = {
    "talk-opt-hire": "I'd like to hire you. ({0} × {1})",
    "talk-opt-hire-free": "I'd like to hire you.",
    "talk-opt-ask": "I have a question.",
    "talk-opt-follow": "Follow me.",
    "talk-opt-hold": "Hold your position here.",
    "talk-opt-guard": "Guard this area.",
    "talk-opt-guard-shared": "Guard this area for our people.",
    "talk-opt-return": "Go back on watch here.",
    "talk-opt-borrow": "Come with me.",
    "talk-opt-looks": "Let me change how you look.",
    "talk-opt-dismiss": "I don't need you any more.",
    "talk-opt-dismiss-yes": "Yes, you're free to go. (The coins aren't refunded.)",
    "talk-opt-dismiss-no": "On second thought, stay.",
    "talk-opt-bye": "Goodbye.",
    "talk-greet-1": "Well met.",
    "talk-stranger-1": "I don't know you. Move along.",
    "talk-stranger-2": "We don't take orders from strangers.",
    "talk-own-1": "Your orders?",
    "talk-shared-1": "I keep watch here for {3}'s people. Do you need me?",
    "talk-shared-2": "{3} said any of their people may call on me.",
    "talk-own-2": "I'm listening.",
    "talk-others-1": "I serve {3}. Take it up with them.",
    "talk-others-2": "{3} pays me, not you.",
    "talk-stay-1": "As you wish.",
    # The appearance dialog (0.0.54).
    "talk-looks-done-1": "Very well. I'll wear it proudly.",
    "talk-looks-done-2": "Do I look the part now?",
    "talk-looks-done-3": "If it pleases you, it pleases me.",
    "talk-looks-cancel-1": "Back as I was, then.",
    "talk-looks-cancel-2": "No changes? Fine by me.",
    "looks-title": "How {0} looks",
    "looks-name": "Name",
    "looks-rename": "Rename",
    "looks-helmet": "Helmet",
    "looks-toggle": "Switch",
    "looks-on": "On",
    "looks-off": "Off",
    "looks-weapon": "Weapon",
    "looks-weapon-none": "None",
    "looks-variant": "{0} ({1} of {2})",
    "looks-done": "Done",
    "looks-cancel": "Cancel",
    "looks-part-baseskin": "Skin",
    "looks-part-face": "Face",
    "looks-part-eyecolor": "Eyes",
    "looks-part-facialexpression": "Expression",
    "looks-part-ears": "Ears",
    "looks-part-hairbase": "Hair",
    "looks-part-hairextra": "More hair",
    "looks-part-haircolor": "Hair color",
    "looks-part-brows": "Brows",
    "looks-part-mustache": "Mustache",
    "looks-part-beard": "Beard",
    "looks-notyours": "Only the one who hired this NPC can change how it looks.",
    "looks-badname": "A name needs 1 to 24 letters, and may also use spaces, apostrophes and hyphens.",
    "looks-badpart": "That look isn't available for this NPC.",
    "looks-badweapon": "This NPC can't carry that weapon.",
    "looks-nohelmet": "This NPC has no helmet.",
    "looks-badhelmet": "This NPC can't wear that helmet.",
    "looks-helmet-none": "No helmet",
    "looks-shield": "Shield",
    "looks-badshield": "This NPC has no shield.",
    "looks-shieldtwohanded": "The poleaxe takes both hands, so the shield can't go with it.",
    "talk-stay-2": "Good. I'd rather stay.",
    "talk-dismiss-ask": "You're sending me away? The coin you paid stays with me.",
    "talk-hired": "Your coin is good. I'm yours to command.",
    "talk-hired-guard": "Your coin is good. Your band is full, so I'll keep watch here until you call for me.",
    "talk-followfull": "Your band is full. {6} Send someone off first.",
    "talk-full": "You lead as many recruits as anyone can keep in line. {7}",
    "talk-captains-full": "Your band already has as many captains as it can keep. Another would only quarrel with them.",
    "talk-poor": "That's not enough. I want {0} × {1}.",
    "talk-nocoin": "I'd want {0} × {1}, but nobody here has seen such a coin. (An admin can change HireCoin in lotrnpcs-factions.json.)",
    "talk-follow": "Lead the way.",
    "talk-hold": "I'll hold here.",
    "talk-guard": "I'll keep watch over this place.",
    "talk-guard-shared": "I'll keep watch here. Any of your people may call on me.",
    "talk-borrowed": "{3} trusts you, so I'll come. Lead the way.",
    "talk-dismissed": "Farewell, then.",
    # Hired NPCs go by rank, then name (user, 2026-11-03: call hired NPCs by their rank, then
    # their name). {3} here (and {1}/{2} below where noted) is "warrior Gorbag"; Shire hobbits
    # are farmers. Rankless NPCs go by their name alone.
    "title-recruit": "Recruit",
    "title-warrior": "Warrior",
    "title-captain": "Captain",
    "title-archer": "Archer",
    # 0.0.113 promotion-only branches. title-<faction>-guard and title-<faction>-shock name a faction's own.
    "title-guard": "Guard",
    "title-shock": "Shock trooper",
    # 0.0.117: mounted riders.
    "title-rider": "Rider",
    "title-shire-recruit": "Farmer",
    "recruit-yours": "Your {3}: {0} in {2} formation, {1} stance",
    "recruit-yours-guard": "Your {1}: guarding their post, {0} stance",
    "recruit-of": "{1}, in the service of {0}",
    "recruit-borrowed": "Following {0} for now",
    "recruit-shared": "Guarding for its owner's group",
    "recruit-yours-borrowed": "Your {1}: lent to {0}",
    "recruit-yours-guard-shared": "Your {1}: guarding their post for your group, {0} stance",
    "order-follow": "following",
    "order-hold": "holding position",
    "order-guard": "guarding",
    # What the owner sees when looking at their recruit. The game draws the orders key next to help-orders, as it
    # is bound (R unless changed).
    "help-talk": "Talk",
    "help-orders": "Hold for orders",
    "recruit-died": "Your {0} has died.",
    # Promotion (0.0.76). {0} the recruit's title and name before, {1} after; in the menu {0} the rank, {1} the price, {2}
    # the coin. talk-promote-* use the talk menu's placeholders ({0} price, {1} coin).
    "recruit-xp": "XP: ({0}/{1})",
    "promote-ready": "Your {0} has earned enough experience to be promoted. Talk to them to promote them.",
    "promote-done": "Your {0} is now {1}.",
    "talk-opt-promote": "Promote to {0} ({1} × {2})",
    "talk-opt-promote-free": "Promote to {0}",
    "talk-promote-poor": "A promotion costs {0} × {1}, and you don't have that much.",
    "talk-promote-nocoin": "A promotion would cost {0} × {1}, but nobody here has seen such a coin. (An admin can change HireCoin in lotrnpcs-factions.json.)",
    # 0.0.113: the promotion screen (Roster.cs). {0} in promote-title is the recruit's title and name; promote-have {0}/{1}
    # XP, {2} coins of {3}.
    "talk-opt-promote-menu": "Promote...",
    "promote-unavailable": "That promotion isn't open to them right now.",
    "promote-title": "Promote {0}",
    "promote-have": "XP {0}/{1}  ·  You have {2} × {3}",
    "promote-health": "Health {0}",
    "promote-damage": "Damage {0}",
    "promote-arrows": "Arrows {0}",
    "promote-withshield": "{0}, always with a shield",
    "promote-leads": "Can become: {0}",
    "promote-last": "The last rank on this path",
    "promote-price": "{0} × {1}",
    "promote-free": "Free",
    "promote-poor": "Not enough coins",
    "promote-captains-full": "Captain places full",
    "promote-button": "Promote",
    "promote-keeps": "The name, looks and orders stay. The armor becomes the new rank's.",
    # 0.0.113: the roster screen. roster-summary {0} recruits, {1} following; roster-where {0} blocks, {1} compass point;
    # roster-away and roster-elsewhere {0} x, {1} z (from the map middle), {2} how long ago.
    "orders-roster": "Roster: all your recruits",
    "roster-title": "Your recruits",
    "roster-summary": "{0} recruits, {1} following you",
    "roster-captains": "Captain places used: {0}",
    "roster-filter-all": "All {0}",
    "roster-filter-following": "Following {0}",
    "roster-filter-guarding": "Guarding {0}",
    "roster-filter-ready": "Ready to promote {0}",
    "roster-filter-away": "Not loaded {0}",
    "roster-col-name": "Name",
    "roster-col-rank": "Rank",
    "roster-col-xp": "XP",
    "roster-col-order": "Order",
    "roster-col-stance": "Stance",
    "roster-col-where": "Where",
    "roster-none": "No recruits here.",
    "roster-borrowed": "Borrowed by {0}",
    "roster-unseen": "Not seen since the update",
    "roster-where": "{0} blocks {1}",
    "roster-away": "Not loaded: last seen {0} / {1}, {2}",
    "roster-elsewhere": "In another place: {0} / {1}",
    "roster-minutes": "{0} min ago",
    "roster-hours": "{0} h ago",
    "roster-days": "{0} days ago",
    "roster-map": "Map",
    "roster-call": "Call",
    "roster-promote": "Promote",
    "roster-dismiss": "Dismiss",
    "roster-dismiss-sure": "Sure?",
    "roster-page": "Page {0} of {1}",
    "roster-lastseen": "(last seen)",
    "roster-mapped": "{0} is marked on your map until you rejoin.",
    "roster-nomap": "The world map isn't available.",
    "roster-gone": "That recruit is no longer yours.",
    "roster-notloaded": "They're too far away to hear you (not loaded).",
    "roster-already": "Your {0} is already following you.",
    "roster-call-full": "Your band is full. Send someone off first.",
    "roster-called": "Your {0} is on the way.",
    "roster-dismissed": "Your {0} has been dismissed.",
    "roster-dismissed-later": "Dismissed. They'll leave your service when their area next loads.",
    "compass-n": "north", "compass-ne": "northeast", "compass-e": "east", "compass-se": "southeast",
    "compass-s": "south", "compass-sw": "southwest", "compass-w": "west", "compass-nw": "northwest",
    # The whitelist menu (0.0.76), opened by /recruits whitelist. {0} in whitelist-more is how many aren't shown.
    "whitelist-title": "Recruit whitelist",
    "whitelist-explain": "Your recruits never attack the players on this list on sight, even when guarding or on the raid stance. They still fight back if one of them hurts you or your recruits.",
    "whitelist-listed": "On your whitelist",
    "whitelist-none": "Nobody yet.",
    "whitelist-remove": "Remove",
    "whitelist-more": "And {0} more. Type /recruits whitelist list to see everyone.",
    "whitelist-add-label": "Add a player",
    "whitelist-add": "Add",
    "whitelist-online": "Or pick a player who is online now:",
    "whitelist-close": "Close",
    # Orders menu (0.0.38, hold R). {0} in orders-title is how many recruits hear the player. Since 0.0.58 the R
    # controls call hired NPCs "recruits" (user, 2026-11-03), and since 0.0.74 everything does (user, 2026-11-04: the
    # mod should not use the old word anywhere).
    "hotkey-orders": "LOTR NPCs: recruit orders (hold)",
    "orders-none": "No recruits of yours are near enough to hear you.",
    "orders-title": "Recruits near you: {0} ({1}, {2})",
    "orders-page-movement": "Movement",
    "orders-page-combat": "Combat",
    "orders-follow": "1. Follow me",
    "orders-hold": "2. Hold position",
    "orders-move": "3. Move there",
    "orders-guard": "4. Guard here",
    "orders-guard-shared": "5. Guard here for my group",
    "orders-formation": "Formation",
    # 0.0.136: combat-page orders after the four stances.
    "orders-retreat": "5. Retreat",
    "orders-charge": "6. Charge (what you look at)",
    "orders-hint": "Scroll or Tab: switch page. Let go of {0} or press Esc to close.",
    "orders-sharedkey": "The recruit orders key {0} is also bound to: {1}. Holding {0} opens the orders menu first, so that won't react to it any more; to keep both, give one of them another key in Settings > Controls.",
    "formation-loose": "Loose",
    "formation-line": "Line",
    "formation-column": "Column",
    "formation-wedge": "Wedge",
    "formation-square": "Square",
    "formation-circle": "Circle",
    "stance-passive": "Passive",
    "stance-defensive": "Defensive",
    "stance-aggressive": "Aggressive",
    "stance-raid": "Raid",
    "stance-desc-passive": "never fight, not even when struck",
    "stance-desc-defensive": "fight only what attacks you or them",
    "stance-desc-aggressive": "attack every enemy they see",
    "stance-desc-raid": "attack every player and creature that isn't allied",
    "cmd-none": "No recruits of yours are near enough to hear you.",
    "cmd-follow": "Recruits following you: {0}",
    "cmd-follow-full": "Recruits following you: {0}. {1} stayed behind because your band is full. {2}",
    "cmd-hold": "Recruits holding position: {0}",
    "cmd-move": "Recruits on the move: {0}",
    "promote-guards": "{0} is promoted, but your ranked places are full, so it guards where it stands. Call it from the roster when a place frees.",
    "roster-band": "Following: {0} of {1} ranked places, {2} of {3} recruit places.",
    "cmd-retreat": "Recruits falling back to you: {0}. They start no fights for {1} seconds.",
    "cmd-charge": "Recruits charging: {0}",
    "cmd-charge-none": "No enemy there to charge. Look at one (up to 48 blocks), or have one near you.",
    "cmd-nofollowers": "None of your followers can hear you. Tell them to follow you first.",
    # Guard here (0.0.44). {1} is GuardRadius from lotrnpcs-factions.json.
    "cmd-guard": "Recruits guarding this spot: {0}. They patrol {1} blocks around it, and on the aggressive or raid stance they attack any player there who isn't a friend of yours (see /recruits whitelist).",
    "cmd-guard-shared": "Recruits guarding this spot for your group: {0}. They patrol {1} blocks around it, and anyone in your groups may talk to them to borrow one.",
    "cmd-notarget": "Look at the ground where they should go.",
    "cmd-toofar": "That's too far away to send them.",
    "cmd-formation": "Formation: {0}",
    "cmd-stance": "Stance: {0} (recruits: {1})",
    # /recruits whitelist (0.0.44): players the owner's recruits never attack on sight. {0} is the player's name.
    # Chat reads text as VTML, so no angle brackets.
    "whitelist-unknown": "Nobody called {0} has played on this server.",
    "whitelist-self": "Your recruits never attack you anyway.",
    "whitelist-already": "{0} is already on your recruits' whitelist.",
    "whitelist-added": "{0} is on your recruits' whitelist now. They won't attack {0} on sight, but they still fight back if {0} hurts you or them.",
    "whitelist-notlisted": "{0} isn't on your recruits' whitelist.",
    "whitelist-removed": "{0} is off your recruits' whitelist.",
    "whitelist-empty": "Your recruits' whitelist is empty. Add a player with /recruits whitelist add [name].",
    "whitelist-list": "Your recruits never attack these players on sight: {0}",
    # /recruits dismissall (0.0.73, user: "make an option in the chat to fire all your recruits"). {0} counts recruits.
    "dismissall-none": "You have no recruits to dismiss.",
    "dismissall-ask": "You have {0} recruits. To dismiss all of them, type /recruits dismissall confirm. Their hire fees aren't refunded.",
    "dismissall-done": "You dismissed all {0} of your recruits. They go back to their own people where they stand.",
    "dismissall-done-later": "You dismissed all {0} of your recruits. {1} of them are far away and will be free the next time someone comes near them.",
    # 0.0.90 follow shares (owner: "Chat would explain it, like room for 1 more orc or 0 more men").
    "room-line": "Room in your band: {0}.",
    # 0.0.91 (owner: players "should be notified how many recruits they have left in their capacity").
    "roster-line": "Room on your roster: {0}.",
    # 0.0.92 join menu (owner: players pick their faction after making a character; once; then admins change it).
    "join-title": "Choose your faction",
    "join-explain": "Pick the people you fight for. Their NPCs treat you as one of their own, you can hire their recruits, and their enemies become yours. You choose once; after that only an admin can change it.",
    "join-followers": "{0} followers",
    "join-follower": "{0} follower",
    "join-total": "{0} total",
    "join-legend": "Followers: how many recruits can walk with you at once. Total: how many recruits you can have in all.",
    "join-bloc-free": "Free Peoples",
    "join-bloc-shadow": "Shadow",
    "join-bloc-other": "Others",
    "join-stance-aggressive": "aggressive",
    "join-stance-defensive": "defensive",
    "join-stance-neutral": "neutral",
    "join-confirm": "Join the {0}? This is permanent: only an admin can change it.",
    "join-yes": "Join",
    "join-back": "Back",
    "join-later": "Decide later",
    "join-none": "There are no factions to join. Ask an admin.",
    "join-done": "You joined the {0}. Their NPCs now treat you as one of their own.",
    "join-reminder": "You haven't joined a faction yet, so every faction's NPCs treat you as a stranger and aggressive ones attack you. Type /recruits join to choose your faction.",
    "join-already": "You are a member of the {0}. Only an admin can change your faction.",
    "join-closed": "The {0} can't be joined from this menu. Ask an admin.",
    "join-unknown": "That faction doesn't exist any more. Open the menu again with /recruits join.",
    "hired-room": "You hired {0}. {1} {2}",
    "limits-dismissed": "Your recruits were over the new limits, so {0} left your service: {1}.",
    "limits-away": "all of them far away",
    "limits-dismissed-later": "{0} more will leave when you next come near them.",
    "limits-returned": "Your band was over the new limits, so {0} borrowed guards went back to keep watch.",
}
# Per faction: greetings for members, what they say when hired, and questions with their answers.
FACTION_TALK = {
    "gondor": (["Well met. The White City needs every sword it can get.", "Hail, friend of Gondor. What brings you here?"],
               "My sword is yours, for Gondor's sake.",
               [("How goes the watch on the eastern border?", "Badly. Fires burn in the mountains at night, and the patrols come back smaller every season."),
                ("What do you think of the horsemen of Rohan?", "Proud, and quick to anger. Still, I'd rather have them beside me than across from me."),
                ("Is the city safe?", "Safer than the fields. The walls have held for an age. Pray they hold another.")]),
    "rohan": (["Hail. The plains are wide, but we see who crosses them.", "You're no stranger to the Mark. Speak."],
              "I'll ride with you, as long as you ride against our foes.",
              [("Where is your horse?", "Grazing where the orcs can't find it. A rider without a horse is half a rider."),
               ("Have you seen orcs on the plains?", "Tracks, mostly. They move by night and burn what they can't carry."),
               ("What worries you most?", "The west. Too many farms near the hills have gone quiet this year.")]),
    "dale": (["Welcome to the lands of Dale. Mind the market, it's busier than it looks.", "A friend of Dale is welcome at our fires."],
             "Pay well and I'll fight well. That's how we do things in Dale.",
             [("How is trade with the dwarves?", "Good, when they're not haggling over every copper. Their toys sell well in the south."),
              ("Is the lake safe?", "Safe enough for fishermen. My grandfather's stories say otherwise, but he liked a tale."),
              ("What news from the east?", "Fewer traders every year, and the ones who come keep looking over their shoulders.")]),
    "arnor": (["Few walk these old roads. Fewer still are friends.", "The North remembers its own. Welcome."],
              "I'll walk your road. I've walked worse.",
              [("Where is the kingdom of Arnor?", "Under your feet. Ruins and barrows now, but we still keep the watch."),
               ("Why do you live in the wilds?", "Someone has to guard the quiet places, and the quiet folk in them. It's thankless work."),
               ("What haunts the old barrows?", "Things that should have stayed buried. Don't sleep near the standing stones.")]),
    "haradrim": (["The sun is hot and my patience is short. But you are one of us. Speak.", "Peace, cousin of the south."],
                 "Gold buys my spear, and your gold is good.",
                 [("What is the desert like?", "Endless. Water is worth more than gold there, and a friend is worth more than water."),
                  ("Have you seen the great war beasts?", "I marched beside one once. The ground shook for a mile around it."),
                  ("Why come so far north?", "Because we were told to. And because the north has rich land for the taking.")]),
    "dunlendings": (["You've the look of the hills. Sit, if you like.", "The hill folk don't forget a friend. Or an enemy."],
                    "I'll fight. The horse-lords owe us blood anyway.",
                    [("Why do you hate the horsemen?", "They took our grazing lands long ago, and they call us wild for wanting them back."),
                     ("Where do your people live?", "In the hills and valleys, in houses of turf and stone. We were here first."),
                     ("Who leads you?", "Whoever the clans follow this year. Lately a voice from a tower has been making promises.")]),
    "blacknumenoreans": (["You stand before the old blood of the West. Be brief.", "A servant of the true lords. Good. Speak."],
                         "My blade and my counsel, at a price. You have paid it.",
                         [("Where do your people come from?", "From the drowned island, long ago. We remember what the others chose to forget."),
                          ("Why do you serve the dark lord?", "Power is power. The kings of the West grew weak, so we chose the stronger master."),
                          ("What do you think of Gondor?", "Our cousins, grown soft and proud. One day they will kneel.")]),
    "easterlings": (["Kin of the east. The road was long, but we're here now.", "Welcome, brother. Share the fire."],
                    "I'll follow you. The east will remember your name.",
                    [("Why did you come west?", "For land, for plunder, for the lord who calls us. Mostly for land."),
                     ("What is your homeland like?", "Wide grass and cold winds. Horse clans and wagon towns. Nothing like these crowded hills."),
                     ("Do you trust the orcs?", "Nobody trusts orcs. We march beside them because we're told to.")]),
    "dwarves": (["Hail, friend of the mountain halls. Mind your step, the floor's new.", "A friend of the dwarves! Come, have a drink."],
                "Right then. My axe goes where you go, so long as the pay holds.",
                [("What do you dig for?", "Iron, gold, gems. And the old halls we lost when the deep things woke."),
                 ("Do you get along with elves?", "We trade. We don't share a table. Old grudges run as deep as mines."),
                 ("What's the best ale?", "Whatever's in my mug. Second best is whatever's in yours.")]),
    "wickeddwarves": (["You've a strong arm and a weak conscience. Good. Speak.", "Hah! Another one who knows where the real gold is."],
                      "Coin first, questions never. Lead on.",
                      [("Why did you leave your kin?", "They hoard for the sake of hoarding. We take what we need, from whoever has it."),
                       ("Who do you serve?", "Whoever pays in gold, not promises."),
                       ("Do you miss the great halls?", "Every day. That's why we'll take them back, from whoever sits in them.")]),
    "shire": (["Oh! Good day to you. Mind the garden.", "Well met, neighbour. Lovely weather for it."],
              "Well, I suppose a bit of adventure won't kill me. Probably.",
              [("Where's the best inn around here?", "Depends what you're after: good beer, good gossip or a quiet corner. Every village claims the first."),
               ("Have you seen anything strange lately?", "More big folk on the roads than usual. And riders in dark cloaks asking questions, so they say."),
               ("What do hobbits do all day?", "Garden, eat, visit, eat again. It's a very busy life, thank you.")]),
    "elves": (["Well met, friend. The stars are bright tonight.", "You walk among the elves. Be welcome."],
              "I will go with you a while. Our roads lie together, for now.",
              [("How old are you?", "Older than the oaks in this wood, and younger than the hills. Why do mortals always ask that?"),
               ("Will you sail west one day?", "One day. Not yet. There is still beauty here worth guarding."),
               ("What do your songs tell of?", "Of the light that was, and the friends we have lost. Our songs are long, and mostly sad.")]),
    "tormentedelves": (["You... you see clearly. Not like the others. Speak.", "The dark has a voice. You hear it too, don't you?"],
                       "Yes. I'll follow. Better to serve you than the voice in the dark.",
                       [("What happened to you?", "Taken, long ago. Broken, then mended wrong. I don't remember the light any more."),
                        ("Do you remember your kin?", "Faces in water. When I reach for them, they're gone."),
                        ("Why do you fight?", "Because the pain stops while I fight. Only then.")]),
    "mordor": (["What do you want? Make it quick, the boss is watching.", "Another one sent by the Eye. Fine. Talk."],
               "Fine. Point me at something soft and I'll stick it.",
               [("Who do you serve?", "The Eye. Everyone serves the Eye sooner or later. Better to do it willingly."),
                ("What do you eat?", "Whatever isn't fast enough. Don't ask what's in the stew."),
                ("Do you like the Isengard lot?", "The white-hand boys? Too proud by half. They think they're better than us because the sun doesn't bother them.")]),
    "angmar": (["Cold up here, eh? You get used to it. Or you die.", "The iron north has room for you. Speak."],
               "I'll come. Better than freezing in these ruins.",
               [("Who rules here?", "A king in iron, cold as the hills. Don't say his name too loud."),
                ("What happened to the old kingdoms here?", "We happened. Now they're barrows and broken towers, and the dead don't rest."),
                ("Why is it always so cold?", "The wind comes off the northern ice. And something else. It keeps the men away.")]),
    "mistymountains": (["Hss... a friend, is it? Don't get lost in the tunnels.", "You're not dinner, then. Pity. What do you want?"],
                       "Gold and meat, and I'll come. More meat than gold.",
                       [("Where do you live?", "Under the mountains. Miles of tunnels, all dark. We like it dark."),
                        ("Who is your king?", "The big one in the great cave. He's fat, but he's mean."),
                        ("Do you go out in daylight?", "Not if we can help it. The sun burns. Night's for hunting.")]),
    "gundabad": (["You walk the north road and live. Speak, before that changes.", "Gundabad remembers its friends. Not many of them."],
                 "Bones and blood, and I'm yours. Point me at something.",
                 [("Why the bone armor?", "Iron rusts in the snow. Bone doesn't, and it shows what we've done to the ones who wore it."),
                  ("What is Gundabad?", "The mountain in the far north. The dwarves call it theirs. They're wrong."),
                  ("Do you fear the cold?", "The cold fears us. We were born in it.")]),
    "isengard": (["State your business. Isengard doesn't wait.", "Uruk-hai of the White Hand. What do you want?"],
                 "Good. I'll march day or night. Uruks don't tire.",
                 [("Who made you?", "The wizard, in the pits under his tower. We're better than the old orcs. Bigger, stronger."),
                  ("What does the white hand mean?", "That we belong to him. Wear it proud."),
                  ("Why cut down the forest?", "Fuel for the forges. Trees don't fight back. Mostly.")]),
}
# Strangers the Shire won't serve, but won't fight either (the Shire is the only defensive faction by default).
FACTION_STRANGER = {
    "shire": ["I don't know you, and I'll thank you to keep off my lawn.", "Big folk. Hmph. Mind where you put your boots.",
              "Good day. I've nothing to say to strangers, I'm afraid."],
}
for faction, (greets, hired, questions) in FACTION_TALK.items():
    for i, line in enumerate(greets, 1):
        TALK[f"talk-greet-{faction}-{i}"] = line
    TALK[f"talk-hired-{faction}"] = hired
    for i, (question, answer) in enumerate(questions, 1):
        TALK[f"talk-ask-{faction}-{i}"] = question
        TALK[f"talk-answer-{faction}-{i}"] = answer
for faction, lines in FACTION_STRANGER.items():
    for i, line in enumerate(lines, 1):
        TALK[f"talk-stranger-{faction}-{i}"] = line
assert set(FACTION_TALK) == set(FACTION_OF.values()), "talk lines for every faction"
# 0.0.90: each faction's people for the room line ("Room in your band: Mordor orcs 1, Men of Gondor 0.").
FOLK = {
    "gondor": "Men of Gondor", "rohan": "Riders of Rohan", "dale": "Men of Dale", "arnor": "Dunedain of Arnor",
    "haradrim": "Haradrim", "easterlings": "Easterlings", "blacknumenoreans": "Black Numenoreans", "dunlendings": "Dunlendings",
    "isengard": "Uruk-hai", "mordor": "Mordor orcs", "angmar": "Angmar orcs", "gundabad": "Gundabad orcs",
    "mistymountains": "Goblins of the Misty Mountains", "elves": "Elves", "tormentedelves": "Tormented elves",
    "dwarves": "Dwarves", "wickeddwarves": "Wicked dwarves", "shire": "Hobbits",
}
assert set(FOLK) == set(FACTION_OF.values()), "a folk name for every faction"
for faction, folk in FOLK.items():
    TALK[f"folk-{faction}"] = folk

# The creative tab's name is looked up in the game domain (user, 2026-11-03: the tab read "tabname-lotrnpcs").
lang = {"game:tabname-lotrnpcs": "LOTR NPCs", "tabname-lotrnpcs": "LOTR NPCs"}
for code, name, *_ in TEST_NPCS:
    lang[f"item-creature-{code}"] = name
    lang[f"entity-{code}"] = name
    lang[f"prefixandcreature-{code}"] = prefix_and_creature(name)
for weapon, w in {**{k: w for k, w in ALL_WEAPONS.items() if "item" not in w}, **SHIELDS, "bow": BOW, "bow-drawn": BOW_DRAWN, **BOW_PULL_ITEMS, "mmbow": MM_BOW, "mmbow-drawn": MM_BOW_DRAWN, **MM_BOW_PULL_ITEMS}.items():
    lang[f"item-orcweapon-{weapon}"] = w["name"]
lang["entity-npcarrow"] = "Arrow"
lang.update(TALK)
write("lang/en.json", lang)

with open(os.path.join(out, "modinfo.json"), "w") as f:
    json.dump({
        "type": "code",
        "modid": "lotrnpcs",
        "name": "LOTR NPCs",
        "version": "0.0.139",
        "description": "Recruits, warriors and captains of every Middle-earth faction. They spawn in admin zones (/recruits zone), "
                       "fight by faction, and can be hired and commanded (talk to them; hold R for orders). Race models from "
                       "LOTR Races and Classes by Dango Dango Dango, Shakgriig and Buggoi, used with their permission.",
        "authors": ["Emperorcharizar"],
        "contributors": ["Dango Dango Dango", "Shakgriig", "Buggoi"],
        "dependencies": {"playermodellib": "1.23.8"},
        "side": "Universal",
        "requiredOnClient": True,
        "requiredOnServer": True,
    }, f, indent=2)
    f.write("\n")
