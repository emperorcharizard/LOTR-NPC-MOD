// Targeting benchmark (0.0.44): how long the AI's "should I attack this?" checks take and how much they allocate.
// Builds against any lotrnpcs.dll with the 0.0.43 public API: dotnet build -c Release -o out -p:ModDll=<path>.
// Usage: dotnet out/bench.dll <generated assets/lotrnpcs/entities folder>
using System; using System.Collections.Generic; using System.Diagnostics; using System.IO; using System.Linq; using System.Reflection;
using Newtonsoft.Json.Linq; using Vintagestory.API.Common; using Vintagestory.API.Common.Entities; using Vintagestory.API.Datastructures;
using Vintagestory.API.Util; using Vintagestory.ServerMods.NoObf; using LotrNpcs;

public class BenchAgent : EntityAgent { public void SetProps(EntityProperties p) { Properties = p; } }

public static class Program {
    static void SetCode(Entity e, AssetLocation code) {
        for (Type t = e.GetType(); t != null; t = t.BaseType) {
            var bf = t.GetField("<Code>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (bf != null) { bf.SetValue(e, code); return; }
            var p = t.GetProperty("Code", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (p != null && p.CanWrite) { p.SetValue(e, code); return; }
        }
    }

    public static int Main(string[] argv) {
        var types = new Dictionary<string, EntityProperties>();
        foreach (string file in Directory.GetFiles(argv[0], "*.json")) {
            JObject jo = JObject.Parse(File.ReadAllText(file)); jo.Remove("sounds"); jo.Remove("tags");
            EntityType et = jo.ToObject<EntityType>(JsonUtil.CreateSerializerForDomain("lotrnpcs"));
            types[(string)jo["code"]] = new EntityProperties { Code = new AssetLocation("lotrnpcs", (string)jo["code"]), Attributes = et.Attributes,
                Server = new EntityServerProperties(et.Server.Behaviors, et.BehaviorConfigs) };
        }
        long id = 1;
        BenchAgent Npc(string code) { var e = new BenchAgent(); e.SetProps(types[code]); SetCode(e, types[code].Code); e.EntityId = id++; return e; }
        BenchAgent Mob(string code, bool mind) {
            var p = new EntityProperties { Code = new AssetLocation(code), Server = new EntityServerProperties(mind ? new[] { JsonObject.FromJson("{ \"code\": \"health\" }"), JsonObject.FromJson("{ \"code\": \"taskai\" }") } : new JsonObject[0], new Dictionary<string, JsonObject>()) };
            var e = new BenchAgent(); e.SetProps(p); SetCode(e, p.Code); e.EntityId = id++; return e;
        }
        EntityPlayer Player(string race) { var p = new EntityPlayer(); if (race != null) p.WatchedAttributes.SetString("skinModel", race); p.EntityId = id++; p.WatchedAttributes.SetString("playerUID", "uid" + p.EntityId); return p; }

        var fs = new FactionSystem(); var cfg = new FactionConfig(); cfg.FillDefaults(); cfg.Factions["mordor"].FriendlyGroups.Add("barad-dur"); fs.SetConfig(cfg);
        var hiring = new HiringSystem { Factions = fs }; fs.Hiring = hiring;

        // A busy spot: NPCs of six factions, players of several races, monsters and animals, and two players' recruits.
        string[] npcCodes = { "test-mordororc-recruit", "test-mordororc-warrior", "test-uruk-recruit", "test-angmarorc-captain", "test-gondor-recruit", "test-gondor-warrior",
            "test-rohan-recruit", "test-dwarf-warrior", "test-elf-recruit", "test-hobbit", "test-haradrim-recruit", "test-easterling-captain" };
        var crowd = new List<Entity>();
        for (int i = 0; i < 3; i++) foreach (string c in npcCodes) crowd.Add(Npc(c));
        var players = new List<EntityPlayer> { Player("lrc:orc"), Player("lrc:longbeard"), Player("faraces:hobbit"), Player(null), Player("lrc:noldor"), Player("lrc:wildmen") };
        fs.SetAssigned(players[3].PlayerUID, "Lord", "gondor");
        crowd.AddRange(players);
        for (int i = 0; i < 4; i++) { crowd.Add(Mob("game:drifter-normal", true)); crowd.Add(Mob("game:wolf-eurasian-adult-male", true)); crowd.Add(Mob("game:deer-redbrocket-male", true)); crowd.Add(Mob("game:armorstand", false)); }
        string lord = players[3].PlayerUID, boss = players[0].PlayerUID;
        hiring.SetOwner(lord, new HiringOwner { Name = "Lord", EntityId = players[3].EntityId });
        hiring.SetOwner(boss, new HiringOwner { Name = "Boss", Race = "lrc:orc", Groups = new List<string> { "barad-dur" }, EntityId = players[0].EntityId });
        var lordsMen = new List<BenchAgent>(); var bossOrcs = new List<BenchAgent>();
        for (int i = 0; i < 5; i++) { var g = Npc(i % 2 == 0 ? "test-gondor-warrior" : "test-gondor-recruit"); Recruit.Bind(g, lord, "Lord"); lordsMen.Add(g); crowd.Add(g); }
        for (int i = 0; i < 5; i++) { var o = Npc("test-mordororc-warrior"); Recruit.Bind(o, boss, "Boss"); bossOrcs.Add(o); crowd.Add(o); }
        Recruit.SetStance(bossOrcs[0], Recruit.Raid); Recruit.SetStance(bossOrcs[1], Recruit.Raid); Recruit.SetStance(lordsMen[0], Recruit.Defensive);
        var wild = crowd.Where(e => Recruit.OwnerUidOf(e) == null && FactionSystem.FactionOf(e) != null).Take(24).ToList();

        int checks = 0, yes = 0;
        void Round() {
            foreach (Entity seeker in wild) {
                string faction = FactionSystem.FactionOf(seeker);
                foreach (Entity e in crowd) { checks++; if (e != seeker && fs.WantsToAttack(faction, e)) yes++; }
            }
            foreach (BenchAgent h in lordsMen) foreach (Entity e in crowd) { checks++; if (fs.RecruitWantsToAttack(h, lord, e)) yes++; }
            foreach (BenchAgent h in bossOrcs) foreach (Entity e in crowd) { checks++; if (fs.RecruitWantsToAttack(h, boss, e)) yes++; }
        }
        for (int i = 0; i < 200; i++) Round();   // warm up the JIT and any caches
        checks = 0; yes = 0;
        GC.Collect(); GC.WaitForPendingFinalizers();
        long bytes0 = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 2000; i++) Round();
        sw.Stop();
        long bytes = GC.GetAllocatedBytesForCurrentThread() - bytes0;
        Console.WriteLine($"lotrnpcs {typeof(FactionSystem).Assembly.GetName().Version} ({Path.GetFileName(typeof(FactionSystem).Assembly.Location)}): {checks} targeting checks ({yes} yes) over {crowd.Count} creatures");
        Console.WriteLine($"  {sw.Elapsed.TotalMilliseconds * 1e6 / checks:0.0} ns per check, {(double)bytes / checks:0.0} bytes allocated per check, {sw.Elapsed.TotalMilliseconds:0} ms total");

        // The same checks by kind of seeker and target, to see where the time goes.
        string Kind(Entity e) => e is EntityPlayer ? "players" : Recruit.OwnerUidOf(e) != null ? "recruits" : FactionSystem.FactionOf(e) != null ? "faction NPCs" : "other creatures";
        foreach (string kind in new[] { "players", "faction NPCs", "recruits", "other creatures" }) {
            var targets = crowd.Where(e => Kind(e) == kind).ToList();
            foreach (bool asRecruit in new[] { false, true }) {
                int n = 0;
                void Part() {
                    if (!asRecruit) foreach (Entity seeker in wild) { string faction = FactionSystem.FactionOf(seeker); foreach (Entity e in targets) { n++; fs.WantsToAttack(faction, e); } }
                    else { foreach (BenchAgent h in lordsMen) foreach (Entity e in targets) { n++; fs.RecruitWantsToAttack(h, lord, e); } foreach (BenchAgent h in bossOrcs) foreach (Entity e in targets) { n++; fs.RecruitWantsToAttack(h, boss, e); } }
                }
                for (int i = 0; i < 100; i++) Part();
                n = 0; long b0 = GC.GetAllocatedBytesForCurrentThread(); var w = Stopwatch.StartNew();
                for (int i = 0; i < 1000; i++) Part();
                w.Stop();
                Console.WriteLine($"  {(asRecruit ? "recruits" : "wild NPCs"),-9} judging {kind,-15}: {w.Elapsed.TotalMilliseconds * 1e6 / n,6:0.0} ns, {(double)(GC.GetAllocatedBytesForCurrentThread() - b0) / n,5:0.0} bytes per check");
            }
        }
        return 0;
    }
}
