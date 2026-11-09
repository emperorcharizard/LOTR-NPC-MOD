from reportlab.lib.pagesizes import letter
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, KeepTogether
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib import colors
from reportlab.lib.units import inch
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

F = "/usr/share/fonts/truetype/dejavu/"
pdfmetrics.registerFont(TTFont("Sans", F + "DejaVuSans.ttf"))
pdfmetrics.registerFont(TTFont("Sans-Bold", F + "DejaVuSans-Bold.ttf"))
pdfmetrics.registerFont(TTFont("Sans-Oblique", F + "DejaVuSans-Oblique.ttf"))
pdfmetrics.registerFont(TTFont("Mono", F + "DejaVuSansMono.ttf"))
from reportlab.lib.fonts import addMapping
addMapping("Sans", 0, 0, "Sans"); addMapping("Sans", 1, 0, "Sans-Bold"); addMapping("Sans", 0, 1, "Sans-Oblique"); addMapping("Sans", 1, 1, "Sans-Bold")

INK = colors.HexColor("#1f2328"); MUTED = colors.HexColor("#57606a"); ACCENT = colors.HexColor("#7a4b1e")
RULE = colors.HexColor("#d0d7de"); HEAD = colors.HexColor("#f3ede4"); ZEBRA = colors.HexColor("#faf8f5")

base = ParagraphStyle("base", fontName="Sans", fontSize=9.5, leading=13.2, textColor=INK)
body = ParagraphStyle("body", parent=base, spaceAfter=6)
small = ParagraphStyle("small", parent=base, fontSize=8.3, leading=11)
cellb = ParagraphStyle("cellb", parent=small, fontName="Sans-Bold")
title = ParagraphStyle("title", parent=base, fontName="Sans-Bold", fontSize=22, leading=27, textColor=INK, spaceAfter=4)
sub = ParagraphStyle("sub", parent=base, fontSize=10.5, textColor=MUTED, spaceAfter=14)
h1 = ParagraphStyle("h1", parent=base, fontName="Sans-Bold", fontSize=15, leading=19, textColor=ACCENT, spaceBefore=14, spaceAfter=6)
h2 = ParagraphStyle("h2", parent=base, fontName="Sans-Bold", fontSize=11.5, leading=15, textColor=INK, spaceBefore=10, spaceAfter=4)
note = ParagraphStyle("note", parent=base, fontSize=9, leading=12.5, textColor=MUTED, spaceAfter=6)

def C(t): return "<font name='Mono' size='8.3'>" + t + "</font>"
def P(t, s=body): return Paragraph(t, s)

def table(rows, widths, header=True):
    data = []
    for i, r in enumerate(rows):
        data.append([Paragraph(c, cellb if (header and i == 0) else small) for c in r])
    t = Table(data, colWidths=widths, repeatRows=1 if header else 0)
    st = [("GRID", (0, 0), (-1, -1), 0.4, RULE), ("VALIGN", (0, 0), (-1, -1), "TOP"),
          ("LEFTPADDING", (0, 0), (-1, -1), 5), ("RIGHTPADDING", (0, 0), (-1, -1), 5),
          ("TOPPADDING", (0, 0), (-1, -1), 3.5), ("BOTTOMPADDING", (0, 0), (-1, -1), 3.5)]
    if header: st.append(("BACKGROUND", (0, 0), (-1, 0), HEAD))
    for i in range(1 if header else 0, len(rows)):
        if i % 2 == 0: st.append(("BACKGROUND", (0, i), (-1, i), ZEBRA))
    t.setStyle(TableStyle(st))
    return t

W = 7.0 * inch
s = []
s.append(P("LOTR NPCs: config and commands", title))
s.append(P("For version 0.0.95 · faction config version 23 · November 2026", sub))
s.append(P("LOTR NPCs adds recruits, warriors, captains and archers of every Middle-earth faction to Vintage Story. They spawn in zones an admin marks, fight by faction, and can be hired and commanded. This guide covers every chat command, the in-game menus, and both config files."))

s.append(P("Quick start for admins", h1))
for i, t in enumerate([
    "Install the mod and <b>Player Model Lib</b> (1.23.8 or newer) on the server and every client. Add Coinage for the hire coin, and the armor and weapon mods for the full looks.",
    "Start the server once. It writes " + C("ModConfig/lotrnpcs-factions.json") + " and " + C("ModConfig/lotrnpcs-spawning.json") + " with their defaults.",
    "Players pick their own faction from a menu after making their character. Use " + C("/recruits faction set") + " to move anyone, and " + C("Joinable") + " to hide a faction from the menu.",
    "Mark where NPCs spawn with " + C("/recruits zone add") + ". Nothing spawns outside zones.",
    "After editing the faction file, run " + C("/recruits faction reload") + ". After editing the spawning file, restart the server.",
], 1):
    s.append(P(f"<b>{i}.</b> {t}", ParagraphStyle("li", parent=body, leftIndent=12, firstLineIndent=-12, spaceAfter=3)))

s.append(P("Chat commands", h1))
s.append(P("Everything is under one command, " + C("/recruits") + ". Type " + C("/help recruits") + " for the list, or " + C("/help recruits &lt;command&gt;") + " for one of them."))
s.append(P("For every player", h2))
s.append(table([
    ["Command", "What it does"],
    [C("/recruits join"), "Opens the menu to choose your faction. You choose once; after that it says your faction, and only an admin can change it."],
    [C("/recruits orders"), "Opens the recruit orders menu (the same menu as holding the orders key, R by default) until you press Escape."],
    [C("/recruits dismissall"), "Shows how many recruits you have. Add " + C("confirm") + " to dismiss them all (" + C("/recruits dismissall confirm") + "); hire fees aren't refunded. Recruits far away go when their area next loads. Alias: " + C("fireall") + "."],
    [C("/recruits whitelist"), "Opens the whitelist menu: players your recruits never attack on sight, even on Guard here or the raid stance. They still fight back if hurt."],
    [C("/recruits whitelist add &lt;player&gt;"), "Puts a player on your whitelist."],
    [C("/recruits whitelist remove &lt;player&gt;"), "Takes a player off it."],
    [C("/recruits whitelist list"), "Lists the players on it."],
], [2.3 * inch, W - 2.3 * inch]))
s.append(P("For admins (server control privilege)", h2))
s.append(table([
    ["Command", "What it does"],
    [C("/recruits faction list"), "Lists every faction: stance, whether it is in the join menu, follow limit, roster, per-rank health and damage, the alliances, and the hit spread."],
    [C("/recruits faction check [player]"), "Shows a player's faction and which factions treat them as a friend or attack them on sight. Without a name, checks you."],
    [C("/recruits faction set &lt;player&gt; &lt;faction&gt;"), "Moves a player into a faction (replacing the one they picked)."],
    [C("/recruits faction clear &lt;player&gt;"), "Takes a player out of their faction, so they can pick again from the menu."],
    [C("/recruits faction members [faction]"), "Lists the members of a faction, or of every faction, picked or set by an admin."],
    [C("/recruits faction reload"), "Reloads the faction file without a restart. Loaded NPCs take their new health at once, damage changes on the next hit, and online players over new limits lose their newest recruits."],
    [C("/recruits zone add &lt;x z&gt; &lt;x z&gt; &lt;factions&gt;"), "Adds a spawn zone between two corners (as shown on screen) for one or more factions. Example: " + C("/recruits zone add 1200 -400 1500 -100 mordor angmar") + "."],
    [C("/recruits zone list"), "Lists every zone with its number and factions."],
    [C("/recruits zone here"), "Lists the zones you are standing in."],
    [C("/recruits zone remove &lt;zone&gt;"), "Removes a zone by its number."],
    [C("/recruits zone addfaction &lt;zone&gt; &lt;faction&gt;"), "Adds a faction to a zone."],
    [C("/recruits zone removefaction &lt;zone&gt; &lt;faction&gt;"), "Removes a faction from a zone."],
    [C("/recruits zone spawnnow"), "Spawns a group near you right away, as the zone timer would (for testing). The server log says where."],
], [2.6 * inch, W - 2.6 * inch]))
s.append(P("Faction ids: gondor, rohan, dale, arnor, haradrim, dunlendings, blacknumenoreans, easterlings, dwarves, wickeddwarves, shire, elves, tormentedelves, mordor, angmar, mistymountains, gundabad, isengard.", note))

s.append(P("Menus in game", h1))
s.append(P("<b>Join menu.</b> Opens right after a new player makes their character, and on every join until they have a faction. It lists every faction with " + C("Joinable") + " on, as a card with its portrait under Free Peoples, Shadow (the wicked dwarves too, since 0.0.95) or Others, saying how many follow at once (followers) and how many it can have in all (total). Picking asks for a confirmation because it is permanent; after that only an admin changes it. Closing it without a pick puts a reminder in the player's chat to type " + C("/recruits join") + ". Until they pick they belong to no faction: aggressive factions attack them and they can't hire."))
s.append(P("<b>Talk menu.</b> Right-click an NPC. A member of its faction (or of an ally with " + C("ShareFriends") + ") can hire it for the price shown. A grey line says what still fits: \"Room in your band: Mordor orcs 1, Men of Gondor 0. Room on your roster: Mordor orcs 12.\" Your own recruits' menu has Follow, Hold, Guard this area, Guard this area for our people (group mates may borrow the guard), Looks (rename, helmet, weapon, shield, face), Promote when it has enough XP, and Dismiss."))
s.append(P("<b>Orders menu.</b> Hold R (rebindable) or type " + C("/recruits orders") + ". Orders: Follow me, Hold, Move there, Guard here, and Guard here for my group. Formations: loose, line, column, wedge, square and circle. Stances: passive (never fights), defensive (only fights what attacks you or your recruits), aggressive (the default: attacks every enemy on sight) and raid (attacks every player and creature that isn't allied)."))
s.append(P("<b>Shares.</b> Each recruit uses a share of your band: 1/" + C("MaxFollowers") + " of your following and 1/" + C("MaxRecruits") + " of your roster, for its own faction. The shares can't add up to more than 1, so factions mix: 3 Gondor men and 2 Mordor orcs use 3/5 + 2/7 = 0.89 of the following. A hire past the following keeps watch where it was hired."))

s.append(PageBreak())
s.append(P("The config files", h1))
s.append(P("Both files are in the server's " + C("ModConfig") + " folder and are created on first start. Your values are kept on updates and older files are upgraded once. Each starts with an " + C("#Instructions") + " list that is rewritten on every start, so edit the settings below it. Don't change " + C("Version") + "."))
s.append(table([
    ["File", "Controls", "After editing"],
    [C("lotrnpcs-factions.json"), "Who fights whom, membership, hiring, prices, limits, health and damage, healing, guards, helmets, shields, battle spacing", C("/recruits faction reload")],
    [C("lotrnpcs-spawning.json"), "Natural spawning inside zones, group sizes, despawning", "Restart the server"],
], [1.9 * inch, 3.6 * inch, 1.5 * inch]))

s.append(P("lotrnpcs-factions.json: per faction (under Factions)", h2))
s.append(table([
    ["Setting", "Default", "What it does"],
    [C("Stance"), C("aggressive") + " (Shire: " + C("defensive") + ")", "Aggressive attacks every non-member player on sight; defensive only fights players who hurt it or its allies. Both attack enemy NPCs and monsters on sight."],
    [C("Joinable"), "true", "false leaves the faction out of the join menu. Admins can still set players to it."],
    [C("FriendlyRaces"), "Per faction", "Only with " + C("RaceAndGroupMembership") + " on: Player Model Lib race codes whose players are members."],
    [C("FriendlyGroups"), "empty", "Only with " + C("RaceAndGroupMembership") + " on: vanilla /group names whose members are members."],
    [C("HireCoin"), C("game:gear-rusty"), "The item paid to hire and promote: the game's rusty gear, for now (was the Coinage planchet)."],
    [C("HirePrices"), "By tier (below)", "Coins per rank: recruit, warrior, captain, archer. Rankless NPCs cost a recruit's price. 0 makes hiring free."],
    [C("PromotePrices"), "Hire price less 3 (captain less 4)", "Coins to promote, by the rank it becomes: warrior, archer, captain. A rank left out gets the default. 0 makes it free."],
    [C("MaxFollowers"), "By tier (Men 5)", "How many of this faction's recruits can follow a player at once (1 to 32). Also sets its wild group size."],
    [C("MaxRecruits"), "By tier (Men 16)", "How many of this faction's recruits a player can have (1 to 256)."],
    [C("Health"), "By tier, per rank", "Health of the faction's recruits, warriors, captains and archers, a whole number each. A rank left out keeps the unit's own."],
    [C("Damage"), "By tier, per rank", "Damage of one hit per rank, melee and arrows. The damage tier doesn't change."],
], [1.35 * inch, 1.65 * inch, W - 3.0 * inch]))

s.append(P("Faction balance (shipped defaults)", h2))
s.append(P("Factions are \"unequally equal\": orcs are weaker and cheaper but more of them follow, elves and dwarves are stronger but fewer. At each rank, a full band of each faction wins about half its fights averaged over every other faction (the Uruk-hai a little more), in the mod's fight simulator."))
s.append(table([
    ["Tier", "Factions", "Follow / roster", "Hire R/W/C/A", "Promote W/A/C"],
    ["Men", "gondor, rohan, dale, arnor, haradrim, easterlings, blacknumenoreans, shire", "5 / 16", "5/15/40/15", "12/12/36"],
    ["Uruk-hai", "isengard", "5 / 15", "6/17/44/17", "14/14/40"],
    ["Dunlendings", "dunlendings", "6 / 19", "4/13/33/13", "10/10/29"],
    ["Orcs", "mordor, angmar, gundabad", "7 / 22", "4/11/29/11", "8/8/25"],
    ["Goblins", "mistymountains", "8 / 26", "3/9/25/9", "6/6/21"],
    ["Elves", "elves, tormentedelves", "4 / 13", "6/19/50/19", "16/16/46"],
    ["Dwarves", "dwarves, wickeddwarves", "4 / 13", "6/19/50/19", "16/16/46"],
], [0.95 * inch, 2.65 * inch, 1.05 * inch, 1.15 * inch, 1.2 * inch]))
s.append(Spacer(1, 6))
s.append(table([
    ["Health / damage", "Recruit", "Warrior", "Captain", "Archer"],
    ["Men", "22 / 3.05", "34 / 4", "48 / 7.05", "34 / 3"],
    ["Uruk-hai", "23 / 2.95", "32 / 4.3", "48 / 7.15", "35 / 2.95"],
    ["Dunlendings", "18 / 2.7", "29 / 3.4", "41 / 6", "28 / 2.6"],
    ["Orcs", "15 / 2.5", "27 / 2.8", "31 / 5.95", "22 / 2.45"],
    ["Goblins", "16 / 1.85", "21 / 2.85", "31 / 4.75", "19 / 2.2"],
    ["Elves", "27 / 3.65", "37 / 5.4", "53 / 9.6", "37 / 4.25"],
    ["Dwarves", "30 / 3.25", "46 / 4.35", "63 / 7.9", "46 / 3.4"],
], [1.4 * inch, 1.4 * inch, 1.4 * inch, 1.4 * inch, 1.4 * inch]))
s.append(P("An archer's damage is its arrow's (and its shove). Every hit also varies by " + C("DamageSpread") + " (below).", note))

s.append(P("Relations between factions (under Relations)", h2))
s.append(P("Each entry has " + C("A") + ", " + C("B") + ", " + C("Relation") + " and " + C("ShareFriends") + ". Two factions without an entry are enemies. No factions are allies by default; the file ships one example entry (Gondor and Rohan as enemies) that changes nothing."))
s.append(table([
    ["Relation", "Effect"],
    [C("ally"), "Never fight, and come to help each other within " + C("CallForHelpRange") + "."],
    [C("neutral"), "Leave each other's NPCs alone."],
    [C("enemy"), "Attack each other's NPCs on sight."],
    [C("ShareFriends: true"), "On an alliance: each faction's members are also safe from the other's NPCs and can hire them. Without it, a player is safe only from their own factions and hires only from them."],
], [1.6 * inch, W - 1.6 * inch]))

s.append(P("Promotion", h2))
s.append(P("A hired recruit or warrior earns 1 XP for each NPC of this mod that dies within 10 seconds of its hit. At 15 XP a recruit can become a warrior or an archer; at 40 XP a warrior can become a captain. The owner promotes it from its talk menu, paying " + C("PromotePrices") + ": by default the hire price of the new rank less 3 coins, or less 4 for a captain (Men 12 to warrior or archer, 36 to captain). The owner and their /group see " + C("XP: (x/y)") + " on it. A promoted NPC keeps its name, face, orders, post and weapon."))

s.append(P("lotrnpcs-factions.json: everything else", h2))
s.append(table([
    ["Setting", "Default", "What it does"],
    [C("MaxRecruits") + " / " + C("MaxFollowers"), "16 / 5", "The baseline: the Men's values, and those of a faction without its own. " + C("MaxFollowers") + " also sets the wild group scale."],
    [C("RaceAndGroupMembership"), "false", "true brings back the rule from before 0.0.92: race (" + C("FriendlyRaces") + ") and /group (" + C("FriendlyGroups") + ") also make players members."],
    [C("DamageSpread"), "0.2 (0 to 0.9)", "Every NPC hit deals its damage times a random 80% to 120%, so fights aren't decided by whole numbers of hits. 0 turns it off."],
    [C("MonsterCodes"), "drifters, bowtorn, shivers, bells, locusts", "Creatures every faction attacks on sight. " + C("*") + " is a wildcard, e.g. " + C("game:drifter-*") + "."],
    [C("GrudgeSeconds"), "30", "How long NPCs keep fighting someone who hurt them or an ally."],
    [C("CallForHelpRange"), "16 blocks", "How far allies come to help."],
    [C("GuardRadius"), "16 blocks (4 to 64)", "How far around their post guards patrol and attack intruders."],
    [C("MaxAttackersPerTarget"), "0 = no limit", "How many NPCs may fight one NPC at a time. Players are never limited."],
    [C("CombatSpacing"), "1.5 blocks (0.8 to 4)", "How far apart fighting NPCs of one side keep. As many as fit round an enemy this far apart (5) fight it; the rest wait in a second ring and step in."],
    [C("DuelDistanceMin") + " / " + C("Max"), "1.0 / 1.6 blocks", "How far from its enemy an attacker stands while fighting."],
    [C("BattleLines"), "false", "true makes groups line up when they meet another faction's NPCs (wild NPCs, and followers in any formation but Loose)."],
    [C("LineHoldRange"), "6 blocks (2 to 24)", "With battle lines, defensive recruits hold their line until an enemy is this close."],
    [C("HelmetChance"), "0.6", "Chance a new NPC that may go bare-headed wears its helmet. Helmets are only for looks."],
    [C("CaptainsAlwaysHelmeted"), "false", "true makes every captain always wear its helmet."],
    [C("ShieldChance"), "0.5", "Chance a new NPC that can carry a shield gets one (never with a two-handed weapon)."],
    [C("HealthRegenSpeed"), "30", "Out-of-combat healing as a multiple of vanilla creature healing (30 = 0.3 health a second)."],
    [C("RegenDelaySeconds"), "10", "Seconds after its last hit before an NPC heals at that speed."],
], [1.75 * inch, 1.45 * inch, W - 3.2 * inch]))

s.append(P("lotrnpcs-spawning.json", h2))
s.append(P("Natural spawning only happens inside zones marked with " + C("/recruits zone") + ". Each player standing in a zone gets their own spawn timer."))
s.append(table([
    ["Setting", "Default", "What it does"],
    [C("MinutesBetweenGroupsMin") + " / " + C("Max"), "3 / 5", "Every 3 to 5 minutes, each player in a zone gets a roll for a group."],
    [C("GroupChance"), "0.5", "The chance each roll spawns a group (1 = every time)."],
    [C("GroupSizeAvg") + " / " + C("Var") + " / " + C("Max"), "3 / 2 / 5", "Group size for a faction with the baseline " + C("MaxFollowers") + " (5). Others scale: Dunlendings up to 6, orcs 7, goblins 8, elves and dwarves 4."],
    [C("WarriorShare"), "0.35", "Chance each member after the first (always a recruit) is a warrior."],
    [C("ArcherShare"), "0.2", "Chance each member after the first is an archer (every faction but the Shire)."],
    [C("CaptainGroupChance"), "0.12", "Chance a group is a captain with an escort instead."],
    [C("CaptainEscortMin") + " / " + C("Max"), "1 / 3", "A captain's escort. The most scales like the group size."],
    [C("RivalGroupChance") + ", " + C("RivalDistanceMin") + " / " + C("Max"), "0.5, 16 / 32 blocks", "Where zones give ground to two factions at war, the chance a group brings an enemy group nearby so they fight. 0 turns it off."],
    [C("MinDistanceToPlayer") + " / " + C("Max"), "40 / 80 blocks", "How far from the player a group appears."],
    [C("MaxNearby") + " / " + C("NearbyRange"), "24 / 128 blocks", "No group spawns while zone NPCs worth 24 are within 128 blocks of the player (was 8 before 0.0.94). Each counts by faction: an orc 5/7, a goblin 5/8, an elf or dwarf 5/4, a man 1."],
    [C("MaxBlockLight"), "7 (0 to 32)", "No spawns where torches and lamps light the ground brighter than this."],
    [C("MaxLifetimeMinutes") + " / " + C("LifetimeDespawnDistance"), "20 minutes / 64 blocks", "A zone NPC alive longer than this disappears once it isn't fighting and no player is within 64 blocks. 0 turns it off."],
    [C("DespawnAfterMinutesAway") + " / " + C("DespawnDistance"), "5 minutes / 128 blocks", "Zone NPCs with no player within 128 blocks for 5 minutes disappear."],
], [2.2 * inch, 1.35 * inch, W - 3.55 * inch]))
s.append(P("Hired recruits and NPCs placed with creative spawn items never despawn. Zones themselves are saved with the world, not in this file.", note))

def footer(canvas, doc):
    canvas.saveState()
    canvas.setFont("Sans", 7.5); canvas.setFillColor(MUTED)
    canvas.drawString(0.75 * inch, 0.5 * inch, "LOTR NPCs 0.0.95 · config and commands")
    canvas.drawRightString(letter[0] - 0.75 * inch, 0.5 * inch, str(doc.page))
    canvas.restoreState()

import os, sys
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "lotrnpcs-config-and-commands.pdf")
doc = SimpleDocTemplate(out, pagesize=letter, leftMargin=0.75 * inch, rightMargin=0.75 * inch, topMargin=0.7 * inch, bottomMargin=0.75 * inch,
                        title="LOTR NPCs: config and commands", author="Emperorcharizar")
doc.build(s, onFirstPage=footer, onLaterPages=footer)
print(out)
