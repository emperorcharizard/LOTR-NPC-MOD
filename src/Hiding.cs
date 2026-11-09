using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// Lines of sight (0.0.94). Owner, 2026-11-05: "npcs seem to be trying to target npcs through walls, can that be
    /// changed to be enemies like, not behind walls?" (chose A: sight to pick a target, about 5 s of memory once
    /// fighting). Only blocks block sight, by their collision boxes (glass and leaves block, open doors and tall grass
    /// don't); other creatures never do.
    /// </summary>
    public static class Sight
    {
        /// <summary>A target this many blocks lower may also be seen from PeekAhead blocks toward it, as from the edge of
        /// a drop the NPC can step up to (0.0.94, owner: archers "not try at all on a cliffs edge").</summary>
        public const double PeekDrop = 2.5, PeekAhead = 1.5;
        /// <summary>How far in front of the archer's eye an arrow starts (Shoot), so a shot is judged from there.</summary>
        public const double ArrowAhead = 0.4;

        /// <summary>Nothing solid between the two points.</summary>
        public static bool Clear(IWorldAccessor world, IWorldIntersectionSupplier blocks, Vec3d from, Vec3d to)
        {
            BlockSelection bsel = null;
            EntitySelection esel = null;
            world.RayTraceForSelection(blocks, from, to, ref bsel, ref esel, null, e => false);
            return bsel == null;
        }

        /// <summary>The point dist blocks from p, level, toward (x, z).</summary>
        public static Vec3d Toward(Vec3d p, double x, double z, double dist)
        {
            double dx = x - p.X, dz = z - p.Z, len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6) return p.Clone();
            double step = Math.Min(dist, len);
            return new Vec3d(p.X + dx / len * step, p.Y, p.Z + dz / len * step);
        }

        /// <summary>Can the looker see the target's chest or eyes? A target well below may also be seen from PeekAhead
        /// blocks toward it (the NPC walks there; the edge guard keeps it from stepping over).</summary>
        public static bool CanSee(Entity looker, Entity target, IWorldIntersectionSupplier blocks)
        {
            if (looker == null || target == null || target.Pos.Dimension != looker.Pos.Dimension) return false;
            IWorldAccessor world = looker.World;
            Vec3d eye = Archery.Eye(looker), chest = Archery.Chest(target), head = Archery.Eye(target);
            if (Clear(world, blocks, eye, chest) || Clear(world, blocks, eye, head)) return true;
            if (target.Pos.Y > looker.Pos.Y - PeekDrop) return false;
            Vec3d peek = Toward(eye, target.Pos.X, target.Pos.Z, PeekAhead);
            return Clear(world, blocks, eye, peek) && (Clear(world, blocks, peek, chest) || Clear(world, blocks, peek, head));
        }
    }

    /// <summary>
    /// Flee and hide (0.0.94). Owner, 2026-11-05: "for units that are being damaged but cannot reach their attacker,
    /// have them flee and hide behind cover"; "yes for recruits". An NPC hit from beyond melee reach by an enemy it
    /// can't get to (much higher or lower, or a second hit while chasing it has not closed the gap) runs to the nearest
    /// spot within SearchRadius the attacker can't see, or away from it when there is none, and stays out of sight until
    /// StayMs pass without a hit. An attacker that comes within reach is fought. An archer with a clear shot back shoots
    /// instead. Recruits stay near their leader (12 blocks), their hold spot (8) or inside their guard area.
    /// </summary>
    public class AiTaskFactionHide : AiTaskBaseTargetable
    {
        public const double MeleeReach = 3.5, UnreachableHeight = 3, FollowerLeash = 12, HoldLeash = 8, CloseGap = 1.0;
        public const int HitWindowMs = 8000, ResearchMs = 2000;
        static readonly double[] Rings = { 3, 5, 7, 9, 12, 16 };
        const int Angles = 16;

        readonly FactionSystem factions;
        readonly float moveSpeed;
        readonly double searchRadius;
        readonly int stayMs;
        Entity threat;
        long lastHitMs, lastSearchMs;
        double distAtLastHit;
        bool wantHide, fleeing, arrived;
        Vec3d goal;
        EntityBehaviorFactionMember member;
        AiTaskFactionShoot shoot;
        bool lookedUp;

        public AiTaskFactionHide(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            factions = entity.Api.ModLoader.GetModSystem<FactionSystem>();
            moveSpeed = taskConfig["movespeed"].AsFloat(0.04f);
            searchRadius = taskConfig["searchRadius"].AsDouble(16);
            stayMs = taskConfig["stayMs"].AsInt(10000);
        }

        void LookUp()
        {
            if (lookedUp) return;
            member = entity.GetBehavior<EntityBehaviorFactionMember>();
            shoot = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.AllTasks?.OfType<AiTaskFactionShoot>().FirstOrDefault();
            lookedUp = true;
        }

        static double Horizontal(Entity a, Entity b)
        {
            double dx = a.Pos.X - b.Pos.X, dz = a.Pos.Z - b.Pos.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Should a hit from this far away, at this height difference, send the NPC into hiding? hitBefore: the
        /// same attacker hit it within HitWindowMs, from distBefore away.</summary>
        public static bool Unreachable(double dist, double heightDiff, bool hitBefore, double distBefore) =>
            dist > MeleeReach && (Math.Abs(heightDiff) >= UnreachableHeight || (hitBefore && dist > distBefore - CloseGap));

        public override void OnEntityHurt(DamageSource source, float damage)
        {
            base.OnEntityHurt(source, damage);
            if (factions == null || damage <= 0 || source?.Type == EnumDamageType.Heal) return;
            Entity attacker = source.GetCauseEntity();
            if (attacker == null || attacker == entity || !attacker.Alive || attacker.Pos.Dimension != entity.Pos.Dimension) return;
            LookUp();
            long now = entity.World.ElapsedMilliseconds;
            double dist = Horizontal(entity, attacker);
            bool hitBefore = threat == attacker && now - lastHitMs < HitWindowMs;
            bool hide = Unreachable(dist, attacker.Pos.Y - entity.Pos.Y, hitBefore, distAtLastHit)
                && !(shoot != null && shoot.CanShootAt(attacker));
            threat = attacker;
            lastHitMs = now;
            distAtLastHit = dist;
            if (hide) wantHide = true;
        }

        bool ThreatGone(long now) =>
            threat == null || !threat.Alive || threat.Pos.Dimension != entity.Pos.Dimension || now - lastHitMs > stayMs;

        bool ThreatInReach() =>
            Horizontal(entity, threat) <= MeleeReach && Math.Abs(threat.Pos.Y - entity.Pos.Y) < 2;

        public override bool ShouldExecute()
        {
            if (!wantHide) return false;
            long now = entity.World.ElapsedMilliseconds;
            if (ThreatGone(now) || ThreatInReach()) { wantHide = false; return false; }
            if (now - lastSearchMs < ResearchMs) return false;
            lastSearchMs = now;
            goal = FindCover(out fleeing);
            if (goal == null) wantHide = false;
            return goal != null;
        }

        /// <summary>May a recruit stand here (wild NPCs anywhere)?</summary>
        bool Allowed(double x, double y, double z)
        {
            if (Recruit.OwnerUidOf(entity) == null) return true;
            string order = Recruit.OrderOf(entity);
            if (order == Recruit.Guard || order == Recruit.Hold)
            {
                if (!Recruit.TryGetPost(entity, out double px, out double py, out double pz)) return true;
                double r = order == Recruit.Guard ? factions.GuardRadius : HoldLeash, dx = x - px, dz = z - pz;
                return dx * dx + dz * dz <= r * r && Math.Abs(y - py) <= r;
            }
            Entity leader = Recruit.OnlineLeader(entity);
            if (leader == null || leader.Pos.Dimension != entity.Pos.Dimension) return true;
            double lx = x - leader.Pos.X, lz = z - leader.Pos.Z;
            return lx * lx + lz * lz <= FollowerLeash * FollowerLeash;
        }

        /// <summary>The nearest place within reach the threat can't see; else a point away from it; null if neither.</summary>
        Vec3d FindCover(out bool flee)
        {
            flee = false;
            IBlockAccessor blocks = entity.World.BlockAccessor;
            Vec3d eye = Archery.Eye(threat);
            double height = entity.SelectionBox?.Y2 ?? 1.8f, eyeHeight = entity.LocalEyePos.Y;
            double nowDist = Horizontal(entity, threat);
            Vec3d best = null;
            double bestScore = double.MaxValue;
            foreach (double r in Rings)
            {
                if (r > searchRadius) break;
                for (int k = 0; k < Angles; k++)
                {
                    double a = k * GameMath.TWOPI / Angles, x = entity.Pos.X + Math.Cos(a) * r, z = entity.Pos.Z + Math.Sin(a) * r;
                    if (!StandY(blocks, x, z, out double y) || !Allowed(x, y, z)) continue;
                    double tx = x - threat.Pos.X, tz = z - threat.Pos.Z, toThreat = Math.Sqrt(tx * tx + tz * tz);
                    if (toThreat <= MeleeReach) continue;
                    // Hidden: neither its chest nor its eyes in the threat's sight.
                    if (Sight.Clear(entity.World, this, eye, new Vec3d(x, y + height * 0.62, z)) || Sight.Clear(entity.World, this, eye, new Vec3d(x, y + eyeHeight, z))) continue;
                    // Nearest first; a place closer to the threat than now costs extra.
                    double score = r + 0.5 * Math.Max(0, nowDist - toThreat);
                    if (score < bestScore) { bestScore = score; best = new Vec3d(x, y, z); }
                }
                if (best != null) break;
            }
            if (best != null) return best;
            Vec3d away = Archery.AwayFrom(entity.Pos.XYZ, threat.Pos.XYZ, Math.Min(12, searchRadius));
            if (away == null || !StandY(blocks, away.X, away.Z, out double ay) || !Allowed(away.X, ay, away.Z)) return null;
            flee = true;
            return new Vec3d(away.X, ay, away.Z);
        }

        // A standing height near the NPC's own (two blocks up or down at most).
        bool StandY(IBlockAccessor blocks, double x, double z, out double y)
        {
            foreach (double dy in new[] { 0.0, 1.0, -1.0, 2.0, -2.0 })
            {
                y = Math.Floor(entity.Pos.InternalY) + dy;
                if (EntityBehaviorFactionMember.CanStepTo(blocks, x, y, z)) return true;
            }
            y = 0;
            return false;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            arrived = false;
            member?.MarkFighting(entity.World.ElapsedMilliseconds);
            if (!pathTraverser.NavigateTo(goal, moveSpeed, 0.5f, OnArrived, OnArrived)) pathTraverser.WalkTowards(goal, moveSpeed, 0.5f, OnArrived, OnArrived);
        }

        void OnArrived() => arrived = true;

        public override bool ContinueExecute(float dt)
        {
            long now = entity.World.ElapsedMilliseconds;
            if (ThreatGone(now) || ThreatInReach()) return false;
            member?.MarkFighting(now);
            if (!arrived && pathTraverser.Active) return true;
            if (!arrived) arrived = true;
            if (animMeta != null) entity.AnimManager.StopAnimation(animMeta.Code);
            // Found and hit again: look for better cover.
            if (now - lastHitMs < 500 && now - lastSearchMs >= ResearchMs)
            {
                lastSearchMs = now;
                Vec3d next = FindCover(out bool flee);
                if (next != null && next.SquareDistanceTo(goal) > 1)
                {
                    goal = next;
                    fleeing = flee;
                    arrived = false;
                    if (animMeta != null) entity.AnimManager.StartAnimation(animMeta);
                    if (!pathTraverser.NavigateTo(goal, moveSpeed, 0.5f, OnArrived, OnArrived)) pathTraverser.WalkTowards(goal, moveSpeed, 0.5f, OnArrived, OnArrived);
                }
            }
            return true;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            pathTraverser.Stop();
            long now = entity.World.ElapsedMilliseconds;
            if (ThreatGone(now) || ThreatInReach()) wantHide = false;
        }
    }
}
