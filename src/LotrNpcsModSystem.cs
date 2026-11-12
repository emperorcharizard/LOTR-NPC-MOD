using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    public class LotrNpcsModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterEntityBehaviorClass("lotrnpcs:presetskinmodel", typeof(EntityBehaviorPresetSkinModel));
            api.RegisterEntityBehaviorClass("lotrnpcs:startinggear", typeof(EntityBehaviorStartingGear));
            api.RegisterEntityBehaviorClass("lotrnpcs:geartextures", typeof(EntityBehaviorGearTextures));
            api.RegisterEntityBehaviorClass("lotrnpcs:helmethidehair", typeof(EntityBehaviorHelmetHidesHair));
            api.RegisterEntityBehaviorClass("lotrnpcs:edgeguard", typeof(EntityBehaviorEdgeGuard));
            api.RegisterEntityBehaviorClass("lotrnpcs:rankarmor", typeof(EntityBehaviorRankArmor));
            api.RegisterEntityBehaviorClass("lotrnpcs:factionmember", typeof(EntityBehaviorFactionMember));
            // 0.0.112: what a dead NPC's harvestable body holds, from the config (BodyLoot.cs).
            api.RegisterEntityBehaviorClass("lotrnpcs:bodyloot", typeof(EntityBehaviorBodyLoot));
            // Cosmetic weapons in the hand, with their own swings (0.0.45, Weapons.cs).
            api.RegisterEntityBehaviorClass("lotrnpcs:heldweapon", typeof(EntityBehaviorHeldWeapon));
            AiTaskRegistry.Register<AiTaskChestMeleeAttack>("lotrnpcs:meleeattack");
            AiTaskRegistry.Register<AiTaskFactionSeek>("lotrnpcs:seekentity");
            // Archers (0.0.55, Archery.cs): their shot and their arrow.
            AiTaskRegistry.Register<AiTaskFactionShoot>("lotrnpcs:shootattack");
            AiTaskRegistry.Register<AiTaskFactionHide>("lotrnpcs:hide");
            api.RegisterEntity("LotrNpcsArrow", typeof(EntityNpcArrow));
            // Hiring (0.0.37): the talk menu and recruit behavior on NPCs, the owner behavior on players, following.
            api.RegisterEntityBehaviorClass("lotrnpcs:hireable", typeof(EntityBehaviorHireable));
            api.RegisterEntityBehaviorClass("lotrnpcs:recruitowner", typeof(EntityBehaviorRecruitOwner));
            AiTaskRegistry.Register<AiTaskFollowOwner>("lotrnpcs:followowner");
            // 0.0.112: wild groups walk their zone's patrol points (ZonePatrol.cs).
            AiTaskRegistry.Register<AiTaskZonePatrol>("lotrnpcs:zonepatrol");
            // Spawn items drawn as head-and-shoulders portraits (0.0.38).
            api.RegisterCollectibleBehaviorClass("lotrnpcs:npcportrait", typeof(CollectibleBehaviorNpcPortrait));
            ChestHitDamage.FindCombatOverhaul();
        }

        bool cameraGuardApplied;

        // Unchanged NPC gear is not resent to players on every save (0.0.114).
        public override void StartServerSide(ICoreServerAPI api) => GearSyncGuard.Apply(api);

        public override void StartClientSide(ICoreClientAPI api)
        {
            cameraGuardApplied = NpcCameraGuard.Apply(api);
            // The XP line under a recruit's health (0.0.76, Promotion.cs).
            XpInfoText.Apply(api);
            // Spawn item portraits are built a few at a time once the player is in creative mode, so opening the LOTR
            // NPCs tab doesn't stall the game.
            api.Event.LevelFinalize += () => PortraitWarmUp.Start(api);
        }

        public override void Dispose()
        {
            if (cameraGuardApplied) NpcCameraGuard.Remove();
            cameraGuardApplied = false;
            XpInfoText.Remove();
            GearSyncGuard.Remove();
        }
    }

    /// <summary>
    /// Stops NPCs from moving the player's camera near plane. Player Model Lib 1.24 scales the camera's near
    /// clipping plane by a skinned entity's eye height, meant for the player's own short or tall race. It does
    /// this for every entity with its skin behavior, NPCs included, and never undoes it when one unloads, so
    /// each orc, dwarf or hobbit that came into view shrank the near plane again. Depth precision collapsed:
    /// the ground, sky, clouds and armor layers showed through each other, worse the more NPCs had loaded.
    /// This lets only the local player's own entity change the camera, as Player Model Lib intends.
    /// </summary>
    public static class NpcCameraGuard
    {
        const string HarmonyId = "lotrnpcs.npccameraguard";
        static Harmony harmony;

        public static bool Apply(ICoreClientAPI api)
        {
            if (harmony != null) return false;

            Type skinBehavior = api.ClassRegistry.GetEntityBehaviorClass("PlayerModelLib:ExtraSkinnable");
            MethodInfo setZNear = skinBehavior?.GetMethod("SetZNear", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(ICoreClientAPI), typeof(float) }, null);
            // Player Model Lib before 1.24 never changes the camera, so there is nothing to guard.
            if (setZNear == null) return false;

            harmony = new Harmony(HarmonyId);
            MethodInfo prefix = typeof(NpcCameraGuard).GetMethod(nameof(OnlyLocalPlayer), BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(setZNear, new HarmonyMethod(prefix), null, null, null);
            api.Logger.Notification("[lotrnpcs] NPCs can no longer change the camera near plane (Player Model Lib SetZNear guarded).");
            return true;
        }

        public static void Remove()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
        }

        // Harmony prefix. Returning false skips Player Model Lib's camera change for that entity.
        static bool OnlyLocalPlayer(EntityBehavior __instance)
        {
            if (!(__instance.entity is EntityPlayer player)) return false;
            string localUid = (__instance.entity.Api as ICoreClientAPI)?.World?.Player?.PlayerUID;
            return localUid == null || Attrs.Uid(player) == localUid;
        }
    }

    /// <summary>
    /// Stops an NPC's unchanged gear from being sent to every player each time the game saves the NPC or sends it to a
    /// player. The game's gear behavior stores the gear in the watched attributes and marks it for sending on every
    /// save (EntityBehaviorContainer.ToBytes calls storeInv), and each client then reloads the gear and rebuilds the
    /// NPC's whole model (5-19 ms each), so an autosave rebuilt every NPC in view at once (0.0.114, client log
    /// 2026-11-11). Only this mod's NPCs, on the server; a real gear change differs and is stored as before.
    /// </summary>
    public static class GearSyncGuard
    {
        const string HarmonyId = "lotrnpcs.gearsyncguard";
        static Harmony harmony;

        public static void Apply(ICoreAPI api)
        {
            if (harmony != null) return;
            MethodInfo store = typeof(EntityBehaviorContainer).GetMethod(nameof(EntityBehaviorContainer.storeInv), BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (store == null)
            {
                api.Logger.Warning("[lotrnpcs] EntityBehaviorContainer.storeInv not found; NPC gear is resent on every save.");
                return;
            }
            harmony = new Harmony(HarmonyId);
            harmony.Patch(store, new HarmonyMethod(typeof(GearSyncGuard).GetMethod(nameof(SkipUnchanged), BindingFlags.Static | BindingFlags.NonPublic)), null, null, null);
        }

        public static void Remove()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
        }

        // Harmony prefix. Returning false skips the store: the watched gear already holds exactly this.
        static bool SkipUnchanged(EntityBehaviorContainer __instance)
        {
            Entity e = __instance.entity;
            if (e?.Api == null || e.Api.Side != EnumAppSide.Server || e.Code?.Domain != "lotrnpcs" || __instance.Inventory == null) return true;
            if (!(e.WatchedAttributes[__instance.InventoryClassName] is ITreeAttribute stored)) return true;
            TreeAttribute now = new TreeAttribute();
            __instance.Inventory.ToTreeAttributes(now);
            return !SameTree(stored, now);
        }

        public static bool SameTree(IAttribute a, IAttribute b) => Bytes(a).AsSpan().SequenceEqual(Bytes(b));

        static byte[] Bytes(IAttribute a)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter w = new BinaryWriter(ms)) a.ToBytes(w);
                return ms.ToArray();
            }
        }
    }

    /// <summary>
    /// Melee attack that always lands on the chest (user: no head shots). With Combat Overhaul, a player hit
    /// lands on a random body part and head hits deal double damage, so player hits are sent as Combat
    /// Overhaul's own located damage aimed at the upper torso. Without Combat Overhaul it is the vanilla attack.
    /// An NPC carrying a weapon (Weapons.cs) swings that weapon's animation instead of the unarmed one; damage,
    /// tier and timing are the same either way.
    /// </summary>
    public class AiTaskChestMeleeAttack : AiTaskMeleeAttack
    {
        readonly FactionSystem factions;
        readonly string faction;
        // The task's own animation from its config, played when the NPC carries no weapon.
        readonly AnimationMetaData unarmedAnimation;
        EntityBehaviorHeldWeapon weapon;
        bool weaponLookedUp;
        // The damage the task's config gives (0.0.90), used when the faction has none for this rank, and the rank.
        readonly float baseDamage;
        readonly string rank;
        // The task's own damagePlayerAtMs, used when the weapon has no wind-up of its own (0.0.99).
        readonly int baseHitMs;
        // 0.0.99 swing sync: when this attack started, and whether its swing animation has begun.
        long startedMs;
        bool swingStarted;
        // Longest the damage timer waits for the NPC to turn to its target before the vanilla timing takes over (so
        // an attack on a target it can never face still ends).
        const long MaxTurnWaitMs = 1000;

        public AiTaskChestMeleeAttack(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            factions = entity.Api.ModLoader.GetModSystem<FactionSystem>();
            faction = FactionSystem.FactionOf(entity);
            unarmedAnimation = animMeta;
            baseDamage = damage;
            baseHitMs = damagePlayerAtMs;
            rank = EntityBehaviorFactionMember.RankOf(entity);
        }

        EntityBehaviorHeldWeapon Weapon
        {
            get
            {
                if (!weaponLookedUp)
                {
                    weapon = entity.GetBehavior<EntityBehaviorHeldWeapon>();
                    weaponLookedUp = true;
                }
                return weapon;
            }
        }

        // 0.0.82 (owner: fighting NPCs "turning in place"): vanilla swings at the nearest enemy in reach, which in a
        // crowd was often another than the one this NPC had walked to, so it turned to and fro between them. While the
        // NPC it claimed (its duel partner) is in reach and not striking back at someone else, that one is hit instead.
        public override bool ShouldExecute()
        {
            if (!base.ShouldExecute()) return false;
            if (factions == null || targetEntity == null) return true;
            if (targetEntity != attackedByEntity)
            {
                long claimed = factions.Spacing.TargetOf(entity.EntityId, entity.World.ElapsedMilliseconds);
                if (claimed != 0 && claimed != targetEntity.EntityId)
                {
                    Entity partner = entity.World.GetEntityById(claimed);
                    if (partner != null && partner.Alive && hasDirectContact(partner, minDist, minVerDist)) targetEntity = partner;
                }
            }
            // 0.0.99: this weapon's own wind-up (gen HIT_MS), set before the turn check below, which uses it.
            int weaponHitMs = Weapon?.HitMs ?? 0;
            damagePlayerAtMs = weaponHitMs > 0 ? weaponHitMs : baseHitMs;
            // 0.0.98 staggered swings (BattleSpacing.HitWaitMs): a hit that would land inside the target's hurt window
            // after another NPC's hit would be thrown away, so this NPC waits its turn. It looks again when the turn is
            // due (at least TurnRetryMs later), not every tick, so waiting costs no extra target searches.
            long now = entity.World.ElapsedMilliseconds;
            // 0.0.109: with NpcHitsIgnoreHurtWindow on, a hit on anything but a player lands anyway, so no turn is kept.
            if (IgnoresWindow(targetEntity)) return true;
            long wait = factions.Spacing.HitWaitMs(targetEntity.EntityId, now + damagePlayerAtMs);
            if (wait > 0)
            {
                factions.Log.TurnWait(entity);
                // The vanilla check waits attackDurationMs after lastCheckOrAttackMs.
                lastCheckOrAttackMs = now - attackDurationMs + Math.Min(Math.Max(wait, TurnRetryMs), attackDurationMs);
                targetEntity = null;
                return false;
            }
            return true;
        }

        // Soonest an NPC waiting for its turn on a target looks again.
        const long TurnRetryMs = 100;

        public override void StartExecute()
        {
            // Battle spacing (0.0.53): a swing renews this NPC's place on its target, or takes one if it had none.
            if (factions != null && targetEntity != null)
            {
                long now = entity.World.ElapsedMilliseconds;
                if (!(targetEntity is EntityPlayer))
                    factions.Spacing.Swing(entity.EntityId, targetEntity.EntityId, Math.Atan2(entity.Pos.X - targetEntity.Pos.X, entity.Pos.Z - targetEntity.Pos.Z), now);
                entity.GetBehavior<EntityBehaviorFactionMember>()?.MarkFighting(now);
                factions.Log.Attacked(entity, now);
                factions.Spacing.BookHit(targetEntity.EntityId, lastCheckOrAttackMs + damagePlayerAtMs, entity.World.Rand.Next(BattleSpacing.HitJitterMs + 1));
            }
            // 0.0.91: the faction's damage for this rank, read at each swing so /recruits faction reload applies at
            // once, varied by DamageSpread (owner: "add a +-20% spread on hits"). The damage tier stays (it sets armor
            // penetration against players).
            FactionConfig config = factions?.Config;
            damage = FactionTiers.Spread(config?.DamageFor(faction, rank) ?? baseDamage, config?.Spread ?? 0, entity.World.Rand.NextDouble());
            // Chosen before the vanilla task starts the animation, which it does once the NPC faces its target.
            animMeta = Weapon?.AttackAnimation ?? unarmedAnimation;
            Weapon?.Lower();
            startedMs = entity.World.ElapsedMilliseconds;
            swingStarted = false;
            base.StartExecute();
        }

        // 0.0.99 swing sync (owner chose "Swing sync + weapon wind-ups"): vanilla counts down to the blow from when the
        // NPC decided to attack, but starts the swing only once it faces its target, so after a turn the damage landed
        // while the weapon was barely raised. Until the swing starts, the countdown (lastCheckOrAttackMs) is held at now,
        // so the blow lands damagePlayerAtMs after the swing really starts, and the attack lasts its full length from
        // there. The swing starts inside base.ContinueExecute, the same tick it is held for the last time. The game keys
        // active animations by their shape animation (animMeta.Animation, e.g. "axehit"), not the config code.
        public override bool ContinueExecute(float dt)
        {
            if (!swingStarted)
            {
                long now = entity.World.ElapsedMilliseconds;
                if (animMeta != null && entity.AnimManager?.IsAnimationActive(animMeta.Animation) == true) swingStarted = true;
                else if (now - startedMs <= MaxTurnWaitMs) lastCheckOrAttackMs = now;
            }
            return base.ContinueExecute(dt);
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            Weapon?.Raise();
        }

        // Targets are chosen by faction (Factions.cs), not by entity code, after the cheap checks (alive, range, game
        // mode). Without a faction, vanilla rules apply.
        public override bool IsTargetableEntity(Entity e, float range)
        {
            if (factions != null && Recruit.OwnerUidOf(entity) is string owner) return e.Alive && CanSense(e, range) && factions.RecruitWantsToAttack(entity, owner, e) && !Crowded(e);
            return faction == null || factions == null ? base.IsTargetableEntity(e, range) : e.Alive && CanSense(e, range) && factions.WantsToAttack(faction, e) && !Crowded(e);
        }

        // An enemy NPC that already has its share of attackers is left to them (battle spacing, 0.0.53).
        bool Crowded(Entity e) => factions.Spacing.Crowded(entity.EntityId, e.EntityId, e is EntityPlayer, entity.World.ElapsedMilliseconds);

        /// <summary>0.0.109: whether this NPC's hits on target skip its hurt window (config NpcHitsIgnoreHurtWindow;
        /// never on players).</summary>
        bool IgnoresWindow(Entity target) => target != null && !(target is EntityPlayer) && factions?.Config.NpcHitsIgnoreHurtWindow == true;

        protected override void attackTarget()
        {
            // 0.0.98: the hit lands now, which is later than booked if the NPC was still turning to face its target.
            if (factions != null && targetEntity != null) factions.Spacing.BookHit(targetEntity.EntityId, entity.World.ElapsedMilliseconds, 0);
            if (!hasDirectContact(targetEntity, minDist, minVerDist)) return;
            // 0.0.109: vanilla's attackTarget, done here so the hit can skip the hurt window (IgnoresWindow) and the
            // battle log learns how it went. A player is struck in the chest under Combat Overhaul (0.0.14).
            Entity target = targetEntity;
            bool chest = target is EntityPlayer && ChestHitDamage.Available;
            DamageSource source = chest ? ChestHitDamage.Create(damageType, damageTier) : new DamageSource { Type = damageType, DamageTier = damageTier };
            source.Source = EnumDamageSource.Entity;
            source.SourceEntity = entity;
            source.KnockbackStrength = knockbackStrength;
            source.IgnoreInvFrames = IgnoresWindow(target);
            bool alive = target.Alive;
            bool window = target.IsActivityRunning("invulnerable");
            float amount = damage * GlobalConstants.CreatureDamageModifier;
            bool received = target.ReceiveDamage(source, amount);
            if (entity is IMeleeAttackListener listener) listener.DidAttack(target);
            bool killed = alive && !target.Alive;
            if (killed)
            {
                if (!(target is EntityPlayer)) entity.WatchedAttributes.SetDouble("lastMealEatenTotalHours", entity.World.Calendar.TotalHours);
                bhEmo?.TryTriggerState("saturated", target.EntityId);
            }
            factions?.Log.Attack(entity, target, false, amount, BattleLog.Classify(received, alive, window && !source.IgnoreInvFrames), killed, entity.World.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Builds Combat Overhaul damage aimed at a player's upper torso. Found by reflection so the mod has no
    /// Combat Overhaul dependency: Combat Overhaul sends damage that names a body collider to that body part.
    /// </summary>
    public static class ChestHitDamage
    {
        static Type sourceType;
        static Type dataType;
        // Looked up once, not on every hit.
        static PropertyInfo colliderProperty, dataProperty;

        public static bool Available => sourceType != null;

        public static void FindCombatOverhaul()
        {
            sourceType = dataType = null;
            colliderProperty = dataProperty = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("CombatOverhaul.DamageSystems.DirectionalTypedDamageSource", false);
                if (type == null) continue;
                sourceType = type;
                dataType = assembly.GetType("CombatOverhaul.DamageSystems.DamageData", false);
                colliderProperty = type.GetProperty("Collider");
                dataProperty = dataType == null ? null : type.GetProperty("DamageTypeData");
                return;
            }
        }

        public static DamageSource Create(EnumDamageType type, int tier)
        {
            DamageSource source = (DamageSource)Activator.CreateInstance(sourceType);
            source.Type = type;
            source.DamageTier = tier;
            colliderProperty?.SetValue(source, "UpperTorso");
            dataProperty?.SetValue(source, Activator.CreateInstance(dataType, type, tier, 0));
            return source;
        }
    }

    /// <summary>
    /// Tells Player Model Lib which race model an NPC uses. Player Model Lib reads the race from the
    /// "skinModel" watched attribute and has no entity JSON key for it, so this behavior sets it.
    /// It must be listed before "PlayerModelLib:ExtraSkinnable" so the value exists when that behavior initializes.
    /// </summary>
    public class EntityBehaviorPresetSkinModel : EntityBehavior
    {
        public EntityBehaviorPresetSkinModel(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            string model = attributes["skinModel"].AsString(null);
            if (model == null || entity.WatchedAttributes.HasAttribute("skinModel")) return;

            entity.WatchedAttributes.SetString("skinModel", model);
            entity.WatchedAttributes.MarkPathDirty("skinModel");
        }

        public override string PropertyName() => "lotrnpcs:presetskinmodel";
    }

    /// <summary>Starting gear codes: an item code, optionally followed by "@slot" naming the gear slot it goes in
    /// (EnumCharacterDressType, such as face or neck).</summary>
    public static class GearCode
    {
        public static string ItemCode(string code)
        {
            int at = code.LastIndexOf('@');
            return at < 0 ? code : code.Substring(0, at);
        }

        public static EnumCharacterDressType? Slot(string code)
        {
            int at = code.LastIndexOf('@');
            if (at < 0) return null;
            return Enum.TryParse(code.Substring(at + 1), true, out EnumCharacterDressType type) && type != EnumCharacterDressType.Unknown ? type : null;
        }

        public static Item Item(IWorldAccessor world, string code) => world.GetItem(new AssetLocation(ItemCode(code)));
    }

    /// <summary>
    /// Dresses an NPC when it first spawns, such as in the armor of its rank. "sets" lists whole outfits from
    /// other mods; one outfit whose items all exist is picked at random. If none is complete, such as when the
    /// armor mods aren't installed, the NPC wears the "items" outfit instead, skipping missing items with a
    /// warning in the server log. The vanilla "seraphinventory" behavior holds the gear and draws it, so it must
    /// be listed before this one.
    /// </summary>
    public class EntityBehaviorStartingGear : EntityBehavior
    {
        string[] itemCodes;
        string[][] setCodes;
        // 0.0.78: sets worn only when none of setCodes is complete (Haradrim: the owner's armor mod first, the 0.0.41
        // outfits without it), so the two never mix on a server that has both.
        string[][] fallbackSetCodes;
        // A hat worn only now and then (0.0.55, hobbits: "maybe a hat on rare occasion"): one of hatCodes, by hatChance.
        string[] hatCodes;
        double hatChance;

        public EntityBehaviorStartingGear(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            itemCodes = attributes["items"].AsArray<string>(Array.Empty<string>(), "game");
            setCodes = ReadSets(attributes["sets"]);
            fallbackSetCodes = ReadSets(attributes["fallbackSets"]);
            hatCodes = attributes["hats"]["items"].AsArray<string>(Array.Empty<string>(), "game");
            hatChance = attributes["hats"]["chance"].AsDouble(0);
        }

        static string[][] ReadSets(JsonObject list) =>
            (list.AsArray() ?? Array.Empty<JsonObject>()).Select(set => set.AsArray<string>(Array.Empty<string>(), "game")).ToArray();

        /// <summary>The sets whose items all exist: the first tier that has any (sets, then fallbackSets). Public for tests.</summary>
        public static List<string[]> CompleteSets(string[][] sets, string[][] fallbackSets, System.Func<string, bool> exists)
        {
            foreach (string[][] tier in new[] { sets, fallbackSets })
            {
                List<string[]> complete = (tier ?? Array.Empty<string[]>()).Where(codes => codes.All(exists)).ToList();
                if (complete.Count > 0) return complete;
            }
            return new List<string[]>();
        }

        /// <summary>A hat for a new NPC: one of the codes that exist, by chance, or null.</summary>
        public static string PickHat(string[] codes, double chance, System.Func<string, bool> exists, Random rand)
        {
            if (codes == null || codes.Length == 0 || rand.NextDouble() >= chance) return null;
            string[] real = codes.Where(exists).ToArray();
            return real.Length == 0 ? null : real[rand.Next(real.Length)];
        }

        void GiveHat()
        {
            string hat = PickHat(hatCodes, hatChance, c => GearCode.Item(entity.World, c) != null, entity.World.Rand);
            if (hat != null) Give(hat);
        }

        public override void OnEntitySpawn()
        {
            if (entity.World.Side != EnumAppSide.Server) return;

            List<string[]> completeSets = CompleteSets(setCodes, fallbackSetCodes, c => GearCode.Item(entity.World, c) != null);

            if (completeSets.Count > 0)
            {
                foreach (string code in completeSets[entity.World.Rand.Next(completeSets.Count)]) Give(code);
                GiveHat();
                Looks.RollHelmet(entity, entity.Api.ModLoader.GetModSystem<FactionSystem>()?.Config);
                return;
            }

            foreach (string code in itemCodes)
            {
                if (GearCode.Item(entity.World, code) == null)
                {
                    entity.World.Logger.Warning("[lotrnpcs] Starting gear item " + code + " does not exist, skipped.");
                    continue;
                }
                Give(code);
            }
            GiveHat();
            Looks.RollHelmet(entity, entity.Api.ModLoader.GetModSystem<FactionSystem>()?.Config);
        }

        // The gear behavior puts each item in the slot that fits it, such as body armor in the body slot. A code ending
        // in "@slot" goes straight into that gear slot instead, if it is free, so an NPC can wear two pieces made for
        // the same slot (user, 2026-11-03: a helmet over a chain coif, a cape and a fur wrap).
        void Give(string code)
        {
            Item item = GearCode.Item(entity.World, code);
            if (item == null) return;
            ItemStack stack = new ItemStack(item, 1);
            EnumCharacterDressType? slotType = GearCode.Slot(code);
            EntityBehaviorContainer gear = entity.GetBehavior<EntityBehaviorTexturedClothing>();
            InventoryBase inv = gear?.Inventory;
            int index = slotType.HasValue ? (int)slotType.Value : -1;
            if (index >= 0 && inv != null && index < inv.Count && inv[index].Empty)
            {
                inv[index].Itemstack = stack;
                inv[index].MarkDirty();
                // Stores the gear in the watched attributes and marks just that part for sending to players.
                gear.storeInv();
                return;
            }
            if (slotType.HasValue) entity.World.Logger.Warning("[lotrnpcs] Gear slot {0} for {1} is missing or taken; wearing it in its own slot.", slotType.Value, code);
            entity.TryGiveItemStack(stack);
        }

        public override string PropertyName() => "lotrnpcs:startinggear";
    }

    /// <summary>
    /// Makes higher ranks resist weaker metals. Attacks with a damage tier below the "tier" attribute (bronze 3,
    /// iron 4, steel 5) deal "perTierFactor" of their damage for each tier short. Worn vanilla armor does not
    /// protect NPCs, so this stands in for it. It must be listed before "health" so health takes the reduced damage.
    /// </summary>
    public class EntityBehaviorRankArmor : EntityBehavior
    {
        int tier;
        float perTierFactor;

        public EntityBehaviorRankArmor(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            tier = attributes["tier"].AsInt(0);
            perTierFactor = attributes["perTierFactor"].AsFloat(0.5f);
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            EnumDamageType type = damageSource.Type;
            if (type != EnumDamageType.BluntAttack && type != EnumDamageType.SlashingAttack && type != EnumDamageType.PiercingAttack) return;

            int tiersShort = tier - damageSource.DamageTier;
            for (int i = 0; i < tiersShort; i++) damage *= perTierFactor;
        }

        public override string PropertyName() => "lotrnpcs:rankarmor";
    }

    /// <summary>
    /// Lets worn gear find its textures on a Player Model Lib NPC. The vanilla gear behavior adds each armor
    /// texture to the entity's texture list, but Player Model Lib's texture source only knows the race model's
    /// textures, so the NPC failed to draw at all. This client behavior wraps that source and falls back to the
    /// entity's texture list. It must be listed before "PlayerModelLib:ExtraSkinnable" to be asked first.
    /// </summary>
    public class EntityBehaviorGearTextures : EntityBehavior
    {
        public EntityBehaviorGearTextures(Entity entity) : base(entity)
        {
        }

        public override ITexPositionSource GetTextureSource(ref EnumHandling handling)
        {
            ICoreClientAPI capi = entity.Api as ICoreClientAPI;
            if (capi == null) return null;

            // Ask the behaviors after this one, as the game would have without us.
            ITexPositionSource inner = null;
            EnumHandling innerHandling = EnumHandling.PassThrough;
            foreach (EntityBehavior behavior in entity.Properties.Client.Behaviors)
            {
                if (behavior == this) continue;
                inner = behavior.GetTextureSource(ref innerHandling);
                if (inner != null || innerHandling == EnumHandling.PreventSubsequent) break;
            }

            handling = EnumHandling.PreventSubsequent;
            return new GearTextureSource(capi, entity, inner);
        }

        public override string PropertyName() => "lotrnpcs:geartextures";
    }

    public class GearTextureSource : ITexPositionSource
    {
        readonly ICoreClientAPI capi;
        readonly Entity entity;
        readonly ITexPositionSource inner;

        public GearTextureSource(ICoreClientAPI capi, Entity entity, ITexPositionSource inner)
        {
            this.capi = capi;
            this.entity = entity;
            this.inner = inner;
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                TextureAtlasPosition position = inner?[textureCode];
                if (position != null) return position;

                if (entity.Properties.Client.Textures.TryGetValue(textureCode, out CompositeTexture texture) && texture?.Baked != null)
                {
                    return capi.EntityTextureAtlas.Positions[texture.Baked.TextureSubId];
                }
                // Draw a missing texture as the "unknown" texture rather than failing the whole NPC.
                return capi.EntityTextureAtlas.UnknownTexturePosition;
            }
        }

        public Size2i AtlasSize => capi.EntityTextureAtlas.Size;
    }
}
