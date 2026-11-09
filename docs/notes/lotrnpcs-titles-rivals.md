---
name: lotrnpcs-titles-rivals
description: 0.0.56 rank-then-name titles for hired NPCs and rival enemy groups spawning near each other in shared zones
metadata:
  type: project
---

0.0.56 (user, 2026-11-03, untested in game): "Instead of referring to the npcs by 'hireling' refer to them as their rank, then name" -> Hireling.Title/TitleInSentence ("Warrior Gorbag", "your warrior Gorbag") in infotext, death message, talk and looks titles; lang title-<rank>, title-shire-recruit = "Farmer"; plural group commands still say "Hirelings". "For two factions that are in the same zone and hostile, or in an overlapping zone area, increase the spawn chance of enemy groups next to each other" -> spawning config RivalGroupChance 0.5, RivalDistanceMin/Max 16/32: a zone group on ground shared with a faction at war (FactionSystem.Relation Enemy) may bring an enemy group 16-32 blocks away (respects MaxNearby, MinDistanceToPlayer, zones).

Related: [[lotrnpcs-factions]], [[lotrnpcs-hiring]], [[lotrnpcs-archery]]
