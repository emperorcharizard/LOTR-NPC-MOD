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
    /// On every faction NPC, both sides: right-click opens the talk menu, and for recruits it keeps them from
    /// wandering, stops friendly fire from their own side, and calls the other recruits when one is hurt. It goes
    /// before "health" so friendly fire is cancelled before it lands.
    /// </summary>
    public class EntityBehaviorHireable : EntityBehavior
    {
        public const int OpenPacket = 46301, ActionPacket = 46302, ReplyPacket = 46303, ClosePacket = 46304, TalkRequestPacket = 46305;
        /// <summary>Client to server: a LooksRequest as JSON. Server to client: the refusal's lang key.</summary>
        public const int LooksPacket = 46306;
        public const double TalkRange = 5;
        HiringSystem hiring;
        long talkingUntilMs;
        GuiDialogTalk dialog;
        long lastRequestMs;

        public EntityBehaviorHireable(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            if (entity.Api.Side == EnumAppSide.Server) hiring = entity.Api.ModLoader.GetModSystem<HiringSystem>();
            // Player Model Lib redraws a model only when the model itself changes, so a face changed by the owner
            // (Looks.cs) is redrawn here.
            else entity.WatchedAttributes.RegisterModifiedListener("skinConfig", () => entity.MarkShapeModified());
        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            if (entity.Api.Side != EnumAppSide.Server) return;
            if (entity is EntityAgent agent && hiring?.DismissIfPending(agent) == false) hiring.OnRecruitLoaded(agent);
            AiTaskManager tasks = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (tasks != null) tasks.OnShouldExecuteTask += AllowTask;
        }

        // Recruits never wander off, and nobody wanders off while a player is talking to them. Passive recruits
        // never start a fight, not even to strike back.
        bool AllowTask(IAiTask task)
        {
            if (task is AiTaskWander || task is AiTaskZonePatrol) return Recruit.OwnerUidOf(entity) == null && entity.World.ElapsedMilliseconds > talkingUntilMs;
            // Recruits standing in a formation face its way instead of looking about.
            if (task is AiTaskLookAround) return !(Recruit.OwnerUidOf(entity) != null && Recruit.HasFacing(entity));
            // While a recruit breaks off (Retreat, or a movement order), no fight task starts at all, not even the
            // game's own strike-back at whoever just hit it; a Charge order still fights.
            if (task is AiTaskSeekEntity || task is AiTaskMeleeAttack || task is AiTaskFactionShoot)
            {
                if (Recruit.OwnerUidOf(entity) == null) return true;
                if (Recruit.StanceOf(entity) == Recruit.Passive && !(task is AiTaskFactionShoot)) return false;
                return hiring == null || hiring.Charging(entity) || !hiring.Disengaging(entity);
            }
            return true;
        }

        bool IsFighting()
        {
            IAiTask[] active = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.ActiveTasksBySlot;
            return active != null && active.Any(t => t is AiTaskSeekEntity || t is AiTaskMeleeAttack);
        }

        // Right-click: the client asks, and the server decides what the NPC says, if anything (OpenFor).
        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (mode != EnumInteractMode.Interact || !entity.Alive || !(entity.Api is ICoreClientAPI capi) || byEntity != capi.World.Player?.Entity) return;
            long now = capi.World.ElapsedMilliseconds;
            if (dialog?.IsOpened() == true || now - lastRequestMs < 500) return;
            lastRequestMs = now;
            capi.Network.SendEntityPacket(entity.EntityId, TalkRequestPacket);
        }

        void OpenFor(IServerPlayer player)
        {
            EntityPlayer eplayer = player.Entity;
            if (!entity.Alive || eplayer == null || eplayer.Pos.SquareDistanceTo(entity.Pos) > TalkRange * TalkRange) return;
            // No menu in the middle of a fight, so blocking or a stray click doesn't open one.
            if (IsFighting()) return;
            TalkState state = hiring.StateFor(entity as EntityAgent, player);
            if (state == null) return;
            talkingUntilMs = entity.World.ElapsedMilliseconds + 30000;
            FacePlayer(eplayer);
            Send(player, OpenPacket, state);
        }

        // As vanilla traders do: stop wandering and turn to the player.
        void FacePlayer(EntityPlayer player)
        {
            AiTaskManager tasks = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (tasks == null || !(entity is EntityAgent agent)) return;
            tasks.StopTask(typeof(AiTaskWander));
            AiTaskLookAtEntity look = new AiTaskLookAtEntity(agent, JsonObject.FromJson("{}"), JsonObject.FromJson("{}")) { manualExecute = true, targetEntity = player };
            tasks.ExecuteTask(look, 1);
        }

        void Send(IServerPlayer player, int packet, TalkState state) =>
            ((ICoreServerAPI)entity.Api).Network.SendEntityPacket(player, entity.EntityId, packet, state == null ? null : Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(state)));

        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled)
        {
            if (hiring == null) return;
            if (packetid == TalkRequestPacket) { OpenFor(player); return; }
            if (packetid == ClosePacket) { talkingUntilMs = 0; return; }
            if (packetid == LooksPacket) { ChangeLooks(player, data); return; }
            if (packetid != ActionPacket || data == null || !(entity is EntityAgent npc)) return;
            TalkState state = hiring.Act(npc, player, Encoding.UTF8.GetString(data));
            talkingUntilMs = state == null ? 0 : entity.World.ElapsedMilliseconds + 30000;
            Send(player, state == null ? ClosePacket : ReplyPacket, state);
        }

        // The owner's appearance dialog (Looks.cs): checked and applied on the server, refusals sent back.
        void ChangeLooks(IServerPlayer player, byte[] data)
        {
            EntityPlayer eplayer = player.Entity;
            if (data == null || eplayer == null || !entity.Alive || eplayer.Pos.SquareDistanceTo(entity.Pos) > (TalkRange + 2) * (TalkRange + 2)) return;
            LooksRequest req;
            try { req = JsonConvert.DeserializeObject<LooksRequest>(Encoding.UTF8.GetString(data)); }
            catch (Exception) { return; }
            // Cancel resends the old loadout: only a real change of weapon or shield may change the class.
            string gearBefore = Attrs.String(entity.WatchedAttributes, EntityBehaviorHeldWeapon.WeaponAttr) + "|" + Attrs.String(entity.WatchedAttributes, EntityBehaviorHeldWeapon.ShieldAttr);
            string refused = Looks.Apply(entity, player.PlayerUID, req, Looks.PartsOf(entity.Api, Attrs.String(entity.WatchedAttributes, "skinModel")), Looks.WeaponChoices(entity), Looks.Head(entity), Looks.ShieldOf(entity), Looks.HelmetChoices(entity));
            talkingUntilMs = entity.World.ElapsedMilliseconds + 30000;
            if (refused != null) ((ICoreServerAPI)entity.Api).Network.SendEntityPacket(player, entity.EntityId, LooksPacket, Encoding.UTF8.GetBytes(refused));
            else if (gearBefore != Attrs.String(entity.WatchedAttributes, EntityBehaviorHeldWeapon.WeaponAttr) + "|" + Attrs.String(entity.WatchedAttributes, EntityBehaviorHeldWeapon.ShieldAttr)) Reclass(player);
        }

        // A warrior whose new weapon or shield makes another class (Looks.ClassFor) becomes that class, as a
        // promotion makes a new NPC (PromotionSystem.Promote), keeping its XP. The looks dialog closes with the old
        // NPC.
        void Reclass(IServerPlayer player)
        {
            string target = Looks.LoadoutClass(entity);
            if (target == null || target == NpcTypeInfo.Of(entity)?.Rank || !(entity is EntityAgent npc)) return;
            int xp = Promotion.Xp(entity);
            string before = Recruit.TitleInSentence(entity, player.LanguageCode);
            EntityAgent next = hiring?.Promotions?.Promote(npc, target);
            if (next == null) return;
            next.WatchedAttributes.SetInt(Promotion.XpAttr, xp);
            next.WatchedAttributes.MarkPathDirty(Promotion.XpAttr);
            player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.GetL(player.LanguageCode, "lotrnpcs:promote-done", before, Recruit.TitleInSentence(next, player.LanguageCode)), EnumChatType.Notification);
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data, ref EnumHandling handled)
        {
            if (!(entity.Api is ICoreClientAPI capi)) return;
            if (packetid == ClosePacket) { dialog?.TryClose(); return; }
            if (packetid == LooksPacket) { if (data != null) dialog?.OnLooksRefused(Encoding.UTF8.GetString(data)); return; }
            if ((packetid != OpenPacket && packetid != ReplyPacket) || data == null) return;
            TalkState state;
            try { state = JsonConvert.DeserializeObject<TalkState>(Encoding.UTF8.GetString(data)); }
            catch (Exception e) { capi.Logger.Error("[lotrnpcs] Bad talk packet: " + e.Message); return; }
            if (packetid == ReplyPacket) { dialog?.OnReply(state); return; }
            // One conversation at a time, as with vanilla traders.
            if (dialog?.IsOpened() == true || capi.Gui.OpenedGuis.Any(g => g is GuiDialogTalk)) return;
            dialog = new GuiDialogTalk(capi, (EntityAgent)entity, state);
            dialog.OnClosed += () => { dialog = null; capi.Network.SendEntityPacket(entity.EntityId, ClosePacket); };
            dialog.Begin();
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            // A hit in the back (from the projectile, attacker or source spot behind this NPC) does more.
            if (damage > 0 && damageSource != null && damageSource.Type != EnumDamageType.Heal && entity.Api.Side == EnumAppSide.Server
                && (entity as EntityAgent)?.MountedOn?.Entity != damageSource.SourceEntity)
            {
                FactionSystem fs = entity.Api.ModLoader.GetModSystem<FactionSystem>();
                Vec3d from = damageSource.SourceEntity?.Pos.XYZ ?? damageSource.SourcePos ?? damageSource.GetCauseEntity()?.Pos.XYZ;
                if (fs?.Config != null && from != null && BattleSpacing.FromBehind(entity.Pos.Yaw, from.X - entity.Pos.X, from.Z - entity.Pos.Z, fs.Config.BackArc))
                {
                    damage *= (float)fs.Config.BackScale;
                    fs.Log.BackHit(entity);
                }
            }
            // Healing is never friendly fire, and nobody is attacked for it.
            if (hiring == null || damageSource?.Type == EnumDamageType.Heal || !(Recruit.OwnerUidOf(entity) is string owner)) return;
            Entity attacker = damageSource?.GetCauseEntity();
            if (attacker == null || attacker == entity) return;
            // No friendly fire from the owner or their other recruits.
            if (Recruit.IsTeam(attacker, owner)) { damage = 0; return; }
            if (damage > 0) hiring.OnTeamHurt(owner, attacker, entity.Pos.XYZ);
        }

        // Never fights back against the owner or the owner's other recruits.
        public override bool ToleratesDamageFrom(Entity eOther, ref EnumHandling handling)
        {
            if (eOther == null || !(Recruit.OwnerUidOf(entity) is string owner) || !Recruit.IsTeam(eOther, owner)) return false;
            handling = EnumHandling.PreventDefault;
            return true;
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            if (hiring != null && Recruit.OwnerUidOf(entity) != null) hiring.OnRecruitGone(entity, true);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            if (hiring == null || Recruit.OwnerUidOf(entity) == null) return;
            EnumDespawnReason reason = despawn?.Reason ?? EnumDespawnReason.Unload;
            // Unloaded recruits stay hired; death is handled in OnEntityDeath. The roster remembers where they were.
            if (reason == EnumDespawnReason.Unload || reason == EnumDespawnReason.OutOfRange) hiring.Summarize(entity);
            if (reason == EnumDespawnReason.Unload || reason == EnumDespawnReason.Disconnect || reason == EnumDespawnReason.OutOfRange || reason == EnumDespawnReason.Death) return;
            hiring.OnRecruitGone(entity, false);
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            if (!(Recruit.OwnerUidOf(entity) is string owner)) return;
            // A named recruit still shows its people and rank.
            if (entity.WatchedAttributes.GetBool(Looks.NamedAttr)) infotext.AppendLine(Lang.Get(entity.Code.Domain + ":item-creature-" + entity.Code.Path));
            string borrowerName = Recruit.BorrowerUidOf(entity) != null ? entity.WatchedAttributes.GetString(Recruit.BorrowerNameAttr) : null;
            if ((entity.Api as ICoreClientAPI)?.World.Player?.PlayerUID != owner)
            {
                infotext.AppendLine(Lang.Get("lotrnpcs:recruit-of", entity.WatchedAttributes.GetString(Recruit.OwnerNameAttr), Recruit.Title(entity)));
                if (borrowerName != null) infotext.AppendLine(Lang.Get("lotrnpcs:recruit-borrowed", borrowerName));
                else if (Recruit.IsShared(entity) && Recruit.OrderOf(entity) == Recruit.Guard) infotext.AppendLine(Lang.Get("lotrnpcs:recruit-shared"));
                return;
            }
            string title = Recruit.TitleInSentence(entity);
            if (borrowerName != null) { infotext.AppendLine(Lang.Get("lotrnpcs:recruit-yours-borrowed", borrowerName, title)); return; }
            string stance = Lang.Get("lotrnpcs:stance-" + Recruit.StanceOf(entity)).ToLowerInvariant();
            // Guards spread over their area, so their formation doesn't matter.
            infotext.AppendLine(Recruit.OrderOf(entity) == Recruit.Guard ? Lang.Get(Recruit.IsShared(entity) ? "lotrnpcs:recruit-yours-guard-shared" : "lotrnpcs:recruit-yours-guard", stance, title)
                : Lang.Get("lotrnpcs:recruit-yours", Lang.Get("lotrnpcs:order-" + Recruit.OrderOf(entity)), stance,
                    Lang.Get("lotrnpcs:formation-" + Formation.Valid(Recruit.FormationOf(entity))).ToLowerInvariant(), title));
        }

        // For the owner: right-click to talk, hold the orders key for orders. The game draws the orders key as it is
        // bound now, which may not be R, the way vanilla shows the tool mode key.
        static readonly WorldInteraction[] OwnerHelp =
        {
            new WorldInteraction { ActionLangCode = "lotrnpcs:help-talk", MouseButton = EnumMouseButton.Right },
            new WorldInteraction { ActionLangCode = "lotrnpcs:help-orders", HotKeyCode = RecruitOrdersSystem.HotkeyCode, MouseButton = EnumMouseButton.None },
        };

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled) =>
            ShowsOwnerHelp(entity, player?.PlayerUID) ? OwnerHelp : null;

        // A dead recruit's body offers only the harvest, not talk and orders.
        public static bool ShowsOwnerHelp(Entity e, string playerUid) =>
            playerUid != null && e.Alive && Recruit.OwnerUidOf(e) != null && Recruit.LeaderUidOf(e) == playerUid;

        public override string PropertyName() => "lotrnpcs:hireable";
    }

    /// <summary>Added to players' entities on the server when they join: tells their recruits when they are hurt.</summary>
    public class EntityBehaviorRecruitOwner : EntityBehavior
    {
        HiringSystem hiring;

        public EntityBehaviorRecruitOwner(Entity entity) : base(entity) { }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            if (damage <= 0 || damageSource?.Type == EnumDamageType.Heal || entity.Api.Side != EnumAppSide.Server || !(entity is EntityPlayer owner)) return;
            Entity attacker = damageSource?.GetCauseEntity();
            if (attacker == null || attacker == entity) return;
            hiring ??= entity.Api.ModLoader.GetModSystem<HiringSystem>();
            if (hiring?.Count(owner.PlayerUID) > 0) hiring.OnTeamHurt(owner.PlayerUID, attacker, entity.Pos.XYZ);
        }

        public override string PropertyName() => "lotrnpcs:recruitowner";
    }
}
