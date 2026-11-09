using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// Archers (0.0.55, user, 2026-11-03: "creating archer units", then "bring up archer units, primarily for mordor, up
    /// to the current spec that the npcs are at right now" and "The archers are just an additional unit, they don't have
    /// ranks"). Shared pieces of the shooting task, the arrow and the seek task's stand-off. Pure math is static so the
    /// tests can check it without a world.
    /// </summary>
    public static class Archery
    {
        /// <summary>Until 0.0.77: how close to the line of fire a friend could stand before the archer held its shot (blocks from the
        /// arrow's path to the friend's middle).</summary>
        public const double FriendClearance = 0.75;

        public static Vec3d Eye(Entity e) => new Vec3d(e.Pos.X, e.Pos.InternalY + e.LocalEyePos.Y, e.Pos.Z);

        /// <summary>Where an archer aims: the chest (user: no head shots), a little above the middle of the body.</summary>
        public static Vec3d Chest(Entity e) => new Vec3d(e.Pos.X, e.Pos.InternalY + (e.SelectionBox?.Y2 ?? 1.8f) * 0.62, e.Pos.Z);

        /// <summary>Shortest distance from point p to the segment a-b, and where along it (0 at a, 1 at b).</summary>
        public static double DistanceToSegment(double ax, double ay, double az, double bx, double by, double bz, double px, double py, double pz, out double t)
        {
            double dx = bx - ax, dy = by - ay, dz = bz - az;
            double len2 = dx * dx + dy * dy + dz * dz;
            t = len2 <= 1e-9 ? 0 : ((px - ax) * dx + (py - ay) * dy + (pz - az) * dz) / len2;
            double tc = Math.Clamp(t, 0, 1);
            double cx = ax + dx * tc - px, cy = ay + dy * tc - py, cz = az + dz * tc - pz;
            return Math.Sqrt(cx * cx + cy * cy + cz * cz);
        }

        /// <summary>True when one of the given friends stands in the line of fire from 'from' to 'to' (between the two,
        /// within FriendClearance of the path). Each friend is (x, middle y, z).</summary>
        public static bool FriendInLine(Vec3d from, Vec3d to, IEnumerable<Vec3d> friends)
        {
            foreach (Vec3d f in friends)
            {
                double d = DistanceToSegment(from.X, from.Y, from.Z, to.X, to.Y, to.Z, f.X, f.Y, f.Z, out double t);
                if (t > 0.02 && t < 1.02 && d < FriendClearance) return true;
            }
            return false;
        }

        /// <summary>
        /// The launch velocity (blocks per tick, as Pos.Motion) for a low arc from start to target at the given speed
        /// (blocks per second) under the given gravity (blocks per second squared). Vanilla's AiTaskShootAtEntityR math
        /// without OpenTK. False when the target is out of reach at that speed.
        /// </summary>
        public static bool SolveArc(Vec3d start, Vec3d target, double speedPerSecond, double gravityPerSecond2, out Vec3d velocity)
        {
            velocity = new Vec3d();
            // Per "minute" units as vanilla: speed / 60 per tick, gravity / 60 per tick.
            double speed = speedPerSecond / 60.0;
            double g = gravityPerSecond2 / 60.0;
            double dx = target.X - start.X, dy = target.Y - start.Y, dz = target.Z - start.Z;
            double horizontal = Math.Sqrt(dx * dx + dz * dz);
            if (horizontal < 1e-6) { velocity.Set(0, Math.Sign(dy) * speed, 0); return true; }
            double s2 = speed * speed;
            double disc = s2 * s2 - g * (g * horizontal * horizontal + 2 * dy * s2);
            if (disc < 0) return false;
            double angle = Math.Atan2(s2 - Math.Sqrt(disc), g * horizontal);
            double h = speed * Math.Cos(angle);
            velocity.Set(dx / horizontal * h, speed * Math.Sin(angle), dz / horizontal * h);
            return true;
        }

        /// <summary>Turns a velocity by random small angles (degrees), keeping its speed.</summary>
        public static Vec3d Scatter(Vec3d v, double yawDeg, double pitchDeg)
        {
            double speed = v.Length();
            if (speed < 1e-9) return v;
            double yaw = Math.Atan2(v.X, v.Z) + yawDeg * GameMath.DEG2RAD;
            double pitch = Math.Asin(Math.Clamp(v.Y / speed, -1, 1)) + pitchDeg * GameMath.DEG2RAD;
            double h = Math.Cos(pitch) * speed;
            return new Vec3d(Math.Sin(yaw) * h, Math.Sin(pitch) * speed, Math.Cos(yaw) * h);
        }

        /// <summary>
        /// Who an archer must never hit: players and faction NPCs it doesn't want to fight (its own side, allies,
        /// neutral factions, its owner's people). Arrows fly through them (since 0.0.77 the archer no longer holds its
        /// shot while one stands in the way). Animals and monsters aren't friends.
        /// </summary>
        public static bool IsFriend(FactionSystem factions, Entity shooter, Entity other)
        {
            if (factions == null || shooter == null || other == null || other == shooter || !other.Alive) return false;
            if (!(other is EntityPlayer) && FactionSystem.FactionOf(other) == null && Recruit.OwnerUidOf(other) == null) return false;
            if (Recruit.OwnerUidOf(shooter) is string owner) return Recruit.IsTeam(other, owner) || !factions.RecruitWantsToAttack(shooter, owner, other);
            string faction = FactionSystem.FactionOf(shooter);
            return faction != null && !factions.WantsToAttack(faction, other);
        }

        /// <summary>A spot about 'distance' blocks straight away from the threat, or null.</summary>
        public static Vec3d AwayFrom(Vec3d me, Vec3d threat, double distance)
        {
            double dx = me.X - threat.X, dz = me.Z - threat.Z;
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-3) return null;
            return new Vec3d(me.X + dx / len * distance, me.Y, me.Z + dz / len * distance);
        }
    }

    /// <summary>
    /// An archer's shot (lotrnpcs:shootattack). Targets are chosen by faction exactly as the melee and seek tasks choose
    /// them (recruits by their owner's rules, guards inside their area). The archer stops, turns, draws for drawMs,
    /// and looses one arrow aimed at the chest if the target is still in sight (friends in the way don't stop it since
    /// 0.0.77: the arrow flies through them); it
    /// steps back from an enemy that gets closer than minRange and lets the melee task fight when cornered.
    /// </summary>
    public class AiTaskFactionShoot : AiTaskBaseTargetable
    {
        readonly FactionSystem factions;
        readonly string faction, rank;
        readonly float range, verticalRange, minRange, damage, dispersionDeg;
        readonly int damageTier, drawMs, releaseMs, searchWaitMs;
        readonly EnumDamageType damageType;
        readonly double arrowSpeed, gravityFactor;
        readonly AssetLocation projectileCode, shootSound;
        readonly string releaseAnimation;
        long lastSearchMs;
        long shotAtMs;
        bool retreating;
        // After stepping back once, the archer stands and shoots (at closer range) for a while instead of backing off
        // again, so a faster chaser can't keep it walking backwards forever.
        long standUntilMs;
        const int StandMs = 4000;
        const double CloseShotRange = 1.8;
        // Closer than this the archer fights where it stands instead of stepping back (1.6 until 0.0.74, when minRange
        // went from 4 to 2 blocks and 1.6 would have left almost no room to step back).
        const double TooCloseToStep = 1.0;
        float curTurnRadPerSec;
        EntityBehaviorHeldWeapon weapon;
        EntityBehaviorFactionMember member;
        bool lookedUp;
        static bool warnedNoProjectile;

        public AiTaskFactionShoot(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            factions = entity.Api.ModLoader.GetModSystem<FactionSystem>();
            faction = FactionSystem.FactionOf(entity);
            rank = EntityBehaviorFactionMember.RankOf(entity);
            range = taskConfig["range"].AsFloat(24);
            // 0.0.74 (user, 2026-11-04: "increase their shooting range to 24 blocks, and up to 20 blocks above or below
            // them"): how far above or below a target may be. Until 0.0.74 it was 0.6 of the range (12 blocks).
            verticalRange = taskConfig["verticalRange"].AsFloat(20);
            minRange = taskConfig["minRange"].AsFloat(2);
            damage = taskConfig["damage"].AsFloat(3);
            damageTier = taskConfig["damageTier"].AsInt(8);
            string type = taskConfig["damageType"].AsString("PiercingAttack");
            damageType = Enum.TryParse(type, true, out EnumDamageType parsed) ? parsed : EnumDamageType.PiercingAttack;
            drawMs = taskConfig["drawMs"].AsInt(1200);
            releaseMs = taskConfig["releaseMs"].AsInt(450);
            searchWaitMs = taskConfig["searchWaitMs"].AsInt(750);
            arrowSpeed = taskConfig["arrowSpeed"].AsDouble(28);
            gravityFactor = taskConfig["gravityFactor"].AsDouble(0.75);
            dispersionDeg = taskConfig["dispersionDeg"].AsFloat(2.5f);
            projectileCode = new AssetLocation(taskConfig["projectileCode"].AsString("lotrnpcs:npcarrow"));
            string soundCode = taskConfig["shootSound"].AsString("game:sounds/bow-release");
            shootSound = string.IsNullOrEmpty(soundCode) ? null : new AssetLocation(soundCode);
            releaseAnimation = taskConfig["releaseAnimation"].AsString(null)?.ToLowerInvariant();
        }

        void LookUp()
        {
            if (lookedUp) return;
            weapon = entity.GetBehavior<EntityBehaviorHeldWeapon>();
            member = entity.GetBehavior<EntityBehaviorFactionMember>();
            lookedUp = true;
        }

        public override bool IsTargetableEntity(Entity e, float range)
        {
            if (factions == null) return faction == null && base.IsTargetableEntity(e, range);
            if (!e.Alive || !CanSense(e, range)) return false;
            if (Recruit.OwnerUidOf(entity) is string owner)
                return factions.RecruitWantsToAttack(entity, owner, e)
                    && !(Recruit.OrderOf(entity) == Recruit.Guard && !factions.InGuardArea(entity, e, FactionSystem.GuardLeash));
            return faction != null && factions.WantsToAttack(faction, e);
        }

        // Straight-line distance to the farthest target in range (24 out and 20 up is 31 blocks), for the sensing check.
        float Reach => (float)Math.Sqrt(range * range + verticalRange * verticalRange);

        double HorizontalDistance(Entity e)
        {
            double dx = e.Pos.X - entity.Pos.X, dz = e.Pos.Z - entity.Pos.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>True when the archer can see the target's chest with nothing solid in between. Friends in the way
        /// no longer stop the shot (0.0.77, owner: "remove the hold fire rule"): our arrows fly through the archer's
        /// friends without hurting them (LotrNpcsArrow.CanHitTarget), so holding only cost damage. Archery.FriendInLine
        /// is kept for the tests and in case the rule comes back.</summary>
        public bool ClearShot(Entity target)
        {
            if (target.Pos.Dimension != entity.Pos.Dimension) return false;
            // 0.0.94 (owner: archers "not try at all on a cliffs edge"): judged from where the arrow starts, ArrowAhead in
            // front of the eye toward the target, as Shoot spawns it. From the eye itself, a steep line down from just
            // behind a ledge clipped the ledge, so no target below was ever picked.
            Vec3d from = Sight.Toward(Archery.Eye(entity), target.Pos.X, target.Pos.Z, Sight.ArrowAhead), to = Archery.Chest(target);
            return Sight.Clear(entity.World, this, from, to);
        }

        /// <summary>In range with a clear shot (0.0.94: an archer that can shoot back at its attacker doesn't hide).</summary>
        public bool CanShootAt(Entity target) => target != null && target.Alive && InShootingRange(target) && ClearShot(target);

        bool InShootingRange(Entity e)
        {
            double d = HorizontalDistance(e);
            double nearest = entity.World.ElapsedMilliseconds < standUntilMs ? CloseShotRange : minRange;
            return d >= nearest && d <= range && Math.Abs(e.Pos.Y - entity.Pos.Y) <= verticalRange;
        }

        public override bool ShouldExecute()
        {
            long now = entity.World.ElapsedMilliseconds;
            if (IsOnCooldown() || !PreconditionsSatisfied()) return false;
            if (now - lastSearchMs < searchWaitMs) return false;
            lastSearchMs = now;
            Vec3d eye = Archery.Eye(entity);
            // An enemy too close: step back from it first (melee takes over if the archer is cornered).
            Entity close = now < standUntilMs ? null : entity.World.GetNearestEntity(eye, minRange, 2, e => e != entity && IsTargetableEntity(e, minRange));
            if (close != null)
            {
                targetEntity = close;
                retreating = HorizontalDistance(close) > TooCloseToStep && RetreatSpot(close) != null;
                return retreating;
            }
            retreating = false;
            Entity attacker = RecentlyAttacked ? attackedByEntity : null;
            if (attacker != null && attacker.Alive && InShootingRange(attacker) && !IgnoreDamageFrom(attacker) && ClearShot(attacker)) targetEntity = attacker;
            else targetEntity = entity.World.GetNearestEntity(eye, range, verticalRange, e => e != entity && InShootingRange(e) && IsTargetableEntity(e, Reach) && ClearShot(e));
            return targetEntity != null;
        }

        Vec3d RetreatSpot(Entity threat)
        {
            Vec3d away = Archery.AwayFrom(entity.Pos.XYZ, threat.Pos.XYZ, minRange + 2);
            if (away == null) return null;
            // The first step has to be possible, otherwise the archer is cornered.
            Vec3d first = Archery.AwayFrom(entity.Pos.XYZ, threat.Pos.XYZ, 1.2);
            if (!EntityBehaviorFactionMember.CanStepTo(entity.World.BlockAccessor, first.X, entity.Pos.InternalY, first.Z)) return null;
            return away;
        }

        public override void StartExecute()
        {
            LookUp();
            long now = entity.World.ElapsedMilliseconds;
            member?.MarkFighting(now);
            shotAtMs = 0;
            curTurnRadPerSec = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser.curTurnRadPerSec;
            if (retreating)
            {
                Vec3d spot = RetreatSpot(targetEntity);
                AnimationMetaData keep = animMeta;
                animMeta = null;
                base.StartExecute();
                animMeta = keep;
                if (spot == null || !pathTraverser.WalkTowards(spot, 0.035f, 0.5f, () => { }, () => { })) retreating = false;
                return;
            }
            pathTraverser.Stop();
            weapon?.Lower();
            base.StartExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            if (targetEntity == null || !targetEntity.Alive || targetEntity.Pos.Dimension != entity.Pos.Dimension) return false;
            long now = entity.World.ElapsedMilliseconds;
            member?.MarkFighting(now);
            long elapsed = now - ExecutionStartTimeMs;
            if (retreating) return elapsed < 2000 && pathTraverser.Active && HorizontalDistance(targetEntity) < minRange + 1.5;

            float desiredYaw = (float)Math.Atan2(targetEntity.Pos.X - entity.Pos.X, targetEntity.Pos.Z - entity.Pos.Z);
            float yawDist = GameMath.AngleRadDistance(entity.Pos.Yaw, desiredYaw);
            float turn = curTurnRadPerSec * dt * GlobalConstants.OverallSpeedMultiplier;
            entity.Pos.Yaw = (entity.Pos.Yaw + GameMath.Clamp(yawDist, -turn, turn)) % GameMath.TWOPI;

            if (shotAtMs == 0 && elapsed >= drawMs)
            {
                bool facing = Math.Abs(yawDist) < 20 * GameMath.DEG2RAD;
                if (facing && InShootingRange(targetEntity) && ClearShot(targetEntity))
                {
                    Shoot(targetEntity);
                    shotAtMs = now;
                }
                // The target ducked away or behind a block: give up after a moment and look again.
                else if (elapsed > drawMs + 1500) return false;
            }
            return shotAtMs == 0 || now - shotAtMs < releaseMs;
        }

        public override void FinishExecute(bool cancelled)
        {
            if (retreating)
            {
                pathTraverser.Stop();
                // Ready to shoot again straight away, and it holds its ground for a while.
                cooldownUntilMs = entity.World.ElapsedMilliseconds + 200;
                standUntilMs = entity.World.ElapsedMilliseconds + StandMs;
                retreating = false;
                return;
            }
            base.FinishExecute(cancelled);
            if (releaseAnimation != null) entity.AnimManager?.StopAnimation(releaseAnimation);
            weapon?.Raise();
        }

        void Shoot(Entity target)
        {
            EntityProperties type = entity.World.GetEntityType(projectileCode);
            if (type == null)
            {
                if (!warnedNoProjectile) entity.World.Logger.Warning("[lotrnpcs] Archer {0}: projectile {1} does not exist.", entity.Code, projectileCode);
                warnedNoProjectile = true;
                return;
            }
            Entity arrow = entity.World.ClassRegistry.CreateEntity(type);
            if (!(arrow is IProjectile projectile)) return;
            Vec3d from = Archery.Eye(entity).Ahead(0.4, 0, entity.Pos.Yaw);
            Vec3d to = Archery.Chest(target);
            double gravity = gravityFactor * GlobalConstants.GravityPerSecond;
            // Lead a moving target by its motion over the flight time, as vanilla does.
            double flight = from.DistanceTo(to) / arrowSpeed * 60;
            to.Add(target.Pos.Motion.X * flight, 0, target.Pos.Motion.Z * flight);
            double speed = arrowSpeed;
            Vec3d velocity = null;
            for (int i = 0; i < 10 && !Archery.SolveArc(from, to, speed, gravity, out velocity); i++) speed *= 1.1;
            if (velocity == null || velocity.Length() < 1e-6) return;
            double spread = dispersionDeg;
            velocity = Archery.Scatter(velocity, (entity.World.Rand.NextDouble() * 2 - 1) * spread, (entity.World.Rand.NextDouble() * 2 - 1) * spread);

            projectile.FiredBy = entity;
            factions?.Log.Attacked(entity, entity.World.ElapsedMilliseconds);
            // 0.0.91: the faction's archer damage, varied by DamageSpread, read at each shot (the tier stays).
            FactionConfig config = factions?.Config;
            projectile.Damage = FactionTiers.Spread(config?.DamageFor(faction, rank) ?? damage, config?.Spread ?? 0, entity.World.Rand.NextDouble());
            projectile.DamageTier = damageTier;
            projectile.DamageType = damageType;
            projectile.DropOnImpactChance = 0;
            projectile.IgnoreInvFrames = false;
            arrow.Pos.SetPosWithDimension(from);
            arrow.Pos.Motion.Set(velocity.X, velocity.Y, velocity.Z);
            arrow.World = entity.World;
            projectile.PreInitialize();
            // Arrows can't be picked up, so archers can't be farmed for them.
            projectile.Collectible = false;
            entity.World.SpawnPriorityEntity(arrow);
            if (shootSound != null) entity.World.PlaySoundAt(shootSound, entity, null, true, 24);
            if (releaseAnimation != null)
            {
                AnimationMetaData meta = Array.Find(entity.Properties.Client?.Animations ?? Array.Empty<AnimationMetaData>(), a => a.Code == releaseAnimation);
                if (animMeta != null) entity.AnimManager?.StopAnimation(animMeta.Code);
                if (meta != null) entity.AnimManager?.StartAnimation(meta);
            }
        }
    }

    /// <summary>
    /// An archer's arrow (entity class LotrNpcsArrow). A vanilla arrow, except that it never hits its archer's friends
    /// (it flies past them), and a player it hits is struck in the chest under Combat Overhaul, as the melee attack is.
    /// It can't be picked up and it disappears soon after landing (the despawn behavior).
    /// </summary>
    public class EntityNpcArrow : EntityProjectile
    {
        FactionSystem factions;

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            factions = api.ModLoader.GetModSystem<FactionSystem>();
            Collectible = false;
        }

        protected override bool CanHitTarget(Entity target)
        {
            if (!base.CanHitTarget(target)) return false;
            if (FiredBy == null) return true;
            if (target.EntityId == FiredBy.EntityId) return false;
            // The client doesn't know the factions' relations: there an arrow flies on past anyone with a side and the
            // server's position update settles it.
            if (Api?.Side == EnumAppSide.Client) return !(target is EntityPlayer) && FactionSystem.FactionOf(target) == null;
            return !Archery.IsFriend(factions, FiredBy, target);
        }

        protected override bool DealDamage(Entity target)
        {
            // 0.0.109: like melee, an arrow skips the hurt window of anything but a player with NpcHitsIgnoreHurtWindow
            // on, and the battle log hears how it went.
            if (target != null && !(target is EntityPlayer)) IgnoreInvFrames = factions?.Config.NpcHitsIgnoreHurtWindow == true;
            bool alive = target?.Alive == true;
            bool window = target?.IsActivityRunning("invulnerable") == true && !IgnoreInvFrames;
            bool received = Hit(target);
            if (factions?.Log.On == true && FiredBy != null && target != null)
                factions.Log.Attack(FiredBy, target, true, Damage, BattleLog.Classify(received, alive, window), alive && !target.Alive, target.World.ElapsedMilliseconds);
            return received;
        }

        bool Hit(Entity target)
        {
            if (!(target is EntityPlayer) || !ChestHitDamage.Available) return base.DealDamage(target);
            float amount = Damage;
            ApplyModifiers(ref amount, target);
            DamageSource source = ChestHitDamage.Create(DamageType, DamageTier);
            source.Source = EnumDamageSource.Entity;
            source.SourceEntity = this;
            source.CauseEntity = FiredBy;
            source.IgnoreInvFrames = IgnoreInvFrames;
            source.KnockbackStrength = Weight * (float)Pos.Motion.Length() * knockbackFactor;
            return target.ReceiveDamage(source, amount);
        }
    }
}
