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
    /// <summary>What the talk menu shows. The server works it out and sends it to the client as JSON.</summary>
    public class TalkState
    {
        // "member": a wild NPC of a faction the player belongs to, so they can hire it. "stranger": a wild NPC that
        // doesn't attack the player but won't serve them. "own": the player's recruit. "others": someone else's.
        // "shared": a group mate's shared guard the player may borrow. "borrowed": a shared guard the player leads now.
        public string Mode;
        public string OwnerName;
        public string Order;
        public int Price;
        public string Coin;
        public int Hired, Cap;
        // Most recruits that may follow the player at a time, and how many do now.
        public int FollowCap, Following;
        // "own" only: who has borrowed this shared guard, or null.
        public string BorrowerName;
        // Lang key (lotrnpcs domain, without it) of the NPC's answer to the player's last choice, or null.
        public string Reply;
        // "own" only: the ranks the recruit can be promoted to now, with their price in Coin. Empty until its XP is
        // full.
        public Dictionary<string, int> Promote = new Dictionary<string, int>();
        // Follow shares: how many more of each faction could still follow the player (this NPC's faction and those of
        // their followers), shown as "Room in your band: ..." ({6} in the talk lines).
        public List<RoomEntry> Room = new List<RoomEntry>();
        // How many more of each faction fit the roster ({7}, "Room on your roster: ...").
        public List<RoomEntry> RosterRoom = new List<RoomEntry>();
        // The player's captain places are full, so no captain is offered.
        public bool CaptainsFull;
    }

    /// <summary>One faction in the room line: how many more of its recruits fit the following.</summary>
    public class RoomEntry
    {
        public string Faction;
        public int Count;
    }

    /// <summary>A recruit as last seen loaded, saved with the world for the roster screen.</summary>
    public class RecruitSummary
    {
        public string Code, Name, Rank, Faction, Order, Stance, Borrower;
        public int Xp;
        public double X, Y, Z;
        public int Dimension;
        public DateTime Seen;

        public static RecruitSummary Of(Entity npc) => new RecruitSummary
        {
            Code = npc.Code?.ToShortString(), Name = Looks.NameOf(npc), Rank = NpcTypeInfo.Of(npc)?.Rank, Faction = FactionSystem.FactionOf(npc),
            Order = Recruit.OrderOf(npc), Stance = Recruit.StanceOf(npc), Xp = Promotion.Xp(npc),
            Borrower = Recruit.BorrowerUidOf(npc) != null ? npc.WatchedAttributes.GetString(Recruit.BorrowerNameAttr) : null,
            X = npc.Pos.X, Y = npc.Pos.Y, Z = npc.Pos.Z, Dimension = npc.Pos.Dimension, Seen = DateTime.UtcNow,
        };
    }

    /// <summary>A player who has hired NPCs, saved with the world.</summary>
    public class HiringOwner
    {
        public string Name;
        // The owner's race and groups when last seen, so wild NPCs treat their recruits the way they treat the
        // owner even while the owner is offline.
        public string Race;
        public List<string> Groups = new List<string>();
        public List<long> Recruits = new List<long>();
        // Recruits dismissed with /recruits dismissall while their chunk wasn't loaded: each is set free when it next
        // loads.
        public List<long> PendingDismiss = new List<long>();
        // Each recruit's faction by entity id, so its roster share is known while it isn't loaded. Recruits missing
        // here are added when they next load; until then they count at the baseline share.
        public Dictionary<long, string> RecruitFactions = new Dictionary<long, string>();
        // Which recruits are captains, for the captain limit while they aren't loaded.
        public HashSet<long> Captains = new HashSet<long>();
        // For the roster screen: what each recruit was like when last seen, for the ones that aren't loaded.
        public Dictionary<long, RecruitSummary> Summaries = new Dictionary<long, RecruitSummary>();
        // Players the owner's recruits never attack on sight (design doc: a whitelist with add, remove and list,
        // now /recruits whitelist), by UID, with the name they had when added.
        public Dictionary<string, string> Whitelist = new Dictionary<string, string>();
        // The formation the owner last chose in the orders menu (Formation.All).
        public string Formation = LotrNpcs.Formation.Loose;
        // The owner's entity while they are online, else 0.
        [JsonIgnore] public long EntityId;
        // Which way the owner is heading (unit vector), for followers in formation, and where they were when it was
        // last worked out. Also the facing of the last hold or move order.
        [JsonIgnore] public double HeadX, HeadZ = 1, LastX, LastZ;
        [JsonIgnore] public bool HasLast;
        [JsonIgnore] public double FaceX, FaceZ = 1;
    }

    /// <summary>
    /// One chat command for the whole mod, /recruits, so /help recruits lists everything: join, dismissall,
    /// whitelist, orders and roster for players, faction and zone for admins. Each mod system adds its own branch to
    /// the same root.
    /// </summary>
    public static class ModCommands
    {
        public const string Root = "recruits";
        public const string RootHelp = "LOTR NPCs, every command of the mod. For players: join, dismissall, whitelist, orders, roster. For admins: faction, zone. For testing (admins, with DebugCommands on in lotrnpcs-factions.json): battlelog, xp, bench, zone spawnnow. Type /help recruits <command> for one of them.";
        public const string AdminOnlyText = "Only server admins can use /recruits faction, zone, battlelog, xp and bench.";
        public const string DebugOffText = "This is a testing command, off on this server. An admin can turn testing commands on with \"DebugCommands\": true in ModConfig/lotrnpcs-factions.json, then /recruits faction reload.";

        /// <summary>The mod's root command, made by whichever system starts first.</summary>
        public static IChatCommand RootOf(ICoreServerAPI api) =>
            api.ChatCommands.GetOrCreate(Root).WithDescription(RootHelp).RequiresPrivilege(Privilege.chat);

        /// <summary>A handler that refuses callers without the controlserver privilege (on top of the branch's own
        /// RequiresPrivilege, in case the game only checks the root's).</summary>
        public static OnCommandDelegate AdminOnly(OnCommandDelegate handler) =>
            args => args.Caller.HasPrivilege(Privilege.controlserver) ? handler(args) : TextCommandResult.Error(AdminOnlyText);

        /// <summary>An admin command for testing: refused unless the faction config's DebugCommands is on, read at
        /// each use so /recruits faction reload switches it.</summary>
        public static OnCommandDelegate DebugOnly(ICoreServerAPI api, OnCommandDelegate handler) =>
            AdminOnly(args => api.ModLoader.GetModSystem<FactionSystem>()?.Config?.DebugCommands == true ? handler(args) : TextCommandResult.Error(DebugOffText));
    }

    /// <summary>Where a recruit keeps its owner and orders: WatchedAttributes, so clients see them too.</summary>
    public static class Recruit
    {
        /// <summary>Archers stand in the back rank of a formation.</summary>
        public static bool IsArcher(Entity npc) => NpcTypeInfo.Of(npc)?.Rank == "archer";

        /// <summary>
        /// The lang key of the word put before a hired NPC's name ("Warrior Gorbag"): title-[faction]-[rank] if the
        /// language has it (the Shire's hobbits are farmers), else title-[rank]. Null for a rankless NPC.
        /// </summary>
        public static string TitleKey(string faction, string rank, System.Func<string, bool> has)
        {
            if (string.IsNullOrEmpty(rank)) return null;
            rank = rank.ToLowerInvariant();
            if (faction != null && has("lotrnpcs:title-" + faction + "-" + rank)) return "lotrnpcs:title-" + faction + "-" + rank;
            return has("lotrnpcs:title-" + rank) ? "lotrnpcs:title-" + rank : null;
        }

        /// <summary>"Warrior Gorbag": rank, then name, for a named NPC; otherwise just its name. langCode null = this side's language.</summary>
        public static string Title(Entity npc, string langCode = null)
        {
            string name = npc.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
            if (string.IsNullOrEmpty(name)) name = npc.GetName();
            if (!npc.WatchedAttributes.GetBool(Looks.NamedAttr)) return name;
            string lc = langCode ?? Lang.CurrentLocale;
            NpcTypeInfo info = NpcTypeInfo.Of(npc);
            string key = TitleKey(info?.Faction, info?.Rank, k => Lang.GetL(lc, k) != k);
            return key == null ? name : Lang.GetL(lc, key) + " " + name;
        }

        /// <summary>The title in the middle of a sentence: "your warrior Gorbag".</summary>
        public static string TitleInSentence(Entity npc, string langCode = null)
        {
            string title = Title(npc, langCode), name = npc.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
            // Only the rank word is lowered; names keep their capitals.
            return name != null && title.Length > name.Length && title.EndsWith(" " + name)
                ? title.Substring(0, title.Length - name.Length).ToLowerInvariant() + name : title;
        }

        public const string OwnerUidAttr = "lotrnpcsOwnerUid", OwnerNameAttr = "lotrnpcsOwnerName", OrderAttr = "lotrnpcsOrder";
        const string HoldX = "lotrnpcsHoldX", HoldY = "lotrnpcsHoldY", HoldZ = "lotrnpcsHoldZ";
        // A guard's place in a formation around its post (formations from the orders menu apply to guards too).
        // Without one, a guard patrols its area.
        const string SpotX = "lotrnpcsSpotX", SpotY = "lotrnpcsSpotY", SpotZ = "lotrnpcsSpotZ";
        // Which way a recruit standing in a formation faces. Set only for formation spots; it also keeps them from
        // looking around (or spinning on the spot).
        const string FaceYawAttr = "lotrnpcsFaceYaw";
        // Follow me, Hold position (also where Move there sends them), and Guard here: patrol the area around a post
        // and attack intruders. The held spot and the guard post share the hold coordinates.
        public const string Follow = "follow", Hold = "hold", Guard = "guard";
        // Combat stances: passive never fights; defensive fights only what attacks the owner or their recruits;
        // aggressive (the default) attacks every enemy; raid attacks every player and creature that isn't allied.
        public const string StanceAttr = "lotrnpcsStance", Passive = "passive", Defensive = "defensive", Aggressive = "aggressive", Raid = "raid";
        public static readonly string[] Stances = { Passive, Defensive, Aggressive, Raid };
        // The formation of the last order, and a follower's place in it (right and forward of the owner, in blocks).
        public const string FormationAttr = "lotrnpcsFormation";
        const string SlotRight = "lotrnpcsSlotRight", SlotForward = "lotrnpcsSlotForward";

        /// <summary>The UID of the player who hired this NPC, or null for wild NPCs (and players).</summary>
        public static string OwnerUidOf(Entity e) => e == null || e is EntityPlayer ? null : Attrs.String(e.WatchedAttributes, OwnerUidAttr);

        public static string OrderOf(Entity e) => Attrs.String(e.WatchedAttributes, OrderAttr) ?? Follow;

        // Shared guards: the owner posts them with "Guard this area for our people"; any player in one of the owner's
        // groups may then borrow one for free. It follows the borrower (and counts toward their MaxFollowers) until
        // they post it again or leave the game; then it guards where it stands and can be borrowed again. The owner
        // keeps it: only they can dismiss it, and any order of theirs takes it back.
        public const string GuardShared = "guard-shared";
        public const string SharedAttr = "lotrnpcsShared", BorrowerUidAttr = "lotrnpcsBorrowerUid", BorrowerNameAttr = "lotrnpcsBorrowerName";
        public static bool IsShared(Entity e) => e?.WatchedAttributes != null && e.WatchedAttributes.GetBool(SharedAttr);

        // The game's RemoveAttribute on watched attributes resends all of them, even for a key that is not there, and
        // that makes every player's client rebuild the NPC's whole model (5-19 ms each), which formation refreshes
        // did for every follower on every order. So the key is taken out of the tree's entries directly and only that
        // key is sent: an update for a key that is gone carries no data, and the client deletes a key whose update
        // has none (the game's PartialUpdate).
        static readonly FieldInfo TreeEntries = typeof(TreeAttribute).GetField("attributes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        static void Drop(Entity npc, string key) => DropWatched(npc.WatchedAttributes, key);

        public static void DropWatched(SyncedTreeAttribute attrs, string key)
        {
            if (!attrs.HasAttribute(key)) return;
            if (TreeEntries?.GetValue(attrs) is IDictionary<string, IAttribute> entries && entries.Remove(key)) attrs.MarkPathDirty(key);
            else attrs.RemoveAttribute(key);
        }

        public static void SetShared(Entity npc, bool shared)
        {
            if (shared) npc.WatchedAttributes.SetBool(SharedAttr, true);
            else Drop(npc, SharedAttr);
        }
        /// <summary>The UID of the player who borrowed this shared guard, or null.</summary>
        public static string BorrowerUidOf(Entity e) => e == null || e is EntityPlayer ? null : Attrs.String(e.WatchedAttributes, BorrowerUidAttr);
        /// <summary>Whom this recruit follows and takes orders from: its borrower, else its owner.</summary>
        public static string LeaderUidOf(Entity e) => BorrowerUidOf(e) ?? OwnerUidOf(e);
        public static void Lend(Entity npc, string uid, string name)
        {
            npc.WatchedAttributes.SetString(BorrowerUidAttr, uid);
            npc.WatchedAttributes.SetString(BorrowerNameAttr, name);
        }
        public static void ClearBorrower(Entity npc)
        {
            Drop(npc, BorrowerUidAttr);
            Drop(npc, BorrowerNameAttr);
        }

        public static string StanceOf(Entity e)
        {
            string stance = Attrs.String(e?.WatchedAttributes, StanceAttr);
            switch (stance)
            {
                case Passive:
                case Defensive:
                case Raid:
                    return stance;
                default:
                    return Aggressive;
            }
        }

        public static void SetStance(Entity npc, string stance) => npc.WatchedAttributes.SetString(StanceAttr, stance);

        public static string FormationOf(Entity e) => e.WatchedAttributes.GetString(FormationAttr, Formation.Loose);

        /// <summary>A follower's place in its owner's formation, or null to follow loosely.</summary>
        public static Vec2d FollowSlot(Entity e) => e.WatchedAttributes.HasAttribute(SlotRight)
            ? new Vec2d(e.WatchedAttributes.GetDouble(SlotRight), e.WatchedAttributes.GetDouble(SlotForward)) : null;

        public static void SetFollowSlot(Entity npc, Vec2d slot)
        {
            if (slot == null)
            {
                Drop(npc, SlotRight);
                Drop(npc, SlotForward);
                return;
            }
            npc.WatchedAttributes.SetDouble(SlotRight, slot.X);
            npc.WatchedAttributes.SetDouble(SlotForward, slot.Y);
        }

        /// <summary>A guard's place in its formation, or null when it patrols.</summary>
        public static Vec3d GuardSpot(Entity e) => e.WatchedAttributes.HasAttribute(SpotX)
            ? new Vec3d(e.WatchedAttributes.GetDouble(SpotX), e.WatchedAttributes.GetDouble(SpotY), e.WatchedAttributes.GetDouble(SpotZ)) : null;

        public static bool HasGuardSpot(Entity e) => e?.WatchedAttributes != null && e.WatchedAttributes.HasAttribute(SpotX);

        public static void SetGuardSpot(Entity npc, Vec3d spot)
        {
            if (spot == null)
            {
                Drop(npc, FaceYawAttr);
                Drop(npc, SpotX);
                Drop(npc, SpotY);
                Drop(npc, SpotZ);
                return;
            }
            npc.WatchedAttributes.SetDouble(SpotX, spot.X);
            npc.WatchedAttributes.SetDouble(SpotY, spot.Y);
            npc.WatchedAttributes.SetDouble(SpotZ, spot.Z);
        }

        /// <summary>Face the way (fx, fz) once in place, as the game measures yaw (atan2 of x over z).</summary>
        public static void SetFacing(Entity npc, double fx, double fz) => npc.WatchedAttributes.SetFloat(FaceYawAttr, (float)Math.Atan2(fx, fz));
        public static bool HasFacing(Entity e) => e?.WatchedAttributes != null && e.WatchedAttributes.HasAttribute(FaceYawAttr);
        public static float FacingYaw(Entity e) => e.WatchedAttributes.GetFloat(FaceYawAttr);

        /// <summary>Hold: go to this spot and stay there.</summary>
        public static void SetHoldAt(Entity npc, Vec3d pos)
        {
            SetGuardSpot(npc, null);
            npc.WatchedAttributes.SetString(OrderAttr, Hold);
            npc.WatchedAttributes.SetDouble(HoldX, pos.X);
            npc.WatchedAttributes.SetDouble(HoldY, pos.Y);
            npc.WatchedAttributes.SetDouble(HoldZ, pos.Z);
        }

        public static Vec3d HoldPos(Entity e) => e.WatchedAttributes.HasAttribute(HoldX)
            ? new Vec3d(e.WatchedAttributes.GetDouble(HoldX), e.WatchedAttributes.GetDouble(HoldY), e.WatchedAttributes.GetDouble(HoldZ)) : null;

        /// <summary>Guard here: patrol the area around this post and attack intruders there.</summary>
        public static void SetGuardAt(Entity npc, Vec3d post)
        {
            SetHoldAt(npc, post);
            npc.WatchedAttributes.SetString(OrderAttr, Guard);
        }

        /// <summary>The guard post (or held spot), read without allocating: the AI asks for it many times a second.</summary>
        public static bool TryGetPost(Entity e, out double x, out double y, out double z)
        {
            ITreeAttribute attrs = e?.WatchedAttributes;
            if (attrs == null || !attrs.HasAttribute(HoldX)) { x = y = z = 0; return false; }
            x = attrs.GetDouble(HoldX);
            y = attrs.GetDouble(HoldY);
            z = attrs.GetDouble(HoldZ);
            return true;
        }

        /// <summary>Is e the owner, or another of the owner's recruits?</summary>
        public static bool IsTeam(Entity e, string ownerUid) => e is EntityPlayer p ? Attrs.Uid(p) == ownerUid : OwnerUidOf(e) == ownerUid;

        /// <summary>The owner's entity while they are in the game, else null.</summary>
        public static EntityPlayer OnlineOwner(Entity e) => OnlinePlayer(e, OwnerUidOf(e));

        /// <summary>The leader's (borrower's, else owner's) entity while they are in the game, else null.</summary>
        public static EntityPlayer OnlineLeader(Entity e) => OnlinePlayer(e, LeaderUidOf(e));

        static EntityPlayer OnlinePlayer(Entity e, string uid)
        {
            if (uid == null) return null;
            IPlayer player = e.World.PlayerByUid(uid);
            if (player is IServerPlayer sp && sp.ConnectionState != EnumClientState.Playing) return null;
            return player?.Entity;
        }

        public static void Bind(Entity npc, string ownerUid, string ownerName)
        {
            npc.WatchedAttributes.SetString(OwnerUidAttr, ownerUid);
            npc.WatchedAttributes.SetString(OwnerNameAttr, ownerName);
            SetOrder(npc, Follow);
        }

        public static void Release(Entity npc)
        {
            foreach (string key in new[] { OwnerUidAttr, OwnerNameAttr, OrderAttr, HoldX, HoldY, HoldZ, StanceAttr, FormationAttr, SlotRight, SlotForward, SharedAttr, BorrowerUidAttr, BorrowerNameAttr, SpotX, SpotY, SpotZ, FaceYawAttr })
                Drop(npc, key);
        }

        /// <summary>Follow: stay near the owner, with no spot of its own. Hold: go back to where the NPC stands now and
        /// stay there. (Guard needs a post: SetGuardAt.)</summary>
        public static void SetOrder(Entity npc, string order)
        {
            npc.WatchedAttributes.SetString(OrderAttr, order);
            SetGuardSpot(npc, null);
            if (order == Follow)
            {
                Drop(npc, HoldX);
                Drop(npc, HoldY);
                Drop(npc, HoldZ);
                return;
            }
            if (order != Hold) return;
            npc.WatchedAttributes.SetDouble(HoldX, npc.Pos.X);
            npc.WatchedAttributes.SetDouble(HoldY, npc.Pos.Y);
            npc.WatchedAttributes.SetDouble(HoldZ, npc.Pos.Z);
        }
    }
}
