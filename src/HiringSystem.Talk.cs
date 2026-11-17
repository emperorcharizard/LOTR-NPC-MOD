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
    public partial class HiringSystem
    {
        // ---- grudges and helping each other ----

        public void RecordTeamGrudge(string uid, Entity attacker)
        {
            if (uid == null || attacker == null) return;
            if (!teamGrudges.TryGetValue(uid, out Dictionary<long, long> byAttacker)) teamGrudges[uid] = byAttacker = new Dictionary<long, long>();
            long now = Now;
            FactionSystem.RemoveExpired(byAttacker, now);
            byAttacker[attacker.EntityId] = now + (Factions?.Config.GrudgeSeconds ?? 30) * 1000L;
        }

        public bool HasTeamGrudge(string uid, long attackerId) =>
            uid != null && teamGrudges.TryGetValue(uid, out Dictionary<long, long> byAttacker) && byAttacker.TryGetValue(attackerId, out long until) && until >= Now;

        /// <summary>Is this creature going for the owner or one of their recruits right now (chasing or hitting)?</summary>
        public static bool IsTargetingTeam(Entity e, string uid)
        {
            // Players and entities outside a world (as in the tests) have no AI running.
            if (e == null || e is EntityPlayer || e.World == null) return false;
            IAiTask[] active = e.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.ActiveTasksBySlot;
            if (active == null) return false;
            foreach (IAiTask task in active)
            {
                Entity target = task is AiTaskSeekEntity || task is AiTaskMeleeAttack ? (task as AiTaskBaseTargetable).TargetEntity
                    : task is AiTaskSeekEntityR || task is AiTaskMeleeAttackR ? (task as AiTaskBaseTargetableR).TargetEntity : null;
                if (target != null && target.Alive && Recruit.IsTeam(target, uid)) return true;
            }
            return false;
        }

        /// <summary>The owner or one of their recruits near pos was hurt: every recruit of theirs nearby fights back.</summary>
        public void OnTeamHurt(string uid, Entity attacker, Vec3d pos)
        {
            if (attacker == null || !attacker.Alive || Recruit.IsTeam(attacker, uid)) return;
            if (attacker is EntityPlayer player && !FactionSystem.CanBeAttacked(player)) return;
            RecordTeamGrudge(uid, attacker);
            if (sapi == null) return;
            // Borrowed guards stand by their borrower too.
            foreach (EntityAgent recruit in Led(uid))
            {
                if (recruit.Pos.Dimension == attacker.Pos.Dimension && recruit.Pos.SquareDistanceTo(pos) <= RallyRange * RallyRange)
                    recruit.Notify("seekEntity", attacker);
            }
        }
        // ---- talking ----

        /// <summary>The talk menu this player gets from this NPC, with reply as the NPC's answer; null if the NPC
        /// won't talk to them (it is hostile to them).</summary>
        public TalkState StateFor(EntityAgent npc, IServerPlayer player, string reply = null)
        {
            string faction = FactionSystem.FactionOf(npc);
            if (faction == null || Factions == null || player.Entity == null) return null;
            TalkState state = new TalkState { Reply = reply, Cap = MaxRecruits, Hired = Count(player.PlayerUID), FollowCap = MaxFollowers, Following = Followers(player.PlayerUID).Count };
            string owner = Recruit.OwnerUidOf(npc), borrower = Recruit.BorrowerUidOf(npc);
            if (owner == player.PlayerUID)
            {
                state.Mode = "own";
                state.Order = Recruit.OrderOf(npc);
                if (borrower != null) state.BorrowerName = npc.WatchedAttributes.GetString(Recruit.BorrowerNameAttr);
                Factions.Config.Factions.TryGetValue(faction, out FactionDef ownDef);
                state.Coin = ownDef?.HireCoin ?? FactionConfig.DefaultHireCoin;
                if (Promotions != null) state.Promote = Promotions.Offers(npc, ownDef);
                state.Classes = OtherClasses(npc);
                // No captain offer while the player's captain places are full.
                if (state.Promote != null && state.Promote.ContainsKey("captain") && RankOf(npc) != "captain" && !CaptainFits(player.PlayerUID, faction))
                {
                    state.Promote = new Dictionary<string, int>(state.Promote);
                    state.Promote.Remove("captain");
                    state.CaptainsFull = true;
                }
            }
            else if (owner != null && borrower == player.PlayerUID)
            {
                state.Mode = "borrowed";
                state.Order = Recruit.OrderOf(npc);
                state.OwnerName = npc.WatchedAttributes.GetString(Recruit.OwnerNameAttr);
            }
            else if (owner != null)
            {
                if (Factions.RecruitWantsToAttack(npc, owner, player.Entity)) return null;
                state.OwnerName = npc.WatchedAttributes.GetString(Recruit.OwnerNameAttr);
                state.Mode = borrower == null && Recruit.IsShared(npc) && SharesGroup(owner, player.Entity) ? "shared" : "others";
            }
            else if (Factions.WantsToAttack(faction, player.Entity)) return null;
            else if (Factions.IsFriend(faction, player.Entity))
            {
                Factions.Config.Factions.TryGetValue(faction, out FactionDef def);
                state.Mode = "member";
                state.Price = PriceFor(def, RankOf(npc));
                state.Coin = def?.HireCoin ?? FactionConfig.DefaultHireCoin;
            }
            else state.Mode = "stranger";
            // The room line for the menus that can hire, follow or borrow.
            if (state.Mode == "member" || state.Mode == "own" || state.Mode == "borrowed" || state.Mode == "shared")
            {
                state.Room = Room(player.PlayerUID, faction);
                state.RosterRoom = RosterRoom(player.PlayerUID, faction);
            }
            return state;
        }

        /// <summary>Does what the player chose in the talk menu. Returns the menu to show next (null: close it).</summary>
        public TalkState Act(EntityAgent npc, IServerPlayer player, string action)
        {
            double range = EntityBehaviorHireable.TalkRange + 2;
            if (!npc.Alive || player.Entity == null || player.Entity.Pos.SquareDistanceTo(npc.Pos) > range * range) return null;
            TalkState state = StateFor(npc, player);
            if (state == null) return null;
            switch (action)
            {
                case "hire":
                    return state.Mode == "member" ? Hire(npc, player, state) : state;
                case string trainAs when trainAs.StartsWith("class:"):
                    {
                        string cls = trainAs.Substring("class:".Length);
                        if (state.Mode != "own" || !state.Classes.Contains(cls)) return state;
                        return ChangeClass(npc, player, cls) ? null : state;
                    }
                case "borrow":
                    {
                        if (state.Mode != "shared") return state;
                        // A borrowed guard takes its faction's share of the borrower's following.
                        if (!FollowFits(player.PlayerUID, npc)) return StateFor(npc, player, "talk-followfull");
                        Recruit.Lend(npc, player.PlayerUID, player.PlayerName);
                        Recruit.SetOrder(npc, Recruit.Follow);
                        Recruit.SetFollowSlot(npc, null);
                        Disengage(npc);
                        RefreshFollowSlots(EnsureOwner(player), player.Entity);
                        sapi?.Logger.Notification("[lotrnpcs] " + player.PlayerName + " borrowed " + state.OwnerName + "'s shared guard " + npc.Code + " (" + npc.EntityId + ").");
                        return StateFor(npc, player, "talk-borrowed");
                    }
                case Recruit.Follow:
                case Recruit.Hold:
                case Recruit.Guard:
                case Recruit.GuardShared:
                    {
                        if (state.Mode != "own" && state.Mode != "borrowed") return state;
                        if (action == Recruit.Follow && !Followers(player.PlayerUID).Contains(npc) && !FollowFits(player.PlayerUID, npc))
                            return StateFor(npc, player, "talk-followfull");
                        bool guard = action == Recruit.Guard || action == Recruit.GuardShared;
                        string reply = "talk-" + action;
                        string borrower = Recruit.BorrowerUidOf(npc);
                        if (state.Mode == "own")
                        {
                            // The owner's word takes a lent guard back, and decides whether it is shared.
                            Recruit.ClearBorrower(npc);
                            Recruit.SetShared(npc, action == Recruit.GuardShared);
                        }
                        else if (guard)
                        {
                            // A borrower who posts a shared guard gives it back: it stays shared.
                            Recruit.ClearBorrower(npc);
                            reply = "talk-guard-shared";
                        }
                        // Guard this area: the post is where the player stands, as with Guard here in the orders menu.
                        if (guard) Recruit.SetGuardAt(npc, PostAt(player.Entity));
                        else Recruit.SetOrder(npc, action);
                        Recruit.SetFollowSlot(npc, null);
                        RefreshFollowSlots(EnsureOwner(player), player.Entity);
                        if (borrower != null && borrower != player.PlayerUID) RefreshBorrowerSlots(borrower);
                        return StateFor(npc, player, reply);
                    }
                case string promote when promote.StartsWith("promote:"):
                    {
                        if (state.Mode != "own") return state;
                        string refusal = PromoteRecruit(npc, player, promote.Substring("promote:".Length), out int price);
                        // Done: the menu belonged to the old NPC, so it closes.
                        if (refusal == null) return null;
                        TalkState refused = StateFor(npc, player, refusal);
                        refused.Price = price;
                        return refused;
                    }
                case "dismiss":
                    if (state.Mode != "own") return state;
                    Dismiss(npc, player.PlayerUID);
                    sapi?.Logger.Notification("[lotrnpcs] " + player.PlayerName + " dismissed their recruit " + npc.Code + " (" + npc.EntityId + ").");
                    return StateFor(npc, player, "talk-dismissed");
                default:
                    return state;
            }
        }

        /// <summary>The recruit classes (Promotion.RecruitClasses) this NPC could take besides its own, whose entity
        /// types exist; empty for any other rank.</summary>
        List<string> OtherClasses(Entity npc)
        {
            string rank = RankOf(npc);
            if (Promotion.BaseRank(rank) != "recruit" || sapi == null) return new List<string>();
            return Promotion.RecruitClasses.Where(c => c != rank && sapi.World.GetEntityType(Promotion.CodeAtRank(npc.Code, c)) != null).ToList();
        }

        /// <summary>The player's recruit becomes another recruit class (a new NPC, as a promotion makes), keeping its
        /// XP, for free. True when done (the owner is told in chat).</summary>
        bool ChangeClass(EntityAgent npc, IServerPlayer player, string cls)
        {
            int xp = Promotion.Xp(npc);
            string before = Recruit.TitleInSentence(npc, player.LanguageCode);
            EntityAgent next = Promotions?.Promote(npc, cls);
            if (next == null) return false;
            next.WatchedAttributes.SetInt(Promotion.XpAttr, xp);
            next.WatchedAttributes.MarkPathDirty(Promotion.XpAttr);
            player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.GetL(player.LanguageCode, "lotrnpcs:promote-done", before, Recruit.TitleInSentence(next, player.LanguageCode)), EnumChatType.Notification);
            sapi?.Logger.Notification("[lotrnpcs] " + player.PlayerName + " made their recruit a " + cls + ".");
            return true;
        }

        /// <summary>
        /// Promotes the player's own recruit to rank if its XP is full, the rank is on offer and the player pays
        /// (from the talk menu, or the promotion screen at any distance). Null when done (the owner is told in chat),
        /// else the reply's lang key, with the price it asked for.
        /// </summary>
        public string PromoteRecruit(EntityAgent npc, IServerPlayer player, string rank, out int price)
        {
            price = 0;
            TalkState state = npc != null && npc.Alive ? StateFor(npc, player) : null;
            if (state?.Mode != "own") return "promote-unavailable";
            if (rank == "captain" && state.CaptainsFull) return "talk-captains-full";
            if (state.Promote == null || !state.Promote.TryGetValue(rank, out price)) return "promote-unavailable";
            if (Pay(player, state.Coin, price) is string poor) return poor == "talk-poor" ? "talk-promote-poor" : "talk-promote-nocoin";
            string before = Recruit.TitleInSentence(npc, player.LanguageCode);
            EntityAgent next = Promotions?.Promote(npc, rank);
            if (next == null) return "promote-unavailable";
            player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.GetL(player.LanguageCode, "lotrnpcs:promote-done", before, Recruit.TitleInSentence(next, player.LanguageCode)), EnumChatType.Notification);
            sapi?.Logger.Notification("[lotrnpcs] " + player.PlayerName + " promoted their recruit to " + rank + " for " + price + " " + state.Coin + ".");
            return null;
        }
        // ---- the roster screen and the promotion screen (Roster.cs) ----

        /// <summary>Server: remembers how a recruit is now, for the roster while it isn't loaded.</summary>
        public void Summarize(Entity npc)
        {
            if (Recruit.OwnerUidOf(npc) is string uid && Owner(uid) is HiringOwner owner && owner.Recruits.Contains(npc.EntityId))
                (owner.Summaries ??= new Dictionary<long, RecruitSummary>())[npc.EntityId] = RecruitSummary.Of(npc);
        }

        /// <summary>Server: the roster screen's contents for this player. Loaded recruits are summarized afresh.</summary>
        public RosterState RosterFor(IServerPlayer player, string notice = null)
        {
            string uid = player.PlayerUID;
            string L(string key, object[] args) => Lang.GetL(player.LanguageCode, "lotrnpcs:" + key, args);
            RosterState state = new RosterState { Notice = notice };
            HiringOwner owner = Owner(uid);
            if (owner == null || sapi == null) { state.Header.Add(L("roster-summary", new object[] { 0, 0 })); return state; }
            owner.Summaries ??= new Dictionary<long, RecruitSummary>();
            foreach (long id in owner.Recruits)
            {
                Entity e = sapi.World.GetEntityById(id);
                bool loaded = e != null && e.Alive && Recruit.OwnerUidOf(e) == uid;
                if (loaded) owner.Summaries[id] = RecruitSummary.Of(e);
                owner.Summaries.TryGetValue(id, out RecruitSummary sum);
                owner.RecruitFactions.TryGetValue(id, out string faction);
                state.Rows.Add(new RosterRow
                {
                    Id = id, Loaded = loaded, Known = sum != null, Code = sum?.Code, Name = sum?.Name, Rank = sum?.Rank, Faction = sum?.Faction ?? faction,
                    Xp = sum?.Xp ?? 0, Need = Promotion.Need(sum?.Rank), Order = sum?.Order, Stance = sum?.Stance, Borrower = sum?.Borrower,
                    X = sum?.X ?? 0, Y = sum?.Y ?? 0, Z = sum?.Z ?? 0, Dimension = sum?.Dimension ?? 0,
                    MinutesAgo = sum == null || loaded ? 0 : Math.Max(0, (int)(DateTime.UtcNow - sum.Seen).TotalMinutes),
                });
            }
            state.Header.Add(L("roster-summary", new object[] { owner.Recruits.Count, Followers(uid).Count }));
            string room = (RoomText(Room(uid, null), L) + " " + RosterText(RosterRoom(uid, null), L)).Trim();
            if (room.Length > 0) state.Header.Add(room);
            if (owner.Captains != null && owner.Captains.Count > 0) state.Header.Add(L("roster-captains", new object[] { Percent(CaptainsUsed(uid)) }));
            // Ranked places and recruit-only places in use.
            List<EntityAgent> band = Followers(uid);
            int recruits = band.Count(e => Promotion.BaseRank(RankOf(e)) == "recruit"), extra = RecruitExtra, inExtra = Math.Min(recruits, extra);
            state.Header.Add(L("roster-band", new object[] { band.Count - inExtra, MaxFollowers, inExtra, extra }));
            return state;
        }

        /// <summary>Server: a roster row's button (call, dismiss). Returns what to tell the player.</summary>
        public string RosterAction(IServerPlayer player, string action, long id)
        {
            string uid = player.PlayerUID;
            string L(string key, params object[] args) => Lang.GetL(player.LanguageCode, "lotrnpcs:" + key, args);
            if (sapi == null || !(Owner(uid) is HiringOwner owner) || !owner.Recruits.Contains(id)) return L("roster-gone");
            EntityAgent npc = sapi.World.GetEntityById(id) as EntityAgent;
            bool loaded = npc != null && npc.Alive && Recruit.OwnerUidOf(npc) == uid;
            switch (action)
            {
                case "call":
                    {
                        if (!loaded || player.Entity == null) return L("roster-notloaded");
                        if (Recruit.OrderOf(npc) == Recruit.Follow && Recruit.BorrowerUidOf(npc) == null) return L("roster-already", Recruit.TitleInSentence(npc, player.LanguageCode));
                        if (!FollowFits(uid, npc)) return L("roster-call-full");
                        string borrower = Recruit.BorrowerUidOf(npc);
                        // As Follow me in the talk menu: the owner's word takes a lent guard back.
                        Recruit.ClearBorrower(npc);
                        Recruit.SetShared(npc, false);
                        Recruit.SetOrder(npc, Recruit.Follow);
                        Recruit.SetFollowSlot(npc, null);
                        RefreshFollowSlots(owner, player.Entity);
                        if (borrower != null && borrower != uid) RefreshBorrowerSlots(borrower);
                        Learn(owner, npc);
                        return L("roster-called", Recruit.TitleInSentence(npc, player.LanguageCode));
                    }
                case "dismiss":
                    {
                        if (loaded)
                        {
                            string title = Recruit.TitleInSentence(npc, player.LanguageCode);
                            Dismiss(npc, uid);
                            if (player.Entity != null) RefreshFollowSlots(owner, player.Entity);
                            sapi.Logger.Notification("[lotrnpcs] " + player.PlayerName + " dismissed their recruit " + npc.Code + " (" + id + ") from the roster.");
                            return L("roster-dismissed", title);
                        }
                        // As /recruits dismissall: off the list now, set free when it next loads.
                        owner.Recruits.Remove(id);
                        if (!owner.PendingDismiss.Contains(id)) owner.PendingDismiss.Add(id);
                        sapi.Logger.Notification("[lotrnpcs] " + player.PlayerName + " dismissed their unloaded recruit " + id + " from the roster.");
                        return L("roster-dismissed-later");
                    }
                default:
                    return null;
            }
        }

        /// <summary>Server: the promotion screen for the player's own loaded recruit, or null when it can't be promoted.</summary>
        public PromoteMenu PromoteMenuFor(IServerPlayer player, long id)
        {
            if (sapi == null || !(sapi.World.GetEntityById(id) is EntityAgent npc) || !npc.Alive) return null;
            TalkState state = StateFor(npc, player);
            if (state?.Mode != "own" || ((state.Promote == null || state.Promote.Count == 0) && !state.CaptainsFull)) return null;
            string faction = FactionSystem.FactionOf(npc);
            AssetLocation coin = new AssetLocation(state.Coin);
            PromoteMenu menu = new PromoteMenu
            {
                Id = id, Code = npc.Code.ToShortString(), Faction = faction, Title = Recruit.Title(npc, player.LanguageCode), Xp = Promotion.Xp(npc),
                Need = Promotion.Need(RankOf(npc)), Coin = state.Coin, CaptainsFull = state.CaptainsFull,
                Coins = Purse(player).Where(slot => WildcardUtil.Match(coin, slot.Itemstack.Collectible.Code)).Sum(slot => slot.StackSize),
            };
            foreach (string rank in Promotion.Targets(RankOf(npc)))
            {
                bool offered = state.Promote != null && state.Promote.ContainsKey(rank);
                if (!offered && !(rank == "captain" && state.CaptainsFull)) continue;
                menu.Cards.Add(new PromoteCard
                {
                    Rank = rank, Price = offered ? state.Promote[rank] : 0, Open = offered,
                    Health = Factions?.Config.HealthFor(faction, rank) ?? 0, Damage = Factions?.Config.DamageFor(faction, rank) ?? 0,
                });
            }
            return menu;
        }

        TalkState Hire(EntityAgent npc, IServerPlayer player, TalkState state)
        {
            // Room on the roster for one more of this faction (shares, not a count).
            string faction = FactionSystem.FactionOf(npc);
            if (!Fits(RosterUsed(player.PlayerUID), RosterShare(faction))) { state.Reply = "talk-full"; return state; }
            if (RankOf(npc) == "captain" && !CaptainFits(player.PlayerUID, faction)) { state.Reply = "talk-captains-full"; return state; }
            if (Pay(player, state.Coin, state.Price) is string poor) { state.Reply = poor; return state; }

            // Past the follow share, a new recruit guards the spot where it was hired instead of following.
            bool guards = !FollowFits(player.PlayerUID, npc);
            Recruit.Bind(npc, player.PlayerUID, player.PlayerName);
            // A name befitting its race, unless it already has one.
            Looks.NameOnHire(npc, sapi.World.Rand);
            if (guards) Recruit.SetGuardAt(npc, PostAt(npc));
            // Recruits never despawn as zone NPCs do. The old origin comes back if they are dismissed.
            string origin = npc.Attributes.GetString(OriginAttr);
            if (origin != null) npc.Attributes.SetString(OriginBeforeAttr, origin);
            npc.Attributes.SetString(OriginAttr, "hired");
            HiringOwner owner = Owner(player.PlayerUID) ?? (owners[player.PlayerUID] = new HiringOwner());
            Refresh(owner, player);
            if (!owner.Recruits.Contains(npc.EntityId)) owner.Recruits.Add(npc.EntityId);
            Learn(owner, npc);
            Promotion.SyncOwnerGroups(npc, owner.Groups);
            npc.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.StopTask(typeof(AiTaskWander));
            if (player.Entity != null) RefreshFollowSlots(owner, player.Entity);
            sapi.Logger.Notification("[lotrnpcs] " + player.PlayerName + " hired " + npc.Code + " (" + npc.EntityId + ") for " + state.Price + " " + state.Coin
                + ", now " + owner.Recruits.Count + " recruits, roster " + Percent(RosterUsed(player.PlayerUID)) + " used.");
            // Tell the player how much room is left on their roster.
            string Lg(string k, object[] a) => Lang.GetL(player.LanguageCode, "lotrnpcs:" + k, a);
            player.SendMessage(GlobalConstants.GeneralChatGroup, Lg("hired-room", new object[] { Recruit.TitleInSentence(npc, player.LanguageCode),
                RoomText(Room(player.PlayerUID, faction), Lg), RosterText(RosterRoom(player.PlayerUID, faction), Lg) }).Trim(), EnumChatType.Notification);
            return StateFor(npc, player, guards ? "talk-hired-guard" : "talk-hired");
        }

        /// <summary>Takes price coins from the player's hotbar and backpack. Null when paid, else the reply's lang key
        /// (not enough coins, or no such coin in this world).</summary>
        string Pay(IServerPlayer player, string coinCode, int price)
        {
            if (price <= 0) return null;
            AssetLocation coin = new AssetLocation(coinCode);
            List<ItemSlot> slots = Purse(player).Where(s => WildcardUtil.Match(coin, s.Itemstack.Collectible.Code)).ToList();
            if (slots.Sum(s => s.StackSize) < price)
                return sapi.World.Collectibles.Any(c => c?.Code != null && WildcardUtil.Match(coin, c.Code)) ? "talk-poor" : "talk-nocoin";
            int left = price;
            foreach (ItemSlot slot in slots)
            {
                int take = Math.Min(left, slot.StackSize);
                slot.TakeOut(take);
                slot.MarkDirty();
                left -= take;
                if (left <= 0) break;
            }
            return null;
        }

        /// <summary>A promoted recruit (PromotionSystem) takes the old one's place in its owner's list and formation.</summary>
        public void Replace(Entity old, Entity next)
        {
            string uid = Recruit.OwnerUidOf(next);
            if (!(Owner(uid) is HiringOwner owner)) return;
            int at = owner.Recruits.IndexOf(old.EntityId);
            if (at >= 0) owner.Recruits[at] = next.EntityId;
            else if (!owner.Recruits.Contains(next.EntityId)) owner.Recruits.Add(next.EntityId);
            // Same faction (promotion only changes the rank), so the same shares.
            owner.RecruitFactions.Remove(old.EntityId);
            owner.Captains?.Remove(old.EntityId);
            owner.Summaries?.Remove(old.EntityId);
            Learn(owner, next);
            Promotion.SyncOwnerGroups(next, owner.Groups);
            // Past the ranked places a newly ranked recruit stays on the roster but guards.
            if (Recruit.OrderOf(next) == Recruit.Follow && !FollowFits(uid, next, old))
            {
                Recruit.SetGuardAt(next, next.Pos.XYZ);
                Recruit.SetFollowSlot(next, null);
                if (sapi?.World.PlayerByUid(uid) is IServerPlayer told)
                    told.SendMessage(GlobalConstants.GeneralChatGroup, Lang.GetL(told.LanguageCode, "lotrnpcs:promote-guards", Recruit.TitleInSentence(next, told.LanguageCode)), EnumChatType.Notification);
            }
            if (sapi?.World.PlayerByUid(uid)?.Entity is EntityPlayer by) RefreshFollowSlots(owner, by);
        }

        TextCommandResult DismissAllCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only a player can have recruits.");
            string L(string key, params object[] a) => Lang.GetL(player.LanguageCode, "lotrnpcs:" + key, a);
            int count = Count(player.PlayerUID);
            if (count == 0) return TextCommandResult.Success(L("dismissall-none"));
            if (!string.Equals(args[0] as string, "confirm", StringComparison.OrdinalIgnoreCase))
                return TextCommandResult.Success(L("dismissall-ask", count));
            (int now, int later) = DismissAll(player.PlayerUID, id => sapi.World.GetEntityById(id) as EntityAgent);
            sapi.Logger.Notification("[lotrnpcs] " + player.PlayerName + " dismissed all their recruits: " + now + " now, " + later + " when their chunks load.");
            return TextCommandResult.Success(later == 0 ? L("dismissall-done", now) : L("dismissall-done-later", now + later, later));
        }

        /// <summary>
        /// Dismisses every recruit of this owner: loaded ones at once, the rest when they next load (PendingDismiss).
        /// find looks a loaded entity up by id. Returns how many went now and how many later. Public for tests.
        /// </summary>
        public (int Now, int Later) DismissAll(string uid, System.Func<long, EntityAgent> find)
        {
            if (!(Owner(uid) is HiringOwner owner)) return (0, 0);
            int now = 0, later = 0;
            foreach (long id in owner.Recruits.ToList())
            {
                EntityAgent npc = find(id);
                if (npc != null && npc.Alive && Recruit.OwnerUidOf(npc) == uid)
                {
                    Dismiss(npc, uid);
                    now++;
                }
                else
                {
                    owner.Recruits.Remove(id);
                    if (npc == null && !owner.PendingDismiss.Contains(id)) { owner.PendingDismiss.Add(id); later++; }
                }
            }
            return (now, later);
        }

        /// <summary>A recruit that loads after /recruits dismissall while it was away is set free now. True if it was.</summary>
        public bool DismissIfPending(EntityAgent npc)
        {
            if (npc == null || !(Recruit.OwnerUidOf(npc) is string uid) || !(Owner(uid) is HiringOwner owner) || !owner.PendingDismiss.Remove(npc.EntityId)) return false;
            Dismiss(npc, uid);
            sapi?.Logger.Notification("[lotrnpcs] " + owner.Name + "'s recruit " + npc.Code + " (" + npc.EntityId + ") was dismissed on load (dismissall).");
            return true;
        }

        void Dismiss(EntityAgent npc, string uid)
        {
            Recruit.Release(npc);
            string before = npc.Attributes.GetString(OriginBeforeAttr);
            if (before != null) npc.Attributes.SetString(OriginAttr, before);
            else npc.Attributes.RemoveAttribute(OriginAttr);
            npc.Attributes.RemoveAttribute(OriginBeforeAttr);
            Owner(uid)?.Recruits.Remove(npc.EntityId);
            Owner(uid)?.RecruitFactions.Remove(npc.EntityId);
        }
    }
}
