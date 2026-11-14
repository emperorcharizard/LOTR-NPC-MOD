using System; using Fn = System.Func<System.Reflection.MethodInfo, object[], object>;
using System.Collections.Concurrent; using System.Collections.Generic; using System.IO; using System.Linq; using System.Reflection; using System.Text;
using Newtonsoft.Json.Linq; using Vintagestory.API.Common; using Vintagestory.API.Common.Entities; using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools; using Vintagestory.API.Server; using Vintagestory.API.Util; using Vintagestory.ServerMods.NoObf; using LotrNpcs;

public class Fake : DispatchProxy {
    public Dictionary<string, Fn> H;
    protected override object Invoke(MethodInfo m, object[] a) {
        if (H.TryGetValue(m.Name, out var f)) return f(m, a);
        if (m.ReturnType == typeof(void)) return null;
        throw new NotImplementedException(m.DeclaringType.Name + "." + m.Name);
    }
    public static T Make<T>(Dictionary<string, Fn> h) { T p = Create<T, Fake>(); ((Fake)(object)p).H = h; return p; }
}
public class TestAgent : EntityAgent { public void SetProps(EntityProperties p) => Properties = p; }

public static class Program {
    static int fails = 0;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static List<string> log = new List<string>();
    const int Mid = 512000, Ground = 100;

    public static int Main(string[] argv) {
        // --- generated entity JSON ---
        var types = new List<EntityProperties>();
        int withSpawn = 0;
        foreach (string file in Directory.GetFiles(argv[0], "*.json").OrderBy(f => f)) {
            JObject jo = JObject.Parse(File.ReadAllText(file));
            if (jo["server"]?["spawnconditions"] != null) withSpawn++;
            jo.Remove("sounds"); jo.Remove("tags");
            EntityType et = jo.ToObject<EntityType>(JsonUtil.CreateSerializerForDomain("lotrnpcs"));
            types.Add(new EntityProperties { Code = new AssetLocation("lotrnpcs", (string)jo["code"]), Attributes = et.Attributes,
                Server = new EntityServerProperties(et.Server.Behaviors, et.BehaviorConfigs) { SpawnConditions = et.Server.SpawnConditions } });
        }
        EntityProperties T(string code) => types.First(t => t.Code.Path == code);
        Check(withSpawn == 0, "no vanilla spawn rules in the JSON any more");
        bool Branch(EntityProperties t) => Array.IndexOf(Promotion.Branches, t.Attributes["lotrnpcsRank"].AsString()) >= 0;
        bool Rider(EntityProperties t) => t.Attributes["lotrnpcsRank"].AsString() == "rider";
        Check(types.Count(t => t.Attributes?["lotrnpcsFaction"].Exists == true && t.Attributes["lotrnpcsRank"].Exists && !Branch(t) && !Rider(t)) == 69
            && types.Count(t => t.Attributes?["lotrnpcsFaction"].Exists == true && t.Attributes["lotrnpcsRank"].Exists && Branch(t)) == 34
            && types.Count(t => t.Attributes?["lotrnpcsFaction"].Exists == true && Rider(t)) == 10,
            "69 zone NPC types (17 factions x 3 ranks with Gundabad, the Shire's hobbit recruits, and archers for every faction but the Shire), plus 0.0.113's guards and shock troops for the 17 armed factions and 0.0.117's riders for the 10 mounted ones");
        var rankedFactions = types.Where(t => t.Attributes?["lotrnpcsRank"].Exists == true).Select(t => t.Attributes["lotrnpcsFaction"].AsString()).Distinct().OrderBy(f => f).ToList();
        Check(rankedFactions.SequenceEqual(SpawnZoneSystem.Factions.OrderBy(f => f)) && SpawnZoneSystem.Factions.All(f => (f == "shire" ? new[] { "recruit" } : new[] { "recruit", "warrior", "captain", "archer" }).All(r => types.Any(t => t.Attributes?["lotrnpcsFaction"].AsString() == f && t.Attributes["lotrnpcsRank"].AsString() == r))), "every faction /lotrzone accepts has a recruit, warrior and captain (the Shire only recruits), and no other faction has ranks");
        Check(T("test-hobbit").Attributes["lotrnpcsFaction"].AsString() == "shire" && T("test-hobbit").Attributes["lotrnpcsRank"].AsString() == "recruit", "hobbits are the Shire's recruits");
        var defaults = FactionConfig.DefaultFactions();
        Check(SpawnZoneSystem.Factions.All(defaults.ContainsKey) && types.All(t => t.Attributes?["lotrnpcsFaction"].Exists != true || defaults.ContainsKey(t.Attributes["lotrnpcsFaction"].AsString())), "zone factions and NPC factions are all in the default faction config");
        Check(T("test-goblin-captain").Attributes["lotrnpcsFaction"].AsString() == "mistymountains" && T("test-goblin-captain").Attributes["lotrnpcsRank"].AsString() == "captain", "attributes read back");

        // --- fake server ---
        long now = 0, nextEntityId = 1, uid = 1000;
        var loaded = new ConcurrentDictionary<long, Entity>();
        System.Func<int, int, int> lightAt = (x, z) => 0;            // block light
        System.Func<int, int, string> groundAt = (x, z) => "soil";   // soil | water | leaves | none
        var soil = new Block { BlockId = 1, BlockMaterial = EnumBlockMaterial.Soil };
        var leaves = new Block { BlockId = 2, BlockMaterial = EnumBlockMaterial.Leaves };
        var water = new Block { BlockId = 3, BlockMaterial = EnumBlockMaterial.Water, MatterState = EnumMatterState.Liquid };
        var air = new Block { BlockId = 0, BlockMaterial = EnumBlockMaterial.Air };
        var grass = new Block { BlockId = 4, BlockMaterial = EnumBlockMaterial.Plant, CollisionBoxes = null, Code = new AssetLocation("game:tallgrass-tall-free") };
        var snow1 = new Block { BlockId = 5, BlockMaterial = EnumBlockMaterial.Snow, Code = new AssetLocation("game:snowlayer-1") };
        Check(!soil.IsLiquid() && water.IsLiquid() && soil.SideSolid[BlockFacing.UP.Index], "harness blocks behave (soil solid, water liquid)");
        var mapChunk = Fake.Make<IMapChunk>(new());
        var chunk = Fake.Make<IWorldChunk>(new());
        var ba = Fake.Make<IBlockAccessor>(new() {
            ["GetMapChunkAtBlockPos"] = (m, a) => groundAt(((BlockPos)a[0]).X, ((BlockPos)a[0]).Z) == "none" ? null : mapChunk,
            ["GetRainMapHeightAt"] = (m, a) => a.Length == 2 ? Ground : throw new Exception("overload"),
            ["GetChunkAtBlockPos"] = (m, a) => chunk,
            ["GetBlock"] = (m, a) => { var p = (BlockPos)a[0]; var g = groundAt(p.X, p.Z);
                if (g == "grass" || g == "snow") { if (p.Y == Ground) return g == "grass" ? grass : snow1; if (p.Y == Ground - 1) return soil; return air; }
                if (g == "torch2") { if (p.Y == Ground + 1) return soil; }
                if (p.Y != Ground) return air; return g == "water" ? water : g == "leaves" ? leaves : soil; },
            ["GetLightLevel"] = (m, a) => { var p = (BlockPos)a[0]; return lightAt(p.X, p.Z); } });
        var logger = Fake.Make<ILogger>(new() { ["Notification"] = (m, a) => { log.Add("N " + a[0]); return null; }, ["Error"] = (m, a) => { log.Add("E " + a[0]); return null; } });
        var classReg = Fake.Make<IClassRegistryAPI>(new() { ["CreateEntity"] = (m, a) => { if (a[0] is EntityProperties p) { var e = new TestAgent(); e.SetProps(p); return e; } throw new Exception("CreateEntity overload"); } });
        var world = Fake.Make<IServerWorldAccessor>(new() {
            ["get_EntityTypes"] = (m, a) => types, ["get_DefaultSpawnPosition"] = (m, a) => new EntityPos(Mid, 110, Mid),
            ["get_ElapsedMilliseconds"] = (m, a) => now,
            // 0.0.117: no mount mods in the test world, so no riders spawn.
            ["GetEntityType"] = (m, a) => types.FirstOrDefault(t => t.Code.Equals(a[0])),
            ["get_LoadedEntities"] = (m, a) => loaded, ["get_BlockAccessor"] = (m, a) => ba, ["get_ClassRegistry"] = (m, a) => classReg,
            ["SpawnEntity"] = (m, a) => { var e = (Entity)a[0]; e.EntityId = nextEntityId++; loaded[e.EntityId] = e; return null; } });
        var save = new Dictionary<string, byte[]>();
        var saveGame = Fake.Make<ISaveGame>(new() { ["GetData"] = (m, a) => save.TryGetValue((string)a[0], out var b) ? b : null, ["StoreData"] = (m, a) => { save[(string)a[0]] = (byte[])a[1]; return null; } });
        var wm = Fake.Make<IWorldManagerAPI>(new() { ["get_SaveGame"] = (m, a) => saveGame, ["GetNextUniqueId"] = (m, a) => uid++ });
        SpawnConfig cfgFile = null; object stored = null; bool throwOnLoad = false;
        Action<float> tick = null;
        var events = Fake.Make<IServerEventAPI>(new() { ["RegisterGameTickListener"] = (m, a) => { if (a[0] is Action<float> f) tick = f; return 1L; } });
        var cmds = Fake.Make<IChatCommandApi>(new());
        var sapi = Fake.Make<ICoreServerAPI>(new() {
            ["get_World"] = (m, a) => world, ["get_Logger"] = (m, a) => logger, ["get_WorldManager"] = (m, a) => wm, ["get_Event"] = (m, a) => events,
            ["get_ChatCommands"] = (m, a) => cmds,
            ["LoadModConfig"] = (m, a) => throwOnLoad ? throw new Exception("bad json") : cfgFile, ["StoreModConfig"] = (m, a) => { stored = a[0]; return null; } });

        // StartServerSide needs the chat command builder; skip it and wire the pieces it sets up by hand.
        var sys = new SpawnZoneSystem();
        var F = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(SpawnZoneSystem).GetField("sapi", F).SetValue(sys, sapi);
        var loadConfig = typeof(SpawnZoneSystem).GetMethod("LoadConfig", BindingFlags.Static | BindingFlags.NonPublic);
        SpawnConfig cfg = (SpawnConfig)loadConfig.Invoke(null, new object[] { sapi });
        Check(stored is SpawnConfig && cfg.MinutesBetweenGroupsMin == 3 && cfg.MinutesBetweenGroupsMax == 5, "no config file: defaults (3 to 5 minutes) get written");
        string cfgJson = Newtonsoft.Json.JsonConvert.SerializeObject(stored, Newtonsoft.Json.Formatting.Indented);
        Check(cfgJson.TrimStart('{', ' ', '\r', '\n').StartsWith("\"#Instructions\"") && cfg.Instructions.Count == SpawnConfig.InstructionText.Count, "the file starts with #Instructions");
        var editedFile = JObject.Parse(cfgJson); editedFile["#Instructions"] = new JArray("old text"); editedFile["MaxNearby"] = 6;
        cfgFile = editedFile.ToObject<SpawnConfig>(); cfg = (SpawnConfig)loadConfig.Invoke(null, new object[] { sapi });
        Check(cfg.Instructions.SequenceEqual(SpawnConfig.InstructionText) && cfg.MaxNearby == 6 && stored == cfg, "#Instructions are rewritten on load, the admin's values stay");
        // the user's 0.0.29 file
        cfgFile = JObject.Parse("{\"RecruitGroupChance\":0.0015,\"RecruitGroupMaxQuantity\":4,\"CaptainChance\":0.0002,\"MinDistanceToPlayer\":40,\"MaxBlockLight\":7}").ToObject<SpawnConfig>();
        stored = null; cfg = (SpawnConfig)loadConfig.Invoke(null, new object[] { sapi });
        Check(stored == cfg && cfg.MinutesBetweenGroupsMax == 5 && cfg.MaxNearby == 24 && cfg.Version == SpawnConfig.CurrentVersion, "0.0.29 config file is rewritten with the new settings (MaxNearby 24 since 0.0.94)");
        // 0.0.94 (owner: "increase the max natural cap by at least 3x"): an unversioned file with the old 8 moves to 24;
        // another value stays; a versioned file's 8 is the admin's and stays.
        cfgFile = JObject.Parse("{\"MaxNearby\":8,\"GroupChance\":0.5}").ToObject<SpawnConfig>(); cfg = (SpawnConfig)loadConfig.Invoke(null, new object[] { sapi });
        bool moved = cfg.MaxNearby == 24 && cfg.Version == 1;
        cfgFile = JObject.Parse("{\"MaxNearby\":10}").ToObject<SpawnConfig>(); cfg = (SpawnConfig)loadConfig.Invoke(null, new object[] { sapi });
        bool kept = cfg.MaxNearby == 10;
        cfgFile = JObject.Parse("{\"Version\":1,\"MaxNearby\":8}").ToObject<SpawnConfig>(); cfg = (SpawnConfig)loadConfig.Invoke(null, new object[] { sapi });
        Check(moved && kept && cfg.MaxNearby == 8 && new SpawnConfig().MaxNearby == 24 && SpawnConfig.InstructionText.Any(l => l.StartsWith("MaxNearby") && l.Contains("24")),
            "spawning file version 1: an old file's MaxNearby 8 becomes 24, other values and a versioned 8 stay, #Instructions say 24");
        throwOnLoad = true; cfg = (SpawnConfig)loadConfig.Invoke(null, new object[] { sapi }); throwOnLoad = false;
        Check(cfg.MinutesBetweenGroupsMin == 3 && log.Any(l => l.StartsWith("E ") && l.Contains("Could not read")), "broken config: logged, defaults used");
        Check(new SpawnConfig().GroupChance == 0.5 && SpawnConfig.InstructionText.Any(l => l.Contains("GroupChance")), "each window spawns a group half the time by default, and #Instructions explain it");
        // The timing checks below need every window to spawn.
        typeof(SpawnZoneSystem).GetField("config", F).SetValue(sys, cfg = new SpawnConfig { GroupChance = 1 });
        var seeded = new Random(12345); typeof(SpawnZoneSystem).GetField("rand", F).SetValue(sys, seeded);
        MethodInfo Priv(string n) => typeof(SpawnZoneSystem).GetMethod(n, F);
        // The faction config's default wars: everyone fights everyone, except Mordor and Isengard (allies).
        System.Func<string, string, bool> defaultWars = (a, b) => a != b && !((a == "mordor" && b == "isengard") || (a == "isengard" && b == "mordor"));
        typeof(SpawnZoneSystem).GetField("atWar", F).SetValue(sys, defaultWars);
        // 0.0.90 wild group scaling: the older checks below use the baseline (every faction scale 1); its own block at
        // the end uses the shipped faction config.
        typeof(SpawnZoneSystem).GetField("groupScale", F).SetValue(sys, (System.Func<string, double>)(f => 1));

        var parsers = new CommandArgumentParsers(sapi);
        TextCommandResult Add(string raw, Vec3d callerPos = null) {
            var args = new TextCommandCallingArgs { Caller = new Caller { Type = EnumCallerType.Player, Pos = callerPos ?? new Vec3d(Mid, 120, Mid) }, RawArgs = new CmdArgs(raw) };
            foreach (var p in new ICommandArgumentParser[] { parsers.WorldPosition2D("c1"), parsers.WorldPosition2D("c2"), parsers.All("f") }) { args.Parsers.Add(p); p.PreProcess(args); if (p.TryProcess(args) != EnumParseResult.Good) return TextCommandResult.Error("parse"); }
            return (TextCommandResult)Priv("OnAdd").Invoke(sys, new object[] { args });
        }

        // players
        var players = new List<PlayerSpot>();
        PlayerSpot MakePlayer(string name, double relX, double relZ) => new PlayerSpot { Uid = "uid-" + name, Name = name, Pos = new Vec3d(Mid + relX, Ground + 1, Mid + relZ) };
        var me = MakePlayer("Emperorcharizar", 0, 0); players.Add(me);
        var tickM = Priv("Tick");
        List<TestAgent> Zone() => loaded.Values.OfType<TestAgent>().Where(e => e.Alive).ToList();
        void Step(double seconds) { for (double s = 0; s < seconds; s += 5) { now += 5000; tickM.Invoke(sys, new object[] { 5f, players }); } }
        void KillAll() { foreach (var e in Zone()) e.Die(EnumDespawnReason.Removed); }

        // --- no zone: nothing ---
        Step(600);
        Check(Zone().Count == 0, "no zones: nothing spawns in 10 minutes");

        // --- zone around the player ---
        Check(Add("-150 -150 150 150 mordor").Status == EnumCommandStatus.Success, "zone added around the player");
        // timer was due long ago (player outside zones), so the next tick spawns
        Step(5);
        Check(Zone().Count >= 1, "overdue timer: a group spawns on the next tick after a zone appears (" + Zone().Count + ")");
        KillAll();
        // now measure the intervals over 2 hours, killing each group as it arrives
        var spawnTimes = new List<double>(); int groups = 0, captains = 0, members = 0; var sizes = new List<int>();
        bool allInZone = true, allInRing = true, factionOk = true; int maxCaptainsAlive = 0;
        for (int i = 0; i < 2 * 60 * 60 / 5; i++) {
            Step(5);
            var alive = Zone();
            if (alive.Count > 0) {
                groups++; spawnTimes.Add(now / 60000.0); sizes.Add(alive.Count); members += alive.Count;
                captains += alive.Count(e => e.Properties.Attributes["lotrnpcsRank"].AsString() == "captain");
                maxCaptainsAlive = Math.Max(maxCaptainsAlive, alive.Count(e => e.Properties.Attributes["lotrnpcsRank"].AsString() == "captain"));
                foreach (var e in alive) {
                    double rx = e.Pos.X - Mid, rz = e.Pos.Z - Mid, d = Math.Sqrt(rx * rx + rz * rz);
                    if (Math.Abs(rx) > 151 || Math.Abs(rz) > 151) allInZone = false;
                    if (d < 40 - 5 || d > 80 + 5) allInRing = false;
                    if (e.Properties.Attributes["lotrnpcsFaction"].AsString() != "mordor") factionOk = false;
                    if (e.Attributes.GetString("lotrnpcsOrigin") != "zone") factionOk = false;
                }
                KillAll();
            }
        }
        var gaps = spawnTimes.Zip(spawnTimes.Skip(1), (a, b) => b - a).ToList();
        Console.WriteLine($"   > 2 hours: {groups} groups, gaps {gaps.Min():F2}-{gaps.Max():F2} min (avg {gaps.Average():F2}), sizes {string.Join(",", sizes.GroupBy(s => s).OrderBy(g => g.Key).Select(g => g.Key + "x" + g.Count()))}, captains {captains}");
        Check(gaps.Min() >= 3 && gaps.Max() <= 5 + 5 / 60.0 + 0.001, "a group every 3 to 5 minutes");
        Check(groups >= 24 && groups <= 40, "about 30 groups in 2 hours: " + groups);
        Check(sizes.All(s => s >= 1 && s <= 5) && sizes.Distinct().Count() >= 3, "group sizes 1 to 5, varied");
        Check(allInZone && allInRing && factionOk, "every NPC inside the zone, 40 to 80 blocks away, mordor, marked as zone-spawned");
        Check(captains >= 1 && captains <= 10 && maxCaptainsAlive <= 1, "captains rare (" + captains + " in " + groups + " groups), never two at once");
        Check(log.Any(l => l.Contains("[lotrnpcs] Zone spawn: mordor")), "spawns are logged: " + log.Last(l => l.Contains("Zone spawn")));

        // --- composition over many groups ---
        var spawnNow = Priv("TrySpawnGroupNear");
        int rec = 0, war = 0, cap = 0, arc = 0, capGroups = 0, capEscortOk = 1;
        for (int i = 0; i < 2000; i++) {
            spawnNow.Invoke(sys, new object[] { me });
            var g = Zone();
            int c = g.Count(e => e.Properties.Attributes["lotrnpcsRank"].AsString() == "captain");
            if (c > 0) { capGroups++; if (g.Count < 2 || g.Count > 4 || c != 1) capEscortOk = 0; }
            rec += g.Count(e => e.Properties.Attributes["lotrnpcsRank"].AsString() == "recruit");
            war += g.Count(e => e.Properties.Attributes["lotrnpcsRank"].AsString() == "warrior"); cap += c;
            arc += g.Count(e => e.Properties.Attributes["lotrnpcsRank"].AsString() == "archer");
            KillAll();
        }
        Console.WriteLine($"   > 2000 groups: {rec} recruits, {war} warriors, {arc} archers, {cap} captains in {capGroups} captain groups");
        Check(capGroups > 160 && capGroups < 320, "about 12% of groups have a captain");
        Check(capEscortOk == 1, "captain groups: 1 captain with 1 to 3 escorts");
        Check(rec > 2.5 * war && rec < 4.5 * war, "about 3 recruits per warrior");
        Check(arc > 0.1 * (rec + war + arc + cap) && arc < 0.25 * (rec + war + arc + cap), "0.0.55: Mordor groups bring archers too (" + arc + ")");

        // --- cap --- (the mechanism, at the pre-0.0.94 cap of 8; the default is 24 since 0.0.94)
        cfg.MaxNearby = 8;
        for (int i = 0; i < 10; i++) spawnNow.Invoke(sys, new object[] { me });
        int capped = Zone().Count;
        Check(capped >= 8 && capped <= 12, "spawning stops once 8 or more are near (" + capped + ")");
        var r = spawnNow.Invoke(sys, new object[] { me }).ToString();
        Check(r == "Full" && Zone().Count == capped, "full area: no group");
        KillAll();

        // --- ground rules ---
        groundAt = (x, z) => "water"; Check(spawnNow.Invoke(sys, new object[] { me }).ToString() == "NoPlace" && Zone().Count == 0, "no spawning on water");
        groundAt = (x, z) => "leaves"; Check(spawnNow.Invoke(sys, new object[] { me }).ToString() == "NoPlace", "no spawning on tree tops");
        groundAt = (x, z) => "none"; Check(spawnNow.Invoke(sys, new object[] { me }).ToString() == "NoPlace", "no spawning in unloaded chunks");
        groundAt = (x, z) => "grass";
        Check(spawnNow.Invoke(sys, new object[] { me }).ToString() == "Spawned" && Zone().All(e => Math.Abs(e.Pos.Y - (Ground + 0.01)) < 0.001), "grass on top: they stand in the grass on the soil below"); KillAll();
        groundAt = (x, z) => "snow";
        Check(spawnNow.Invoke(sys, new object[] { me }).ToString() == "Spawned" && Zone().All(e => Math.Abs(e.Pos.Y - (Ground + 0.01)) < 0.001), "thin snow on top: they spawn in it"); KillAll();
        groundAt = (x, z) => "torch2";
        Check(spawnNow.Invoke(sys, new object[] { me }).ToString() == "NoPlace", "no spawning with a solid block above the ground (no room)");
        groundAt = (x, z) => "soil"; lightAt = (x, z) => 8; Check(spawnNow.Invoke(sys, new object[] { me }).ToString() == "NoPlace", "no spawning in torch light above 7");
        lightAt = (x, z) => x > Mid ? 14 : 0;
        for (int i = 0; i < 50; i++) { spawnNow.Invoke(sys, new object[] { me }); foreach (var e in Zone()) if (e.Pos.X > Mid + 4) Check(false, "spawned in a lit area"); KillAll(); }
        lightAt = (x, z) => 0;

        // --- outside zone, overlap, factions ---
        Check(Add("200 -150 400 150 angmar,isengard").Status == EnumCommandStatus.Success, "second zone (angmar, isengard)");
        Check(Add("-150 -150 400 150 mistymountains").Status == EnumCommandStatus.Success, "overlapping zone (mistymountains)");
        var far = MakePlayer("Far", 180, 0);
        var seen = new HashSet<string>();
        for (int i = 0; i < 300; i++) { spawnNow.Invoke(sys, new object[] { far }); foreach (var e in Zone()) { seen.Add(e.Properties.Attributes["lotrnpcsFaction"].AsString()); if (e.Pos.X - Mid > 151 && e.Properties.Attributes["lotrnpcsFaction"].AsString() == "mordor") Check(false, "mordor outside its zone"); } KillAll(); }
        Check(seen.SetEquals(new[] { "mordor", "angmar", "isengard", "mistymountains" }), "overlapping zones: all their factions spawn (" + string.Join(",", seen) + ")");

        // --- 0.0.56: enemies sharing ground spawn near each other ---
        Check(new SpawnConfig().RivalGroupChance == 0.5 && new SpawnConfig().RivalDistanceMin == 16 && new SpawnConfig().RivalDistanceMax == 32 && SpawnConfig.InstructionText.Any(l => l.StartsWith("RivalGroupChance")), "0.0.56 rival group settings: defaults and #Instructions");
        Check(SpawnZoneSystem.RivalsAmong(new[] { "mordor", "isengard", "mistymountains", "gondor" }, "mordor", defaultWars).SequenceEqual(new[] { "mistymountains", "gondor" }) && SpawnZoneSystem.RivalsAmong(new[] { "mordor" }, "mordor", defaultWars).Count == 0 && SpawnZoneSystem.RivalsAmong(new[] { "gondor" }, "mordor", null).Count == 0, "rivals: the factions here at war with it, never itself or an ally");
        int pairs = 0, singles = 0, badPair = 0; double closest = 999, farthest = 0;
        for (int i = 0; i < 400; i++) {
            spawnNow.Invoke(sys, new object[] { me });
            var g = Zone(); var fs = g.Select(e => e.Properties.Attributes["lotrnpcsFaction"].AsString()).Distinct().ToList();
            if (fs.Count == 2) {
                pairs++;
                if (!defaultWars(fs[0], fs[1])) badPair++;
                // Leaders: the first of each faction to spawn.
                var a = g.First(e => e.Properties.Attributes["lotrnpcsFaction"].AsString() == fs[0]); var b = g.First(e => e.Properties.Attributes["lotrnpcsFaction"].AsString() == fs[1]);
                double d = a.Pos.HorDistanceTo(b.Pos.XYZ); closest = Math.Min(closest, d); farthest = Math.Max(farthest, d);
                if (g.Any(e => e.Pos.HorDistanceTo(me.Pos) < 40 - 4)) badPair++;
            } else if (fs.Count == 1) singles++; else badPair++;
            KillAll();
        }
        Console.WriteLine($"   > mordor + misty mountains ground, 400 spawns: {pairs} with rivals, {singles} alone, leaders {closest:0.0} to {farthest:0.0} blocks apart");
        Check(pairs > 400 * 0.38 && pairs < 400 * 0.62 && badPair == 0, "enemies sharing ground: about half the groups bring an enemy group, never an ally, never near the player");
        Check(closest >= 15 && farthest <= 33.5, "the rival group's leader spawns 16 to 32 blocks from the first group's");
        typeof(SpawnZoneSystem).GetField("atWar", F).SetValue(sys, (System.Func<string, string, bool>)((a, b) => false));
        int peaceful = 0; for (int i = 0; i < 100; i++) { spawnNow.Invoke(sys, new object[] { me }); if (Zone().Select(e => e.Properties.Attributes["lotrnpcsFaction"].AsString()).Distinct().Count() > 1) peaceful++; KillAll(); }
        Check(peaceful == 0, "factions at peace never spawn as rivals");
        typeof(SpawnZoneSystem).GetField("atWar", F).SetValue(sys, defaultWars);
        cfg.RivalGroupChance = 0; int off = 0; for (int i = 0; i < 100; i++) { spawnNow.Invoke(sys, new object[] { me }); if (Zone().Select(e => e.Properties.Attributes["lotrnpcsFaction"].AsString()).Distinct().Count() > 1) off++; KillAll(); }
        Check(off == 0, "RivalGroupChance 0 turns it off"); cfg.RivalGroupChance = 0.5;
        Check(Add("-3000 -3000 -2800 -2800 elves tormentedelves gondor").Status == EnumCommandStatus.Success, "a zone for the new factions (elves, tormented elves, gondor)");
        Check(Add("-3000 -3000 -2800 -2800 rivendell").Status == EnumCommandStatus.Error, "unknown factions can't get a zone");
        Check(Add("5800 5800 6200 6200 shire").Status == EnumCommandStatus.Success, "a Shire zone");
        var shirePlayer = MakePlayer("Shire", 6000, 6000); int hobbits = 0; bool onlyHobbits = true;
        for (int i = 0; i < 300; i++) { spawnNow.Invoke(sys, new object[] { shirePlayer }); foreach (var e in Zone()) { hobbits++; if (e.Properties.Code.Path != "test-hobbit") onlyHobbits = false; } KillAll(); }
        Check(hobbits > 300 && onlyHobbits, "Shire zones spawn groups of hobbits only, no warriors or captains (" + hobbits + " in 300 groups)");
        var west = MakePlayer("West", -2900, -2900); var seenWest = new HashSet<string>();
        for (int i = 0; i < 200; i++) { spawnNow.Invoke(sys, new object[] { west }); foreach (var e in Zone()) seenWest.Add(e.Properties.Code.Path.Replace("-recruit", "").Replace("-warrior", "").Replace("-captain", "").Replace("-archer", "")); KillAll(); }
        Check(seenWest.SetEquals(new[] { "test-elf", "test-tormentedelf", "test-gondor" }), "new factions spawn in their zone (" + string.Join(",", seenWest) + ")");
        var outside = MakePlayer("Outside", 5000, 5000);
        Check(spawnNow.Invoke(sys, new object[] { outside }).ToString() == "NoPlace", "player far from every zone: no group");
        var edge = MakePlayer("Edge", -200, 0);  // 50 blocks west of zone 1's edge: spawns only on the zone side
        for (int i = 0; i < 50; i++) { spawnNow.Invoke(sys, new object[] { edge }); foreach (var e in Zone()) if (e.Pos.X - Mid < -151) Check(false, "spawned outside the zone"); KillAll(); }
        Check(true, "player near a zone edge: groups only inside the zone");


        // --- despawn ---
        spawnNow.Invoke(sys, new object[] { me });
        var group = Zone(); int gcount = group.Count;
        var creative = new TestAgent(); creative.SetProps(T("test-mordororc-recruit")); creative.Pos.SetPos(Mid + 1000, Ground + 1, Mid); world.SpawnEntity(creative);
        Step(10 * 60);   // player stays near: nothing despawns (timer spawns more, fine)
        Check(group.All(e => e.Alive) && creative.Alive, "zone NPCs near a player don't despawn");
        var away = Zone().Where(e => e != creative).ToList();
        me.Pos = new Vec3d(Mid + 5000, Ground + 1, Mid + 5000);   // walk away (outside zones)
        Step(4 * 60);
        Check(away.All(e => e.Alive), "still there after 4 minutes away");
        Step(75);
        Check(away.All(e => !e.Alive && e.DespawnReason?.Reason == EnumDespawnReason.Expire), "zone NPCs despawn after 5 minutes with no player within 128 blocks");
        Check(creative.Alive, "creative-spawned NPCs never despawn");

        // --- save/load still fine ---
        var sys2 = new SpawnZoneSystem(); typeof(SpawnZoneSystem).GetField("sapi", F).SetValue(sys2, sapi);
        Priv("LoadZones").Invoke(sys2, null);
        var list = (TextCommandResult)Priv("OnList").Invoke(sys2, new object[] { new TextCommandCallingArgs() });
        Check(list.StatusMessage.StartsWith("5 spawn zones"), "zones reload: " + list.StatusMessage.Replace("\n", " | "));

        // --- veto still works ---
        var allow = Priv("AllowSpawn");
        bool Allow(string code, double rx, double rz) => (bool)allow.Invoke(sys, new object[] { null, T(code), new Vec3d(Mid + rx, 100, Mid + rz), 0L });
        bool wolfOk = (bool)allow.Invoke(sys, new object[] { null, new EntityProperties { Code = new AssetLocation("game:wolf-eurasian-adult-male") }, new Vec3d(Mid + 9999, 100, Mid), 0L });
        Check(Allow("test-mordororc-recruit", 0, 0) && !Allow("test-mordororc-recruit", 300, 0) && Allow("test-uruk-warrior", 300, 0) && !Allow("test-gondor-recruit", 9999, 0) && !Allow("test-hobbit", 0, 0) && wolfOk, "safety veto for other spawners: our NPCs only in their zones, other creatures untouched");

        // --- 0.0.44: a damaged or hand-edited save still loads, and the veto reads the cached type ---
        var savedBefore = save["lotrnpcs:spawnzones"];
        save["lotrnpcs:spawnzones"] = Encoding.UTF8.GetBytes("{ \"NextId\": 3, \"Zones\": [ null, { \"Id\": 7, \"MinX\": " + (Mid + 50) + ", \"MaxX\": " + (Mid - 50) + ", \"MinZ\": " + (Mid - 50) + ", \"MaxZ\": " + (Mid + 50)
            + ", \"Factions\": [ \" Mordor \", null, \"mordor\", \"\" ] }, { \"Id\": 8, \"MinX\": 0, \"MaxX\": 10, \"MinZ\": 0, \"MaxZ\": 10, \"Factions\": null } ] }");
        var sys3 = new SpawnZoneSystem(); typeof(SpawnZoneSystem).GetField("sapi", F).SetValue(sys3, sapi);
        bool loadThrew = false;
        try { Priv("LoadZones").Invoke(sys3, null); } catch (Exception e) { loadThrew = true; Console.WriteLine("  " + e.InnerException); }
        var damaged = (TextCommandResult)Priv("OnList").Invoke(sys3, new object[] { new TextCommandCallingArgs() });
        bool Allow3(string code, double rx, double rz) => (bool)allow.Invoke(sys3, new object[] { null, T(code), new Vec3d(Mid + rx, 100, Mid + rz), 0L });
        Check(!loadThrew && damaged.StatusMessage.StartsWith("2 spawn zones") && damaged.StatusMessage.Contains("zone 7: mordor, from") && Allow3("test-mordororc-recruit", 10, 10) && !Allow3("test-gondor-recruit", 10, 10),
            "a damaged save loads: empty zones and faction names are dropped, names are trimmed and lower case, and swapped corners are put in order");
        save["lotrnpcs:spawnzones"] = savedBefore;
        Check(ReferenceEquals(NpcTypeInfo.Of(T("test-mordororc-recruit")), NpcTypeInfo.Of(T("test-mordororc-recruit"))) && NpcTypeInfo.Of(T("test-mordororc-recruit")).Faction == "mordor" && NpcTypeInfo.Of(T("test-hobbit")).Rank == "recruit", "the spawn veto and the spawner read each NPC type once");

        // --- 0.0.52: each window rolls GroupChance ---
        me.Pos = new Vec3d(Mid, Ground + 1, Mid);   // back inside the zone
        KillAll();
        typeof(SpawnZoneSystem).GetField("config", F).SetValue(sys, cfg = new SpawnConfig { GroupChance = 0 });
        Step(60 * 60);
        Check(Zone().Count == 0, "GroupChance 0: no group in an hour");
        typeof(SpawnZoneSystem).GetField("config", F).SetValue(sys, cfg = new SpawnConfig { GroupChance = 0.5 });
        int halfGroups = 0;
        for (int i = 0; i < 6 * 60 * 60 / 5; i++) { Step(5); if (Zone().Count > 0) { halfGroups++; KillAll(); } }
        // 6 hours of 3 to 5 minute windows is about 90 windows, so about 45 groups.
        Check(halfGroups >= 30 && halfGroups <= 60, "GroupChance 0.5: about half the windows bring a group (" + halfGroups + " in 6 hours)");

        // --- 0.0.90: wild groups scale with the faction's follow limit (owner chose "A") ---
        {
            var fc = FactionConfig.FromFile(null, out _);
            var sc = new SpawnConfig();
            var rr = new Random(90);
            List<List<string>> Many(string faction, bool captain) => Enumerable.Range(0, 3000).Select(_ => SpawnZoneSystem.GroupRanks(sc, rr, captain, true, fc.GroupScaleOf(faction))).ToList();
            int MaxSize(string f) => Many(f, false).Max(g => g.Count);
            (int, int) Escort(string f) { var gs = Many(f, true); return (gs.Min(g => g.Count) - 1, gs.Max(g => g.Count) - 1); }
            var menG = Many("gondor", false); var orcG = Many("mordor", false);
            Console.WriteLine($"   > largest groups: men {MaxSize("gondor")}, levy {MaxSize("dunlendings")}, orcs {MaxSize("mordor")}, goblins {MaxSize("mistymountains")}, elves {MaxSize("elves")}, dwarves {MaxSize("dwarves")}; average men {menG.Average(g => g.Count):F2}, orcs {orcG.Average(g => g.Count):F2}");
            Check(new[] { "gondor", "isengard", "dunlendings", "mordor", "mistymountains", "elves", "dwarves", "shire" }.All(f => MaxSize(f) == 5),
                "wild groups are up to 5 for every faction (0.0.135: everyone follows 3, so every group scale is 1)");
            Check(Math.Abs(orcG.Average(g => g.Count) - menG.Average(g => g.Count)) < 0.15 && menG.All(g => g[0] == "recruit") && orcG.All(g => g[0] == "recruit")
                && menG.Concat(Many("gondor", true)).All(g => g.All(r => Array.IndexOf(Promotion.Branches, r) < 0)),
                "orc groups are as big as men's on average (0.0.135), and every group still starts with a recruit; no wild guards or shock troops (0.0.113, players only)");
            Check(new[] { "gondor", "mordor", "mistymountains", "elves" }.All(f => Escort(f) == (1, 3)),
                "a captain's escort is 1 to 3 for every faction (0.0.135)");
            Check(new[] { "mordor", "mistymountains", "elves", "nobody" }.All(f => Math.Abs(fc.GroupScaleOf(f) - 1) < 1e-9),
                "in MaxNearby every NPC counts as one (0.0.135: every faction follows 3, the baseline)");
            // 0.0.135: every faction weighs one.
            typeof(SpawnZoneSystem).GetField("groupScale", F).SetValue(sys, (System.Func<string, double>)(f => fc.GroupScaleOf(f)));
            var weight = typeof(SpawnZoneSystem).GetMethod("Weight", F);
            double W(string f) => (double)weight.Invoke(sys, new object[] { f });
            Check(Math.Abs(W("mistymountains") - 1) < 1e-9 && Math.Abs(W("elves") - 1) < 1e-9 && Math.Abs(W("gondor") - 1) < 1e-9,
                "every faction's NPCs weigh one in MaxNearby (0.0.135)");
            Check(SpawnConfig.InstructionText.Any(l => l.StartsWith("GroupSizeAvg") && l.Contains("orcs up to 7")) && SpawnConfig.InstructionText.Any(l => l.StartsWith("MaxNearby") && l.Contains("5/8")),
                "#Instructions explain the scaled groups and the weighted MaxNearby");
        }

        // --- 0.0.91: lifetime despawn (owner: unhired NPCs "despawn over time so areas dont get utterly flooded", chose "A") ---
        {
            KillAll();
            typeof(SpawnZoneSystem).GetField("config", F).SetValue(sys, cfg = new SpawnConfig { GroupChance = 0 });
            me.Pos = new Vec3d(Mid, Ground + 1, Mid);
            TestAgent Z(double rx, string origin) { var e = new TestAgent(); e.SetProps(T("test-mordororc-recruit")); e.Pos.SetPos(Mid + rx, Ground + 1, Mid); if (origin != null) e.Attributes.SetString(SpawnZoneSystem.OriginAttr, origin); world.SpawnEntity(e); return e; }
            var near = Z(30, "zone"); var mid = Z(90, "zone"); var hired = Z(90, "hired"); var placed = Z(90, null);
            Step(19 * 60);
            Check(near.Alive && mid.Alive && hired.Alive && placed.Alive, "nobody goes before 20 minutes, even 90 blocks from the player");
            Step(70);
            Check(!mid.Alive && mid.DespawnReason?.Reason == EnumDespawnReason.Expire && near.Alive && hired.Alive && placed.Alive,
                "after 20 minutes a zone NPC 90 blocks from the player (beyond 64) disappears; one 30 blocks away stays in sight; hired and creative-spawned NPCs stay");
            me.Pos = new Vec3d(Mid - 50, Ground + 1, Mid);
            Step(5);
            Check(!near.Alive && hired.Alive && placed.Alive, "as soon as the player is more than 64 blocks from the old one, it goes");
            cfg.MaxLifetimeMinutes = 0;
            var keep = Z(40, "zone");   // 90 blocks from the player (now at -50): beyond 64, within 128
            Step(40 * 60);
            Check(keep.Alive, "MaxLifetimeMinutes 0 turns the lifetime off (the player is within 128, so the away rule doesn't apply)");
            Check(new SpawnConfig().MaxLifetimeMinutes == 20 && new SpawnConfig().LifetimeDespawnDistance == 64 && SpawnConfig.InstructionText.Any(l => l.StartsWith("MaxLifetimeMinutes") && l.Contains("isn't fighting")),
                "defaults 20 minutes and 64 blocks, explained in #Instructions");
            keep.Die(EnumDespawnReason.Removed); hired.Die(EnumDespawnReason.Removed); placed.Die(EnumDespawnReason.Removed);
        }

        // ---- 0.0.112: zone patrols ----
        {
            int[] route = { 0, 0, 100, 0 };
            int next = 0;
            bool walk = AiTaskZonePatrol.Step(route, ref next, 50.5, 0.5, 0, 0, out double gx, out double gz);
            Check(walk && next == 0 && Math.Abs(gx - 30.5) < 1e-9 && Math.Abs(gz - 0.5) < 1e-9, "0.0.112: a far patrol point is walked toward one 20-block leg at a time");
            walk = AiTaskZonePatrol.Step(route, ref next, 1.5, 0.5, 0, 0, out _, out _);
            int after = next;
            bool walk2 = AiTaskZonePatrol.Step(route, ref next, 101, 0.5, 0, 0, out _, out _);
            Check(!walk && after == 1 && !walk2 && next == 0, "0.0.112: a reached point pauses the NPC and moves it on to the next, round to the first again");
            int bad = 7;
            AiTaskZonePatrol.Step(route, ref bad, 0, 0, 0, 0, out _, out _);
            Check(bad == 0 || bad == 1, "0.0.112: an out-of-range saved index wraps onto the route");
            Check(SpawnZoneSystem.NearestPoint(new List<int> { 0, 0, 100, 0, 50, 50 }, 60, 40) == 2, "0.0.112: a group starts for the patrol point nearest its leader");
            Check(new SpawnZone { Patrol = new List<int> { 1, 2 } }.Patrols == false && new SpawnZone { Patrol = new List<int> { 1, 2, 3, 4 } }.Patrols && !new SpawnZone().Patrols,
                "0.0.112: a zone patrols only with 2 or more points");
            Check(Directory.GetFiles(argv[0], "*.json").Select(f => JObject.Parse(File.ReadAllText(f))).All(j => j["server"]["behaviors"].First(b => (string)b["code"] == "taskai")["aitasks"].Any(t => (string)t["code"] == "lotrnpcs:zonepatrol")),
                "0.0.112: every NPC type has the zone patrol task");
        }

        Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
        return fails;
    }
}
