// Compile-only reference stub of the game's VSEssentials.dll. Signatures copied from anegostudios/vsessentialsmod 1.22.7.
// Never shipped: at runtime the game's real VSEssentials.dll is used.
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.API.Common
{
    public interface IAiTask
    {
    }

    public abstract class AiTaskBase : IAiTask
    {
        public readonly EntityAgent entity;
        public AiTaskBase(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) => throw null;
    }
}

namespace Vintagestory.GameContent
{
    using Vintagestory.API.Common;

    public abstract class AiTaskBaseTargetable : AiTaskBase
    {
        public Entity targetEntity;
        protected AiTaskBaseTargetable(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) => throw null;
        protected virtual bool hasDirectContact(Entity targetEntity, float minDist, float minVerDist) => throw null;
    }

    public class AiTaskMeleeAttack : AiTaskBaseTargetable
    {
        protected float damage;
        protected float knockbackStrength;
        protected float minDist;
        protected float minVerDist;
        public EnumDamageType damageType;
        public int damageTier;
        public AiTaskMeleeAttack(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) => throw null;
        protected virtual void attackTarget() => throw null;
    }

    public static class AiTaskRegistry
    {
        public static void Register<TTask>(string code) where TTask : IAiTask => throw null;
    }
}
