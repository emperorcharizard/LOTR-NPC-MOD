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
        // ---- over the limits: recruits past the roster or following are dismissed ----

        /// <summary>
        /// Brings a player within their roster and follow shares after the limits were lowered (on joining, and after
        /// /recruits faction reload). First, while the roster is over, the most recently hired recruit whose faction
        /// is known goes (loaded ones now, the others when they next load, as /recruits dismissall does); recruits
        /// whose faction isn't known yet are never dismissed. Then, while the following is over, shared guards they
        /// borrowed go back to watch where they stand (they belong to someone else), and then the most recently hired
        /// of their own followers are dismissed. find looks a loaded entity up by id; titleOf names one for the chat
        /// line. Returns the titles of those dismissed now and how many go later. Public for tests.
        /// </summary>
        public (List<string> Gone, int Later, int Returned) EnforceLimits(string uid, System.Func<long, EntityAgent> find, System.Func<EntityAgent, string> titleOf)
        {
            List<string> gone = new List<string>();
            int later = 0, returned = 0;
            if (!(Owner(uid) is HiringOwner owner)) return (gone, later, returned);
            for (int i = owner.Recruits.Count - 1; i >= 0 && Over(RosterUsed(uid)); i--)
            {
                long id = owner.Recruits[i];
                if (!owner.RecruitFactions.ContainsKey(id)) continue;
                EntityAgent npc = find(id);
                if (npc != null && npc.Alive && Recruit.OwnerUidOf(npc) == uid)
                {
                    gone.Add(titleOf(npc));
                    Dismiss(npc, uid);
                }
                else
                {
                    owner.Recruits.RemoveAt(i);
                    owner.RecruitFactions.Remove(id);
                    if (npc == null && !owner.PendingDismiss.Contains(id)) { owner.PendingDismiss.Add(id); later++; }
                }
            }
            if (Over(FollowUsed(uid)))
            {
                foreach (EntityAgent borrowed in Followers(uid).Where(e => Recruit.OwnerUidOf(e) != uid).ToList())
                {
                    if (!Over(FollowUsed(uid))) break;
                    Recruit.ClearBorrower(borrowed);
                    Recruit.SetGuardAt(borrowed, borrowed.Pos.XYZ);
                    Recruit.SetFollowSlot(borrowed, null);
                    returned++;
                }
                List<EntityAgent> own = Followers(uid).Where(e => Recruit.OwnerUidOf(e) == uid).OrderByDescending(e => owner.Recruits.IndexOf(e.EntityId)).ToList();
                foreach (EntityAgent npc in own)
                {
                    if (!Over(FollowUsed(uid))) break;
                    gone.Add(titleOf(npc));
                    Dismiss(npc, uid);
                }
            }
            return (gone, later, returned);
        }

        /// <summary>EnforceLimits for an online player, telling them and the server log what happened.</summary>
        public void EnforceLimits(IServerPlayer player)
        {
            if (sapi == null || player?.Entity == null || Owner(player.PlayerUID) == null) return;
            (List<string> gone, int later, int returned) = EnforceLimits(player.PlayerUID, id => sapi.World.GetEntityById(id) as EntityAgent,
                npc => Recruit.TitleInSentence(npc, player.LanguageCode));
            if (gone.Count == 0 && later == 0 && returned == 0) return;
            string L(string key, params object[] a) => Lang.GetL(player.LanguageCode, "lotrnpcs:" + key, a);
            if (gone.Count + later > 0)
                player.SendMessage(GlobalConstants.GeneralChatGroup, L("limits-dismissed", gone.Count == 0 ? later : gone.Count, gone.Count == 0 ? L("limits-away") : string.Join(", ", gone))
                    + (later > 0 && gone.Count > 0 ? " " + L("limits-dismissed-later", later) : ""), EnumChatType.Notification);
            if (returned > 0) player.SendMessage(GlobalConstants.GeneralChatGroup, L("limits-returned", returned), EnumChatType.Notification);
            if (Owner(player.PlayerUID) is HiringOwner owner) RefreshFollowSlots(owner, player.Entity);
            sapi.Logger.Notification("[lotrnpcs] " + player.PlayerName + " was over the recruit limits: dismissed " + gone.Count + " now ("
                + string.Join(", ", gone) + "), " + later + " when they load; gave back " + returned + " borrowed guards.");
        }

        /// <summary>EnforceLimits for every online player (after /recruits faction reload).</summary>
        public void EnforceLimitsOnline()
        {
            if (sapi == null) return;
            foreach (IPlayer p in sapi.World.AllOnlinePlayers)
                if (p is IServerPlayer sp && sp.ConnectionState == EnumClientState.Playing) EnforceLimits(sp);
        }

        /// <summary>
        /// A recruit loaded. Its faction is remembered for its roster share. A recruit whose faction was only learned
        /// now may put its owner over the roster, which is then put right as on joining. A follower that loads into a
        /// following that has filled up meanwhile (it was in an unloaded chunk) never counts as over: it keeps watch
        /// where it stands instead, like a hire past the following.
        /// </summary>
        public void OnRecruitLoaded(EntityAgent npc)
        {
            if (npc == null || !npc.Alive || !(Recruit.OwnerUidOf(npc) is string uid) || !(Owner(uid) is HiringOwner owner) || !owner.Recruits.Contains(npc.EntityId)) return;
            bool learned = !owner.RecruitFactions.ContainsKey(npc.EntityId);
            Learn(owner, npc);
            if (learned && Over(RosterUsed(uid)) && sapi?.World.PlayerByUid(uid) is IServerPlayer online && online.ConnectionState == EnumClientState.Playing)
                EnforceLimits(online);
            else if (learned && Over(RosterUsed(uid)))
                EnforceLimits(uid, id => sapi?.World.GetEntityById(id) as EntityAgent, e => e.Code.ToShortString());
            string leader = Recruit.LeaderUidOf(npc);
            // Whether or not the game lists it among the loaded entities yet: would it fit beside the others?
            if (npc.Alive && Recruit.OrderOf(npc) == Recruit.Follow && leader != null && !FollowFits(leader, npc))
            {
                if (Recruit.BorrowerUidOf(npc) != null) Recruit.ClearBorrower(npc);
                Recruit.SetGuardAt(npc, npc.Pos.XYZ);
                Recruit.SetFollowSlot(npc, null);
            }
        }

        // The hotbar and the backpack with its bags, where a player carries coins.
        static IEnumerable<ItemSlot> Purse(IPlayer player)
        {
            foreach (string inventory in new[] { GlobalConstants.hotBarInvClassName, GlobalConstants.backpackInvClassName })
            {
                IInventory inv = player.InventoryManager?.GetOwnInventory(inventory);
                if (inv == null) continue;
                foreach (ItemSlot slot in inv)
                {
                    if (slot?.Itemstack?.Collectible?.Code != null && slot.StackSize > 0) yield return slot;
                }
            }
        }

        /// <summary>A recruit died or was removed for good (not unloaded).</summary>
        public void OnRecruitGone(Entity npc, bool died)
        {
            string uid = Recruit.OwnerUidOf(npc);
            if (Owner(uid) is not HiringOwner owner) return;
            owner.Recruits.Remove(npc.EntityId);
            owner.RecruitFactions.Remove(npc.EntityId);
            owner.Captains?.Remove(npc.EntityId);
            if (!died || sapi == null) return;
            sapi.Logger.Notification("[lotrnpcs] " + owner.Name + "'s recruit " + npc.Code + " (" + npc.EntityId + ") died.");
            if (sapi.World.PlayerByUid(uid) is IServerPlayer player && player.ConnectionState == EnumClientState.Playing)
                player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.GetL(player.LanguageCode, "lotrnpcs:recruit-died", Recruit.TitleInSentence(npc, player.LanguageCode)), EnumChatType.Notification);
        }
        // ---- upkeep ----

        // The owner's name, race and groups as they are now. Unchanged values keep their objects, so what the faction
        // system worked out from them stays valid.
        static void Refresh(HiringOwner owner, IPlayer player)
        {
            owner.Name = player.PlayerName;
            owner.EntityId = player.Entity?.EntityId ?? 0;
            if (player.Entity == null) return;
            string race = FactionSystem.RaceOf(player.Entity);
            if (race != owner.Race) owner.Race = race;
            List<string> groups = FactionSystem.GroupsOf(player.Entity);
            if (owner.Groups == null || !owner.Groups.SequenceEqual(groups)) owner.Groups = groups;
        }

        void OnPlayerNowPlaying(IServerPlayer player)
        {
            if (player.Entity == null) return;
            // Recruits help when their owner is hurt; this behavior tells them.
            if (player.Entity.GetBehavior<EntityBehaviorRecruitOwner>() == null) player.Entity.AddBehavior(new EntityBehaviorRecruitOwner(player.Entity));
            if (Owner(player.PlayerUID) is HiringOwner owner)
            {
                Refresh(owner, player);
                // Over limits an admin lowered, on joining.
                EnforceLimits(player);
            }
        }

        int ticks;

        void OnTick(float dt)
        {
            bool summarizeNow = ++ticks % 30 == 0;
            foreach (IPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (Owner(player.PlayerUID) is not HiringOwner owner || !(player.Entity is EntityPlayer ownerEntity)) continue;
                Refresh(owner, player);
                UpdateHeading(owner, ownerEntity);
            }
            // Every owner's loaded recruits, online or not, since a borrower may lead them while the owner is away.
            foreach (KeyValuePair<string, HiringOwner> kv in owners)
            {
                List<long> list = kv.Value.Recruits;
                // Backwards, so dropping one doesn't skip the next.
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Entity recruit = sapi.World.GetEntityById(list[i]);
                    if (recruit == null) continue;   // not loaded: it stays on the list
                    if (!recruit.Alive || Recruit.OwnerUidOf(recruit) != kv.Key) { ForgetFollowFight(list[i]); list.RemoveAt(i); continue; }
                    // Who may see its XP: the owner and the owner's /group.
                    Promotion.SyncOwnerGroups(recruit, kv.Value.Groups);
                    // The roster's picture of it, every 30 seconds.
                    if (summarizeNow) (kv.Value.Summaries ??= new Dictionary<long, RecruitSummary>())[recruit.EntityId] = RecruitSummary.Of(recruit);
                    // A borrower who left the game gives the guard back: it keeps watch where it stands.
                    if (Recruit.BorrowerUidOf(recruit) != null && Recruit.OnlineLeader(recruit) == null)
                    {
                        Recruit.ClearBorrower(recruit);
                        Recruit.SetGuardAt(recruit, recruit.Pos.XYZ);
                        Recruit.SetFollowSlot(recruit, null);
                        continue;
                    }
                    // Only followers come along; holders and guards stay at their posts.
                    EntityPlayer leader = Recruit.OnlineLeader(recruit);
                    bool follows = leader != null && Recruit.OrderOf(recruit) == Recruit.Follow && leader.Alive && recruit.Pos.Dimension == leader.Pos.Dimension;
                    // A charge ends after ChargeMs, or ChargeQuietMs after the last enemy near the recruit.
                    if (charging.TryGetValue(recruit.EntityId, out var ch))
                    {
                        long nowMs = Now;
                        bool enemyNear = Factions != null && sapi.World.GetEntitiesAround(recruit.Pos.XYZ, (float)ChargeQuietRange, (float)ChargeQuietRange / 2,
                            e => e != recruit && Factions.RecruitWantsToAttack(recruit, kv.Key, e)).Length > 0;
                        long quiet = enemyNear ? 0 : ch.QuietSince > 0 ? ch.QuietSince : nowMs;
                        if (ChargeOver(nowMs, ch.Until, quiet)) EndCharge(recruit.EntityId);
                        else charging[recruit.EntityId] = (ch.Until, quiet);
                    }
                    // Formation followers too far from the leader break off their fight.
                    if (UpdateFollowFight(recruit, follows ? leader : null) && recruit is EntityAgent agent) StopFights(agent);
                    // A loose follower in a fight isn't pulled away from it; it comes when the fight is over.
                    bool looseFighting = Recruit.FormationOf(recruit) == Formation.Loose
                        && recruit.GetBehavior<EntityBehaviorFactionMember>()?.Fighting(sapi.World.ElapsedMilliseconds) == true;
                    if (follows && !looseFighting && recruit.Pos.SquareDistanceTo(leader.Pos) > TeleportDistance * TeleportDistance)
                        TeleportNear(recruit, leader);
                }
            }
        }

        // A free spot on solid ground a few blocks from the owner, as vanilla's stay-close task finds one.
        static void TeleportNear(Entity recruit, Entity owner)
        {
            Random rnd = recruit.World.Rand;
            for (int i = 1; i < 31; i++)
            {
                float range = GameMath.Clamp(i / 5f, 2, 4.5f);
                double x = owner.Pos.X + rnd.NextDouble() * 2 * range - range, z = owner.Pos.Z + rnd.NextDouble() * 2 * range - range;
                if (Ground.StandHeight(recruit.World.BlockAccessor, x, owner.Pos.Y, z, owner.Pos.Dimension, 8) is double y)
                {
                    recruit.TeleportToDouble(x, y, z);
                    return;
                }
            }
        }
    }
}
