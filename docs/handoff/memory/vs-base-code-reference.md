---
name: vs-base-code-reference
description: Where the Vintage Story base-code NPC reference doc lives and the key facts from mapping anegostudios repos (v1.22.7)
metadata:
  type: reference
  modified: 2026-10-30T17:32:44.695Z
---

Reference doc (Claude Doc): https://claude.ai/artifact/WacMjb2Cg1z4t9pCimnSDc — "Vintage Story NPC modding: base code reference", written 2026-10-30 from anegostudios/vsapi, vssurvivalmod, vsessentialsmod (1.22.7) and vscreativemod (1.22.0-rc.1). Repos are public; clone read-only with `git clone --depth 1 https://github.com/anegostudios/<repo>.git`.

Key facts:
- Repos are C# only; vanilla JSON assets (entities, dialogue, trade lists, outfits, activity collections) are NOT in them — they ship with the game install / example mods.
- NPC class chain: EntityAgent > EntityHumanoid (vsapi) > EntityDressedHumanoid > EntityTradingHumanoid > EntityTrader / EntityVillager (vssurvivalmod).
- AI: `taskai` behavior + AiTaskRegistry in vsessentialsmod/Entity/AI/AiTaskManager.cs; new tasks should use refactored AiTaskBaseR (`-r` codes).
- Dialogue: `conversable` behavior, vssurvivalmod/Systems/Dialogue/. Routines: `activitydriven`, vssurvivalmod/Systems/EntityActivitySystem/.
- vscreativemod has nothing NPC-relevant.

Related: [[npc-mod-maker-goals]]
