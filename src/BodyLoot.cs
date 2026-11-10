using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// 0.0.112: fills a dead NPC's body. The vanilla harvestable behavior (empty drops list in the entity JSON) asks
    /// every IHarvestableDrops on the entity for its drops when it makes them; this answers from the config's
    /// WildBodyLoot or HiredBodyLoot (a recruit's owner stays set after death) for the NPC's rank.
    /// </summary>
    public class EntityBehaviorBodyLoot : EntityBehavior, IHarvestableDrops
    {
        public EntityBehaviorBodyLoot(Entity entity) : base(entity) { }

        public ItemStack[] GetHarvestableDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
        {
            FactionConfig config = world.Api.ModLoader.GetModSystem<FactionSystem>()?.Config;
            if (config == null) return Array.Empty<ItemStack>();
            var stacks = new List<ItemStack>();
            string rank = EntityBehaviorFactionMember.RankOf(entity);
            foreach ((string code, int count) in config.RollBodyLoot(rank, Recruit.OwnerUidOf(entity) != null, world.Rand))
            {
                var loc = new AssetLocation(code);
                CollectibleObject item = (CollectibleObject)world.GetItem(loc) ?? world.GetBlock(loc);
                if (item == null) { world.Logger.Warning("[lotrnpcs] Body loot " + code + " is not an item or block; left out."); continue; }
                stacks.Add(new ItemStack(item, count));
            }
            return stacks.ToArray();
        }

        public override string PropertyName() => "lotrnpcs:bodyloot";
    }
}
