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

        /// <summary>0.0.126: the rider task move speed from which a mount may run. Wander (0.012), patrol (0.02) and a
        /// recruit's walking follow (0.027) walk the mount; seek (0.03), hide (0.04) and a running follow (0.055) may run.
        /// Until 0.0.125 a wandering rider galloped off at about 8 blocks a second and out of Synergy's range.</summary>
        public const float RunTaskSpeed = 0.029f;
        /// <summary>How close the mount comes to a waypoint before it stands (0.0.126: half a block, was a block, which
        /// parked it a block short of a waypoint the path follower was still waiting for).</summary>
        public const double StopDistance = 0.5;
        /// <summary>0.0.126: in a fight with no path to follow, the mount walks in until its enemy is this close
        /// (centers, horizontally), inside a rider's reach from the saddle (MountedReach), instead of parking out of reach.</summary>
        public const double CloseInDistance = 2.5;

        /// <summary>Stand, walk or run: stand at the waypoint, walk for slow tasks (taskSpeed below RunTaskSpeed), else
        /// run while the rider sprints or its next waypoint is more than 3 blocks off.</summary>
        public static double SpeedFor(double waypointDistance, bool riderSprints, float taskSpeed) =>
            waypointDistance <= StopDistance ? 0 : taskSpeed < RunTaskSpeed ? WalkSpeed : riderSprints || waypointDistance > 3 ? SprintSpeed : WalkSpeed;

        /// <summary>0.0.131: within this many blocks of its melee target (centers, horizontally), or while it swings,
        /// a rider's mount stops following the seek path and only turns to the enemy (walking in past CloseInDistance).
        /// Until 0.0.130 the mount kept walking about 4 blocks a second during the swing, so the blow often fell with
        /// the enemy out of reach (battle logs 233500, 233700: riders went 10-17 s without a hit while surrounded).</summary>
        public const double HoldFightDistance = 3.5;

        /// <summary>0.0.132: a mounted rider keeps its melee target (its focus) while that target lives and is within this
        /// many blocks (centers, horizontally), instead of hitting whichever enemy is nearest or struck it last. In 0.0.131
        /// three riders dealt 134 damage to ten 28-health Dunlendings but killed one: the hits were spread.</summary>
        public const double FocusRange = 16; // 0.0.133: 16 (was 6), so ride-outs keep it

        sealed class Focus { public Entity Target; }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Entity, Focus> focus = new System.Runtime.CompilerServices.ConditionalWeakTable<Entity, Focus>();

        /// <summary>Whether a rider keeps a focus this far away (dead targets are dropped).</summary>
        public static bool KeepsFocus(bool alive, double distance) => alive && distance <= FocusRange;

        static double Flat(Entity a, Entity b) => Math.Sqrt((a.Pos.X - b.Pos.X) * (a.Pos.X - b.Pos.X) + (a.Pos.Z - b.Pos.Z) * (a.Pos.Z - b.Pos.Z));

        /// <summary>The rider's focus if it still holds (alive, same dimension, within FocusRange), else null.</summary>
        public static Entity FocusOf(Entity rider)
        {
            if (rider == null || !focus.TryGetValue(rider, out Focus f) || f.Target == null) return null;
            Entity t = f.Target;
            if (t.Pos.Dimension != rider.Pos.Dimension || !KeepsFocus(t.Alive, Flat(rider, t))) { f.Target = null; return null; }
            return t;
        }

        public static void SetFocus(Entity rider, Entity target)
        {
            if (rider != null) focus.GetValue(rider, _ => new Focus()).Target = target;
        }

        // ---- 0.0.133 (owner chose "Charge and wheel away"): approach, charge, ride through, wheel, repeat ----

        /// <summary>What a mounted rider's mount is doing in a fight: Hold (the 0.0.131 standing fight), Charge (gallop
        /// straight at the focus) or RideOut (gallop on along the charge's heading, then wheel back).</summary>
        public enum ChargePhase { Hold, Charge, RideOut }

        /// <summary>Charge when the focus is ChargeMin to ChargeMax blocks off; ride out RideOutDistance blocks past it; a
        /// pass is over within PassDistance or once the focus is behind; boxed in when a galloping mount moved under
        /// BoxedMove blocks in BoxedMs, then it holds for BoxedHoldMs. Followers ride out at most FollowerLeash blocks
        /// from their leader, guards and holders stay in their post's area.</summary>
        public const double ChargeMin = 5, ChargeMax = 16, RideOutDistance = 7, PassDistance = 1.5, BoxedMove = 0.3, FollowerLeash = 12;
        public const long BoxedMs = 1500, BoxedHoldMs = 3000;
        /// <summary>A blow landed at a gallop: damage times this, and the target is pushed along the mount's heading
        /// (ChargeKnockback blocks a tick of motion, about 1.5 blocks on the ground; NPCs only, a player's motion is
        /// its own client's).</summary>
        public const float ChargeDamageMultiplier = 2f;
        public const double ChargeKnockback = 0.45;

        /// <summary>The next phase. foeDistance: to the focus (centers, horizontally); rodeOut: blocks since the
        /// ride-out began; passed: the focus is behind, within PassDistance or was hit on this pass; outOfBounds: the
        /// rider's orders don't let it go farther; boxed: it couldn't gallop lately.</summary>
        public static ChargePhase NextPhase(ChargePhase phase, double foeDistance, double rodeOut, bool passed, bool outOfBounds, bool boxed)
        {
            if (boxed) return ChargePhase.Hold;
            switch (phase)
            {
                case ChargePhase.Charge:
                    return passed ? ChargePhase.RideOut : foeDistance > ChargeMax ? ChargePhase.Hold : ChargePhase.Charge;
                case ChargePhase.RideOut:
                    return rodeOut >= RideOutDistance || outOfBounds || foeDistance >= ChargeMax ? ChargePhase.Charge : ChargePhase.RideOut;
                default:
                    if (foeDistance > ChargeMax) return ChargePhase.Hold;
                    return foeDistance >= ChargeMin ? ChargePhase.Charge : outOfBounds ? ChargePhase.Hold : ChargePhase.RideOut;
            }
        }

        /// <summary>One rider's charge state (server side).</summary>
        public sealed class ChargeState
        {
            public ChargePhase Phase;
            public double Speed, HeadX, HeadZ, StartX, StartZ, CheckX, CheckZ;
            public long CheckMs, BoxedUntilMs;
            public bool HitThisPass;
        }

        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Entity, ChargeState> charges = new System.Runtime.CompilerServices.ConditionalWeakTable<Entity, ChargeState>();

        public static ChargeState ChargeOf(Entity rider) => charges.GetValue(rider, _ => new ChargeState());

        /// <summary>Whether a blow this rider lands now is a charge hit: mounted, charging, at a gallop.</summary>
        public static bool IsChargeHit(Entity rider) =>
            (rider as EntityAgent)?.MountedOn != null && charges.TryGetValue(rider, out ChargeState c) && c.Phase == ChargePhase.Charge && c.Speed >= SprintSpeed;

        /// <summary>A charge hit landed: the target is pushed along the mount's heading (NPCs only) and the pass ends.</summary>
        public static void ChargeHitLanded(Entity rider, Entity target)
        {
            if (!charges.TryGetValue(rider, out ChargeState c)) return;
            c.HitThisPass = true;
            Entity mount = (rider as EntityAgent)?.MountedOn?.Entity;
            if (target == null || target is EntityPlayer || mount == null) return;
            target.Pos.Motion.X += Math.Sin(mount.Pos.Yaw) * ChargeKnockback;
            target.Pos.Motion.Z += Math.Cos(mount.Pos.Yaw) * ChargeKnockback;
            target.Pos.Motion.Y += 0.1;
        }

        /// <summary>Whether a recruit rider's orders keep it from riding farther: a guard or holder outside its post's
        /// area, a follower more than FollowerLeash blocks from its leader. Wild riders: never.</summary>
        public static bool OutOfBounds(Entity rider, FactionSystem factions)
        {
            if (Recruit.OwnerUidOf(rider) == null) return false;
            string order = Recruit.OrderOf(rider);
            if (order == Recruit.Guard || order == Recruit.Hold)
            {
                if (!Recruit.TryGetPost(rider, out double px, out double py, out double pz)) return false;
                double r = order == Recruit.Guard ? (factions?.GuardRadius ?? 8) : AiTaskFactionHide.HoldLeash;
                return (rider.Pos.X - px) * (rider.Pos.X - px) + (rider.Pos.Z - pz) * (rider.Pos.Z - pz) > r * r;
            }
            Entity leader = Recruit.OnlineLeader(rider);
            return leader != null && leader.Pos.Dimension == rider.Pos.Dimension && Flat(rider, leader) > FollowerLeash;
        }

        /// <summary>Whether the mount holds for the fight instead of following the path.</summary>
        public static bool HoldsForFight(double enemyDistance, bool swinging) => swinging || enemyDistance <= HoldFightDistance;

        /// <summary>Standing in a fight: walk toward the enemy while it is farther than CloseInDistance.</summary>
        public static double CloseInSpeed(double enemyDistance) => enemyDistance > CloseInDistance ? WalkSpeed : 0;

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

        // The speed the rider's current task asked the path follower for (protected in the game's PathTraverserBase).
        static readonly AccessTools.FieldRef<PathTraverserBase, float> taskSpeedOf = AccessTools.FieldRefAccess<PathTraverserBase, float>("movingSpeed");

        // Toward the rider's AI path-follower target (a mounted entity's own walk vector stays 0), turning at TurnRate.
        static double Steer(EntityAgent rider, Entity mount, float dt)
        {
            PathTraverserBase path = rider.GetBehavior<EntityBehaviorTaskAI>()?.PathTraverser;
            Vec3d target = path != null && path.Active ? path.CurrentTarget : null;
            double tx = target == null ? 0 : target.X - mount.Pos.X, tz = target == null ? 0 : target.Z - mount.Pos.Z;
            double speed = target == null ? 0 : Riders.SpeedFor(Math.Sqrt(tx * tx + tz * tz), rider.Controls.Sprint, taskSpeedOf(path));
            EntityBehaviorGait gait = mount.GetBehavior<EntityBehaviorGait>();
            var active = rider.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.ActiveTasksBySlot;
            AiTaskChestMeleeAttack swing = active?.OfType<AiTaskChestMeleeAttack>().FirstOrDefault(t => t.TargetEntity != null && t.TargetEntity.Alive);
            // 0.0.132: the rider's focus first, so the mount stays with the enemy it is finishing.
            Entity kept = Riders.FocusOf(rider);
            Entity foe = kept ?? swing?.TargetEntity ?? active?.OfType<AiTaskBaseTargetable>().Select(t => t.TargetEntity).FirstOrDefault(e => e != null && e.Alive);
            double foeDist = foe == null ? double.MaxValue : Math.Sqrt((foe.Pos.X - mount.Pos.X) * (foe.Pos.X - mount.Pos.X) + (foe.Pos.Z - mount.Pos.Z) * (foe.Pos.Z - mount.Pos.Z));
            bool holding = foe != null && (kept != null || Riders.HoldsForFight(foeDist, swing != null));
            // 0.0.133: charge and wheel away while the rider has a focus.
            Riders.ChargeState cs = Riders.ChargeOf(rider);
            long now = rider.World.ElapsedMilliseconds;
            if (kept == null) { cs.Phase = Riders.ChargePhase.Hold; cs.HitThisPass = false; }
            else
            {
                double fx = kept.Pos.X - mount.Pos.X, fz = kept.Pos.Z - mount.Pos.Z;
                bool galloping = cs.Phase != Riders.ChargePhase.Hold && cs.Speed >= Riders.SprintSpeed;
                if (!galloping || now - cs.CheckMs >= Riders.BoxedMs)
                {
                    double moved = Math.Sqrt((mount.Pos.X - cs.CheckX) * (mount.Pos.X - cs.CheckX) + (mount.Pos.Z - cs.CheckZ) * (mount.Pos.Z - cs.CheckZ));
                    if (galloping && moved < Riders.BoxedMove) cs.BoxedUntilMs = now + Riders.BoxedHoldMs;
                    cs.CheckMs = now; cs.CheckX = mount.Pos.X; cs.CheckZ = mount.Pos.Z;
                }
                bool passed = cs.HitThisPass || foeDist <= Riders.PassDistance || (cs.Phase == Riders.ChargePhase.Charge && fx * Math.Sin(mount.Pos.Yaw) + fz * Math.Cos(mount.Pos.Yaw) < 0);
                double rodeOut = Math.Sqrt((mount.Pos.X - cs.StartX) * (mount.Pos.X - cs.StartX) + (mount.Pos.Z - cs.StartZ) * (mount.Pos.Z - cs.StartZ));
                Riders.ChargePhase next = Riders.NextPhase(cs.Phase, foeDist, rodeOut, passed, Riders.OutOfBounds(rider, rider.Api.ModLoader.GetModSystem<FactionSystem>()), now < cs.BoxedUntilMs);
                if (next == Riders.ChargePhase.RideOut && cs.Phase != Riders.ChargePhase.RideOut)
                {
                    // Ride on the way the charge went (from standing: straight away from the focus).
                    double hx = cs.Phase == Riders.ChargePhase.Charge ? Math.Sin(mount.Pos.Yaw) : -fx, hz = cs.Phase == Riders.ChargePhase.Charge ? Math.Cos(mount.Pos.Yaw) : -fz, hl = Math.Sqrt(hx * hx + hz * hz);
                    cs.HeadX = hl < 1e-6 ? Math.Sin(mount.Pos.Yaw) : hx / hl; cs.HeadZ = hl < 1e-6 ? Math.Cos(mount.Pos.Yaw) : hz / hl;
                    cs.StartX = mount.Pos.X; cs.StartZ = mount.Pos.Z;
                }
                if (next == Riders.ChargePhase.Charge && cs.Phase != Riders.ChargePhase.Charge) cs.HitThisPass = false;
                cs.Phase = next;
                if (next != Riders.ChargePhase.Hold)
                {
                    double wx = next == Riders.ChargePhase.Charge ? fx : cs.HeadX, wz = next == Riders.ChargePhase.Charge ? fz : cs.HeadZ;
                    mount.Pos.Yaw = Riders.TurnToward(mount.Pos.Yaw, (float)Math.Atan2(wx, wz), Riders.TurnRate * dt);
                    speed = Riders.SprintSpeed;
                    holding = false;
                }
            }
            bool charging = cs.Phase != Riders.ChargePhase.Hold;
            if (!charging && speed > 0 && !holding) mount.Pos.Yaw = Riders.TurnToward(mount.Pos.Yaw, (float)Math.Atan2(tx, tz), Riders.TurnRate * dt);
            else if (!charging && foe != null)
            {
                // Standing in a fight: face the enemy, since a rider turns only a little in the saddle and melee contact
                // is measured from just ahead of the rider (AiTaskBaseTargetable.hasDirectContact); 0.0.126: and walk in to reach it.
                double fx = foe.Pos.X - mount.Pos.X, fz = foe.Pos.Z - mount.Pos.Z;
                mount.Pos.Yaw = Riders.TurnToward(mount.Pos.Yaw, (float)Math.Atan2(fx, fz), Riders.TurnRate * dt);
                speed = Riders.CloseInSpeed(Math.Sqrt(fx * fx + fz * fz));
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
            Riders.ChargeOf(rider).Speed = speed;
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
