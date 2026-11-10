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
        // "own" only (0.0.76): the ranks the recruit can be promoted to now, with their price in Coin. Empty until its XP
        // is full.
        public Dictionary<string, int> Promote = new Dictionary<string, int>();
        // 0.0.90 follow shares: how many more of each faction could still follow the player (this NPC's faction and
        // those of their followers), shown as "Room in your band: ..." ({6} in the talk lines).
        public List<RoomEntry> Room = new List<RoomEntry>();
        // 0.0.91 (owner, 2026-11-05: "since the number has changed, players should be notified how many recruits they
        // have left in their capacity"): how many more of each faction fit the roster ({7}, "Room on your roster: ...").
        public List<RoomEntry> RosterRoom = new List<RoomEntry>();
        // 0.0.104: the player's captain places are full, so no captain is offered.
        public bool CaptainsFull;
    }

    /// <summary>One faction in the room line (0.0.90): how many more of its recruits fit the following.</summary>
    public class RoomEntry
    {
        public string Faction;
        public int Count;
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
        // Recruits dismissed with /recruits dismissall while their chunk wasn't loaded (0.0.73): each is set free when
        // it next loads.
        public List<long> PendingDismiss = new List<long>();
        // 0.0.90: each recruit's faction by entity id, so its roster share is known while it isn't loaded. Recruits
        // hired before 0.0.90 are added when they next load; until then they count at the baseline share.
        public Dictionary<long, string> RecruitFactions = new Dictionary<long, string>();
        // 0.0.104: which recruits are captains, for the captain limit while they aren't loaded.
        public HashSet<long> Captains = new HashSet<long>();
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

    /// <summary>Where a recruit keeps its owner and orders: WatchedAttributes, so clients see them too.</summary>
    /// <summary>
    /// One chat command for the whole mod (0.0.89, owner, 2026-11-04: "for all the commands, streamline the name ... so
    /// that when I do /help "x" it gives all the available commands for this mod"): /recruits, with dismissall,
    /// whitelist and orders for players and faction and zone for admins (they were /recruits faction, /recruits zone and the client
    /// command /recruits orders). Each mod system adds its own branch to the same root.
    /// </summary>
    public static class ModCommands
    {
        public const string Root = "recruits";
        public const string RootHelp = "LOTR NPCs, every command of the mod. For players: join, dismissall, whitelist, orders. For admins: faction, zone. Type /help recruits <command> for one of them.";
        public const string AdminOnlyText = "Only server admins can use /recruits faction and /recruits zone.";

        /// <summary>The mod's root command, made by whichever system starts first.</summary>
        public static IChatCommand RootOf(ICoreServerAPI api) =>
            api.ChatCommands.GetOrCreate(Root).WithDescription(RootHelp).RequiresPrivilege(Privilege.chat);

        /// <summary>A handler that refuses callers without the controlserver privilege (on top of the branch's own
        /// RequiresPrivilege, in case the game only checks the root's).</summary>
        public static OnCommandDelegate AdminOnly(OnCommandDelegate handler) =>
            args => args.Caller.HasPrivilege(Privilege.controlserver) ? handler(args) : TextCommandResult.Error(AdminOnlyText);
    }

    public static class Recruit
    {
        /// <summary>Archers (0.0.55) stand in the back rank of a formation.</summary>
        public static bool IsArcher(Entity npc) => NpcTypeInfo.Of(npc)?.Rank == "archer";

        /// <summary>
        /// The lang key of the word put before a hired NPC's name (user, 2026-11-03: call hired NPCs by
        /// their rank, then their name): title-[faction]-[rank] if the language has it (the
        /// Shire's hobbits are farmers), else title-[rank]. Null for a rankless NPC.
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
        // A guard's place in a formation around its post (0.0.50, user: formations from the R menu should apply to
        // guards too). Without one, a guard patrols its area.
        const string SpotX = "lotrnpcsSpotX", SpotY = "lotrnpcsSpotY", SpotZ = "lotrnpcsSpotZ";
        // Which way a recruit standing in a formation faces (0.0.51, user's screenshots: formations came out ragged and
        // guards in a column spun on the spot). Set only for formation spots; it also keeps them from looking around.
        const string FaceYawAttr = "lotrnpcsFaceYaw";
        // Follow me, Hold position (also where Move there sends them), and Guard here (0.0.44): patrol the area around
        // a post and attack intruders. The held spot and the guard post share the hold coordinates.
        public const string Follow = "follow", Hold = "hold", Guard = "guard";
        // Combat stances (user, 2026-11-03): passive never fights; defensive fights only what attacks the owner or
        // their recruits; aggressive (the default, 0.0.37's behavior) attacks every enemy; raid attacks every player
        // and creature that isn't allied.
        public const string StanceAttr = "lotrnpcsStance", Passive = "passive", Defensive = "defensive", Aggressive = "aggressive", Raid = "raid";
        public static readonly string[] Stances = { Passive, Defensive, Aggressive, Raid };
        // The formation of the last order, and a follower's place in it (right and forward of the owner, in blocks).
        public const string FormationAttr = "lotrnpcsFormation";
        const string SlotRight = "lotrnpcsSlotRight", SlotForward = "lotrnpcsSlotForward";

        /// <summary>The UID of the player who hired this NPC, or null for wild NPCs (and players).</summary>
        public static string OwnerUidOf(Entity e) => e == null || e is EntityPlayer ? null : Attrs.String(e.WatchedAttributes, OwnerUidAttr);

        public static string OrderOf(Entity e) => Attrs.String(e.WatchedAttributes, OrderAttr) ?? Follow;

        // Shared guards (user, 2026-11-03: guards "picked up by other members of the faction to follow them, without
        // having to pay", as "a new option", by /group): the owner posts them with "Guard this area for our people";
        // any player in one of the owner's groups may then borrow one for free. It follows the borrower (and counts
        // toward their MaxFollowers) until they post it again or leave the game; then it guards where it stands and
        // can be borrowed again. The owner keeps it: only they can dismiss it, and any order of theirs takes it back.
        public const string GuardShared = "guard-shared";
        public const string SharedAttr = "lotrnpcsShared", BorrowerUidAttr = "lotrnpcsBorrowerUid", BorrowerNameAttr = "lotrnpcsBorrowerName";
        public static bool IsShared(Entity e) => e?.WatchedAttributes != null && e.WatchedAttributes.GetBool(SharedAttr);
        public static void SetShared(Entity npc, bool shared)
        {
            if (shared) npc.WatchedAttributes.SetBool(SharedAttr, true);
            else npc.WatchedAttributes.RemoveAttribute(SharedAttr);
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
            npc.WatchedAttributes.RemoveAttribute(BorrowerUidAttr);
            npc.WatchedAttributes.RemoveAttribute(BorrowerNameAttr);
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
                npc.WatchedAttributes.RemoveAttribute(SlotRight);
                npc.WatchedAttributes.RemoveAttribute(SlotForward);
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
                npc.WatchedAttributes.RemoveAttribute(FaceYawAttr);
                npc.WatchedAttributes.RemoveAttribute(SpotX);
                npc.WatchedAttributes.RemoveAttribute(SpotY);
                npc.WatchedAttributes.RemoveAttribute(SpotZ);
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
                npc.WatchedAttributes.RemoveAttribute(key);
        }

        /// <summary>Follow: stay near the owner, with no spot of its own. Hold: go back to where the NPC stands now and
        /// stay there. (Guard needs a post: SetGuardAt.)</summary>
        public static void SetOrder(Entity npc, string order)
        {
            npc.WatchedAttributes.SetString(OrderAttr, order);
            SetGuardSpot(npc, null);
            if (order == Follow)
            {
                npc.WatchedAttributes.RemoveAttribute(HoldX);
                npc.WatchedAttributes.RemoveAttribute(HoldY);
                npc.WatchedAttributes.RemoveAttribute(HoldZ);
                return;
            }
            if (order != Hold) return;
            npc.WatchedAttributes.SetDouble(HoldX, npc.Pos.X);
            npc.WatchedAttributes.SetDouble(HoldY, npc.Pos.Y);
            npc.WatchedAttributes.SetDouble(HoldZ, npc.Pos.Z);
        }
    }

    /// <summary>
    /// Hiring (user, 2026-11-02): right-clicking a faction NPC opens a talk menu like the traders'. Members of its
    /// faction can hire it for the faction's HirePrices in HireCoin, up to MaxRecruits each. Recruits follow, hold
    /// or guard (orders and stances: Orders.cs), fight whatever would attack their owner and whoever hurts the owner or
    /// them, and wild NPCs treat them the way they treat the owner. Owners keep a whitelist of players their recruits
    /// never attack on sight (/recruits whitelist). Server only; the menu itself is GuiDialogTalk on the client.
    /// </summary>
    public class HiringSystem : ModSystem
    {
        public const string SaveKey = "lotrnpcs:hirelings";
        const string OriginAttr = SpawnZoneSystem.OriginAttr, OriginBeforeAttr = "lotrnpcsOriginBeforeHire";
        // Farther than this from a following recruit, the owner gets it teleported to them.
        public const double TeleportDistance = 48;
        // How far from a hurt owner or recruit the owner's other recruits come to help.
        public const double RallyRange = 24;
        // How far the owner's orders reach (the orders menu, hold R).
        public const double CommandRange = 96;
        // How long a movement order keeps recruits from starting fights, so they break off and go.
        public const int DisengageMs = 5000;
        // 0.0.77 (owner, 2026-11-04: "12 blocks, but when that amount is exceeded, they leave combat to return to you";
        // "Have it apply to all formations except loose, where on loose they dont try to return to me, and fight until
        // the enemy is dead"). A follower in a formation other than loose fights only enemies within FollowFightRange
        // of its leader. Farther than that from the leader, it breaks off and starts no fight (not even striking back)
        // until it is back within RegroupWithin. Loose followers fight on wherever the fight goes and come back when it
        // is over; the pull to the leader at TeleportDistance waits until then too.
        public const double FollowFightRange = 12, RegroupWithin = 4;

        ICoreServerAPI sapi;
        public FactionSystem Factions { get; set; }
        public PromotionSystem Promotions { get; set; }
        Dictionary<string, HiringOwner> owners = new Dictionary<string, HiringOwner>();
        // owner UID -> attacker entity id -> until when (ms): the owner's recruits fight whoever hurt the owner or one
        // of them, for the faction config's GrudgeSeconds.
        readonly Dictionary<string, Dictionary<long, long>> teamGrudges = new Dictionary<string, Dictionary<long, long>>();
        // recruit entity id -> until when (ms) it starts no fights after a movement order. Kept in memory only: the
        // clock restarts with the server.
        readonly Dictionary<long, long> disengageUntil = new Dictionary<long, long>();
        // 0.0.77: followers in a formation (not loose) -> their leader's entity, and those breaking off to regroup.
        readonly Dictionary<long, Entity> formationLeader = new Dictionary<long, Entity>();
        readonly HashSet<long> regrouping = new HashSet<long>();
        /// <summary>Clock for tests, used when no server is attached.</summary>
        public long NowForTests;
        long Now => sapi?.World.ElapsedMilliseconds ?? NowForTests;

        /// <summary>The baseline roster and follow limit (the Men's; the file's top-level values). Each faction has its
        /// own since 0.0.90 (FactionConfig.RosterOf, FollowersOf).</summary>
        public int MaxRecruits => Math.Max(1, Factions?.Config.MaxRecruits ?? FactionConfig.DefaultMaxRecruits);
        public int MaxFollowers => Math.Max(1, Factions?.Config.MaxFollowers ?? FactionConfig.DefaultMaxFollowers);

        // ---- 0.0.90 follow and roster shares (owner: "Follow shares": each follower uses 1/(its faction's follow
        // limit) of the following, and the same kind of share applies to the total roster; never more than 1, owner
        // chose "a") ----

        /// <summary>Rounding room for sums of shares, so 7 sevenths still fit.</summary>
        public const double ShareSlack = 1e-6;
        public double FollowShare(string faction) => 1.0 / Math.Max(1, Factions?.Config.FollowersOf(faction) ?? FactionConfig.DefaultMaxFollowers);
        public double RosterShare(string faction) => 1.0 / Math.Max(1, Factions?.Config.RosterOf(faction) ?? FactionConfig.DefaultMaxRecruits);
        public static bool Fits(double used, double share) => used + share <= 1 + ShareSlack;
        public static bool Over(double used) => used > 1 + ShareSlack;

        /// <summary>How much of the player's roster their recruits use. A recruit whose faction isn't known yet (hired
        /// before 0.0.90 and not loaded since) counts at the baseline share.</summary>
        public double RosterUsed(string uid)
        {
            if (!(Owner(uid) is HiringOwner owner)) return 0;
            double used = 0;
            foreach (long id in owner.Recruits)
                used += owner.RecruitFactions.TryGetValue(id, out string faction) ? RosterShare(faction) : 1.0 / MaxRecruits;
            return used;
        }

        /// <summary>How much of the player's following the loaded recruits told to follow them use (shared guards they
        /// borrowed included), leaving out one.</summary>
        public double FollowUsed(string uid, Entity except = null) =>
            Followers(uid).Where(e => e != except).Sum(e => FollowShare(FactionSystem.FactionOf(e)));

        /// <summary>How many more of each faction fit the player's following: the given faction first, then those of
        /// their followers, at most four (0.0.90 room line).</summary>
        public List<RoomEntry> Room(string uid, string faction)
        {
            List<EntityAgent> following = Followers(uid);
            double used = following.Sum(e => FollowShare(FactionSystem.FactionOf(e)));
            List<string> shown = new List<string>();
            if (faction != null) shown.Add(faction);
            foreach (EntityAgent e in following)
            {
                string f = FactionSystem.FactionOf(e);
                if (f != null && !shown.Contains(f)) shown.Add(f);
            }
            return shown.Take(4).Select(f => new RoomEntry { Faction = f, Count = Math.Max(0, (int)Math.Floor((1 - used + ShareSlack) / FollowShare(f))) }).ToList();
        }

        /// <summary>How many more of each faction fit the player's roster (0.0.91): the given faction first, then those
        /// of their recruits whose faction is known, at most four.</summary>
        public List<RoomEntry> RosterRoom(string uid, string faction)
        {
            double used = RosterUsed(uid);
            List<string> shown = new List<string>();
            if (faction != null) shown.Add(faction);
            if (Owner(uid) is HiringOwner owner)
                foreach (long id in owner.Recruits)
                    if (owner.RecruitFactions.TryGetValue(id, out string f) && !shown.Contains(f)) shown.Add(f);
            return shown.Take(4).Select(f => new RoomEntry { Faction = f, Count = Math.Max(0, (int)Math.Floor((1 - used + ShareSlack) / RosterShare(f))) }).ToList();
        }

        /// <summary>The roster line ("Room on your roster: Mordor orcs 12, Men of Gondor 8."), or "".</summary>
        public static string RosterText(List<RoomEntry> room, System.Func<string, object[], string> lang)
        {
            if (room == null || room.Count == 0) return "";
            return lang("roster-line", new object[] { string.Join(", ", room.Select(r => Folk(r.Faction, lang) + " " + r.Count)) });
        }

        /// <summary>The room line, or "" when there is nothing to show. lang(key, args) translates a lotrnpcs lang key
        /// (without the domain) in the player's language.</summary>
        public static string RoomText(List<RoomEntry> room, System.Func<string, object[], string> lang)
        {
            if (room == null || room.Count == 0) return "";
            return lang("room-line", new object[] { string.Join(", ", room.Select(r => Folk(r.Faction, lang) + " " + r.Count)) });
        }

        /// <summary>A faction's people for the room line ("Mordor orcs"), or its id when the lang file has none.</summary>
        public static string Folk(string faction, System.Func<string, object[], string> lang)
        {
            string text = lang("folk-" + faction, Array.Empty<object>());
            return string.IsNullOrEmpty(text) || text.EndsWith("folk-" + faction, StringComparison.Ordinal) ? faction : text;
        }

        static string Percent(double share) => Math.Round(share * 100) + "%";

        // Remembers a recruit's faction for its roster share.
        static void Learn(HiringOwner owner, Entity npc)
        {
            string faction = FactionSystem.FactionOf(npc);
            if (owner != null && faction != null) owner.RecruitFactions[npc.EntityId] = faction;
            if (owner == null) return;
            owner.Captains ??= new HashSet<long>();
            if (RankOf(npc) == "captain") owner.Captains.Add(npc.EntityId);
            else owner.Captains.Remove(npc.EntityId);
        }

        /// <summary>A captain's share of the player's captain places (0.0.104): 1 / CaptainsOf(its faction).</summary>
        public double CaptainShare(string faction)
        {
            int n = Factions?.Config.CaptainsOf(faction) ?? FactionConfig.CaptainsFor(FactionConfig.DefaultMaxRecruits, FactionConfig.DefaultRosterPerCaptain);
            return n == int.MaxValue ? 0 : 1.0 / n;
        }

        /// <summary>How much of the player's captain places their captains use (0.0.104).</summary>
        public double CaptainsUsed(string uid)
        {
            if (!(Owner(uid) is HiringOwner owner) || owner.Captains == null) return 0;
            double used = 0;
            foreach (long id in owner.Captains)
                if (owner.Recruits.Contains(id)) used += CaptainShare(owner.RecruitFactions.TryGetValue(id, out string f) ? f : null);
            return used;
        }

        /// <summary>Room for one more captain of this faction (0.0.104)?</summary>
        public bool CaptainFits(string uid, string faction) => CaptainShare(faction) == 0 || Fits(CaptainsUsed(uid), CaptainShare(faction));

        /// <summary>The player's loaded, living recruits that are told to follow them, wherever they are.</summary>
        List<EntityAgent> Followers(string uid) => Led(uid).Where(e => Recruit.OrderOf(e) == Recruit.Follow).ToList();

        /// <summary>The loaded, living recruits this player leads, wherever they are: their own that nobody has
        /// borrowed, and shared guards they borrowed.</summary>
        public List<EntityAgent> Led(string uid)
        {
            List<EntityAgent> led = new List<EntityAgent>();
            if (sapi == null || uid == null) return led;
            if (Owner(uid) is HiringOwner owner)
            {
                foreach (long id in owner.Recruits)
                {
                    if (sapi.World.GetEntityById(id) is EntityAgent e && e.Alive && Recruit.OwnerUidOf(e) == uid && Recruit.BorrowerUidOf(e) == null) led.Add(e);
                }
            }
            foreach (Entity e in sapi.World.LoadedEntities.Values)
            {
                if (e is EntityAgent a && a.Alive && Recruit.BorrowerUidOf(a) == uid && Recruit.OwnerUidOf(a) != null && Recruit.OwnerUidOf(a) != uid) led.Add(a);
            }
            return led;
        }

        /// <summary>Is the player in one of the owner's /groups (as the owner's groups were when last seen)?</summary>
        public bool SharesGroup(string ownerUid, EntityPlayer player)
        {
            if (player == null || !(Owner(ownerUid) is HiringOwner owner) || owner.Groups == null || owner.Groups.Count == 0) return false;
            return FactionSystem.GroupsOf(player).Any(owner.Groups.Contains);
        }

        /// <summary>The player's record, made if they have none yet (a borrower needs one for formations).</summary>
        HiringOwner EnsureOwner(IServerPlayer player)
        {
            HiringOwner owner = Owner(player.PlayerUID) ?? (owners[player.PlayerUID] = new HiringOwner());
            Refresh(owner, player);
            return owner;
        }

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            Factions = api.ModLoader.GetModSystem<FactionSystem>();
            Promotions = api.ModLoader.GetModSystem<PromotionSystem>();
            api.Event.SaveGameLoaded += Load;
            api.Event.GameWorldSave += Save;
            api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
            api.Event.PlayerRespawn += OnPlayerNowPlaying;
            api.Event.PlayerDisconnect += player => { if (owners.TryGetValue(player.PlayerUID, out HiringOwner owner)) owner.EntityId = 0; };
            api.Event.RegisterGameTickListener(OnTick, 1000);

            CommandArgumentParsers parsers = api.ChatCommands.Parsers;
            ModCommands.RootOf(api)
                // 0.0.73 (user, 2026-11-04: "make an option in the chat to fire all your recruits").
                .BeginSubCommand("dismissall")
                    .WithAlias("fireall")
                    .RequiresPlayer()
                    .WithDescription("Dismiss every recruit you have, as Dismiss in their talk menu does (hire fees aren't refunded). Add confirm to do it.")
                    .WithArgs(parsers.OptionalWord("confirm"))
                    .HandleWith(DismissAllCommand)
                .EndSubCommand()
                .BeginSubCommand("whitelist")
                    .RequiresPlayer()
                    .WithDescription("Players your recruits never attack on sight, even on Guard here or the raid stance. They still fight back if hurt. With nothing after it, opens the whitelist menu.")
                    // 0.0.76 (user: the whitelist menu "would be good for this round"): /recruits whitelist alone opens it.
                    .HandleWith(OpenWhitelistMenu)
                    .BeginSubCommand("add")
                        .WithDescription("Put a player on your whitelist")
                        .WithArgs(parsers.Word("player"))
                        .HandleWith(args => Whitelist(args, true))
                    .EndSubCommand()
                    .BeginSubCommand("remove")
                        .WithDescription("Take a player off your whitelist")
                        .WithArgs(parsers.Word("player"))
                        .HandleWith(args => Whitelist(args, false))
                    .EndSubCommand()
                    .BeginSubCommand("list")
                        .WithDescription("List the players on your whitelist")
                        .HandleWith(ListWhitelist)
                    .EndSubCommand()
                .EndSubCommand();
        }

        public HiringOwner Owner(string uid) => uid != null && owners.TryGetValue(uid, out HiringOwner owner) ? owner : null;

        /// <summary>Puts an owner record in place. Public for tests.</summary>
        public void SetOwner(string uid, HiringOwner owner)
        {
            owners[uid] = owner;
            Factions?.ForgetSide(uid);
        }

        /// <summary>Is other on this owner's whitelist?</summary>
        public bool Whitelisted(string ownerUid, string otherUid) =>
            otherUid != null && Owner(ownerUid)?.Whitelist is Dictionary<string, string> list && list.ContainsKey(otherUid);

        /// <summary>How far around their post guards patrol (GuardRadius in the faction config).</summary>
        public double GuardRadius => Factions?.GuardRadius ?? FactionConfig.DefaultGuardRadius;

        public int Count(string uid) => Owner(uid)?.Recruits.Count ?? 0;

        /// <summary>What hiring an NPC of this rank costs. Rankless NPCs cost a recruit's price.</summary>
        public static int PriceFor(FactionDef def, string rank)
        {
            Dictionary<string, int> prices = def?.HirePrices ?? FactionConfig.DefaultHirePrices();
            if (rank != null && prices.TryGetValue(rank.ToLowerInvariant(), out int price)) return Math.Max(0, price);
            return prices.TryGetValue("recruit", out int recruit) ? Math.Max(0, recruit) : 0;
        }

        public static string RankOf(Entity npc) => NpcTypeInfo.Of(npc)?.Rank;

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
                // 0.0.104: no captain offer while the player's captain places are full.
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
            // 0.0.90: the room line for the menus that can hire, follow or borrow.
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
                case "borrow":
                    {
                        if (state.Mode != "shared") return state;
                        // 0.0.90: a borrowed guard takes its faction's share of the borrower's following.
                        if (!Fits(FollowUsed(player.PlayerUID), FollowShare(FactionSystem.FactionOf(npc)))) return StateFor(npc, player, "talk-followfull");
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
                        if (action == Recruit.Follow && !Followers(player.PlayerUID).Contains(npc)
                            && !Fits(FollowUsed(player.PlayerUID), FollowShare(FactionSystem.FactionOf(npc))))
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
                        string rank = promote.Substring("promote:".Length);
                        if (state.Mode != "own") return state;
                        if (rank == "captain" && state.CaptainsFull) return StateFor(npc, player, "talk-captains-full");
                        if (state.Promote == null || !state.Promote.TryGetValue(rank, out int price)) return state;
                        if (Pay(player, state.Coin, price) is string poor)
                        {
                            TalkState refused = StateFor(npc, player, poor == "talk-poor" ? "talk-promote-poor" : "talk-promote-nocoin");
                            refused.Price = price;
                            return refused;
                        }
                        string before = Recruit.TitleInSentence(npc, player.LanguageCode);
                        EntityAgent next = Promotions?.Promote(npc, rank);
                        if (next == null) return state;
                        player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.GetL(player.LanguageCode, "lotrnpcs:promote-done", before, Recruit.TitleInSentence(next, player.LanguageCode)), EnumChatType.Notification);
                        sapi?.Logger.Notification("[lotrnpcs] " + player.PlayerName + " promoted their recruit to " + rank + " for " + price + " " + state.Coin + ".");
                        // The menu belonged to the old NPC: it closes.
                        return null;
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

        TalkState Hire(EntityAgent npc, IServerPlayer player, TalkState state)
        {
            // 0.0.90: room on the roster for one more of this faction (shares, not a count).
            string faction = FactionSystem.FactionOf(npc);
            if (!Fits(RosterUsed(player.PlayerUID), RosterShare(faction))) { state.Reply = "talk-full"; return state; }
            if (RankOf(npc) == "captain" && !CaptainFits(player.PlayerUID, faction)) { state.Reply = "talk-captains-full"; return state; }
            if (Pay(player, state.Coin, state.Price) is string poor) { state.Reply = poor; return state; }

            // Past the follow share, a new recruit guards the spot where it was hired instead of following.
            bool guards = !Fits(FollowUsed(player.PlayerUID, npc), FollowShare(faction));
            Recruit.Bind(npc, player.PlayerUID, player.PlayerName);
            // A name befitting its race (0.0.54), unless it already has one.
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
            // 0.0.91 (owner: players "should be notified how many recruits they have left in their capacity").
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
            Learn(owner, next);
            Promotion.SyncOwnerGroups(next, owner.Groups);
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
        /// Dismisses every recruit of this owner (0.0.73): loaded ones at once, the rest when they next load
        /// (PendingDismiss). find looks a loaded entity up by id. Returns how many went now and how many later.
        /// Public for tests.
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

        // ---- 0.0.90: over the limits (owner chose "B" then "BB": dismiss, for the roster and for the following) ----

        /// <summary>
        /// Brings a player within their roster and follow shares after the limits were lowered (on joining, and after
        /// /recruits faction reload). First, while the roster is over, the most recently hired recruit whose faction
        /// is known goes (loaded ones now, the others when they next load, as /recruits dismissall does); recruits from
        /// before 0.0.90 whose faction isn't known yet are never dismissed. Then, while the following is over, shared
        /// guards they borrowed go back to watch where they stand (they belong to someone else), and then the most
        /// recently hired of their own followers are dismissed. find looks a loaded entity up by id; titleOf names one
        /// for the chat line. Returns the titles of those dismissed now and how many go later. Public for tests.
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
        /// A recruit loaded (0.0.90). Its faction is remembered for its roster share. A recruit from before 0.0.90 whose
        /// faction was only learned now may put its owner over the roster, which is then put right as on joining. A
        /// follower that loads into a following that has filled up meanwhile (it was in an unloaded chunk) never
        /// counts as over: it keeps watch where it stands instead, like a hire past the following.
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
            if (npc.Alive && Recruit.OrderOf(npc) == Recruit.Follow && leader != null && !Fits(FollowUsed(leader, npc), FollowShare(FactionSystem.FactionOf(npc))))
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
                // 0.0.90: over limits an admin lowered (or a pre-0.0.90 army over the new ones) on joining.
                EnforceLimits(player);
            }
        }

        void OnTick(float dt)
        {
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
                    // Who may see its XP (0.0.76): the owner and the owner's /group.
                    Promotion.SyncOwnerGroups(recruit, kv.Value.Groups);
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
                    // Formation followers too far from the leader break off their fight (0.0.77).
                    if (UpdateFollowFight(recruit, follows ? leader : null) && recruit is EntityAgent agent) StopFights(agent);
                    // A loose follower in a fight isn't pulled away from it (0.0.77); it comes when the fight is over.
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

        // ---- orders (hold R) ----

        /// <summary>Is this recruit still breaking off after a movement order?</summary>
        public bool Disengaging(Entity recruit) => regrouping.Contains(recruit.EntityId) || (disengageUntil.TryGetValue(recruit.EntityId, out long until) && until > Now);

        /// <summary>Is this follower breaking off a fight to get back to its leader (0.0.77)?</summary>
        public bool Regrouping(Entity recruit) => regrouping.Contains(recruit.EntityId);

        /// <summary>Is the target more than FollowFightRange from a formation follower's leader? 0.0.77 kept such
        /// followers from picking it; 0.0.84 (owner: "remove the limit") no longer does, so nothing calls this now.</summary>
        public bool BeyondFollowRange(Entity recruit, Entity target) =>
            target != null && formationLeader.TryGetValue(recruit.EntityId, out Entity leader) && leader != null
            && target.Pos.SquareDistanceTo(leader.Pos) > FollowFightRange * FollowFightRange;

        /// <summary>
        /// Once a second for every loaded recruit, with its leader's entity when it is following an online leader
        /// (else null). Remembers formation followers' leaders, and starts or ends the break-off. True when this
        /// call started a break-off (the caller then stops the recruit's fight). Public for tests.
        /// </summary>
        public bool UpdateFollowFight(Entity recruit, Entity leader)
        {
            long id = recruit.EntityId;
            bool inFormation = leader != null && Recruit.OrderOf(recruit) == Recruit.Follow && Recruit.FormationOf(recruit) != Formation.Loose;
            if (!inFormation)
            {
                formationLeader.Remove(id);
                regrouping.Remove(id);
                return false;
            }
            formationLeader[id] = leader;
            double d2 = recruit.Pos.SquareDistanceTo(leader.Pos);
            if (regrouping.Contains(id))
            {
                if (d2 <= RegroupWithin * RegroupWithin) regrouping.Remove(id);
                return false;
            }
            if (d2 <= FollowFightRange * FollowFightRange) return false;
            regrouping.Add(id);
            return true;
        }

        /// <summary>Drops what UpdateFollowFight remembers about a recruit that left the list.</summary>
        void ForgetFollowFight(long id) { formationLeader.Remove(id); regrouping.Remove(id); }

        /// <summary>Starts the break-off after a movement order. Public for tests.</summary>
        public void SetDisengaging(Entity recruit)
        {
            long now = Now;
            FactionSystem.RemoveExpired(disengageUntil, now);
            disengageUntil[recruit.EntityId] = now + DisengageMs;
        }

        /// <summary>Which way followers in formation line up: the way the owner has been walking, else the way they last
        /// faced when giving an order.</summary>
        public Vec2d Heading(string uid) => Owner(uid) is HiringOwner owner ? new Vec2d(owner.HeadX, owner.HeadZ) : new Vec2d(0, 1);

        static void UpdateHeading(HiringOwner owner, EntityPlayer entity)
        {
            if (!owner.HasLast)
            {
                owner.LastX = entity.Pos.X;
                owner.LastZ = entity.Pos.Z;
                owner.HasLast = true;
                return;
            }
            double dx = entity.Pos.X - owner.LastX, dz = entity.Pos.Z - owner.LastZ;
            if (dx * dx + dz * dz < 2 * 2) return;
            double len = Math.Sqrt(dx * dx + dz * dz);
            owner.HeadX = dx / len;
            owner.HeadZ = dz / len;
            owner.LastX = entity.Pos.X;
            owner.LastZ = entity.Pos.Z;
        }

        /// <summary>The owner's recruits that hear an order: loaded, alive and within CommandRange.</summary>
        List<EntityAgent> Crew(EntityPlayer by) =>
            Led(by.PlayerUID).Where(e => e.Pos.Dimension == by.Pos.Dimension && e.Pos.SquareDistanceTo(by.Pos) <= CommandRange * CommandRange).ToList();

        // A borrower lost one of their followers to its owner: close up their formation.
        void RefreshBorrowerSlots(string uid)
        {
            if (sapi?.World.PlayerByUid(uid)?.Entity is EntityPlayer e && Owner(uid) is HiringOwner rec) RefreshFollowSlots(rec, e);
        }

        static void StopFights(EntityAgent npc)
        {
            AiTaskManager tasks = npc.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (tasks == null) return;
            tasks.StopTask<AiTaskMeleeAttack>();
            tasks.StopTask<AiTaskSeekEntity>();
            tasks.StopTask<AiTaskFactionShoot>();
        }

        void Disengage(EntityAgent npc)
        {
            StopFights(npc);
            SetDisengaging(npc);
        }

        /// <summary>Gives the owner's followers their places in the owner's formation (none when loose).</summary>
        public void RefreshFollowSlots(HiringOwner owner, EntityPlayer by)
        {
            if (sapi == null) return;
            List<EntityAgent> followers = Crew(by).Where(e => Recruit.OrderOf(e) == Recruit.Follow).ToList();
            string formation = Formation.Valid(owner.Formation);
            foreach (EntityAgent npc in followers) npc.WatchedAttributes.SetString(Recruit.FormationAttr, formation);
            if (formation == Formation.Loose)
            {
                foreach (EntityAgent npc in followers) Recruit.SetFollowSlot(npc, null);
                return;
            }
            List<Vec2d> slots = Formation.FollowOffsets(formation, followers.Count);
            Vec3d at = by.Pos.XYZ;
            int[] spotOf = Formation.AssignByRank(followers.Select(e => e.Pos.XYZ).ToList(),
                slots.Select(o => Formation.World(at, owner.HeadX, owner.HeadZ, o)).ToList(), slots, followers.Select(e => Formation.OrderOfRank(RankOf(e))).ToList());
            for (int i = 0; i < followers.Count; i++) Recruit.SetFollowSlot(followers[i], slots[spotOf[i]]);
        }

        /// <summary>Sends these recruits to hold the spots of a formation around anchor, facing (fx, fz).</summary>
        void Place(List<EntityAgent> crew, string formation, Vec3d anchor, double fx, double fz, int dimension, bool guards = false)
        {
            if (crew.Count == 0) return;
            List<Vec2d> offsets = Formation.Offsets(formation, crew.Count);
            List<Vec3d> spots = offsets.Select(o =>
            {
                Vec3d p = Formation.World(anchor, fx, fz, o);
                if (Ground.StandHeight(sapi.World.BlockAccessor, p.X, anchor.Y + 0.5, p.Z, dimension, 10) is double y) p.Y = y;
                return p;
            }).ToList();
            int[] spotOf = Formation.AssignByRank(crew.Select(e => e.Pos.XYZ).ToList(), spots, offsets, crew.Select(e => Formation.OrderOfRank(RankOf(e))).ToList());
            for (int i = 0; i < crew.Count; i++)
            {
                if (guards) Recruit.SetGuardSpot(crew[i], spots[spotOf[i]]); else Recruit.SetHoldAt(crew[i], spots[spotOf[i]]);
                if (formation != Formation.Loose) Recruit.SetFacing(crew[i], fx, fz);
                Recruit.SetFollowSlot(crew[i], null);
                crew[i].WatchedAttributes.SetString(Recruit.FormationAttr, formation);
            }
        }

        /// <summary>Carries out an order from the orders menu. Returns what to tell the player.</summary>
        public string Command(IServerPlayer player, RecruitCommand cmd)
        {
            string L(string key, params object[] args) => Lang.GetL(player.LanguageCode, "lotrnpcs:" + key, args);
            EntityPlayer by = player.Entity;
            if (sapi == null || by == null || cmd == null) return L("cmd-none");
            List<EntityAgent> crew = Crew(by);
            if (crew.Count == 0) return L("cmd-none");
            HiringOwner owner = EnsureOwner(player);
            // Movement orders from the owner make their recruits private again (Guard here for my group shares them).
            void OwnOrder(IEnumerable<EntityAgent> npcs) { foreach (EntityAgent npc in npcs) if (Recruit.OwnerUidOf(npc) == player.PlayerUID) Recruit.SetShared(npc, false); }
            if (cmd.Action == Recruit.Hold || cmd.Action == "move" || cmd.Action == Recruit.Guard) OwnOrder(crew);

            // The way the player looks, for hold orders, and the default way to face.
            double fx = cmd.Fx, fz = cmd.Fz, flen = Math.Sqrt(fx * fx + fz * fz);
            if (flen < 1e-3) { fx = owner.HeadX; fz = owner.HeadZ; flen = Math.Sqrt(fx * fx + fz * fz); }
            fx /= flen; fz /= flen;
            string formation = Formation.Valid(owner.Formation);

            switch (cmd.Action)
            {
                case Recruit.Follow:
                    {
                        // As many follow as fit the follow shares (0.0.90; before, at most MaxFollowers). Those already
                        // following stay with the owner; the others are tried nearest first, each joining if its share
                        // fits (so a goblin may still join after an elf didn't), and the rest keep their orders.
                        List<EntityAgent> following = Followers(player.PlayerUID);
                        double used = following.Sum(e => FollowShare(FactionSystem.FactionOf(e)));
                        List<EntityAgent> joining = crew.Where(e => !following.Contains(e)).OrderBy(e => e.Pos.SquareDistanceTo(by.Pos)).ToList();
                        List<EntityAgent> coming = following.Where(crew.Contains).ToList();
                        int joined = 0, left = 0;
                        string firstLeft = null;
                        foreach (EntityAgent npc in joining)
                        {
                            double share = FollowShare(FactionSystem.FactionOf(npc));
                            if (Fits(used, share)) { used += share; coming.Add(npc); joined++; }
                            else { left++; firstLeft ??= FactionSystem.FactionOf(npc); }
                        }
                        OwnOrder(coming);
                        foreach (EntityAgent npc in coming) { Recruit.SetOrder(npc, Recruit.Follow); Disengage(npc); }
                        owner.HeadX = fx; owner.HeadZ = fz;
                        owner.LastX = by.Pos.X; owner.LastZ = by.Pos.Z; owner.HasLast = true;
                        RefreshFollowSlots(owner, by);
                        int now = following.Count + joined;
                        return left > 0 ? L("cmd-follow-full", now, left, RoomText(Room(player.PlayerUID, firstLeft), (k, a) => L(k, a))) : L("cmd-follow", now);
                    }

                case Recruit.Hold:
                    owner.FaceX = fx; owner.FaceZ = fz;
                    if (formation == Formation.Loose)
                    {
                        foreach (EntityAgent npc in crew)
                        {
                            Recruit.SetOrder(npc, Recruit.Hold);
                            Recruit.SetFollowSlot(npc, null);
                            npc.WatchedAttributes.SetString(Recruit.FormationAttr, formation);
                        }
                    }
                    else Place(crew, formation, Centre(crew.Select(e => e.Pos.XYZ)), fx, fz, by.Pos.Dimension);
                    return L("cmd-hold", crew.Count);

                case Recruit.Guard:
                case Recruit.GuardShared:
                    {
                        // Guard here: the post is where the player stands. Guards spread out over the area on their own.
                        // Guard here for my group shares the owner's own; borrowed guards posted either way go back to
                        // being shared and free to borrow.
                        Vec3d post = PostAt(by);
                        bool returned = false;
                        foreach (EntityAgent npc in crew)
                        {
                            if (Recruit.OwnerUidOf(npc) == player.PlayerUID) Recruit.SetShared(npc, cmd.Action == Recruit.GuardShared);
                            else { Recruit.ClearBorrower(npc); returned = true; }
                            Recruit.SetGuardAt(npc, post);
                            Recruit.SetFollowSlot(npc, null);
                            npc.WatchedAttributes.SetString(Recruit.FormationAttr, formation);
                            Disengage(npc);
                        }
                        // In a formation, guards stand in it around the post, facing the way the player looks.
                        if (formation != Formation.Loose)
                        {
                            owner.FaceX = fx; owner.FaceZ = fz;
                            Place(crew, formation, post, fx, fz, by.Pos.Dimension, true);
                        }
                        return cmd.Action == Recruit.GuardShared || returned ? L("cmd-guard-shared", crew.Count, (int)GuardRadius) : L("cmd-guard", crew.Count, (int)GuardRadius);
                    }

                case "move":
                    if (!cmd.HasTarget) return L("cmd-notarget");
                    Vec3d target = new Vec3d(cmd.X, cmd.Y, cmd.Z);
                    if (target.SquareDistanceTo(by.Pos.XYZ) > CommandRange * CommandRange) return L("cmd-toofar");
                    double mx = target.X - by.Pos.X, mz = target.Z - by.Pos.Z, mlen = Math.Sqrt(mx * mx + mz * mz);
                    if (mlen > 0.5) { fx = mx / mlen; fz = mz / mlen; }
                    owner.FaceX = fx; owner.FaceZ = fz;
                    Place(crew, formation, target, fx, fz, by.Pos.Dimension);
                    foreach (EntityAgent npc in crew) Disengage(npc);
                    return L("cmd-move", crew.Count);

                case "formation":
                    if (!Formation.All.Contains(cmd.Arg)) return L("cmd-none");
                    owner.Formation = cmd.Arg;
                    RefreshFollowSlots(owner, by);
                    List<EntityAgent> holders = crew.Where(e => Recruit.OrderOf(e) == Recruit.Hold).ToList();
                    // Holders re-form around the middle of the spots they hold; loose leaves them where they are.
                    if (cmd.Arg != Formation.Loose && holders.Count > 0)
                        Place(holders, cmd.Arg, Centre(holders.Select(e => Recruit.HoldPos(e) ?? e.Pos.XYZ)), owner.FaceX, owner.FaceZ, by.Pos.Dimension);
                    foreach (EntityAgent npc in holders)
                    {
                        npc.WatchedAttributes.SetString(Recruit.FormationAttr, cmd.Arg);
                        if (cmd.Arg == Formation.Loose) Recruit.SetGuardSpot(npc, null);   // drops the facing: loose holders stand easy
                    }
                    // Guards form up around their post (each post its own formation); loose sends them back to patrolling.
                    foreach (IGrouping<string, EntityAgent> post in crew.Where(e => Recruit.OrderOf(e) == Recruit.Guard && Recruit.HoldPos(e) != null)
                        .GroupBy(e => { Vec3d p = Recruit.HoldPos(e); return Math.Round(p.X, 1) + "/" + Math.Round(p.Y, 1) + "/" + Math.Round(p.Z, 1); }))
                    {
                        List<EntityAgent> guards = post.ToList();
                        foreach (EntityAgent npc in guards) { npc.WatchedAttributes.SetString(Recruit.FormationAttr, cmd.Arg); Recruit.SetGuardSpot(npc, null); }
                        if (cmd.Arg != Formation.Loose) Place(guards, cmd.Arg, Recruit.HoldPos(guards[0]), owner.FaceX, owner.FaceZ, by.Pos.Dimension, true);
                    }
                    return L("cmd-formation", L("formation-" + cmd.Arg));

                case "stance":
                    if (!Recruit.Stances.Contains(cmd.Arg)) return L("cmd-none");
                    foreach (EntityAgent npc in crew)
                    {
                        Recruit.SetStance(npc, cmd.Arg);
                        // Calmer stances drop fights the new stance wouldn't pick; the others carry on.
                        if (cmd.Arg == Recruit.Passive || cmd.Arg == Recruit.Defensive) StopFights(npc);
                    }
                    return L("cmd-stance", L("stance-" + cmd.Arg), crew.Count);
            }
            return L("cmd-none");
        }

        /// <summary>Where a guard post goes when this player orders Guard here: on the ground where they stand.</summary>
        Vec3d PostAt(Entity by)
        {
            Vec3d post = by.Pos.XYZ;
            if (sapi != null && Ground.StandHeight(sapi.World.BlockAccessor, post.X, post.Y + 0.5, post.Z, by.Pos.Dimension, 10) is double y) post.Y = y;
            return post;
        }

        static Vec3d Centre(IEnumerable<Vec3d> points)
        {
            List<Vec3d> list = points.ToList();
            if (list.Count == 0) return new Vec3d();
            return new Vec3d(list.Average(p => p.X), list.Average(p => p.Y), list.Average(p => p.Z));
        }

        // ---- the whitelist (/recruits whitelist) ----

        TextCommandResult Whitelist(TextCommandCallingArgs args, bool add)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only a player can have recruits.");
            string text = ChangeWhitelist(player, args[0] as string, add, out bool ok);
            return ok ? TextCommandResult.Success(text) : TextCommandResult.Error(text);
        }

        /// <summary>
        /// Puts a player on the owner's whitelist or takes them off (the command and, since 0.0.76, the whitelist menu).
        /// Returns the reply; ok is false when nothing changed because of a mistake (unknown player, yourself, not
        /// listed).
        /// </summary>
        public string ChangeWhitelist(IServerPlayer player, string name, bool add, out bool ok)
        {
            string L(string key, params object[] a) => Lang.GetL(player?.LanguageCode, "lotrnpcs:" + key, a);
            ok = false;
            if (!(FactionSystem.FindPlayer(sapi, name) is (string uid, string known))) return L("whitelist-unknown", name);
            if (uid == player.PlayerUID) return L("whitelist-self");
            HiringOwner owner = Owner(player.PlayerUID);
            if (add)
            {
                if (owner == null) { owners[player.PlayerUID] = owner = new HiringOwner(); Refresh(owner, player); }
                ok = true;
                if (owner.Whitelist.ContainsKey(uid)) return L("whitelist-already", known);
                owner.Whitelist[uid] = known;
                sapi.Logger.Notification("[lotrnpcs] " + player.PlayerName + " put " + known + " on their recruit whitelist.");
                return L("whitelist-added", known);
            }
            if (owner == null || !owner.Whitelist.Remove(uid)) return L("whitelist-notlisted", known);
            ok = true;
            sapi.Logger.Notification("[lotrnpcs] " + player.PlayerName + " took " + known + " off their recruit whitelist.");
            return L("whitelist-removed", known);
        }

        /// <summary>The whitelist menu's contents for this player (0.0.76): who is listed, and who is online to add.</summary>
        public WhitelistState WhitelistStateFor(IServerPlayer player, string notice = null)
        {
            Dictionary<string, string> list = Owner(player.PlayerUID)?.Whitelist ?? new Dictionary<string, string>();
            return new WhitelistState
            {
                Listed = list.OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Value).ToList(),
                Online = sapi.World.AllOnlinePlayers.Where(p => p.PlayerUID != player.PlayerUID && !list.ContainsKey(p.PlayerUID))
                    .Select(p => p.PlayerName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
                Notice = notice,
            };
        }

        TextCommandResult OpenWhitelistMenu(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only a player can have recruits.");
            sapi.ModLoader.GetModSystem<WhitelistMenuSystem>()?.Open(player);
            return TextCommandResult.Success();
        }

        TextCommandResult ListWhitelist(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only a player can have recruits.");
            Dictionary<string, string> list = Owner(player.PlayerUID)?.Whitelist;
            if (list == null || list.Count == 0) return TextCommandResult.Success(Lang.GetL(player.LanguageCode, "lotrnpcs:whitelist-empty"));
            return TextCommandResult.Success(Lang.GetL(player.LanguageCode, "lotrnpcs:whitelist-list", string.Join(", ", list.Values.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))));
        }

        void Load()
        {
            byte[] bytes = sapi.WorldManager.SaveGame.GetData(SaveKey);
            if (bytes == null) return;
            try
            {
                owners = JsonConvert.DeserializeObject<Dictionary<string, HiringOwner>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, HiringOwner>();
                foreach (string uid in owners.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) owners.Remove(uid);
                foreach (HiringOwner owner in owners.Values)
                {
                    owner.Recruits = owner.Recruits?.Distinct().ToList() ?? new List<long>();
                    owner.Groups ??= new List<string>();
                    owner.Whitelist ??= new Dictionary<string, string>();
                    owner.PendingDismiss ??= new List<long>();
                    owner.RecruitFactions ??= new Dictionary<long, string>();
                    owner.Captains ??= new HashSet<long>();
                }
                sapi.Logger.Notification("[lotrnpcs] Loaded " + owners.Values.Sum(o => o.Recruits.Count) + " recruits of " + owners.Count + " players.");
            }
            catch (Exception e)
            {
                sapi.Logger.Error("[lotrnpcs] Could not read the recruits list: " + e.Message);
            }
        }

        void Save()
        {
            // Factions of recruits no longer on the list (dead, dismissed) aren't needed any more.
            foreach (HiringOwner owner in owners.Values)
            {
                if (owner.RecruitFactions == null) continue;
                foreach (long id in owner.RecruitFactions.Keys.Where(id => !owner.Recruits.Contains(id)).ToList()) owner.RecruitFactions.Remove(id);
                owner.Captains?.RemoveWhere(id => !owner.Recruits.Contains(id));
            }
            sapi.WorldManager.SaveGame.StoreData(SaveKey, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(owners)));
        }
    }

    /// <summary>Finding somewhere to stand: for teleporting followers, placing formations, guard posts and patrols.</summary>
    public static class Ground
    {
        /// <summary>The height to stand at near (x, y, z): a free block on solid, harmless ground, searched up and down
        /// from y (tries: how many heights). Null if there is none.</summary>
        public static double? StandHeight(IBlockAccessor ba, double x, double y0, double z, int dimension, int tries)
        {
            BlockPos bpos = new BlockPos(dimension);
            int bx = (int)Math.Floor(x), bz = (int)Math.Floor(z);
            for (int j = 0; j < tries; j++)
            {
                int dy = (1 - (j % 2) * 2) * (int)Math.Ceiling(j / 2f);
                int y = (int)Math.Floor(y0) + dy;
                bpos.Set(bx, y, bz);
                if (!Free(ba, bpos)) continue;
                bpos.Up();
                if (!Free(ba, bpos)) continue;
                bpos.Set(bx, y - 1, bz);
                Block below = ba.GetBlock(bpos);
                Cuboidf[] boxes = below.GetCollisionBoxes(ba, bpos);
                if (boxes == null || boxes.Length == 0 || below.Attributes?["insideDamage"].AsInt(0) > 0) continue;
                float top = 0;
                foreach (Cuboidf box in boxes) top = Math.Max(top, box.Y2);
                return y - 1 + top;
            }
            return null;
        }

        static bool Free(IBlockAccessor ba, BlockPos pos)
        {
            Block block = ba.GetBlock(pos);
            Cuboidf[] boxes = block.GetCollisionBoxes(ba, pos);
            return (boxes == null || boxes.Length == 0) && !(block.Attributes?["insideDamage"].AsInt(0) > 0) && !block.IsLiquid();
        }
    }

    /// <summary>
    /// On every faction NPC, both sides: right-click opens the talk menu (user, 2026-11-02: "a menu similar to the
    /// traders"), and for recruits it keeps them from wandering, stops friendly fire from their own side, and calls
    /// the other recruits when one is hurt. It goes before "health" so friendly fire is cancelled before it lands.
    /// </summary>
    public class EntityBehaviorHireable : EntityBehavior
    {
        public const int OpenPacket = 46301, ActionPacket = 46302, ReplyPacket = 46303, ClosePacket = 46304, TalkRequestPacket = 46305;
        /// <summary>Client to server: a LooksRequest as JSON (0.0.54). Server to client: the refusal's lang key.</summary>
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
            if (task is AiTaskSeekEntity || task is AiTaskMeleeAttack) return Recruit.OwnerUidOf(entity) == null || Recruit.StanceOf(entity) != Recruit.Passive;
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
            string refused = Looks.Apply(entity, player.PlayerUID, req, Looks.PartsOf(entity.Api, Attrs.String(entity.WatchedAttributes, "skinModel")), Looks.WeaponChoices(entity), Looks.Head(entity), Looks.ShieldOf(entity), Looks.HelmetChoices(entity));
            talkingUntilMs = entity.World.ElapsedMilliseconds + 30000;
            if (refused != null) ((ICoreServerAPI)entity.Api).Network.SendEntityPacket(player, entity.EntityId, LooksPacket, Encoding.UTF8.GetBytes(refused));
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
            // Unloaded recruits stay hired; death is handled in OnEntityDeath.
            if (reason == EnumDespawnReason.Unload || reason == EnumDespawnReason.Disconnect || reason == EnumDespawnReason.OutOfRange || reason == EnumDespawnReason.Death) return;
            hiring.OnRecruitGone(entity, false);
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            if (!(Recruit.OwnerUidOf(entity) is string owner)) return;
            // A named recruit still shows its people and rank (0.0.54).
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
            player?.PlayerUID != null && Recruit.OwnerUidOf(entity) != null && Recruit.LeaderUidOf(entity) == player.PlayerUID ? OwnerHelp : null;

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

    /// <summary>
    /// Recruits follow their owner, loosely or in the owner's formation, go to the spot they hold, or patrol the area
    /// they guard. They walk, and run whenever they fall behind (user, 2026-11-03: "they move at a walking speed"),
    /// switching on the way; patrols are walked, unless a guard is heading back from outside its area. Faction fights
    /// come first: seek and melee have higher priority.
    /// </summary>
    public class AiTaskFollowOwner : AiTaskBase
    {
        // Run when farther than this from the goal, walk again when closer than WalkWithin.
        const float RunBeyond = 6, WalkWithin = 3;
        // Guards stand this long (ms) between patrol walks, which go up to PatrolShare of the radius from the post.
        // Coming back from outside the area, they aim for the inner ReturnShare of it.
        const int PatrolPauseMinMs = 6000, PatrolPauseMaxMs = 16000;
        const double PatrolShare = 0.8, ReturnShare = 0.5;
        static readonly FieldInfo SpeedField = typeof(PathTraverserBase).GetField("movingSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo NewSpeedField = typeof(Vintagestory.Essentials.WaypointsTraverser).GetField("movingSpeed_New", BindingFlags.NonPublic | BindingFlags.Instance);

        readonly float walkSpeed, runSpeed, followDistance, holdDistance;
        readonly AnimationMetaData runAnim;
        readonly HiringSystem hiring;
        Vec3d goalAtStart;
        bool stuck, running, patrolling, returning;
        // Set while the guard fights, so it walks back to its post once the fight is over (0.0.76).
        bool foughtSincePost;
        // Back at the post after a chase: a spot within this many blocks of it.
        const double BackToPostWithin = 3;
        long nextCheckMs, nextPatrolMs;
        float seconds;

        public AiTaskFollowOwner(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            walkSpeed = taskConfig["walkSpeed"].AsFloat(0.025f);
            runSpeed = taskConfig["runSpeed"].AsFloat(0.055f);
            followDistance = taskConfig["followDistance"].AsFloat(4);
            holdDistance = taskConfig["holdDistance"].AsFloat(2);
            hiring = entity.Api.ModLoader.GetModSystem<HiringSystem>();
            AnimationMetaData sprint = entity.Properties.Client?.Animations?.FirstOrDefault(a => a.Code == "sprint");
            if (sprint != null)
            {
                runAnim = sprint.Clone();
                runAnim.AnimationSpeed = taskConfig["runAnimationSpeed"].AsFloat(0.75f);
            }
        }

        // Holding a spot: a held one, or a guard's place in its formation.
        bool Holding
        {
            get
            {
                string order = Recruit.OrderOf(entity);
                return order == Recruit.Hold || (order == Recruit.Guard && Recruit.HasGuardSpot(entity));
            }
        }
        // Going for an exact spot: a held spot, a patrol spot, or a place in the owner's formation.
        bool Exact => patrolling || Holding || (hiring != null && Recruit.FollowSlot(entity) != null);
        // Standing in a formation (held or guarded): close to the exact spot, so the shape stays neat.
        bool InFormation => Holding && Recruit.HasFacing(entity);
        float StartDistance => InFormation ? 0.8f : Holding ? holdDistance : Exact ? 1.6f : followDistance;
        float StopDistance => InFormation ? 0.35f : Exact ? 0.9f : 2.5f;

        Vec3d Goal()
        {
            string uid = Recruit.LeaderUidOf(entity);
            if (uid == null) return null;
            string order = Recruit.OrderOf(entity);
            // A guard walks to the spot ShouldPatrol picked, and nowhere else.
            if (order == Recruit.Guard) return Recruit.HasGuardSpot(entity) ? Recruit.GuardSpot(entity) : patrolling ? goalAtStart : null;
            if (patrolling) return null;
            if (order == Recruit.Hold) return Recruit.HoldPos(entity);
            EntityPlayer owner = Recruit.OnlineLeader(entity);
            if (owner == null || !owner.Alive || owner.Pos.Dimension != entity.Pos.Dimension) return null;
            Vec2d slot = Recruit.FollowSlot(entity);
            if (slot == null || hiring == null) return owner.Pos.XYZ;
            Vec2d heading = hiring.Heading(uid);
            return Formation.World(owner.Pos.XYZ, heading.X, heading.Y, slot);
        }

        // Exact spots count across the ground, so a slope or a step doesn't keep them shuffling.
        double Distance(Vec3d goal)
        {
            double dx = entity.Pos.X - goal.X, dy = entity.Pos.Y - goal.Y, dz = entity.Pos.Z - goal.Z;
            return Exact && Math.Abs(dy) < 2.5 ? Math.Sqrt(dx * dx + dz * dz) : Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public override bool ShouldExecute()
        {
            long now = entity.World.ElapsedMilliseconds;
            if (now < nextCheckMs) return false;
            nextCheckMs = now + 400;
            if (Recruit.OwnerUidOf(entity) == null) return false;
            if (Recruit.OrderOf(entity) == Recruit.Guard && !Recruit.HasGuardSpot(entity)) return ShouldPatrol(now);
            patrolling = returning = false;
            // 0.0.77 (owner, 2026-11-04: on aggressive, "they run to me after they make a hit, then re aggro back onto
            // the closest enemy"): a follower in a fight stays in it between blows instead of running back to its
            // leader. It comes back when the fight is over, or (in a formation other than loose) when it breaks off.
            if (Recruit.OrderOf(entity) == Recruit.Follow && hiring != null && Recruit.StanceOf(entity) != Recruit.Passive && !hiring.Disengaging(entity)
                && entity.GetBehavior<EntityBehaviorFactionMember>()?.Fighting(now) == true) return false;
            Vec3d goal = Goal();
            if (goal == null || Distance(goal) <= StartDistance)
            {
                // In place: turn to face the formation's way.
                if (goal != null && InFormation)
                {
                    float yaw = Recruit.FacingYaw(entity);
                    if (Math.Abs(GameMath.AngleRadDistance(entity.Pos.Yaw, yaw)) > 0.15f)
                    {
                        entity.Pos.Yaw = yaw;
                        if (entity is EntityAgent agent) agent.BodyYaw = yaw;
                    }
                }
                return false;
            }
            goalAtStart = goal;
            return true;
        }

        // Guard here: every so often, and whenever it is outside its area, a guard picks a spot in the area to walk to.
        bool ShouldPatrol(long now)
        {
            if (!Recruit.TryGetPost(entity, out double px, out double py, out double pz)) return false;
            double radius = hiring?.GuardRadius ?? FactionConfig.DefaultGuardRadius;
            double dx = entity.Pos.X - px, dz = entity.Pos.Z - pz;
            // 0.0.76 (user, 2026-11-04: "after a chase, the recruits should return to post"): a guard that chased
            // something walks back to its post when the fight is over, instead of staying where it ended.
            if (entity.GetBehavior<EntityBehaviorFactionMember>()?.Fighting(now) == true) { foughtSincePost = true; return false; }
            if (foughtSincePost)
            {
                foughtSincePost = false;
                if (dx * dx + dz * dz > (BackToPostWithin + 1) * (BackToPostWithin + 1))
                {
                    Vec3d home = new Vec3d(px, py, pz);
                    patrolling = true;
                    returning = true;
                    goalAtStart = PatrolSpot(home, BackToPostWithin) ?? home;
                    nextPatrolMs = now + PatrolPauseMinMs;
                    return true;
                }
            }
            bool outside = dx * dx + dz * dz > radius * radius || Math.Abs(entity.Pos.Y - py) > radius;
            if (!outside && now < nextPatrolMs) return false;
            Vec3d post = new Vec3d(px, py, pz);
            Vec3d spot = PatrolSpot(post, radius * (outside ? ReturnShare : PatrolShare)) ?? (outside ? post : null);
            if (spot == null)
            {
                nextPatrolMs = now + 3000;
                return false;
            }
            patrolling = true;
            returning = outside;
            goalAtStart = spot;
            return true;
        }

        // A random spot to stand on within reach blocks of the post, or null if a few tries find no ground.
        Vec3d PatrolSpot(Vec3d post, double reach)
        {
            Random rand = entity.World.Rand;
            IBlockAccessor ba = entity.World.BlockAccessor;
            for (int i = 0; i < 6; i++)
            {
                // Evenly over the disc, not bunched in the middle.
                double r = reach * Math.Sqrt(rand.NextDouble()), a = rand.NextDouble() * 2 * Math.PI;
                double x = post.X + r * Math.Cos(a), z = post.Z + r * Math.Sin(a);
                if (Ground.StandHeight(ba, x, post.Y + 0.5, z, entity.Pos.Dimension, 12) is double y) return new Vec3d(x, y, z);
            }
            return null;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            seconds = 0;
            stuck = false;
            running = false;
            bool run = MayRun && Distance(goalAtStart) > RunBeyond;
            // Without a path it walks straight there (WaypointsTraverser does that when no onNoPath is given).
            pathTraverser.NavigateTo_Async(goalAtStart.Clone(), run ? runSpeed : walkSpeed, InFormation ? 0.25f : Exact ? 0.6f : 2f, OnGoalReached, OnStuck, null, 2000, 1);
            SetRunning(run);
        }

        // A patrol is walked; everything else is run when far behind.
        bool MayRun => !patrolling || returning;

        public override bool CanContinueExecute() => pathTraverser.Ready;

        public override bool ContinueExecute(float dt)
        {
            seconds += dt;
            Vec3d goal = Goal();
            if (goal == null || stuck) return false;
            // The owner moved on: aim for where they (or their place in the formation) are now.
            if (pathTraverser.CurrentTarget is Vec3d target)
            {
                if (goalAtStart.SquareDistanceTo(goal) > 3 * 3)
                {
                    goalAtStart = goal;
                    pathTraverser.Retarget();
                }
                target.Set(goal.X, goal.Y, goal.Z);
            }
            double distance = Distance(goal);
            bool run = MayRun && (distance > RunBeyond || (running && distance > WalkWithin));
            if (run != running) SetRunning(run);
            if (distance < StopDistance) return false;
            return pathTraverser.Active || seconds < 2;
        }

        // Changes pace on the way: the traverser keeps the speed it was started with, so it is set directly.
        void SetRunning(bool run)
        {
            float speed = run ? runSpeed : walkSpeed;
            SpeedField?.SetValue(pathTraverser, speed);
            if (pathTraverser is Vintagestory.Essentials.WaypointsTraverser) NewSpeedField?.SetValue(pathTraverser, speed);
            if (runAnim != null)
            {
                if (run && !running) entity.AnimManager.StartAnimation(runAnim);
                if (!run && running) entity.AnimManager.StopAnimation(runAnim.Code);
            }
            running = run;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            pathTraverser.Stop();
            if (running && runAnim != null) entity.AnimManager.StopAnimation(runAnim.Code);
            running = false;
            long now = entity.World.ElapsedMilliseconds;
            if (patrolling) nextPatrolMs = now + entity.World.Rand.Next(PatrolPauseMinMs, PatrolPauseMaxMs + 1);
            patrolling = returning = false;
            if (stuck) nextCheckMs = now + 3000;
        }

        void OnStuck() => stuck = true;
        void OnGoalReached() { }
    }

    /// <summary>
    /// The talk menu, built like vanilla's trader dialogue: the NPC's lines and the player's numbered choices in
    /// a scrolling text box (number keys pick a choice). The server decides what is on offer (TalkState).
    /// </summary>
    public class GuiDialogTalk : GuiDialog
    {
        readonly EntityAgent npc;
        readonly string faction;
        TalkState state;
        GuiElementRichtext textElem;
        ElementBounds clipBounds;
        bool confirmDismiss, closing;
        int lastQuestion;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogTalk(ICoreClientAPI capi, EntityAgent npc, TalkState state) : base(capi)
        {
            this.npc = npc;
            this.state = state;
            faction = FactionSystem.FactionOf(npc);
        }

        public void Begin()
        {
            Compose();
            TryOpen();
            string kind = state.Mode == "member" ? "greet" : state.Mode == "own" || state.Mode == "borrowed" ? "own" : state.Mode == "others" ? "others"
                : state.Mode == "shared" ? "shared" : "stranger";
            Npc(Line(kind));
            if (state.Mode == "member") Status();
            Options();
        }

        public void OnReply(TalkState next)
        {
            state = next;
            confirmDismiss = false;
            if (state.Reply != null)
            {
                string key = Has("lotrnpcs:" + state.Reply + "-" + faction) ? "lotrnpcs:" + state.Reply + "-" + faction : "lotrnpcs:" + state.Reply;
                Npc(Format(key));
                if (state.Reply == "talk-hired" || state.Reply == "talk-hired-guard") Status();
            }
            Options();
        }

        void Compose()
        {
            ClearComposers();
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle).WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);
            const int w = 480, h = 340;
            ElementBounds textBounds = ElementBounds.Fixed(0, 30, w, h);
            clipBounds = textBounds.ForkBoundingParent();
            ElementBounds insetBounds = textBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-2, -2);
            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(3 + textBounds.fixedWidth + 7).WithFixedWidth(20);
            ElementBounds byeBounds = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);
            string title = Recruit.OwnerUidOf(npc) != null ? Recruit.Title(npc) : npc.GetBehavior<EntityBehaviorNameTag>()?.DisplayName ?? npc.GetName();

            SingleComposer = capi.Gui.CreateCompo("lotrnpcs-talk-" + npc.EntityId, dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(title, () => TryClose())
                .BeginChildElements(bgBounds)
                    .BeginClip(clipBounds)
                        .AddInset(insetBounds, 3)
                        .AddRichtext("", CairoFont.WhiteSmallText(), textBounds.WithFixedPadding(5).WithFixedSize(w - 10, h - 10), "talkText")
                    .EndClip()
                    .AddVerticalScrollbar(OnScroll, scrollbarBounds, "scrollbar")
                    .AddSmallButton(Lang.Get("lotrnpcs:talk-opt-bye"), () => TryClose(), byeBounds.FixedUnder(clipBounds, 20))
                .EndChildElements()
                .Compose();
            textElem = SingleComposer.GetRichtext("talkText");
        }

        void OnScroll(float value)
        {
            textElem.Bounds.fixedY = -value;
            textElem.Bounds.CalcWorldBounds();
        }

        static CairoFont Font() => CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);

        void Emit(RichTextComponentBase[] comps)
        {
            // Only the newest choices can be clicked.
            foreach (RichTextComponentBase comp in textElem.Components)
            {
                if (comp is LinkTextComponent link) link.Clickable = false;
            }
            textElem.AppendText(comps);
            GuiElementScrollbar bar = SingleComposer.GetScrollbar("scrollbar");
            bar.Bounds.CalcWorldBounds();
            bar.SetHeights((float)clipBounds.fixedHeight, (float)textElem.Bounds.fixedHeight);
            bar.ScrollToBottom();
        }

        void Npc(string text) => Emit(VtmlUtil.Richtextify(capi, text + "\r\n", Font()));

        void Player(string text) =>
            Emit(new RichTextComponentBase[] { new RichTextComponent(capi, text + "\r\n\r\n", Font().WithColor(GuiStyle.ColorParchment).WithOrientation(EnumTextOrientation.Right)) });

        void Options()
        {
            List<RichTextComponentBase> comps = new List<RichTextComponentBase> { new RichTextComponent(capi, "\r\n", Font()) };
            int number = 1;
            void Add(string text, Action onClick)
            {
                comps.Add(new LinkTextComponent(capi, number++ + ". " + text, Font().WithColor(GuiStyle.ColorTime1).WithOrientation(EnumTextOrientation.Right), _ => onClick()));
                comps.Add(new RichTextComponent(capi, "\r\n", Font()));
            }
            void Choice(string key, Action onClick) { string text = Format(key); Add(text, () => { Player(text); onClick(); }); }

            if (confirmDismiss)
            {
                Choice("lotrnpcs:talk-opt-dismiss-yes", () => Send("dismiss"));
                Choice("lotrnpcs:talk-opt-dismiss-no", () => { confirmDismiss = false; Npc(Line("stay")); Options(); });
            }
            else if (state.Mode == "member")
            {
                Choice(state.Price > 0 ? "lotrnpcs:talk-opt-hire" : "lotrnpcs:talk-opt-hire-free", () => Send("hire"));
                Add(Lang.Get("lotrnpcs:talk-opt-ask"), Ask);
            }
            else if (state.Mode == "own")
            {
                Choice("lotrnpcs:talk-opt-follow", () => Send(Recruit.Follow));
                Choice("lotrnpcs:talk-opt-hold", () => Send(Recruit.Hold));
                Choice("lotrnpcs:talk-opt-guard", () => Send(Recruit.Guard));
                Choice("lotrnpcs:talk-opt-guard-shared", () => Send(Recruit.GuardShared));
                Choice("lotrnpcs:talk-opt-looks", OpenLooks);
                // Promotion (0.0.76), once the recruit's XP is full.
                foreach (KeyValuePair<string, int> offer in state.Promote ?? new Dictionary<string, int>())
                {
                    string text = offer.Value > 0 ? Lang.Get("lotrnpcs:talk-opt-promote", Lang.Get("lotrnpcs:title-" + offer.Key), offer.Value, CoinName())
                        : Lang.Get("lotrnpcs:talk-opt-promote-free", Lang.Get("lotrnpcs:title-" + offer.Key));
                    string rank = offer.Key;
                    Add(text, () => { Player(text); Send("promote:" + rank); });
                }
                Add(Lang.Get("lotrnpcs:talk-opt-ask"), Ask);
                Choice("lotrnpcs:talk-opt-dismiss", () => { confirmDismiss = true; Npc(Format("lotrnpcs:talk-dismiss-ask")); Options(); });
            }
            else if (state.Mode == "borrowed")
            {
                Choice("lotrnpcs:talk-opt-follow", () => Send(Recruit.Follow));
                Choice("lotrnpcs:talk-opt-hold", () => Send(Recruit.Hold));
                Choice("lotrnpcs:talk-opt-return", () => Send(Recruit.Guard));
                Add(Lang.Get("lotrnpcs:talk-opt-ask"), Ask);
            }
            else if (state.Mode == "shared")
            {
                Choice("lotrnpcs:talk-opt-borrow", () => Send("borrow"));
                Add(Lang.Get("lotrnpcs:talk-opt-ask"), Ask);
            }
            Add(Lang.Get("lotrnpcs:talk-opt-bye"), () => TryClose());
            Emit(comps.ToArray());
        }

        GuiDialogLooks looks;

        // The appearance dialog opens beside the talk; the NPC remarks on it when the owner is done.
        void OpenLooks()
        {
            if (looks?.IsOpened() == true) { Options(); return; }
            looks = new GuiDialogLooks(capi, npc, done =>
            {
                looks = null;
                if (!IsOpened()) return;
                Npc(Line(done ? "looks-done" : "looks-cancel"));
                Options();
            });
            looks.TryOpen();
        }

        public void OnLooksRefused(string key) => looks?.Refused(key);

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            looks?.TryClose();
        }

        // The NPC is gone (promoted into a new one, or dead): the conversation ends.
        public override void OnRenderGUI(float deltaTime)
        {
            if (!closing && (!npc.Alive || capi.World.GetEntityById(npc.EntityId) == null)) { closing = true; TryClose(); return; }
            base.OnRenderGUI(deltaTime);
        }

        // The server answers with a new TalkState (OnReply).
        void Send(string action) => capi.Network.SendEntityPacket(npc.EntityId, EntityBehaviorHireable.ActionPacket, Encoding.UTF8.GetBytes(action));

        // A random question and the NPC's answer, never the same twice in a row.
        void Ask()
        {
            string prefix = Count("lotrnpcs:talk-ask-" + faction + "-") > 0 ? faction + "-" : "";
            int count = Count("lotrnpcs:talk-ask-" + prefix);
            if (count == 0) { Options(); return; }
            int pick = capi.World.Rand.Next(count) + 1;
            if (pick == lastQuestion && count > 1) pick = pick % count + 1;
            lastQuestion = pick;
            Player(Lang.Get("lotrnpcs:talk-ask-" + prefix + pick));
            Npc(Lang.Get("lotrnpcs:talk-answer-" + prefix + pick));
            Options();
        }

        // A random line of this kind for the NPC's faction, or a general one.
        string Line(string kind)
        {
            foreach (string prefix in new[] { "lotrnpcs:talk-" + kind + "-" + faction + "-", "lotrnpcs:talk-" + kind + "-" })
            {
                int count = Count(prefix);
                if (count > 0) return Format(prefix + (capi.World.Rand.Next(count) + 1));
            }
            return "...";
        }

        // {0} price, {1} coin name, {2} the baseline roster, {3} owner, {4} recruits the player leads, {5} the baseline
        // follow limit, {6} the room line (0.0.90 follow shares: "Room in your band: Mordor orcs 1, Men of Gondor 0."),
        // {7} the roster line (0.0.91: "Room on your roster: Mordor orcs 12.").
        string Format(string key) => Lang.Get(key, state.Price, CoinName(), state.Cap, state.OwnerName ?? "", state.Hired, state.FollowCap,
            HiringSystem.RoomText(state.Room, ClientLang), HiringSystem.RosterText(state.RosterRoom, ClientLang));

        static string ClientLang(string key, object[] args) => Lang.Get("lotrnpcs:" + key, args);

        // 0.0.91: what still fits, in grey under the NPC's words, for a player who could hire it and after a hire.
        void Status()
        {
            string text = (HiringSystem.RoomText(state.Room, ClientLang) + " " + HiringSystem.RosterText(state.RosterRoom, ClientLang)).Trim();
            if (text.Length > 0) Emit(new RichTextComponentBase[] { new RichTextComponent(capi, text + "\r\n", Font().WithColor(GuiStyle.ColorParchment)) });
        }

        string CoinName()
        {
            if (string.IsNullOrEmpty(state.Coin)) return "";
            AssetLocation code = new AssetLocation(state.Coin);
            CollectibleObject coin = (CollectibleObject)capi.World.GetItem(code) ?? capi.World.GetBlock(code);
            return coin == null ? state.Coin : new ItemStack(coin).GetName();
        }

        static int Count(string prefix)
        {
            int n = 0;
            while (Has(prefix + (n + 1))) n++;
            return n;
        }

        // Our lines are English only so far: fall back to English in other languages, as Lang.Get does.
        static bool Has(string key) => Lang.HasTranslation(key, false, false)
            || (Lang.AvailableLanguages.TryGetValue(Lang.DefaultLocale, out ITranslationService english) && english.HasTranslation(key, false, false));

        public override void OnKeyDown(KeyEvent args)
        {
            if (args.KeyCode >= (int)GlKeys.Number1 && args.KeyCode <= (int)GlKeys.Number9)
            {
                int index = args.KeyCode - (int)GlKeys.Number1, i = 0;
                foreach (RichTextComponentBase comp in textElem.Components)
                {
                    if (!(comp is LinkTextComponent link) || !link.Clickable) continue;
                    if (i++ == index)
                    {
                        link.Trigger();
                        args.Handled = true;
                        break;
                    }
                }
            }
            base.OnKeyDown(args);
        }

        // Closes when the NPC dies or goes, or the player walks away.
        public override void OnFinalizeFrame(float dt)
        {
            base.OnFinalizeFrame(dt);
            EntityPlayer player = capi.World.Player?.Entity;
            double range = EntityBehaviorHireable.TalkRange + 1;
            if (closing || !IsOpened() || player == null) return;
            if (!npc.Alive || npc.ShouldDespawn || player.Pos.SquareDistanceTo(npc.Pos) > range * range)
            {
                closing = true;
                capi.Event.EnqueueMainThreadTask(() => TryClose(), "lotrnpcs-closetalk");
            }
        }
    }
}
