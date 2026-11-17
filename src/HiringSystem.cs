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
    /// Hiring (user, 2026-11-02): right-clicking a faction NPC opens a talk menu like the traders'. Members of its
    /// faction can hire it for the faction's HirePrices in HireCoin, up to MaxRecruits each. Recruits follow, hold
    /// or guard (orders and stances: Orders.cs), fight whatever would attack their owner and whoever hurts the owner or
    /// them, and wild NPCs treat them the way they treat the owner. Owners keep a whitelist of players their recruits
    /// never attack on sight (/recruits whitelist). Server only; the menu itself is GuiDialogTalk on the client.
    /// </summary>
    public partial class HiringSystem : ModSystem
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
        // 0.0.136 (owner: "I also want to add retreat and charge commands"): recruit entity id -> charge (until when, and
        // since when no enemy has been within ChargeQuietRange, 0 while one is). In memory only, like disengageUntil.
        readonly Dictionary<long, (long Until, long QuietSince)> charging = new Dictionary<long, (long, long)>();
        // The same ids for the battle lines (BattleLineSystem.SideOf is static): charging recruits form no lines.
        static readonly HashSet<long> chargingIds = new HashSet<long>();
        public static bool ChargingId(long id) { lock (chargingIds) return chargingIds.Contains(id); }
        /// <summary>A charge lasts at most ChargeMs and ends ChargeQuietMs after the last enemy within ChargeQuietRange of
        /// the recruit; the target is looked for ChargeLookRange blocks along the owner's view, then ChargeNearPoint
        /// blocks around the point looked at, then ChargeNearOwner blocks around the owner.</summary>
        public const long ChargeMs = 30000, ChargeQuietMs = 5000;
        public const double ChargeQuietRange = 16, ChargeLookRange = 48, ChargeNearPoint = 8, ChargeNearOwner = 24;
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

        /// <summary>0.0.138: does a band fit? Members are (share, is a recruit). Up to extra recruits ride free in the
        /// recruit-only places (the biggest shares first); every other member uses its share of the ranked places.</summary>
        public static bool BandFits(IEnumerable<(double Share, bool Recruit)> members, int extra) => Over(BandUsed(members, extra)) == false;

        /// <summary>The ranked places a band uses (see BandFits).</summary>
        public static double BandUsed(IEnumerable<(double Share, bool Recruit)> members, int extra)
        {
            var list = members.ToList();
            return list.Where(m => !m.Recruit).Sum(m => m.Share) + list.Where(m => m.Recruit).Select(m => m.Share).OrderByDescending(x => x).Skip(Math.Max(0, extra)).Sum();
        }

        int RecruitExtra => Factions?.Config.RecruitExtra ?? FactionConfig.DefaultRecruitExtraFollowers;
        (double Share, bool Recruit) Member(Entity e) => (FollowShare(FactionSystem.FactionOf(e)), Promotion.BaseRank(RankOf(e)) == "recruit");

        /// <summary>0.0.138: would candidate fit the player's following (its followers but except, plus candidate)?</summary>
        public bool FollowFits(string uid, Entity candidate, Entity except = null) =>
            BandFits(Followers(uid).Where(e => e != except && e != candidate).Append(candidate).Select(Member), RecruitExtra);
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
            double used = BandUsed(following.Select(Member), RecruitExtra);
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
            (owner.Summaries ??= new Dictionary<long, RecruitSummary>())[npc.EntityId] = RecruitSummary.Of(npc);
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
            if (rank != null && (prices.TryGetValue(rank.ToLowerInvariant(), out int price) || prices.TryGetValue(Promotion.PriceRank(rank.ToLowerInvariant()), out price))) return Math.Max(0, price);
            return prices.TryGetValue("recruit", out int recruit) ? Math.Max(0, recruit) : 0;
        }

        public static string RankOf(Entity npc) => NpcTypeInfo.Of(npc)?.Rank;
    }
}
