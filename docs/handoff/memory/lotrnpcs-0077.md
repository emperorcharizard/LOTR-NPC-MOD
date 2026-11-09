# lotrnpcs 0.0.77 (2026-11-04, second account, new chat)

- Owner's 0.0.76 test: two-handed holds never played. Server-debug log: "attempted to play an animation code which its
  shape does not have: poleaxehold/poleaxechop". The server builds animations from the entity type's base shape, not the
  PML race model. NPC types now use `lotrnpcs:entity/humanoid/seraph-faceless` (our copy with the injected anims).
- Follow and fight: fighting followers don't run back between blows; formation (not loose) followers fight within 12
  blocks of the leader and break off past 12 until back within 4; loose followers fight on, no teleport mid-fight.
- Attacker cap 3 kept, but a crowded target is taken when the search finds nothing else (every NPC).
- Archers no longer hold fire for friends in the line (arrows pass through friends anyway).
- Walking axe x +90; boar spear z 180 as the owner asked (now thrusts butt-first, flagged); Easterling shield
  (`orcweapon-eastshield`); Misty Mountains warriors wear the Moria helmet.
- Game files on the owner's PC: device link worked, but folder access to AppData\Roaming\Vintagestory was blocked by the
  auto-mode check. Falx/longbow codes still from the wiki.

## 0.0.78 (same chat)
- Owner's three Blockbench Haradrim armors (infantry, heavy infantry, warlord) -> separate mod `haradrimarmor`
  (build-inputs/haradrim-armor-0.1.0.zip, owner chose a separate mod). Blockbench preview body legs are shorter than the
  game body (shin 7 vs 9 visible, thigh 7 vs 6 visible): leg pieces rescaled in Y. `fallbackSets` in startinggear keeps
  the Nadiyan outfits for servers without the mod.
