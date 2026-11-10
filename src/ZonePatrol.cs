using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// 0.0.112 (owner, review Q11: "admin gives a zone 2+ points, groups walk between them"): a wild NPC spawned in a
    /// zone with a patrol walks its points in order, round and round, pausing a little at each. Fights come first
    /// (seek and melee are higher priority); hiring it ends the patrol. The route is in the NPC's own attributes, so it
    /// survives the zone being changed or removed.
    /// </summary>
    public class AiTaskZonePatrol : AiTaskBase
    {
        public const string RouteAttr = "lotrnpcsPatrol", NextAttr = "lotrnpcsPatrolNext";
        // Arrived within this many blocks; one walk goes at most Leg blocks, so far points don't need a long path search.
        public const double ArriveWithin = 2.5, Leg = 20;
        const int PauseMinMs = 4000, PauseMaxMs = 12000;
        readonly float walkSpeed;
        long nextCheckMs;
        Vec3d goal;
        bool stuck;
        float seconds;

        public AiTaskZonePatrol(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            walkSpeed = taskConfig["movespeed"].AsFloat(0.02f);
        }

        /// <summary>
        /// Where to walk next from (x, z) on a route of x, z pairs, given the point it is heading for. A point reached
        /// (within ArriveWithin) moves next on, wrapping round, and returns false so the NPC pauses there. Otherwise the
        /// goal is that point, or Leg blocks toward it. Offsets keep a group's members apart.
        /// </summary>
        public static bool Step(int[] route, ref int next, double x, double z, double offX, double offZ, out double gx, out double gz)
        {
            int points = route.Length / 2;
            next = ((next % points) + points) % points;
            double px = route[2 * next] + 0.5 + offX, pz = route[2 * next + 1] + 0.5 + offZ, dx = px - x, dz = pz - z, d = Math.Sqrt(dx * dx + dz * dz);
            gx = px; gz = pz;
            if (d <= ArriveWithin) { next = (next + 1) % points; return false; }
            if (d > Leg) { gx = x + dx / d * Leg; gz = z + dz / d * Leg; }
            return true;
        }

        public override bool ShouldExecute()
        {
            long now = entity.World.ElapsedMilliseconds;
            if (now < nextCheckMs) return false;
            nextCheckMs = now + 1000;
            if (Recruit.OwnerUidOf(entity) != null || !(entity.Attributes[RouteAttr] is IntArrayAttribute arr) || arr.value == null || arr.value.Length < 4) return false;
            if (entity.GetBehavior<EntityBehaviorFactionMember>()?.Fighting(now) == true) return false;
            int next = entity.Attributes.GetInt(NextAttr);
            // A few blocks apart by entity, so a group walks side by side instead of into one spot.
            double offX = entity.EntityId % 5 - 2, offZ = entity.EntityId / 5 % 5 - 2;
            bool walk = Step(arr.value, ref next, entity.Pos.X, entity.Pos.Z, offX, offZ, out double gx, out double gz);
            entity.Attributes.SetInt(NextAttr, next);
            if (!walk) { nextCheckMs = now + entity.World.Rand.Next(PauseMinMs, PauseMaxMs + 1); return false; }
            double y = Ground.StandHeight(entity.World.BlockAccessor, gx, entity.Pos.Y + 0.5, gz, entity.Pos.Dimension, 12) ?? entity.Pos.Y;
            goal = new Vec3d(gx, y, gz);
            return true;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            seconds = 0;
            stuck = false;
            pathTraverser.NavigateTo_Async(goal.Clone(), walkSpeed, 1.5f, () => { }, () => stuck = true, null, 2000, 1);
        }

        public override bool CanContinueExecute() => pathTraverser.Ready;

        public override bool ContinueExecute(float dt)
        {
            seconds += dt;
            if (stuck || seconds > 40) return false;
            double dx = entity.Pos.X - goal.X, dz = entity.Pos.Z - goal.Z;
            if (dx * dx + dz * dz < 1.5 * 1.5) return false;
            return pathTraverser.Active || seconds < 2;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            pathTraverser.Stop();
            if (stuck) nextCheckMs = entity.World.ElapsedMilliseconds + 5000;
        }
    }
}
