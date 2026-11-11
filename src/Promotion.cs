using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// Recruits earn XP and are promoted (0.0.76, user, 2026-11-04):
    /// "I want recruits that hit a target that dies within the next 10 seconds to get 1 xp, at 15 xp, they have the option
    /// to rank up to warrior or archer for 10 coins, then another 40 xp for warrior to rank up to captain for 30 coins,
    /// theres no second rank for archers"; "they promote in the right clicked menu as a chat option ... Xp is capped when
    /// they hit the requirement and doesnt carry over. NPCS only, not animals. Archers and captains do not earn xp. It can
    /// be shown under their name and under health labeled 'XP: (x/x)' ... keep as much of the owners choices as possible
    /// and swap the armor to the new rank"; "count only npcs from this mod. the owner of the recruit and the others in the
    /// group sees the xp. The helmet line is correct."
    /// </summary>
    public static class Promotion
    {
        /// <summary>Watched: the recruit's XP toward its next rank.</summary>
        public const string XpAttr = "lotrnpcsXp";
        /// <summary>Watched: the owner's /group names (lowercase, comma separated), so clients know who sees the XP.</summary>
        public const string OwnerGroupsAttr = "lotrnpcsOwnerGroups";
        /// <summary>A hit counts when the target dies within this many milliseconds of it.</summary>
        public const int WindowMs = 10000;

        /// <summary>XP a rank needs before it can be promoted; 0 for ranks that earn none (archers, captains, rankless).</summary>
        public static int Need(string rank) => rank == "recruit" ? 15 : BaseRank(rank) == "warrior" ? 40 : 0;

        /// <summary>
        /// 0.0.113 (design-roster-promotion.md, owner: "branching promotion is good"): promotion-only sidegrades of the
        /// warrior. Guard: health x1.25, damage x0.8, always a shield. Shock: health x0.8, damage x1.25, a two-handed
        /// weapon. Both count as warriors everywhere else (prices they lack, body loot, order lists) and lead to captain.
        /// </summary>
        public static readonly string[] Branches = { "guard", "shock" };
        /// <summary>The rank a branch stands in for (warrior), else the rank itself.</summary>
        public static string BaseRank(string rank) => Array.IndexOf(Branches, rank) >= 0 ? "warrior" : rank;

        /// <summary>The ranks a rank can be promoted to, in menu order.</summary>
        public static string[] Targets(string rank) => rank == "recruit" ? new[] { "warrior", "guard", "shock", "archer" } : BaseRank(rank) == "warrior" ? new[] { "captain" } : Array.Empty<string>();

        public static int Xp(Entity e) => e?.WatchedAttributes?.GetInt(XpAttr) ?? 0;

        /// <summary>A hired NPC of a rank that earns XP.</summary>
        public static bool Earns(Entity e) => e != null && e.Alive && Recruit.OwnerUidOf(e) != null && Need(NpcTypeInfo.Of(e)?.Rank) > 0;

        /// <summary>One more XP for a recruit, capped at its rank's need. True when that filled it.</summary>
        public static bool AddXp(Entity e)
        {
            int need = Need(NpcTypeInfo.Of(e)?.Rank);
            int xp = Xp(e);
            if (need <= 0 || xp >= need) return false;
            e.WatchedAttributes.SetInt(XpAttr, xp + 1);
            e.WatchedAttributes.MarkPathDirty(XpAttr);
            return xp + 1 == need;
        }

        /// <summary>The entity code of the same race at another rank: test-goblin-recruit -> test-goblin-warrior.</summary>
        public static AssetLocation CodeAtRank(AssetLocation code, string rank)
        {
            if (code == null) return null;
            int dash = code.Path.LastIndexOf('-');
            return dash <= 0 ? null : new AssetLocation(code.Domain, code.Path.Substring(0, dash + 1) + rank);
        }

        /// <summary>May this player (uid, lowercase group names) see the XP of a recruit with this owner and these owner
        /// groups (comma separated)?</summary>
        public static bool MaySeeXp(string ownerUid, string ownerGroups, string viewerUid, IEnumerable<string> viewerGroups)
        {
            if (ownerUid == null || viewerUid == null) return false;
            if (ownerUid == viewerUid) return true;
            if (string.IsNullOrEmpty(ownerGroups) || viewerGroups == null) return false;
            HashSet<string> owners = new HashSet<string>(ownerGroups.Split(',', StringSplitOptions.RemoveEmptyEntries));
            return viewerGroups.Any(g => g != null && owners.Contains(g.ToLowerInvariant()));
        }

        /// <summary>Server: writes the owner's groups onto the recruit if they changed.</summary>
        public static void SyncOwnerGroups(Entity npc, IEnumerable<string> groups)
        {
            if (npc?.WatchedAttributes == null) return;
            string joined = string.Join(",", (groups ?? Enumerable.Empty<string>()).Where(g => !string.IsNullOrEmpty(g)).Select(g => g.ToLowerInvariant()).Distinct().OrderBy(g => g));
            if (Attrs.String(npc.WatchedAttributes, OwnerGroupsAttr) == joined) return;
            npc.WatchedAttributes.SetString(OwnerGroupsAttr, joined);
            npc.WatchedAttributes.MarkPathDirty(OwnerGroupsAttr);
        }

        /// <summary>
        /// Which watched attributes a promotion carries over to the new NPC: everything this mod keeps on it (owner,
        /// orders, stance, posts, sharing, borrower, name flag, weapon, shield) except the XP and the gear-only kept
        /// helmet, plus the name tag and Player Model Lib's face and race.
        /// </summary>
        public static bool CarriesOver(string key) =>
            key == "nametag" || key == "skinConfig" || key == "skinModel"
            || (key.StartsWith("lotrnpcs") && key != XpAttr && key != Looks.KeptHelmetAttr);
    }

    /// <summary>Server: counts the hits that earn XP, and promotes recruits.</summary>
    public class PromotionSystem : ModSystem
    {
        ICoreServerAPI sapi;
        HiringSystem hiring;
        // target entity id -> hired attacker entity id -> when it last hit (ms).
        readonly Dictionary<long, Dictionary<long, long>> hits = new Dictionary<long, Dictionary<long, long>>();
        /// <summary>Clock for tests, used when no server is attached.</summary>
        public long NowForTests;
        long Now => sapi?.World.ElapsedMilliseconds ?? NowForTests;
        /// <summary>Finds a loaded entity by id (the world's, or the tests').</summary>
        public System.Func<long, Entity> Find;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            hiring = api.ModLoader.GetModSystem<HiringSystem>();
            Find = id => api.World.GetEntityById(id);
            api.Event.RegisterGameTickListener(_ => Purge(), 10000);
        }

        /// <summary>An NPC of this mod was hurt: remember the hit if a recruit that earns XP made it.</summary>
        public void OnHit(Entity target, Entity attacker)
        {
            if (target == null || attacker == null || attacker == target || FactionSystem.FactionOf(target) == null || !Promotion.Earns(attacker)) return;
            if (!hits.TryGetValue(target.EntityId, out Dictionary<long, long> by)) hits[target.EntityId] = by = new Dictionary<long, long>();
            by[attacker.EntityId] = Now;
        }

        /// <summary>An NPC of this mod died: every recruit that hit it in the last 10 seconds gets 1 XP. Returns them.</summary>
        public List<Entity> OnDeath(Entity target)
        {
            List<Entity> credited = new List<Entity>();
            if (target == null || !hits.TryGetValue(target.EntityId, out Dictionary<long, long> by)) return credited;
            hits.Remove(target.EntityId);
            long now = Now;
            foreach (KeyValuePair<long, long> hit in by)
            {
                if (now - hit.Value > Promotion.WindowMs || !(Find?.Invoke(hit.Key) is Entity recruit) || !Promotion.Earns(recruit)) continue;
                bool full = Promotion.AddXp(recruit);
                credited.Add(recruit);
                if (full) TellOwner(recruit);
            }
            return credited;
        }

        void Purge()
        {
            long now = Now;
            foreach (long target in hits.Keys.ToList())
            {
                Dictionary<long, long> by = hits[target];
                foreach (long a in by.Where(kv => now - kv.Value > Promotion.WindowMs).Select(kv => kv.Key).ToList()) by.Remove(a);
                if (by.Count == 0) hits.Remove(target);
            }
        }

        void TellOwner(Entity recruit)
        {
            if (sapi?.World.PlayerByUid(Recruit.OwnerUidOf(recruit)) is IServerPlayer player && player.ConnectionState == EnumClientState.Playing)
                player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.GetL(player.LanguageCode, "lotrnpcs:promote-ready", Recruit.TitleInSentence(recruit, player.LanguageCode)), EnumChatType.Notification);
        }

        /// <summary>The ranks this NPC can be promoted to right now, with their prices, for its owner's talk menu.</summary>
        public Dictionary<string, int> Offers(EntityAgent npc, FactionDef def)
        {
            Dictionary<string, int> offers = new Dictionary<string, int>();
            string rank = NpcTypeInfo.Of(npc)?.Rank;
            int need = Promotion.Need(rank);
            if (need <= 0 || Promotion.Xp(npc) < need) return offers;
            foreach (string target in Promotion.Targets(rank))
            {
                if (npc.World.GetEntityType(Promotion.CodeAtRank(npc.Code, target)) == null) continue;
                Dictionary<string, int> prices = def?.PromotePrices ?? FactionTiers.Men.Promote;
                offers[target] = prices.TryGetValue(target, out int price) ? Math.Max(0, price) : 0;
            }
            return offers;
        }

        /// <summary>
        /// Replaces the recruit with an NPC of the new rank where it stands. The game rebuilds an NPC's health, attacks,
        /// AI and gear from its type every time it loads, so a rank changed in place would undo itself; a new NPC of the
        /// new rank keeps it. Everything the owner chose carries over (Promotion.CarriesOver); the armor is the new
        /// rank's, with a helmet on if the old one wore one and off if not. The owner's recruit list keeps its place.
        /// Returns the new NPC, or null.
        /// </summary>
        public EntityAgent Promote(EntityAgent npc, string rank)
        {
            if (sapi == null || npc == null || !npc.Alive) return null;
            EntityProperties type = sapi.World.GetEntityType(Promotion.CodeAtRank(npc.Code, rank));
            if (type == null) return null;
            if (!(sapi.World.ClassRegistry.CreateEntity(type) is EntityAgent next)) return null;

            next.Pos.SetFrom(npc.Pos);
            next.PositionBeforeFalling.Set(npc.Pos.X, npc.Pos.Y, npc.Pos.Z);
            foreach (string key in npc.WatchedAttributes.Keys.ToList())
            {
                if (Promotion.CarriesOver(key)) next.WatchedAttributes[key] = npc.WatchedAttributes[key].Clone();
            }
            foreach (string key in npc.Attributes.Keys.ToList())
            {
                if (key.StartsWith("lotrnpcs")) next.Attributes[key] = npc.Attributes[key].Clone();
            }
            string name = Looks.NameOf(npc);
            bool helmeted = Looks.Head(npc)?.Empty == false;

            sapi.World.SpawnEntity(next);
            if (name != null) Looks.SetName(next, name);
            ItemSlot head = Looks.Head(next);
            if (head != null && Looks.MoveHelmet(head, next.WatchedAttributes, helmeted, sapi.World)) Looks.StoreGear(next);

            hiring?.Replace(npc, next);
            sapi.World.DespawnEntity(npc, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            sapi.Logger.Notification("[lotrnpcs] " + npc.Code + " (" + npc.EntityId + ") was promoted to " + next.Code + " (" + next.EntityId + ").");
            return next;
        }
    }

    /// <summary>
    /// Client: adds "XP: (x/y)" at the end of a recruit's hover text, after the game's own lines (its name above and
    /// its health), for its owner and the players in the owner's /group (0.0.76). A Harmony postfix on EntityAgent's
    /// GetInfoText, since the game appends its lines after every behavior's.
    /// </summary>
    public static class XpInfoText
    {
        const string HarmonyId = "lotrnpcs.xpinfotext";
        static Harmony harmony;
        static ICoreClientAPI capi;

        public static void Apply(ICoreClientAPI api)
        {
            if (harmony != null) return;
            MethodInfo target = typeof(EntityAgent).GetMethod("GetInfoText", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (target == null) { api.Logger.Warning("[lotrnpcs] EntityAgent.GetInfoText not found; recruits' XP won't show."); return; }
            capi = api;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(target, null, new HarmonyMethod(typeof(XpInfoText).GetMethod(nameof(AppendXp), BindingFlags.Static | BindingFlags.NonPublic)), null, null);
        }

        public static void Remove()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            capi = null;
        }

        /// <summary>The XP and the need this viewer is shown, or null when they see no XP line.</summary>
        public static (int Xp, int Need)? Shown(Entity npc, string viewerUid, IEnumerable<string> viewerGroups)
        {
            string owner = Recruit.OwnerUidOf(npc);
            int need = Promotion.Need(NpcTypeInfo.Of(npc)?.Rank);
            if (owner == null || need <= 0 || !Promotion.MaySeeXp(owner, Attrs.String(npc.WatchedAttributes, Promotion.OwnerGroupsAttr), viewerUid, viewerGroups)) return null;
            return (Math.Min(Promotion.Xp(npc), need), need);
        }

        /// <summary>The line to add for this viewer, or null.</summary>
        public static string Line(Entity npc, string viewerUid, IEnumerable<string> viewerGroups) =>
            Shown(npc, viewerUid, viewerGroups) is (int xp, int need) ? Lang.Get("lotrnpcs:recruit-xp", xp, need) : null;

        static void AppendXp(EntityAgent __instance, ref string __result)
        {
            IClientPlayer me = capi?.World?.Player;
            if (me == null || __instance == null || __instance.Api?.Side != EnumAppSide.Client) return;
            string line = Line(__instance, me.PlayerUID, (me.Groups ?? Array.Empty<PlayerGroupMembership>()).Where(g => g?.GroupName != null).Select(g => g.GroupName.ToLowerInvariant()));
            if (line == null) return;
            __result = (__result ?? "") + (__result?.EndsWith("\n") == false && __result.Length > 0 ? "\n" : "") + line + "\n";
        }
    }
}
