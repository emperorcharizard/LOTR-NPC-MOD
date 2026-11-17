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
    /// Who NPC factions fight: players by faction membership (the join menu, or race and /group with
    /// RaceAndGroupMembership), other factions by the config's Relations (no entry: enemies), and monsters. Allies
    /// never fight and help each other when attacked. A player is safe from their own factions, and from those
    /// factions' allies only where an admin set ShareFriends on the alliance.
    /// The AI tasks ask WantsToAttack and RecruitWantsToAttack; NPCs carry their faction in the lotrnpcsFaction
    /// entity type attribute. Everything here runs on the server's main thread.
    /// </summary>
    public partial class FactionSystem : ModSystem
    {
        public const string ConfigFile = "lotrnpcs-factions.json";
        public const string AssignedKey = "lotrnpcs:playerfactions";
        /// <summary>How far past their area guards chase something before they give up and go back.</summary>
        public const double GuardLeash = 8;
        // How long a player's side (PlayerSide) is trusted before it is worked out again, so a /group change shows soon.
        const long SideCacheMs = 1000;

        ICoreServerAPI sapi;
        public FactionConfig Config { get; private set; } = new FactionConfig();
        /// <summary>Who is fighting whom, to spread battles out (Battle.cs).</summary>
        public readonly BattleSpacing Spacing = new BattleSpacing();
        /// <summary>The battle log (BattleLog.cs), off until an admin turns it on.</summary>
        public readonly BattleLog Log = new BattleLog();
        BattleLineSystem lines;
        /// <summary>Battle lines (BattleLines.cs).</summary>
        public BattleLineSystem Lines => lines ??= new BattleLineSystem(this);
        AssetLocation[] monsterCodes = Array.Empty<AssetLocation>();
        // Bumped on every SetConfig, so cached monster answers from an older list are worked out again.
        static int stampCounter;
        int monsterStamp = -1;
        HashSet<string> aggressive = new HashSet<string>();
        // faction -> other faction -> how they treat each other, for every pair the config knows or names; worked out
        // once per config, since the AI asks on every target check.
        Dictionary<string, Dictionary<string, FactionRelation>> relations = new Dictionary<string, Dictionary<string, FactionRelation>>();
        // Pairs ("a|b", both orders) whose alliance shares friends.
        HashSet<string> sharedFriends = new HashSet<string>();
        // faction -> itself and its allies, worked out once per config (the AI asks many times a second).
        Dictionary<string, string[]> alliesOf = new Dictionary<string, string[]>();
        // faction -> itself and the allies whose friends it spares (ShareFriends).
        Dictionary<string, string[]> friendsFrom = new Dictionary<string, string[]>();
        // faction -> attacker entity id -> until when (server elapsed ms) its NPCs and their allies fight back.
        readonly Dictionary<string, Dictionary<long, long>> grudges = new Dictionary<string, Dictionary<long, long>>();
        // Player UID -> the faction an admin set with /recruits faction set, saved with the world.
        Dictionary<string, AssignedPlayer> assigned = new Dictionary<string, AssignedPlayer>();
        // Players' sides as last worked out: by entity while they play (no UID lookup on the hot path), and by UID
        // from their recruits' owner record while they don't. Kept apart, so a player judged both ways (an owner who
        // is also a target) doesn't keep replacing one with the other.
        readonly Dictionary<EntityPlayer, PlayerSide> sides = new Dictionary<EntityPlayer, PlayerSide>(ReferenceEqualityComparer.Instance);
        readonly Dictionary<string, PlayerSide> recordSides = new Dictionary<string, PlayerSide>();
        static readonly object NoRecord = new object();

        /// <summary>A player as the factions see them: race, groups, admin-set faction, the factions they are a friend
        /// of, and every faction allied to one of those. Worked out from their entity while they are online, else from
        /// their recruits' owner record.</summary>
        sealed class PlayerSide
        {
            public string Race, Assigned;
            public string[] Groups;
            public readonly HashSet<string> FriendOf = new HashSet<string>(), AlliedTo = new HashSet<string>();
            // Whose side it is, and what it was worked out from, to notice when that changes.
            public string Uid;
            public object Source;
            public string RawRace;
            public object GroupsSource;
            public long Until;
        }

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        /// <summary>Owners of recruits and their grudges. Set by StartServerSide, or by tests.</summary>
        public HiringSystem Hiring { get; set; }

        /// <summary>Clock for tests, used when no server is attached.</summary>
        public long NowForTests;
        long Now => sapi?.World.ElapsedMilliseconds ?? NowForTests;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            Hiring = api.ModLoader.GetModSystem<HiringSystem>();
            LoadConfig();
            api.Event.SaveGameLoaded += LoadAssigned;
            api.Event.GameWorldSave += SaveAssigned;
            api.Event.PlayerDisconnect += player => ForgetSide(player.PlayerUID);
            api.Event.RegisterGameTickListener(dt => Lines.Update(api.World, api.World.ElapsedMilliseconds), (int)BattleLineSystem.UpdateMs);
            api.Event.RegisterGameTickListener(dt => Log.Flush(), 5000);
            api.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, () => Log.Stop());
            CommandArgumentParsers parsers = api.ChatCommands.Parsers;
            // A branch of the mod's one command (ModCommands).
            ModCommands.RootOf(api)
                .BeginSubCommand("battlelog")
                .WithDescription("Admins: battle log for testing. on starts it (a CSV file in the Logs folder), off stops it, no argument shows the totals so far")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(parsers.OptionalWord("on|off"))
                .HandleWith(ModCommands.DebugOnly(api, args => TextCommandResult.Success(BattleLogCommand(args[0] as string))))
                .EndSubCommand();
            ModCommands.RootOf(api)
                .BeginSubCommand("faction")
                .WithDescription("Admins: LOTR NPC factions, who they treat as friends and enemies")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("check")
                    .WithDescription("Show which factions treat a player as a friend, and which attack them on sight")
                    .WithArgs(parsers.OptionalWord("player"))
                    .HandleWith(ModCommands.AdminOnly(OnCheck))
                .EndSubCommand()
                .BeginSubCommand("set")
                    .WithDescription("Make a player a member of a faction (replacing the one they picked)")
                    .WithArgs(parsers.Word("player"), parsers.Word("faction"))
                    .HandleWith(ModCommands.AdminOnly(OnSet))
                .EndSubCommand()
                .BeginSubCommand("clear")
                    .WithDescription("Take a player out of their faction, so they can pick again with /recruits join")
                    .WithArgs(parsers.Word("player"))
                    .HandleWith(ModCommands.AdminOnly(OnClear))
                .EndSubCommand()
                .BeginSubCommand("members")
                    .WithDescription("List the members of a faction (or of every faction), picked or set by an admin")
                    .WithArgs(parsers.OptionalWord("faction"))
                    .HandleWith(ModCommands.AdminOnly(OnMembers))
                .EndSubCommand()
                .BeginSubCommand("list")
                    .WithDescription("List the factions with their stance, friendly races and groups, and who is allied")
                    .HandleWith(ModCommands.AdminOnly(args => TextCommandResult.Success(DescribeFactions())))
                .EndSubCommand()
                .BeginSubCommand("reload")
                    .WithDescription("Reload ModConfig/" + ConfigFile)
                    .HandleWith(ModCommands.AdminOnly(args => { LoadConfig(); int synced = AfterReload(); return TextCommandResult.Success("Reloaded " + ConfigFile + ". " + synced + " loaded NPCs took their new health.\n" + DescribeFactions()); }))
                .EndSubCommand()
                .EndSubCommand();
        }

        string BattleLogCommand(string arg)
        {
            long now = sapi.World.ElapsedMilliseconds;
            switch (arg?.Trim().ToLowerInvariant())
            {
                case "on":
                    string folder = null;
                    try { folder = sapi.GetOrCreateDataPath("Logs"); } catch (Exception) { }
                    Log.Start(folder, now);
                    return "Battle log on" + (Log.FilePath != null ? ", writing " + Log.FilePath : " (no file could be opened; totals only)") + ". NPC hits ignore the hurt window: " + (Config.NpcHitsIgnoreHurtWindow ? "yes" : "no") + ".";
                case "off":
                    Log.Stop();
                    return Log.Describe(now);
                case null:
                case "":
                    return Log.Describe(now);
                default:
                    return "Use /recruits battlelog on, off, or no argument for the totals.";
            }
        }

        public void LoadConfig()
        {
            FactionConfig loaded;
            try
            {
                loaded = sapi.LoadModConfig<FactionConfig>(ConfigFile);
            }
            catch (Exception e)
            {
                sapi.Logger.Error("[lotrnpcs] Could not read ModConfig/" + ConfigFile + ", using the default factions until it is fixed: " + e.Message);
                SetConfig(new FactionConfig());
                return;
            }
            FactionConfig config = FactionConfig.FromFile(loaded, out bool store);
            SetConfig(config);
            if (store) sapi.StoreModConfig(config, ConfigFile);
            sapi.Logger.Notification("[lotrnpcs] Factions from " + ConfigFile + ": " + string.Join(", ", Config.Factions.Keys) + ".");
        }

        /// <summary>After /recruits faction reload: loaded NPCs take their faction's new health at once (damage is
        /// read at each attack), and online players over new limits lose the newest recruits. Returns how many NPCs
        /// changed health.</summary>
        public int AfterReload()
        {
            int synced = 0;
            foreach (Entity e in sapi.World.LoadedEntities.Values)
            {
                if (e.Alive && e.GetBehavior<EntityBehaviorFactionMember>() != null && EntityBehaviorFactionMember.SyncMaxHealth(e, EntityBehaviorFactionMember.ScaledMaxHealth(e, Config))) synced++;
            }
            Hiring?.EnforceLimitsOnline();
            return synced;
        }

        void Warn(string message) => sapi?.Logger.Warning("[lotrnpcs] " + ConfigFile + ": " + message);

        /// <summary>Uses this config: ids, races and groups in lower case, defaults filled in, and the tables the AI
        /// reads worked out. A file with mistakes (an id twice, empty entries) is used as far as it makes sense, with
        /// a warning in the server log. Public for tests.</summary>
        public void SetConfig(FactionConfig config)
        {
            config.FillDefaults();
            Dictionary<string, FactionDef> factions = new Dictionary<string, FactionDef>();
            foreach (KeyValuePair<string, FactionDef> kv in config.Factions)
            {
                string id = kv.Key?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(id)) continue;
                if (factions.ContainsKey(id)) { Warn("faction " + id + " is listed twice; the first entry counts."); continue; }
                FactionDef def = kv.Value ?? new FactionDef();
                def.Stance = (def.Stance ?? "aggressive").Trim().ToLowerInvariant();
                if (def.Stance != "aggressive" && def.Stance != "defensive") Warn("faction " + id + " has the unknown stance \"" + def.Stance + "\", so it fights only when attacked.");
                def.FriendlyRaces = Clean(def.FriendlyRaces);
                def.FriendlyGroups = Clean(def.FriendlyGroups);
                factions[id] = def;
            }
            config.Factions = factions;
            config.FillDefaults();
            config.Relations = config.Relations.Where(r => r != null).ToList();
            config.MonsterCodes = config.MonsterCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
            Spacing.MaxAttackers = config.MaxAttackersPerTarget;
            Spacing.Spacing = config.CombatSpacing;
            Spacing.SetDuel(config.DuelDistanceMin, config.DuelDistanceMax);
            config.DuelDistanceMin = Spacing.DuelMin;
            config.DuelDistanceMax = Spacing.DuelMax;
            config.LineHoldRange = double.IsNaN(config.LineHoldRange) ? 6 : GameMath.Clamp(config.LineHoldRange, 2, 24);
            Lines.Enabled = config.BattleLines;
            BattleLineSystem.WildLines = config.WildBattleLines;
            Lines.Gap = Spacing.Spacing;
            Lines.HoldRange = config.LineHoldRange;
            Lines.PerEnemy = Spacing.MaxAttackers;
            Config = config;

            monsterCodes = config.MonsterCodes.Select(c => new AssetLocation(c)).ToArray();
            monsterStamp = ++stampCounter;
            aggressive = new HashSet<string>(factions.Where(kv => kv.Value.Stance == "aggressive").Select(kv => kv.Key));

            // Relations: the first entry for a pair counts. Pairs of known factions without an entry are enemies.
            relations = new Dictionary<string, Dictionary<string, FactionRelation>>();
            sharedFriends = new HashSet<string>();
            void SetPair(string a, string b, FactionRelation rel)
            {
                if (!relations.TryGetValue(a, out Dictionary<string, FactionRelation> row)) relations[a] = row = new Dictionary<string, FactionRelation>();
                row[b] = rel;
            }
            foreach (FactionRelationDef r in config.Relations)
            {
                string a = r.A?.Trim().ToLowerInvariant(), b = r.B?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                {
                    Warn("a relation entry is missing A or B, so it is ignored.");
                    continue;
                }
                if (a == b || (relations.TryGetValue(a, out Dictionary<string, FactionRelation> known) && known.ContainsKey(b))) continue;
                FactionRelation rel;
                switch (r.Relation?.Trim().ToLowerInvariant())
                {
                    case "ally": rel = FactionRelation.Ally; break;
                    case "enemy": rel = FactionRelation.Enemy; break;
                    default: rel = FactionRelation.Neutral; break;
                }
                SetPair(a, b, rel);
                SetPair(b, a, rel);
                if (rel == FactionRelation.Ally && r.ShareFriends) { sharedFriends.Add(a + "|" + b); sharedFriends.Add(b + "|" + a); }
            }
            foreach (string a in factions.Keys)
            {
                foreach (string b in factions.Keys)
                {
                    if (a != b && !(relations.TryGetValue(a, out Dictionary<string, FactionRelation> row) && row.ContainsKey(b))) SetPair(a, b, FactionRelation.Enemy);
                }
            }

            alliesOf = factions.Keys.ToDictionary(f => f, f => new[] { f }.Concat(factions.Keys.Where(o => o != f && Relation(f, o) == FactionRelation.Ally)).ToArray());
            friendsFrom = factions.Keys.ToDictionary(f => f, f => alliesOf[f].Where(o => o == f || SharesFriends(f, o)).ToArray());
            sides.Clear();
            recordSides.Clear();
        }

        // Non-empty entries, trimmed and in lower case, each once.
        static List<string> Clean(List<string> list) =>
            (list ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim().ToLowerInvariant()).Distinct().ToList();

        public static string FactionOf(Entity entity) => NpcTypeInfo.Of(entity)?.Faction;

        public FactionRelation Relation(string a, string b)
        {
            if (a == null || b == null) return FactionRelation.Neutral;
            if (a == b) return FactionRelation.Ally;
            // Factions the config doesn't know and no entry names are left alone.
            return relations.TryGetValue(a, out Dictionary<string, FactionRelation> row) && row.TryGetValue(b, out FactionRelation rel) ? rel : FactionRelation.Neutral;
        }

        /// <summary>Do these allies spare each other's friends? Only if an admin set ShareFriends on them.</summary>
        public bool SharesFriends(string a, string b) => (a != null && a == b) || (a != null && b != null && sharedFriends.Contains(a + "|" + b));

        /// <summary>The faction and every faction allied to it.</summary>
        public string[] WithAllies(string faction) =>
            faction == null ? Array.Empty<string>() : alliesOf.TryGetValue(faction, out string[] allies) ? allies : new[] { faction };

        public static string RaceOf(EntityPlayer player) => player.WatchedAttributes.GetString("skinModel")?.ToLowerInvariant();

        public static List<string> GroupsOf(EntityPlayer player)
        {
            List<string> groups = new List<string>();
            foreach (PlayerGroupMembership g in player.Player?.Groups ?? Array.Empty<PlayerGroupMembership>())
            {
                if (g != null && g.Level != EnumPlayerGroupMemberShip.None && g.GroupName != null) groups.Add(g.GroupName.ToLowerInvariant());
            }
            return groups;
        }

        /// <summary>Is a player with this race, groups and admin-set faction (null: none) a member of the faction,
        /// directly or through an ally that shares friends? An admin-set faction replaces the race.</summary>
        public bool IsFriend(string faction, string race, ICollection<string> groups, string assignedFaction = null)
        {
            if (faction == null) return false;
            foreach (string f in friendsFrom.TryGetValue(faction, out string[] sources) ? sources : new[] { faction })
            {
                if (!Config.Factions.TryGetValue(f, out FactionDef def)) continue;
                if (assignedFaction == f) return true;
                // Race and groups count only with RaceAndGroupMembership.
                if (!Config.RaceAndGroupMembership) continue;
                if (assignedFaction == null && race != null && def.FriendlyRaces.Contains(race)) return true;
                if (groups == null) continue;
                foreach (string g in groups)
                {
                    if (def.FriendlyGroups.Contains(g)) return true;
                }
            }
            return false;
        }

        public bool IsFriend(string faction, EntityPlayer player) => faction != null && SideOf(player).FriendOf.Contains(faction);
    }
}
