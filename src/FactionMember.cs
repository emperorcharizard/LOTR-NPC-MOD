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
    /// <summary>
    /// Server behavior on every faction NPC: when something hurts it, its faction and allies hold a grudge against the
    /// attacker, and allied NPCs nearby come to help. It also heals NPCs faster out of combat, and brings NPCs from
    /// older versions up to date when they load.
    /// </summary>
    public class EntityBehaviorFactionMember : EntityBehavior
    {
        public const string TestPrefix = "Test: ";
        FactionSystem factions;
        long lastHurtMs = long.MinValue / 2;
        float sinceRegenCheck;
        // Battle spacing: until when this NPC counts as fighting, and the push away from NPCs standing too close,
        // worked out a few times a second from the entity partitions (no allocation: the callback is cached).
        public const long FightingMs = 3000;
        const float ScanSeconds = 0.25f, IdleScanSeconds = 1f;
        long fightingUntil;
        float sinceScan;
        double pushX, pushZ, scanSpacing, enemySpacing, brake, headX, headZ;
        bool lookAhead, scanFighting;
        // Neighbours within BattleSpacing.ContactScan at the last scan, kept out of this NPC's body every tick.
        readonly List<Entity> contacts = new List<Entity>(8);
        // The turn toward the NPC it fights, at most FaceRadPerSec, while standing within FaceRange of it.
        public const double FaceRange = 4, FaceRadPerSec = 6;
        long scanTarget;
        EntityPartitioning partitions;
        ActionConsumable<Entity> onNeighbor;

        public EntityBehaviorFactionMember(Entity entity) : base(entity) { }

        /// <summary>The seek and melee tasks call this while chasing or hitting something.</summary>
        public void MarkFighting(long now) => fightingUntil = now + FightingMs;
        public bool Fighting(long now) => now < fightingUntil;

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            if (entity.Api.Side != EnumAppSide.Server) return;
            factions = entity.Api.ModLoader.GetModSystem<FactionSystem>();
            StripTestName(entity);
            // The faction's health for this rank, so new NPCs spawn with it and older ones get it (keeping their
            // wounds) when they load.
            SyncMaxHealth(entity, ScaledMaxHealth(entity, factions?.Config));
        }

        /// <summary>The health this NPC should have: its faction's for its rank, else its type's.</summary>
        public static float ScaledMaxHealth(Entity entity, FactionConfig config)
        {
            float typeMax = TypeMaxHealth(entity.Properties);
            if (typeMax <= 0) return typeMax;
            float? own = config?.HealthFor(FactionSystem.FactionOf(entity), RankOf(entity));
            return own is float h ? Math.Max(1, FactionTiers.Round(h)) : typeMax;
        }

        /// <summary>The NPC's rank (recruit, warrior, captain, archer) in lower case, or null for rankless NPCs.</summary>
        public static string RankOf(Entity entity) => NpcTypeInfo.Of(entity)?.Rank?.Trim().ToLowerInvariant();

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);
            if (entity.Api.Side != EnumAppSide.Server) return;
            AiTaskManager tasks = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (tasks != null) tasks.OnShouldExecuteTask += AllowTask;
            // Guard and shock classes and the tormented elves were taken out: NPCs of them still in a world turn into
            // their plain rank, or go, shortly after loading.
            if (Promotion.IsRetired(NpcTypeInfo.Of(entity)?.Rank) || FactionConfig.IsRetiredFaction(FactionSystem.FactionOf(entity)))
                entity.World.RegisterCallback(_ => entity.Api.ModLoader.GetModSystem<PromotionSystem>()?.Retire(entity as EntityAgent), 1000);
        }

        // No wandering off or looking about in a fight: between swings, or waiting in a battle line, the look-around
        // task would turn NPCs away from their enemy.
        bool AllowTask(IAiTask task) =>
            !((task is AiTaskWander || task is AiTaskLookAround) && Fighting(entity.World.ElapsedMilliseconds));

        /// <summary>The "maxhealth" the entity type's health behavior gives, or 0.</summary>
        public static float TypeMaxHealth(EntityProperties properties) => NpcTypeInfo.Of(properties)?.MaxHealth ?? 0;

        /// <summary>Vanilla health keeps the max health an NPC spawned with, so after a change to the entity type
        /// NPCs already in a world are brought to the new value when they load. A wounded NPC keeps its wounds, and
        /// one at full health stays full. True if changed. Our behavior comes after "health" in the entity JSON, so
        /// the health tree exists by now.</summary>
        public static bool SyncMaxHealth(Entity entity, float typeMax)
        {
            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("health");
            if (tree == null || typeMax <= 0) return false;
            float stored = tree.GetFloat("basemaxhealth");
            if (stored <= 0 || Math.Abs(stored - typeMax) < 0.01f) return false;
            float oldMax = tree.GetFloat("maxhealth", stored);
            float health = tree.GetFloat("currenthealth", oldMax);
            float newMax = Math.Max(1, oldMax + typeMax - stored);
            tree.SetFloat("basemaxhealth", typeMax);
            tree.SetFloat("maxhealth", newMax);
            if (health > 0)
            {
                // Same wounds as before, but never killed or brought below 1 by the change.
                float lost = Math.Max(0, oldMax - health);
                tree.SetFloat("currenthealth", Math.Min(newMax, Math.Max(newMax - lost, Math.Min(health, 1))));
            }
            entity.WatchedAttributes.MarkPathDirty("health");
            return true;
        }

        /// <summary>NPCs spawned by very old versions keep the "Test: " name their name tag picked at spawn; drop
        /// it.</summary>
        public static bool StripTestName(Entity entity)
        {
            ITreeAttribute tag = entity.WatchedAttributes.GetTreeAttribute("nametag");
            string name = tag?.GetString("name");
            if (name == null || !name.StartsWith(TestPrefix, StringComparison.Ordinal)) return false;
            tag.SetString("name", name.Substring(TestPrefix.Length));
            entity.WatchedAttributes.MarkPathDirty("nametag");
            return true;
        }

        // Faster healing out of combat, through vanilla health's own "regenSpeed" multiplier, checked once a second.
        public override void OnGameTick(float deltaTime)
        {
            if (factions == null || !entity.Alive) return;
            Separate(deltaTime);
            Face(deltaTime);
            sinceRegenCheck += deltaTime;
            if (sinceRegenCheck < 1) return;
            sinceRegenCheck = 0;
            if (factions.Log.On) factions.Log.Tick(entity, Fighting(entity.World.ElapsedMilliseconds), 1, entity.World.ElapsedMilliseconds, factions.Lines);
            float speed = RegenSpeed(factions.Config, entity.World.ElapsedMilliseconds - lastHurtMs);
            if (entity.WatchedAttributes.GetFloat("regenSpeed", 1) != speed) entity.WatchedAttributes.SetFloat("regenSpeed", speed);
        }

        // Fighting NPCs closer than CombatSpacing to another NPC drift apart, at up to BattleSpacing.PushSpeed blocks
        // a second, sideways to whatever they are doing. NPCs that aren't fighting keep BodySpacing apart, and a
        // walking NPC looks LookAhead blocks along its way for its own side, steering past them and slowing behind
        // one right in front, so men don't walk through each other. Only neighbours from the entity partitions are
        // looked at, never every NPC. Standing NPCs that aren't fighting scan once a second, walking or fighting ones
        // four times a second.
        void Separate(float dt)
        {
            long now = entity.World.ElapsedMilliseconds;
            bool fighting = now < fightingUntil;
            double mx = entity.Pos.Motion.X, mz = entity.Pos.Motion.Z;
            bool walking = mx * mx + mz * mz > BattleSpacing.WalkingMotion * BattleSpacing.WalkingMotion;
            sinceScan += dt;
            if (sinceScan >= (fighting || walking ? ScanSeconds : IdleScanSeconds))
            {
                sinceScan = 0;
                Scan(now, fighting, walking);
            }
            KeepApart();
            if ((pushX == 0 && pushZ == 0 && brake == 0) || !entity.OnGround) return;
            double k = BattleSpacing.PushImpulse(dt);
            entity.Pos.Motion.X += pushX * k;
            entity.Pos.Motion.Z += pushZ * k;
            if (brake > 0)
            {
                // Takes away up to BrakeShare of the speed along the way each tick, never turning it around.
                double along = mx * headX + mz * headZ;
                if (along > 0)
                {
                    double cut = along * Math.Min(1, brake) * BattleSpacing.BrakeShare;
                    entity.Pos.Motion.X -= headX * cut;
                    entity.Pos.Motion.Z -= headZ * cut;
                }
            }
        }

        // Hard bodies: each tick the NPC steps out of any overlap with a neighbour from its last scan, by half the
        // overlap (the neighbour does the same on its own tick), never into a wall or off a ledge. Riders and mounts
        // are left out (a rider sits inside its mount); players keep the game's own push.
        void KeepApart()
        {
            if (contacts.Count == 0 || (entity as EntityAgent)?.MountedOn != null) return;
            double x = entity.Pos.X, z = entity.Pos.Z;
            foreach (Entity e in contacts)
            {
                if (!e.Alive || (e as EntityAgent)?.MountedOn != null || Math.Abs(e.Pos.InternalY - entity.Pos.InternalY) > 1.5) continue;
                if (BattleSpacing.ContactShift(x - e.Pos.X, z - e.Pos.Z, entity.EntityId, out double sx, out double sz)) { x += sx; z += sz; }
            }
            if ((x != entity.Pos.X || z != entity.Pos.Z) && CanStepTo(entity.World.BlockAccessor, x, entity.Pos.InternalY, z))
            {
                entity.Pos.X = x;
                entity.Pos.Z = z;
            }
        }

        // A fighting NPC keeps its front toward the enemy: its claimed target, else the nearest enemy from its scan
        // or whoever last hurt it, within BattleSpacing.FaceScan, also while it walks (this behavior runs after the
        // AI's path follower, which turns it the way it walks). Moving away from that enemy it steps back at a walk
        // with the walkback animation instead of turning its back. Not mounted (the mount turns it) and not while
        // breaking off (Retreat: they turn and run).
        void Face(float dt)
        {
            long now = entity.World.ElapsedMilliseconds;
            EntityAgent agent = entity as EntityAgent;
            bool backing = false;
            Entity threat = now < fightingUntil && agent?.MountedOn == null && factions.Hiring?.Disengaging(entity) != true ? Threat(now) : null;
            if (threat != null)
            {
                double dx = threat.Pos.X - entity.Pos.X, dz = threat.Pos.Z - entity.Pos.Z;
                if (dx * dx + dz * dz > 1e-4)
                {
                    float desired = (float)Math.Atan2(dx, dz);
                    float diff = GameMath.AngleRadDistance(entity.Pos.Yaw, desired);
                    float max = (float)(FaceRadPerSec * Math.Min(dt, 0.2f));
                    entity.Pos.Yaw = (entity.Pos.Yaw + GameMath.Clamp(diff, -max, max)) % GameMath.TWOPI;
                    agent.BodyYaw = entity.Pos.Yaw;
                    Vec3d walk = agent.Controls.WalkVector;
                    double wl = Math.Sqrt(walk.X * walk.X + walk.Z * walk.Z);
                    if (wl > 1e-4 && BattleSpacing.Backing(entity.Pos.Yaw, walk.X, walk.Z))
                    {
                        backing = true;
                        if (wl > BattleSpacing.BackWalkSpeed) { walk.X *= BattleSpacing.BackWalkSpeed / wl; walk.Z *= BattleSpacing.BackWalkSpeed / wl; }
                        agent.Controls.Sprint = false;
                    }
                }
            }
            if (backing == walkingBack) return;
            walkingBack = backing;
            if (backing)
            {
                entity.AnimManager?.StopAnimation("sprint");
                entity.AnimManager?.StartAnimation("lotrnpcs-walkback");
            }
            else entity.AnimManager?.StopAnimation("lotrnpcs-walkback");
        }

        bool walkingBack;
        Entity nearestEnemy, lastAttacker;
        double nearestEnemyD2;

        // The enemy to face: the claimed target, else the nearer of the nearest scanned enemy and the last attacker
        // (hit within FightingMs), within FaceScan.
        Entity Threat(long now)
        {
            double r2 = BattleSpacing.FaceScan * BattleSpacing.FaceScan;
            bool Near(Entity e) => e != null && e.Alive && e.Pos.Dimension == entity.Pos.Dimension && e.Pos.SquareDistanceTo(entity.Pos) <= r2;
            long id = factions.Spacing.TargetOf(entity.EntityId, now);
            if (id == 0) id = factions.Lines.TargetOf(entity.EntityId);
            Entity claimed = id == 0 ? null : entity.World.GetEntityById(id);
            if (Near(claimed)) return claimed;
            Entity a = Near(nearestEnemy) ? nearestEnemy : null, b = now - lastHurtMs < FightingMs && Near(lastAttacker) ? lastAttacker : null;
            if (a == null || b == null) return a ?? b;
            return a.Pos.SquareDistanceTo(entity.Pos) <= b.Pos.SquareDistanceTo(entity.Pos) ? a : b;
        }

        void Scan(long now, bool fighting, bool walking)
        {
            pushX = pushZ = 0; brake = 0;
            contacts.Clear();
            nearestEnemy = null; nearestEnemyD2 = double.MaxValue; scanFighting = fighting;
            BattleSpacing spacing = factions.Spacing;
            // Enemies, the one it fights included, may come as close as the duel minimum; only its own side keeps
            // CombatSpacing (keeping every NPC but its target away shoved fighters out of reach). Out of a fight its
            // own side keeps BodySpacing (formations stand 1.8 apart, so they are left as they are).
            scanSpacing = fighting ? spacing.Spacing : Math.Min(spacing.Spacing, BattleSpacing.BodySpacing);
            enemySpacing = Math.Min(spacing.DuelMin, scanSpacing);
            scanTarget = spacing.TargetOf(entity.EntityId, now);
            lookAhead = walking;
            if (walking)
            {
                double mx = entity.Pos.Motion.X, mz = entity.Pos.Motion.Z, len = Math.Sqrt(mx * mx + mz * mz);
                headX = mx / len; headZ = mz / len;
            }
            partitions ??= entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
            if (partitions == null) return;
            onNeighbor ??= OnNeighbor;
            double radius = Math.Max(walking ? Math.Max(scanSpacing + 0.5, BattleSpacing.LookAhead) : scanSpacing + 0.5, BattleSpacing.ContactScan);
            if (fighting) radius = Math.Max(radius, BattleSpacing.FaceScan);
            partitions.WalkEntities(entity.Pos.X, entity.Pos.InternalY, entity.Pos.Z, radius, onNeighbor, null, EnumEntitySearchType.Creatures);
            double length = Math.Sqrt(pushX * pushX + pushZ * pushZ);
            if (length < 1e-6) { pushX = pushZ = 0; return; }
            double nx = pushX / length, nz = pushZ / length;
            if (length > 1) { pushX = nx; pushZ = nz; }
            // Never off a ledge or into a wall: the spot 0.7 blocks along the push needs open air and ground below.
            if (!CanStepTo(entity.World.BlockAccessor, entity.Pos.X + nx * 0.7, entity.Pos.InternalY, entity.Pos.Z + nz * 0.7)) pushX = pushZ = 0;
        }

        bool OnNeighbor(Entity e)
        {
            if (e == entity || !e.Alive || e is EntityPlayer || FactionSystem.FactionOf(e) == null) return true;
            if (Math.Abs(e.Pos.InternalY - entity.Pos.InternalY) > 1.5) return true;
            double dx = entity.Pos.X - e.Pos.X, dz = entity.Pos.Z - e.Pos.Z;
            bool target = e.EntityId == scanTarget;
            // Cheap distance check first: most neighbours of a 5-block look-ahead scan are neither close nor ahead.
            double d2 = dx * dx + dz * dz;
            if (d2 < BattleSpacing.ContactScan * BattleSpacing.ContactScan && contacts.Count < 8) contacts.Add(e);
            // The nearest enemy, for Face.
            if (scanFighting && d2 < nearestEnemyD2 && (target || !factions.SameSide(entity, e))) { nearestEnemy = e; nearestEnemyD2 = d2; }
            bool near = d2 < scanSpacing * scanSpacing;
            bool ahead = lookAhead && !target && d2 < BattleSpacing.LookAhead * BattleSpacing.LookAhead && dx * headX + dz * headZ < 0;
            if (!near && !ahead) return true;
            bool sameSide = !target && factions.SameSide(entity, e);
            if (near) BattleSpacing.AddPush(ref pushX, ref pushZ, dx, dz, sameSide ? scanSpacing : enemySpacing, entity.EntityId);
            // Only one it is catching up with: a neighbour walking the same way about as fast (a column, a marching
            // line's rear rank) is left alone, or formations would be pushed out of shape.
            if (ahead && sameSide && BattleSpacing.Closing(entity.Pos.Motion.X * headX + entity.Pos.Motion.Z * headZ, e.Pos.Motion.X * headX + e.Pos.Motion.Z * headZ))
                BattleSpacing.AddAvoid(ref pushX, ref pushZ, ref brake, headX, headZ, -dx, -dz, entity.EntityId);
            return true;
        }

        /// <summary>True when an NPC can stand at (x, y, z): nothing solid at its feet or head, and solid ground at
        /// most one block below its feet.</summary>
        public static bool CanStepTo(IBlockAccessor blocks, double x, double y, double z)
        {
            int bx = (int)Math.Floor(x), by = (int)Math.Floor(y + 0.01), bz = (int)Math.Floor(z);
            return !Solid(blocks, bx, by, bz) && !Solid(blocks, bx, by + 1, bz) && (Solid(blocks, bx, by - 1, bz) || Solid(blocks, bx, by - 2, bz));
        }

        static bool Solid(IBlockAccessor blocks, int x, int y, int z)
        {
            Cuboidf[] boxes = blocks.GetBlockRaw(x, y, z).CollisionBoxes;
            return boxes != null && boxes.Length > 0;
        }

        // A dead or unloaded NPC frees its place in the fight, and nobody keeps a place on it.
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            // Recruits that hit it in the last 10 seconds earn XP.
            if (factions != null) entity.Api.ModLoader.GetModSystem<PromotionSystem>()?.OnDeath(entity);
            factions?.Log.Death(entity, damageSourceForDeath?.GetCauseEntity(), entity.World.ElapsedMilliseconds);
            factions?.Spacing.Forget(entity.EntityId);
            factions?.Lines.Forget(entity.EntityId);
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            factions?.Spacing.Forget(entity.EntityId);
            factions?.Lines.Forget(entity.EntityId);
            base.OnEntityDespawn(despawn);
        }

        public static float RegenSpeed(FactionConfig config, long msSinceHurt) =>
            msSinceHurt < (config?.RegenDelaySeconds ?? 10) * 1000L ? 1 : Math.Max(0, config?.HealthRegenSpeed ?? 1);

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            if (factions == null || damage <= 0 || damageSource?.Type == EnumDamageType.Heal) return;
            lastHurtMs = entity.World.ElapsedMilliseconds;
            MarkFighting(lastHurtMs);
            if (entity.WatchedAttributes.GetFloat("regenSpeed", 1) != 1) entity.WatchedAttributes.SetFloat("regenSpeed", 1);

            Entity attacker = damageSource?.GetCauseEntity();
            if (attacker != null && attacker != entity) lastAttacker = attacker;
            entity.Api.ModLoader.GetModSystem<PromotionSystem>()?.OnHit(entity, attacker);
            string faction = FactionSystem.FactionOf(entity);
            if (attacker == null || attacker == entity || !attacker.Alive || faction == null) return;
            if (Recruit.OwnerUidOf(attacker) == null && FactionSystem.FactionOf(attacker) is string other && factions.Relation(faction, other) == FactionRelation.Ally) return;
            factions.RecordAttack(faction, attacker);
            if (attacker is EntityPlayer player && !FactionSystem.CanBeAttacked(player)) return;

            string[] allies = factions.WithAllies(faction);
            float range = factions.Config.CallForHelpRange;
            foreach (Entity helper in entity.World.GetEntitiesAround(entity.Pos.XYZ, range, range / 2, e => e != entity && e.Alive && e is EntityAgent))
            {
                // Recruits answer only their owner's side (HiringSystem.OnTeamHurt).
                if (Recruit.OwnerUidOf(helper) == null && Array.IndexOf(allies, FactionSystem.FactionOf(helper)) >= 0) helper.Notify("seekEntity", attacker);
            }
        }

        public override string PropertyName() => "lotrnpcs:factionmember";
    }
}
