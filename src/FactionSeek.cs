using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>Vanilla seek, but the target is chosen by faction instead of entity code, guards stay near their post,
    /// and battles spread out (0.0.53): an NPC skips enemy NPCs that already have enough attackers, and goes to its
    /// own side of its target instead of onto the target's position.</summary>
    public class AiTaskFactionSeek : AiTaskSeekEntity
    {
        readonly FactionSystem factions;
        readonly string faction;
        EntityBehaviorFactionMember member;
        bool memberLookedUp;
        // Where this NPC stands around its target (block coordinates, Y as InternalY), worked out every SpotSeconds.
        readonly Vec3d spot = new Vec3d();
        bool surrounding;
        // 0.0.82: the spot is a battle line slot (stand there) rather than a place around the target (stand anywhere in
        // the duel band near it), and the angle of that place.
        bool slotSpot;
        double spotAngle;
        // 0.0.87: the place is in the outer ring (waiting to step in), not the duel band.
        bool spotOuter;
        /// <summary>How far, in radians, an attacker in the duel band may stand from its place's angle and still stay put
        /// (0.0.82: it no longer walks to an exact point, which made it step in and out).</summary>
        public const double BandAngleSlack = 0.6;
        /// <summary>How far in or out from the outer ring's radius a waiting attacker may stand (0.0.87).</summary>
        public const double OuterSlack = 0.5;
        float sinceSpot;
        const float SpotSeconds = 0.5f;
        /// <summary>Close enough to its spot to stop walking and let the melee task fight.</summary>
        public const double AtSpotDistance = 0.65;

        // Archers (0.0.55): stop this far from a target they can shoot at, instead of walking up to it, and never take
        // a place around it. 0 for everyone else.
        readonly double standOff;
        AiTaskFactionShoot shoot;
        bool shootLookedUp;

        public AiTaskFactionSeek(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            factions = entity.Api.ModLoader.GetModSystem<FactionSystem>();
            faction = FactionSystem.FactionOf(entity);
            standOff = taskConfig["standOff"].AsDouble(0);
        }

        AiTaskFactionShoot Shoot
        {
            get
            {
                if (!shootLookedUp)
                {
                    shoot = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.AllTasks?.OfType<AiTaskFactionShoot>().FirstOrDefault();
                    shootLookedUp = true;
                }
                return shoot;
            }
        }

        /// <summary>An archer close enough to shoot, with a clear shot, stays where it is. 0.0.105 (owner, 2026-11-09:
        /// archers "end up jumping off walls in order to enter melee combat"): so does one standing more than
        /// ArcherHoldHeight blocks above its target within standOff, shot or not: it keeps its height and waits for a
        /// shot (or for the enemy to come up, when its melee fights as before) instead of going down to it.</summary>
        bool CanShootFromHere(Entity target)
        {
            if (standOff <= 0 || target == null || Shoot == null) return false;
            double dx = target.Pos.X - entity.Pos.X, dz = target.Pos.Z - entity.Pos.Z;
            if (dx * dx + dz * dz > standOff * standOff) return false;
            return HoldsHeight(entity.Pos.Y - target.Pos.Y) || Shoot.ClearShot(target);
        }

        public const double ArcherHoldHeight = 2;
        public static bool HoldsHeight(double above) => above > ArcherHoldHeight;

        // Recruits choose as RecruitWantsToAttack says, guards only within their leash, so the search picks the
        // nearest target they may chase. Without a faction, vanilla rules apply. The cheap checks (alive, range, game
        // mode) come first, the crowd check last.
        public override bool IsTargetableEntity(Entity e, float range)
        {
            // 0.0.94 (owner: not "through walls", chose A): only an enemy it can see, checked after the cheap rules.
            if (factions != null && Recruit.OwnerUidOf(entity) is string owner) return e.Alive && CanSense(e, range) && factions.RecruitWantsToAttack(entity, owner, e) && !BeyondLeash(e) && CanSee(e) && NotCrowded(e) && ArcherFirst(e);
            return faction == null || factions == null ? base.IsTargetableEntity(e, range) : e.Alive && CanSense(e, range) && factions.WantsToAttack(faction, e) && CanSee(e) && NotCrowded(e) && ArcherFirst(e);
        }

        // 0.0.151 (owner, 2026-11-16: riders "a harassing unit for archers"): a mounted NPC passes over a free enemy that
        // isn't an archer, remembering the nearest, which ShouldExecute takes when no archer turns up.
        Entity nonArcherFallback;
        double nonArcherFallbackDistSq;
        bool ArcherFirst(Entity e)
        {
            if (!Riders.PassesOver((entity as EntityAgent)?.MountedOn != null, Recruit.IsArcher(e))) return true;
            double d = e.Pos.SquareDistanceTo(entity.Pos);
            // 0.0.161: a rider's seek reaches 36 blocks for archers; anyone else only within the usual 24.
            if (d > Riders.NonArcherSeekRange * Riders.NonArcherSeekRange) return false;
            if (nonArcherFallback == null || d < nonArcherFallbackDistSq) { nonArcherFallback = e; nonArcherFallbackDistSq = d; }
            return false;
        }

        /// <summary>Lines of sight (0.0.94): a target is picked only when seen (Sight.CanSee); once fighting, one out of
        /// sight is chased for LoseSightMs more (round corners, through doors), then given up, unless it hit this NPC
        /// in that time.</summary>
        public const int LoseSightMs = 5000;
        const float SightSeconds = 0.5f;
        long lastSeenMs;
        float sinceSight;
        bool CanSee(Entity e) => Sight.CanSee(entity, e, this);

        // 0.0.77 (owner, 2026-11-04: "Keep the cap of 3 for spreading out, but when there's no other enemy within
        // reach, the extra NPCs join in anyway"): a target that already has its share of attackers is passed over in
        // the search, but the nearest such one is remembered; if the search finds nothing else, ShouldExecute takes it.
        Entity crowdedFallback;
        double crowdedFallbackDistSq;
        // 0.0.82 (owner: "paired duels"; extras "wrap the flanks"): an enemy NPC someone else already fights, and that
        // isn't fighting this NPC back, is passed over too, and taken only when no free enemy is found.
        Entity pairedFallback;
        double pairedFallbackDistSq;
        // 0.0.87: an enemy whose inner ring is full is taken only when no enemy has room left.
        Entity fullFallback;
        double fullFallbackDistSq;
        bool NotCrowded(Entity e)
        {
            if (Crowded(e))
            {
                double d = e.Pos.SquareDistanceTo(entity.Pos);
                if (crowdedFallback == null || d < crowdedFallbackDistSq) { crowdedFallback = e; crowdedFallbackDistSq = d; }
                return false;
            }
            if (e is EntityPlayer || factions == null) return true;
            long now = entity.World.ElapsedMilliseconds;
            if (factions.Spacing.OthersOn(entity.EntityId, e.EntityId, now) == 0 || factions.Spacing.TargetOf(e.EntityId, now) == entity.EntityId) return true;
            double dp = e.Pos.SquareDistanceTo(entity.Pos);
            if (factions.Spacing.HasRoom(entity.EntityId, e.EntityId, now))
            {
                if (pairedFallback == null || dp < pairedFallbackDistSq) { pairedFallback = e; pairedFallbackDistSq = dp; }
            }
            else if (fullFallback == null || dp < fullFallbackDistSq) { fullFallback = e; fullFallbackDistSq = dp; }
            return false;
        }
        bool Ours => factions != null && (faction != null || Recruit.OwnerUidOf(entity) != null);
        public override bool IsTargetableEntityNoTagsNoAll(Entity e, float range) => Ours ? IsTargetableEntity(e, range) : base.IsTargetableEntityNoTagsNoAll(e, range);
        public override bool IsTargetableEntityNoTagsAll(Entity e, float range) => Ours ? IsTargetableEntity(e, range) : base.IsTargetableEntityNoTagsAll(e, range);

        // An enemy NPC that already has its share of attackers counts only for those already fighting it.
        bool Crowded(Entity e) => factions.Spacing.Crowded(entity.EntityId, e.EntityId, e is EntityPlayer, entity.World.ElapsedMilliseconds);

        // A guard chases nothing past its leash, not even to strike back, so nobody can lure it away from its post.
        // (0.0.77's 12-block limit around a formation follower's leader went in 0.0.84, owner: "remove the limit".)
        bool BeyondLeash(Entity target) =>
            factions != null && target != null && Recruit.OwnerUidOf(entity) != null
            && Recruit.OrderOf(entity) == Recruit.Guard && !factions.InGuardArea(entity, target, FactionSystem.GuardLeash);

        EntityBehaviorFactionMember Member
        {
            get
            {
                if (!memberLookedUp) { member = entity.GetBehavior<EntityBehaviorFactionMember>(); memberLookedUp = true; }
                return member;
            }
        }

        /// <summary>NPC targets get surrounded; players are approached the vanilla way (how hard players' fights are
        /// doesn't change).</summary>
        bool Surrounds(Entity target) => factions != null && Ours && target != null && !(target is EntityPlayer) && standOff <= 0;

        double Bearing(Entity target) => Math.Atan2(entity.Pos.X - target.Pos.X, entity.Pos.Z - target.Pos.Z);

        // Claims the target and works out this NPC's place around it. False when there is no usable place (the
        // NPC then walks at the target as vanilla does). 0.0.82: an NPC told by its battle line to stand in its slot
        // claims nothing and goes there instead.
        bool Place(Entity target, long now)
        {
            BattleSpacing spacing = factions.Spacing;
            if (factions.Lines.HoldSlot(entity, target, out double sx, out double sz))
            {
                spacing.Release(entity.EntityId);
                if (!StandY(sx, sz, out double sy)) return false;
                spot.Set(sx, sy, sz);
                slotSpot = true;
                return true;
            }
            slotSpot = false;
            double bearing = Bearing(target);
            spacing.Claim(entity.EntityId, target.EntityId, bearing, now);
            // 0.0.104: a front member of a line that has met the enemy fights from its own side, keeping the line.
            if (factions.Lines.HoldsFront(entity, out double fdx, out double fdz))
            {
                double front = Math.Atan2(-fdx, -fdz);
                BattleSpacing.SpotAt(target.Pos.X, target.Pos.Z, front, spacing.SurroundRadius, out double fx, out double fz);
                if (EntityBehaviorFactionMember.CanStepTo(entity.World.BlockAccessor, fx, target.Pos.InternalY, fz))
                {
                    spotOuter = false;
                    spot.Set(fx, target.Pos.InternalY, fz);
                    spotAngle = front;
                    return true;
                }
            }
            if (!spacing.SurroundAngle(entity.EntityId, target.EntityId, bearing, now, out double angle, out bool inner)) return false;
            spotOuter = !inner;
            BattleSpacing.SpotAt(target.Pos.X, target.Pos.Z, angle, inner ? spacing.SurroundRadius : spacing.OuterRadius, out double x, out double z);
            if (!EntityBehaviorFactionMember.CanStepTo(entity.World.BlockAccessor, x, target.Pos.InternalY, z)) return false;
            spot.Set(x, target.Pos.InternalY, z);
            spotAngle = angle;
            return true;
        }

        // A slot's standing height: the NPC's own level, or one block up or down.
        bool StandY(double x, double z, out double y)
        {
            IBlockAccessor blocks = entity.World.BlockAccessor;
            foreach (double dy in new[] { 0.0, 1.0, -1.0 })
            {
                y = Math.Floor(entity.Pos.InternalY) + dy;
                if (EntityBehaviorFactionMember.CanStepTo(blocks, x, y, z)) return true;
            }
            y = entity.Pos.InternalY;
            return false;
        }

        // In a slot: within AtSpotDistance of it. Fighting (0.0.82): anywhere in the duel band on its own side of the
        // target, so small moves of the target don't set it walking again.
        bool AtSpot()
        {
            if (Math.Abs(entity.Pos.InternalY - spot.Y) >= 1.5) return false;
            if (slotSpot || targetEntity == null)
            {
                double dx = entity.Pos.X - spot.X, dz = entity.Pos.Z - spot.Z;
                return dx * dx + dz * dz < AtSpotDistance * AtSpotDistance;
            }
            double tx = entity.Pos.X - targetEntity.Pos.X, tz = entity.Pos.Z - targetEntity.Pos.Z;
            double dist = Math.Sqrt(tx * tx + tz * tz);
            if (spotOuter ? Math.Abs(dist - factions.Spacing.OuterRadius) > OuterSlack : !factions.Spacing.InDuelBand(dist)) return false;
            return Math.Abs(GameMath.AngleRadDistance((float)spotAngle, (float)Math.Atan2(tx, tz))) <= BandAngleSlack;
        }

        // Striking back skips IsTargetableEntity in vanilla's seek, so the leash is checked here too. An NPC already
        // standing in its place doesn't start walking again; the melee task does the fighting from there.
        public override bool ShouldExecute()
        {
            crowdedFallback = null;
            pairedFallback = null;
            fullFallback = null;
            nonArcherFallback = null;
            long searchedBefore = lastSearchTotalMs;
            bool found = base.ShouldExecute();
            // The search ran (lastSearchTotalMs moved) and found no free target: take one already being fought (0.0.82),
            // or else join a full one (0.0.77), except in a battle line, where the rest wait behind (owner: never more
            // than 2 on one enemy).
            if (!found && targetEntity == null && lastSearchTotalMs != searchedBefore)
            {
                Entity paired = pairedFallback != null && pairedFallback.Alive ? pairedFallback : null;
                Entity full = fullFallback != null && fullFallback.Alive ? fullFallback : null;
                Entity crowded = crowdedFallback != null && crowdedFallback.Alive && !(factions?.Lines.Wants(entity, crowdedFallback) ?? false) ? crowdedFallback : null;
                // 0.0.152 (in game: a third rider fought a warrior for 22 swings while the archers were taken): a rider
                // joins an archer someone else already fights before it takes a free enemy that isn't one.
                Entity fallback = (entity as EntityAgent)?.MountedOn != null ? (Recruit.IsArcher(paired) ? paired : Recruit.IsArcher(full) ? full : Recruit.IsArcher(crowded) ? crowded : null) : null;
                fallback = fallback ?? (nonArcherFallback != null && nonArcherFallback.Alive ? nonArcherFallback : null) ?? paired ?? full ?? crowded;
                if (fallback != null)
                {
                    targetEntity = fallback;
                    targetPos = targetEntity.Pos.XYZ;
                    found = entity.Pos.SquareDistanceTo(targetPos) > MinDistanceToTarget();
                }
            }
            crowdedFallback = null;
            pairedFallback = null;
            fullFallback = null;
            nonArcherFallback = null;
            if (!found || BeyondLeash(targetEntity)) return false;
            long now = entity.World.ElapsedMilliseconds;
            Member?.MarkFighting(now);
            if (CanShootFromHere(targetEntity)) return false;
            if (!Surrounds(targetEntity)) { factions?.Lines.Leave(entity.EntityId); return true; }
            factions.Lines.Join(entity, targetEntity, now);
            return !(Place(targetEntity, now) && AtSpot());
        }

        public override void StartExecute()
        {
            long now = entity.World.ElapsedMilliseconds;
            surrounding = false;
            sinceSpot = 0;
            if (Surrounds(targetEntity))
            {
                factions.Lines.Join(entity, targetEntity, now);
                surrounding = Place(targetEntity, now);
                if (surrounding) targetPos = spot.Clone();
            }
            else factions?.Spacing.Release(entity.EntityId);
            CallIdleAllies(now);
            Member?.MarkFighting(now);
            lastSeenMs = now;
            sinceSight = 0;
            base.StartExecute();
            // 0.0.82: walking to a battle line slot, vanilla must not re-aim the path at the enemy (it does in direct
            // attack mode every 0.75 s); ContinueExecute keeps the path on the slot instead.
            if (surrounding && slotSpot) attackPattern = EnumAttackPattern.CircleTarget;
        }

        // 0.0.123 (owner, 2026-11-13: "Sure" to "when an NPC starts fighting, idle allies within about 16 blocks walk
        // over to join, with no lines"): in the 0.0.122 run 14 of 38 wild orcs never noticed a battle 24+ blocks away.
        // A wild NPC that goes for a target calls the idle wild NPCs of its faction within JoinFightRange (0.0.124; CallForHelpRange in 0.0.123); they come
        // without the sight and crowd checks (their own search sorts out the crowding once there), and call in turn, so
        // a whole army joins in hops. At most once per CallAlliesEveryMs per NPC.
        public const long CallAlliesEveryMs = 5000;
        long lastCallMs = -CallAlliesEveryMs;
        readonly List<Entity> called = new List<Entity>();

        /// <summary>Does this ally answer a wild NPC's call to join its fight: alive, wild, same faction, not fighting.</summary>
        public static bool AnswersCall(Entity caller, Entity ally, long now) =>
            ally != null && caller != null && ally != caller && ally.Alive && ally is EntityAgent && !(ally is EntityPlayer)
            && Recruit.OwnerUidOf(ally) == null && FactionSystem.FactionOf(ally) is string f && f == FactionSystem.FactionOf(caller)
            && (ally.World == null || ally.GetBehavior<EntityBehaviorFactionMember>()?.Fighting(now) != true); // no world: test entities

        void CallIdleAllies(long now)
        {
            if (factions == null || targetEntity == null || !targetEntity.Alive || Recruit.OwnerUidOf(entity) != null || now - lastCallMs < CallAlliesEveryMs) return;
            // 0.0.129: no "already fighting" check here. ShouldExecute marks the NPC fighting before StartExecute runs, so
            // that check stopped every NPC that found its own target from calling (only called-in ones called on), which
            // is why 14 of 38 orcs still sat out in the 0.0.124 run. The per-NPC CallAlliesEveryMs limit and AnswersCall
            // (only allies that aren't fighting) keep the calls few.
            lastCallMs = now;
            float range = factions.Config.JoinFightRange;
            if (range <= 0) return;
            called.Clear();
            foreach (Entity e in entity.World.GetEntitiesAround(entity.Pos.XYZ, range, range / 2, e => AnswersCall(entity, e, now))) called.Add(e);
            foreach (Entity ally in called) ally.Notify("joinFight", targetEntity);
        }

        public override bool ContinueExecute(float dt)
        {
            if (!base.ContinueExecute(dt) || BeyondLeash(targetEntity)) return false;
            long now = entity.World.ElapsedMilliseconds;
            Member?.MarkFighting(now);
            if (Ours && targetEntity != null)
            {
                sinceSight += dt;
                if (sinceSight >= SightSeconds)
                {
                    sinceSight = 0;
                    if (CanSee(targetEntity)) lastSeenMs = now;
                    else if (now - lastSeenMs > LoseSightMs && !(attackedByEntity == targetEntity && RecentlyAttacked)) return false;
                }
            }
            if (standOff > 0)
            {
                sinceSpot += dt;
                if (sinceSpot >= SpotSeconds) { sinceSpot = 0; if (CanShootFromHere(targetEntity)) return false; }
            }
            if (!surrounding) return true;
            sinceSpot += dt;
            if (sinceSpot >= SpotSeconds)
            {
                sinceSpot = 0;
                // The place follows the target (and the others around it) twice a second, not every tick.
                bool wasSlot = slotSpot;
                if (!Place(targetEntity, now)) { surrounding = false; attackPattern = EnumAttackPattern.DirectAttack; return true; }
                // From slot to fighting or back: start again, so the path and mode fit (0.0.82).
                if (wasSlot != slotSpot) return false;
                // A marching line moves the slot: follow it.
                if (slotSpot && targetPos.SquareDistanceTo(spot) > 0.25)
                {
                    targetPos.Set(spot);
                    pathTraverser.NavigateTo(targetPos, moveSpeed, 0.3f, NoOp, NoOp);
                }
            }
            if (AtSpot()) return false;
            // Vanilla points the last waypoint at the target every tick; point it at this NPC's place instead.
            if (attackPattern == EnumAttackPattern.DirectAttack && pathTraverser.Ready)
            {
                Vec3d goal = pathTraverser.CurrentTarget;
                goal.X = spot.X;
                goal.Y = spot.Y;
                goal.Z = spot.Z;
            }
            return true;
        }

        static void NoOp() { }

        // A chase that ended away from the target (it fled, died, or the path failed) gives up the place.
        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            if (factions == null || targetEntity == null) return;
            // 0.0.87: from the outer ring, so an attacker waiting there keeps its place in the queue.
            double reach = factions.Spacing.OuterRadius + 1.5;
            double dx = entity.Pos.X - targetEntity.Pos.X, dz = entity.Pos.Z - targetEntity.Pos.Z;
            if (!targetEntity.Alive || dx * dx + dz * dz > reach * reach)
                factions.Spacing.Release(entity.EntityId);
        }

        // A call for help never turns a recruit on its own side, nor takes a guard past its leash, nor sends a fourth
        // NPC onto an enemy NPC that already has its share.
        public override bool Notify(string key, object data)
        {
            // 0.0.128 (owner, 2026-11-13: "the archers seem to rush into the melee even when they start at range"): every
            // ally starting a fight calls the others (joinFight, call for help), and each call restarted an archer's chase
            // for up to SpotSeconds before it noticed it could shoot from where it stood, so repeated calls walked it
            // into the melee. An archer that can already shoot the target from here stays put; its shoot task fights.
            if ((key == "joinLine" || key == "joinFight" || key == "seekEntity") && data is Entity heard && CanShootFromHere(heard)) return false;
            // 0.0.119: rallied into its side's battle line (BattleLineSystem.Rally), which already counted it in, so
            // no crowd or sight check: it takes its slot like any member. A recruit still never turns on its side.
            if ((key == "joinLine" || key == "joinFight") && data is Entity rallyTarget)
            {
                if (!rallyTarget.Alive || BeyondLeash(rallyTarget)) return false;
                if (Recruit.OwnerUidOf(entity) is string rallyOwner
                    && (Recruit.IsTeam(rallyTarget, rallyOwner) || (factions != null && !factions.RecruitWantsToAttack(entity, rallyOwner, rallyTarget)))) return false;
                // Vanilla ContinueExecute gives up on a target beyond NowSeekRange (seekingRange, 24 for warriors), which a
                // rallied NPC's target often is; the next search sets it back.
                NowSeekRange = Math.Max(NowSeekRange, (float)entity.Pos.DistanceTo(rallyTarget.Pos) + 8);
                return base.Notify("seekEntity", rallyTarget);
            }
            if (key == "seekEntity" && data is Entity target)
            {
                if (Recruit.OwnerUidOf(entity) is string owner
                    && (Recruit.IsTeam(target, owner) || (factions != null && !factions.RecruitWantsToAttack(entity, owner, target)) || BeyondLeash(target))) return false;
                // 0.0.136: a charge goes for its target whoever else is on it, seen or not.
                bool ordered = factions?.Hiring != null && factions.Hiring.Charging(entity);
                if (!ordered && factions != null && Ours && Crowded(target)) return false;
                // 0.0.94: a call for help sends only those who can see the attacker.
                if (!ordered && factions != null && Ours && !CanSee(target)) return false;
            }
            return base.Notify(key, data);
        }
    }
}
