using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace LotrNpcs
{
    /// <summary>
    /// A rectangle on the map where NPCs of the listed factions spawn naturally. It covers every height; the
    /// spawn rules put NPCs on the ground surface. Corners are absolute block coordinates, both inclusive.
    /// </summary>
    public class SpawnZone
    {
        public int Id;
        public int MinX, MinZ, MaxX, MaxZ;
        public List<string> Factions = new List<string>();
        // 0.0.112: patrol points, x and z pairs (absolute block coordinates). With 2 or more, groups spawned here walk them.
        public List<int> Patrol = new List<int>();
        public bool Patrols => Patrol != null && Patrol.Count >= 4;

        public bool Contains(double x, double z) => x >= MinX && x < MaxX + 1 && z >= MinZ && z < MaxZ + 1;
    }

    /// <summary>A player the zone spawner works around: who, and where.</summary>
    public class PlayerSpot
    {
        public string Uid, Name;
        public Vec3d Pos;
        public int Dimension;
    }

    /// <summary>
    /// Tunable spawn numbers, stored in the server's ModConfig/lotrnpcs-spawning.json (rewritten on every start, so new
    /// settings appear and old ones go). User, 2026-11-02: at least one group every 3 to 5 minutes near a player in a
    /// zone; small groups as in Fantasy Creatures' surface goblins; captains very rare, with an escort.
    /// </summary>
    public class SpawnConfig
    {
        // How to edit the file (user, 2026-11-02: "add #instructions to the config"). Rewritten on every load.
        [JsonProperty("#Instructions")]
        public List<string> Instructions;
        // 0.0.94: the file gets a version so a changed default can move files that still have the old one, and leave
        // an admin's own value alone. Files from before 0.0.94 have none (0).
        public const int CurrentVersion = 1;
        public int Version;
        public double MinutesBetweenGroupsMin = 3;
        public double MinutesBetweenGroupsMax = 5;
        // 0.0.52 (user, 2026-11-03: "the chances of a group spawns within this time, not that it is actually timed"):
        // each window between those times rolls this chance; a miss means no group until the next window.
        public double GroupChance = 0.5;
        public float GroupSizeAvg = 3;
        public float GroupSizeVar = 2;
        public int GroupSizeMax = 5;
        // Chance for each group member after the first (always a recruit) to be a warrior: about 3 recruits per warrior.
        public double WarriorShare = 0.35;
        // 0.0.55 archers: the chance for each member after the first to be an archer, in factions that have them
        // (Mordor for now). Rolled before WarriorShare.
        public double ArcherShare = 0.2;
        public double CaptainGroupChance = 0.12;
        // 0.0.56 (user, 2026-11-03: "For two factions that are in the same zone and hostile, or in an overlapping zone
        // area, increase the spawn chance of enemy groups next to each other"): when a group spawns on ground that a
        // zone also gives to a faction it is at war with, the chance that a group of that enemy spawns
        // RivalDistanceMin to RivalDistanceMax blocks from it, so the two meet.
        public double RivalGroupChance = 0.5;
        public int RivalDistanceMin = 16;
        public int RivalDistanceMax = 32;
        public int CaptainEscortMin = 1;
        public int CaptainEscortMax = 3;
        public int MinDistanceToPlayer = 40;
        public int MaxDistanceToPlayer = 80;
        // 0.0.94 (owner, 2026-11-05: "For spawning within zones, increase the max natural cap by at least 3x"): 24, was 8.
        public int MaxNearby = 24;
        public const int OldMaxNearby = 8;
        public int NearbyRange = 128;
        public int MaxBlockLight = 7;
        public double DespawnAfterMinutesAway = 5;
        // 0.0.91 (owner, 2026-11-05: "have recruits that arent hired, despawn over time so areas dont get utterly
        // flooded", chose "A"): a zone NPC alive longer than MaxLifetimeMinutes disappears as soon as it isn't fighting
        // and no player is within LifetimeDespawnDistance blocks, so it never vanishes in front of anyone. 0 turns it off.
        public double MaxLifetimeMinutes = 20;
        public int LifetimeDespawnDistance = 64;
        public int DespawnDistance = 128;

        /// <summary>Brings a file from an older build up to date (0.0.94: an unchanged MaxNearby 8 becomes 24).</summary>
        public void Migrate()
        {
            if (Version < 1 && MaxNearby == OldMaxNearby) MaxNearby = 24;
            Version = CurrentVersion;
        }

        public static readonly List<string> InstructionText = new List<string>
        {
            "LOTR NPCs: natural spawning inside the zones admins mark with /recruits zone. Restart the server after editing. This #Instructions list is rewritten on every start, so don't edit it; your values are kept.",
            "MinutesBetweenGroupsMin, MinutesBetweenGroupsMax and GroupChance: for each player standing in a zone, every so many minutes (a random time between the two) a group spawns with chance GroupChance (0 to 1; 0.5 = half the time, 1 = every time).",
            "GroupSizeAvg, GroupSizeVar and GroupSizeMax: how many NPCs a group has (average, plus or minus the variation, never more than the maximum), for a faction with the baseline MaxFollowers in the faction file (5, the Men's). Other factions' groups scale with their MaxFollowers: Dunlendings up to 6, orcs up to 7, goblins up to 8, elves and dwarves up to 4.",
            "WarriorShare: the chance for each member after the first (always a recruit) to be a warrior instead of a recruit. The Shire has no warriors or captains, so its groups are all hobbit farmers.",
            "ArcherShare: the chance for each member after the first to be an archer, in factions that have archers (every faction but the Shire since 0.0.69). Archers are a unit of their own, without ranks; the rest of the group follows WarriorShare.",
            "RivalGroupChance, RivalDistanceMin and RivalDistanceMax: where zones give the same ground to two factions at war (one zone listing both, or overlapping zones), the chance that a group spawning there brings a group of the enemy faction RivalDistanceMin to RivalDistanceMax blocks away, so they fight. 0 turns it off. The rival group still respects MaxNearby, MinDistanceToPlayer and the zones.",
            "CaptainGroupChance: the chance that a group is a captain with CaptainEscortMin to CaptainEscortMax recruits, warriors and archers. CaptainEscortMax scales with the faction's MaxFollowers like the group size (orcs up to 4, elves up to 2).",
            "MinDistanceToPlayer and MaxDistanceToPlayer: how many blocks from the player a group appears. It always appears inside a zone that lists its faction.",
            "MaxNearby and NearbyRange: no group spawns while MaxNearby zone NPCs (24 by default since 0.0.94; was 8) are within NearbyRange blocks of the player. NPCs count by their faction's share, as their groups are bigger or smaller: an orc counts 5/7, a goblin 5/8, an elf or dwarf 5/4.",
            "MaxBlockLight: no group spawns where torches and other lamps make the light brighter than this (0 to 32). Daylight doesn't count.",
            "DespawnAfterMinutesAway and DespawnDistance: zone NPCs that stay farther than DespawnDistance blocks from every player for that many minutes disappear, so new groups keep coming. NPCs from creative spawn items never despawn.",
            "MaxLifetimeMinutes and LifetimeDespawnDistance: a zone NPC alive longer than MaxLifetimeMinutes (20) disappears as soon as it isn't fighting and no player is within LifetimeDespawnDistance blocks (64), so areas don't fill up while players stay nearby. 0 turns it off. Hired recruits and NPCs from creative spawn items never despawn.",
        };
    }

    /// <summary>
    /// Natural spawning inside admin-marked zones (user, 2026-11-02). Each online player has a timer: every 3 to 5
    /// minutes (config) one group of a faction listed by the zone spawns on the surface 40 to 80 blocks from them,
    /// inside a zone. Groups are recruits with some warriors, and now and then a captain with an escort. Zone NPCs that
    /// stay far from every player for 5 minutes despawn, so new groups keep coming. Zones may overlap and may list
    /// several factions. Admins manage zones with /recruits zone; zones are saved with the world. Creative spawn items
    /// are unaffected, and their NPCs never despawn.
    /// </summary>
    public class SpawnZoneSystem : ModSystem
    {
        // Every faction with ranked NPCs (0.0.35: all of them, not just the orcs). The Shire has only recruits, its
        // hobbit farmers (user, 2026-11-02: "make them recruit equivalent", 0.0.36).
        public static readonly string[] Factions = { "mordor", "angmar", "mistymountains", "gundabad", "isengard", "gondor", "rohan", "dale", "arnor", "haradrim",
            "dunlendings", "blacknumenoreans", "easterlings", "dwarves", "wickeddwarves", "elves", "tormentedelves", "shire" };
        const string SaveKey = "lotrnpcs:spawnzones";
        const string ConfigFile = "lotrnpcs-spawning.json";
        public const string OriginAttr = "lotrnpcsOrigin";
        const string AwayAttr = "lotrnpcsAwaySeconds";
        // 0.0.91: how long a zone NPC has been alive (seconds of server ticks), for MaxLifetimeMinutes.
        public const string AgeAttr = "lotrnpcsZoneAgeSeconds";
        const int TickMs = 5000;

        class SaveData
        {
            public int NextId = 1;
            public List<SpawnZone> Zones = new List<SpawnZone>();
        }

        ICoreServerAPI sapi;
        SpawnConfig config = new SpawnConfig();
        Random rand = new Random();
        // Replaced whole on every change, so spawn checks on other threads always see a complete list.
        volatile SpawnZone[] zones = Array.Empty<SpawnZone>();
        int nextId = 1;
        // Per player: when their next group is due (server elapsed milliseconds).
        readonly Dictionary<string, long> nextSpawnAt = new Dictionary<string, long>();
        readonly List<string> gone = new List<string>();
        // (faction, rank) -> entity type, read from the lotrnpcsFaction and lotrnpcsRank attributes.
        Dictionary<string, EntityProperties> typesByFactionRank;
        // Are two factions at war? From the faction config; null until first needed (and in tests without one).
        System.Func<string, string, bool> atWar;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        static SpawnConfig LoadConfig(ICoreAPI api)
        {
            SpawnConfig config = null;
            try
            {
                config = api.LoadModConfig<SpawnConfig>(ConfigFile);
            }
            catch (Exception e)
            {
                api.Logger.Error("[lotrnpcs] Could not read ModConfig/" + ConfigFile + ", using the default spawn settings: " + e.Message);
                return new SpawnConfig();
            }
            config = config ?? new SpawnConfig();
            config.Migrate();
            config.Instructions = new List<string>(SpawnConfig.InstructionText);
            // Rewrite the file so it always lists the current settings (older builds had different ones).
            api.StoreModConfig(config, ConfigFile);
            return config;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            config = LoadConfig(api);
            api.Event.SaveGameLoaded += LoadZones;
            api.Event.GameWorldSave += SaveZones;
            api.Event.OnTrySpawnEntity += AllowSpawn;
            api.Event.RegisterGameTickListener(OnTick, TickMs);
            api.Logger.Notification("[lotrnpcs] Zone spawning: every " + config.MinutesBetweenGroupsMin + " to "
                + config.MinutesBetweenGroupsMax + " minutes, a " + Math.Round(GameMath.Clamp(config.GroupChance, 0, 1) * 100) + "% chance of a group near each player in a zone (ModConfig/" + ConfigFile + ").");

            CommandArgumentParsers parsers = api.ChatCommands.Parsers;
            string factionList = string.Join(", ", Factions);
            // 0.0.89: was /recruits zone; now a branch of the mod's one command (ModCommands).
            ModCommands.RootOf(api)
                .BeginSubCommand("zone")
                .WithDescription("Admins: LOTR NPC spawn zones, rectangles where faction NPCs spawn naturally. Factions: " + factionList)
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("add")
                    .WithDescription("Add a zone between two corners (x z x z, as shown on screen) for one or more factions")
                    .WithArgs(parsers.WorldPosition2D("corner1"), parsers.WorldPosition2D("corner2"), parsers.All("factions"))
                    .HandleWith(ModCommands.AdminOnly(OnAdd))
                .EndSubCommand()
                .BeginSubCommand("list")
                    .WithDescription("List all zones")
                    .HandleWith(ModCommands.AdminOnly(OnList))
                .EndSubCommand()
                .BeginSubCommand("here")
                    .WithDescription("List the zones you are standing in")
                    .HandleWith(ModCommands.AdminOnly(OnHere))
                .EndSubCommand()
                .BeginSubCommand("remove")
                    .WithDescription("Remove a zone by its number")
                    .WithArgs(parsers.Int("zone"))
                    .HandleWith(ModCommands.AdminOnly(OnRemove))
                .EndSubCommand()
                .BeginSubCommand("addfaction")
                    .WithDescription("Add a faction to a zone")
                    .WithArgs(parsers.Int("zone"), parsers.WordRange("faction", Factions))
                    .HandleWith(ModCommands.AdminOnly(OnAddFaction))
                .EndSubCommand()
                .BeginSubCommand("removefaction")
                    .WithDescription("Remove a faction from a zone")
                    .WithArgs(parsers.Int("zone"), parsers.WordRange("faction", Factions))
                    .HandleWith(ModCommands.AdminOnly(OnRemoveFaction))
                .EndSubCommand()
                .BeginSubCommand("patrol")
                    .WithDescription("Add where you stand as the zone's next patrol point; with 2 or more, its groups walk between them in order")
                    .WithArgs(parsers.Int("zone"))
                    .HandleWith(ModCommands.AdminOnly(OnPatrol))
                .EndSubCommand()
                .BeginSubCommand("patrolclear")
                    .WithDescription("Remove a zone's patrol points (groups spawned after that wander again)")
                    .WithArgs(parsers.Int("zone"))
                    .HandleWith(ModCommands.AdminOnly(OnPatrolClear))
                .EndSubCommand()
                .BeginSubCommand("spawnnow")
                    .WithDescription("Spawn a group near you right away, as the zone timer would (for testing)")
                    .HandleWith(ModCommands.AdminOnly(OnSpawnNow))
                .EndSubCommand()
                .EndSubCommand();
        }

        // Safety net: the NPCs carry no vanilla spawn rules, but if anything else asks to spawn one naturally, only
        // allow it inside a zone of its faction. The game asks this for every natural spawn attempt of every creature,
        // possibly on the world generation thread, so it only reads the cached type and the zone array.
        bool AllowSpawn(IBlockAccessor blockAccessor, ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {
            string faction = NpcTypeInfo.Of(properties)?.Faction;
            return faction == null || InZoneOf(faction, spawnPosition.X, spawnPosition.Z);
        }

        bool InZoneOf(string faction, double x, double z)
        {
            foreach (SpawnZone zone in zones)
            {
                if (zone.Contains(x, z) && zone.Factions.Contains(faction)) return true;
            }
            return false;
        }

        // The factions of every zone at x, z that have NPCs to spawn, each once.
        List<string> SpawnableFactionsAt(double x, double z)
        {
            List<string> factions = new List<string>();
            foreach (SpawnZone zone in zones)
            {
                if (!zone.Contains(x, z)) continue;
                foreach (string f in zone.Factions)
                {
                    if (!factions.Contains(f) && typesByFactionRank.ContainsKey(f + "/recruit")) factions.Add(f);
                }
            }
            return factions;
        }

        void OnTick(float dt) => Tick(dt, OnlinePlayers());

        List<PlayerSpot> OnlinePlayers()
        {
            List<PlayerSpot> spots = new List<PlayerSpot>();
            foreach (IPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (!(player is IServerPlayer splayer) || splayer.ConnectionState != EnumClientState.Playing) continue;
                if (player.Entity == null || player.WorldData?.CurrentGameMode == EnumGameMode.Spectator) continue;
                spots.Add(new PlayerSpot { Uid = player.PlayerUID, Name = player.PlayerName, Pos = player.Entity.Pos.XYZ, Dimension = player.Entity.Pos.Dimension });
            }
            return spots;
        }

        void Tick(float dt, List<PlayerSpot> players)
        {
            long now = sapi.World.ElapsedMilliseconds;
            foreach (PlayerSpot player in players)
            {
                if (!nextSpawnAt.TryGetValue(player.Uid, out long due))
                {
                    nextSpawnAt[player.Uid] = now + NextIntervalMs();
                    continue;
                }
                if (now < due) continue;
                // The window's roll: a miss waits for the next window.
                if (rand.NextDouble() >= GameMath.Clamp(config.GroupChance, 0, 1))
                {
                    nextSpawnAt[player.Uid] = now + NextIntervalMs();
                    continue;
                }

                SpawnResult result = TrySpawnGroupNear(player);
                // A group spawned, or the area is full: wait a whole interval. Otherwise (no zone or no ground in
                // reach) try again next tick, so a player walking into a zone soon gets a group.
                if (result != SpawnResult.NoPlace) nextSpawnAt[player.Uid] = now + NextIntervalMs();
            }
            // Players who left get a fresh timer when they come back.
            gone.Clear();
            foreach (string uid in nextSpawnAt.Keys)
            {
                if (!Online(players, uid)) gone.Add(uid);
            }
            foreach (string uid in gone) nextSpawnAt.Remove(uid);

            DespawnFarZoneNpcs(dt, players);
        }

        static bool Online(List<PlayerSpot> players, string uid)
        {
            foreach (PlayerSpot player in players)
            {
                if (player.Uid == uid) return true;
            }
            return false;
        }

        long NextIntervalMs()
        {
            double min = Math.Max(0.1, config.MinutesBetweenGroupsMin);
            double max = Math.Max(min, config.MinutesBetweenGroupsMax);
            return (long)((min + rand.NextDouble() * (max - min)) * 60000);
        }

        enum SpawnResult { Spawned, Full, NoPlace }

        SpawnResult TrySpawnGroupNear(PlayerSpot player)
        {
            if (player.Dimension != 0) return SpawnResult.NoPlace;
            if (typesByFactionRank == null) typesByFactionRank = ReadTypes();
            Vec3d at = player.Pos;
            List<Entity> nearby = ZoneNpcsAround(at, config.NearbyRange);
            // 0.0.90: NPCs count by their faction's share (a goblin 5/8 of a man), as their groups are bigger.
            double nearbyWeight = nearby.Sum(e => Weight(FactionSystem.FactionOf(e)));
            if (nearbyWeight >= config.MaxNearby - 1e-6) return SpawnResult.Full;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double dist = config.MinDistanceToPlayer + rand.NextDouble() * Math.Max(0, config.MaxDistanceToPlayer - config.MinDistanceToPlayer);
                int x = (int)Math.Floor(at.X + Math.Cos(angle) * dist);
                int z = (int)Math.Floor(at.Z + Math.Sin(angle) * dist);
                List<string> factions = SpawnableFactionsAt(x + 0.5, z + 0.5);
                if (factions.Count == 0) continue;
                BlockPos feet = SpawnSpot(x, z);
                if (feet == null) continue;

                string faction = factions[rand.Next(factions.Count)];
                bool captainNearby = nearby.Any(e => NpcTypeInfo.Of(e)?.Rank == "captain");
                bool captainGroup = !captainNearby && typesByFactionRank.ContainsKey(faction + "/captain") && rand.NextDouble() < config.CaptainGroupChance;
                // A faction without warriors (the Shire) sends recruits in their place.
                List<string> ranks = GroupRanks(captainGroup, typesByFactionRank.ContainsKey(faction + "/archer"), faction).Select(r => typesByFactionRank.ContainsKey(faction + "/" + r) ? r : "recruit").ToList();
                SpawnGroup(faction, ranks, feet);
                Vec3d middle = sapi.World.DefaultSpawnPosition.XYZ;
                sapi.Logger.Notification("[lotrnpcs] Zone spawn: " + faction + " " + string.Join(", ", ranks) + " at "
                    + (feet.X - (int)middle.X) + " " + (feet.Z - (int)middle.Z) + ", " + (int)dist + " blocks from " + player.Name);
                if (nearbyWeight + ranks.Count * Weight(faction) < config.MaxNearby && rand.NextDouble() < GameMath.Clamp(config.RivalGroupChance, 0, 1))
                    TrySpawnRivalGroup(player, faction, feet, captainNearby || captainGroup);
                return SpawnResult.Spawned;
            }
            return SpawnResult.NoPlace;
        }

        /// <summary>Of the factions a zone gives this ground to, those at war with the faction.</summary>
        public static List<string> RivalsAmong(IEnumerable<string> factionsHere, string faction, System.Func<string, string, bool> atWar) =>
            atWar == null ? new List<string>() : factionsHere.Where(f => f != faction && atWar(faction, f)).ToList();

        System.Func<string, string, bool> AtWar()
        {
            if (atWar != null) return atWar;
            FactionSystem factions = sapi.ModLoader?.GetModSystem<FactionSystem>();
            return factions == null ? null : atWar = (a, b) => factions.Relation(a, b) == FactionRelation.Enemy;
        }

        // A group of a faction at war with the one just spawned at feet, 16 to 32 blocks from it (config), on ground a
        // zone gives to that enemy and no closer to the player than any group may spawn.
        void TrySpawnRivalGroup(PlayerSpot player, string faction, BlockPos feet, bool captainAround)
        {
            System.Func<string, string, bool> war = AtWar();
            if (war == null || RivalsAmong(SpawnableFactionsAt(feet.X + 0.5, feet.Z + 0.5), faction, war).Count == 0) return;
            int min = Math.Max(4, config.RivalDistanceMin), max = Math.Max(min, config.RivalDistanceMax);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double dist = min + rand.NextDouble() * (max - min);
                int x = (int)Math.Floor(feet.X + 0.5 + Math.Cos(angle) * dist);
                int z = (int)Math.Floor(feet.Z + 0.5 + Math.Sin(angle) * dist);
                double dx = x + 0.5 - player.Pos.X, dz = z + 0.5 - player.Pos.Z;
                if (dx * dx + dz * dz < (double)config.MinDistanceToPlayer * config.MinDistanceToPlayer) continue;
                List<string> rivals = RivalsAmong(SpawnableFactionsAt(x + 0.5, z + 0.5), faction, war);
                if (rivals.Count == 0) continue;
                BlockPos spot = SpawnSpot(x, z);
                if (spot == null) continue;
                string rival = rivals[rand.Next(rivals.Count)];
                bool captainGroup = !captainAround && typesByFactionRank.ContainsKey(rival + "/captain") && rand.NextDouble() < config.CaptainGroupChance;
                List<string> ranks = GroupRanks(captainGroup, typesByFactionRank.ContainsKey(rival + "/archer"), rival).Select(r => typesByFactionRank.ContainsKey(rival + "/" + r) ? r : "recruit").ToList();
                SpawnGroup(rival, ranks, spot);
                Vec3d middle = sapi.World.DefaultSpawnPosition.XYZ;
                sapi.Logger.Notification("[lotrnpcs] Zone spawn: " + rival + " " + string.Join(", ", ranks) + " at "
                    + (spot.X - (int)middle.X) + " " + (spot.Z - (int)middle.Z) + ", rivals of the " + faction + " group " + (int)dist + " blocks away");
                return;
            }
        }

        List<string> GroupRanks(bool captainGroup, bool archers, string faction) => GroupRanks(config, rand, captainGroup, archers, GroupScale(faction));

        // 0.0.90 (owner chose "A": wild groups scale with the follow limit): a faction's group size over the baseline's
        // (its MaxFollowers over the file's baseline; orcs 6/5 since 0.0.107, elves 4/5), and what one of its NPCs counts for in
        // MaxNearby (1 over that).
        double GroupScale(string faction)
        {
            if (groupScale == null)
            {
                FactionSystem factions = sapi?.ModLoader?.GetModSystem<FactionSystem>();
                groupScale = f => factions?.Config.GroupScaleOf(f) ?? 1;
            }
            return Math.Max(0.05, groupScale(faction));
        }
        // Looked up once, like atWar; tests set it.
        System.Func<string, double> groupScale;
        double Weight(string faction) => 1 / GroupScale(faction);

        /// <summary>The units of one group: a captain and escort, or a recruit and the rest by ArcherShare (when the
        /// faction has archers) and WarriorShare.</summary>
        public static List<string> GroupRanks(SpawnConfig config, Random rand, bool captainGroup, bool archers, double scale = 1)
        {
            string Member(double warriorShare)
            {
                if (archers && rand.NextDouble() < config.ArcherShare) return "archer";
                return rand.NextDouble() < warriorShare ? "warrior" : "recruit";
            }
            List<string> ranks = new List<string>();
            if (captainGroup)
            {
                ranks.Add("captain");
                // 0.0.90: the escort's most grows with the faction (Men 1 to 3, orcs 1 to 4, elves 1 to 2).
                int escortMax = Math.Max(config.CaptainEscortMin, FactionTiers.Round(config.CaptainEscortMax * scale));
                int escort = rand.Next(Math.Max(0, config.CaptainEscortMin), escortMax + 1);
                for (int i = 0; i < escort; i++) ranks.Add(Member(1 / 3.0));
                return ranks;
            }
            // Bell curve over avg +- var, rounded up or down at random by its fraction, as vanilla does for herds.
            // 0.0.90: average, variation and most scaled with the faction (Men 1 to 5, orcs 1 to 7, goblins 1 to 8, elves 1 to 4).
            float size = new NatFloat((float)(config.GroupSizeAvg * scale), (float)(config.GroupSizeVar * scale), EnumDistribution.GAUSSIAN).nextFloat(1, rand);
            int count = (int)size + (size - (int)size > rand.NextDouble() ? 1 : 0);
            count = GameMath.Clamp(count, 1, Math.Max(1, FactionTiers.Round(config.GroupSizeMax * scale)));
            ranks.Add("recruit");
            for (int i = 1; i < count; i++) ranks.Add(Member(config.WarriorShare));
            return ranks;
        }

        // The block an NPC can stand in at the surface of x, z: solid dry ground below (not water, not tree tops),
        // room for its body, and no torch light (block light above the limit). Grass, flowers and thin snow on top
        // are fine. Null when there is none or the chunk isn't loaded.
        BlockPos SpawnSpot(int x, int z)
        {
            IBlockAccessor ba = sapi.World.BlockAccessor;
            BlockPos pos = new BlockPos(x, 0, z, 0);
            if (ba.GetMapChunkAtBlockPos(pos) == null) return null;
            pos.Y = ba.GetRainMapHeightAt(x, z);
            if (pos.Y <= 1 || ba.GetChunkAtBlockPos(pos) == null) return null;
            // The height map can stop on a plant or a snow layer; step down to the ground under it.
            for (int i = 0; i < 3 && pos.Y > 1 && Passable(ba.GetBlock(pos)); i++) pos.Y--;
            Block ground = ba.GetBlock(pos);
            if (Passable(ground) || ground.IsLiquid() || ground.BlockMaterial == EnumBlockMaterial.Leaves || !ground.SideSolid[BlockFacing.UP.Index]) return null;
            BlockPos feet = pos.UpCopy();
            if (!Passable(ba.GetBlock(feet)) || !Passable(ba.GetBlock(feet.UpCopy()))) return null;
            if (ba.GetLightLevel(feet, EnumLightLevelType.OnlyBlockLight) > config.MaxBlockLight) return null;
            return feet;
        }

        // Air, or something a body fits through: no liquid and no collision box, or a single snow layer.
        static bool Passable(Block block)
        {
            if (block.Id == 0) return true;
            if (block.IsLiquid()) return false;
            return block.CollisionBoxes == null || block.CollisionBoxes.Length == 0 || block.Code?.Path == "snowlayer-1";
        }

        void SpawnGroup(string faction, List<string> ranks, BlockPos leaderFeet)
        {
            long herdId = sapi.WorldManager.GetNextUniqueId();
            // 0.0.112: in a zone of this faction with a patrol, the whole group starts for the point nearest the leader.
            SpawnZone patrol = zones.FirstOrDefault(z => z.Patrols && z.Factions.Contains(faction) && z.Contains(leaderFeet.X + 0.5, leaderFeet.Z + 0.5));
            int start = patrol == null ? 0 : NearestPoint(patrol.Patrol, leaderFeet.X, leaderFeet.Z);
            for (int i = 0; i < ranks.Count; i++)
            {
                BlockPos feet = leaderFeet;
                if (i > 0)
                {
                    for (int tries = 0; tries < 6; tries++)
                    {
                        BlockPos near = SpawnSpot(leaderFeet.X + rand.Next(-3, 4), leaderFeet.Z + rand.Next(-3, 4));
                        if (near != null && Math.Abs(near.Y - leaderFeet.Y) <= 3 && InZoneOf(faction, near.X + 0.5, near.Z + 0.5)) { feet = near; break; }
                    }
                }
                Entity entity = sapi.World.ClassRegistry.CreateEntity(typesByFactionRank[faction + "/" + ranks[i]]);
                entity.Pos.SetPosWithDimension(new Vec3d(feet.X + 0.5, feet.Y + 0.01, feet.Z + 0.5));
                entity.Pos.SetYaw((float)rand.NextDouble() * GameMath.TWOPI);
                entity.PositionBeforeFalling.Set(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
                entity.Attributes.SetString(OriginAttr, "zone");
                if (patrol != null)
                {
                    entity.Attributes[AiTaskZonePatrol.RouteAttr] = new IntArrayAttribute(patrol.Patrol.ToArray());
                    entity.Attributes.SetInt(AiTaskZonePatrol.NextAttr, start);
                }
                if (entity is EntityAgent agent) agent.HerdId = herdId;
                sapi.World.SpawnEntity(entity);
            }
        }

        Dictionary<string, EntityProperties> ReadTypes()
        {
            Dictionary<string, EntityProperties> types = new Dictionary<string, EntityProperties>();
            foreach (EntityProperties type in sapi.World.EntityTypes)
            {
                NpcTypeInfo info = NpcTypeInfo.Of(type);
                if (info?.Faction != null && info.Rank != null) types[info.Faction + "/" + info.Rank] = type;
            }
            return types;
        }

        List<Entity> ZoneNpcsAround(Vec3d at, int range)
        {
            List<Entity> found = new List<Entity>();
            // The loaded entities directly: their Values property copies the whole table first.
            foreach (KeyValuePair<long, Entity> pair in sapi.World.LoadedEntities)
            {
                Entity entity = pair.Value;
                // Recruits don't count: a player leading some still gets groups to fight.
                if (!entity.Alive || NpcTypeInfo.Of(entity)?.Faction == null || Recruit.OwnerUidOf(entity) != null) continue;
                if (entity.Pos.Dimension == 0 && entity.Pos.HorDistanceTo(at) <= range) found.Add(entity);
            }
            return found;
        }

        // Zone NPCs that no player has come within DespawnDistance of for DespawnAfterMinutesAway disappear, like
        // vanilla wildlife, so an area doesn't fill up with groups nobody fights; since 0.0.91 also those older than
        // MaxLifetimeMinutes that aren't fighting and have no player within LifetimeDespawnDistance. Creative-spawned
        // NPCs and recruits stay.
        void DespawnFarZoneNpcs(float dt, List<PlayerSpot> players)
        {
            // Safe while NPCs die: the table tolerates changes while it's walked, and Die only marks them.
            foreach (KeyValuePair<long, Entity> pair in sapi.World.LoadedEntities)
            {
                Entity entity = pair.Value;
                if (!entity.Alive || Attrs.String(entity.Attributes, OriginAttr) != "zone") continue;
                float away = AnyPlayerNear(players, entity, config.DespawnDistance) ? 0 : entity.Attributes.GetFloat(AwayAttr) + dt;
                entity.Attributes.SetFloat(AwayAttr, away);
                float age = entity.Attributes.GetFloat(AgeAttr) + dt;
                entity.Attributes.SetFloat(AgeAttr, age);
                if (away >= config.DespawnAfterMinutesAway * 60) entity.Die(EnumDespawnReason.Expire);
                // 0.0.91 lifetime: old, not fighting, and out of every player's sight range.
                else if (config.MaxLifetimeMinutes > 0 && age >= config.MaxLifetimeMinutes * 60
                    && !AnyPlayerNear(players, entity, Math.Max(0, config.LifetimeDespawnDistance)) && !Fighting(entity))
                    entity.Die(EnumDespawnReason.Expire);
            }
        }

        // Is it in a fight (seek or melee in the last 3 seconds, or hurt)? Entities without a world (tests) never are.
        bool Fighting(Entity entity) =>
            entity.World != null && entity.GetBehavior<EntityBehaviorFactionMember>()?.Fighting(sapi.World.ElapsedMilliseconds) == true;

        bool AnyPlayerNear(List<PlayerSpot> players, Entity entity, double range)
        {
            foreach (PlayerSpot player in players)
            {
                if (player.Dimension == entity.Pos.Dimension && entity.Pos.HorDistanceTo(player.Pos) <= range) return true;
            }
            return false;
        }

        TextCommandResult OnSpawnNow(TextCommandCallingArgs args)
        {
            IPlayer player = args.Caller.Player;
            if (player?.Entity == null) return TextCommandResult.Error("Only a player in the world can use this.");
            SpawnResult result = TrySpawnGroupNear(new PlayerSpot { Uid = player.PlayerUID, Name = player.PlayerName, Pos = player.Entity.Pos.XYZ, Dimension = player.Entity.Pos.Dimension });
            if (result == SpawnResult.Full) return TextCommandResult.Error("There are already zone NPCs worth " + config.MaxNearby + " or more within " + config.NearbyRange + " blocks (an orc counts 5/7, a goblin 5/8, an elf or dwarf 5/4).");
            if (result == SpawnResult.NoPlace) return TextCommandResult.Error("No spot " + config.MinDistanceToPlayer + " to " + config.MaxDistanceToPlayer + " blocks from you is inside a zone, on dry ground and away from torches.");
            nextSpawnAt[player.PlayerUID] = sapi.World.ElapsedMilliseconds + NextIntervalMs();
            return TextCommandResult.Success("Spawned a group. See the server log for where.");
        }

        TextCommandResult OnAdd(TextCommandCallingArgs args)
        {
            Vec2i a = (Vec2i)args[0];
            Vec2i b = (Vec2i)args[1];
            List<string> factions = new List<string>();
            foreach (string word in ((string)args[2]).Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string faction = word.ToLowerInvariant();
                if (!Factions.Contains(faction)) return TextCommandResult.Error("Unknown faction '" + word + "'. Factions: " + string.Join(", ", Factions));
                if (!factions.Contains(faction)) factions.Add(faction);
            }
            if (factions.Count == 0) return TextCommandResult.Error("Name at least one faction: " + string.Join(", ", Factions));

            SpawnZone zone = new SpawnZone
            {
                Id = nextId++,
                MinX = Math.Min(a.X, b.X), MaxX = Math.Max(a.X, b.X),
                MinZ = Math.Min(a.Y, b.Y), MaxZ = Math.Max(a.Y, b.Y),
                Factions = factions,
            };
            zones = zones.Append(zone).ToArray();
            SaveZones();
            return TextCommandResult.Success("Added " + Describe(zone));
        }

        TextCommandResult OnList(TextCommandCallingArgs args)
        {
            SpawnZone[] all = zones;
            if (all.Length == 0) return TextCommandResult.Success("No spawn zones yet. Add one with /recruits zone add x z x z faction.");
            return TextCommandResult.Success(all.Length + " spawn zones:\n" + string.Join("\n", all.Select(Describe)));
        }

        TextCommandResult OnHere(TextCommandCallingArgs args)
        {
            Vec3d pos = args.Caller.Pos;
            if (pos == null) return TextCommandResult.Error("This command needs a position.");
            SpawnZone[] inside = zones.Where(z => z.Contains(pos.X, pos.Z)).ToArray();
            if (inside.Length == 0) return TextCommandResult.Success("You are not in any spawn zone.");
            return TextCommandResult.Success("You are in:\n" + string.Join("\n", inside.Select(Describe)));
        }

        TextCommandResult OnRemove(TextCommandCallingArgs args)
        {
            int id = (int)args[0];
            SpawnZone zone = zones.FirstOrDefault(z => z.Id == id);
            if (zone == null) return TextCommandResult.Error("There is no zone " + id + ". See /recruits zone list.");
            zones = zones.Where(z => z.Id != id).ToArray();
            SaveZones();
            return TextCommandResult.Success("Removed " + Describe(zone));
        }

        TextCommandResult OnAddFaction(TextCommandCallingArgs args) => ChangeFaction((int)args[0], (string)args[1], true);

        TextCommandResult OnRemoveFaction(TextCommandCallingArgs args) => ChangeFaction((int)args[0], (string)args[1], false);

        TextCommandResult ChangeFaction(int id, string faction, bool add)
        {
            SpawnZone zone = zones.FirstOrDefault(z => z.Id == id);
            if (zone == null) return TextCommandResult.Error("There is no zone " + id + ". See /recruits zone list.");

            List<string> factions = new List<string>(zone.Factions);
            if (add && !factions.Contains(faction)) factions.Add(faction);
            if (!add) factions.Remove(faction);
            if (factions.Count == 0) return TextCommandResult.Error("A zone needs at least one faction. Remove the zone instead.");

            return Replace(zone, factions, zone.Patrol);
        }

        // Zones are replaced whole (see zones), never changed in place.
        TextCommandResult Replace(SpawnZone zone, List<string> factions, List<int> patrol)
        {
            SpawnZone changed = new SpawnZone { Id = zone.Id, MinX = zone.MinX, MinZ = zone.MinZ, MaxX = zone.MaxX, MaxZ = zone.MaxZ, Factions = factions, Patrol = patrol };
            zones = zones.Select(z => z.Id == zone.Id ? changed : z).ToArray();
            SaveZones();
            return TextCommandResult.Success("Now " + Describe(changed));
        }

        TextCommandResult OnPatrol(TextCommandCallingArgs args)
        {
            int id = (int)args[0];
            SpawnZone zone = zones.FirstOrDefault(z => z.Id == id);
            if (zone == null) return TextCommandResult.Error("There is no zone " + id + ". See /recruits zone list.");
            Vec3d pos = args.Caller.Pos;
            if (pos == null) return TextCommandResult.Error("This command needs a position.");
            return Replace(zone, zone.Factions, new List<int>(zone.Patrol) { (int)Math.Floor(pos.X), (int)Math.Floor(pos.Z) });
        }

        TextCommandResult OnPatrolClear(TextCommandCallingArgs args)
        {
            int id = (int)args[0];
            SpawnZone zone = zones.FirstOrDefault(z => z.Id == id);
            if (zone == null) return TextCommandResult.Error("There is no zone " + id + ". See /recruits zone list.");
            return Replace(zone, zone.Factions, new List<int>());
        }

        /// <summary>The index of the patrol point (x, z pairs) nearest to x, z.</summary>
        public static int NearestPoint(List<int> patrol, double x, double z)
        {
            int best = 0;
            double bestD = double.MaxValue;
            for (int i = 0; i + 1 < patrol.Count; i += 2)
            {
                double d = (patrol[i] - x) * (patrol[i] - x) + (patrol[i + 1] - z) * (patrol[i + 1] - z);
                if (d < bestD) { bestD = d; best = i / 2; }
            }
            return best;
        }

        // Coordinates as the player sees them on screen: relative to the map middle, like /tp takes them.
        string Describe(SpawnZone zone)
        {
            Vec3d middle = sapi.World.DefaultSpawnPosition.XYZ;
            int ox = (int)middle.X, oz = (int)middle.Z;
            return "zone " + zone.Id + ": " + string.Join(", ", zone.Factions) + ", from " + (zone.MinX - ox) + " " + (zone.MinZ - oz)
                + " to " + (zone.MaxX - ox) + " " + (zone.MaxZ - oz) + " (" + (zone.MaxX - zone.MinX + 1) + " x " + (zone.MaxZ - zone.MinZ + 1) + " blocks)"
                + (zone.Patrol.Count >= 2 ? ", patrol " + string.Join(" > ", Enumerable.Range(0, zone.Patrol.Count / 2).Select(i => (zone.Patrol[2 * i] - ox) + " " + (zone.Patrol[2 * i + 1] - oz)))
                    + (zone.Patrols ? "" : " (add a second point to start it)") : "");
        }

        void LoadZones()
        {
            byte[] bytes = sapi.WorldManager.SaveGame.GetData(SaveKey);
            if (bytes == null) return;
            try
            {
                SaveData data = JsonConvert.DeserializeObject<SaveData>(Encoding.UTF8.GetString(bytes));
                zones = Clean(data?.Zones);
                nextId = Math.Max(data?.NextId ?? 1, zones.Select(z => z.Id + 1).DefaultIfEmpty(1).Max());
                sapi.Logger.Notification("[lotrnpcs] Loaded " + zones.Length + " spawn zones.");
            }
            catch (Exception e)
            {
                sapi.Logger.Error("[lotrnpcs] Could not read the saved spawn zones: " + e.Message);
            }
        }

        // Saved zones as the spawner expects them: no empty entries, faction names in lower case and listed once, and
        // corners in order. A hand-edited or damaged save must never stop the server.
        static SpawnZone[] Clean(List<SpawnZone> saved)
        {
            List<SpawnZone> clean = new List<SpawnZone>();
            foreach (SpawnZone zone in saved ?? new List<SpawnZone>())
            {
                if (zone == null) continue;
                List<string> factions = new List<string>();
                foreach (string f in zone.Factions ?? new List<string>())
                {
                    string faction = f?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(faction) && !factions.Contains(faction)) factions.Add(faction);
                }
                clean.Add(new SpawnZone
                {
                    Id = zone.Id,
                    MinX = Math.Min(zone.MinX, zone.MaxX), MaxX = Math.Max(zone.MinX, zone.MaxX),
                    MinZ = Math.Min(zone.MinZ, zone.MaxZ), MaxZ = Math.Max(zone.MinZ, zone.MaxZ),
                    Factions = factions,
                    // A damaged save's odd number keeps only whole pairs.
                    Patrol = (zone.Patrol ?? new List<int>()).Take((zone.Patrol?.Count ?? 0) / 2 * 2).ToList(),
                });
            }
            return clean.ToArray();
        }

        void SaveZones()
        {
            SaveData data = new SaveData { NextId = nextId, Zones = zones.ToList() };
            sapi.WorldManager.SaveGame.StoreData(SaveKey, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data)));
        }
    }
}
