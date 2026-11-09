// Compile-only reference stub. Signatures copied from anegostudios/vsapi 1.22.7.
// Never shipped: at runtime the game's real VintagestoryAPI.dll is used.
using System;

namespace Vintagestory.API.Common
{
    public enum EnumAppSide { Server = 1, Client = 2, Universal = 3 }

    public enum EnumHandling { PassThrough, Handled, PreventDefault, PreventSubsequent }

    public enum EnumDamageType
    {
        Gravity, Fire, BluntAttack, SlashingAttack, PiercingAttack, Suffocation, Heal, Poison,
        Hunger, Crushing, Frost, Electricity, Heat, Injury, Acid
    }

    public enum EnumDamageSource
    {
        Block, Player, Fall, Drown, Revive, Void, Suicide, Internal, Entity, Explosion, Machine, Unknown, Weather, Bleed
    }

    public class DamageSource
    {
        public EnumDamageSource Source;
        public EnumDamageType Type;
        public Vintagestory.API.Common.Entities.Entity SourceEntity;
        public int DamageTier = 0;
        public float KnockbackStrength = 1f;
    }

    public class EntityAgent : Vintagestory.API.Common.Entities.Entity
    {
    }

    public class EntityHumanoid : EntityAgent
    {
    }

    public class EntityPlayer : EntityHumanoid
    {
        public string PlayerUID => throw null;
    }

    public interface IPlayer
    {
        string PlayerUID { get; }
    }

    public interface IClassRegistryAPI
    {
        Type GetEntityBehaviorClass(string entityBehaviorName);
    }

    public interface ICoreAPICommon
    {
        void RegisterEntityBehaviorClass(string className, Type entityBehavior);
    }

    public interface ICoreAPI : ICoreAPICommon
    {
        ILogger Logger { get; }
        IWorldAccessor World { get; }
        IClassRegistryAPI ClassRegistry { get; }
    }

    public interface ILogger
    {
        void Notification(string message);
        void Warning(string message);
    }

    public interface IWorldAccessor
    {
        ILogger Logger { get; }
        EnumAppSide Side { get; }
        Item GetItem(AssetLocation itemCode);
        System.Random Rand { get; }
    }

    public class AssetLocation
    {
        public AssetLocation(string domainAndPath) => throw null;
    }

    public abstract class RegistryObject
    {
    }

    public abstract class CollectibleObject : RegistryObject
    {
    }

    public class Item : CollectibleObject
    {
    }

    public class ItemStack
    {
        public ItemStack(Item item, int stacksize = 1) => throw null;
    }

    public abstract class ModSystem
    {
        public virtual bool ShouldLoad(EnumAppSide forSide) => throw null;
        public virtual void Start(ICoreAPI api) => throw null;
        public virtual void StartClientSide(Vintagestory.API.Client.ICoreClientAPI api) => throw null;
        public virtual void Dispose() => throw null;
    }
}

namespace Vintagestory.API.MathTools
{
    public class Size2i
    {
    }
}

namespace Vintagestory.API.Client
{
    using Vintagestory.API.Common;
    using Vintagestory.API.MathTools;

    public class TextureAtlasPosition
    {
    }

    public interface ITexPositionSource
    {
        TextureAtlasPosition this[string textureCode] { get; }
        Size2i AtlasSize { get; }
    }

    public interface ITextureAtlasAPI
    {
        TextureAtlasPosition UnknownTexturePosition { get; }
        Size2i Size { get; }
        TextureAtlasPosition[] Positions { get; }
    }

    public interface IClientPlayer : IPlayer
    {
    }

    public interface IClientWorldAccessor : IWorldAccessor
    {
        IClientPlayer Player { get; }
    }

    public interface ICoreClientAPI : ICoreAPI
    {
        ITextureAtlasAPI EntityTextureAtlas { get; }
        new IClientWorldAccessor World { get; }
    }

    public class CompositeTexture
    {
        public BakedCompositeTexture Baked;
    }

    public class BakedCompositeTexture
    {
        public int TextureSubId;
    }
}

namespace Vintagestory.API.Config
{
    public class GlobalConstants
    {
        public static float CreatureDamageModifier = 1;
    }
}

namespace Vintagestory.API.Datastructures
{
    public class JsonObject
    {
        public JsonObject this[string key] => throw null;
        public string AsString(string defaultValue = null) => throw null;
        public int AsInt(int defaultValue = 0) => throw null;
        public float AsFloat(float defaultValue = 0) => throw null;
        public T[] AsArray<T>(T[] defaultValue = null, string defaultDomain = "game") => throw null;
        public JsonObject[] AsArray() => throw null;
    }

    public class TreeAttribute
    {
        public bool HasAttribute(string key) => throw null;
        public virtual void SetString(string key, string value) => throw null;
    }

    public class SyncedTreeAttribute : TreeAttribute
    {
        public void MarkPathDirty(string path) => throw null;
    }
}

namespace Vintagestory.API.Common.Entities
{
    using System.Collections.Generic;
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.Datastructures;

    public class EntityProperties
    {
        public EntityClientProperties Client;
    }

    public abstract class EntitySidedProperties
    {
        public List<EntityBehavior> Behaviors;
    }

    public class EntityClientProperties : EntitySidedProperties
    {
        public IDictionary<string, CompositeTexture> Textures;
    }

    public abstract class Entity
    {
        public SyncedTreeAttribute WatchedAttributes;
        public IWorldAccessor World;
        public ICoreAPI Api;
        public EntityProperties Properties { protected set => throw null; get => throw null; }
        public virtual bool TryGiveItemStack(ItemStack itemstack) => throw null;
        public virtual bool ReceiveDamage(DamageSource damageSource, float damage) => throw null;
    }

    public abstract class EntityBehavior
    {
        public Entity entity;
        public EntityBehavior(Entity entity) => throw null;
        public virtual void Initialize(EntityProperties properties, JsonObject attributes) => throw null;
        public virtual void OnEntitySpawn() => throw null;
        public virtual void OnEntityReceiveDamage(DamageSource damageSource, ref float damage) => throw null;
        public virtual ITexPositionSource GetTextureSource(ref EnumHandling handling) => throw null;
        public abstract string PropertyName();
    }
}
