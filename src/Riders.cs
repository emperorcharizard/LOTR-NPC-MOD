using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Essentials;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// 0.0.117 mounted riders (docs/notes/feverstone-mounts.md; owner, 2026-11-12: "the goal is to have [the mount]
    /// spawn as a part of the unit", "the behavior should remain the same for commanding the unit"). A rider is a rank of
    /// its own, promoted from the warrior line next to captain. Its mount comes from another mod (Feverstone's horses and
    /// dire wolves, vanilla elk, Teapot's giant boars), so the mod works without them: no mount type, no rider rank.
    /// Found in the spike: vanilla seats take only players (CanMount is patched for our NPCs), the game's tack path throws
    /// on the server (tack goes straight into the mount's container), and a ridden mount only moves from a player's keys
    /// on that player's client (the server pushes it by its walk vector toward where the rider's own AI is walking).
    /// </summary>
    public static class Riders
    {
        /// <summary>Type attribute: the mount entity codes this rider may get, in order of preference.</summary>
        public const string MountsAttr = "lotrnpcsMounts";
        /// <summary>Watched: the rider's mount's entity id, once it has had one.</summary>
        public const string MountIdAttr = "lotrnpcsMountId";
        /// <summary>Watched: true once its mount is gone (died, or lost): it fights on foot and gets no new one.</summary>
        public const string MountLostAttr = "lotrnpcsMountLost";
        /// <summary>The mount walk vector lengths per tick for walking and running. Measured in game (2026-11-12): 0.06
        /// galloped a horse about 16 blocks a second, so these are about 8 (run) and 4 (walk).</summary>
        public const double WalkSpeed = 0.017, SprintSpeed = 0.033;
        /// <summary>How fast a mount turns toward where its rider wants to go, in radians a second.</summary>
        public const double TurnRate = 5;

        /// <summary>The mount codes of a rider type that exist in this world.</summary>
        public static string[] AvailableMounts(IWorldAccessor world, EntityProperties riderType)
        {
            string[] codes = riderType?.Attributes?[MountsAttr].AsArray<string>(null);
            if (codes == null || world == null) return Array.Empty<string>();
            return codes.Where(c => world.GetEntityType(new AssetLocation(c)) != null).ToArray();
        }

        /// <summary>Whether this NPC type is a rider whose mount can be had here (otherwise the rank isn't offered).</summary>
        public static bool CanHaveMount(IWorldAccessor world, EntityProperties riderType) => AvailableMounts(world, riderType).Length > 0;

        /// <summary>The yaw a mount turns to this step: toward want, at most maxStep radians (the game's yaw convention).</summary>
        public static float TurnToward(float yaw, float want, double maxStep)
        {
            float diff = GameMath.AngleRadDistance(yaw, want);
            return Math.Abs(diff) <= maxStep ? want : yaw + (float)(Math.Sign(diff) * maxStep);
        }

        /// <summary>Walk or run: run while the rider sprints or its next waypoint is more than 3 blocks off.</summary>
        public static double SpeedFor(double waypointDistance, bool riderSprints) =>
            waypointDistance <= 1 ? 0 : riderSprints || waypointDistance > 3 ? SprintSpeed : WalkSpeed;

        /// <summary>The extra horizontal reach from the saddle. 0.0.125 (owner, 2026-11-13: "a bit longer attack range to
        /// compensate for the height difference"): a block (was half a block, the mount's body only).</summary>
        public const float MountedExtraReach = 1f;

        /// <summary>How much farther (horizontally, vertically) a mounted NPC's melee reaches: MountedExtraReach, and the
        /// height its seat lifts it above the mount's feet. (0, 0) on foot.</summary>
        public static (float Horizontal, float Vertical) MountedReach(Entity npc)
        {
            Entity mount = (npc as EntityAgent)?.MountedOn?.Entity;
            if (mount == null) return (0, 0);
            return (MountedExtraReach, (float)Math.Max(0, npc.Pos.Y - mount.Pos.Y));
        }

        // The rider's own walking, running, idle and swimming animations (its AI tasks start walk or sprint while it moves).
        static readonly HashSet<string> legAnimations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "walk", "sprint", "run", "walkback", "idle", "idle1", "swim", "swimidle", "jump", "newjump", "land" };

        /// <summary>0.0.125 (owner: "the legs were moving constantly for the passengers"): animations a seated rider
        /// doesn't play, so only the seat's riding pose (the game's rideelk animations) moves its legs.</summary>
        public static bool IsLegAnimation(AnimationMetaData meta) =>
            meta != null && (legAnimations.Contains(meta.Code ?? "") || legAnimations.Contains(meta.Animation ?? ""));

        // Tack items by the category their attachableToEntity names ("saddle", "saddle-quarter", "bridle-giantboar"...).
        static Dictionary<string, Item> tackByCategory;

        static Item TackFor(IWorldAccessor world, string category)
        {
            if (tackByCategory == null)
            {
                tackByCategory = new Dictionary<string, Item>();
                foreach (Item item in world.Items)
                {
                    string cat = item?.Code == null ? null : item.Attributes?["attachableToEntity"]?["categoryCode"].AsString();
                    if (cat != null && !tackByCategory.ContainsKey(cat)) tackByCategory[cat] = item;
                }
            }
            return category != null && tackByCategory.TryGetValue(category, out Item found) ? found : null;
        }

        /// <summary>Puts a saddle and a bridle on a mount: for each, the first accessory slot taking a category that
        /// starts with "saddle" / "bridle", filled with an item of that category, straight into the mount's tack
        /// container (the game's own TryGiveItemStack throws on the server). True if a saddle went on.</summary>
        public static bool PutOnTack(Entity mount)
        {
            EntityBehaviorContainer tack = mount.SidedProperties.Behaviors.OfType<EntityBehaviorContainer>().FirstOrDefault(b => !(b is EntityBehaviorHarvestable));
            InventoryBase inv = tack?.Inventory;
            JsonObject[] slots = mount.Properties.Server?.BehaviorsAsJsonObj?.FirstOrDefault(b => b["code"].AsString() == "rideableaccessories")?["wearableSlots"].AsArray();
            if (inv == null || slots == null) return false;
            bool saddled = false;
            foreach (string kind in new[] { "saddle", "bridle" })
            {
                for (int i = 0; i < slots.Length && i < inv.Count; i++)
                {
                    string category = (slots[i]["forCategoryCodes"].AsArray<string>() ?? Array.Empty<string>()).FirstOrDefault(c => c.StartsWith(kind));
                    Item item = TackFor(mount.World, category);
                    if (item == null) continue;
                    inv[i].Itemstack = new ItemStack(item);
                    inv[i].MarkDirty();
                    if (kind == "saddle") saddled = true;
                    break;
                }
            }
            tack.storeInv();
            return saddled;
        }

        /// <summary>Spawns a saddled mount for the rider where it stands and seats it. Null when there's no mount type,
        /// no seat, or the game refused.</summary>
        public static Entity SpawnMountFor(EntityAgent rider)
        {
            string[] codes = AvailableMounts(rider.World, rider.Properties);
            if (codes.Length == 0) return null;
            EntityProperties type = rider.World.GetEntityType(new AssetLocation(codes[rider.World.Rand.Next(codes.Length)]));
            if (!(rider.World.ClassRegistry.CreateEntity(type) is EntityAgent mount)) return null;
            mount.Pos.SetFrom(rider.Pos);
            mount.Pos.Yaw = rider.Pos.Yaw;
            mount.PositionBeforeFalling.Set(mount.Pos.X, mount.Pos.Y, mount.Pos.Z);
            rider.World.SpawnEntity(mount);
            PutOnTack(mount);
            IMountableSeat seat = Seat(mount);
            if (seat == null || !rider.TryMount(seat))
            {
                mount.Die(EnumDespawnReason.Removed);
                return null;
            }
            rider.WatchedAttributes.SetLong(MountIdAttr, mount.EntityId);
            return mount;
        }

        public static IMountableSeat Seat(Entity mount)
        {
            IMountable mountable = mount?.SidedProperties?.Behaviors.OfType<IMountable>().FirstOrDefault();
            return mountable?.Seats?.FirstOrDefault(s => s.Config?.Controllable == true) ?? mountable?.Seats?.FirstOrDefault();
        }
    }

    /// <summary>Lets this mod's NPCs onto the game's riding seats (vanilla's EntityRideableSeat.CanMount takes only
    /// players, then checks taming and the player's ownership). Players keep every vanilla check. Both sides.</summary>
    public class RiderSystem : ModSystem
    {
        const string HarmonyId = "lotrnpcs.riders";
        static Harmony harmony;
        ICoreServerAPI sapi;
        // Mounts whose rider died: they run off for a few seconds and are gone (owner: "mount runs off").
        readonly Dictionary<long, (Entity Mount, double AwayX, double AwayZ, long UntilMs)> runningOff = new Dictionary<long, (Entity, double, double, long)>();

        public override void Start(ICoreAPI api)
        {
            api.RegisterEntityBehaviorClass("lotrnpcs:rider", typeof(EntityBehaviorRider));
            if (harmony != null) return;
            MethodInfo canMount = AccessTools.TypeByName("Vintagestory.GameContent.EntityRideableSeat")?.GetMethod("CanMount", new[] { typeof(EntityAgent) });
            if (canMount == null) { api.Logger.Warning("[lotrnpcs] EntityRideableSeat.CanMount not found; riders stay on foot."); return; }
            harmony = new Harmony(HarmonyId);
            harmony.Patch(canMount, new HarmonyMethod(typeof(RiderSystem).GetMethod(nameof(NpcMayMount), BindingFlags.Static | BindingFlags.NonPublic)));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.RegisterGameTickListener(_ => TickRunningOff(), 100);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
        }

        static bool NpcMayMount(EntityAgent __0, ref bool __result)
        {
            if (__0?.Code?.Domain != "lotrnpcs") return true;
            __result = true;
            return false;
        }

        /// <summary>The rider died: its mount gallops away from where it fell for a few seconds, then is gone.</summary>
        public void RunOff(Entity mount, Vec3d from)
        {
            if (sapi == null || mount == null || !mount.Alive) return;
            double dx = mount.Pos.X - from.X, dz = mount.Pos.Z - from.Z, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-3) { double a = sapi.World.Rand.NextDouble() * GameMath.TWOPI; dx = Math.Sin(a); dz = Math.Cos(a); len = 1; }
            runningOff[mount.EntityId] = (mount, dx / len, dz / len, sapi.World.ElapsedMilliseconds + 8000);
        }

        void TickRunningOff()
        {
            if (runningOff.Count == 0) return;
            long now = sapi.World.ElapsedMilliseconds;
            foreach (var kv in runningOff.ToList())
            {
                (Entity mount, double ax, double az, long until) = kv.Value;
                if (!mount.Alive || now >= until)
                {
                    runningOff.Remove(kv.Key);
                    if (mount.Alive) sapi.World.DespawnEntity(mount, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                    continue;
                }
                mount.Pos.Yaw = (float)Math.Atan2(ax, az);
                if (mount is EntityAgent agent)
                {
                    agent.Controls.WalkVector.Set(ax * Riders.SprintSpeed, 0, az * Riders.SprintSpeed);
                    agent.Controls.Forward = agent.Controls.Sprint = true;
                }
                if (mount.GetBehavior<EntityBehaviorGait>() is EntityBehaviorGait gait && gait.Gaits.TryGetValue("sprint", out GaitMeta sprint)) gait.CurrentGait = sprint;
            }
        }
    }

    /// <summary>A rider's server side: gets its mount when it first appears, carries it wherever its own AI walks
    /// (follow, hold, move, guard, formations, fights), lets the mount run off when the rider dies, and fights on foot
    /// once the mount is gone.</summary>
    public class EntityBehaviorRider : EntityBehavior
    {
        const int StepMs = 100, SettleMs = 1500;
        long bornMs = -1, lastStepMs, lastLogMs;

        public EntityBehaviorRider(Entity entity) : base(entity) { }

        public override string PropertyName() => "lotrnpcs:rider";

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);
            if (entity.AnimManager is AnimationManager anims) anims.OnStartAnimation += SkipLegsWhileMounted;
        }

        // The game starts the seat's riding pose on mounting; the rider's AI tasks would lay walk or sprint over it.
        bool SkipLegsWhileMounted(ref AnimationMetaData meta, ref EnumHandling handling)
        {
            if ((entity as EntityAgent)?.MountedOn == null || !Riders.IsLegAnimation(meta)) return false;
            handling = EnumHandling.PreventSubsequent;
            return false;
        }

        public override void OnGameTick(float deltaTime)
        {
            if (entity.Api.Side != EnumAppSide.Server || !entity.Alive || !(entity is EntityAgent rider)) return;
            long now = entity.World.ElapsedMilliseconds;
            if (bornMs < 0) bornMs = now;
            if (now - lastStepMs < StepMs) return;
            float dt = (now - lastStepMs) / 1000f;
            lastStepMs = now;

            Entity mount = rider.MountedOn?.Entity;
            if (mount == null)
            {
                // A loaded rider is put back on its saddle by the game a moment after loading; wait for that first.
                if (now - bornMs < SettleMs || entity.WatchedAttributes.GetBool(Riders.MountLostAttr)) return;
                long id = entity.WatchedAttributes.GetLong(Riders.MountIdAttr);
                Entity old = id == 0 ? null : entity.World.GetEntityById(id);
                if (id != 0 && (old == null || !old.Alive || !rider.TryMount(Riders.Seat(old))))
                {
                    // Its mount died or is gone: on foot from now on.
                    entity.WatchedAttributes.SetBool(Riders.MountLostAttr, true);
                    return;
                }
                if (id == 0 && Riders.SpawnMountFor(rider) == null) entity.WatchedAttributes.SetBool(Riders.MountLostAttr, true);
                return;
            }
            double speed = Steer(rider, mount, dt);
            // With the testing commands on (DebugCommands), one line a second per rider to the server log.
            if (now - lastLogMs >= 1000 && entity.Api.ModLoader.GetModSystem<FactionSystem>()?.Config?.DebugCommands == true)
            {
                lastLogMs = now;
                PathTraverserBase path = rider.GetBehavior<EntityBehaviorTaskAI>()?.PathTraverser;
                string tasks = string.Join("+", rider.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.ActiveTasksBySlot?.Where(t => t != null).Select(t => t.GetType().Name.Replace("AiTask", "")) ?? Enumerable.Empty<string>());
                entity.Api.Logger.Notification(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[lotrnpcs] rider {0} on {1}: at {2:0.0} {3:0.0}, path {4}, speed {5:0.000}, tasks {6}, health {7:0.0}",
                    entity.EntityId, mount.Code?.ToShortString(), mount.Pos.X, mount.Pos.Z, path?.Active == true ? "on" : "off", speed, tasks,
                    entity.WatchedAttributes.GetTreeAttribute("health")?.GetFloat("currenthealth") ?? -1));
            }
        }

        // Toward the rider's AI path-follower target (a mounted entity's own walk vector stays 0), turning at TurnRate.
        static double Steer(EntityAgent rider, Entity mount, float dt)
        {
            PathTraverserBase path = rider.GetBehavior<EntityBehaviorTaskAI>()?.PathTraverser;
            Vec3d target = path != null && path.Active ? path.CurrentTarget : null;
            double tx = target == null ? 0 : target.X - mount.Pos.X, tz = target == null ? 0 : target.Z - mount.Pos.Z;
            double speed = target == null ? 0 : Riders.SpeedFor(Math.Sqrt(tx * tx + tz * tz), rider.Controls.Sprint);
            EntityBehaviorGait gait = mount.GetBehavior<EntityBehaviorGait>();
            if (speed > 0) mount.Pos.Yaw = Riders.TurnToward(mount.Pos.Yaw, (float)Math.Atan2(tx, tz), Riders.TurnRate * dt);
            else if (rider.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.ActiveTasksBySlot?.OfType<AiTaskBaseTargetable>().Select(t => t.TargetEntity).FirstOrDefault(e => e != null && e.Alive) is Entity foe)
            {
                // Standing in a fight: face the enemy, since a rider turns only a little in the saddle and melee contact
                // is measured from just ahead of the rider (AiTaskBaseTargetable.hasDirectContact).
                mount.Pos.Yaw = Riders.TurnToward(mount.Pos.Yaw, (float)Math.Atan2(foe.Pos.X - mount.Pos.X, foe.Pos.Z - mount.Pos.Z), Riders.TurnRate * dt);
            }
            if (gait != null)
            {
                if (speed <= 0) gait.SetIdle();
                else if (gait.Gaits.TryGetValue(speed >= Riders.SprintSpeed ? "sprint" : "walk", out GaitMeta meta)) gait.CurrentGait = meta;
            }
            if (mount is EntityAgent agent)
            {
                agent.Controls.WalkVector.Set(Math.Sin(mount.Pos.Yaw) * speed, 0, Math.Cos(mount.Pos.Yaw) * speed);
                agent.Controls.Forward = speed > 0;
                agent.Controls.Sprint = speed >= Riders.SprintSpeed;
            }
            return speed;
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            if (entity.Api.Side != EnumAppSide.Server || !(entity is EntityAgent rider)) return;
            Entity mount = rider.MountedOn?.Entity;
            if (mount == null) return;
            rider.TryUnmount();
            entity.Api.ModLoader.GetModSystem<RiderSystem>()?.RunOff(mount, damageSourceForDeath?.GetCauseEntity()?.Pos.XYZ ?? entity.Pos.XYZ);
        }
    }
}
