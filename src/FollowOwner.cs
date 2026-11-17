using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// Recruits follow their owner, loosely or in the owner's formation, go to the spot they hold, or patrol the area
    /// they guard. They walk, and run whenever they fall behind (user, 2026-11-03: "they move at a walking speed"),
    /// switching on the way; patrols are walked, unless a guard is heading back from outside its area. Faction fights
    /// come first: seek and melee have higher priority.
    /// </summary>
    public class AiTaskFollowOwner : AiTaskBase
    {
        // Run when farther than this from the goal, walk again when closer than WalkWithin.
        const float RunBeyond = 6, WalkWithin = 3;
        // Guards stand this long (ms) between patrol walks, which go up to PatrolShare of the radius from the post.
        // Coming back from outside the area, they aim for the inner ReturnShare of it.
        const int PatrolPauseMinMs = 6000, PatrolPauseMaxMs = 16000;
        const double PatrolShare = 0.8, ReturnShare = 0.5;
        static readonly FieldInfo SpeedField = typeof(PathTraverserBase).GetField("movingSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo NewSpeedField = typeof(Vintagestory.Essentials.WaypointsTraverser).GetField("movingSpeed_New", BindingFlags.NonPublic | BindingFlags.Instance);

        readonly float walkSpeed, runSpeed, followDistance, holdDistance;
        readonly AnimationMetaData runAnim;
        readonly HiringSystem hiring;
        Vec3d goalAtStart;
        bool stuck, running, patrolling, returning;
        // Set while the guard fights, so it walks back to its post once the fight is over (0.0.76).
        bool foughtSincePost;
        // Back at the post after a chase: a spot within this many blocks of it.
        const double BackToPostWithin = 3;
        long nextCheckMs, nextPatrolMs;
        float seconds;

        public AiTaskFollowOwner(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            walkSpeed = taskConfig["walkSpeed"].AsFloat(0.025f);
            runSpeed = taskConfig["runSpeed"].AsFloat(0.055f);
            followDistance = taskConfig["followDistance"].AsFloat(4);
            holdDistance = taskConfig["holdDistance"].AsFloat(2);
            hiring = entity.Api.ModLoader.GetModSystem<HiringSystem>();
            AnimationMetaData sprint = entity.Properties.Client?.Animations?.FirstOrDefault(a => a.Code == "sprint");
            if (sprint != null)
            {
                runAnim = sprint.Clone();
                runAnim.AnimationSpeed = taskConfig["runAnimationSpeed"].AsFloat(0.75f);
            }
        }

        // Holding a spot: a held one, or a guard's place in its formation.
        bool Holding
        {
            get
            {
                string order = Recruit.OrderOf(entity);
                return order == Recruit.Hold || (order == Recruit.Guard && Recruit.HasGuardSpot(entity));
            }
        }
        // Going for an exact spot: a held spot, a patrol spot, or a place in the owner's formation.
        bool Exact => patrolling || Holding || (hiring != null && Recruit.FollowSlot(entity) != null);
        // Standing in a formation (held or guarded): close to the exact spot, so the shape stays neat.
        bool InFormation => Holding && Recruit.HasFacing(entity);
        float StartDistance => InFormation ? 0.8f : Holding ? holdDistance : Exact ? 1.6f : followDistance;
        float StopDistance => InFormation ? 0.35f : Exact ? 0.9f : 2.5f;

        Vec3d Goal()
        {
            string uid = Recruit.LeaderUidOf(entity);
            if (uid == null) return null;
            string order = Recruit.OrderOf(entity);
            // A guard walks to the spot ShouldPatrol picked, and nowhere else.
            if (order == Recruit.Guard) return Recruit.HasGuardSpot(entity) ? Recruit.GuardSpot(entity) : patrolling ? goalAtStart : null;
            if (patrolling) return null;
            if (order == Recruit.Hold) return Recruit.HoldPos(entity);
            EntityPlayer owner = Recruit.OnlineLeader(entity);
            if (owner == null || !owner.Alive || owner.Pos.Dimension != entity.Pos.Dimension) return null;
            Vec2d slot = Recruit.FollowSlot(entity);
            if (slot == null || hiring == null) return owner.Pos.XYZ;
            Vec2d heading = hiring.Heading(uid);
            return Formation.World(owner.Pos.XYZ, heading.X, heading.Y, slot);
        }

        // Exact spots count across the ground, so a slope or a step doesn't keep them shuffling.
        double Distance(Vec3d goal)
        {
            double dx = entity.Pos.X - goal.X, dy = entity.Pos.Y - goal.Y, dz = entity.Pos.Z - goal.Z;
            return Exact && Math.Abs(dy) < 2.5 ? Math.Sqrt(dx * dx + dz * dz) : Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public override bool ShouldExecute()
        {
            long now = entity.World.ElapsedMilliseconds;
            if (now < nextCheckMs) return false;
            nextCheckMs = now + 400;
            if (Recruit.OwnerUidOf(entity) == null) return false;
            if (Recruit.OrderOf(entity) == Recruit.Guard && !Recruit.HasGuardSpot(entity)) return ShouldPatrol(now);
            patrolling = returning = false;
            // 0.0.77 (owner, 2026-11-04: on aggressive, "they run to me after they make a hit, then re aggro back onto
            // the closest enemy"): a follower in a fight stays in it between blows instead of running back to its
            // leader. It comes back when the fight is over, or (in a formation other than loose) when it breaks off.
            // 0.0.114 (owner, 0.0.113 test: followers run to the player in battle again): the 3 s fighting mark lapses
            // in a battle line's pauses (waiting in its slot, a turn on the target), so a line member counts as fighting
            // too; lines keep members MemberGraceMs after their last target.
            if (Recruit.OrderOf(entity) == Recruit.Follow && hiring != null && Recruit.StanceOf(entity) != Recruit.Passive && !hiring.Disengaging(entity)
                && (entity.GetBehavior<EntityBehaviorFactionMember>()?.Fighting(now) == true || hiring.Factions?.Lines.LineOf(entity.EntityId) != null)) return false;
            Vec3d goal = Goal();
            if (goal == null || Distance(goal) <= StartDistance)
            {
                // In place: turn to face the formation's way.
                if (goal != null && InFormation)
                {
                    float yaw = Recruit.FacingYaw(entity);
                    if (Math.Abs(GameMath.AngleRadDistance(entity.Pos.Yaw, yaw)) > 0.15f)
                    {
                        entity.Pos.Yaw = yaw;
                        if (entity is EntityAgent agent) agent.BodyYaw = yaw;
                    }
                }
                return false;
            }
            goalAtStart = goal;
            return true;
        }

        // Guard here: every so often, and whenever it is outside its area, a guard picks a spot in the area to walk to.
        bool ShouldPatrol(long now)
        {
            if (!Recruit.TryGetPost(entity, out double px, out double py, out double pz)) return false;
            double radius = hiring?.GuardRadius ?? FactionConfig.DefaultGuardRadius;
            double dx = entity.Pos.X - px, dz = entity.Pos.Z - pz;
            // 0.0.76 (user, 2026-11-04: "after a chase, the recruits should return to post"): a guard that chased
            // something walks back to its post when the fight is over, instead of staying where it ended.
            if (entity.GetBehavior<EntityBehaviorFactionMember>()?.Fighting(now) == true) { foughtSincePost = true; return false; }
            if (foughtSincePost)
            {
                foughtSincePost = false;
                if (dx * dx + dz * dz > (BackToPostWithin + 1) * (BackToPostWithin + 1))
                {
                    Vec3d home = new Vec3d(px, py, pz);
                    patrolling = true;
                    returning = true;
                    goalAtStart = PatrolSpot(home, BackToPostWithin) ?? home;
                    nextPatrolMs = now + PatrolPauseMinMs;
                    return true;
                }
            }
            bool outside = dx * dx + dz * dz > radius * radius || Math.Abs(entity.Pos.Y - py) > radius;
            if (!outside && now < nextPatrolMs) return false;
            Vec3d post = new Vec3d(px, py, pz);
            Vec3d spot = PatrolSpot(post, radius * (outside ? ReturnShare : PatrolShare)) ?? (outside ? post : null);
            if (spot == null)
            {
                nextPatrolMs = now + 3000;
                return false;
            }
            patrolling = true;
            returning = outside;
            goalAtStart = spot;
            return true;
        }

        // A random spot to stand on within reach blocks of the post, or null if a few tries find no ground.
        Vec3d PatrolSpot(Vec3d post, double reach)
        {
            Random rand = entity.World.Rand;
            IBlockAccessor ba = entity.World.BlockAccessor;
            for (int i = 0; i < 6; i++)
            {
                // Evenly over the disc, not bunched in the middle.
                double r = reach * Math.Sqrt(rand.NextDouble()), a = rand.NextDouble() * 2 * Math.PI;
                double x = post.X + r * Math.Cos(a), z = post.Z + r * Math.Sin(a);
                if (Ground.StandHeight(ba, x, post.Y + 0.5, z, entity.Pos.Dimension, 12) is double y) return new Vec3d(x, y, z);
            }
            return null;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            seconds = 0;
            stuck = false;
            running = false;
            bool run = MayRun && Distance(goalAtStart) > RunBeyond;
            // Without a path it walks straight there (WaypointsTraverser does that when no onNoPath is given).
            pathTraverser.NavigateTo_Async(goalAtStart.Clone(), run ? runSpeed : walkSpeed, InFormation ? 0.25f : Exact ? 0.6f : 2f, OnGoalReached, OnStuck, null, 2000, 1);
            SetRunning(run);
        }

        // A patrol is walked; everything else is run when far behind.
        bool MayRun => !patrolling || returning;

        public override bool CanContinueExecute() => pathTraverser.Ready;

        public override bool ContinueExecute(float dt)
        {
            seconds += dt;
            Vec3d goal = Goal();
            if (goal == null || stuck) return false;
            // The owner moved on: aim for where they (or their place in the formation) are now.
            if (pathTraverser.CurrentTarget is Vec3d target)
            {
                if (goalAtStart.SquareDistanceTo(goal) > 3 * 3)
                {
                    goalAtStart = goal;
                    pathTraverser.Retarget();
                }
                target.Set(goal.X, goal.Y, goal.Z);
            }
            double distance = Distance(goal);
            bool run = MayRun && (distance > RunBeyond || (running && distance > WalkWithin));
            if (run != running) SetRunning(run);
            if (distance < StopDistance) return false;
            return pathTraverser.Active || seconds < 2;
        }

        // Changes pace on the way: the traverser keeps the speed it was started with, so it is set directly.
        void SetRunning(bool run)
        {
            float speed = run ? runSpeed : walkSpeed;
            SpeedField?.SetValue(pathTraverser, speed);
            if (pathTraverser is Vintagestory.Essentials.WaypointsTraverser) NewSpeedField?.SetValue(pathTraverser, speed);
            if (runAnim != null)
            {
                if (run && !running) entity.AnimManager.StartAnimation(runAnim);
                if (!run && running) entity.AnimManager.StopAnimation(runAnim.Code);
            }
            running = run;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            pathTraverser.Stop();
            if (running && runAnim != null) entity.AnimManager.StopAnimation(runAnim.Code);
            running = false;
            long now = entity.World.ElapsedMilliseconds;
            if (patrolling) nextPatrolMs = now + entity.World.Rand.Next(PatrolPauseMinMs, PatrolPauseMaxMs + 1);
            patrolling = returning = false;
            if (stuck) nextCheckMs = now + 3000;
        }

        void OnStuck() => stuck = true;
        void OnGoalReached() { }
    }
}
