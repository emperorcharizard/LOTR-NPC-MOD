using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Reflection;
using Newtonsoft.Json.Linq; using Vintagestory.API.Common; using Vintagestory.API.Common.Entities; using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools; using Vintagestory.API.Util; using Vintagestory.ServerMods.NoObf; using LotrNpcs;

public class TestAgent : EntityAgent { public void SetProps(EntityProperties p) { Properties = p; } }

public static class Program {
    static int fails = 0;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
    static void SetCode(Entity e, AssetLocation code) {
        for (Type t = e.GetType(); t != null; t = t.BaseType) {
            var p = t.GetProperty("Code", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (p != null && p.CanWrite) { p.SetValue(e, code); return; }
            var f = t.GetField("Code", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f != null) { f.SetValue(e, code); return; }
            var bf = t.GetField("<Code>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (bf != null) { bf.SetValue(e, code); return; }
        }
        throw new Exception("no Code member");
    }

    // 0.0.92: race and /group count only with RaceAndGroupMembership; the checks written before 0.0.92 test that rule.
    static void SetLegacy(FactionSystem f, FactionConfig c) { c.RaceAndGroupMembership = true; f.SetConfig(c); }

    public static int Main(string[] argv) {
        var types = new Dictionary<string, EntityProperties>();
        foreach (string file in Directory.GetFiles(argv[0], "*.json")) {
            JObject jo = JObject.Parse(File.ReadAllText(file)); jo.Remove("sounds"); jo.Remove("tags");
            EntityType et = jo.ToObject<EntityType>(JsonUtil.CreateSerializerForDomain("lotrnpcs"));
            types[(string)jo["code"]] = new EntityProperties { Code = new AssetLocation("lotrnpcs", (string)jo["code"]), Attributes = et.Attributes };
            var ai = jo["server"]["behaviors"].First(b => (string)b["code"] == "taskai")["aitasks"].Select(t => (string)t["code"]).ToList();
            if (!ai.Contains("lotrnpcs:seekentity") || !ai.Contains("lotrnpcs:meleeattack")) Check(false, (string)jo["code"] + " lacks the faction AI tasks");
            if (!jo["server"]["behaviors"].Any(b => (string)b["code"] == "lotrnpcs:factionmember")) Check(false, (string)jo["code"] + " lacks lotrnpcs:factionmember");
            // Hiring (0.0.37): the talk/recruit behavior on both sides, before health on the server (to cancel friendly
            // fire), and the follow task between seek and wander.
            var server = jo["server"]["behaviors"].Select(b => (string)b["code"]).ToList();
            if (!(server.IndexOf("lotrnpcs:hireable") >= 0 && server.IndexOf("lotrnpcs:hireable") < server.IndexOf("health"))) Check(false, (string)jo["code"] + " lacks lotrnpcs:hireable before health");
            if (!jo["client"]["behaviors"].Any(b => (string)b["code"] == "lotrnpcs:hireable")) Check(false, (string)jo["code"] + " lacks the client lotrnpcs:hireable");
            var tasks = jo["server"]["behaviors"].First(b => (string)b["code"] == "taskai")["aitasks"];
            float Prio(string code) => (float)tasks.First(t => (string)t["code"] == code)["priority"];
            if (!ai.Contains("lotrnpcs:followowner") || !(Prio("lotrnpcs:followowner") < Prio("lotrnpcs:seekentity") && Prio("lotrnpcs:followowner") > Prio("wander"))) Check(false, (string)jo["code"] + " lacks lotrnpcs:followowner between seek and wander");
        }
        Check(types.Values.All(t => t.Attributes["lotrnpcsFaction"].Exists), "every NPC has a faction (" + types.Count + ")");
        Check(types["test-hobbit"].Attributes["lotrnpcsFaction"].AsString() == "shire" && types["test-uruk"].Attributes["lotrnpcsFaction"].AsString() == "isengard"
            && types["test-goblin"].Attributes["lotrnpcsFaction"].AsString() == "mistymountains" && types["test-dunlending-recruit"].Attributes["lotrnpcsFaction"].AsString() == "dunlendings"
            && types["test-elf-captain"].Attributes["lotrnpcsFaction"].AsString() == "elves" && types["test-wickeddwarf-warrior"].Attributes["lotrnpcsFaction"].AsString() == "wickeddwarves", "faction names match the config");
        Check(!types["test-uruk"].Attributes["lotrnpcsRank"].Exists && !types["test-goblin"].Attributes["lotrnpcsRank"].Exists, "rankless NPCs have no rank (so zones never spawn them)");
        Check(types["test-hobbit"].Attributes["lotrnpcsRank"].AsString() == "recruit", "hobbits count as the Shire's recruits");
        Check(types.Values.All(t => FactionConfig.DefaultFactions().ContainsKey(t.Attributes["lotrnpcsFaction"].AsString())), "every NPC's faction is in the default config");
        Check(!types.ContainsKey("test-gondor") && !types.ContainsKey("test-dwarf") && types.Count == 115, "the unarmored Gondor, Rohan, Dunlending, Easterling and Dwarf NPCs are replaced by ranked ones (" + types.Count + " NPCs)");

        TestAgent Npc(string code) { var e = new TestAgent(); e.SetProps(types[code]); SetCode(e, types[code].Code); return e; }
        long nextId = 1;
        TestAgent Mob(string code) { var p = new EntityProperties { Code = new AssetLocation(code) }; var e = new TestAgent(); e.SetProps(p); SetCode(e, p.Code); e.EntityId = 10000 + nextId++; return e; }
        EntityPlayer Player(string race) { var p = new EntityPlayer(); if (race != null) p.WatchedAttributes.SetString("skinModel", race); p.EntityId = 20000 + nextId++; p.WatchedAttributes.SetString("playerUID", "uid" + p.EntityId); return p; }

        var fs = new FactionSystem();
        // A player an admin put in a faction with /lotrfaction set.
        EntityPlayer Member(string race, string faction) { var p = Player(race); fs.SetAssigned(p.PlayerUID, "Member" + p.EntityId, faction); return p; }
        var cfg = new FactionConfig();
        SetLegacy(fs, cfg);
        List<string> none = new List<string>();
        Check(fs.Config.Factions.Count == 18, "18 default factions (Gundabad since 0.0.62): " + string.Join(",", fs.Config.Factions.Keys));
        // relations: no sides, factions are only friendly when the config says so (user, 2026-11-02)
        var keys = fs.Config.Factions.Keys.ToList();
        Check(keys.All(a => keys.All(b => a == b || fs.Relation(a, b) == FactionRelation.Enemy)), "no factions are allies by default (0.0.76, user: \"No factions should be allies by default\"); every pair are enemies");
        Check(fs.Relation("mordor", "angmar") == FactionRelation.Enemy && fs.Relation("isengard", "mistymountains") == FactionRelation.Enemy && fs.Relation("angmar", "easterlings") == FactionRelation.Enemy, "orc factions are enemies of each other");
        Check(fs.Relation("gondor", "rohan") == FactionRelation.Enemy && fs.Relation("dunlendings", "gondor") == FactionRelation.Enemy, "Free peoples and Dunlendings too");
        Check(fs.Relation("gondor", "gondor") == FactionRelation.Ally && fs.Relation("gondor", "rivendell") == FactionRelation.Neutral, "a faction is its own ally; factions not in the config are left alone");
        Check(fs.WithAllies("angmar").SequenceEqual(new[] { "angmar" }) && fs.WithAllies("mordor").SequenceEqual(new[] { "mordor" }), "only the faction itself answers a call for help when it has no allies");

        // players
        var orcPlayer = Player("lrc:orc"); var faOrc = Player("lrc:uruk"); var hobbitPlayer = Player("lrc:harfoot"); var nobody = Player(null); var dunPlayer = Player("lrc:wildmen");
        var gondorian = Member("lrc:freepeople", "gondor");
        Check(new[] { "mordor", "angmar", "mistymountains", "isengard" }.All(f => fs.WantsToAttack(f, orcPlayer) && fs.WantsToAttack(f, faOrc) && fs.WantsToAttack(f, Player("lrc:uruk")) && fs.WantsToAttack(f, Player("lrc:orc"))), "orc-race players are nobody's friends: orc players join through groups");
        Check(fs.WantsToAttack("mordor", hobbitPlayer) && fs.WantsToAttack("isengard", nobody), "aggressive orcs attack non-friendly players on sight");
        Check(fs.WantsToAttack("gondor", orcPlayer) && fs.WantsToAttack("gondor", hobbitPlayer) && fs.WantsToAttack("rohan", gondorian) && fs.WantsToAttack("dwarves", nobody) && fs.WantsToAttack("dunlendings", nobody), "every faction but the Shire attacks non-members on sight");
        Check(!fs.WantsToAttack("gondor", gondorian) && !fs.WantsToAttack("easterlings", Player("lrc:easterling")) && !fs.WantsToAttack("dwarves", Player("lrc:longbeard")) && !fs.WantsToAttack("dunlendings", dunPlayer), "factions leave their own members alone");
        Check(!fs.WantsToAttack("shire", nobody) && !fs.WantsToAttack("shire", orcPlayer), "the Shire stays defensive");
        Check(!fs.WantsToAttack("dwarves", Player("LRC:Longbeard")), "race codes compare case-insensitively");
        Check(fs.WantsToAttack("mordor", dunPlayer), "a Dunlending player is not a Mordor friend");
        Check(fs.IsFriend("shire", "lrc:harfoot", none) && !fs.IsFriend("rohan", "lrc:harfoot", none) && !fs.IsFriend("shire", "lrc:longbeard", none), "friendly races count for their own faction only");
        Check(fs.Config.Factions["mordor"].FriendlyRaces.Count == 0 && fs.Config.Factions["angmar"].FriendlyRaces.Count == 0, "orc factions list no races");
        // groups
        cfg = new FactionConfig(); cfg.FillDefaults(); cfg.Factions["gondor"].FriendlyGroups.Add("Minas Tirith"); cfg.Factions["mordor"].FriendlyGroups.Add("Barad-dur");
        SetLegacy(fs, cfg);
        Check(fs.IsFriend("mordor", "lrc:orc", new List<string> { "barad-dur" }), "an orc player in Mordor's group is Mordor's friend");
        Check(!fs.IsFriend("angmar", "lrc:orc", new List<string> { "barad-dur" }) && !fs.IsFriend("isengard", "lrc:orc", new List<string> { "barad-dur" }), "...but not a friend of the other orc factions");
        Check(fs.IsFriend("gondor", "lrc:orc", new List<string> { "minas tirith" }) && !fs.IsFriend("mordor", "lrc:orc", new List<string> { "minas tirith" }) && !fs.IsFriend("rohan", "lrc:orc", new List<string> { "minas tirith" }), "an orc in Gondor's group is Gondor's friend only");
        Check(fs.IsFriend("gondor", null, new List<string> { "minas tirith", "barad-dur" }) && fs.IsFriend("mordor", null, new List<string> { "minas tirith", "barad-dur" }), "a player in two factions' groups is a friend of both");
        Check(!fs.IsFriend("gondor", "lrc:orc", new List<string> { "edoras" }), "other groups don't count");
        Console.WriteLine("   > " + fs.DescribePlayer("Emperorcharizar", "lrc:orc", new List<string> { "barad-dur" }).Replace("\n", "\n   > "));
        Console.WriteLine("   > " + fs.DescribePlayer("Hobbit", "lrc:harfoot", new List<string>()).Replace("\n", "\n   > "));

        // NPC vs NPC
        Check(fs.WantsToAttack("mordor", Npc("test-angmarorc-warrior")) && fs.WantsToAttack("isengard", Npc("test-goblin")) && fs.WantsToAttack("angmar", Npc("test-uruk-captain")) && fs.WantsToAttack("mistymountains", Npc("test-mordororc-recruit")), "orc factions fight each other on sight");
        Check(fs.WantsToAttack("gondor", Npc("test-dwarf-recruit")) && fs.WantsToAttack("shire", Npc("test-rohan-warrior")) && fs.WantsToAttack("dunlendings", Npc("test-gondor-captain")), "Free peoples and Dunlendings fight each other too, the defensive Shire included");
        Check(fs.WantsToAttack("mordor", Npc("test-gondor-captain")) && fs.WantsToAttack("gondor", Npc("test-uruk-captain")) && fs.WantsToAttack("shire", Npc("test-angmarorc-recruit")), "orcs and Free peoples fight on sight");
        Check(!fs.WantsToAttack("mordor", Npc("test-mordororc-recruit")) && !fs.WantsToAttack("angmar", Npc("test-angmarorc-warrior")), "same faction never fights");
        Check(fs.WantsToAttack("mordor", Npc("test-uruk-captain")) && fs.WantsToAttack("isengard", Npc("test-mordororc-warrior")) && fs.WantsToAttack("isengard", Npc("test-angmarorc-recruit")), "Mordor and Isengard fight too since 0.0.76 (no default alliance), and both fight Angmar");

        // monsters
        Check(fs.WantsToAttack("gondor", Mob("game:drifter-normal")) && fs.WantsToAttack("mordor", Mob("game:bowtorn-deep")) && fs.WantsToAttack("shire", Mob("game:locust-bronze")), "every faction attacks drifters, bowtorns, locusts");
        Check(!fs.WantsToAttack("mordor", Mob("game:wolf-eurasian-adult-male")) && !fs.WantsToAttack("gondor", Mob("game:chicken-hen")), "animals are left alone");
        var deadDrifter = Mob("game:drifter-normal"); deadDrifter.Alive = false;
        Check(!fs.WantsToAttack("gondor", deadDrifter), "dead things are not targets");

        // grudges and stance, with two alliances set in the config
        cfg = new FactionConfig(); cfg.FillDefaults();
        cfg.Relations.Add(new FactionRelationDef { A = "shire", B = "gondor", Relation = "ally" });
        cfg.Relations.Add(new FactionRelationDef { A = "Shire", B = "Dwarves", Relation = "Ally" });
        SetLegacy(fs, cfg);
        Check(fs.WithAllies("shire").OrderBy(f => f).SequenceEqual(new[] { "dwarves", "gondor", "shire" }) && fs.WithAllies("gondor").OrderBy(f => f).SequenceEqual(new[] { "gondor", "shire" }), "alliances come from the config only, and aren't passed on (Gondor and the Dwarves stay enemies)");
        Check(!fs.WantsToAttack("gondor", Npc("test-hobbit")) && !fs.WantsToAttack("shire", Npc("test-dwarf-recruit")) && fs.WantsToAttack("gondor", Npc("test-dwarf-recruit")), "allies never fight");
        var wolf = Mob("game:wolf-eurasian-adult-male");
        fs.NowForTests = 1000;
        fs.RecordAttack("shire", gondorian);
        Check(fs.WantsToAttack("shire", gondorian) && fs.WantsToAttack("gondor", gondorian), "a Gondor member who hits the Shire is fought by the Shire and its ally Gondor");
        fs.RecordAttack("shire", dunPlayer);
        Check(fs.WantsToAttack("shire", dunPlayer) && !fs.WantsToAttack("dunlendings", dunPlayer), "...but not by factions that aren't allied");
        fs.RecordAttack("shire", hobbitPlayer);
        Check(fs.WantsToAttack("shire", hobbitPlayer), "friends who attack get fought back too");
        fs.RecordAttack("mordor", wolf);
        Check(fs.WantsToAttack("mordor", wolf) && !fs.WantsToAttack("angmar", wolf) && !fs.WantsToAttack("gondor", wolf), "an animal that attacks Mordor is fought by Mordor only");
        fs.RecordAttack("shire", Npc("test-gondor-captain"));
        Check(!fs.WantsToAttack("shire", Npc("test-gondor-captain")), "an ally's stray hit never starts a fight");
        fs.NowForTests = 1000 + 31000;
        Check(!fs.WantsToAttack("gondor", gondorian) && !fs.WantsToAttack("shire", hobbitPlayer) && !fs.WantsToAttack("mordor", wolf), "grudges end after 30 seconds");

        // config overrides
        cfg = new FactionConfig { Relations = new List<FactionRelationDef> { new FactionRelationDef { A = "Mordor", B = "Isengard", Relation = "ally" }, new FactionRelationDef { A = "gondor", B = "dunlendings", Relation = "neutral" } } };
        SetLegacy(fs, cfg);
        Check(fs.Relation("mordor", "isengard") == FactionRelation.Ally && !fs.WantsToAttack("isengard", Npc("test-mordororc-recruit")) && !fs.WantsToAttack("mordor", Npc("test-uruk")), "admins can ally two orc factions: they stop fighting");
        Check(fs.WantsToAttack("angmar", Npc("test-uruk")) && fs.WantsToAttack("isengard", Npc("test-angmarorc-recruit")), "...and the other orc factions still fight both");
        Check(fs.Relation("gondor", "dunlendings") == FactionRelation.Neutral && !fs.WantsToAttack("gondor", Npc("test-dunlending-recruit")) && !fs.WantsToAttack("dunlendings", Npc("test-gondor-captain")) && fs.WantsToAttack("gondor", dunPlayer), "a neutral entry: their NPCs leave each other alone, players are still judged by friend lists");
        Check(fs.Config.Relations.Count == 2, "the file's relations replace the default list");

        // share friends
        cfg = new FactionConfig(); cfg.FillDefaults();
        cfg.Relations.Add(new FactionRelationDef { A = "Rohan", B = "Dwarves", Relation = "ally", ShareFriends = true });
        cfg.Relations.Add(new FactionRelationDef { A = "mordor", B = "gondor", Relation = "enemy", ShareFriends = true });
        cfg.Relations.Add(new FactionRelationDef { A = "dunlendings", B = "shire", Relation = "neutral", ShareFriends = true });
        SetLegacy(fs, cfg);
        var rohirrim = Member("lrc:freepeople", "rohan"); var dwarf = Player("lrc:longbeard");
        Check(!fs.WantsToAttack("dwarves", rohirrim) && !fs.WantsToAttack("rohan", dwarf) && fs.IsFriend("dwarves", null, none, "rohan"), "ShareFriends on an alliance: each side spares the other's friends");
        Check(fs.WantsToAttack("gondor", rohirrim) && fs.WantsToAttack("gondor", dwarf) && fs.WantsToAttack("rohan", gondorian), "...and only those two factions");
        Check(!fs.IsFriend("mordor", null, none, "gondor") && !fs.IsFriend("dunlendings", "lrc:harfoot", none), "ShareFriends does nothing between enemies or neutral factions");
        Check(fs.SharesFriends("rohan", "dwarves") && fs.SharesFriends("dwarves", "rohan") && !fs.SharesFriends("gondor", "rohan") && fs.Relation("gondor", "rohan") == FactionRelation.Enemy, "the default gondor/rohan example entry changes nothing");
        var example = cfg.Relations.First(r => r.A == "gondor" && r.B == "rohan"); example.Relation = "ally"; example.ShareFriends = true; SetLegacy(fs, cfg);
        Check(!fs.WantsToAttack("rohan", gondorian) && !fs.WantsToAttack("gondor", rohirrim) && fs.Relation("gondor", "rohan") == FactionRelation.Ally && !fs.WantsToAttack("gondor", Npc("test-rohan-warrior")), "editing the example to an ally that shares friends works");
        Console.WriteLine("   > " + string.Join("\n   > ", fs.DescribeFactions().Split('\n').Skip(17)));

        var stanceCfg = new FactionConfig(); stanceCfg.FillDefaults(); stanceCfg.Factions["gondor"].Stance = "Defensive"; stanceCfg.Factions["shire"].Stance = "Aggressive";
        SetLegacy(fs, stanceCfg);
        Check(!fs.WantsToAttack("gondor", orcPlayer) && fs.WantsToAttack("shire", orcPlayer) && !fs.WantsToAttack("shire", hobbitPlayer), "stance from config: defensive Gondor, aggressive Shire that spares its friends");

        // JSON round trip of the config file
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(FactionConfig.FromFile(null, out _), Newtonsoft.Json.Formatting.Indented);
        var back = Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(json);
        back.Factions.Remove("dunlendings"); back.Factions["mordor"].FriendlyRaces = new List<string> { "lrc:uruk" };
        var again = Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(Newtonsoft.Json.JsonConvert.SerializeObject(back));
        SetLegacy(fs, again);
        Check(!fs.Config.Factions.ContainsKey("dunlendings") && fs.Config.Factions["mordor"].FriendlyRaces.SequenceEqual(new[] { "lrc:uruk" }), "edits in the file replace the defaults, nothing gets re-added");
        Check(!json.Contains("Bloc"), "the file has no sides any more");

        // migrating older files
        var freshCfg = FactionConfig.FromFile(null, out bool storeFresh);
        Check(storeFresh && freshCfg.Version == FactionConfig.CurrentVersion && FactionConfig.CurrentVersion == 30 && json.Contains("\"Version\": 30") && json.Contains("\"RosterPerCaptain\": 8") && !json.Contains("MaxHirelings") && json.Contains("\"ShieldChance\": 0.5") && json.Contains("\"HelmetChance\": 0.6") && json.Contains("\"CaptainsAlwaysHelmeted\": false") && json.Contains("\"MaxAttackersPerTarget\": 4") && json.Contains("\"CombatSpacing\": 1.5") && json.Contains("\"BattleLines\": true") && json.Contains("\"DuelDistanceMin\": 1.0") && json.Contains("\"DuelDistanceMax\": 1.6") && json.Contains("\"LineHoldRange\": 6.0") && json.Contains("\"MaxRecruits\": 16") && json.Contains("\"MaxFollowers\": 5") && json.Contains("\"GuardRadius\": 16") && json.Contains("\"HealthRegenSpeed\": 30.0") && json.Contains("\"ShareFriends\": false") && json.Contains("\"Relation\": \"enemy\"") && !freshCfg.Relations.Any(r => r.Relation == "ally") && freshCfg.Relations.Count == 1, "a new file is written with version 30 (no MaxHirelings, no alliance), the example entry, the battle spacing, helmet and shield settings, the regen settings, the guard radius and the hire and follow caps");
        var current = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(json.Replace("\"Stance\": \"aggressive\"", "\"Stance\": \"defensive\"")), out bool storeCurrent);
        Check(!storeCurrent && current.Factions["gondor"].Stance == "defensive", "a current file is left alone, even with defensive factions");
        // 0.0.112: files before version 19 are no longer converted step by step (both servers run 0.0.108 or later).

        // ---- 0.0.35: the factions the user listed, admin-set factions, hire prices, instructions ----
        cfg = new FactionConfig(); SetLegacy(fs, cfg);
        var d = fs.Config.Factions;
        Check(new[] { "gondor", "rohan", "dale", "arnor", "haradrim", "dunlendings", "blacknumenoreans", "easterlings", "dwarves", "wickeddwarves", "shire", "elves", "tormentedelves", "mordor", "angmar", "mistymountains", "isengard" }.All(d.ContainsKey), "all 17 factions are in the default config");
        Check(new[] { "gondor", "rohan", "dale", "arnor" }.All(f => d[f].FriendlyRaces.Count == 0) && new[] { "gondor", "rohan", "dale", "arnor" }.All(f => fs.WantsToAttack(f, Player("lrc:freepeople")) && fs.WantsToAttack(f, Player("lrc:freepeople"))), "Men of the West share one race, so Gondor, Rohan, Dale and Arnor list none and attack it (players join by group or /lotrfaction set)");
        Check(!fs.WantsToAttack("haradrim", Player("lrc:haradrim")) && !fs.WantsToAttack("blacknumenoreans", Player("lrc:bnumenorean")) && !fs.WantsToAttack("dunlendings", Player("lrc:wildmen")) && !fs.WantsToAttack("easterlings", Player("lrc:easterling")), "Haradrim, Black Numenoreans, Dunlendings and Easterlings spare their own race");
        Check(new[] { "lrc:harfoot", "lrc:stoor", "lrc:fallohide" }.All(r => fs.IsFriend("shire", r, none)), "every hobbit sub-race is the Shire");
        Check(new[] { "lrc:longbeard", "lrc:firebeardsandbroadbeams" }.All(r => fs.IsFriend("dwarves", r, none) && fs.WantsToAttack("wickeddwarves", Player(r))) && fs.IsFriend("wickeddwarves", "lrc:wickeddwarf", none) && fs.WantsToAttack("dwarves", Player("lrc:wickeddwarf")), "Longbeards and Firebeards are the Dwarves; wicked dwarves are their own faction");
        Check(new[] { "lrc:noldor", "lrc:sindar", "lrc:avari" }.All(r => fs.IsFriend("elves", r, none) && fs.WantsToAttack("tormentedelves", Player(r))) && fs.IsFriend("tormentedelves", "lrc:tormentedelf", none) && fs.WantsToAttack("elves", Player("lrc:tormentedelf")), "Noldor, Sindar and Avari are the Elves; tormented elves are their own faction");
        Check(fs.WantsToAttack("elves", Npc("test-tormentedelf-recruit")) && fs.WantsToAttack("dale", Npc("test-arnor-warrior")) && fs.WantsToAttack("wickeddwarves", Npc("test-dwarf-captain")) && fs.WantsToAttack("haradrim", Npc("test-blacknumenorean-recruit")), "the new factions fight each other on sight (no alliances by default)");
        // admin-set factions
        var hobbitOrc = Member("lrc:harfoot", "mordor");
        Check(!fs.WantsToAttack("mordor", hobbitOrc) && fs.WantsToAttack("angmar", hobbitOrc) && !fs.IsFriend("shire", "lrc:harfoot", none, "mordor") && fs.IsFriend("mordor", "lrc:harfoot", none, "mordor"), "/lotrfaction set: a hobbit set to Mordor is spared by Mordor, and is no longer a Shire member");
        Check(fs.IsFriend("rohan", "lrc:freepeople", new List<string> { "edoras" }, "gondor") == false && fs.IsFriend("gondor", "lrc:freepeople", new List<string> { "edoras" }, "gondor"), "...the set faction counts");
        cfg = new FactionConfig(); cfg.FillDefaults(); cfg.Factions["rohan"].FriendlyGroups.Add("Edoras"); SetLegacy(fs, cfg);
        Check(fs.IsFriend("rohan", "lrc:freepeople", new List<string> { "edoras" }, "gondor"), "...and the player's groups still count");
        Check(fs.AssignedFaction(hobbitOrc.PlayerUID) == "mordor" && fs.AssignedPlayers.Any(a => a.Faction == "mordor"), "the set faction is remembered");
        fs.SetAssigned(hobbitOrc.PlayerUID, "x", null);
        Check(fs.AssignedFaction(hobbitOrc.PlayerUID) == null && fs.IsFriend("shire", FactionSystem.RaceOf(hobbitOrc), none, fs.AssignedFaction(hobbitOrc.PlayerUID)) && fs.WantsToAttack("mordor", hobbitOrc), "/lotrfaction clear: the race counts again");
        fs.SetAssigned("uidX", "Frodo", "SHIRE");
        Check(fs.AssignedFaction("uidX") == "shire", "set factions are stored lower case");
        cfg.Relations.Add(new FactionRelationDef { A = "elves", B = "dwarves", Relation = "ally", ShareFriends = true }); SetLegacy(fs, cfg);
        Check(!fs.WantsToAttack("dwarves", Member("lrc:wildmen", "elves")) && fs.WantsToAttack("gondor", Member("lrc:wildmen", "elves")), "a set faction shares friends through ShareFriends alliances like a race does");
        string desc = fs.DescribePlayer("Bilbo", "lrc:harfoot", new List<string>(), "mordor");
        Check(desc.Contains("faction mordor") && desc.Contains("Friend of: mordor"), "/recruits faction check shows the player's faction");
        // hire prices
        cfg = new FactionConfig(); SetLegacy(fs, cfg);
        Check(fs.Config.Factions.All(kv => kv.Value.HireCoin == "game:gear-rusty" && kv.Value.HirePrices.Count == 4 && kv.Value.HirePrices.All(p => FactionTiers.Of(kv.Key).Hire[p.Key] == p.Value))
            && fs.Config.Factions["gondor"].HirePrices["recruit"] == 5 && fs.Config.Factions["gondor"].HirePrices["captain"] == 40 && fs.Config.Factions["gondor"].HirePrices["archer"] == 15
            && fs.Config.Factions["mordor"].HirePrices["recruit"] == 5 && fs.Config.Factions["mordor"].HirePrices["captain"] == 29,
            "every faction costs its tier's prices by default: Men 5, 15 and 40 coins and 15 for an archer, Mordor orcs 5, 12, 29 and 12 (0.0.108)");
        Check(!fs.DescribeFactions().Contains(", hire ") && !fs.DescribeFactions().Contains("coinage") && fs.DescribeFactions().Contains("gondor: aggressive, friendly races"),
            "0.0.89 (owner): /recruits faction list shows each faction's stance, races and groups, without unit prices or hire coin");
        var priced = Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>("{ \"Factions\": { \"gondor\": { \"HireCoin\": \"coinage:coin-gold-md\", \"HirePrices\": { \"Captain\": 60 } }, \"mordor\": {} } }");
        SetLegacy(fs, priced);
        Check(fs.Config.Factions["gondor"].HireCoin == "coinage:coin-gold-md" && fs.Config.Factions["gondor"].HirePrices["captain"] == 60 && fs.Config.Factions["gondor"].HirePrices["recruit"] == 5 && fs.Config.Factions["gondor"].HirePrices.Count == 4, "a faction's own coin and prices from the file; ranks it leaves out cost the default");
        Check(fs.Config.Factions["mordor"].HireCoin == "game:gear-rusty" && fs.Config.Factions["mordor"].HirePrices["warrior"] == 12, "factions without prices in the file get their tier's defaults");
        // instructions
        var withText = FactionConfig.FromFile(null, out _);
        string text = Newtonsoft.Json.JsonConvert.SerializeObject(withText, Newtonsoft.Json.Formatting.Indented);
        Check(text.TrimStart('{', ' ', '\r', '\n').StartsWith("\"#Instructions\"") && withText.Instructions.SequenceEqual(FactionConfig.InstructionText), "the file starts with #Instructions");
        var staleText = Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(text.Replace("After editing", "OLD TEXT"));
        var refreshed = FactionConfig.FromFile(staleText, out bool storeText);
        Check(storeText && refreshed.Instructions.SequenceEqual(FactionConfig.InstructionText), "changed #Instructions are rewritten");
        FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(text), out bool storeSame);
        Check(!storeSame, "a current file with current instructions is left alone");
        File.WriteAllText("factions-default.json", text);

        // ---- hiring (0.0.37) ----
        Check(types.Count == 115, "every NPC has the recruit behavior on both sides and the follow task (checked per file above)");
        var lang = JObject.Parse(File.ReadAllText(Path.Combine(argv[0], "..", "lang", "en.json")));
        var factionIds = FactionConfig.DefaultFactions().Keys.ToList();
        Check(factionIds.All(f => lang["talk-greet-" + f + "-1"] != null && lang["talk-greet-" + f + "-2"] != null && lang["talk-hired-" + f] != null
            && Enumerable.Range(1, 3).All(i => lang["talk-ask-" + f + "-" + i] != null && lang["talk-answer-" + f + "-" + i] != null)), "every faction has 2 greetings, a hired line and 3 questions with answers");
        Check(new[] { "talk-opt-hire", "talk-opt-hire-free", "talk-opt-ask", "talk-opt-follow", "talk-opt-hold", "talk-opt-guard", "talk-opt-dismiss", "talk-opt-dismiss-yes", "talk-opt-dismiss-no", "talk-opt-bye",
            "talk-greet-1", "talk-stranger-1", "talk-own-1", "talk-others-1", "talk-stay-1", "talk-dismiss-ask", "talk-hired", "talk-full", "talk-poor", "talk-nocoin", "talk-follow", "talk-hold", "talk-guard", "talk-dismissed",
            "recruit-yours", "recruit-yours-guard", "recruit-of", "order-follow", "order-hold", "order-guard", "help-talk", "help-orders", "recruit-died", "talk-stranger-shire-1" }.All(k => lang[k] != null), "every menu line and reply the code uses is in the lang file");
        // 0.0.74 (user, 2026-11-04: "no mention of the word hirelings in this mod").
        Check(lang.Properties().All(p => !p.Name.ToLowerInvariant().Contains("hireling") && !((string)p.Value).ToLowerInvariant().Contains("hireling"))
            && lang.Properties().Any(p => ((string)p.Value).Contains("/recruits")) && !lang.Properties().Any(p => ((string)p.Value).Contains("/hirelings")), "no lang line says hireling, and the command is /recruits");
        Check(lang.Properties().All(p => !((string)p.Value).Contains("{") || System.Text.RegularExpressions.Regex.IsMatch((string)p.Value, @"^([^{}]|\{[0-7]\})*$")), "lang placeholders are only {0} to {7} (6: the 0.0.90 room line, 7: the 0.0.91 roster line)");

        Check(new FactionConfig().MaxRecruits == 16 && new FactionConfig().MaxFollowers == 5 && FactionConfig.InstructionText.Any(l => l.Contains("MaxRecruits") && l.Contains("MaxFollowers")) && !FactionConfig.InstructionText.Any(l => l.Contains("isn't built")), "players may hire 16 and lead 5 at a time by default, and #Instructions explain both");
        Check(!FactionConfig.InstructionText.Any(l => l.ToLowerInvariant().Contains("hireling")), "#Instructions never say hireling");
        Check(FactionConfig.InstructionText.Any(l => l.Contains("game:gear-rusty")) && !FactionConfig.InstructionText.Any(l => l.Contains("coinage:coin-")),
            "the #Instructions name the planchet, not a coin that doesn't exist");
        Check(new[] { "talk-hired-guard", "talk-followfull", "cmd-follow-full" }.All(k => lang[k] != null), "the follower-cap replies are in the lang file");
        var defs = new FactionConfig(); defs.FillDefaults();
        Check(HiringSystem.PriceFor(defs.Factions["gondor"], "warrior") == 15 && HiringSystem.PriceFor(defs.Factions["gondor"], "Captain") == 40 && HiringSystem.PriceFor(defs.Factions["gondor"], null) == 5
            && HiringSystem.PriceFor(defs.Factions["gondor"], "farmer") == 5 && HiringSystem.PriceFor(null, "captain") == 40, "price by rank; rankless and unknown ranks cost a recruit's price");
        var free = new FactionConfig { Factions = new Dictionary<string, FactionDef> { ["shire"] = new FactionDef { HirePrices = new Dictionary<string, int> { ["recruit"] = 0 } } } }; free.FillDefaults();
        Check(HiringSystem.PriceFor(free.Factions["shire"], "recruit") == 0 && HiringSystem.PriceFor(free.Factions["shire"], "warrior") == 15, "a price of 0 makes hiring free");
        Check(HiringSystem.RankOf(Npc("test-gondor-captain")) == "captain" && HiringSystem.RankOf(Npc("test-uruk")) == null && HiringSystem.RankOf(Npc("test-hobbit")) == "recruit", "the rank comes from the NPC type");

        fs = new FactionSystem(); SetLegacy(fs, new FactionConfig());
        var hiring = new HiringSystem { Factions = fs };
        fs.Hiring = hiring;
        TestAgent Wild(string code) { var e = Npc(code); e.EntityId = 30000 + nextId++; return e; }
        TestAgent Hired(string code, string owner) { var e = Wild(code); Recruit.Bind(e, owner, "owner"); return e; }
        var lord = Member(null, "gondor"); string uid = lord.PlayerUID;
        hiring.SetOwner(uid, new HiringOwner { Name = "Lord", EntityId = lord.EntityId });
        var guard = Hired("test-gondor-warrior", uid); var guard2 = Hired("test-gondor-recruit", uid);
        var wildGondor = Wild("test-gondor-captain"); var wildMordor = Wild("test-mordororc-warrior"); var wildRohan = Wild("test-rohan-recruit"); var hobbitNpc = Wild("test-hobbit");
        var drifter = Mob("game:drifter-normal"); var animal = Mob("game:wolf-male");
        Check(Recruit.OwnerUidOf(guard) == uid && Recruit.OwnerUidOf(wildGondor) == null && Recruit.OwnerUidOf(lord) == null && Recruit.OrderOf(guard) == Recruit.Follow, "a hired NPC knows its owner and starts with Follow me");
        Recruit.SetOrder(guard2, Recruit.Hold);
        Check(Recruit.OrderOf(guard2) == Recruit.Hold && Recruit.HoldPos(guard2) != null && Recruit.HoldPos(guard) == null, "Hold your position remembers the spot");
        {
            // 0.0.114: a removal sends only that key (a full resend rebuilds the NPC's model on every client).
            var server = new SyncedTreeAttribute(); server.SetDouble("a", 1); server.SetDouble("b", 2); server.MarkClean();
            Recruit.DropWatched(server, "missing");
            bool quiet = !server.AllDirty && !server.PartialDirty;
            Recruit.DropWatched(server, "a");
            server.GetDirtyPathData(out string[] paths, out byte[][] data);
            var client = new SyncedTreeAttribute(); client.SetDouble("a", 1); client.SetDouble("b", 2);
            for (int i = 0; i < paths.Length; i++) client.PartialUpdate(paths[i], data[i]);
            guard.WatchedAttributes.MarkClean(); guard2.WatchedAttributes.MarkClean();
            Recruit.SetOrder(guard, Recruit.Follow); Recruit.SetFollowSlot(guard, null);
            bool looseQuiet = !guard.WatchedAttributes.AllDirty;
            Recruit.SetOrder(guard2, Recruit.Follow);
            Check(quiet && !server.AllDirty && paths.SequenceEqual(new[] { "a" }) && !server.HasAttribute("a") && !client.HasAttribute("a") && client.HasAttribute("b")
                && looseQuiet && !guard2.WatchedAttributes.AllDirty && Recruit.HoldPos(guard2) == null,
                "0.0.114: removing a recruit's watched key sends just that key and the client deletes it; a missing key sends nothing, so loose followers' orders resend nothing");
            Recruit.SetOrder(guard2, Recruit.Hold);
            TreeAttribute Gear(int durability) { var t = new TreeAttribute(); var slot = new TreeAttribute(); slot.SetInt("durability", durability); t["slot0"] = slot; t.SetInt("qslots", 15); return t; }
            Check(GearSyncGuard.SameTree(Gear(40), Gear(40)) && !GearSyncGuard.SameTree(Gear(40), Gear(39)),
                "0.0.114: an NPC's gear is resent only when it differs from what players already have (a save no longer rebuilds every NPC's model)");
            var benchFrames = Enumerable.Repeat(10.0, 199).Append(50.0).Append(30.0).ToList();
            string bench = BenchStats.Summarize(benchFrames);
            Check(bench == "avg 97.1 FPS (10.3 ms), 1% low 25.0 FPS (40.0 ms), worst frame 50 ms, 201 frames" && BenchStats.Summarize(new List<double>()) == "no frames timed"
                && BenchStats.Summarize(new List<double> { 40 }, "ticks/s", "tick") == "avg 25.0 ticks/s (40.0 ms), 1% low 25.0 ticks/s (40.0 ms), worst tick 40 ms, 1 ticks",
                "0.0.114: /recruits bench reports average FPS, the 1% low as the slowest 1% of frames, and the worst frame (got: " + bench + ")");
        }
        Check(!fs.WantsToAttack("gondor", guard) && !fs.WantsToAttack("shire", guard) && fs.WantsToAttack("mordor", guard) && fs.WantsToAttack("rohan", guard), "wild NPCs treat a recruit as they treat its owner: Gondor and the Shire leave a Gondor member's recruit alone, Mordor and Rohan attack it");
        Check(!fs.RecruitWantsToAttack(guard, uid, lord) && !fs.RecruitWantsToAttack(guard, uid, guard2) && !fs.RecruitWantsToAttack(guard, uid, guard), "a recruit never targets its owner or the owner's other recruits");
        Check(fs.RecruitWantsToAttack(guard, uid, wildMordor) && fs.RecruitWantsToAttack(guard, uid, wildRohan) && fs.RecruitWantsToAttack(guard, uid, drifter), "...attacks NPCs that would attack its owner, and monsters, on sight");
        Check(!fs.RecruitWantsToAttack(guard, uid, wildGondor) && !fs.RecruitWantsToAttack(guard, uid, hobbitNpc) && !fs.RecruitWantsToAttack(guard, uid, animal), "...leaves its owner's faction, the Shire and animals alone");
        var stranger = Player(null); var theirs = Hired("test-mordororc-warrior", stranger.PlayerUID);
        Check(!fs.RecruitWantsToAttack(guard, uid, stranger) && fs.RecruitWantsToAttack(guard, uid, theirs) && fs.RecruitWantsToAttack(theirs, stranger.PlayerUID, lord), "0.0.102: a player of no faction is left alone, but their recruit of an enemy faction is fought (and goes for the Gondor member too)");
        Check(fs.RecruitWantsToAttack(theirs, stranger.PlayerUID, guard), "...both ways: the faction-less player's Mordor orc goes for the Gondor member's recruit");
        // 0.0.102 (owner: two players' armies on Aggressive must fight): enemy-faction players and their recruits on sight.
        var enemyLord = Member(null, "mordor"); var enemyOrc = Hired("test-mordororc-warrior", enemyLord.PlayerUID); var enemyMan = Hired("test-gondor-recruit", enemyLord.PlayerUID);
        Check(fs.RecruitWantsToAttack(guard, uid, enemyLord) && fs.RecruitWantsToAttack(guard, uid, enemyOrc) && fs.RecruitWantsToAttack(enemyOrc, enemyLord.PlayerUID, guard) && fs.RecruitWantsToAttack(enemyOrc, enemyLord.PlayerUID, lord),
            "0.0.102: a Gondor member's recruits attack a Mordor member and their recruits on sight, and the other way round");
        Check(!fs.RecruitWantsToAttack(guard, uid, enemyMan), "...but not a Gondor-looking recruit of theirs (the same faction counts as allied)");
        var friendGondor = Member(null, "gondor"); var friendsMan = Hired("test-gondor-warrior", friendGondor.PlayerUID);
        var shireling = Member(null, "shire"); var shiresHobbit = Hired("test-hobbit", shireling.PlayerUID);
        Check(!fs.RecruitWantsToAttack(guard, uid, friendGondor) && !fs.RecruitWantsToAttack(guard, uid, friendsMan) && !fs.RecruitWantsToAttack(guard, uid, shireling) && !fs.RecruitWantsToAttack(guard, uid, shiresHobbit),
            "...nor another Gondor member or theirs, nor a member of the defensive Shire or theirs");
        Check(fs.RecruitWantsToAttack(shiresHobbit, shireling.PlayerUID, guard) && fs.RecruitWantsToAttack(shiresHobbit, shireling.PlayerUID, lord), "...while the Shire member's aggressive recruits go for Gondor's, whose wild NPCs would attack them on sight");
        Recruit.SetStance(guard, Recruit.Defensive);
        Check(!fs.RecruitWantsToAttack(guard, uid, enemyLord) && !fs.RecruitWantsToAttack(guard, uid, enemyOrc), "...defensive recruits still only fight back");
        Recruit.SetStance(guard, Recruit.Aggressive);
        hiring.RecordTeamGrudge(uid, stranger); hiring.RecordTeamGrudge(uid, animal); hiring.RecordTeamGrudge(uid, wildGondor);
        Check(fs.RecruitWantsToAttack(guard, uid, stranger) && fs.RecruitWantsToAttack(guard2, uid, animal) && fs.RecruitWantsToAttack(guard, uid, wildGondor), "every recruit of the owner fights whoever hurt the owner or one of them, even of their own faction");
        Check(!fs.RecruitWantsToAttack(theirs, stranger.PlayerUID, animal), "...other owners' recruits don't");
        hiring.NowForTests += 31000;
        Check(!fs.RecruitWantsToAttack(guard, uid, stranger) && !fs.RecruitWantsToAttack(guard, uid, animal), "...for GrudgeSeconds");
        // A Mordor-group player's orc: allied Isengard still attacks it, since Isengard attacks that player.
        var orcBoss = Player("lrc:orc"); hiring.SetOwner(orcBoss.PlayerUID, new HiringOwner { Name = "Boss", Race = "lrc:orc", Groups = new List<string> { "barad-dur" }, EntityId = orcBoss.EntityId });
        var cfgOrc = new FactionConfig(); cfgOrc.FillDefaults(); cfgOrc.Factions["mordor"].FriendlyGroups.Add("barad-dur"); SetLegacy(fs, cfgOrc);
        var grunt = Hired("test-mordororc-recruit", orcBoss.PlayerUID);
        Check(!fs.WantsToAttack("mordor", grunt) && fs.WantsToAttack("isengard", grunt) && fs.RecruitWantsToAttack(grunt, orcBoss.PlayerUID, Wild("test-uruk-warrior")), "a recruit keeps its looks, not its friends: allied Isengard fights a Mordor member's orc, and the orc fights back");
        // Offline owners: the race and groups last seen still count.
        var gone = Player("lrc:longbeard"); hiring.SetOwner(gone.PlayerUID, new HiringOwner { Name = "Gone", Race = "lrc:longbeard", EntityId = 0 });
        var axe = Hired("test-dwarf-warrior", gone.PlayerUID);
        Check(!fs.WantsToAttack("dwarves", axe) && fs.WantsToAttack("gondor", axe), "an offline dwarf's recruit: dwarves leave it alone, Gondor attacks it");
        var lost = Hired("test-elf-warrior", "nobody-knows");
        Check(fs.WantsToAttack("elves", lost) && !fs.WantsToAttack("shire", lost), "a recruit whose owner isn't on record is a stranger to every faction");
        // Hurting a faction: it fights the owner's recruits too, and grudges hold against recruits of the same faction.
        fs.NowForTests = hiring.NowForTests;
        fs.RecordAttack("gondor", lord);
        Check(fs.WantsToAttack("gondor", guard), "a faction the owner hurt fights the owner's recruits");
        fs.RecordAttack("gondor", guard2);
        Check(fs.HasGrudge("gondor", guard2.EntityId), "a Gondor recruit that hurt a wild Gondor NPC is fought back, though they share a faction");
        fs.RecordAttack("gondor", wildGondor);
        Check(!fs.HasGrudge("gondor", wildGondor.EntityId), "...while wild NPCs of one faction still never hold grudges against each other");

        // ---- 0.0.38: stances, raid, disengaging, formations, portraits, names ----
        fs = new FactionSystem(); SetLegacy(fs, new FactionConfig());
        hiring = new HiringSystem { Factions = fs }; fs.Hiring = hiring;
        var boss = Member(null, "gondor"); string bu = boss.PlayerUID;
        hiring.SetOwner(bu, new HiringOwner { Name = "Boss", EntityId = boss.EntityId });
        var sword = Hired("test-gondor-warrior", bu);
        TestAgent Beast(string code) {
            var p = new EntityProperties { Code = new AssetLocation(code), Server = new EntityServerProperties(new[] { JsonObject.FromJson("{ \"code\": \"taskai\" }") }, new Dictionary<string, JsonObject>()) };
            var e = new TestAgent(); e.SetProps(p); SetCode(e, p.Code); e.EntityId = 40000 + nextId++; return e;
        }
        var orcNpc = Wild("test-mordororc-warrior"); var shireNpc = Wild("test-hobbit"); var rohanNpc = Wild("test-rohan-recruit"); var ownNpc = Wild("test-gondor-recruit");
        var monster = Mob("game:drifter-normal"); var deer = Beast("game:deer-redbrocket-male"); var stand = Mob("game:armorstand");
        var outsider = Player(null); var fellow = Member(null, "gondor");
        Check(Recruit.StanceOf(sword) == Recruit.Aggressive && Recruit.FormationOf(sword) == Formation.Loose && Recruit.FollowSlot(sword) == null, "recruits start aggressive, in no formation");
        Check(fs.RecruitWantsToAttack(sword, bu, rohanNpc) && fs.RecruitWantsToAttack(sword, bu, monster) && !fs.RecruitWantsToAttack(sword, bu, shireNpc)
            && !fs.RecruitWantsToAttack(sword, bu, deer) && !fs.RecruitWantsToAttack(sword, bu, outsider) && !fs.RecruitWantsToAttack(sword, bu, ownNpc), "aggressive (0.0.37's behavior): enemies and monsters on sight; not the Shire, animals, players or its own faction");
        Recruit.SetStance(sword, Recruit.Passive);
        hiring.RecordTeamGrudge(bu, orcNpc);
        Check(!fs.RecruitWantsToAttack(sword, bu, monster) && !fs.RecruitWantsToAttack(sword, bu, rohanNpc) && !fs.RecruitWantsToAttack(sword, bu, orcNpc), "passive: never attacks, not even whoever hurt the team");
        Recruit.SetStance(sword, Recruit.Defensive);
        Check(fs.RecruitWantsToAttack(sword, bu, orcNpc) && !fs.RecruitWantsToAttack(sword, bu, rohanNpc) && !fs.RecruitWantsToAttack(sword, bu, monster), "defensive: only whoever hurt the owner or a recruit (or is going for them)");
        Recruit.SetStance(sword, Recruit.Raid);
        Check(fs.RecruitWantsToAttack(sword, bu, outsider) && !fs.RecruitWantsToAttack(sword, bu, fellow) && !fs.RecruitWantsToAttack(sword, bu, boss), "raid: players who aren't allied, not fellow members or the owner");
        Check(fs.RecruitWantsToAttack(sword, bu, shireNpc) && fs.RecruitWantsToAttack(sword, bu, deer) && fs.RecruitWantsToAttack(sword, bu, monster) && fs.RecruitWantsToAttack(sword, bu, rohanNpc)
            && !fs.RecruitWantsToAttack(sword, bu, ownNpc) && !fs.RecruitWantsToAttack(sword, bu, stand), "raid: neutral NPCs, animals and enemies too; not its own faction, nor things without a mind of their own");
        var fellowsMan = Hired("test-gondor-recruit", fellow.PlayerUID); var outsidersOrc = Hired("test-mordororc-recruit", outsider.PlayerUID);
        Check(!fs.RecruitWantsToAttack(sword, bu, fellowsMan) && fs.RecruitWantsToAttack(sword, bu, outsidersOrc), "raid: an allied player's recruits are spared, others' aren't");
        var tame = Beast("game:horse"); var tree = new TreeAttribute(); tree.SetString("uid", bu); tame.WatchedAttributes["ownedby"] = tree;
        var fellowsHorse = Beast("game:elk"); var tree2 = new TreeAttribute(); tree2.SetString("uid", fellow.PlayerUID); fellowsHorse.WatchedAttributes["ownedby"] = tree2;
        Check(!fs.RecruitWantsToAttack(sword, bu, tame) && !fs.RecruitWantsToAttack(sword, bu, fellowsHorse), "raid: the owner's and allies' own animals are spared");
        var groupie = Player(null); hiring.SetOwner(groupie.PlayerUID, new HiringOwner { Name = "G", Groups = new List<string> { "fellowship" } });
        hiring.Owner(bu).Groups = new List<string> { "fellowship" };
        Check(!fs.RecruitWantsToAttack(sword, bu, groupie) && fs.AlliedPlayers(bu, groupie.PlayerUID) && !fs.AlliedPlayers(bu, outsider.PlayerUID), "raid: players sharing a /group with the owner are allies");
        var cfgA = new FactionConfig(); cfgA.FillDefaults();
        cfgA.Relations.First(r => (r.A == "gondor" && r.B == "rohan") || (r.A == "rohan" && r.B == "gondor")).Relation = "ally"; SetLegacy(fs, cfgA);
        Check(!fs.RecruitWantsToAttack(sword, bu, rohanNpc) && !fs.RecruitWantsToAttack(sword, bu, Member(null, "rohan")) && fs.RecruitWantsToAttack(sword, bu, Wild("test-dale-recruit")), "raid: allied factions' NPCs and members are spared, the rest aren't");
        SetLegacy(fs, new FactionConfig());
        Recruit.SetStance(sword, Recruit.Aggressive);
        hiring.SetDisengaging(sword);
        Check(!fs.RecruitWantsToAttack(sword, bu, monster) && !fs.RecruitWantsToAttack(sword, bu, orcNpc) && hiring.Disengaging(sword), "after a movement order a recruit starts no fights for a few seconds");
        hiring.NowForTests += HiringSystem.DisengageMs + 1;
        Check(fs.RecruitWantsToAttack(sword, bu, monster) && !hiring.Disengaging(sword), "...then fights again");
        Recruit.SetStance(sword, "nonsense");
        Check(Recruit.StanceOf(sword) == Recruit.Aggressive, "an unknown stance counts as aggressive");
        // 0.0.77: followers in a formation fight within 12 blocks of the leader and break off past that until back within
        // 4; loose followers have no such limit.
        {
            Recruit.SetStance(sword, Recruit.Aggressive);
            Recruit.SetOrder(sword, Recruit.Follow);
            sword.WatchedAttributes.SetString(Recruit.FormationAttr, Formation.Line);
            boss.Pos.SetPos(0, 0, 0);
            var nearFoe = Wild("test-mordororc-warrior"); nearFoe.Pos.SetPos(10, 0, 0);
            var farFoe = Wild("test-mordororc-recruit"); farFoe.Pos.SetPos(15, 0, 0);
            sword.Pos.SetPos(5, 0, 0);
            Check(!hiring.UpdateFollowFight(sword, boss) && !hiring.Regrouping(sword) && fs.RecruitWantsToAttack(sword, bu, nearFoe) && fs.RecruitWantsToAttack(sword, bu, farFoe),
                "0.0.84 (owner: \"remove the limit\"): a follower in a formation fights enemies more than 12 blocks from its leader too");
            sword.Pos.SetPos(13, 0, 0);
            Check(hiring.UpdateFollowFight(sword, boss) && hiring.Regrouping(sword) && hiring.Disengaging(sword) && !fs.RecruitWantsToAttack(sword, bu, nearFoe),
                "...more than 12 blocks from its leader it breaks off and starts no fight");
            sword.Pos.SetPos(6, 0, 0);
            bool still = !hiring.UpdateFollowFight(sword, boss) && hiring.Regrouping(sword);
            sword.Pos.SetPos(3, 0, 0);
            Check(still && !hiring.UpdateFollowFight(sword, boss) && !hiring.Regrouping(sword) && fs.RecruitWantsToAttack(sword, bu, nearFoe),
                "...and fights again only once back within 4 blocks");
            sword.WatchedAttributes.SetString(Recruit.FormationAttr, Formation.Loose);
            sword.Pos.SetPos(30, 0, 0);
            Check(!hiring.UpdateFollowFight(sword, boss) && !hiring.Regrouping(sword) && fs.RecruitWantsToAttack(sword, bu, farFoe) && !hiring.BeyondFollowRange(sword, farFoe),
                "a loose follower never breaks off, wherever the fight goes");
            sword.WatchedAttributes.SetString(Recruit.FormationAttr, Formation.Line);
            Check(!hiring.UpdateFollowFight(sword, null) && !hiring.Regrouping(sword) && !hiring.BeyondFollowRange(sword, farFoe),
                "without an online leader the follow limit doesn't apply");
            Recruit.SetOrder(sword, Recruit.Hold);
            Check(!hiring.UpdateFollowFight(sword, boss) && !hiring.BeyondFollowRange(sword, farFoe), "holders keep their own rules");
            sword.WatchedAttributes.SetString(Recruit.FormationAttr, Formation.Loose);
            Recruit.SetOrder(sword, Recruit.Follow);
            sword.Pos.SetPos(0, 0, 0);
        }
        Recruit.SetHoldAt(sword, new Vec3d(10, 64, 10)); Recruit.SetFollowSlot(sword, new Vec2d(1, -3)); Recruit.SetStance(sword, Recruit.Raid);
        Check(Recruit.OrderOf(sword) == Recruit.Hold && Recruit.HoldPos(sword).X == 10 && Recruit.FollowSlot(sword).Y == -3, "orders remember the held spot and the place in a formation");
        Recruit.Release(sword);
        // ---- 0.0.49: shared guards a group mate may borrow ----
        var lent = Hired("test-gondor-recruit", "uidA");
        Check(Recruit.LeaderUidOf(lent) == "uidA" && !Recruit.IsShared(lent) && Recruit.BorrowerUidOf(lent) == null, "a new recruit is led by its owner and not shared");
        Recruit.SetShared(lent, true); Recruit.SetGuardAt(lent, new Vec3d(1, 2, 3)); Recruit.Lend(lent, "uidB", "bob");
        Check(Recruit.LeaderUidOf(lent) == "uidB" && Recruit.OwnerUidOf(lent) == "uidA" && Recruit.IsShared(lent), "a borrowed shared guard is led by the borrower and stays the owner's");
        Recruit.ClearBorrower(lent);
        Check(Recruit.LeaderUidOf(lent) == "uidA" && Recruit.IsShared(lent) && Recruit.OrderOf(lent) == Recruit.Guard, "given back, it is a shared guard of its owner again");
        Recruit.SetShared(lent, false);
        Check(!Recruit.IsShared(lent), "the owner's own orders make it private");
        Recruit.SetShared(lent, true); Recruit.Lend(lent, "uidB", "bob"); Recruit.Release(lent);
        Check(Recruit.BorrowerUidOf(lent) == null && !Recruit.IsShared(lent) && Recruit.OwnerUidOf(lent) == null, "dismissing clears the share and the borrower");
        // ---- 0.0.50: guards stand in the R-menu formation around their post ----
        var postGuard = Hired("test-gondor-recruit", "uidA");
        Recruit.SetGuardAt(postGuard, new Vec3d(5, 60, 5)); Recruit.SetGuardSpot(postGuard, new Vec3d(7, 60, 4));
        Check(Recruit.OrderOf(postGuard) == Recruit.Guard && Recruit.HasGuardSpot(postGuard) && Recruit.GuardSpot(postGuard).X == 7 && Recruit.HoldPos(postGuard).X == 5, "a guard in formation keeps its post and has its own place");
        Recruit.SetGuardAt(postGuard, new Vec3d(9, 60, 9));
        Check(!Recruit.HasGuardSpot(postGuard) && Recruit.HoldPos(postGuard).X == 9, "a new Guard order without a formation patrols again");
        Recruit.SetGuardSpot(postGuard, new Vec3d(1, 60, 1)); Recruit.SetOrder(postGuard, Recruit.Follow);
        Check(!Recruit.HasGuardSpot(postGuard), "following drops the guard place");
        // 0.0.51: hire cap 16; formation spots carry a facing
        Recruit.SetGuardAt(postGuard, new Vec3d(5, 60, 5)); Recruit.SetGuardSpot(postGuard, new Vec3d(6, 60, 6)); Recruit.SetFacing(postGuard, 1, 0);
        Check(Recruit.HasFacing(postGuard) && Math.Abs(Recruit.FacingYaw(postGuard) - Math.PI / 2) < 1e-4, "a guard in formation faces the formation's way (east is yaw pi/2)");
        Recruit.SetGuardAt(postGuard, new Vec3d(5, 60, 5));
        Check(!Recruit.HasFacing(postGuard), "patrolling again, it may look around");
        Check(Recruit.OwnerUidOf(sword) == null && Recruit.FollowSlot(sword) == null && Recruit.StanceOf(sword) == Recruit.Aggressive && Recruit.HoldPos(sword) == null, "dismissing clears every order");

        bool shapesOk = true, followOk = true, assignOk = true;
        foreach (string f in Formation.All) {
            for (int n = 1; n <= 9; n++) {
                var o = Formation.Offsets(f, n);
                double min = double.MaxValue;
                for (int i = 0; i < o.Count; i++) for (int j = i + 1; j < o.Count; j++) min = Math.Min(min, Math.Sqrt(Math.Pow(o[i].X - o[j].X, 2) + Math.Pow(o[i].Y - o[j].Y, 2)));
                if (o.Count != n || min < (f == Formation.Loose ? 1.2 : Formation.Spacing * 0.99)) { shapesOk = false; Console.WriteLine("  " + f + " x" + n + ": " + o.Count + " spots, closest " + min); }
                var fo = Formation.FollowOffsets(f, n);
                if (fo.Count != n || (f == Formation.Circle ? fo.Any(v => Math.Sqrt(v.X * v.X + v.Y * v.Y) < Formation.FollowGap - 1e-6) : fo.Any(v => v.Y > -Formation.FollowGap + 1e-6))) { followOk = false; Console.WriteLine("  follow " + f + " x" + n); }
                var members = Enumerable.Range(0, n).Select(i => new Vec3d(i * 3 - 4, 64, (i * 7) % 5)).ToList();
                var spots = o.Select(v => Formation.World(new Vec3d(0, 64, 0), 0.6, 0.8, v)).ToList();
                var spotOf = Formation.Assign(members, spots);
                if (spotOf.Distinct().Count() != n || spotOf.Any(x => x < 0 || x >= n)) assignOk = false;
                if (o.Zip(spots, (v, w) => Math.Abs(Math.Sqrt(v.X * v.X + v.Y * v.Y) - Math.Sqrt(w.X * w.X + w.Z * w.Z))).Any(d => d > 1e-6)) assignOk = false;
            }
        }
        Check(shapesOk, "every formation gives each of 1 to 9 recruits its own spot, " + Formation.Spacing + " blocks apart (loose clusters a little closer)");
        Check(followOk, "following formations walk behind the owner; a circle goes around them");
        Check(assignOk, "every recruit gets a different spot, and turning a formation keeps its shape");
        Check(Formation.Valid("bogus") == Formation.Loose && Formation.Valid(null) == Formation.Loose && Formation.Valid("wedge") == Formation.Wedge, "unknown formations count as loose");
        var lineRight = Formation.World(new Vec3d(0, 0, 0), 0, 1, new Vec2d(0, 2));
        Check(Math.Abs(lineRight.Z - 2) < 1e-9 && Math.Abs(lineRight.X) < 1e-9, "forward offsets go the way the group faces");

        Check(new[] { "hotkey-orders", "orders-none", "orders-title", "orders-page-movement", "orders-page-combat", "orders-follow", "orders-hold", "orders-move", "orders-guard", "orders-formation", "orders-hint",
            "cmd-none", "cmd-follow", "cmd-hold", "cmd-move", "cmd-guard", "cmd-notarget", "cmd-toofar", "cmd-formation", "cmd-stance" }.All(k => lang[k] != null)
            && Formation.All.All(f => lang["formation-" + f] != null) && Recruit.Stances.All(st => lang["stance-" + st] != null && lang["stance-desc-" + st] != null), "every orders-menu line is in the lang file");
        Check(lang.Properties().Where(p => p.Name.StartsWith("item-creature-") || p.Name.StartsWith("entity-") || p.Name.StartsWith("prefixandcreature-")).All(p => !((string)p.Value).Contains("Test")), "names no longer say Test");
        var items = JObject.Parse(File.ReadAllText(Path.Combine(argv[0], "..", "itemtypes", "creature.json")));
        var entityFiles = Directory.GetFiles(argv[0], "*.json").Select(f => JObject.Parse(File.ReadAllText(f))).ToDictionary(j => (string)j["code"]);
        JToken GearSets(JObject jo) => jo["server"]["behaviors"].FirstOrDefault(b => (string)b["code"] == "lotrnpcs:startinggear")?["sets"];
        // 0.0.78: sets worn only when none of "sets" is complete.
        JToken FallbackSets(JObject jo) => jo["server"]["behaviors"].FirstOrDefault(b => (string)b["code"] == "lotrnpcs:startinggear")?["fallbackSets"];
        JArray PortraitSets(JObject jo) => new JArray(((GearSets(jo) as JArray) ?? new JArray()).Concat((FallbackSets(jo) as JArray) ?? new JArray()).Select(t => t.DeepClone()));
        Check(items["behaviors"].Any(b => (string)b["name"] == "lotrnpcs:npcportrait") && entityFiles.Keys.All(c => items["attributesByType"]["*-" + c]?["lotrnpcsPortrait"]?["model"] != null
            && JToken.DeepEquals(items["attributesByType"]["*-" + c]["lotrnpcsPortrait"]["sets"], PortraitSets(entityFiles[c]))), "every spawn item has a portrait wearing its NPC's armor sets (then its fallback sets, 0.0.78)");
        Check(entityFiles.Values.Where(j => j["attributes"]["lotrnpcsRank"] != null && GearSets(j) != null && (string)j["attributes"]["lotrnpcsFaction"] != "shire").All(j => GearSets(j).Any()), "every armored rank of every faction has faction armor sets, Haradrim, Easterlings and tormented elves included");
        // 0.0.62 Gundabad (work in progress): the bone set from the owner's gundabad mod for every rank, and never a helmet.
        string[] gBone = { "gundabad:armor-body-tierthreebrig-gundabadbonearmor-gundabadbonearmor-none", "gundabad:armor-legs-tierthreebrig-gundabadboneleggings-gundabadboneleggings-none" };
        Check(new[] { "recruit", "warrior", "captain" }.All(r => {
                var g = entityFiles["test-gundabadorc-" + r]; var sets = GearSets(g);
                var fallback = g["server"]["behaviors"].First(b => (string)b["code"] == "lotrnpcs:startinggear")["items"].Select(c => (string)c);
                // 0.0.76 (user: "give the warrior the gundabad helmet / armor from the angmar misty mountains mod"): warriors
                // wear the norc Gundabad set; recruits and captains the bone armor.
                // 0.0.95: warriors also the tusked helmet; captains the norc warlord set with the warlord, tusked or owner's
                // helmet (gundabad-bone-armor 0.3.0), or the bone armor.
                string gHelm = "norc:armor-head-tierfourplate-norchelmetheavy-norchelmetheavy-none", gTusk = "norc:armor-head-tierfourplate-norchelmetheavytwo-norchelmetheavytwo-none";
                string gWarlordHead = "norc:armor-head-tierfiveplate-norchelmetheavier-norchelmetheavier-none", gOwner = "gundabad:armor-head-tierfourplate-gundabadhelmetone-gundabadhelmetone-none";
                string[] gWarrior = { "norc:armor-body-tierfourplate-norcarmorheavy-norcarmorheavy-none", "norc:armor-legs-tierfourplate-norcleggingsheavy-norcleggingsheavy-none" };
                string[] gWarlord = { "norc:armor-body-tierfiveplate-norcarmorheavier-norcarmorheavier-none", "norc:armor-legs-tierfiveplate-norcleggingsheavier-norcleggingsheavier-none" };
                var want = r == "recruit" ? new[] { gBone } : r == "warrior" ? new[] { gWarrior.Prepend(gHelm).ToArray(), gWarrior.Prepend(gTusk).ToArray() }
                    : new[] { gWarlord.Prepend(gWarlordHead).ToArray(), gWarlord.Prepend(gTusk).ToArray(), gWarlord.Prepend(gOwner).ToArray() };
                // 0.0.112: orcs offer only their own unit's helmets (no lower ranks').
                string[] helms = r == "warrior" ? new[] { gHelm, gTusk } : new[] { gWarlordHead, gTusk, gOwner };
                return sets.Count() == want.Length && want.Select((w, i) => sets[i].Select(c => (string)c).SequenceEqual(w)).All(b => b) && fallback.All(c => !c.Contains("armor-head-")) && fallback.Any()
                    && (r == "recruit" ? g["attributes"]["lotrnpcsHelmets"] == null : g["attributes"]["lotrnpcsHelmets"].Select(c => (string)c).SequenceEqual(helms))
                    && (string)g["attributes"]["lotrnpcsFaction"] == "gundabad" && (string)g["attributes"]["lotrnpcsRank"] == r
                    && (string)g["client"]["behaviors"].First(b => (string)b["code"] == "lotrnpcs:presetskinmodel")["skinModel"] == "lotrnpcs:gundabadorc";
            }) && lang.Properties().All(p => !((string)p.Value).Contains("(WIP)")) && SpawnZoneSystem.Factions.Contains("gundabad"),
            "Gundabad orcs: their own race model (0.0.63), bone armor for recruits, the norc Gundabad set for warriors (0.0.76) with the tusked helmet too, captains the warlord set with three helmets (0.0.95; no bone armor since 0.0.96), no helmet without the mod, no \"(WIP)\" (0.0.76), and zones accept them");
        var gParts = Looks.ParseParts(File.ReadAllText(Path.Combine(argv[0], "..", "config", "customplayermodels", "gundabadorc.json")), "gundabadorc");
        var mParts = Looks.ParseParts(File.ReadAllText(Path.Combine(argv[0], "..", "config", "customplayermodels", "mordororc.json")), "mordororc");
        Check(gParts["baseskin"].SequenceEqual(new[] { "orcskin2", "orcskin1", "orcskin7" }) && gParts["face"].SequenceEqual(gParts["baseskin"])
            && gParts.Keys.Where(k => k != "baseskin" && k != "face").All(k => mParts.ContainsKey(k) && gParts[k].SequenceEqual(mParts[k])),
            "0.0.63: Gundabad orcs are the Mordor orc with the three lightest orc skins (" + string.Join(", ", gParts["baseskin"]) + ")");
        var follow = entityFiles["test-gondor-warrior"]["server"]["behaviors"].First(b => (string)b["code"] == "taskai")["aitasks"].First(t => (string)t["code"] == "lotrnpcs:followowner");
        Check((double)follow["walkSpeed"] > 0.025 && (double)follow["runSpeed"] >= 0.05, "followers walk a little faster than a walking player and run at least as fast as a sprinting one");

        // ---- 0.0.39: old names, regeneration, mixed armor, portrait crop ----
        var oldOrc = Wild("test-mordororc-captain"); var tag = new TreeAttribute(); tag.SetString("name", "Test: Mordor orc captain"); oldOrc.WatchedAttributes["nametag"] = tag;
        bool stripped = EntityBehaviorFactionMember.StripTestName(oldOrc);
        Check(stripped && oldOrc.WatchedAttributes.GetTreeAttribute("nametag").GetString("name") == "Mordor orc captain" && !EntityBehaviorFactionMember.StripTestName(oldOrc), "an NPC spawned before 0.0.38 loses the Test: in its name, once");
        var regenCfg = new FactionConfig();
        Check(EntityBehaviorFactionMember.RegenSpeed(regenCfg, 0) == 1 && EntityBehaviorFactionMember.RegenSpeed(regenCfg, 9999) == 1 && EntityBehaviorFactionMember.RegenSpeed(regenCfg, 10000) == 30
            && EntityBehaviorFactionMember.RegenSpeed(regenCfg, long.MaxValue / 4) == 30, "NPCs heal at vanilla speed in a fight and 30 times faster from 10 seconds after their last hit (0.0.41: was 20)");
        Check(22 / (0.01 * 30) / 60 < 1.5 && 48 / (0.01 * 30) / 60 < 3, "...so a hurt recruit is whole again in under 1.5 minutes and a captain in under 3");
        // ---- 0.0.41: the user's outfits, gear slots, faster regen, creative menu, portrait framing ----
        var vanilla = new HashSet<string>(File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "..", "vanilla-armor-codes.txt")).Where(l => l.Length > 0 && !l.StartsWith("#")).Select(l => "game:" + l.Trim()));
        var clothes = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "..", "vanilla-clothes-codes.txt")).Where(l => l.Length > 0 && !l.StartsWith("#")).Select(l => l.Split('\t')).ToDictionary(p => "game:" + p[0], p => p[1]);
        foreach (string c in clothes.Keys) vanilla.Add(c);
        List<List<string>> Outfits(string code) => GearSets(entityFiles[code]).Select(st => st.Select(c => (string)c).ToList()).ToList();
        List<List<string>> FallbackOutfits(string code) => ((FallbackSets(entityFiles[code]) as JArray) ?? new JArray()).Select(st => st.Select(c => (string)c).ToList()).ToList();
        var allCodes = entityFiles.Keys.Where(c => GearSets(entityFiles[c]) != null).SelectMany(c => Outfits(c).Concat(FallbackOutfits(c))).SelectMany(o => o).Distinct().ToList();
        var unknownVanilla = allCodes.Where(c => c.StartsWith("game:") && !vanilla.Contains(GearCode.ItemCode(c))).ToList();
        foreach (string c in unknownVanilla) Console.WriteLine("  not a vanilla item: " + c);
        Check(vanilla.Count > 900 && unknownVanilla.Count == 0 && allCodes.Where(c => c.Contains('@')).All(c => GearCode.Slot(c) != null), "every vanilla armor and clothing code in an outfit is real, and every @slot names a gear slot");
        Check(clothes["game:clothes-nadiya-foot-guard"] == "Nadiyan guard boots" && clothes["game:clothes-nadiya-lowerbody-guard"] == "Nadiyan guard pants" && clothes["game:clothes-nadiya-lowerbody-hunter"] == "Nadiyan hunter pants"
            && clothes["game:clothes-upperbodyover-forgotten"] == "Forgotten noble robe" && clothes["game:clothes-waist-sturdy-leather-belt"] == "Sturdy leather belt" && clothes["game:clothes-shoulder-furwrap"] == "Fur wrap"
            && clothes["game:clothes-face-miner"] == "Miner mask" && clothes["game:clothes-head-rottenking"] == "Rotten king crown" && new[] { "hunter", "peasant1", "peasant2", "peasant3", "shepherd" }.All(c => clothes["game:clothes-nadiya-shoulder-" + c].EndsWith("cape")),
            "the clothing codes are the items the user named");
        Check(GearCode.ItemCode("game:armor-head-chain-iron@face") == "game:armor-head-chain-iron" && GearCode.Slot("game:armor-head-chain-iron@face") == EnumCharacterDressType.Face
            && GearCode.Slot("game:clothes-shoulder-furwrap@Neck") == EnumCharacterDressType.Neck && GearCode.Slot("game:armor-head-chain-iron") == null && GearCode.Slot("x@bogus") == null && GearCode.ItemCode("lotrnpcs:a") == "lotrnpcs:a",
            "gear codes: \"@slot\" names the gear slot, case-insensitive; unknown slots are ignored");
        // Tormented elves (user): the Angmar Ranger tunic and leggings, no helmets, the captain in the Rotten king crown.
        string rangerBody = "angmar:armor-body-tiertwogambeson-angmararmorranger-angmararmorranger-none", rangerLegs = "angmar:armor-legs-tiertwogambeson-angmarleggingsranger-angmarleggingsranger-none";
        Check(new[] { "recruit", "warrior" }.All(r => Outfits("test-tormentedelf-" + r).Count == 1 && Outfits("test-tormentedelf-" + r)[0].SequenceEqual(new[] { rangerBody, rangerLegs }))
            && Outfits("test-tormentedelf-captain").Count == 1 && Outfits("test-tormentedelf-captain")[0].SequenceEqual(new[] { rangerBody, rangerLegs, "game:clothes-head-rottenking" }),
            "tormented elves wear the Angmar Ranger tunic and leggings bareheaded; captains add the Rotten king crown");
        // Easterlings (user): tin bronze brigandine, tin bronze plate, and plate with the Woodland Sentinel helmet over an iron chain coif.
        string[] Tb(string kind) => new[] { "head", "body", "legs" }.Select(p => "game:armor-" + p + "-" + kind + "-tinbronze").ToArray();
        Check(Outfits("test-easterling-recruit").Count == 1 && Outfits("test-easterling-recruit")[0].SequenceEqual(Tb("brigandine"))
            && Outfits("test-easterling-warrior").Count == 1 && Outfits("test-easterling-warrior")[0].SequenceEqual(Tb("plate"))
            && Outfits("test-easterling-captain").Count == 1 && Outfits("test-easterling-captain")[0].SequenceEqual(new[] { "wlel:armor-head-tierthreescale-woodlandhelmetmedium-woodlandhelmetmedium-none", "game:armor-head-chain-iron@face", "game:armor-body-plate-tinbronze", "game:armor-legs-plate-tinbronze" }),
            "Easterlings: brigandine recruits, plate warriors, captains in plate with the Woodland Sentinel helmet over an iron chain coif in the face slot");
        // Haradrim (user), every rank alike: since 0.0.78 their fallback, worn without the haradrimarmor mod.
        bool haradOk = true;
        foreach (string rank in new[] { "recruit", "warrior", "captain", "archer" }) {
            var outfits = FallbackOutfits("test-haradrim-" + rank);
            foreach (var o in outfits) {
                string colour = o.FirstOrDefault(c => c.StartsWith("game:armor-body-tailored-"))?.Split('-')[3];
                bool ok = o.Count == 9 && o.Contains("game:clothes-nadiya-foot-guard") && o.Count(c => c == "game:clothes-nadiya-lowerbody-guard" || c == "game:clothes-nadiya-lowerbody-hunter") == 1
                    && o.Count(c => c.StartsWith("game:clothes-nadiya-shoulder-")) == 1 && (colour == "black" || colour == "red") && o.Contains("game:armor-legs-tailored-" + colour + "-linen")
                    && o.Contains("game:clothes-upperbodyover-forgotten") && o.Contains("game:clothes-waist-sturdy-leather-belt") && o.Contains("game:clothes-shoulder-furwrap@neck") && o.Contains("game:clothes-face-miner");
                if (!ok) { haradOk = false; Console.WriteLine("  haradrim " + rank + ": " + string.Join(", ", o)); }
            }
            var flat = outfits.SelectMany(o => o).ToHashSet();
            if (outfits.Count != 20 || !new[] { "black", "red" }.All(c => flat.Contains("game:armor-body-tailored-" + c + "-linen")) || !flat.Contains("game:clothes-nadiya-lowerbody-hunter") || flat.Count(c => c.StartsWith("game:clothes-nadiya-shoulder-")) != 5) haradOk = false;
        }
        // 0.0.78 (owner: "Use these as skins for the haradrim, mostly as a temporary placeholder"): the haradrimarmor mod.
        string[] Harad(string set, string tier) => new[] { ("head", "helmet"), ("body", "armor"), ("legs", "leggings") }
            .Select(p => $"haradrimarmor:armor-{p.Item1}-{tier}-haradrim{set}{p.Item2}-haradrim{set}{p.Item2}-none").ToArray();
        bool OneSet(string code, string[] want) => Outfits(code).Count == 1 && Outfits(code)[0].SequenceEqual(want);
        Check(OneSet("test-haradrim-recruit", Harad("infantry", "tierthreechain")) && OneSet("test-haradrim-archer", Harad("infantry", "tierthreechain"))
            && OneSet("test-haradrim-warrior", Harad("heavy", "tierfourplate")) && OneSet("test-haradrim-captain", Harad("warlord", "tierfiveplate"))
            && entityFiles.Keys.Where(c => !c.StartsWith("test-haradrim-")).All(c => FallbackSets(entityFiles[c]) == null),
            "Haradrim wear the owner's armor mod (infantry recruits and archers, heavy infantry warriors, warlord captains); only they have fallback sets (0.0.78)");
        Check(new[] { "recruit", "warrior", "captain" }.All(r => ((JArray)entityFiles["test-haradrim-" + r]["attributes"]["lotrnpcsHelmets"]).Select(t => (string)t).First().StartsWith("haradrimarmor:armor-head-")),
            "...and their looks menu offers the mod's helmets");
        {
            string[][] sets = { new[] { "mod:a", "mod:b" } }, fallback = { new[] { "game:x" }, new[] { "game:y", "game:z" } };
            var withMod = EntityBehaviorStartingGear.CompleteSets(sets, fallback, c => true);
            var noMod = EntityBehaviorStartingGear.CompleteSets(sets, fallback, c => !c.StartsWith("mod:"));
            var noneAtAll = EntityBehaviorStartingGear.CompleteSets(sets, fallback, c => false);
            Check(withMod.Count == 1 && withMod[0][0] == "mod:a" && noMod.Count == 2 && noMod.All(st => st[0].StartsWith("game:")) && noneAtAll.Count == 0,
                "with the armor mod only its sets are worn, without it only the fallback sets, never a mix (0.0.78)");
        }
        Check(haradOk, "Haradrim of every rank wear guard boots, guard or hunter pants, a Nadiyan cape, a black or red tailored gambeson, the noble robe, the sturdy belt, the fur wrap (neck slot) and the miner mask; 20 combinations");
        // Creative menu (user): the tab is called LOTR NPCs, and the unranked goblin and Uruk-hai are gone from it
        // (0.0.42: the hobbit came back).
        string[] hidden = { "test-goblin", "test-uruk" };
        // 0.0.71 (user: "have all the units of a faction next to each other"): one variant carries the whole tab as an
        // ordered stack list, since the game would otherwise list by the world's item ids.
        var stackHosts = (JObject)items["creativeinventoryStacksByType"];
        var tabEntry = stackHosts?["*-test-goblin"]?[0];
        var tabCodes = tabEntry?["stacks"].Select(x => ((string)x["code"]).Replace("lotrnpcs:creature-", "")).ToList() ?? new List<string>();
        bool tabsOk = items["creativeinventory"] == null && stackHosts.Count == 1 && tabEntry["tabs"].Select(x => (string)x).SequenceEqual(new[] { "general", "lotrnpcs" })
            && tabEntry["stacks"].All(x => (string)x["type"] == "item") && tabCodes.Count == tabCodes.Distinct().Count()
            && tabCodes.ToHashSet().SetEquals(entityFiles.Keys.Where(c => !hidden.Contains(c)));
        var tabFactions = tabCodes.Select(c => types[c].Attributes["lotrnpcsFaction"].AsString()).ToList();
        bool grouped = tabFactions.Select((f, i) => i == 0 || f == tabFactions[i - 1] || !tabFactions.Take(i).Contains(f)).All(x => x);
        Check(grouped && tabCodes.First() == "test-hobbit" && tabCodes.IndexOf("test-mordororc-recruit") + 6 == tabCodes.IndexOf("test-mordororc-archer") && tabCodes.IndexOf("test-mordororc-captain") + 1 == tabCodes.IndexOf("test-mordororc-rider") && tabCodes.IndexOf("test-mordororc-warrior") + 1 == tabCodes.IndexOf("test-mordororc-guard"),
            "0.0.71: the creative tab lists each faction's units together, recruit, warrior, guard, shock trooper (0.0.113), captain, rider (0.0.117), archer (" + string.Join(" ", tabFactions.Distinct()) + ")");
        Check((string)lang["game:tabname-lotrnpcs"] == "LOTR NPCs" && tabsOk && hidden.All(c => entityFiles.ContainsKey(c) && (bool?)entityFiles[c]["attributes"]["inCreativeInventory"] == false)
            && entityFiles.Keys.Where(c => !hidden.Contains(c)).All(c => entityFiles[c]["attributes"]["inCreativeInventory"] == null), "the creative tab reads LOTR NPCs and leaves out the plain goblin and Uruk-hai, which still exist; the hobbit is in it");
        Check(lang["orders-sharedkey"] != null && ((string)lang["orders-hint"]).Contains("Esc"), "the menu tells how to close it and warns about a shared key");
        Check((double)items["guiTransform"]["scale"] > 1.2, "portraits are drawn bigger in the slot");

        // A wall face 1 high and a face wholly below, cut at 0.4: the wall keeps its top 0.6 with the texture cut to
        // match, and the low face goes.
        var mesh = new Vintagestory.API.Client.MeshData(8, 12);
        mesh.AddVertex(0, 0, 0, 0, 0); mesh.AddVertex(1, 0, 0, 1, 0); mesh.AddVertex(1, 1, 0, 1, 1); mesh.AddVertex(0, 1, 0, 0, 1);
        mesh.AddIndices(0, 1, 2, 0, 2, 3);
        mesh.AddVertex(0, -1, 0, 0, 0); mesh.AddVertex(1, -1, 0, 1, 0); mesh.AddVertex(1, -0.5f, 0, 1, 1); mesh.AddVertex(0, -0.5f, 0, 0, 1);
        mesh.AddIndices(4, 5, 6, 4, 6, 7);
        NpcPortrait.Clip(mesh, 0.4f);
        bool wallOk = Math.Abs(mesh.xyz[1] - 0.4f) < 1e-5 && Math.Abs(mesh.xyz[4] - 0.4f) < 1e-5 && Math.Abs(mesh.Uv[1] - 0.4f) < 1e-5 && Math.Abs(mesh.Uv[3] - 0.4f) < 1e-5
            && mesh.xyz[7] == 1 && mesh.xyz[10] == 1 && mesh.xyz[0] == 0 && mesh.xyz[3] == 1;
        bool lowGone = mesh.Indices.Skip(6).Take(6).Distinct().Count() == 1 && mesh.Indices.Take(6).Distinct().Count() == 4;
        Check(wallOk && lowGone, "portrait crop: faces crossing the cut are shortened with their texture, faces below it vanish");
        Check(GuiDialogRecruitOrders.Rows(Formation.All.Length, 3) == 2 && GuiDialogRecruitOrders.Rows(Recruit.Stances.Length, 2) == 2, "formations and stances sit in two rows so their labels fit");
        Check(NpcPortrait.CropShare > 0.55 && NpcPortrait.CropShare < 0.8, "portraits show the head and the top of the shoulders");

        // Portrait framing (0.0.41): a bust of two boxes, shoulders 16 wide and 4 deep under an 8-wide head, cut and placed
        // as the GUI shows it (scale 1.5, turned 65 degrees). Without a helmet the cut stands on the slot's bottom edge
        // and the bust fills BustHeight of the slot; a tall crest makes it shrink so the crest stays inside.
        float guiScale = (float)(double)items["guiTransform"]["scale"], guiYaw = (float)(double)items["guiTransform"]["rotation"]["y"];
        Vintagestory.API.Client.MeshData Box(Vintagestory.API.Client.MeshData m, float x0, float y0, float z0, float x1, float y1, float z1) {
            int b = m.VerticesCount;
            float[][] c = { new[] { x0, y0, z0 }, new[] { x1, y0, z0 }, new[] { x1, y1, z0 }, new[] { x0, y1, z0 }, new[] { x0, y0, z1 }, new[] { x1, y0, z1 }, new[] { x1, y1, z1 }, new[] { x0, y1, z1 } };
            foreach (var v in c) m.AddVertex(v[0], v[1], v[2], 0, 0);
            int[][] faces = { new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 4, 0, 3, 7 }, new[] { 1, 5, 6, 2 }, new[] { 3, 2, 6, 7 }, new[] { 4, 5, 1, 0 } };
            foreach (var f in faces) m.AddIndices(b + f[0], b + f[1], b + f[2], b + f[0], b + f[2], b + f[3]);
            return m;
        }
        Vintagestory.API.Client.MeshData Bust(bool crest) {
            var m = new Vintagestory.API.Client.MeshData(64, 128);
            Box(m, -8 / 16f, 0, -2 / 16f, 8 / 16f, 12 / 16f, 2 / 16f);
            Box(m, -4 / 16f, 12 / 16f, -4 / 16f, 4 / 16f, 20 / 16f, 4 / 16f);
            if (crest) Box(m, -1 / 16f, 20 / 16f, -1 / 16f, 1 / 16f, 28 / 16f, 1 / 16f);
            return m;
        }
        float[] bareFrame = { -0.5f, 0, -0.25f, 0.5f, 20 / 16f, 0.25f };
        float Screen(float u) => 0.5f + (u - 0.5f) * guiScale / NpcPortrait.SlotUnits;
        (float bottom, float top, float half) Shown(Vintagestory.API.Client.MeshData m) {
            float lo = float.MaxValue, hi = float.MinValue, half = 0, cos = (float)Math.Abs(Math.Cos(guiYaw * Math.PI / 180)), sin = (float)Math.Abs(Math.Sin(guiYaw * Math.PI / 180));
            foreach (int i in m.Indices.Take(m.IndicesCount)) {
                float x = m.xyz[i * 3] - 0.5f, y = m.xyz[i * 3 + 1], z = m.xyz[i * 3 + 2] - 0.5f;
                lo = Math.Min(lo, y); hi = Math.Max(hi, y); half = Math.Max(half, Math.Max(Math.Abs(x * cos + z * sin), Math.Abs(x * cos - z * sin)));
            }
            return (Screen(lo), Screen(hi), half * guiScale / NpcPortrait.SlotUnits);
        }
        var plain = Bust(false); NpcPortrait.Frame(plain, bareFrame, guiScale, guiYaw);
        var crested = Bust(true); NpcPortrait.Frame(crested, bareFrame, guiScale, guiYaw);
        var p0 = Shown(plain); var p1 = Shown(crested);
        Console.WriteLine($"  plain: bottom {p0.bottom:0.000} top {p0.top:0.000} half-width {p0.half:0.000}; crested: bottom {p1.bottom:0.000} top {p1.top:0.000} half-width {p1.half:0.000}");
        Check(Math.Abs(p0.bottom - NpcPortrait.BottomEdge) < 1e-3 && Math.Abs(p0.top - (NpcPortrait.BottomEdge + NpcPortrait.BustHeight)) < 1e-3 && p0.half <= NpcPortrait.HalfWidth + 1e-3,
            "a bare bust stands on the slot's bottom edge and fills the set share of the slot, inside it sideways");
        Check(Math.Abs(p1.bottom - NpcPortrait.BottomEdge) < 1e-3 && p1.top <= NpcPortrait.TopLimit + 1e-3 && p1.top > p0.top && p1.half <= NpcPortrait.HalfWidth + 1e-3, "a tall crest shrinks the portrait so it stays inside the slot, still on the bottom edge");
        Check(NpcPortrait.BottomEdge <= 0 && NpcPortrait.TopLimit < 1 && NpcPortrait.HalfWidth < 0.5, "portrait limits lie inside the slot");
        // Hair (0.0.41): a portrait shows hair unless the race has none, or its spec asks for a style.
        var hair = JObject.Parse("{ code: 'hairbase', type: 'shape', variants: [ { code: 'bald' }, { code: 'balding' }, { code: 'combed' } ] }");
        var orcHair = JObject.Parse("{ code: 'hairbase', type: 'shape', variants: [ { code: 'bald' }, { code: 'balding' }, { code: 'indigenousbalding' } ] }");
        var noSpec = new JsonObject(JObject.Parse("{ parts: { facialexpression: 'angry' } }"));
        var wantsBalding = new JsonObject(JObject.Parse("{ parts: { hairbase: 'balding' } }"));
        var skinLoc = new AssetLocation("lotrnpcs:entity/humanoid/skinparts/human/haradrim/humanskin23");
        Check((string)NpcPortrait.Pick(hair, noSpec, skinLoc)["code"] == "combed" && (string)NpcPortrait.Pick(orcHair, noSpec, skinLoc)["code"] == "bald" && (string)NpcPortrait.Pick(hair, wantsBalding, skinLoc)["code"] == "balding",
            "portraits show hair instead of a bald head unless the race is bald or the spec picks a style");
        var dwarfSpec = items["attributesByType"]["*-test-dwarf-captain"]["lotrnpcsPortrait"]["parts"];
        Check((string)dwarfSpec["beard"] == "brd-box20-full" && (string)dwarfSpec["facialexpression"] == "determined", "dwarf portraits wear a full beard");

        // ---- 0.0.43: 6 less health for every NPC (user) ----
        float JsonMaxHealth(string code) => (float)entityFiles[code]["server"]["behaviors"].First(b => (string)b["code"] == "health")["maxhealth"];
        bool healthOk = entityFiles.Keys.All(c => {
            string rank = (string)entityFiles[c]["attributes"]["lotrnpcsRank"];
            float want = rank == "recruit" ? 22 : rank == "warrior" || rank == "archer" ? 34 : rank == "captain" ? 48 : rank == "guard" ? 43 : rank == "shock" ? 27 : rank == "rider" ? 34 : 19;
            float cur = (float)entityFiles[c]["server"]["behaviors"].First(b => (string)b["code"] == "health")["currenthealth"];
            if (JsonMaxHealth(c) != want || cur != want) { Console.WriteLine("  " + c + ": " + JsonMaxHealth(c) + " health"); return false; }
            return true;
        });
        Check(healthOk && JsonMaxHealth("test-hobbit") == 22 && JsonMaxHealth("test-goblin") == 19, "recruits have 22 health, warriors and archers 34, captains 48, the rankless leftovers 19 (6 less than before); 0.0.113 guards 43 and shock troops 27; 0.0.117 riders 34");
        TestAgent Hurt(float baseMax, float max, float cur) {
            var e = Wild("test-gondor-captain"); var h = new TreeAttribute();
            h.SetFloat("basemaxhealth", baseMax); h.SetFloat("maxhealth", max); h.SetFloat("currenthealth", cur); e.WatchedAttributes["health"] = h; return e;
        }
        float Hp(TestAgent e, string key) => e.WatchedAttributes.GetTreeAttribute("health").GetFloat(key);
        var full = Hurt(54, 54, 54); bool fullChanged = EntityBehaviorFactionMember.SyncMaxHealth(full, 48);
        var wounded = Hurt(54, 54, 30); EntityBehaviorFactionMember.SyncMaxHealth(wounded, 48);
        var nearDead = Hurt(54, 54, 5); EntityBehaviorFactionMember.SyncMaxHealth(nearDead, 48);
        var dead = Hurt(54, 54, 0); EntityBehaviorFactionMember.SyncMaxHealth(dead, 48);
        var buffed = Hurt(54, 56, 56); EntityBehaviorFactionMember.SyncMaxHealth(buffed, 48);
        Check(fullChanged && Hp(full, "basemaxhealth") == 48 && Hp(full, "maxhealth") == 48 && Hp(full, "currenthealth") == 48
            && Hp(wounded, "currenthealth") == 24 && Hp(nearDead, "currenthealth") == 1 && Hp(dead, "currenthealth") == 0 && Hp(buffed, "maxhealth") == 50 && Hp(buffed, "currenthealth") == 50,
            "an NPC spawned with the old health drops to the new maximum on load, keeping its wounds but never dying of it");
        Check(!EntityBehaviorFactionMember.SyncMaxHealth(full, 48) && !EntityBehaviorFactionMember.SyncMaxHealth(Wild("test-gondor-recruit"), 22) && !EntityBehaviorFactionMember.SyncMaxHealth(Hurt(54, 54, 54), 0),
            "...and NPCs already at the new value, without a health record, or of a type without health are left alone");

        // ---- 0.0.44: Guard here, the whitelist, sturdier configs, cached answers ----
        fs = new FactionSystem(); SetLegacy(fs, new FactionConfig());
        hiring = new HiringSystem { Factions = fs }; fs.Hiring = hiring;
        E At<E>(E e, double x, double y, double z) where E : Entity { e.Pos.SetPos(x, y, z); return e; }
        var chief = Member(null, "gondor"); string cu = chief.PlayerUID;
        hiring.SetOwner(cu, new HiringOwner { Name = "Chief", EntityId = chief.EntityId });
        var sentry = At(Hired("test-gondor-warrior", cu), 100, 64, 100);
        Recruit.SetGuardAt(sentry, new Vec3d(100, 64, 100));
        Check(Recruit.OrderOf(sentry) == Recruit.Guard && Recruit.TryGetPost(sentry, out double gx, out double gy, out double gz) && gx == 100 && gy == 64 && gz == 100
            && Recruit.HoldPos(sentry) != null && !Recruit.TryGetPost(Hired("test-gondor-recruit", cu), out _, out _, out _), "Guard here remembers the post; a follower has none");
        Check(fs.GuardRadius == 16 && new FactionConfig().GuardRadius == FactionConfig.DefaultGuardRadius && FactionConfig.ClampGuardRadius(1) == 4 && FactionConfig.ClampGuardRadius(500) == 64 && FactionConfig.ClampGuardRadius(20) == 20,
            "guards watch 16 blocks around their post by default, and the config may set 4 to 64");
        var cfgR = new FactionConfig(); cfgR.GuardRadius = 1000; SetLegacy(fs, cfgR);
        Check(fs.GuardRadius == 64, "a radius past the limit counts as 64");
        SetLegacy(fs, new FactionConfig());
        Check(fs.InGuardArea(sentry, At(Player(null), 110, 64, 100)) && fs.InGuardArea(sentry, At(Player(null), 100, 64, 116)) && !fs.InGuardArea(sentry, At(Player(null), 117, 64, 100))
            && fs.InGuardArea(sentry, At(Player(null), 100, 80, 100)) && !fs.InGuardArea(sentry, At(Player(null), 100, 81, 100)), "the area reaches the radius across, and as far up and down");
        Check(fs.InGuardArea(sentry, At(Player(null), 123, 64, 100), FactionSystem.GuardLeash) && !fs.InGuardArea(sentry, At(Player(null), 125, 64, 100), FactionSystem.GuardLeash), "the leash lets a chase run 8 blocks past the area, no farther");
        var elsewhere = At(Player(null), 100, 64, 100); elsewhere.Pos.Dimension = 1;
        Check(!fs.InGuardArea(sentry, elsewhere) && !fs.InGuardArea(Hired("test-gondor-recruit", cu), At(Player(null), 0, 0, 0)), "...never into another dimension, and only guards have an area");

        // Who an aggressive guard attacks on sight.
        var intruder = At(Player(null), 105, 64, 105); var passerby = At(Player(null), 130, 64, 100);
        var countryman = At(Member(null, "gondor"), 105, 64, 105);
        var mate = At(Player(null), 105, 64, 105); hiring.SetOwner(mate.PlayerUID, new HiringOwner { Name = "Mate", Groups = new List<string> { "watch" } });
        hiring.Owner(cu).Groups = new List<string> { "watch" };
        Check(fs.RecruitWantsToAttack(sentry, cu, intruder) && !fs.RecruitWantsToAttack(sentry, cu, passerby), "an aggressive guard attacks a stranger inside its area, not one passing outside");
        Check(!fs.RecruitWantsToAttack(sentry, cu, At(chief, 105, 64, 105)) && !fs.RecruitWantsToAttack(sentry, cu, countryman) && !fs.RecruitWantsToAttack(sentry, cu, mate), "...never the owner, a member of the owner's faction, or the owner's /group mate");
        var cfgG = new FactionConfig(); cfgG.FillDefaults();
        cfgG.Relations.First(r => (r.A == "gondor" && r.B == "rohan") || (r.A == "rohan" && r.B == "gondor")).Relation = "ally"; SetLegacy(fs, cfgG);
        Check(!fs.RecruitWantsToAttack(sentry, cu, At(Member(null, "rohan"), 105, 64, 105)) && fs.RecruitWantsToAttack(sentry, cu, At(Member(null, "dale"), 105, 64, 105)), "...nor a member of a faction allied to the owner's; members of other factions are strangers");
        SetLegacy(fs, new FactionConfig());
        var theirMan = At(Hired("test-gondor-recruit", intruder.PlayerUID), 105, 64, 105); var theirManOutside = At(Hired("test-gondor-recruit", intruder.PlayerUID), 130, 64, 100);
        var matesMan = At(Hired("test-gondor-recruit", mate.PlayerUID), 105, 64, 105);
        Check(fs.RecruitWantsToAttack(sentry, cu, theirMan) && !fs.RecruitWantsToAttack(sentry, cu, theirManOutside) && !fs.RecruitWantsToAttack(sentry, cu, matesMan), "a stranger's recruits in the area are intruders too, even of the guard's own faction; a friend's are not");
        var orcIn = At(Wild("test-mordororc-warrior"), 108, 64, 100); var orcOut = At(Wild("test-mordororc-warrior"), 140, 64, 100);
        var drifterIn = At(Mob("game:drifter-normal"), 100, 64, 108); var drifterOut = At(Mob("game:drifter-normal"), 100, 64, 140);
        var hobbitIn = At(Wild("test-hobbit"), 101, 64, 101); var deerIn = At(Beast("game:deer-redbrocket-male"), 102, 64, 100); var deerOut = At(Beast("game:deer-redbrocket-male"), 140, 64, 100);
        Check(fs.RecruitWantsToAttack(sentry, cu, orcIn) && fs.RecruitWantsToAttack(sentry, cu, drifterIn) && !fs.RecruitWantsToAttack(sentry, cu, orcOut) && !fs.RecruitWantsToAttack(sentry, cu, drifterOut),
            "guards fight NPCs hostile to the owner and monsters inside their area only");
        Check(!fs.RecruitWantsToAttack(sentry, cu, hobbitIn) && !fs.RecruitWantsToAttack(sentry, cu, deerIn) && !fs.RecruitWantsToAttack(sentry, cu, At(Wild("test-gondor-recruit"), 101, 64, 100)), "...and leave the Shire, animals and the owner's faction alone, as aggressive recruits do");
        hiring.RecordTeamGrudge(cu, orcOut); hiring.RecordTeamGrudge(cu, passerby);
        Check(fs.RecruitWantsToAttack(sentry, cu, orcOut) && fs.RecruitWantsToAttack(sentry, cu, passerby), "fighting back works outside the area too (the leash ends the chase)");
        hiring.NowForTests += 31000;

        // The whitelist: one-way, per owner, live.
        var listed = At(Player(null), 105, 64, 105); var listedMan = At(Hired("test-gondor-recruit", listed.PlayerUID), 105, 64, 105);
        Check(fs.RecruitWantsToAttack(sentry, cu, listed) && fs.RecruitWantsToAttack(sentry, cu, listedMan) && !hiring.Whitelisted(cu, listed.PlayerUID), "a stranger and their recruit are attacked...");
        hiring.Owner(cu).Whitelist[listed.PlayerUID] = "Listed";
        Check(hiring.Whitelisted(cu, listed.PlayerUID) && fs.Friendly(cu, listed.PlayerUID) && !fs.RecruitWantsToAttack(sentry, cu, listed) && !fs.RecruitWantsToAttack(sentry, cu, listedMan), "...until the owner whitelists them: then neither is attacked on sight");
        Check(!fs.Friendly(listed.PlayerUID, cu) && !fs.AlliedPlayers(cu, listed.PlayerUID) && !hiring.Whitelisted(mate.PlayerUID, listed.PlayerUID), "the whitelist is one-way and per owner, and doesn't make allies");
        Recruit.SetStance(sentry, Recruit.Raid);
        var roamer = Hired("test-gondor-recruit", cu); Recruit.SetStance(roamer, Recruit.Raid);
        Check(!fs.RecruitWantsToAttack(sentry, cu, listed) && !fs.RecruitWantsToAttack(roamer, cu, At(listed, 500, 64, 500)) && fs.RecruitWantsToAttack(roamer, cu, intruder), "raiding recruits spare whitelisted players too, guarding or not");
        Recruit.SetStance(sentry, Recruit.Aggressive);
        hiring.RecordTeamGrudge(cu, listed); At(listed, 105, 64, 105);
        Check(fs.RecruitWantsToAttack(sentry, cu, listed), "a whitelisted player who hurts the owner or a recruit is still fought back");
        hiring.NowForTests += 31000;
        hiring.Owner(cu).Whitelist.Remove(listed.PlayerUID);
        Check(fs.RecruitWantsToAttack(sentry, cu, listed), "taken off the whitelist, they are a stranger again at once");
        var oldSave = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, HiringOwner>>("{ \"u1\": { \"Name\": \"Old\", \"Recruits\": [ 5, 6 ] } }");
        var saved = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, HiringOwner>>(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, HiringOwner> { ["u2"] = new HiringOwner { Name = "New", Whitelist = new Dictionary<string, string> { ["u3"] = "Friend" } } }));
        Check(oldSave["u1"].Whitelist != null && oldSave["u1"].Whitelist.Count == 0 && oldSave["u1"].Recruits.Count == 2 && saved["u2"].Whitelist["u3"] == "Friend", "whitelists are saved with the recruits list; older saves load with an empty one");

        // Stances still decide how eager guards are.
        Recruit.SetStance(sentry, Recruit.Raid);
        Check(fs.RecruitWantsToAttack(sentry, cu, deerIn) && fs.RecruitWantsToAttack(sentry, cu, hobbitIn) && fs.RecruitWantsToAttack(sentry, cu, intruder) && !fs.RecruitWantsToAttack(sentry, cu, deerOut)
            && !fs.RecruitWantsToAttack(sentry, cu, countryman) && !fs.RecruitWantsToAttack(sentry, cu, At(Wild("test-gondor-recruit"), 101, 64, 100)), "a raiding guard attacks every creature that isn't allied, inside its area only");
        Recruit.SetStance(sentry, Recruit.Defensive);
        Check(!fs.RecruitWantsToAttack(sentry, cu, intruder) && !fs.RecruitWantsToAttack(sentry, cu, orcIn), "a defensive guard only fights back");
        Recruit.SetStance(sentry, Recruit.Passive); hiring.RecordTeamGrudge(cu, intruder);
        Check(!fs.RecruitWantsToAttack(sentry, cu, intruder), "a passive guard never fights");
        hiring.NowForTests += 31000; Recruit.SetStance(sentry, Recruit.Aggressive);
        hiring.SetDisengaging(sentry);
        Check(!fs.RecruitWantsToAttack(sentry, cu, intruder), "a guard just given the order breaks off for a moment, like other movement orders");
        hiring.NowForTests += HiringSystem.DisengageMs + 1;
        Recruit.SetOrder(sentry, Recruit.Follow);
        Check(Recruit.OrderOf(sentry) == Recruit.Follow && !Recruit.TryGetPost(sentry, out _, out _, out _) && !fs.RecruitWantsToAttack(sentry, cu, intruder), "Follow me replaces Guard here: no post, and players are left alone again");
        Recruit.SetGuardAt(sentry, new Vec3d(100, 64, 100));
        // With the owner offline, guards go by the owner's last known race, groups, faction and whitelist.
        var away = Player("lrc:longbeard"); hiring.SetOwner(away.PlayerUID, new HiringOwner { Name = "Away", Race = "lrc:longbeard", EntityId = 0 });
        var keeper = At(Hired("test-dwarf-warrior", away.PlayerUID), 200, 64, 200); Recruit.SetGuardAt(keeper, new Vec3d(200, 64, 200));
        Check(!fs.RecruitWantsToAttack(keeper, away.PlayerUID, At(Member("lrc:longbeard", "dwarves"), 205, 64, 200)) && fs.RecruitWantsToAttack(keeper, away.PlayerUID, At(Player("lrc:harfoot"), 205, 64, 200)),
            "with the owner offline, guards still spare the owner's people (a dwarf) and stop strangers (a hobbit)");

        // Cached player sides follow every change at once.
        var shifter = Player("lrc:longbeard");
        Check(!fs.WantsToAttack("dwarves", shifter), "a dwarf player is a friend of the dwarves...");
        shifter.WatchedAttributes.SetString("skinModel", "lrc:orc");
        Check(fs.WantsToAttack("dwarves", shifter), "...and stops being one the moment they change race");
        fs.SetAssigned(shifter.PlayerUID, "Shifter", "dwarves");
        Check(!fs.WantsToAttack("dwarves", shifter), "/lotrfaction set applies at once");
        fs.SetAssigned(shifter.PlayerUID, "Shifter", null);
        Check(fs.WantsToAttack("dwarves", shifter), "...and so does /lotrfaction clear");
        var cfgD = new FactionConfig(); cfgD.FillDefaults(); cfgD.Factions["dwarves"].FriendlyRaces.Add("lrc:orc"); SetLegacy(fs, cfgD);
        Check(!fs.WantsToAttack("dwarves", shifter), "...and a config reload");
        SetLegacy(fs, new FactionConfig());
        var recorded = Player(null); hiring.SetOwner(recorded.PlayerUID, new HiringOwner { Name = "Rec" });
        bool before = fs.AlliedPlayers(cu, recorded.PlayerUID);
        hiring.Owner(recorded.PlayerUID).Groups = new List<string> { "watch" };
        Check(!before && fs.AlliedPlayers(cu, recorded.PlayerUID), "...and a /group change on an owner's record");
        hiring.SetOwner(recorded.PlayerUID, new HiringOwner { Name = "Rec" });
        Check(!fs.AlliedPlayers(cu, recorded.PlayerUID), "...and a replaced owner record");

        // The speedups give the same answers as before.
        var cfgT = new FactionConfig(); cfgT.FillDefaults();
        cfgT.Relations.Add(new FactionRelationDef { A = "mordor", B = "isengard", Relation = "ally" });
        cfgT.Relations.Add(new FactionRelationDef { A = "Isengard", B = "Mordor", Relation = "enemy" });   // a second entry for a pair: the first counts
        cfgT.Relations.Add(new FactionRelationDef { A = "gondor", B = "rivendell", Relation = "ally" });  // a faction the config doesn't list
        cfgT.Relations.Add(new FactionRelationDef { A = "Dale", B = "DWARVES", Relation = "Ally", ShareFriends = true });
        cfgT.Relations.Add(new FactionRelationDef { A = "elves", B = "arnor", Relation = "bogus" });
        SetLegacy(fs, cfgT);
        FactionRelation Slow(string a, string b) {
            if (a == b) return FactionRelation.Ally;
            foreach (var r in cfgT.Relations) {
                string ra = r.A?.Trim().ToLowerInvariant(), rb = r.B?.Trim().ToLowerInvariant();
                if (ra == null || rb == null || ra == rb || !((ra == a && rb == b) || (ra == b && rb == a))) continue;
                string rel = r.Relation?.Trim().ToLowerInvariant();
                return rel == "ally" ? FactionRelation.Ally : rel == "enemy" ? FactionRelation.Enemy : FactionRelation.Neutral;
            }
            return fs.Config.Factions.ContainsKey(a) && fs.Config.Factions.ContainsKey(b) ? FactionRelation.Enemy : FactionRelation.Neutral;
        }
        var names = fs.Config.Factions.Keys.Concat(new[] { "rivendell", "nowhere" }).ToList();
        Check(names.All(a => names.All(b => fs.Relation(a, b) == Slow(a, b))) && fs.Relation("mordor", "isengard") == FactionRelation.Ally && fs.Relation("elves", "arnor") == FactionRelation.Neutral
            && fs.Relation("rivendell", "gondor") == FactionRelation.Ally && fs.SharesFriends("dwarves", "dale"), "the relation table answers exactly as reading the entries one by one: first entry wins, both ways, unknown words are neutral");
        Check(NpcTypeInfo.Of(Npc("test-gondor-captain")).Faction == "gondor" && NpcTypeInfo.Of(Npc("test-gondor-captain")).Rank == "captain" && NpcTypeInfo.Of(Npc("test-uruk")).Rank == null
            && NpcTypeInfo.Of(Mob("game:wolf-male")).Faction == null && NpcTypeInfo.Of((Entity)null) == null && ReferenceEquals(NpcTypeInfo.Of(Npc("test-elf-recruit")), NpcTypeInfo.Of(Npc("test-elf-recruit"))),
            "each NPC type's faction and rank are read once and shared by every NPC of the type");
        var oddType = new EntityProperties { Code = new AssetLocation("lotrnpcs:odd"), Attributes = new JsonObject(JObject.Parse("{ lotrnpcsFaction: ' Gondor ' }")) };
        Check(NpcTypeInfo.Of(oddType).Faction == "gondor", "faction attributes compare in lower case, as the config's ids do");
        var wolfMob = Mob("game:wolf-male"); var drifterMob = Mob("game:drifter-normal");
        bool wasMonster = fs.IsMonster(drifterMob) && !fs.IsMonster(wolfMob);
        var monsterCfg = new FactionConfig(); monsterCfg.FillDefaults(); monsterCfg.MonsterCodes = new List<string> { "game:wolf-*" }; SetLegacy(fs, monsterCfg);
        Check(wasMonster && fs.IsMonster(wolfMob) && !fs.IsMonster(drifterMob), "the cached monster answer follows a changed monster list");
        SetLegacy(fs, new FactionConfig());
        Check(!fs.IsMonster(wolfMob) && fs.IsMonster(drifterMob), "...both ways");

        // A config with mistakes is used as far as it makes sense, never stopping the server.
        var messy = new FactionConfig {
            Factions = new Dictionary<string, FactionDef> {
                ["Gondor"] = new FactionDef { FriendlyRaces = new List<string> { null, " LRC:FreePeople ", "" }, FriendlyGroups = new List<string> { null, "Minas Tirith", "minas tirith" }, Stance = " Aggressive ",
                    HirePrices = new Dictionary<string, int> { ["Warrior"] = 20, ["warrior"] = 99, [" "] = 3 } },
                ["gondor"] = new FactionDef { Stance = "defensive" },
                ["mordor"] = null,
                [" "] = new FactionDef(),
                ["rohan"] = new FactionDef { Stance = "sleepy", FriendlyRaces = null, FriendlyGroups = null } },
            Relations = new List<FactionRelationDef> { null, new FactionRelationDef { A = "gondor", B = null, Relation = "ally" }, new FactionRelationDef { A = " MORDOR", B = "Gondor", Relation = " Ally " }, new FactionRelationDef { A = "gondor", B = "mordor", Relation = "enemy" }, new FactionRelationDef { A = "rohan", B = "rohan", Relation = "enemy" } },
            MonsterCodes = new List<string> { null, " ", " game:drifter-* " } };
        bool threw = false;
        try { SetLegacy(fs, messy); } catch (Exception e) { threw = true; Console.WriteLine("  " + e); }
        Check(!threw && fs.Config.Factions.Count == 3 && fs.Config.Factions.Keys.OrderBy(k => k).SequenceEqual(new[] { "gondor", "mordor", "rohan" }), "a config with an id twice (in other case), an empty id and an empty entry loads, keeping the first of each");
        Check(!threw && fs.Config.Factions["gondor"].Stance == "aggressive" && fs.Config.Factions["gondor"].FriendlyRaces.SequenceEqual(new[] { "lrc:freepeople" }) && fs.Config.Factions["gondor"].FriendlyGroups.SequenceEqual(new[] { "minas tirith" })
            && HiringSystem.PriceFor(fs.Config.Factions["gondor"], "warrior") == 20 && HiringSystem.PriceFor(fs.Config.Factions["gondor"], "captain") == 40, "...empty and repeated races, groups and prices are dropped, case and spaces don't matter");
        Check(!threw && fs.Relation("mordor", "gondor") == FactionRelation.Ally && fs.Relation("rohan", "rohan") == FactionRelation.Ally && fs.WantsToAttack("gondor", Mob("game:drifter-normal"))
            && !fs.IsAggressive("rohan") && fs.IsAggressive("mordor") && !fs.WantsToAttack("rohan", Player(null)), "...relations without both sides are skipped, monster codes are trimmed, and an unknown stance fights only when attacked");
        SetLegacy(fs, new FactionConfig());

        // The orders menu: Guard here is the fourth movement order, and every button keeps its own number key.
        Check(GuiDialogRecruitOrders.MoveOrders == 5 && GuiDialogRecruitOrders.KeyLabel(0) == "1" && GuiDialogRecruitOrders.KeyLabel(3) == "4" && GuiDialogRecruitOrders.KeyLabel(4) == "5" && GuiDialogRecruitOrders.KeyLabel(9) == "0"
            && GuiDialogRecruitOrders.KeyPrefix(9) == "0. " && GuiDialogRecruitOrders.KeyPrefix(10) == "" && Recruit.Stances.Length <= 10, "Guard here is 4, Guard here for my group 5, the formations follow on 6 to 9 and 0 (the last has no key), and the stances keep 1 to 4");
        Check(GuiDialogRecruitOrders.Rows(GuiDialogRecruitOrders.MoveOrders, 2) == 3 && ((string)lang["orders-guard"]).StartsWith("4.") && ((string)lang["orders-guard-shared"]).StartsWith("5.") && ((string)lang["cmd-guard"]).Contains("{1}") && ((string)lang["cmd-guard-shared"]).Contains("{1}"), "the five movement orders sit in three rows, and the replies name the radius");
        Check(new[] { "whitelist-unknown", "whitelist-self", "whitelist-already", "whitelist-added", "whitelist-notlisted", "whitelist-removed", "whitelist-empty", "whitelist-list" }.All(k => lang[k] != null)
            && lang.Properties().Where(p => p.Name.StartsWith("whitelist-") || p.Name.StartsWith("cmd-")).All(p => !((string)p.Value).Contains("<")), "every whitelist reply is in the lang file, with no angle brackets (chat reads them as markup)");
        Check(FactionConfig.InstructionText.Any(l => l.StartsWith("GuardRadius") && l.Contains("whitelist")), "#Instructions explain GuardRadius and the whitelist");

        // ---- 0.0.45: Mordor orcs carry cosmetic weapons; every NPC attacks 0.25 s more often ----
        string assets = Path.Combine(argv[0], "..");
        JToken HeldWeapon(JObject e, string side) => e[side]["behaviors"].FirstOrDefault(b => (string)b["code"] == "lotrnpcs:heldweapon");
        var armed = entityFiles.Where(kv => HeldWeapon(kv.Value, "server") != null || HeldWeapon(kv.Value, "client") != null).Select(kv => kv.Key).OrderBy(k => k).ToList();
        string[] ranked = entityFiles.Keys.Where(c => c.EndsWith("-recruit") || c.EndsWith("-warrior") || c.EndsWith("-captain") || c.EndsWith("-guard") || c.EndsWith("-shock") || c.EndsWith("-rider")).OrderBy(k => k).ToArray();
        Check(armed.Where(c => !c.EndsWith("-archer")).SequenceEqual(ranked) && armed.All(c => HeldWeapon(entityFiles[c], "server")["weapons"].Any())
            && armed.Count(c => c.EndsWith("-archer")) == 17 && entityFiles.Keys.Where(c => c.EndsWith("-archer")).All(armed.Contains),
            "every ranked NPC carries a weapon (0.0.76: the falx where its faction has no model yet), the Shire's hobbits and the rankless ones none, and every archer its bow");
        armed.RemoveAll(c => !c.StartsWith("test-mordororc-") || c.EndsWith("-archer") || c.EndsWith("-guard") || c.EndsWith("-shock"));
        Check(armed.All(c => JToken.DeepEquals(HeldWeapon(entityFiles[c], "server"), HeldWeapon(entityFiles[c], "client")) && HeldWeapon(entityFiles[c], "server")["weapons"].Count() == 6), "...with the same six weapons listed on both sides");
        var bodyShape = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "entity", "humanoid", "seraph-faceless.json")));
        var bodyAnims = new HashSet<string>(bodyShape["animations"].Select(a => (string)a["code"]));
        // 0.0.130 solid bodies.
        Check(!BattleSpacing.ContactShift(0.8, 0, 1, out _, out _) && BattleSpacing.ContactShift(0.5, 0, 1, out double csx, out double csz) && Math.Abs(csx - 0.1) < 1e-9 && csz == 0
            && BattleSpacing.ContactShift(0, 0, 90, out double ctx, out double ctz) && Math.Abs(Math.Sqrt(ctx * ctx + ctz * ctz) - BattleSpacing.BodyContact / 2) < 1e-9
            && BattleSpacing.ContactScan > BattleSpacing.BodyContact,
            "0.0.130: two NPCs closer than 0.7 blocks each step out by half the overlap, along the line between them (one spot: a direction from the id); none at 0.7 or more");
        // 0.0.127 gear detail: only tiny leaves go; parents, attachment points, step parent targets and roots stay.
        ShapeElement gdBox(string name, double sx, double sy, double sz, string stepParent = null, params ShapeElement[] children) =>
            new ShapeElement { Name = name, From = new double[] { 0, 0, 0 }, To = new double[] { sx, sy, sz }, StepParentName = stepParent, Children = children.Length > 0 ? children : null };
        var gdRivet = gdBox("rivet", 0.3, 0.3, 0.3); var gdStrip = gdBox("strip", 0.3, 0.3, 2); var gdHolder = gdBox("holder", 0.2, 0.2, 0.2, null, gdBox("inner", 2, 2, 2));
        var gdSocket = gdBox("socket", 0.2, 0.2, 0.2); gdSocket.AttachmentPoints = new[] { new AttachmentPoint { Code = "x" } };
        var gdTarget = gdBox("plume-base", 0.2, 0.2, 0.2); var gdRider = gdBox("plume", 0.2, 0.2, 0.2, "plume-base");
        var gdRoot = gdBox("plate", 0.2, 0.2, 0.2, "Torso", gdRivet, gdStrip, gdHolder, gdSocket, gdTarget, gdBox("stud", 0.49, 0.1, 0.4), gdRider);
        var gdGearShape = new Shape { Elements = new[] { gdRoot } };
        int gdStripped = GearDetail.Strip(gdGearShape, 0.5);
        var gdLeft = gdRoot.Children.Select(c => c.Name).ToArray();
        Check(gdStripped == 2 && gdLeft.SequenceEqual(new[] { "strip", "holder", "socket", "plume-base", "plume" }) && gdGearShape.Elements.Single() == gdRoot && gdHolder.Children.Length == 1
            && GearDetail.Strip(new Shape { Elements = new[] { gdBox("p", 1, 1, 1, "Torso", gdBox("r", 0.3, 0.3, 0.3)) } }, 0.2) == 0 && GearDetail.Strip(null, 0.5) == 0,
            "0.0.127: gear leaves out only leaves under 0.5 voxels on every side; parents, attachment points, step parents and their targets, and the attached roots stay; a smaller size keeps more");
        // 0.0.125 riders: spears weigh PREFER (3) times their foot weight, the rest stay; seated riders keep only the riding pose.
        float MountWeaponWeight(string code, string item) => HeldWeapon(entityFiles[code], "server")["weapons"].Where(w => ((string)w["item"]).Contains(item)).Select(w => (float?)w["weight"] ?? 1f).Single();
        Check(MountWeaponWeight("test-rohan-rider", "rhspear") == 3 && MountWeaponWeight("test-rohan-rider", "rhsword") == 1 && MountWeaponWeight("test-rohan-warrior", "rhspear") == 1
            && MountWeaponWeight("test-gondor-rider", "spear-generic-steel") == 9 && MountWeaponWeight("test-gondor-rider", "gdsword") == 1,
            "0.0.125: riders prefer spears (3x their weight on foot), warriors on foot are unchanged");
        Check(Riders.IsLegAnimation(new AnimationMetaData { Code = "sprint", Animation = "sprint" }) && Riders.IsLegAnimation(new AnimationMetaData { Code = "idle", Animation = "idle1" })
            && !Riders.IsLegAnimation(new AnimationMetaData { Code = "rideelk-walk", Animation = "rideelk-walk" }) && !Riders.IsLegAnimation(new AnimationMetaData { Code = "hit", Animation = "hit" })
            && !Riders.IsLegAnimation(null) && new[] { "rideelk-idle", "rideelk-walk", "rideelk-run" }.All(bodyAnims.Contains) && Riders.MountedExtraReach == 1f && Riders.MountedReach(null) == (0f, 0f),
            "0.0.125: a seated rider skips walk, run and idle (its body has the riding poses), and reaches a block farther from the saddle");
        var weaponEntries = HeldWeapon(entityFiles["test-mordororc-warrior"], "server")["weapons"].ToList();
        var entityAnims = entityFiles["test-mordororc-warrior"]["client"]["animations"].ToDictionary(a => (string)a["code"]);
        bool animsOk = true, itemsOk = true, texturesOk = true;
        var ownTextures = new HashSet<string>(); var standIns = new HashSet<string>();
        var goodTextures = new HashSet<string> { "game:block/metal/ingot/iron", "game:block/metal/tarnished/rusty-iron", "game:block/metal/tarnished/rusty-iron2", "game:block/metal/tarnished/iron",
            "game:item/tool/handle", "game:block/leather/plain", "game:block/leather/black", "game:block/cloth/linen/black", "game:block/cloth/burlap/string", "game:item/resource/hide/pelt",
            "game:block/wood/debarked/walnut", "game:block/wood/debarked/ebony" };
        foreach (JToken w in weaponEntries) {
            string item = (string)w["item"], name = item.Split('-').Last();
            foreach (string anim in new[] { (string)w["attackAnimation"], (string)w["holdAnimation"] }.Where(a => a != null)) {
                if (!entityAnims.TryGetValue(anim, out JToken meta) || !bodyAnims.Contains((string)meta["animation"])) { animsOk = false; Console.WriteLine("  missing animation " + anim); }
            }
            string itemFile = Path.Combine(assets, "itemtypes", "orcweapon-" + name + ".json"), shapeFile = Path.Combine(assets, "shapes", "item", "orcweapon", name + ".json");
            if (!item.StartsWith("lotrnpcs:orcweapon-") || !File.Exists(itemFile) || !File.Exists(shapeFile)) { itemsOk = false; Console.WriteLine("  missing item or shape for " + item); continue; }
            var it = JObject.Parse(File.ReadAllText(itemFile));
            if ((string)it["code"] != "orcweapon-" + name || it["creativeinventory"] != null || (bool?)it["attributes"]?["handbook"]?["exclude"] != true || (float)it["tpHandTransform"]["scale"] != 0.6f
                || (float)it["tpHandTransform"]["origin"]["x"] != 0 || lang["item-orcweapon-" + name] == null) { itemsOk = false; Console.WriteLine("  item " + item + " is set up wrong"); }
            foreach (var tex in ((JObject)JObject.Parse(File.ReadAllText(shapeFile))["textures"]).Properties()) {
                string path = (string)tex.Value;
                if (path.StartsWith("lotrnpcs:item/orcweapon/")) {
                    ownTextures.Add(path);
                    string png = Path.Combine(new[] { assets, "textures" }.Concat(path.Substring("lotrnpcs:".Length).Split('/')).ToArray()) + ".png";
                    if (path != path.ToLowerInvariant() || !File.Exists(png) || !File.ReadAllBytes(png).Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) { texturesOk = false; Console.WriteLine("  " + name + " texture " + tex.Name + " = " + path + " is not a bundled lowercase PNG"); }
                }
                else if (!goodTextures.Contains(path)) { texturesOk = false; Console.WriteLine("  " + name + " texture " + tex.Name + " = " + path); }
                else standIns.Add(path);
            }
        }
        Check(animsOk, "every weapon's attack and hold animation is in the Mordor orcs' animation list and in the body shape");
        Check(itemsOk, "every weapon item and model exists, stays out of the creative inventory and handbook, has a name, and is held at 0.6 scale from its grip");
        Check(texturesOk, "every weapon texture is either one of the owner's, bundled as a lowercase PNG, or a vanilla one the modpack uses (none of the models' missing paths are left)");
        var expectedOwn = new[] { "scimitar", "axe", "hammer", "dagger", "knife", "poleaxe" }.Select(t => "lotrnpcs:item/orcweapon/atlas/" + t);
        Check(ownTextures.OrderBy(t => t).SequenceEqual(expectedOwn.OrderBy(t => t)) && standIns.Count == 0,
            "0.0.65: each of the owner's six replacement weapons uses its own bundled texture sheet, with no vanilla stand-ins left");
        var daggerHand = JObject.Parse(File.ReadAllText(Path.Combine(assets, "itemtypes", "orcweapon-dagger.json")))["tpHandTransform"];
        Check((float)daggerHand["rotation"]["x"] == 180 && Math.Abs((float)daggerHand["translation"]["y"] - 0.0594f) < 0.001f && Math.Abs((float)daggerHand["translation"]["z"] + 0.5f) < 0.001f,
            "0.0.46: the dagger is rolled over in the hand (owner: it was upside down), with its grip still on the hand");
        JToken HandRot(string n) => JObject.Parse(File.ReadAllText(Path.Combine(assets, "itemtypes", "orcweapon-" + n + ".json")))["tpHandTransform"]["rotation"];
        Check((float)HandRot("scimitar")["x"] == 180 && (float)HandRot("scimitar")["z"] == 20 && (float)HandRot("knife")["x"] == 0 && (float)HandRot("knife")["z"] == 0,
            "0.0.66: the scimitar and knife are rolled over in the hand (owner: both were upside down), the scimitar keeping its downward slant");
        string[] OrcHair(string race) => ((JObject)JObject.Parse(File.ReadAllText(Path.Combine(assets, "config", "customplayermodels", race + ".json"))).Properties().First().Value)["SkinnableParts"]
            .First(p => (string)p["code"] == "hairbase")["variants"].Select(v => (string)v["code"]).ToArray();
        Check(new[] { "goblin", "mordororc", "gundabadorc", "uruk" }.All(r => OrcHair(r).Contains("dreadlocks") && OrcHair(r).Contains("messy") && !OrcHair(r).Contains("longbalding"))
            && !File.Exists(Path.Combine(assets, "shapes", "skinparts", "hair-base", "orclongbalding.json")),
            "0.0.67: every orc body offers the game's dreadlocks and messy hair, and the long balding hair is gone (owner did not like it)");
        Check((string)entityAnims["lotrnpcs-poleaxe-attack"]["animation"] == "poleaxechop" && bodyAnims.Contains("poleaxechop") && !bodyAnims.Contains("lotrnpcs-poleaxe-attack"),
            "0.0.68: the poleaxe swings our own two-handed chop, built into the bundled body (owner: it used the spear thrust)");
        Check(weaponEntries.Count(w => w["holdAnimation"] != null) == 1 && (string)weaponEntries.First(w => w["holdAnimation"] != null)["item"] == "lotrnpcs:orcweapon-poleaxe", "only the poleaxe has a guard pose between attacks");
        bool speedsOk = weaponEntries.All(w => { var m = entityAnims[(string)w["attackAnimation"]]; return (string)m["blendMode"] == "Average" && (float)m["elementWeight"]["UpperArmR"] > (float)m["elementWeight"]["UpperFootR"] * 10; });
        Check(speedsOk, "swings take over the arms and torso but leave the legs to walking and running");
        var melee = entityFiles.Values.Select(e => e["server"]["behaviors"].First(b => (string)b["code"] == "taskai")["aitasks"].First(t => (string)t["code"] == "lotrnpcs:meleeattack")).ToList();
        Check(melee.Count == 115 && melee.All(t => (int)t["mincooldown"] == 1700 && (int)t["maxcooldown"] == 2100 && (int)t["attackDurationMs"] == 900 && (int)t["damagePlayerAtMs"] == 400 && (string)t["animation"] == "hit"),
            "every NPC waits 1700 to 2100 ms between swings (0.0.108: 0.5 s less than 0.0.44), with the same swing length, hit timing and unarmed punch");
        Check(entityFiles["test-mordororc-captain"]["server"]["behaviors"].First(b => (string)b["code"] == "taskai")["aitasks"].First(t => (string)t["code"] == "lotrnpcs:meleeattack").Value<int>("damage") == 7
            && (int)melee.First()["damageTier"] > 0, "weapons change no damage numbers (captain still 7)");

        // The behavior itself.
        var listJson = new JsonObject(JToken.Parse("[{item: ' LotrNpcs:OrcWeapon-Axe ', attackAnimation: 'Lotrnpcs-Axe-Attack'}, {item: 'orcweapon-dagger', attackAnimation: 'x'}, {item: 'lotrnpcs:orcweapon-axe', attackAnimation: 'y'}, {attackAnimation: 'z'}, {item: 'lotrnpcs:nothing'}, null, {item: 'lotrnpcs:orcweapon-poleaxe', attackAnimation: 'p', holdAnimation: ' P-Hold '}]"));
        NpcWeapon[] read = EntityBehaviorHeldWeapon.ReadWeapons(listJson);
        Check(read.Select(w => w.Item).SequenceEqual(new[] { "lotrnpcs:orcweapon-axe", "lotrnpcs:orcweapon-dagger", "lotrnpcs:orcweapon-poleaxe" }) && read[0].AttackAnimation == "lotrnpcs-axe-attack" && read[2].HoldAnimation == "p-hold" && read[0].HoldAnimation == null,
            "the weapon list is cleaned: codes lowercased and trimmed, our domain added, repeats, empty and incomplete entries dropped");
        Check(EntityBehaviorHeldWeapon.ReadWeapons(null).Length == 0 && EntityBehaviorHeldWeapon.ReadWeapons(new JsonObject(JToken.Parse("{}"))).Length == 0, "...and a missing list means no weapon");
        NpcWeapon[] five = EntityBehaviorHeldWeapon.ReadWeapons(new JsonObject(HeldWeapon(entityFiles["test-mordororc-recruit"], "server")["weapons"]));
        var rand = new Random(45);
        var picks = Enumerable.Range(0, 500).Select(_ => EntityBehaviorHeldWeapon.PickWeapon(null, five, rand)).ToList();
        Check(five.Length == 6 && five.All(w => picks.Count(p => p == w.Item) > 50), "a new orc gets any of the six weapons at random (" + string.Join(", ", five.Select(w => w.Item.Split('-').Last() + " " + picks.Count(p => p == w.Item))) + ")");
        string repick = EntityBehaviorHeldWeapon.PickWeapon("lotrnpcs:orcweapon-club", five, rand);
        Check(EntityBehaviorHeldWeapon.PickWeapon("lotrnpcs:orcweapon-hammer", five, rand) == "lotrnpcs:orcweapon-hammer" && five.Any(w => w.Item == repick)
            && EntityBehaviorHeldWeapon.PickWeapon("lotrnpcs:orcweapon-hammer", Array.Empty<NpcWeapon>(), rand) == null, "an orc keeps its weapon, picks again when its weapon isn't listed any more, and carries nothing when the list is empty");
        var orc = Npc("test-mordororc-warrior");
        var held = new EntityBehaviorHeldWeapon(orc);
        held.Configure(new JsonObject(HeldWeapon(entityFiles["test-mordororc-warrior"], "server")));
        held.Choose(new Random(1));
        string first = orc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.WeaponAttr);
        held.Choose(new Random(2)); held.Choose(new Random(3));
        Check(first != null && five.Any(w => w.Item == first) && orc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.WeaponAttr) == first && held.Current?.Item == first, "the server stores the orc's weapon once and keeps it (" + first + ")");
        Check(orc.RightHandItemSlot == null, "...and never puts it in the orc's hand on the server, so nothing there can see it or use its stats");
        orc.WatchedAttributes.SetString(EntityBehaviorHeldWeapon.WeaponAttr, "lotrnpcs:orcweapon-club");
        held.Choose(new Random(4));
        Check(five.Any(w => w.Item == orc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.WeaponAttr)), "an orc saved with a weapon that is no longer listed gets a listed one on load");
        orc.WatchedAttributes.SetString(EntityBehaviorHeldWeapon.WeaponAttr, "lotrnpcs:orcweapon-poleaxe");
        Check(held.Current?.HoldAnimation == "lotrnpcs-poleaxe-hold" && held.Current.AttackAnimation == "lotrnpcs-poleaxe-attack", "the poleaxe names its guard pose and chop");
        Check(held.AttackAnimation == null, "without the animation in the entity's list the orc keeps its unarmed punch (and nothing crashes)");
        orc.Properties.Client = new EntityClientProperties(Array.Empty<JsonObject>(), new Dictionary<string, JsonObject>()) {
            Animations = entityAnims.Values.Select(a => new AnimationMetaData { Code = (string)a["code"], Animation = (string)a["animation"] }).ToArray() };
        Check(held.AttackAnimation == null, "...the lookup is cached per weapon");
        orc.WatchedAttributes.SetString(EntityBehaviorHeldWeapon.WeaponAttr, "lotrnpcs:orcweapon-axe");
        Check(held.AttackAnimation?.Code == "lotrnpcs-axe-attack" && held.AttackAnimation.Animation == "axehit", "a new weapon looks up its own swing (the axe chops)");
        held.Lower(); held.Raise();
        held.Configure(new JsonObject(JToken.Parse("{weapons: []}"))); held.Choose(new Random(5));
        Check(!orc.WatchedAttributes.HasAttribute(EntityBehaviorHeldWeapon.WeaponAttr) && held.Current == null && held.AttackAnimation == null, "an NPC whose list was emptied puts its weapon away");
        var axeItem = new Item(); axeItem.Code = new AssetLocation("lotrnpcs:orcweapon-axe");
        var shown = new EntityBehaviorHeldWeapon(orc);
        shown.SetHand(orc, axeItem);
        Check(orc.RightHandItemSlot is DummySlot && orc.RightHandItemSlot.Itemstack?.Collectible == axeItem && orc.RightHandItemSlot.Inventory == null, "the client shows the weapon through a slot that belongs to no inventory, so it can't be dropped or taken");
        shown.SetHand(orc, null);
        Check(orc.RightHandItemSlot == null, "...and takes it away again");
        var foreign = new DummySlot(new ItemStack(axeItem)); orc.RightHandItemSlot = foreign;
        shown.SetHand(orc, axeItem);
        Check(orc.RightHandItemSlot == foreign, "...but never replaces a hand item something else put there");


        // Battle spacing (0.0.53): who fights whom, the attacker limit, places around a target and the push apart.
        {
            var bs = new BattleSpacing { MaxAttackers = 3 };
            long t0 = 100000;
            const long T = 1, P = 2;
            bs.Claim(11, T, 0, t0); bs.Claim(12, T, 0, t0); bs.Claim(13, T, 0, t0);
            Check(bs.AttackersOn(T, t0) == 3 && bs.TargetOf(12, t0) == T, "three attackers claim one NPC");
            Check(bs.Crowded(14, T, false, t0) && !bs.Crowded(12, T, false, t0), "a fourth NPC finds it crowded, but one already fighting it doesn't");
            Check(!bs.Crowded(14, P, true, t0) && !bs.Crowded(14, 99, false, t0), "players and NPCs nobody fights are never crowded");
            bs.MaxAttackers = 0;
            Check(!bs.Crowded(14, T, false, t0), "MaxAttackersPerTarget 0 turns the limit off");
            bs.MaxAttackers = 3;
            bs.Claim(14, T, 0, t0);
            Check(bs.AttackersOn(T, t0) == 4, "striking back may go past the limit (the claim is still counted)");
            bs.Release(14);
            Check(bs.AttackersOn(T, t0) == 3 && bs.TargetOf(14, t0) == 0, "an attacker that leaves frees its place");
            bs.Claim(13, 3, 0, t0);
            Check(bs.AttackersOn(T, t0) == 2 && bs.TargetOf(13, t0) == 3 && !bs.Crowded(15, T, false, t0), "switching target moves the claim, so a place opens up");
            bs.Swing(12, 3, 0, t0 + 10);
            Check(bs.TargetOf(12, t0) == T, "a swing at a passer-by leaves the claim alone");
            bs.Swing(16, 3, 0, t0 + 10);
            Check(bs.TargetOf(16, t0) == 3, "a swing claims the target when the NPC had none");
            Check(bs.AttackersOn(T, t0 + BattleSpacing.ClaimMs + 1) == 0 && bs.TargetOf(11, t0 + BattleSpacing.ClaimMs + 1) == 0, "claims run out after " + BattleSpacing.ClaimMs + " ms unless renewed");
            bs.Claim(21, 5, 0, t0); bs.Claim(5, 7, 0, t0);
            bs.Forget(5);
            Check(bs.AttackersOn(5, t0) == 0 && bs.TargetOf(21, t0) == 0 && bs.AttackersOn(7, t0) == 0, "an NPC that dies or unloads frees its place and every place on it");
            bs.Claim(31, 8, 0, t0 + 20000);
            Check(bs.TrackedTargets <= 2 && bs.TrackedAttackers <= 2, "old claims are swept away (" + bs.TrackedTargets + " targets, " + bs.TrackedAttackers + " attackers tracked)");
            bs.Spacing = 10; double hi = bs.Spacing; bs.Spacing = 0.1; double lo = bs.Spacing; bs.Spacing = double.NaN; double nan = bs.Spacing;
            Check(hi == 4 && lo == 0.8 && nan == BattleSpacing.DefaultSpacing, "CombatSpacing is kept between 0.8 and 4");
            bs.Spacing = 3;
            Check(bs.SurroundRadius == 1.3 && bs.DuelMin == 1.0 && bs.DuelMax == 1.6, "0.0.82: attackers aim for the middle of the 1.0 to 1.6 duel band whatever the spacing");
            var band = new BattleSpacing(); band.SetDuel(5, 0.1); double bMin = band.DuelMin, bMax = band.DuelMax; band.SetDuel(0.1, 9);
            Check(bMin == BattleSpacing.MaxDuel - 0.2 && bMax == BattleSpacing.MaxDuel && band.DuelMin == BattleSpacing.MinDuel && band.DuelMax == BattleSpacing.MaxDuel
                && band.InDuelBand(1.0) && !new BattleSpacing().InDuelBand(0.9) && new BattleSpacing().InDuelBand(1.6) && !new BattleSpacing().InDuelBand(1.7),
                "the duel band is kept within 0.6 to 2.2 blocks, its top 0.2 past its bottom, and both ends count as inside");
            // 0.0.98 staggered swings: hits on one target land at least HitGapMs apart (the game's hurt window is 500 ms).
            var turns = new BattleSpacing(); const long HT = 77;
            bool freeFirst = turns.HitWaitMs(HT, 1000) == 0;
            turns.BookHit(HT, 1000, 100);
            long w2 = turns.HitWaitMs(HT, 1000), w3 = turns.HitWaitMs(HT, 1000 + BattleSpacing.HitGapMs + 100);
            turns.BookHit(HT, 900, 0); long kept = turns.HitWaitMs(HT, 1000);
            turns.BookHit(HT, 2000, 0); long late = turns.HitWaitMs(HT, 2000);
            Check(freeFirst && w2 == BattleSpacing.HitGapMs + 100 && w3 == 0 && kept == BattleSpacing.HitGapMs + 100 && late == BattleSpacing.HitGapMs
                && BattleSpacing.HitGapMs > 500, "0.0.98: a second hit on a target waits out the first one's hurt window, an earlier booking never shortens the wait, a late hit moves it later");
            turns.Forget(HT); bool forgot = turns.HitWaitMs(HT, 2000) == 0;
            turns.BookHit(99, 3000, 0); turns.BookHit(88, 20000, 0);
            Check(forgot && turns.TrackedHitTurns == 1 && turns.HitWaitMs(99, 3000) == 0, "0.0.98: a dead or unloaded target's turn is forgotten, and turns that passed are swept");
            bs.Spacing = 1.5;

            // Places: three NPCs coming from the same side walk to a third of a turn apart and stay there.
            var sur = new BattleSpacing();
            double[] ax = { 6, 6.3, 5.7 }, az = { 0.2, -0.1, 0.4 };
            for (int step = 0; step < 40; step++)
            {
                for (int k = 0; k < 3; k++)
                {
                    long id = 40 + k;
                    double bearing = Math.Atan2(ax[k], az[k]);
                    sur.Claim(id, 9, bearing, t0);
                    sur.SurroundAngle(id, 9, bearing, t0, out double angle);
                    BattleSpacing.SpotAt(0, 0, angle, sur.SurroundRadius, out double sx, out double sz);
                    ax[k] += (sx - ax[k]) * 0.3; az[k] += (sz - az[k]) * 0.3;
                }
            }
            double minGap = double.MaxValue, maxR = 0, minR = double.MaxValue;
            for (int k = 0; k < 3; k++)
            {
                double r = Math.Sqrt(ax[k] * ax[k] + az[k] * az[k]); maxR = Math.Max(maxR, r); minR = Math.Min(minR, r);
                for (int m = k + 1; m < 3; m++) minGap = Math.Min(minGap, Math.Sqrt((ax[k] - ax[m]) * (ax[k] - ax[m]) + (az[k] - az[m]) * (az[k] - az[m])));
            }
            Check(minGap > 2.2 && Math.Abs(maxR - 1.3) < 0.05 && Math.Abs(minR - 1.3) < 0.05, "0.0.82: three attackers (only on players now) spread round their target 1.3 blocks out, " + minGap.ToString("0.00") + " blocks apart (a third of a turn is 2.25)");
            var one = new BattleSpacing();
            one.Claim(50, 9, 1.0, t0); one.SurroundAngle(50, 9, 1.0, t0, out double oneAngle);
            Check(Math.Abs(oneAngle - 1.0) < 1e-9, "a lone attacker comes straight in");
            var two = new BattleSpacing();
            two.Claim(51, 9, 0.1, t0); two.Claim(52, 9, -0.1, t0);
            two.SurroundAngle(51, 9, 0.1, t0, out double a51); two.SurroundAngle(52, 9, -0.1, t0, out double a52);
            double apart = Math.Abs(Math.IEEERemainder(a51 - a52, 2 * Math.PI));
            Check(Math.Abs(apart - Math.PI / 2) < 1e-6 && Math.Abs(Math.IEEERemainder((a51 + a52) / 2, 2 * Math.PI)) < 1e-6,
                "0.0.82 (owner: \"wrap the flanks\"): two attackers coming from the same side flank the target 90 degrees apart, around the way they came");
            var others = new BattleSpacing();
            others.Claim(61, 9, 0, t0); others.Claim(62, 9, 0, t0);
            Check(others.OthersOn(61, 9, t0) == 1 && others.OthersOn(63, 9, t0) == 2 && others.OthersOn(61, 10, t0) == 0,
                "pairing: an attacker counts the others on a target, not itself");
            Check(!two.SurroundAngle(53, 9, 0, t0, out _), "an NPC without a claim gets no place");

            // The push: two orcs in the same spot drift apart to about the spacing in a second or two, using vanilla's
            // ground physics (motion kept at 70% every 1/60 s); one 3 blocks away is left alone.
            double x1 = 0, x2 = 0.2, m1v = 0, m2v = 0, z1 = 0, z2 = 0, mz1 = 0, mz2 = 0;
            float dt = 0.05f; double maxSeen = 0; double after1s = 0;
            for (int tick = 0; tick < 60; tick++)
            {
                double p1x = 0, p1z = 0, p2x = 0, p2z = 0;
                BattleSpacing.AddPush(ref p1x, ref p1z, x1 - x2, z1 - z2, 1.5, 101);
                BattleSpacing.AddPush(ref p2x, ref p2z, x2 - x1, z2 - z1, 1.5, 102);
                double k = BattleSpacing.PushImpulse(dt);
                m1v += p1x * k; mz1 += p1z * k; m2v += p2x * k; mz2 += p2z * k;
                for (int f = 0; f < 3; f++)
                {
                    x1 += m1v; z1 += mz1; x2 += m2v; z2 += mz2;
                    m1v *= 0.7; mz1 *= 0.7; m2v *= 0.7; mz2 *= 0.7;
                }
                double sep = Math.Sqrt((x1 - x2) * (x1 - x2) + (z1 - z2) * (z1 - z2));
                maxSeen = Math.Max(maxSeen, sep);
                if (tick == 19) after1s = sep;
            }
            Check(after1s > 1.0 && maxSeen <= 1.5 + 1e-6, "two overlapping NPCs are " + after1s.ToString("0.00") + " blocks apart after a second and never pushed past the spacing (" + maxSeen.ToString("0.00") + ")");
            double fx = 0, fz = 0;
            Check(!BattleSpacing.AddPush(ref fx, ref fz, 3, 0, 1.5, 1) && fx == 0 && fz == 0, "an NPC 3 blocks away isn't pushed");
            double sx1 = 0, sz1 = 0, sx2 = 0, sz2 = 0;
            BattleSpacing.AddPush(ref sx1, ref sz1, 0, 0, 1.5, 7); BattleSpacing.AddPush(ref sx2, ref sz2, 0, 0, 1.5, 8);
            Check(Math.Abs(sx1) + Math.Abs(sz1) > 0.99 && Math.Abs(Math.Atan2(sx1, sz1) - Math.Atan2(sx2, sz2)) > 0.5, "two NPCs in exactly the same spot split in different directions");

            var fsys = new FactionSystem();
            var cfgS = FactionConfig.FromFile(null, out _); cfgS.MaxAttackersPerTarget = 5; cfgS.CombatSpacing = 9;
            SetLegacy(fsys, cfgS);
            Check(fsys.Spacing.MaxAttackers == 5 && fsys.Spacing.Spacing == 4, "the faction system uses the file's limit and clamps its spacing");
        }


        // Looks and names (0.0.54): helmets, the owner's changes, names on hire.
        {
            var rnd = new Random(3);
            int worn = 0; for (int i = 0; i < 1000; i++) if (Looks.WearsHelmet("recruit", 0.6, true, rnd)) worn++;
            Check(worn > 540 && worn < 660, "about 60% of new recruits wear their helmet (" + worn + " of 1000)");
            Check(!Looks.WearsHelmet("warrior", 0, true, rnd) && Looks.WearsHelmet("warrior", 1, true, rnd) && Looks.WearsHelmet("captain", 0, true, rnd) && !Looks.WearsHelmet("captain", 0, false, rnd),
                "HelmetChance 0 and 1 mean never and always; captains always wear theirs unless CaptainsAlwaysHelmeted is false");
            var helmItem = new Item(); helmItem.Code = new AssetLocation("game:armor-head-chain-iron");
            var head = new DummySlot(new ItemStack(helmItem));
            var hattrs = new TreeAttribute();
            Check(Looks.MoveHelmet(head, hattrs, false, null) && head.Empty && Looks.KeptHelmet(hattrs, null)?.Collectible == helmItem, "taking a helmet off keeps it with the NPC");
            Check(!Looks.MoveHelmet(head, hattrs, false, null), "...and it can't be taken off twice");
            Check(Looks.MoveHelmet(head, hattrs, true, null) && head.Itemstack?.Collectible == helmItem && !hattrs.HasAttribute(Looks.KeptHelmetAttr), "putting it back on wears the same helmet");
            Check(!Looks.MoveHelmet(new DummySlot(), new TreeAttribute(), true, null), "an NPC without a helmet can't put one on");

            Check(Looks.CleanName("  Gorbuth   the  Bold ") == "Gorbuth the Bold" && Looks.CleanName("Dúnhir") == "Dúnhir" && Looks.CleanName("O'Brien-Took") == "O'Brien-Took",
                "names are trimmed to single spaces and may use letters (with accents), apostrophes and hyphens");
            Check(Looks.CleanName("") == null && Looks.CleanName("   ") == null && Looks.CleanName(new string('a', 25)) == null && Looks.CleanName("<b>x</b>") == null && Looks.CleanName("Orc 2") == null && Looks.CleanName("-Orc") == null,
                "empty, too long (over 24), markup, digits and names not starting with a letter are refused");

            var lnpc = Npc("test-mordororc-warrior"); lnpc.EntityId = 777;
            Recruit.Bind(lnpc, "uidOwner", "Owner");
            var lparts = Looks.ParseParts(@"{ ""mordororc"": { ""SkinnableParts"": [
                { ""code"": ""hairbase"", ""variants"": [ { ""code"": ""bald"" }, { ""code"": ""mohawk"" }, { ""code"": ""secret"", ""category"": ""hidden"" } ] },
                { ""code"": ""voicetype"", ""variants"": [ { ""code"": ""a"" }, { ""code"": ""b"" } ] },
                { ""code"": ""eyecolor"", ""variants"": [ { ""code"": ""red"" }, { ""code"": ""yellow"" } ] },
                { ""code"": ""underwear"", ""variants"": [ { ""code"": ""loincloth"" } ] } ] } }", "mordororc");
            Check(lparts.Count == 2 && lparts["hairbase"].SequenceEqual(new[] { "bald", "mohawk" }) && lparts.ContainsKey("eyecolor"), "the face parts are read from the race's model file, leaving out the voice, underwear, single-choice parts and hidden variants");
            string[] lweapons = { "lotrnpcs:orcweapon-axe", Looks.NoWeapon };
            var lhead = new DummySlot(new ItemStack(helmItem));
            Check(Looks.Apply(lnpc, "uidStranger", new LooksRequest { Name = "Bob" }, lparts, lweapons, lhead) == "looks-notyours" && Looks.NameOf(lnpc) != "Bob", "only the owner can change a recruit's looks");
            Check(Looks.Apply(lnpc, "uidOwner", new LooksRequest { Name = "Bad<name>", Weapon = Looks.NoWeapon }, lparts, lweapons, lhead) == "looks-badname"
                && lnpc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.WeaponAttr) != Looks.NoWeapon, "a bad name refuses the whole request, so nothing else in it changes");
            Check(Looks.Apply(lnpc, "uidOwner", new LooksRequest { Parts = new Dictionary<string, string> { ["hairbase"] = "secret" } }, lparts, lweapons, lhead) == "looks-badpart"
                && Looks.Apply(lnpc, "uidOwner", new LooksRequest { Parts = new Dictionary<string, string> { ["voicetype"] = "a" } }, lparts, lweapons, lhead) == "looks-badpart",
                "a variant or part the race doesn't offer is refused");
            Check(Looks.Apply(lnpc, "uidOwner", new LooksRequest { Weapon = "game:sword-steel" }, lparts, lweapons, lhead) == "looks-badweapon"
                && Looks.Apply(lnpc, "uidOwner", new LooksRequest { Weapon = "none" }, lparts, Array.Empty<string>(), lhead) == "looks-badweapon", "a weapon the NPC can't carry is refused, and so is any for an unarmed type");
            Check(Looks.Apply(lnpc, "uidOwner", new LooksRequest { Helmet = "on" }, lparts, lweapons, new DummySlot()) == "looks-nohelmet", "a helmet it doesn't have can't be put on");
            Check(Looks.Apply(lnpc, "uidOwner", new LooksRequest { Name = " Ugrath  ", Parts = new Dictionary<string, string> { ["hairbase"] = "mohawk", ["eyecolor"] = "red" }, Weapon = Looks.NoWeapon, Helmet = "off" }, lparts, lweapons, lhead) == null
                && Looks.NameOf(lnpc) == "Ugrath" && lnpc.WatchedAttributes.GetBool(Looks.NamedAttr) && Looks.CurrentPart(lnpc, "hairbase") == "mohawk" && Looks.CurrentPart(lnpc, "eyecolor") == "red"
                && lnpc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.WeaponAttr) == Looks.NoWeapon && lhead.Empty && lnpc.WatchedAttributes.HasAttribute(Looks.KeptHelmetAttr),
                "the owner renames it, changes its hair and eyes, takes its weapon away and its helmet off");
            Check(Looks.Apply(lnpc, "uidOwner", new LooksRequest { Helmet = "on" }, lparts, lweapons, lhead) == null && !lhead.Empty, "...and can put the helmet back on");
            Check(Looks.ClassFor(true, false) == "shock" && Looks.ClassFor(true, true) == "shock" && Looks.ClassFor(false, true) == "guard" && Looks.ClassFor(false, false) == "warrior",
                "a warrior's loadout picks its class: two-handed makes a shock trooper, a shield a guard, neither a warrior (0.0.115)");
            Check(Looks.LoadoutClass(lnpc) == null, "an NPC outside a warrior line has no loadout class");
            Check(typeof(Vintagestory.GameContent.EntityBehaviorHarvestable).IsSubclassOf(typeof(Vintagestory.GameContent.EntityBehaviorContainer)) && !typeof(Vintagestory.GameContent.EntityBehaviorHarvestable).IsSubclassOf(typeof(Vintagestory.GameContent.EntityBehaviorTexturedClothing)),
                "0.0.115: gear is found through EntityBehaviorTexturedClothing, since the body's harvestable inventory (listed first) is also a container");
            Check(!FactionConfig.FromFile(null, out _).DebugCommands && !FactionConfig.FromFile(new FactionConfig { Version = 29 }, out _).DebugCommands
                && FactionConfig.InstructionText.Any(l => l.StartsWith("DebugCommands")) && ModCommands.DebugOffText.Contains("DebugCommands"),
                "0.0.115: the testing commands are off in a new or an old config, the file explains DebugCommands, and the refusal says how to turn them on");
            Check(AnimationLod.Interval(19.9 * 19.9, 20) == 1 && AnimationLod.Interval(39 * 39, 20) == 2 && AnimationLod.Interval(59 * 59, 20) == 3 && AnimationLod.Interval(61 * 61, 20) == 4
                && AnimationLod.Interval(100, 1) == 3 && AnimationLod.Interval(100 * 100, 1000) == 1,
                "0.0.116: far NPCs animate every 2nd, 3rd or 4th frame past 1, 2 and 3 times the full-rate distance (kept between 4 and 256 blocks)");
            int lodCount = 0; float lodSkipped = 0; int lodRuns = 0; float lodTime = 0;
            for (int f = 0; f < 12; f++) { float dt = 0.01f; if (AnimationLod.Step(ref lodCount, ref lodSkipped, ref dt, 3)) { lodRuns++; lodTime += dt; } }
            float nearDt = 0.01f; int nearCount = 0; float nearSkipped = 0;
            Check(lodRuns == 4 && Math.Abs(lodTime - 0.12f) < 1e-4 && AnimationLod.Step(ref nearCount, ref nearSkipped, ref nearDt, 1) && nearDt == 0.01f && new ClientConfig().FarAnimationLod,
                "0.0.116: an NPC animated every 3rd frame runs 4 times in 12 frames and still advances the full 12 frames' time; near ones run every frame; on by default");
            // ---- 0.0.117 mounted riders ----
            string[] riderCodes = entityFiles.Keys.Where(c => c.EndsWith("-rider")).OrderBy(c => c).ToArray();
            string[] MountsOf(string code) => entityFiles[code]["attributes"]["lotrnpcsMounts"]?.Select(x => (string)x).ToArray() ?? Array.Empty<string>();
            Check(riderCodes.SequenceEqual(new[] { "angmarorc", "arnor", "dale", "dwarf", "elf", "gondor", "gundabadorc", "mordororc", "rohan", "uruk" }.Select(r => "test-" + r + "-rider"))
                && riderCodes.All(c => (string)entityFiles[c]["attributes"]["lotrnpcsRank"] == "rider" && entityFiles[c]["server"]["behaviors"].Any(b => (string)b["code"] == "lotrnpcs:rider") && MountsOf(c).Length > 0)
                && MountsOf("test-rohan-rider").All(m => m.StartsWith("feverstonewilds:horse-tamed-")) && MountsOf("test-uruk-rider").All(m => m.StartsWith("feverstonewilds:tame-direwolf-"))
                && MountsOf("test-elf-rider").All(m => m.StartsWith("game:tameddeer-elk-")) && MountsOf("test-dwarf-rider").All(m => m.StartsWith("teapotsgiantboar:tame-giantboar-")),
                "0.0.117: riders for Men of the West on horses, Shadow orcs on dire wolves, elves on elk and dwarves on giant boars, each with the rider behavior and its mounts");
            var riderConfig = new FactionConfig { Version = FactionConfig.CurrentVersion }; riderConfig.FillDefaults();
            Check(Promotion.StatRank("rider") == "warrior" && Promotion.PriceRank("rider") == "captain" && Promotion.StatRank("guard") == "warrior" && Promotion.PriceRank("shock") == "warrior"
                && HiringSystem.PriceFor(new FactionDef { HirePrices = new Dictionary<string, int> { ["captain"] = 40, ["warrior"] = 15 } }, "rider") == 40,
                "0.0.117: a rider costs a captain's price unless the config lists its own");
            {
                FactionDef rohanDef = riderConfig.Def("rohan");
                float scale = FactionConfig.RiderScale(rohanDef), wh = riderConfig.HealthFor("rohan", "warrior") ?? 0, wd = riderConfig.DamageFor("rohan", "warrior") ?? 0;
                double expect = Math.Pow(rohanDef.HirePrices["captain"] / (double)rohanDef.HirePrices["warrior"], FactionConfig.RiderStatExponent);
                var own = new FactionDef { Health = new Dictionary<string, float> { ["warrior"] = 34, ["rider"] = 50 }, Damage = new Dictionary<string, float> { ["warrior"] = 4 } };
                Check(Math.Abs(scale - expect) < 1e-4 && scale > 2 && scale < 2.5 && riderConfig.HealthFor("rohan", "rider") == (float)Math.Round(wh * scale)
                    && Math.Abs((riderConfig.DamageFor("rohan", "rider") ?? 0) - wd * scale) < 0.006 && riderConfig.HealthFor("rohan", "guard") == riderConfig.HealthFor("rohan", "guard")
                    && FactionConfig.RiderScale(null) == (float)FactionConfig.DefaultRiderScale && FactionConfig.RiderScale(own) == (float)FactionConfig.DefaultRiderScale,
                    "0.0.130: a rider's health and damage are its faction's warrior's times (captain price / warrior price)^0.82, about 2.2 (worth its coin in warriors), unless the config lists its own");
            }
            Check(!Riders.CanHaveMount(null, null) && Riders.AvailableMounts(null, null).Length == 0, "0.0.117: no world or no type means no mount (the rank isn't offered)");
            var rr = new Random(3);
            var withRider = SpawnZoneSystem.WithRider(new List<string> { "recruit", "warrior", "warrior" }, rr, 1);
            var noRider = SpawnZoneSystem.WithRider(new List<string> { "recruit", "warrior" }, rr, 0);
            var noWarrior = SpawnZoneSystem.WithRider(new List<string> { "recruit", "recruit" }, rr, 1);
            Check(withRider.SequenceEqual(new[] { "recruit", "rider", "warrior" }) && noRider.SequenceEqual(new[] { "recruit", "warrior" }) && noWarrior.SequenceEqual(new[] { "recruit", "recruit" }),
                "0.0.117: a wild group's first warrior rides at RiderChance; a group without warriors gets no rider");
            Check(Math.Abs(Riders.TurnToward(0, 2, 0.5f) - 0.5f) < 1e-4 && Math.Abs(Riders.TurnToward(0, -2, 0.5f) + 0.5f) < 1e-4 && Riders.TurnToward(1, 1.2f, 0.5f) == 1.2f
                && Riders.SpeedFor(0.5, true, 0.03f) == 0 && Riders.SpeedFor(2, false, 0.03f) == Riders.WalkSpeed && Riders.SpeedFor(2, true, 0.03f) == Riders.SprintSpeed && Riders.SpeedFor(5, false, 0.03f) == Riders.SprintSpeed,
                "0.0.117: a mount turns toward its rider's next waypoint at a limited rate, stands within half a block of it (0.0.126), walks to near ones and runs to far ones or when the rider sprints");
            Check(Riders.SpeedFor(20, false, 0.012f) == Riders.WalkSpeed && Riders.SpeedFor(20, true, 0.02f) == Riders.WalkSpeed && Riders.SpeedFor(20, false, 0.027f) == Riders.WalkSpeed
                && Riders.SpeedFor(20, false, 0.04f) == Riders.SprintSpeed && Riders.SpeedFor(0.8, false, 0.012f) == Riders.WalkSpeed
                && Riders.CloseInSpeed(4) == Riders.WalkSpeed && Riders.CloseInSpeed(2) == 0 && Riders.CloseInDistance < 2 + Riders.MountedExtraReach,
                "0.0.126: a wandering, patrolling or walking-follow rider walks its mount however far the waypoint (only seek, hide or a running follow may run); in a fight a standing mount walks in until its enemy is within the rider's reach");
            Check(EntityBehaviorHeldWeapon.PickWeapon(Looks.NoWeapon, new[] { new NpcWeapon { Item = "lotrnpcs:orcweapon-axe" } }, new Random(1)) == Looks.NoWeapon, "an orc whose weapon was taken away stays unarmed after a reload");
            Check(!Looks.NameOnHire(lnpc, new Random(1)) && Looks.NameOf(lnpc) == "Ugrath", "a recruit its owner named keeps its name when hired again");

            // Choosing the helmet (0.0.58).
            string[] TypeHelmets(string code) => Npc(code).Properties.Attributes?[Looks.HelmetsAttr].AsArray<string>(null) ?? Array.Empty<string>();
            string[] mordRecruit = TypeHelmets("test-mordororc-recruit");
            Check(mordRecruit.Length == 3 && mordRecruit.Any(c => c.Contains("servanthelmetorcmedium")) && mordRecruit.Any(c => c.Contains("angmarhelmetmedium")) && mordRecruit.Any(c => c.Contains("servanthelmetorcheavy")),
                "0.0.82: a Mordor recruit may choose the Mordor raider and warrior helmets and the Angmar orc helmet (" + string.Join(", ", mordRecruit) + ")");
            // 0.0.112 (owner: "make sure they have the correct helmets whitelisted", "specifically orcs"): orcs and Uruk-hai
            // offer only their own unit's helmets from the 0.0.82 list; other factions keep the lower ranks' too (0.0.76).
            string[] OwnHelmets(string code) => ((JArray)entityFiles[code]["server"]["behaviors"].First(b => (string)b["code"] == "lotrnpcs:startinggear")["sets"])
                .SelectMany(st => st.Select(c => (string)c)).Where(c => c.Contains("armor-head-") && !c.Contains('@')).Distinct().ToArray();
            var orcUnits = entityFiles.Keys.Where(c => new[] { "mordororc", "angmarorc", "goblin", "uruk", "gundabadorc" }.Any(f => c.StartsWith("test-" + f + "-"))).ToArray();
            Check(orcUnits.Length >= 15 && orcUnits.All(c => TypeHelmets(c).SequenceEqual(OwnHelmets(c)))
                && TypeHelmets("test-mordororc-captain").SequenceEqual(new[] { "mord:armor-head-tierfourplate-servanthelmethumanheavy-servanthelmethumanheavy-maskred" })
                && TypeHelmets("test-mordororc-warrior").SequenceEqual(new[] { "mord:armor-head-tierfourplate-servanthelmetorcheavy-servanthelmetorcheavy-none" })
                && TypeHelmets("test-uruk-captain").All(c => c.Contains("urukhaihelmetgeneral")) && TypeHelmets("test-uruk-warrior").All(c => !c.Contains("scout") && !c.Contains("general"))
                && TypeHelmets("test-gondor-recruit").All(TypeHelmets("test-gondor-captain").Contains),
                "0.0.112: each orc and Uruk unit may choose only its own helmets (a Mordor captain the red mask, a Black Uruk the Orc Warrior helmet, Uruk captains the General helmets); other factions' ranks still choose the helmets beneath them (0.0.76)");
            Check(TypeHelmets("test-easterling-captain").All(c => !c.Contains('@')) && TypeHelmets("test-easterling-captain").Any(c => c.Contains("woodlandhelmetmedium")),
                "the Easterling captain's face coif isn't offered as a helmet");
            Check(TypeHelmets("test-hobbit").Length == 0, "hobbits have no helmets to choose (their hats aren't helmets)");
            Check(TypeHelmets("test-gondor-recruit").Length == 3 && TypeHelmets("test-gondor-recruit").Any(c => c.Contains("gondorlevyhelmet")), "0.0.82: a Gondor Man-at-Arms chooses the Man-at-Arms helmet with or without chain, or the Levy helmet");

            string[] hchoices = Looks.HelmetChoices(new[] { "armor-head-plate-iron", "mord:armor-head-missing" }, c => !c.Contains("missing"), new ItemStack(helmItem), null);
            Check(hchoices.SequenceEqual(new[] { "game:armor-head-plate-iron", "game:armor-head-chain-iron" }),
                "the choices are the type's helmets that exist, with domains, then the one it wears (" + string.Join(", ", hchoices) + ")");
            var chead = new DummySlot(new ItemStack(helmItem));
            string[] cchoices = { "game:armor-head-chain-iron", "game:armor-head-plate-iron" };
            Check(Looks.Apply(lnpc, "uidOwner", new LooksRequest { Helmet = "game:armor-head-leather" }, lparts, lweapons, chead, null, cchoices) == "looks-badhelmet" && chead.Itemstack?.Collectible == helmItem,
                "a helmet that isn't one of its choices is refused");
            Check(Looks.Apply(lnpc, "uidOwner", new LooksRequest { Helmet = Looks.NoHelmet }, lparts, lweapons, chead, null, cchoices) == null && chead.Empty && Looks.KeptHelmet(lnpc.WatchedAttributes, null)?.Collectible == helmItem,
                "choosing no helmet takes it off and keeps it");
            Check(Looks.Apply(lnpc, "uidOwner", new LooksRequest { Helmet = "armor-head-chain-iron" }, lparts, lweapons, chead, null, cchoices) == null && chead.Itemstack?.Collectible == helmItem && !lnpc.WatchedAttributes.HasAttribute(Looks.KeptHelmetAttr),
                "choosing the kept helmet (code without its domain) puts it back on");
            Check(Looks.Apply(lnpc, "uidOwner", new LooksRequest { Helmet = "game:armor-head-chain-iron" }, lparts, lweapons, chead, null, cchoices) == null && chead.Itemstack?.Collectible == helmItem,
                "choosing the helmet it already wears changes nothing");
            Check(!Looks.WearHelmet(chead, new TreeAttribute(), "game:armor-head-plate-iron", null) && chead.Itemstack?.Collectible == helmItem,
                "without a world to make the new helmet, the old one stays on (new helmets are checked in game)");

            var fresh = Npc("test-gondor-recruit");
            string[] gondorNames = fresh.Properties.Attributes["lotrnpcsNames"].AsArray<string>();
            Check(Looks.NameOnHire(fresh, new Random(5)) && gondorNames.Contains(Looks.NameOf(fresh)) && fresh.WatchedAttributes.GetBool(Looks.NamedAttr), "a hired Gondor recruit gets a name from Gondor's list (" + Looks.NameOf(fresh) + ")");
            var hob = Npc("test-hobbit");
            Looks.NameOnHire(hob, new Random(6));
            string[] hobNames = hob.Properties.Attributes["lotrnpcsNames"].AsArray<string>(), hobFamilies = hob.Properties.Attributes["lotrnpcsSurnames"].AsArray<string>();
            string hobName = Looks.NameOf(hob);
            Check(hobName != null && hobName.Split(' ').Length == 2 && hobNames.Contains(hobName.Split(' ')[0]) && hobFamilies.Contains(hobName.Split(' ')[1]), "a hobbit gets a first name and a family name (" + hobName + ")");
            var shortLists = types.Where(kv => kv.Key != "test-hobbit").Where(kv => { var n = kv.Value.Attributes?["lotrnpcsNames"].AsArray<string>(); return n == null || n.Distinct().Count() < 40 || n.Any(x => Looks.CleanName(x) != x); }).Select(kv => kv.Key).ToList();
            Check(shortLists.Count == 0 && hobNames.Distinct().Count() >= 40 && hobFamilies.Distinct().Count() >= 20, "every NPC type has 40 or more unique valid names" + (shortLists.Count > 0 ? " (missing: " + string.Join(", ", shortLists) + ")" : ""));
            Check(types["test-mordororc-recruit"].Attributes["lotrnpcsNames"].AsArray<string>().SequenceEqual(types["test-angmarorc-captain"].Attributes["lotrnpcsNames"].AsArray<string>())
                && !types["test-mordororc-recruit"].Attributes["lotrnpcsNames"].AsArray<string>().SequenceEqual(types["test-uruk-recruit"].Attributes["lotrnpcsNames"].AsArray<string>()), "orcs of Mordor and Angmar share a list, Uruk-hai have their own");

            Check(FactionConfig.InstructionText.Any(l => l.Contains("HelmetChance") && l.Contains("CaptainsAlwaysHelmeted")), "#Instructions explain the helmet settings");
        }

        // ---- 0.0.55: wider battle spacing, shared orc helmets, the Mordor shield, archers, hobbit clothes ----
        {
            var reachSpacing = new BattleSpacing();
            // Melee reaches 2 blocks from the attacker's front (0.3 ahead of its middle) to the target's body (0.3 wide).
            Check(reachSpacing.DuelMax - 0.3 - 0.3 < 2.0, "attackers at the far end of the duel band (1.6 blocks from their target's middle) are still inside melee reach");
            Check(FactionConfig.InstructionText.Any(l => l.StartsWith("ShieldChance") && l.Contains("poleaxe")), "#Instructions explain ShieldChance");

            // Shared orc helmets.
            JArray Sets(string code) => (JArray)GearSets(entityFiles[code]);
            List<string> Heads(string code) => Sets(code).Select(st => st.Select(c => (string)c).First(c => c.Contains(":armor-head-"))).Distinct().OrderBy(h => h).ToList();
            string mRaid = "mord:armor-head-tierthreechain-servanthelmetorcmedium-servanthelmetorcmedium-none", aRaid = "angmar:armor-head-tierthreescale-angmarhelmetmedium-angmarhelmetmedium-none";
            string mWar = "mord:armor-head-tierfourplate-servanthelmetorcheavy-servanthelmetorcheavy-none", aSol = "angmar:armor-head-tierfourplate-angmarhelmetheavy-angmarhelmetheavy-none";
            string moria = "norc:armor-head-tierthreechain-norchelmetmedium-norchelmetmedium-none", gunda = "norc:armor-head-tierfourplate-norchelmetheavy-norchelmetheavy-none";
            // 0.0.82 (owner's loadout list) replaces the shared helmets with each unit's own list.
            string aSal = "angmar:armor-head-tierfourplate-angmarhelmetheavy2-angmarhelmetheavy2-none", tusk = "norc:armor-head-tierfourplate-norchelmetheavytwo-norchelmetheavytwo-none";
            bool helmetsOk = Heads("test-mordororc-recruit").SequenceEqual(new[] { aRaid, mRaid, mWar }.OrderBy(h => h)) && Heads("test-mordororc-warrior").SequenceEqual(new[] { mWar })
                && Heads("test-angmarorc-recruit").SequenceEqual(new[] { aRaid, mRaid, mWar, tusk }.OrderBy(h => h)) && Heads("test-angmarorc-warrior").SequenceEqual(new[] { aSol, aSal }.OrderBy(h => h))
                && Heads("test-goblin-recruit").SequenceEqual(new[] { moria, gunda, mRaid }.OrderBy(h => h)) && Heads("test-goblin-warrior").SequenceEqual(new[] { gunda, mWar, moria }.OrderBy(h => h));
            Check(helmetsOk, "0.0.82: Mordor orcs may wear the raider, warrior or Angmar orc helmet, Black Uruks only the warrior one; Angmar orcs both Mordor helmets, the Gundabad tusked or the Angmar orc helmet, soldiers either soldier helmet; Moria orcs the Moria, Gundabad or snaga (Mordor raider) helmet, Gundabad orcs (Misty Mountains warriors) the Gundabad, black uruk or, since 0.0.115, Moria helmet");
            Check(Sets("test-mordororc-warrior").All(st => st.Select(c => (string)c).Where(c => c.Contains("servant") && !c.Contains("helmet")).Select(c => c.EndsWith("black-none") ? "black" : c.EndsWith("red-none") ? "red" : "?").Distinct().Count() == 1)
                && Sets("test-gondor-recruit").All(st => { var b = st.Select(c => (string)c).First(c => c.Contains(":armor-body-")); var l = st.Select(c => (string)c).First(c => c.Contains(":armor-legs-")); string col = new[] { "blue", "black", "green", "yellow" }.FirstOrDefault(x => b.Contains("arms" + x + "-")) ?? "black"; return l.Contains("medium" + col + "-"); }),
                "every colour variant is worn as a whole set: body and legs the same colour");
            bool ownBodies = new[] { "mordororc", "angmarorc", "goblin" }.All(f => new[] { "recruit", "warrior" }.All(r => Sets($"test-{f}-{r}").All(st => st.Select(c => (string)c).Where(c => !c.Contains(":armor-head-")).All(c => c.StartsWith(f == "mordororc" ? "mord:" : f == "angmarorc" ? "angmar:" : "norc:")))));
            Check(ownBodies, "...each still wears its own faction's body and leg armor");
            Check(Sets("test-mordororc-captain").Count == 1 && Sets("test-angmarorc-captain").Count == 2 && Sets("test-goblin-captain").Count == 2 && Sets("test-uruk-recruit").Count == 1 && Sets("test-uruk-warrior").Count == 6
                && Heads("test-uruk-recruit").All(h => h.StartsWith("iseng:")) && Heads("test-uruk-warrior").All(h => !h.Contains("scout") && !h.Contains("general")) && Heads("test-uruk-captain").All(h => h.Contains("general")),
                "0.0.82: captains keep their own faction's helmets (Gundabad warlords may wear the Gundabad orc helmet too); Uruk-hai warriors any Uruk helmet but scout and general, captains only general ones");
            Check((string)Sets("test-mordororc-recruit")[0][0] == mRaid && (string)Sets("test-angmarorc-warrior")[0][0] == aSol && (string)Sets("test-goblin-recruit")[0][0] == moria, "each faction's own helmet stays first, so portraits look as before");

            // The Mordor shield.
            var mw = HeldWeapon(entityFiles["test-mordororc-warrior"], "server");
            string ShieldOf(string c) => entityFiles[c]["server"]["behaviors"].Any(b => (string)b["code"] == "lotrnpcs:heldweapon") && HeldWeapon(entityFiles[c], "server")["shields"] is JArray sh ? string.Join("|", sh.Select(x => (string)x)) : null;
            string ShieldsWanted(string c) => c.EndsWith("-archer") || c.EndsWith("-shock") ? null : c.StartsWith("test-mordororc-") ? "lotrnpcs:orcweapon-shield|lotrnpcs:orcweapon-bnshield" : c.StartsWith("test-goblin-") ? "lotrnpcs:orcweapon-mmshield"
                : c.StartsWith("test-blacknumenorean-") ? "lotrnpcs:orcweapon-bnshield" : c.StartsWith("test-arnor-") ? "lotrnpcs:orcweapon-kiteshield" : c.StartsWith("test-dwarf-") ? "lotrnpcs:orcweapon-dwtowershield|lotrnpcs:orcweapon-dwshield" : c.StartsWith("test-easterling-") ? "lotrnpcs:orcweapon-eastshield"
                : c.StartsWith("test-gondor-") ? "lotrnpcs:orcweapon-gondorshield|lotrnpcs:orcweapon-boromirshield" : c.StartsWith("test-elf-") ? "lotrnpcs:orcweapon-elfshield"
                : c == "test-haradrim-recruit" ? "lotrnpcs:orcweapon-hdlightshield" : c == "test-haradrim-warrior" || c == "test-haradrim-captain" || c == "test-haradrim-guard" ? "lotrnpcs:orcweapon-hdheavyshield"
                : c.StartsWith("test-rohan-") || c.StartsWith("test-dale-") ? "game:shield-woodmetal" : c.StartsWith("test-dunlending-") ? "game:shield-crude"
                // 0.0.113: guards of factions without a shield carry a stand-in.
                : c == "test-uruk-guard" || c == "test-tormentedelf-guard" ? "game:shield-woodmetal" : c == "test-angmarorc-guard" || c == "test-gundabadorc-guard" ? "game:shield-crude"
                : c == "test-wickeddwarf-guard" ? "lotrnpcs:orcweapon-dwtowershield|lotrnpcs:orcweapon-dwshield" : null;
            Check(ShieldOf("test-mordororc-warrior") == "lotrnpcs:orcweapon-shield|lotrnpcs:orcweapon-bnshield" && entityFiles.Keys.All(c => ShieldOf(c) == ShieldsWanted(c)),
                "Mordor orcs (not archers) may carry the Mordor shield or (0.0.112) the Black Numenorean one, Misty Mountains orcs (0.0.71) theirs, Black Numenoreans their own (0.0.112; the kite shield went to Arnor), dwarves one of their two (0.0.76), Easterlings theirs (0.0.77), Gondor one of its two and elves theirs (0.0.94), Haradrim recruits the light and warriors and captains the heavy shield (0.0.97), Rohan and Dale the game's wood-and-metal and Dunlendings its crude shield (0.0.101); nobody else has one");
            var mmItem = JObject.Parse(File.ReadAllText(Path.Combine(assets, "itemtypes", "orcweapon-mmshield.json")));
            var mmShape = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "item", "orcweapon", "mmshield.json")));
            Check((string)mmItem["shape"]["base"] == "lotrnpcs:item/orcweapon/mmshield" && mmItem["tpOffHandTransform"] != null && lang["item-orcweapon-mmshield"] != null
                && ((JObject)mmShape["textures"]).Properties().All(p => ((string)p.Value).StartsWith("lotrnpcs:item/orcweapon/") ? File.Exists(Path.Combine(new[] { assets, "textures" }.Concat(((string)p.Value).Substring(9).Split('/')).ToArray()) + ".png") : (string)p.Value == "game:block/metal/tarnished/rusty-iron")
                && HeldWeapon(entityFiles["test-goblin-warrior"], "server")["weapons"].Select(w => (string)w["item"]).SequenceEqual(new[] { "lotrnpcs:orcweapon-mmsword", "lotrnpcs:orcweapon-mmspear", "lotrnpcs:orcweapon-dagger", "lotrnpcs:orcweapon-axe" })
                && HeldWeapon(entityFiles["test-goblin-warrior"], "server")["weapons"].All(w => w["twoHanded"] == null),
                "0.0.71: the Misty Mountains shield is held in the left hand with its textures bundled; 0.0.75: Misty Mountains orcs carry the sword or the spear, both one-handed, so the shield stays");
            Check(((JObject)mmShape["textures"]).Properties().Select(p => (string)p.Value).SequenceEqual(new[] { "lotrnpcs:item/orcweapon/atlas/mmshield" }),
                "0.0.72: the Misty Mountains shield draws from the owner's one packed Moria texture");
            NpcWeapon[] mwList = EntityBehaviorHeldWeapon.ReadWeapons(new JsonObject(mw["weapons"]));
            Check(mwList.Count(w => w.TwoHanded) == 1 && mwList.First(w => w.TwoHanded).Item == "lotrnpcs:orcweapon-poleaxe", "only the poleaxe is two-handed");
            var srand = new Random(3);
            int withAxe = Enumerable.Range(0, 1000).Count(_ => EntityBehaviorHeldWeapon.RollShield(true, "lotrnpcs:orcweapon-axe", mwList, 0.5, srand));
            int withPole = Enumerable.Range(0, 1000).Count(_ => EntityBehaviorHeldWeapon.RollShield(true, "lotrnpcs:orcweapon-poleaxe", mwList, 1, srand));
            int oldOrcs = Enumerable.Range(0, 100).Count(_ => EntityBehaviorHeldWeapon.RollShield(false, "lotrnpcs:orcweapon-axe", mwList, 1, srand));
            Check(withAxe > 430 && withAxe < 570 && withPole == 0 && oldOrcs == 0, "a new orc gets a shield half the time (" + withAxe + " of 1000), never with the poleaxe, and orcs from before 0.0.55 get none");
            var shieldItem = JObject.Parse(File.ReadAllText(Path.Combine(assets, "itemtypes", "orcweapon-shield.json")));
            var off = shieldItem["tpOffHandTransform"];
            // Where the grip bar's middle lands: hand attachment point (rotation y -180) * item rotation, then translation.
            double[] Rot(double x, double y, double z, double[] v)
            {
                double rx = x * Math.PI / 180, ry = y * Math.PI / 180, rz = z * Math.PI / 180;
                double a = v[0] * Math.Cos(rz) - v[1] * Math.Sin(rz), b = v[0] * Math.Sin(rz) + v[1] * Math.Cos(rz), c = v[2];
                double a2 = a * Math.Cos(ry) + c * Math.Sin(ry), c2 = -a * Math.Sin(ry) + c * Math.Cos(ry);
                double b3 = b * Math.Cos(rx) - c2 * Math.Sin(rx), c3 = b * Math.Sin(rx) + c2 * Math.Cos(rx);
                return new[] { a2, b3, c3 };
            }
            double[] gp = Rot((double)off["rotation"]["x"], (double)off["rotation"]["y"] - 180, (double)off["rotation"]["z"], new[] { 3.7 / 16, 0.75 / 16, 6.0 / 16 });
            double gripMiss = Math.Sqrt(Math.Pow(gp[0] + (double)off["translation"]["x"], 2) + Math.Pow(gp[1] + (double)off["translation"]["y"], 2) + Math.Pow(gp[2] + (double)off["translation"]["z"], 2));
            double[] up = Rot((double)off["rotation"]["x"], (double)off["rotation"]["y"] - 180, (double)off["rotation"]["z"], new[] { 1.0, 0, 0 });
            // 0.0.59 (user: 2.5 px "floats off of the body", none clipped): slid 1.0 px out along its face, so the grip bar
            // rests on the hand's surface. gripMiss is in the shield's units: 1.0 px / 16 / 0.6 scale = 0.104.
            Check(Math.Abs(gripMiss - 1.0 / 16 / 0.6) < 0.01 && (float)off["scale"] == 0.6f && Math.Abs(up[1]) < 0.05 && shieldItem["tpHandTransform"] != null, "0.0.59: the shield is held by its grip bar in the left hand, 1 px out to rest on the hand, across the forearm (user: \"perpendicular\"), at the weapons' scale (grip out by " + (gripMiss * 0.6 * 16).ToString("0.00") + " px, along the arm " + up[1].ToString("0.00") + ")");
            // 0.0.56: hired NPCs go by rank, then name.
            System.Func<string, bool> hasTitle = k => lang[k.Substring("lotrnpcs:".Length)] != null;
            Check(Recruit.TitleKey("mordor", "warrior", hasTitle) == "lotrnpcs:title-warrior" && (string)lang["title-warrior"] == "Warrior" && (string)lang["title-captain"] == "Captain" && (string)lang["title-recruit"] == "Recruit" && (string)lang["title-archer"] == "Archer"
                && Recruit.TitleKey("shire", "recruit", hasTitle) == "lotrnpcs:title-shire-recruit" && (string)lang["title-shire-recruit"] == "Farmer" && Recruit.TitleKey("mordor", null, hasTitle) == null && Recruit.TitleKey("gondor", "Captain", hasTitle) == "lotrnpcs:title-captain",
                "0.0.56: the rank word before a hired NPC's name (Shire hobbits are farmers, rankless NPCs have none)");
            Check(((string)lang["recruit-yours"]).Contains("Your {3}") && !((string)lang["recruit-yours"]).Contains("recruit") && ((string)lang["recruit-died"]) == "Your {0} has died." && ((string)lang["recruit-of"]).Contains("{1}") && !((string)lang["recruit-yours-guard"]).Contains("recruit") && !((string)lang["recruit-yours-borrowed"]).Contains("recruit") && !((string)lang["looks-notyours"]).Contains("recruit"),
                "0.0.56: what an NPC's owner and others read names it by rank and name, not \"recruit\"");
            // 0.0.56 (user: "FA has the tusks. lotr chars does not."): Mordor orcs wear LRC's own tuskless orc faces.
            string orcModel = File.ReadAllText(Path.Combine(argv[0], "..", "config", "customplayermodels", "mordororc.json"));
            var orcFaces = System.Text.RegularExpressions.Regex.Matches(orcModel, "\"lotrnpcs:seraphfaces/([a-z-]+)\"").Select(m => m.Groups[1].Value).ToList();
            Check(!orcModel.Contains("face/orc/") && orcFaces.Count == 4 && orcFaces.Contains("angry") && orcFaces.Contains("determined")
                && orcFaces.All(f => File.Exists(Path.Combine(assets, "shapes", "seraphfaces", f + ".json")) && !File.ReadAllText(Path.Combine(assets, "shapes", "seraphfaces", f + ".json")).Contains("orkteeth")),
                "0.0.56: Mordor orc faces are LRC's tuskless ones (" + string.Join(", ", orcFaces) + "), and orcs from before keep their expression code");
            Check(lang["item-orcweapon-shield"] != null && lang["looks-shield"] != null && lang["looks-shieldtwohanded"] != null && lang["item-orcweapon-bow"] != null, "the shield, the bow and the new looks messages have their text");

            var snpc = Npc("test-mordororc-warrior"); snpc.EntityId = 778;
            Recruit.Bind(snpc, "uidOwner", "Owner");
            var srule = new Looks.ShieldRule { TwoHanded = new[] { "lotrnpcs:orcweapon-poleaxe" } };
            string[] sweapons = { "lotrnpcs:orcweapon-axe", "lotrnpcs:orcweapon-poleaxe", Looks.NoWeapon };
            snpc.WatchedAttributes.SetString(EntityBehaviorHeldWeapon.WeaponAttr, "lotrnpcs:orcweapon-axe");
            Check(Looks.Apply(snpc, "uidOwner", new LooksRequest { Shield = "on" }, new Dictionary<string, string[]>(), sweapons, null, srule) == null && snpc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.ShieldAttr) == "on", "the owner can give an axe-carrying orc its shield");
            Check(Looks.Apply(snpc, "uidOwner", new LooksRequest { Weapon = "lotrnpcs:orcweapon-poleaxe" }, new Dictionary<string, string[]>(), sweapons, null, srule) == null && snpc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.ShieldAttr) == "off",
                "taking up the poleaxe puts the shield away");
            Check(Looks.Apply(snpc, "uidOwner", new LooksRequest { Shield = "on" }, new Dictionary<string, string[]>(), sweapons, null, srule) == "looks-shieldtwohanded" && snpc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.ShieldAttr) == "off"
                && Looks.Apply(snpc, "uidOwner", new LooksRequest { Weapon = "lotrnpcs:orcweapon-poleaxe", Shield = "on" }, new Dictionary<string, string[]>(), sweapons, null, srule) == "looks-shieldtwohanded",
                "the shield can't be put on with the poleaxe, nor both asked for at once");
            Check(Looks.Apply(snpc, "uidOwner", new LooksRequest { Weapon = "lotrnpcs:orcweapon-axe", Shield = "on" }, new Dictionary<string, string[]>(), sweapons, null, srule) == null && snpc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.ShieldAttr) == "on"
                && Looks.Apply(snpc, "uidOwner", new LooksRequest { Shield = "off" }, new Dictionary<string, string[]>(), sweapons, null, srule) == null && snpc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.ShieldAttr) == "off",
                "swapping back to the axe with the shield works, and the shield comes off again (the choice is a saved attribute, so it survives a reload)");
            Check(Looks.Apply(snpc, "uidOwner", new LooksRequest { Shield = "on" }, new Dictionary<string, string[]>(), sweapons, null, null) == "looks-badshield" && Looks.Apply(snpc, "uidStranger", new LooksRequest { Shield = "on" }, new Dictionary<string, string[]>(), sweapons, null, srule) == "looks-notyours",
                "an NPC without a shield can't get one, and only the owner may change it");

            // Archers: one unit, Mordor first.
            var arch = entityFiles["test-mordororc-archer"];
            var archTasks = arch["server"]["behaviors"].First(b => (string)b["code"] == "taskai")["aitasks"];
            var shoot = archTasks.FirstOrDefault(t => (string)t["code"] == "lotrnpcs:shootattack");
            Check(shoot != null && (float)shoot["minRange"] == 2 && (float)shoot["range"] == 24 && (float)shoot["verticalRange"] == 20, "archers step back from enemies within 2 blocks and shoot 24 blocks out and 20 up or down (0.0.74)");
            var warriorMelee = entityFiles["test-mordororc-warrior"]["server"]["behaviors"].First(b => (string)b["code"] == "taskai")["aitasks"].First(t => (string)t["code"] == "lotrnpcs:meleeattack");
            Check(types["test-mordororc-archer"].Attributes["lotrnpcsRank"].AsString() == "archer" && types["test-mordororc-archer"].Attributes["lotrnpcsFaction"].AsString() == "mordor"
                && entityFiles.Keys.Count(c => c.EndsWith("-archer")) == 17, "one archer unit for every faction but the Shire (0.0.70; Mordor's since 0.0.55, the orcs' since 0.0.69)");
            string[] orcArchers = entityFiles.Keys.Where(c => c.EndsWith("-archer") && c != "test-mordororc-archer").ToArray();
            var archerFactions = orcArchers.Select(c => types[c].Attributes["lotrnpcsFaction"].AsString()).Append("mordor").ToList();
            Check(archerFactions.Distinct().Count() == 17 && !archerFactions.Contains("shire") && FactionConfig.DefaultFactions().Keys.All(f => f == "shire" || archerFactions.Contains(f)), "...one per faction, every faction but the Shire");
            string GameBowOf(string c) => c is "test-elf-archer" or "test-easterling-archer" or "test-haradrim-archer" ? "game:bow-recurve" : c == "test-dunlending-archer" ? "game:bow-simple" : "game:bow-long";
            var orcArcherFaction = orcArchers.ToDictionary(c => c, c => types[c].Attributes["lotrnpcsFaction"].AsString());
            JToken TaskList(string c) => entityFiles[c]["server"]["behaviors"].First(b => (string)b["code"] == "taskai")["aitasks"];
            Check(orcArchers.All(c => types[c].Attributes["lotrnpcsRank"].AsString() == "archer" && types[c].Attributes["lotrnpcsFaction"].AsString() == orcArcherFaction[c]
                    && JToken.DeepEquals(TaskList(c).First(t => (string)t["code"] == "lotrnpcs:shootattack"), TaskList("test-mordororc-archer").First(t => (string)t["code"] == "lotrnpcs:shootattack"))
                    && (string)HeldWeapon(entityFiles[c], "server")["weapons"][0]["item"] == (c == "test-goblin-archer" ? "lotrnpcs:orcweapon-mmbow" : GameBowOf(c))
                    && (c == "test-gondor-archer" ? GearSets(entityFiles[c]).ToString().Contains("gondorrangerarmor") && !GearSets(entityFiles[c]).ToString().Contains("chain")
                        : GearSets(entityFiles[c]).ToString() == GearSets(entityFiles[c.Replace("-archer", "-recruit")]).ToString())
                    && JsonMaxHealth(c) == 34),
                "0.0.69/0.0.70: every other faction's archers shoot, carry the bow and have the health of Mordor's, in their own recruits' armor (Gondor's in the Ranger set, not the chainmail one, since 0.0.82)");
            Check(shoot != null && (float)shoot["range"] == 24 && (float)shoot["damage"] < (float)warriorMelee["damage"] && (string)shoot["damageType"] == "PiercingAttack"
                && (int)shoot["drawMs"] + (int)shoot["mincooldown"] >= 3000 && (int)shoot["drawMs"] + (int)shoot["maxcooldown"] <= 4000 && (string)shoot["projectileCode"] == "lotrnpcs:npcarrow",
                "archers shoot up to 24 blocks, every 3 to 4 seconds, for less than a warrior's sword");
            Check((double)archTasks.First(t => (string)t["code"] == "lotrnpcs:seekentity")["standOff"] == 16 && archTasks.Any(t => (string)t["code"] == "lotrnpcs:meleeattack")
                && entityFiles.Where(kv => !kv.Key.EndsWith("-archer")).All(kv => kv.Value["server"]["behaviors"].First(b => (string)b["code"] == "taskai")["aitasks"].All(t => (string)t["code"] != "lotrnpcs:shootattack" && t["standOff"] == null)),
                "archers stop 16 blocks from a target they can shoot and still fight in melee when cornered; nobody else changes");
            var bowHeld = HeldWeapon(arch, "server");
            Check((bool)bowHeld["fixed"] && bowHeld["weapons"].Count() == 1 && (string)bowHeld["weapons"][0]["item"] == "lotrnpcs:orcweapon-bow" && JToken.DeepEquals(bowHeld, HeldWeapon(arch, "client")),
                "archers carry their bow on both sides, and it can't be swapped");
            NpcWeapon[] bowList = EntityBehaviorHeldWeapon.ReadWeapons(new JsonObject(bowHeld["weapons"]));
            Check(EntityBehaviorHeldWeapon.PickWeapon(Looks.NoWeapon, bowList, new Random(1), true) == "lotrnpcs:orcweapon-bow" && EntityBehaviorHeldWeapon.PickWeapon(null, bowList, new Random(1), true) == "lotrnpcs:orcweapon-bow",
                "an archer always has its bow");
            var archAnims = arch["client"]["animations"].ToDictionary(a => (string)a["code"]);
            Check(new[] { (string)shoot["animation"], (string)shoot["releaseAnimation"], "lotrnpcs-bow-attack" }.All(a => archAnims.ContainsKey(a) && bodyAnims.Contains((string)archAnims[a]["animation"])),
                "the draw, loose and shove animations are in the archer's list and the body shape");

            // 0.0.59 (user: "update the animations of the bow to be at rest until it is drawn to fire"): no pose between
            // shots, the rest bow in the hand, and the drawn bow only while drawing and early in the release.
            NpcWeapon bowW = bowList[0];
            string drawA = (string)shoot["animation"], relA = (string)shoot["releaseAnimation"];
            // 0.0.73 (user: "whenever the bow is drawn back, can the string on it change to a pulled back state?"): poses in
            // order, the string pulled back by stages as the hand draws, then the straight drawn bow for the rest of the draw,
            // the first 200 ms of the release and the shove (0.0.59/0.0.60).
            Check(bowW.HoldAnimation == null && !archAnims.ContainsKey("lotrnpcs-bow-hold") && bowW.Poses.Select(p => p.Item).SequenceEqual(new[] {
                    "lotrnpcs:orcweapon-bow-pull3", "lotrnpcs:orcweapon-bow-pull2", "lotrnpcs:orcweapon-bow-pull1", "lotrnpcs:orcweapon-bow-drawn" })
                && bowW.Poses[0].While[drawA] == (1031, 0) && bowW.Poses[1].While[drawA] == (894, 0) && bowW.Poses[2].While[drawA] == (756, 0)
                && bowW.Poses[3].While.Count == 3 && bowW.Poses[3].While[drawA] == (0, 0) && bowW.Poses[3].While[relA] == (0, 200) && bowW.Poses[3].While["lotrnpcs-bow-attack"] == (0, 0),
                "archers have no ready pose; the string pulls back in three stages from 756, 894 and 1031 ms of the draw, and the straight drawn bow covers the rest of the draw, 200 ms of the release and the shove");
            var legacy = EntityBehaviorHeldWeapon.ReadWeapons(new JsonObject(JToken.Parse("[{item: 'bow', attackAnimation: 'a', drawnItem: 'bow-drawn', drawnWhile: { 'Draw': 0, 'loose': 200 }}]")))[0];
            Check(legacy.Poses.Count == 1 && legacy.Poses[0].Item == "lotrnpcs:bow-drawn" && legacy.Poses[0].While["draw"] == (0, 0) && legacy.Poses[0].While["loose"] == (0, 200),
                "the 0.0.59 drawnItem/drawnWhile form still reads, as one pose");
            // 0.0.60 (user: "the bow is held wrong"): the game tracks running animations by the shape animation's name, so
            // the codes must be looked up in the archer's own list. 0.0.59 asked for the codes and never saw them run.
            AnimationMetaData[] archMetas = arch["client"]["animations"].Select(a => new AnimationMetaData { Code = (string)a["code"], Animation = (string)a["animation"] }).ToArray();
            var tracked = bowW.Poses.SelectMany(p => p.While.Keys).Distinct().ToDictionary(k => k, k => EntityBehaviorHeldWeapon.TrackedAnimation(k, archMetas));
            Check(tracked.Count == 3 && tracked[drawA] == "bowaim" && tracked[relA] == "bowhit" && tracked["lotrnpcs-bow-attack"] == "hit"
                && EntityBehaviorHeldWeapon.TrackedAnimation("lotrnpcs-nothing", archMetas) == "lotrnpcs-nothing",
                "the bow's poses watch the animations the game actually runs: " + string.Join(", ", tracked.Values));
            var restBow = JObject.Parse(File.ReadAllText(Path.Combine(assets, "itemtypes", "orcweapon-bow.json")));
            var drawnBow = JObject.Parse(File.ReadAllText(Path.Combine(assets, "itemtypes", "orcweapon-bow-drawn.json")));
            Check((string)restBow["shape"]["base"] == "lotrnpcs:item/orcweapon/bow" && (string)drawnBow["shape"]["base"] == "lotrnpcs:item/orcweapon/bow"
                && (double)drawnBow["tpHandTransform"]["rotation"]["x"] == 14.1 && (double)restBow["tpHandTransform"]["rotation"]["z"] == -157.9 && lang["item-orcweapon-bow-drawn"] != null,
                "both bows share the model: the drawn one keeps the 0.0.55 full-draw hold, the rest one is carried level at the side (0.0.61, option C)");
            // The pulled bows: their own shapes, the drawn bow's hold, and a string of two segments meeting at the apex.
            var bowShape = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "item", "orcweapon", "bow.json")));
            bool pullsOk = true;
            foreach (var (name, apexY) in new[] { ("bow-pull1", -8.5), ("bow-pull2", -12.3), ("bow-pull3", -16.0) })
            {
                var item = JObject.Parse(File.ReadAllText(Path.Combine(assets, "itemtypes", "orcweapon-" + name + ".json")));
                var shape = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "item", "orcweapon", name + ".json")));
                var els = shape["elements"].ToList();
                var segs = els.Where(e => ((string)e["name"]).StartsWith("bowstring")).ToList();
                bool meet = segs.Count == 2 && segs.All(e => {
                    double len = (double)e["to"][0] - (double)e["from"][0], a = (double)e["rotationZ"] * Math.PI / 180;
                    double ex = (double)e["rotationOrigin"][0] + len * Math.Cos(a), ey = (double)e["rotationOrigin"][1] + len * Math.Sin(a);
                    return Math.Abs(ex - 2.2) < 0.01 && Math.Abs(ey - apexY) < 0.01; });
                pullsOk &= (string)item["shape"]["base"] == "lotrnpcs:item/orcweapon/" + name && JToken.DeepEquals(item["tpHandTransform"], drawnBow["tpHandTransform"])
                    && meet && !els.Any(e => (string)e["name"] == "bowstring") && els.Count == bowShape["elements"].Count() + 1 && lang["item-orcweapon-" + name] != null;
            }
            Check(pullsOk, "three pulled bows: the drawn bow's hold, and the string as two segments from the tips meeting at the draw hand (y -8.5, -12.3, -16.0)");
            var rtf = restBow["tpHandTransform"];
            double[] rg = Rot((double)rtf["rotation"]["x"], (double)rtf["rotation"]["y"] - 180, (double)rtf["rotation"]["z"], new[] { 2.6 / 16, 0.9 / 16, 0.5 });  // 0.0.71: the owner's bow, gripped at (2.6, 0.9, 8)
            double restMiss = Math.Sqrt(Math.Pow(rg[0] + (double)rtf["translation"]["x"], 2) + Math.Pow(rg[1] + (double)rtf["translation"]["y"], 2) + Math.Pow(rg[2] + (double)rtf["translation"]["z"], 2));
            Check(restMiss < 0.01, "the rest bow is held by its grip (off by " + restMiss.ToString("0.000") + ")");

            // 0.0.75 (user: yes to the Moria bow, sword and spear for the Misty Mountains orcs; "moria = misty mountains").
            double[] HandPoint(JToken tf, double gx, double gy, double gz)
            {
                double[] r = Rot((double)tf["rotation"]["x"], (double)tf["rotation"]["y"] - 180, (double)tf["rotation"]["z"], new[] { gx / 16, gy / 16, gz / 16 });
                return new[] { r[0] + (double)tf["translation"]["x"], r[1] + (double)tf["translation"]["y"], r[2] + (double)tf["translation"]["z"] };
            }
            double Gap(double[] a, double[] b) => Math.Sqrt(a.Zip(b, (x, y) => (x - y) * (x - y)).Sum());
            JToken Tf(string item) => JObject.Parse(File.ReadAllText(Path.Combine(assets, "itemtypes", "orcweapon-" + item + ".json")))["tpHandTransform"];
            var mmBowW = EntityBehaviorHeldWeapon.ReadWeapons(new JsonObject(HeldWeapon(entityFiles["test-goblin-archer"], "server")["weapons"]))[0];
            Check(mmBowW.Item == "lotrnpcs:orcweapon-mmbow" && mmBowW.Poses.Select(p => p.Item).SequenceEqual(new[] {
                    "lotrnpcs:orcweapon-mmbow-pull3", "lotrnpcs:orcweapon-mmbow-pull2", "lotrnpcs:orcweapon-mmbow-pull1", "lotrnpcs:orcweapon-mmbow-drawn" })
                && mmBowW.Poses[0].While[drawA] == (1031, 0) && mmBowW.Poses[1].While[drawA] == (928, 0) && mmBowW.Poses[2].While[drawA] == (825, 0)
                && JToken.DeepEquals(JToken.FromObject(mmBowW.Poses[3].While), JToken.FromObject(bowW.Poses[3].While))
                && (string)HeldWeapon(entityFiles["test-mordororc-archer"], "server")["weapons"][0]["item"] == "lotrnpcs:orcweapon-bow"
                && entityFiles.Keys.Where(c => c.EndsWith("-archer") && c != "test-goblin-archer" && c != "test-mordororc-archer").All(c => (string)HeldWeapon(entityFiles[c], "server")["weapons"][0]["item"] == GameBowOf(c) && HeldWeapon(entityFiles[c], "server")["weapons"][0]["poses"] == null),
                "Misty Mountains archers carry the Moria bow, its string pulled from 825, 928 and 1031 ms; Mordor keeps the orc bow; everyone else a game bow with no string stages: the longbow (0.0.76), the recurve for elves, Easterlings and Haradrim and the simple bow for Dunlendings (0.0.101)");
            Check(Gap(HandPoint(Tf("mmbow"), 3.25, 0.8, 8), HandPoint(Tf("bow"), 2.6, 0.9, 8)) < 0.002 && Gap(HandPoint(Tf("mmbow-drawn"), 3.25, 0.8, 8), HandPoint(Tf("bow-drawn"), 2.6, 0.9, 8)) < 0.002
                && JToken.DeepEquals(Tf("mmbow")["rotation"], Tf("bow")["rotation"]) && JToken.DeepEquals(Tf("mmbow-drawn")["rotation"], Tf("bow-drawn")["rotation"])
                && new[] { "mmbow-pull1", "mmbow-pull2", "mmbow-pull3" }.All(n => JToken.DeepEquals(Tf(n), Tf("mmbow-drawn"))),
                "the Moria bow is held at rest and drawn exactly as the Mordor bow, its own grip (3.25, 0.8, 8) in the hand");
            bool mmPullsOk = true;
            var mmBowShape = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "item", "orcweapon", "mmbow.json")));
            foreach (var (name, apexY) in new[] { ("mmbow-pull1", -10.5), ("mmbow-pull2", -13.3), ("mmbow-pull3", -16.1) })
            {
                var shape = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "item", "orcweapon", name + ".json")));
                var segs = shape["elements"].Where(e => ((string)e["name"]).StartsWith("bowstring")).ToList();
                mmPullsOk &= segs.Count == 2 && shape["elements"].Count() == mmBowShape["elements"].Count() + 1 && segs.All(e => {
                    double len = (double)e["to"][0] - (double)e["from"][0], a = (double)e["rotationZ"] * Math.PI / 180;
                    return Math.Abs((double)e["rotationOrigin"][0] + len * Math.Cos(a) - 2.85) < 0.01 && Math.Abs((double)e["rotationOrigin"][1] + len * Math.Sin(a) - apexY) < 0.01; })
                    && ((JObject)shape["textures"]).Properties().Select(p => (string)p.Value).SequenceEqual(new[] { "lotrnpcs:item/orcweapon/atlas/mmbow" });
            }
            Check(mmPullsOk && File.Exists(Path.Combine(assets, "textures", "item", "orcweapon", "atlas", "mmbow.png"))
                && !File.Exists(Path.Combine(assets, "textures", "item", "orcweapon", "atlas", "mmbow-pull1.png")),
                "the pulled Moria bows meet the draw hand at x 2.85 (y -10.5, -13.3, -16.1) and share the bow's one texture");
            var gw = HeldWeapon(entityFiles["test-goblin-warrior"], "client");
            var goblinAnims = entityFiles["test-goblin-warrior"]["client"]["animations"].ToDictionary(a => (string)a["code"], a => a);
            Check(Gap(HandPoint(Tf("mmsword"), 3.6, 0.9, 8), new double[] { 0, 0, 0 }) < 0.002 && Gap(HandPoint(Tf("mmspear"), -4, 0.75, 8), new double[] { 0, 0, 0 }) < 0.002
                && (string)goblinAnims["lotrnpcs-mmsword-attack"]["animation"] == "falx" && (string)goblinAnims["lotrnpcs-mmspear-attack"]["animation"] == "spearhit"
                && (string)goblinAnims["lotrnpcs-mmspear-hold"]["animation"] == "spearidle"
                && ((JObject)goblinAnims["lotrnpcs-mmspear-hold"]["elementWeight"]).Properties().Select(p => p.Name).OrderBy(n => n).SequenceEqual(new[] { "ItemAnchor", "LowerArmR", "UpperArmR" })
                && (string)gw["weapons"][1]["holdAnimation"] == "lotrnpcs-mmspear-hold" && gw["weapons"][0]["holdAnimation"] == null
                && JToken.DeepEquals(gw, HeldWeapon(entityFiles["test-goblin-warrior"], "server")) && HeldWeapon(entityFiles["test-goblin"], "server") == null
                && lang["item-orcweapon-mmsword"] != null && lang["item-orcweapon-mmspear"] != null && lang["item-orcweapon-mmbow"] != null,
                "the sword is swung like the scimitar and the spear thrust, each held by its grip; between thrusts only the right arm holds the spear level; the rankless legacy goblin stays unarmed");
            // 0.0.76: the owner's Blockbench weapons, each held by its grip (measured on the converted models).
            Check(new (string item, double x, double y, double z)[] { ("dwaxe", -3, 3.97, 7.37), ("dwbattleaxe", -13, 1.05, 8.05), ("dwdagger", -6, 2.5, 8.1), ("dwspear", -8.6, 0, 8), ("elfspear", 1.15, 0, 11.39), ("hdspear", 11, 1, 26.2), ("poleaxe", 9.27, 0.75, 8) }
                    .All(g => Gap(HandPoint(Tf(g.item), g.x, g.y, g.z), new double[] { 0, 0, 0 }) < 0.002) && Math.Abs((double)Tf("elfspear")["scale"] - 0.6) < 0.001,
                "the dwarven axe, battleaxe, dagger and boar spear, the elven spear (60 units since 0.0.94) and the Haradrim spear are each held by their grip; the poleaxe grip is 0.25 blocks further up the shaft (6.67 px)");
            JToken OffTf(string item) => JObject.Parse(File.ReadAllText(Path.Combine(assets, "itemtypes", "orcweapon-" + item + ".json")))["tpOffHandTransform"];
            double[] mordorGrip = HandPoint(OffTf("shield"), 3.7, 0.75, 6.0);
            Check(new (string item, double x, double y, double z)[] { ("kiteshield", 8.0, 21.9, 12.8 - 1.4 + 1.0), ("eastshield", 6.25, 16.0, 7.38 - 1.4 + 1.0), ("gondorshield", 7.5, 16.68, 7.3 - 1.4 + 1.75), ("dwtowershield", 5.25, 18.0, 7.3 - 1.4 + 1.0), ("dwshield", 7.0, 17.85, 4.9 - 1.4 + 1.0) }
                    .All(g => Gap(HandPoint(OffTf(g.item), g.x, g.y, g.z), mordorGrip) < 0.002 && Math.Abs((double)OffTf(g.item)["rotation"]["z"] - ((double)OffTf("shield")["rotation"]["z"] - 90)) < 0.01),
                "the new shields are held like the Mordor shield, turned a quarter to stand along the forearm, each by its handle slid 1.4 px out so it clears the arm (the dwarven ones 1 px back in since 0.0.96, owner: \"a little bit closer\"; 0.0.100: the kite and Easterling 1 px and the Gondorian 1.75 px in, so the hand meets the handle)");
            // 0.0.112: the owner's new swords, spears, Easterling axe and steel hammers, each held by its grip; the Black
            // Numenorean shield (no handle) where the kite shield's grip is, 0.1 px behind its rim.
            Check(new (string item, double x, double y, double z)[] { ("gdsword", 2.5, 0, 8), ("gdsteelsword", 2.5, 0, 8), ("rhsword", 2.5, 0, 8), ("bnsteelsword", 2.5, 0, 8), ("morgulblade", 2.5, 0, 8),
                    ("elflongsword", 14.5, 0, 8.3), ("bnspear", -6.8, 0, 8), ("rhspear", -1.2, 0.75, 8), ("eastaxe", 27, 0.75, 8), ("dwwarhammersteel", -28, 2.05, 8.05), ("dwshorthammersteel", 4, 1.05, 8.05) }
                    .All(g => Gap(HandPoint(Tf(g.item), g.x, g.y, g.z), new double[] { 0, 0, 0 }) < 0.002)
                && Gap(HandPoint(OffTf("bnshield"), 8.0, 16.0, 9.96), mordorGrip) < 0.002,
                "0.0.112: the new swords, spears, Easterling axe and steel hammers are each held by their grip, and the Black Numenorean shield where the kite shield's grip is");
            var converted = new[] { "elfspear", "kiteshield", "dwaxe", "dwbattleaxe", "dwtowershield", "dwshield", "dwdagger" };
            Check(converted.All(n => {
                    var shp = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "item", "orcweapon", n + ".json")));
                    return ((JObject)shp["textures"]).Properties().Select(p => (string)p.Value).SequenceEqual(new[] { "lotrnpcs:item/orcweapon/atlas/" + n })
                        && File.Exists(Path.Combine(assets, "textures", "item", "orcweapon", "atlas", n + ".png")) && lang["item-orcweapon-" + n] != null; })
                && ((JObject)JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "item", "orcweapon", "dwspear.json")))["textures"]).Properties().Select(p => (string)p.Value).OrderBy(v => v)
                    .SequenceEqual(new[] { "game:block/metal/ingot/iron", "game:item/tool/handle" }),
                "each converted Blockbench model draws from one packed texture of its own; the boar spear, saved without one, is painted with the game's wood and iron");
            var since = new Dictionary<string, long>();
            // Running animations as the game names them (bowaim, bowhit, hit), seen through the same lookup the game uses.
            string Pose(long now, params string[] running) => EntityBehaviorHeldWeapon.PoseNow(bowW, a => running.Select(r => tracked[r]).Contains(EntityBehaviorHeldWeapon.TrackedAnimation(a, archMetas)), now, since);
            string pzDrawn = "lotrnpcs:orcweapon-bow-drawn", pzP1 = "lotrnpcs:orcweapon-bow-pull1", pzP2 = "lotrnpcs:orcweapon-bow-pull2", pzP3 = "lotrnpcs:orcweapon-bow-pull3";
            // A draw starting at 1000 ms, held, the shot at 2600, the release until 3050, the next draw at 5000.
            Check(Pose(0) == null && Pose(1000, drawA) == pzDrawn && Pose(1755, drawA) == pzDrawn && Pose(1756, drawA) == pzP1 && Pose(1893, drawA) == pzP1 && Pose(1894, drawA) == pzP2
                && Pose(2030, drawA) == pzP2 && Pose(2031, drawA) == pzP3 && Pose(2500, drawA) == pzP3 && Pose(2600, relA) == pzDrawn && Pose(2799, relA) == pzDrawn && Pose(2800, relA) == null
                && Pose(3050) == null && Pose(5000, drawA) == pzDrawn && Pose(5800, drawA) == pzP1,
                "over a draw the string pulls back by stages, holds at full draw, snaps straight on the shot for 200 ms, then the bow rests; the next draw starts straight again");
            Check(Pose(9000, "lotrnpcs-bow-attack") == pzDrawn && EntityBehaviorHeldWeapon.PoseNow(null, a => true, 0, since) == null && since.Count == 0
                && EntityBehaviorHeldWeapon.PoseNow(EntityBehaviorHeldWeapon.ReadWeapons(new JsonObject(mw["weapons"]))[0], a => true, 0, since) == null,
                "the shove shows the upright bow; melee weapons never swap");
            Check(GearSets(arch).ToString() == GearSets(entityFiles["test-mordororc-recruit"]).ToString(), "archers wear the recruits' Orc Raider mail and helmets");
            var defs55 = new FactionConfig(); defs55.FillDefaults();
            Check(HiringSystem.PriceFor(defs55.Factions["mordor"], "archer") == 12 && HiringSystem.PriceFor(defs55.Factions["gondor"], "archer") == 15 && HiringSystem.RankOf(Npc("test-mordororc-archer")) == "archer" && Recruit.IsArcher(Npc("test-mordororc-archer")) && !Recruit.IsArcher(Npc("test-mordororc-warrior")),
                "an archer costs a warrior's price to hire (15 for Men, 12 for orcs since 0.0.108)");
            var names55 = types["test-mordororc-archer"].Attributes["lotrnpcsNames"].AsArray<string>();
            Check(names55 != null && names55.SequenceEqual(types["test-mordororc-recruit"].Attributes["lotrnpcsNames"].AsArray<string>()) && items["attributesByType"]["*-test-mordororc-archer"] != null
                && tabCodes.Contains("test-mordororc-archer"),
                "archers get orc names on hire, a portrait and a spawn item in the LOTR NPCs tab");
            var arrowFile = JObject.Parse(File.ReadAllText(Path.Combine(assets, "entities", "projectile", "npcarrow.json")));
            Check((string)arrowFile["class"] == "LotrNpcsArrow" && arrowFile["server"]["behaviors"].Any(b => (string)b["code"] == "despawn") && arrowFile["server"]["behaviors"].Any(b => (string)b["code"] == "passivephysics")
                && File.Exists(Path.Combine(assets, "shapes", "entity", "npcarrow.json")), "the arrow is our own projectile class with physics, a model, and despawns");

            // Aim and line of fire.
            var from = new Vec3d(0, 65.6, 0);
            bool arcsOk = true;
            foreach (var target in new[] { new Vec3d(10, 65.1, 3), new Vec3d(19, 63, -4), new Vec3d(5, 68, 5), new Vec3d(-15, 65.5, 8) })
            {
                double g = 0.75 * Vintagestory.API.Config.GlobalConstants.GravityPerSecond;
                if (!Archery.SolveArc(from, target, 28, g, out Vec3d v)) { arcsOk = false; continue; }
                double px = from.X, py = from.Y, pz = from.Z, vx = v.X, vy = v.Y, vz = v.Z, best = double.MaxValue;
                for (int tick = 0; tick < 600; tick++)
                {
                    px += vx; py += vy; pz += vz; vy -= g / 60.0;
                    best = Math.Min(best, Math.Sqrt((px - target.X) * (px - target.X) + (py - target.Y) * (py - target.Y) + (pz - target.Z) * (pz - target.Z)));
                }
                if (best > 0.25) { arcsOk = false; Console.WriteLine("  arc misses by " + best); }
            }
            Check(arcsOk, "the aim solves a low arc that passes through the target, up and down hill");
            Check(!Archery.SolveArc(from, new Vec3d(500, 65, 0), 28, 0.75 * Vintagestory.API.Config.GlobalConstants.GravityPerSecond, out _), "...and says when a target is out of reach");
            var sc = Archery.Scatter(new Vec3d(0.3, 0.05, 0.2), 2, -2);
            Check(Math.Abs(sc.Length() - new Vec3d(0.3, 0.05, 0.2).Length()) < 1e-9, "scattering a shot keeps its speed");
            var shotFrom = new Vec3d(0, 66, 0); var shotTo = new Vec3d(10, 66, 0);
            Check(Archery.FriendInLine(shotFrom, shotTo, new[] { new Vec3d(5, 65.9, 0.4) }) && !Archery.FriendInLine(shotFrom, shotTo, new[] { new Vec3d(5, 66, 1.2) })
                && !Archery.FriendInLine(shotFrom, shotTo, new[] { new Vec3d(-2, 66, 0) }) && !Archery.FriendInLine(shotFrom, shotTo, new[] { new Vec3d(12, 66, 0) }),
                "a friend in the line of fire stops the shot; one beside it, behind the archer or behind the target doesn't");
            var ffs = new FactionSystem(); var fcfg = new FactionConfig(); fcfg.FillDefaults();
            fcfg.Relations.Add(new FactionRelationDef { A = "mordor", B = "isengard", Relation = "ally" }); SetLegacy(ffs, fcfg);
            var archer = Npc("test-mordororc-archer"); archer.EntityId = 901;
            var buddy = Npc("test-mordororc-warrior"); buddy.EntityId = 902;
            var foe = Npc("test-gondor-warrior"); foe.EntityId = 903;
            var isengardOrc = Npc("test-uruk-warrior"); isengardOrc.EntityId = 904;
            Check(Archery.IsFriend(ffs, archer, buddy) && Archery.IsFriend(ffs, archer, isengardOrc) && !Archery.IsFriend(ffs, archer, foe) && !Archery.IsFriend(ffs, archer, archer),
                "the archer's friends are its own faction and its allies (here Mordor and Isengard, allied in the config), never its enemies");
            var stepAway = Archery.AwayFrom(new Vec3d(0, 64, 0), new Vec3d(2, 64, 0), 6);
            Check(Math.Abs(stepAway.X + 6) < 1e-9 && Math.Abs(stepAway.Z) < 1e-9, "an archer steps straight away from an enemy that gets too close");

            // Formations: archers take the back rank.
            var col = Formation.Offsets(Formation.Column, 4);
            var colSpots = col.Select(o => Formation.World(new Vec3d(0, 64, 0), 0, 1, o)).ToList();
            var colMembers = new List<Vec3d> { new Vec3d(0, 64, -5), new Vec3d(0, 64, -6), new Vec3d(1, 64, 5), new Vec3d(-1, 64, 5) };
            var colBack = new List<bool> { false, false, true, true };
            int[] colAt = Formation.Assign(colMembers, colSpots, col, colBack);
            double frontY = col.Max(o => o.Y), backY = col.Min(o => o.Y);
            Check(colAt.Distinct().Count() == 4 && col[colAt[2]].Y == backY && col[colAt[3]].Y == backY && col[colAt[0]].Y == frontY && col[colAt[1]].Y == frontY,
                "in a formation archers stand in the back rank even when they start in front");
            Check(Formation.Assign(colMembers, colSpots, col, new List<bool> { false, false, false, false }).SequenceEqual(Formation.Assign(colMembers, colSpots)), "...and a formation without archers is unchanged");

            // Spawning shares.
            var spawnCfg = new SpawnConfig();
            var sr = new Random(9);
            var withArchers = Enumerable.Range(0, 400).SelectMany(_ => SpawnZoneSystem.GroupRanks(spawnCfg, sr, false, true)).ToList();
            var noArchers = Enumerable.Range(0, 400).SelectMany(_ => SpawnZoneSystem.GroupRanks(spawnCfg, sr, false, false)).ToList();
            var captainGroups = Enumerable.Range(0, 400).Select(_ => SpawnZoneSystem.GroupRanks(spawnCfg, sr, true, true)).ToList();
            double archerShare = withArchers.Count(r => r == "archer") / (double)withArchers.Count;
            Check(spawnCfg.ArcherShare == 0.2 && archerShare > 0.08 && archerShare < 0.25 && !noArchers.Contains("archer") && captainGroups.All(gr => gr[0] == "captain") && captainGroups.Any(gr => gr.Contains("archer")),
                "zone groups include archers where the faction has them (" + (archerShare * 100).ToString("0") + "% of members), none elsewhere, and captains' escorts can include them");
            Check(SpawnConfig.InstructionText.Any(l => l.StartsWith("ArcherShare")), "#Instructions in the spawning config explain ArcherShare");

            // Hobbits: trader-style clothes, barefoot, a hat now and then.
            var hobSets = Sets("test-hobbit").Select(st => st.Select(c => (string)c).ToList()).ToList();
            Check(hobSets.Count >= 10 && hobSets.All(st => st.All(c => c.StartsWith("game:clothes-")) && st.Any(c => c.Contains("-upperbody-")) && st.Any(c => c.Contains("-lowerbody-"))
                && !st.Any(c => c.Contains("-foot-") || c.Contains("-head-") || c.Contains("armor"))),
                "hobbits wear one of " + hobSets.Count + " vanilla outfits, each a shirt and trousers with something more, no helmets and no shoes");
            var hats = entityFiles["test-hobbit"]["server"]["behaviors"].First(b => (string)b["code"] == "lotrnpcs:startinggear")["hats"];
            string[] hatCodes = hats["items"].Select(h => (string)h).ToArray();
            Check((double)hats["chance"] <= 0.15 && hatCodes.Length >= 4 && hatCodes.All(h => h.StartsWith("game:clothes-head-") && vanilla.Contains(h)), "a hobbit wears a vanilla hat only now and then (" + hats["chance"] + ")");
            var hr = new Random(4);
            int hatted = Enumerable.Range(0, 2000).Count(_ => EntityBehaviorStartingGear.PickHat(hatCodes, (double)hats["chance"], c => true, hr) != null);
            Check(hatted > 170 && hatted < 340 && EntityBehaviorStartingGear.PickHat(hatCodes, 1, c => c.EndsWith("straw-hat"), hr) == "game:clothes-head-straw-hat"
                && EntityBehaviorStartingGear.PickHat(hatCodes, 1, c => false, hr) == null && EntityBehaviorStartingGear.PickHat(hatCodes, 0, c => true, hr) == null,
                "about one hobbit in eight gets a hat (" + hatted + " of 2000), only one that exists");
            Check(entityFiles.Where(kv => kv.Key != "test-hobbit").All(kv => kv.Value["server"]["behaviors"].All(b => b["hats"] == null)), "nobody else gets random hats");
        }

        // 0.0.73: /recruits dismissall (user: "make an option in the chat to fire all your recruits").
        {
            var dh = new HiringSystem { Factions = new FactionSystem() };
            string daMe = "uidFirer", daOther = "uidOther";
            TestAgent DaHired(string code, long id, string owner) { var e = Npc(code); e.EntityId = id; Recruit.Bind(e, owner, "N"); return e; }
            var daA1 = DaHired("test-mordororc-recruit", 9101, daMe); var daA2 = DaHired("test-gondor-captain", 9102, daMe);
            var daLent = DaHired("test-dwarf-warrior", 9103, daMe); daLent.WatchedAttributes.SetString(Recruit.BorrowerUidAttr, daOther); daLent.WatchedAttributes.SetString(Recruit.BorrowerNameAttr, "Friend");
            var daTheirs = DaHired("test-elf-recruit", 9104, daOther); daTheirs.WatchedAttributes.SetString(Recruit.BorrowerUidAttr, daMe);
            daA2.Attributes.SetString("lotrnpcsOriginBeforeHire", "zone");
            dh.SetOwner(daMe, new HiringOwner { Name = "Firer", Recruits = new List<long> { 9101, 9102, 9103, 9105 } });
            dh.SetOwner(daOther, new HiringOwner { Name = "Other", Recruits = new List<long> { 9104 } });
            var daLoaded = new Dictionary<long, EntityAgent> { [9101] = daA1, [9102] = daA2, [9103] = daLent, [9104] = daTheirs };
            Check(dh.Count(daMe) == 4 && dh.DismissAll("uidNobody", id => null) == (0, 0), "nothing happens for a player without recruits");
            var daRes = dh.DismissAll(daMe, id => daLoaded.TryGetValue(id, out var e) ? e : null);
            Check(daRes == (3, 1) && dh.Count(daMe) == 0 && dh.Owner(daMe).PendingDismiss.SequenceEqual(new long[] { 9105 })
                && Recruit.OwnerUidOf(daA1) == null && Recruit.OwnerUidOf(daA2) == null && Recruit.OwnerUidOf(daLent) == null && Recruit.BorrowerUidOf(daLent) == null
                && daA2.Attributes.GetString(SpawnZoneSystem.OriginAttr) == "zone",
                "dismissing all frees the loaded recruits at once (a lent guard too, an old zone NPC goes back to its zone) and marks the far-away one for later");
            Check(Recruit.OwnerUidOf(daTheirs) == daOther && Recruit.BorrowerUidOf(daTheirs) == daMe && dh.Count(daOther) == 1, "a guard borrowed from someone else stays with its owner");
            var daRound = Newtonsoft.Json.JsonConvert.DeserializeObject<HiringOwner>(Newtonsoft.Json.JsonConvert.SerializeObject(dh.Owner(daMe)));
            Check(daRound.PendingDismiss.SequenceEqual(new long[] { 9105 }), "the far-away ones are saved with the world");
            var daFar = DaHired("test-uruk-recruit", 9105, daMe); var daStranger = DaHired("test-uruk-recruit", 9106, daMe);
            Check(dh.DismissIfPending(daFar) && Recruit.OwnerUidOf(daFar) == null && dh.Owner(daMe).PendingDismiss.Count == 0 && !dh.DismissIfPending(daFar) && !dh.DismissIfPending(daStranger) && Recruit.OwnerUidOf(daStranger) == daMe,
                "a marked recruit is freed when it next loads, once; others stay hired");
        }

        // ---- 0.0.76: promotion and XP, no default allies, shield choice, the owner's new weapons ----
        {
            var d16 = new FactionConfig(); d16.FillDefaults();
            Check(d16.Factions.All(kv => kv.Value.PromotePrices.All(p => FactionTiers.Of(kv.Key).Promote[p.Key] == p.Value)) && d16.Factions["gondor"].PromotePrices["warrior"] == 12 && d16.Factions["gondor"].PromotePrices["captain"] == 36
                && FactionConfig.InstructionText.Any(l => l.StartsWith("PromotePrices")) && FactionConfig.InstructionText.Any(l => l.Contains("No factions are allies by default")),
                "every faction promotes for its tier's price (0.0.93: Men 12 coins to warrior or archer and 36 to captain), and #Instructions explain it");
            var pp = Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(@"{ ""Version"": 16, ""Factions"": { ""dale"": { ""PromotePrices"": { ""Captain"": 0 } } } }"); pp.FillDefaults();
            Check(pp.Factions["dale"].PromotePrices["captain"] == 0 && pp.Factions["dale"].PromotePrices["warrior"] == 12, "a file's promotion price replaces the default, ranks it leaves out get the default");

            // XP rules.
            Check(Promotion.Need("recruit") == 15 && Promotion.Need("warrior") == 40 && Promotion.Need("captain") == 0 && Promotion.Need("archer") == 0 && Promotion.Need(null) == 0
                && Promotion.Need("guard") == 40 && Promotion.Need("shock") == 40 && Promotion.Targets("guard").SequenceEqual(new[] { "captain", "rider" }) && Promotion.Targets("shock").SequenceEqual(new[] { "captain", "rider" })
                && Promotion.Targets("recruit").SequenceEqual(new[] { "warrior", "guard", "shock", "archer" }) && Promotion.Targets("warrior").SequenceEqual(new[] { "captain", "rider" }) && Promotion.Targets("archer").Length == 0
                && Promotion.Need("rider") == 0 && Promotion.Targets("rider").Length == 0,
                "recruits need 15 XP to become warriors, guards, shock troops (0.0.113) or archers, those three 40 to become captains or (0.0.117) riders; archers, captains and riders go no further");
            Check(Promotion.CodeAtRank(new AssetLocation("lotrnpcs:test-goblin-recruit"), "archer").ToString() == "lotrnpcs:test-goblin-archer"
                && Promotion.CodeAtRank(new AssetLocation("lotrnpcs:test-blacknumenorean-warrior"), "captain").ToString() == "lotrnpcs:test-blacknumenorean-captain", "a promotion keeps the race and changes the rank");
            var ps = new PromotionSystem();
            var pmap = new Dictionary<long, Entity>();
            ps.Find = id => pmap.TryGetValue(id, out var e) ? e : null;
            TestAgent P(string code, long id, string owner = null) { var e = Npc(code); e.EntityId = id; e.Alive = true; if (owner != null) Recruit.Bind(e, owner, "N"); pmap[id] = e; return e; }
            var r1 = P("test-gondor-recruit", 9201, "uidXp"); var r2 = P("test-gondor-warrior", 9202, "uidXp");
            var arc = P("test-gondor-archer", 9203, "uidXp"); var cap = P("test-gondor-captain", 9204, "uidXp"); var wild = P("test-gondor-recruit", 9205);
            var foe1 = P("test-uruk-recruit", 9301); var xpDrifter = Mob("game:drifter-normal"); xpDrifter.EntityId = 9302; pmap[9302] = xpDrifter;
            ps.NowForTests = 1000;
            foreach (var a in new Entity[] { r1, r2, arc, cap, wild }) { ps.OnHit(foe1, a); ps.OnHit(xpDrifter, a); }
            ps.NowForTests = 10999;
            var got = ps.OnDeath(foe1); ps.OnDeath(xpDrifter);
            Check(got.Count == 2 && Promotion.Xp(r1) == 1 && Promotion.Xp(r2) == 1 && Promotion.Xp(arc) == 0 && Promotion.Xp(cap) == 0 && Promotion.Xp(wild) == 0,
                "a kill within 10 seconds of the hit gives each hired recruit and warrior that hit it 1 XP; archers, captains, unhired NPCs and drifters give or get none");
            var foe2 = P("test-uruk-recruit", 9303);
            ps.NowForTests = 20000; ps.OnHit(foe2, r1); ps.NowForTests = 30001; ps.OnDeath(foe2);
            Check(Promotion.Xp(r1) == 1, "...not when it dies more than 10 seconds after the hit");
            var foe3 = P("test-uruk-recruit", 9304);
            ps.NowForTests = 40000; ps.OnHit(foe3, r1); ps.NowForTests = 41000; ps.OnHit(foe3, r1); ps.NowForTests = 49000; ps.OnDeath(foe3);
            Check(Promotion.Xp(r1) == 2, "...and once per kill however often it hit");
            for (int i = 0; i < 20; i++) Promotion.AddXp(r1);
            Check(Promotion.Xp(r1) == 15 && !Promotion.AddXp(r1), "XP stops at the rank's requirement");
            Check(!Promotion.AddXp(r1, -20) && Promotion.Xp(r1) == 0 && Promotion.AddXp(r1, 99) && Promotion.Xp(r1) == 15 && !Promotion.AddXp(arc, 5) && Promotion.Xp(arc) == 0,
                "an admin's /recruits xp takes XP down to 0 or straight up to the requirement, and gives an archer none");
            Check(Promotion.MaySeeXp("uidA", "rangers,watch", "uidA", null) && Promotion.MaySeeXp("uidA", "rangers,watch", "uidB", new[] { "Watch" }) && !Promotion.MaySeeXp("uidA", "rangers", "uidB", new[] { "orcs" })
                && !Promotion.MaySeeXp("uidA", "", "uidB", new[] { "orcs" }),
                "the XP line shows to the owner and to players in one of the owner's /groups, nobody else");
            Check(XpInfoText.Shown(r1, "uidXp", null) == (15, 15) && XpInfoText.Shown(arc, "uidXp", null) == null && XpInfoText.Shown(r1, "uidStranger", new string[0]) == null
                && ((string)lang["recruit-xp"]).StartsWith("XP: ("), "the line reads XP: (15/15) for its owner; archers have none");
            Check(Promotion.CarriesOver(Recruit.OwnerUidAttr) && Promotion.CarriesOver("lotrnpcsOrder") && Promotion.CarriesOver(EntityBehaviorHeldWeapon.WeaponAttr) && Promotion.CarriesOver("nametag")
                && Promotion.CarriesOver("skinConfig") && Promotion.CarriesOver(Looks.NamedAttr) && !Promotion.CarriesOver(Promotion.XpAttr) && !Promotion.CarriesOver(Looks.KeptHelmetAttr) && !Promotion.CarriesOver("inventory") && !Promotion.CarriesOver("health"),
                "a promotion carries over the owner, orders, weapon, name and face, but not the XP, the old gear or the old health");
            Check(new[] { "promote-ready", "promote-done", "talk-opt-promote", "talk-opt-promote-free", "talk-promote-poor", "talk-promote-nocoin" }.All(k => lang[k] != null)
                && new[] { "whitelist-title", "whitelist-explain", "whitelist-listed", "whitelist-none", "whitelist-remove", "whitelist-more", "whitelist-add-label", "whitelist-add", "whitelist-online", "whitelist-close" }.All(k => lang[k] != null),
                "the promotion and whitelist menu lines are in the lang file");

            // Shields: one picked from several.
            Check(EntityBehaviorHeldWeapon.PickShield(null, new[] { "a", "b" }) == "a" && EntityBehaviorHeldWeapon.PickShield("b", new[] { "a", "b" }) == "b" && EntityBehaviorHeldWeapon.PickShield("z", new[] { "a" }) == "a"
                && EntityBehaviorHeldWeapon.PickShield("a", new string[0]) == null, "an NPC keeps the shield it picked while its type still lists it");
            var hw = new EntityBehaviorHeldWeapon(Npc("test-dwarf-warrior"));
            hw.Configure(new JsonObject(HeldWeapon(entityFiles["test-dwarf-warrior"], "server")));
            var hwOld = new EntityBehaviorHeldWeapon(Npc("test-mordororc-warrior"));
            hwOld.Configure(new JsonObject(JToken.Parse("{ shield: 'orcweapon-shield', weapons: [] }")));
            Check(hw.ShieldItems.SequenceEqual(new[] { "lotrnpcs:orcweapon-dwtowershield", "lotrnpcs:orcweapon-dwshield" }) && hwOld.ShieldItems.SequenceEqual(new[] { "lotrnpcs:orcweapon-shield" }),
                "the behavior reads a list of shields, and the old single shield still reads");

            // Weapons by faction.
            string[] Items(string code) => HeldWeapon(entityFiles[code], "server")["weapons"].Select(w => (string)w["item"]).ToArray();
            Check(Items("test-dwarf-captain").OrderBy(x => x).SequenceEqual(new[] { "lotrnpcs:orcweapon-dwaxe", "lotrnpcs:orcweapon-dwbattleaxe", "lotrnpcs:orcweapon-dwdagger", "lotrnpcs:orcweapon-dwspear", "lotrnpcs:orcweapon-dwwarhammersteel", "lotrnpcs:orcweapon-dwshorthammersteel" }.OrderBy(x => x))
                && new[] { "recruit", "warrior" }.All(k => Items("test-dwarf-" + k).Contains("lotrnpcs:orcweapon-dwwarhammer") && !Items("test-dwarf-" + k).Any(i => i.Contains("steel")))
                && Items("test-elf-recruit").Contains("lotrnpcs:orcweapon-elfspear")
                && Items("test-angmarorc-warrior").Take(2).SequenceEqual(new[] { "lotrnpcs:orcweapon-dagger", "lotrnpcs:orcweapon-axe" }) && Items("test-gundabadorc-captain").Take(2).SequenceEqual(new[] { "lotrnpcs:orcweapon-dagger", "lotrnpcs:orcweapon-axe" })
                && Items("test-mordororc-recruit").Length == 6 && Items("test-goblin-warrior").Length == 4,
                "dwarves carry the six dwarven weapons (0.0.95: the owner's warhammer and shorthammer replace the war pick stand-in; 0.0.112: captains the steel hammers, nobody else), elves the elven spear, Angmar and Gundabad the orc dagger and axe, Mordor its six, Misty Mountains its four");
            // 0.0.96 (owner, 2026-11-07: "remove the not only spears expansion dependancy, as it wont be implemented into
            // the server"): no NPC names a Spear Expansion item or animation; factions carry the owner's models and the game's
            // falx; the Haradrim still carry no falx (0.0.86); wicked dwarves like the dwarves.
            var gameStandIns = new HashSet<string> { "game:blade-falx-iron", "game:blade-falx-steel", "game:blade-blackguard-iron", "game:blade-forlorn-iron", "game:blade-sabre-ruined", "game:blade-gladius-ruined",
                "game:spear-generic-steel", "game:spear-generic-iron", "game:spear-generic-blackbronze", "game:spear-generic-flint", "game:spear-generic-ornatesilver", "game:spear-scrap-scrap",
                "game:club-generic-wood", "game:club-scrapmace-scrap", "game:club-flanged-ruined", "game:axe-bearded-ruined", "game:axe-battle-ruined", "game:axe-bardiche-ruined" };
            string[] armedRaces = { "gondor", "rohan", "dale", "arnor", "haradrim", "dunlending", "blacknumenorean", "tormentedelf", "uruk", "elf", "angmarorc", "gundabadorc", "dwarf", "wickeddwarf", "easterling", "mordororc", "goblin" };
            Check(entityFiles.Values.All(f => !f.ToString().Contains("spearexpantion") && !f.ToString().Contains("lotrnpcs-sx"))
                && armedRaces.All(r => new[] { "recruit", "warrior", "captain" }.All(k => Items("test-" + r + "-" + k).Length >= 1 && Items("test-" + r + "-" + k).All(i => i.StartsWith("lotrnpcs:") || gameStandIns.Contains(i))))
                && new[] { "recruit", "warrior", "captain" }.All(k => Items("test-haradrim-" + k).SequenceEqual(new[] { "lotrnpcs:orcweapon-hdspear", "lotrnpcs:orcweapon-hdsword", "lotrnpcs:orcweapon-hdcurvedsword" }))
                && new (string race, string[] items)[] {
                    ("rohan", new[] { "lotrnpcs:orcweapon-rhspear", "lotrnpcs:orcweapon-rhsword", "lotrnpcs:orcweapon-rhaxe" }), ("dale", new[] { "game:spear-generic-iron", "game:blade-falx-iron" }),
                    ("arnor", new[] { "game:spear-generic-steel", "game:blade-falx-steel" }),
                    ("dunlending", new[] { "game:club-generic-wood", "game:spear-generic-flint", "game:axe-bearded-ruined" }),
                    ("easterling", new[] { "game:spear-generic-blackbronze", "lotrnpcs:orcweapon-eastscimitar", "lotrnpcs:orcweapon-eastaxe" }),
                    ("uruk", new[] { "game:blade-falx-iron", "game:spear-generic-iron", "game:axe-battle-ruined" }),
                    ("tormentedelf", new[] { "game:blade-sabre-ruined", "game:blade-gladius-ruined", "game:spear-generic-ornatesilver" }),
                    ("angmarorc", new[] { "lotrnpcs:orcweapon-dagger", "lotrnpcs:orcweapon-axe", "game:spear-scrap-scrap", "game:club-scrapmace-scrap" }) }
                    .All(x => new[] { "recruit", "warrior", "captain" }.All(k => Items("test-" + x.race + "-" + k).SequenceEqual(x.items)))
                && new[] { "recruit", "warrior" }.All(k => Items("test-blacknumenorean-" + k).SequenceEqual(new[] { "game:blade-blackguard-iron", "game:club-flanged-ruined", "lotrnpcs:orcweapon-bnspear" }))
                && Items("test-blacknumenorean-captain").SequenceEqual(new[] { "lotrnpcs:orcweapon-bnsteelsword", "lotrnpcs:orcweapon-morgulblade", "lotrnpcs:orcweapon-bnspear" })
                && HeldWeapon(entityFiles["test-easterling-warrior"], "server")["weapons"].First(w => (string)w["item"] == "lotrnpcs:orcweapon-eastaxe")["twoHanded"]?.Value<bool>() == true
                // 0.0.113: the Rohirric axe and the Easterling scimitar are one-handed, so a shield goes with them.
                && HeldWeapon(entityFiles["test-rohan-warrior"], "server")["weapons"].First(w => (string)w["item"] == "lotrnpcs:orcweapon-rhaxe")["twoHanded"]?.Value<bool>() != true
                && HeldWeapon(entityFiles["test-easterling-warrior"], "server")["weapons"].First(w => (string)w["item"] == "lotrnpcs:orcweapon-eastscimitar")["twoHanded"]?.Value<bool>() != true
                && Items("test-wickeddwarf-warrior").OrderBy(x => x).SequenceEqual(Items("test-dwarf-warrior").OrderBy(x => x)),
                "no Spear Expansion items or animations left; every armed rank carries at least one weapon, all ours or the game's stand-ins; the Haradrim their spear, sword or curved sword (0.0.97; no falx); Men, Uruks, tormented elves and Angmar the game's stand-ins until their models come (0.0.101), the owner's models where they have come (0.0.112: Rohan's spear and sword, the Black Numenorean spear, captains the steel sword and Morgul blade, the Easterling axe two-handed, so no shield; 0.0.113: the Rohirric axe and Easterling scimitar one-handed); wicked dwarves like the dwarves");
            // 0.0.97 (owner's Haradrim sword, curved sword, light and heavy shields): the swords are one-handed and
            // slash; recruits carry the light shield, warriors and captains the heavy one, archers none.
            string[] ShieldsOf(string code) => HeldWeapon(entityFiles[code], "server")["shields"]?.Select(t => (string)t).ToArray() ?? new string[0];
            var hdList = HeldWeapon(entityFiles["test-haradrim-warrior"], "server")["weapons"];
            var hdAnims = entityFiles["test-haradrim-warrior"]["client"]["animations"].ToDictionary(a => (string)a["code"], a => a);
            Check(ShieldsOf("test-haradrim-recruit").SequenceEqual(new[] { "lotrnpcs:orcweapon-hdlightshield" })
                && ShieldsOf("test-haradrim-warrior").SequenceEqual(new[] { "lotrnpcs:orcweapon-hdheavyshield" }) && ShieldsOf("test-haradrim-captain").SequenceEqual(new[] { "lotrnpcs:orcweapon-hdheavyshield" })
                && ShieldsOf("test-haradrim-archer").Length == 0 && hdList.All(w => w["twoHanded"] == null)
                && (string)hdAnims["lotrnpcs-hdsword-attack"]["animation"] == "falx" && (string)hdAnims["lotrnpcs-hdcurvedsword-attack"]["animation"] == "falx"
                && new[] { "hdsword", "hdcurvedsword", "hdlightshield", "hdheavyshield" }.All(n => File.Exists(Path.Combine(argv[0], "..", "itemtypes", "orcweapon-" + n + ".json")) && File.Exists(Path.Combine(argv[0], "..", "textures", "item", "orcweapon", "atlas", n + ".png")))
                && lang["item-orcweapon-hdsword"]?.ToString() == "Haradrim sword" && lang["item-orcweapon-hdheavyshield"]?.ToString() == "Haradrim heavy shield",
                "Haradrim: the owner's sword and curved sword, one-handed slashes; the light shield for recruits, the heavy one for warriors and captains, none for archers");
            // 0.0.96 (owner: "the gundabad captain should not spawn in the bone armor like it does").
            Check(!entityFiles["test-gundabadorc-captain"].ToString().Contains("gundabadbonearmor") && entityFiles["test-gundabadorc-recruit"].ToString().Contains("gundabadbonearmor"),
                "Gundabad captains never wear the bone armor; recruits still do");
            // 0.0.96 (owner: "Let the gundabad orcs use the moria spear"): Gundabad's dagger, axe and the Moria spear, held
            // like the Misty Mountains orcs hold it.
            var gbAnims = entityFiles["test-gundabadorc-warrior"]["client"]["animations"].ToDictionary(a => (string)a["code"], a => a);
            Check(new[] { "recruit", "warrior", "captain" }.All(k => Items("test-gundabadorc-" + k).SequenceEqual(new[] { "lotrnpcs:orcweapon-dagger", "lotrnpcs:orcweapon-axe", "lotrnpcs:orcweapon-mmspear", "game:club-scrapmace-scrap" }))
                && (string)gbAnims["lotrnpcs-mmspear-attack"]["animation"] == "spearhit" && (string)gbAnims["lotrnpcs-mmspear-hold"]["animation"] == "spearidle"
                && (string)HeldWeapon(entityFiles["test-gundabadorc-captain"], "server")["weapons"][2]["holdAnimation"] == "lotrnpcs-mmspear-hold",
                "Gundabad orcs carry the orc dagger, the orc axe, the Moria spear (thrust and held as the Misty Mountains orcs do) or the game's scrap mace (0.0.101)");
            // 0.0.82 (owner's loadout list, "prefer spears" and the like): weights stay where there is more than one model.
            int WeightOf(string code, string item) => HeldWeapon(entityFiles[code], "server")["weapons"].Where(w => (string)w["item"] == item).Select(w => (int?)w["weight"] ?? 1).DefaultIfEmpty(0).First();
            Check(WeightOf("test-dwarf-recruit", "lotrnpcs:orcweapon-dwaxe") == 3 && WeightOf("test-dwarf-recruit", "lotrnpcs:orcweapon-dwwarhammer") == 3 && WeightOf("test-dwarf-recruit", "lotrnpcs:orcweapon-dwshorthammer") == 3 && WeightOf("test-dwarf-captain", "lotrnpcs:orcweapon-dwwarhammersteel") == 3 && WeightOf("test-dwarf-captain", "lotrnpcs:orcweapon-dwshorthammersteel") == 3 && WeightOf("test-dwarf-captain", "lotrnpcs:orcweapon-dwaxe") == 1
                && WeightOf("test-wickeddwarf-captain", "lotrnpcs:orcweapon-dwwarhammersteel") == 3
                && Items("test-gondor-captain").SequenceEqual(new[] { "lotrnpcs:orcweapon-gdcaptsword", "lotrnpcs:orcweapon-gdsteelsword" })
                && Items("test-gondor-warrior").SequenceEqual(new[] { "game:spear-generic-steel", "lotrnpcs:orcweapon-gdsword" }) && WeightOf("test-gondor-recruit", "game:spear-generic-steel") == 3 && WeightOf("test-arnor-captain", "game:spear-generic-steel") == 3 && Items("test-elf-warrior").SequenceEqual(new[] { "lotrnpcs:orcweapon-elfspear", "lotrnpcs:orcweapon-elflongsword" }),
                "dwarven axes and hammers come up three times as often as the others; Gondor captains carry the captain's sword or (0.0.112) the steel sword, Gondor recruits and warriors the game's steel spear (three times as often, as for Arnor) or the owner's iron sword (0.0.112), elves the elven spear or (0.0.112) the longsword");
            var wrand = new Random(11); int spears = 0;
            var wlist = new[] { new NpcWeapon { Item = "a:spear", Weight = 3 }, new NpcWeapon { Item = "a:sword", Weight = 1 } };
            for (int i = 0; i < 4000; i++) if (EntityBehaviorHeldWeapon.PickWeapon(null, wlist, wrand) == "a:spear") spears++;
            Check(spears > 2850 && spears < 3150, "a weight 3 weapon is picked about three times in four (" + spears + " of 4000)");
            var dwList = HeldWeapon(entityFiles["test-dwarf-warrior"], "server")["weapons"];
            Check(dwList.Single(w => (string)w["item"] == "lotrnpcs:orcweapon-dwbattleaxe")["twoHanded"]?.Value<bool>() == true && dwList.Single(w => (string)w["item"] == "lotrnpcs:orcweapon-dwwarhammer")["twoHanded"]?.Value<bool>() == true
                && dwList.Count(w => w["twoHanded"] != null) == 2 && dwList.Single(w => (string)w["item"] == "lotrnpcs:orcweapon-dwshorthammer")["twoHanded"] == null
                && (string)HeldWeapon(entityFiles["test-mordororc-warrior"], "server")["weapons"].Single(w => (string)w["item"] == "lotrnpcs:orcweapon-poleaxe")["holdAnimation"] == "lotrnpcs-poleaxe-hold",
                "the battleaxe and (0.0.95) the warhammer are the dwarves' two-handed weapons, never carried with a shield; the shorthammer goes with one");
            var animsOf = entityFiles["test-dwarf-warrior"]["client"]["animations"].ToDictionary(a => (string)a["code"], a => a);
            Check((string)animsOf["lotrnpcs-poleaxe-hold"]["animation"] == "poleaxehold" && (string)animsOf["lotrnpcs-dwbattleaxe-hold"]["animation"] == "poleaxehold"
                && (string)animsOf["lotrnpcs-dwbattleaxe-attack"]["animation"] == "poleaxechop" && (string)animsOf["lotrnpcs-falx-attack"]["animation"] == "falx"
                && (string)animsOf["lotrnpcs-dwspear-hold"]["animation"] == "spearidle" && (string)animsOf["lotrnpcs-elfspear-attack"]["animation"] == "spearhit",
                "the poleaxe and battleaxe hold poleaxehold between chops; spears thrust and hold spearidle; the falx slashes");
            Check(new[] { "seraph-faceless", "dwarf-faceless", "elf-faceless" }.All(b => {
                    var shape = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "entity", "humanoid", b + ".json")));
                    var codes = shape["animations"].Select(a => (string)a["code"]).ToList();
                    return codes.Contains("poleaxehold") && codes.Contains("poleaxechop") && codes.Contains("spearidle") && codes.Contains("greatswordhold"); }),
                "the human, dwarf and elf bodies all have the poleaxe's hold and chop (0.0.76) and the greatsword guard (0.0.96)");
            // 0.0.96 (owner: "Fix the way the poleaxes are held"; chose the shoulder carry): poleaxehold moves only the
            // right arm and the item, and the poleaxe, battleaxe and warhammer hold only those, so the left arm walks.
            var carry = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "entity", "humanoid", "dwarf-faceless.json")))["animations"].Single(a => (string)a["code"] == "poleaxehold");
            var carryEls = carry["keyframes"].SelectMany(k => ((JObject)k["elements"]).Properties().Select(p => p.Name)).Distinct().OrderBy(n => n).ToArray();
            Check(carryEls.SequenceEqual(new[] { "ItemAnchor", "LowerArmR", "UpperArmR" })
                && new[] { "poleaxe", "dwbattleaxe", "dwwarhammer" }.All(w => ((JObject)animsOf["lotrnpcs-" + w + "-hold"]["elementWeight"]).Properties().Select(p => p.Name).OrderBy(n => n).SequenceEqual(new[] { "ItemAnchor", "LowerArmR", "UpperArmR" })),
                "the poleaxe, battleaxe and warhammer are carried on the right shoulder: the hold moves only the right arm and the weapon, so the left arm swings freely");
            // 0.0.77: the server builds an NPC's animations from its type's base shape (the owner's 0.0.76 server log:
            // "attempted to play an animation code which its shape does not have: poleaxehold"), so every NPC type uses
            // our own body, and every animation its type names must be in that body.
            var ourBody = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes", "entity", "humanoid", "seraph-faceless.json")));
            var ourBodyAnims = new HashSet<string>(ourBody["animations"].Select(a => (string)a["code"]));
            var npcFiles = entityFiles.Where(kv => kv.Key.StartsWith("test-")).ToList();
            var missing = npcFiles.SelectMany(kv => (kv.Value["client"]?["animations"] ?? new JArray()).Select(a => (string)a["animation"]).Where(a => a != null && !ourBodyAnims.Contains(a)).Select(a => kv.Key + ":" + a)).Distinct().ToList();
            Check(npcFiles.Count > 70 && npcFiles.All(kv => (string)kv.Value["client"]["shape"]["base"] == "lotrnpcs:entity/humanoid/seraph-faceless") && missing.Count == 0,
                "every NPC type's base shape is our body, which has every animation the type plays (0.0.77)" + (missing.Count > 0 ? ": missing " + string.Join(", ", missing.Take(5)) : ""));
        // 0.0.80 (owner: "when the helmet is on, hide the hair options underneath").
        {
            var harModel = JObject.Parse(File.ReadAllText(Path.Combine(assets, "config", "customplayermodels", "haradrim.json")))["haradrim"] as JObject;
            Check(EntityBehaviorHelmetHidesHair.HairShapePath(harModel, "hairbase", "short1") == "game:entity/humanoid/seraphskinparts/hair-base/short1"
                && EntityBehaviorHelmetHidesHair.HairShapePath(harModel, "hairextra", "braids") == "game:entity/humanoid/seraphskinparts/hair-extra/braids"
                && EntityBehaviorHelmetHidesHair.HairShapePath(harModel, "hairbase", "none") == null && EntityBehaviorHelmetHidesHair.HairShapePath(harModel, "nosuchpart", "x") == null,
                "a helmet hides the NPC's own hair shapes, found from its race model's hair templates (0.0.80)");
            Check(EntityBehaviorHelmetHidesHair.RemovableNames(new[] { "hairfront", "Head", "hairback", "hairfront", "" }, new HashSet<string> { "Head", "Neck" }).SequenceEqual(new[] { "hairfront", "hairback" })
                && EntityBehaviorHelmetHidesHair.IsHelmetCategory("armorhead") && !EntityBehaviorHelmetHidesHair.IsHelmetCategory("head") && !EntityBehaviorHelmetHidesHair.IsHelmetCategory(null),
                "...never a part of the body itself, and only for head armor, not hats");
            var armoredClient = entityFiles.Where(kv => GearSets(kv.Value) != null).Select(kv => kv.Value["client"]["behaviors"].Select(b => (string)b["code"]).ToList()).ToList();
            Check(armoredClient.Count > 60 && armoredClient.All(l => l.IndexOf("lotrnpcs:helmethidehair") == l.IndexOf("seraphinventory") + 1),
                "every NPC with gear hides its hair under a helmet, right after its gear is drawn");
            // 0.0.81 (owner: "beards cannot be worn with these helmets either. but when the helmet is toggled off, the hair
            // and beard should come back as was").
            var harPattern = new[] { new AssetLocation("haradrimarmor:armor-head-*") };
            var harHelmet = new AssetLocation("haradrimarmor:armor-head-tierthreechain-haradriminfantryhelmet-haradriminfantryhelmet-none");
            Check(EntityBehaviorHelmetHidesHair.PartsUnder(null, harPattern) == null
                && EntityBehaviorHelmetHidesHair.PartsUnder(new AssetLocation("game:armor-head-plate-iron"), harPattern).SequenceEqual(new[] { "hairbase", "hairextra" })
                && EntityBehaviorHelmetHidesHair.PartsUnder(harHelmet, harPattern).SequenceEqual(new[] { "hairbase", "hairextra", "beard", "mustache" })
                && EntityBehaviorHelmetHidesHair.PartsUnder(harHelmet, Array.Empty<AssetLocation>()).SequenceEqual(new[] { "hairbase", "hairextra" })
                && EntityBehaviorHelmetHidesHair.HairShapePath(harModel, "beard", "medium") == "game:entity/humanoid/seraphskinparts/hair-face/medium"
                && EntityBehaviorHelmetHidesHair.HairShapePath(harModel, "mustache", "none") == null,
                "a Haradrim helmet also hides the beard and mustache; other helmets only the hair, and no helmet nothing (0.0.81)");
            // Every helmet a type can wear: a Haradrim one hides the beard, no other faction's does.
            List<AssetLocation> Patterns(JToken npc) => npc["client"]["behaviors"].FirstOrDefault(b => (string)b["code"] == "lotrnpcs:helmethidehair")?["hideBeardUnder"]?.Select(t => new AssetLocation((string)t)).ToList() ?? new List<AssetLocation>();
            IEnumerable<string> Helmets(JToken npc) => npc.SelectTokens("$..*").OfType<JValue>().Select(v => v.Value as string).Where(v => v != null && v.Contains(":armor-head-")).Distinct();
            var harTypes = entityFiles.Where(kv => kv.Key.Contains("haradrim") && GearSets(kv.Value) != null).ToList();
            var badHar = harTypes.SelectMany(kv => Helmets(kv.Value).Where(h => h.StartsWith("haradrimarmor:") && !EntityBehaviorHelmetHidesHair.HidesBeard(new AssetLocation(h), Patterns(kv.Value))).Select(h => kv.Key + ":" + h)).ToList();
            var badOther = entityFiles.Where(kv => GearSets(kv.Value) != null).SelectMany(kv => Helmets(kv.Value).Where(h => !h.StartsWith("haradrimarmor:") && EntityBehaviorHelmetHidesHair.HidesBeard(new AssetLocation(h), Patterns(kv.Value))).Select(h => kv.Key + ":" + h)).ToList();
            Check(harTypes.Count >= 4 && harTypes.All(kv => Helmets(kv.Value).Any(h => h.StartsWith("haradrimarmor:"))) && badHar.Count == 0 && badOther.Count == 0,
                "every Haradrim helmet hides the beard on the types that wear it, and no other helmet does" + (badHar.Concat(badOther).Any() ? ": " + string.Join(", ", badHar.Concat(badOther).Take(4)) : ""));
            // 0.0.96 (owner: "with the new gundabad helmet, the ears stick out from the sides"): the owner's Gundabad
            // Helmet also hides the ears; ear variants name their own shape.
            var gbModel = JObject.Parse(File.ReadAllText(Path.Combine(assets, "config", "customplayermodels", "gundabadorc.json")))["gundabadorc"] as JObject;
            var gbHelmet = new AssetLocation("gundabad:armor-head-tierfourplate-gundabadhelmetone-gundabadhelmetone-none");
            List<AssetLocation> EarPatterns(JToken npc) => npc["client"]["behaviors"].FirstOrDefault(b => (string)b["code"] == "lotrnpcs:helmethidehair")?["hideEarsUnder"]?.Select(t => new AssetLocation((string)t)).ToList() ?? new List<AssetLocation>();
            var gbCaptain = entityFiles["test-gundabadorc-captain"];
            var earOther = entityFiles.Where(kv => GearSets(kv.Value) != null).SelectMany(kv => Helmets(kv.Value).Where(h => h != gbHelmet.ToString() && EntityBehaviorHelmetHidesHair.HidesBeard(new AssetLocation(h), EarPatterns(kv.Value))).Select(h => kv.Key + ":" + h)).ToList();
            Check(EntityBehaviorHelmetHidesHair.HairShapePath(gbModel, "ears", "plumpears") == "lotrnpcs:entity/humanoid/ears/orc_earsplump"
                && EntityBehaviorHelmetHidesHair.HairShapePath(gbModel, "hairbase", "messy") == "game:entity/humanoid/seraphskinparts/hair-base/messy"
                && EntityBehaviorHelmetHidesHair.PartsUnder(gbHelmet, Patterns(gbCaptain), EarPatterns(gbCaptain)).SequenceEqual(new[] { "hairbase", "hairextra", "ears" })
                && EntityBehaviorHelmetHidesHair.PartsUnder(new AssetLocation("norc:armor-head-tierfiveplate-norchelmetheavier-norchelmetheavier-none"), Patterns(gbCaptain), EarPatterns(gbCaptain)).SequenceEqual(new[] { "hairbase", "hairextra" })
                && Helmets(gbCaptain).Contains(gbHelmet.ToString()) && earOther.Count == 0,
                "the owner's Gundabad Helmet hides the hair and the ears; no other helmet hides the ears" + (earOther.Any() ? ": " + string.Join(", ", earOther.Take(4)) : ""));
        }
        }
        // ---- 0.0.82: battle lines, the helmet rule, config version 17 ----
        {
            // Line geometry.
            Check(LineMath.FrontCount(7, 3, 0) == 7 && LineMath.FrontCount(1, 4, 0) == 1, "0.0.85: with no attacker limit every member stands in the front rank");
            Check(LineMath.FrontCount(5, 3) == 3 && LineMath.FrontCount(7, 3) == 3 && LineMath.FrontCount(3, 5) == 3 && LineMath.FrontCount(4, 0) == 1 && LineMath.FrontCount(0, 2) == 0 && LineMath.FrontCount(7, 3, 2) == 6,
                "0.0.104: the front rank holds one per enemy; the members past that are the extras");
            LineMath.SlotOffset(0, 5, 3, 1.5, out double r0, out double b0); LineMath.SlotOffset(2, 5, 3, 1.5, out double r2, out double b2);
            LineMath.SlotOffset(3, 5, 3, 1.5, out double r3, out double b3); LineMath.SlotOffset(4, 5, 3, 1.5, out double r4, out double b4);
            LineMath.SlotOffset(5, 7, 3, 1.5, out double r5, out _); LineMath.SlotOffset(6, 7, 3, 1.5, out double r6, out _);
            LineMath.SlotOffset(3, 7, 3, 1.5, out double r3b, out _);
            Check(r0 == -1.5 && r2 == 1.5 && b0 == 0 && b2 == 0 && r3 == -3 && r4 == 3 && b3 == 0 && b4 == 0 && r3b == -4.5 && r5 == 3 && r6 == 4.5,
                "0.0.110: front slots are 1.5 apart around the center; the extras extend the same rank, the left half past its left end, the rest past its right");
            // 0.0.112: 7 members, 3 in front, 2 captains in reserve: the 2 extras flank the front, the captains stand one gap behind the middle.
            LineMath.SlotOffset(3, 7, 3, 1.5, out double q3, out double qb3, 2); LineMath.SlotOffset(4, 7, 3, 1.5, out double q4, out _, 2);
            LineMath.SlotOffset(5, 7, 3, 1.5, out double q5, out double qb5, 2); LineMath.SlotOffset(6, 7, 3, 1.5, out double q6, out double qb6, 2);
            Check(q3 == -3 && q4 == 3 && qb3 == 0 && q5 == -0.75 && q6 == 0.75 && qb5 == 1.5 && qb6 == 1.5,
                "0.0.112: the reserve stands side by side one gap behind the middle; the rest of the line is laid out as if it were not there");
            LineMath.SlotAt(10, 10, 1, 0, 1.5, 0, out double sax, out double saz); LineMath.SlotAt(10, 10, 1, 0, 0, 1.5, out double sbx, out double sbz);
            Check(Math.Abs(sax - 10) < 1e-9 && Math.Abs(saz - 8.5) < 1e-9 && Math.Abs(sbx - 8.5) < 1e-9 && Math.Abs(sbz - 10) < 1e-9,
                "facing +x, right is -z and behind is -x");
            // Five NPCs in a loose huddle facing +x (right is -z): the middle three across the line hold the front, the
            // leftmost and rightmost take the flanks.
            var hx = new List<double> { 0.0, 1.0, 0.5, -1.0, 0.8 }; var hz = new List<double> { 0.0, 1.0, -1.0, 0.5, 0.2 };
            int[] arr = LineMath.Arrange(hx, hz, 0, 0, 1, 0, 3);
            Check(arr.SequenceEqual(new[] { 3, 4, 0, 1, 2 }),
                "0.0.110: the middle members across the line take the front left to right, then the left flank, then the right, so nobody crosses (" + string.Join(",", arr) + ")");
            Check(LineMath.Advance(10, 3, 0.6, 0.5, 1.5) == 0.6 && LineMath.Advance(3.4, 3, 0.6, 0.5, 1.5) - 0.4 < 1e-9 && LineMath.Advance(2, 3, 0.6, 0.5, 1.5) == 0 && LineMath.Advance(10, 3, 0.6, 2, 1.5) == 0,
                "a line marches 0.6 blocks an update, stops 3 blocks from the enemy, and waits while its members lag behind");
            double bx = 1, bz = 0; LineMath.Blend(ref bx, ref bz, 0, 1, 0.2);
            Check(Math.Abs(bx * bx + bz * bz - 1) < 1e-9 && bz > 0 && bz < 0.3, "a line turns toward the enemy a fifth of the way an update");

            // 0.0.83 (owner's 0.0.82 test: aggressive recruits "not fighting nearby hostile factions until they are hit
            // first"): the lines meet as soon as one front member is in reach, and waiting members aren't dropped.
            Check(LineMath.FrontEngaged(new List<double> { 9, 3.2, double.NaN, 1 }, 3, 3.5) && !LineMath.FrontEngaged(new List<double> { 9, 4, double.NaN, 1 }, 3, 3.5)
                && LineMath.FrontEngaged(new List<double> { 9, 5.5 }, 2, 6),
                "one front member in reach of its enemy means the lines have met (rear members don't count; holding lines use their hold range)");
            Check(LineMath.StillMember(true, 20000, 0, 8000) && LineMath.StillMember(false, 7000, 0, 8000) && !LineMath.StillMember(false, 9000, 0, 8000),
                "a member waiting in its slot stays in the line for 8 seconds after it last picked a target, even between the seek task's searches");

            // Joining and slots (wild lines on for these checks; 0.0.122 turns them off by default).
            BattleLineSystem.WildLines = true;
            var lsys = new BattleLineSystem(fs);
            var o1 = Wild("test-mordororc-warrior"); o1.Pos.SetPos(0, 0, 0);
            var o2 = Wild("test-mordororc-recruit"); o2.Pos.SetPos(0, 0, 2);
            var oArcher = Wild("test-mordororc-archer"); oArcher.Pos.SetPos(0, 0, 1);
            var g1 = Wild("test-gondor-warrior"); g1.Pos.SetPos(12, 0, 0);
            var line1 = lsys.Join(o1, g1, 0);
            var line2 = lsys.Join(o2, g1, 0);
            Check(line1 != null && line1 == line2 && line1.Members.Count == 2 && lsys.Join(oArcher, g1, 0) == null && lsys.LineCount == 1,
                "two wild Mordor orcs going for the same Gondor NPC share one line; archers never join");
            Check(lsys.Join(o1, Player("lrc:freepeople"), 0) == null && lsys.LineOf(o1.EntityId) == null && lsys.Join(o1, g1, 0) != null,
                "an NPC that turns on a player leaves its line, and joins again when it turns back to an NPC");
            line1.Front = 2;
            Check(lsys.HoldSlot(o1, g1, out double hsx, out double hsz) && lsys.HoldSlot(o2, g1, out double hsx2, out double hsz2) && Math.Abs(Math.Sqrt((hsx - hsx2) * (hsx - hsx2) + (hsz - hsz2) * (hsz - hsz2)) - 1.5) < 1e-6,
                "while the enemy is far, line members stand in slots 1.5 apart");
            g1.Pos.SetPos(3, 0, 0);
            Check(!lsys.HoldSlot(o1, g1, out _, out _), "a front member fights once an enemy is within 3.5 blocks");
            line1.Front = 1; g1.Pos.SetPos(12, 0, 0);
            int rearIndex = line1.Members.IndexOf(o2.EntityId) == 1 ? 1 : 0;
            var rear = rearIndex == 1 ? o2 : o1;
            g1.Pos.SetPos(rear.Pos.X + 2.5, 0, rear.Pos.Z);
            bool waits = lsys.HoldSlot(rear, g1, out _, out _);
            g1.Pos.SetPos(rear.Pos.X + 1.5, 0, rear.Pos.Z);
            Check(waits && !lsys.HoldSlot(rear, g1, out _, out _), "a rear member waits in its slot until an enemy is right on it (within 2 blocks)");
            lsys.Forget(o1.EntityId); lsys.Forget(o2.EntityId);
            Check(lsys.LineCount == 0 && lsys.LineOf(o2.EntityId) == null, "a line with no members left is gone");
            var gGondor = Wild("test-gondor-recruit"); gGondor.Pos.SetPos(0, 0, 30);
            Check(lsys.Join(o1, gGondor, 0) != lsys.Join(Wild("test-gondor-warrior").Also(e => e.Pos.SetPos(0, 0, 40)), o1, 0), "each side has its own line");
            {
                var ls = new BattleLineSystem(fs);
                var near = Wild("test-mordororc-warrior"); near.Pos.SetPos(0, 0, 0);
                var straggler = Wild("test-mordororc-warrior"); straggler.Pos.SetPos(-17, 0, 0);
                var farOrc = Wild("test-mordororc-warrior"); farOrc.Pos.SetPos(-17, 0, 60);
                var foe = Wild("test-gondor-warrior"); foe.Pos.SetPos(8, 0, 0);
                var foe2 = Wild("test-gondor-warrior"); foe2.Pos.SetPos(8, 0, 60);
                var fought = ls.Join(near, foe, 0);
                Check(ls.Join(straggler, foe, 0) == fought && ls.Join(farOrc, foe2, 0) != fought && ls.LineCount == 2,
                    "0.0.114: a straggler beyond the join radius joins its side's line fighting near its enemy; one fighting another enemy far away starts its own");
            }

            // Recruits: lines only for followers in a formation other than Loose.
            var rec = Wild("test-gondor-warrior");
            rec.WatchedAttributes.SetString(Recruit.OwnerUidAttr, "uidL");
            Recruit.SetOrder(rec, Recruit.Follow);
            rec.WatchedAttributes.SetString(Recruit.FormationAttr, Formation.Line);
            string sideLine = BattleLineSystem.SideOf(rec);
            rec.WatchedAttributes.SetString(Recruit.FormationAttr, Formation.Loose);
            string sideLoose = BattleLineSystem.SideOf(rec);
            rec.WatchedAttributes.SetString(Recruit.FormationAttr, Formation.Wedge);
            Recruit.SetOrder(rec, Recruit.Guard);
            string sideGuard = BattleLineSystem.SideOf(rec);
            Recruit.SetOrder(rec, Recruit.Follow);
            Recruit.SetStance(rec, Recruit.Defensive);
            bool defHolds = !BattleLineSystem.AdvancesOf(rec);
            Recruit.SetStance(rec, Recruit.Raid);
            Check(sideLine == "o:uidL" && sideLoose == null && sideGuard == null && defHolds && BattleLineSystem.AdvancesOf(rec) && BattleLineSystem.AdvancesOf(o1)
                && BattleLineSystem.SideOf(o1) == "f:mordor" && BattleLineSystem.SideOf(oArcher) == null,
                "followers in a formation line up by their leader, Loose followers and guards don't; defensive recruits hold, raid recruits and wild NPCs march");
            SetLegacy(fs, new FactionConfig { BattleLines = false });
            Check(!fs.Lines.Enabled && fs.Lines.Join(o1, g1, 0) == null, "BattleLines false turns lines off");
            SetLegacy(fs, new FactionConfig { BattleLines = true });
            Check(fs.Lines.Enabled && fs.Lines.Gap == 1.5 && fs.Lines.HoldRange == 6 && fs.Spacing.DuelMin == 1.0 && fs.Spacing.DuelMax == 1.6 && fs.Spacing.MaxAttackers == 4 && fs.Lines.PerEnemy == 4,
                "the faction system passes the line gap, hold range, duel band and the attacker limit (4 by default since 0.0.102) on");
            Check(fs.SameSide(o1, Wild("test-mordororc-captain")) && !fs.SameSide(o1, g1) && !fs.SameSide(o1, rec) && FactionConfig.InstructionText.Any(l => l.StartsWith("BattleLines") && l.Contains("LineHoldRange"))
                && FactionConfig.InstructionText.Any(l => l.StartsWith("DuelDistanceMin")),
                "same side = one faction (or allies) or one leader's recruits; #Instructions explain lines and the duel band");

            // Helmets at spawn: the owner's list says who may go bare-headed.
            var hr = new Random(4); int bare = 0;
            for (int i = 0; i < 500; i++) if (!Looks.WearsHelmet("recruit", 0.6, false, hr, false)) bare++;
            Check(bare == 0, "a unit that may not go bare-headed always spawns with its helmet");
            bool HelmetOptional(string code) => types[code].Attributes["lotrnpcsHelmetOptional"].AsBool(true);
            Check(!HelmetOptional("test-gondor-recruit") && !HelmetOptional("test-gondor-captain") && !HelmetOptional("test-elf-warrior") && !HelmetOptional("test-dwarf-captain") && !HelmetOptional("test-dunlending-captain")
                && !HelmetOptional("test-arnor-recruit") && !HelmetOptional("test-dale-warrior") && !HelmetOptional("test-rohan-recruit")
                && HelmetOptional("test-mordororc-captain") && HelmetOptional("test-uruk-captain") && HelmetOptional("test-dwarf-warrior") && HelmetOptional("test-rohan-captain")
                && HelmetOptional("test-arnor-warrior") && HelmetOptional("test-dale-captain") && HelmetOptional("test-dunlending-warrior"),
                "orcs and Uruk-hai may spawn bare-headed, and so may the units the list marks; the others always wear theirs");
            Check(types.Where(kv => kv.Value.Attributes?["lotrnpcsHelmets"].Exists == true).All(kv => kv.Value.Attributes["lotrnpcsHelmets"].AsArray<string>().Length > 0)
                && FactionConfig.InstructionText.Any(l => l.StartsWith("HelmetChance") && l.Contains("bare-headed")),
                "every unit with helmets still has them to choose from in the looks menu (No helmet stays for all)");
        }

        // ---- 0.0.106 (owner: captains "in the front middle, then warriors, then recruits", archers "at the back or ends") ----
        {
            // A line of 7 facing +Y, members standing in a jumble: captain, 2 warriors, 2 recruits, 2 archers.
            var lineOff = Formation.Offsets(Formation.Line, 7);
            var lineSpots = lineOff.Select(o => new Vec3d(o.X, 0, o.Y)).ToList();
            var jumble = new List<Vec3d> { new Vec3d(5, 0, 3), new Vec3d(-4, 0, 1), new Vec3d(4, 0, -2), new Vec3d(0, 0, 0), new Vec3d(1, 0, 1), new Vec3d(-1, 0, 2), new Vec3d(-5, 0, 0) };
            string[] rk = { "captain", "warrior", "warrior", "recruit", "recruit", "archer", "archer" };
            int[] at = Formation.AssignByRank(jumble, lineSpots, lineOff, rk.Select(Formation.OrderOfRank).ToList());
            double Ax(int i) => Math.Abs(lineOff[at[i]].X);
            Check(at.Distinct().Count() == 7 && Ax(0) < 0.01 && Ax(1) < 2 && Ax(2) < 2 && Ax(3) > 3 && Ax(3) < 4 && Ax(4) > 3 && Ax(4) < 4 && Ax(5) > 5 && Ax(6) > 5,
                "in a line the captain stands in the middle, warriors beside him, recruits further out and archers at the ends");
            var sqOff = Formation.Offsets(Formation.Square, 9);
            var sqSpots = sqOff.Select(o => new Vec3d(o.X, 0, o.Y)).ToList();
            var rnd9 = new Random(3); var mem9 = Enumerable.Range(0, 9).Select(_ => new Vec3d(rnd9.NextDouble() * 6 - 3, 0, rnd9.NextDouble() * 6 - 3)).ToList();
            string[] rk9 = { "archer", "recruit", "captain", "recruit", "warrior", "archer", "recruit", "warrior", "archer" };
            int[] at9 = Formation.AssignByRank(mem9, sqSpots, sqOff, rk9.Select(Formation.OrderOfRank).ToList());
            double frontY = sqOff.Max(o => o.Y), backY = sqOff.Min(o => o.Y);
            Check(Math.Abs(sqOff[at9[2]].Y - frontY) < 0.01 && Math.Abs(sqOff[at9[2]].X) < 0.01 && Math.Abs(sqOff[at9[4]].Y - frontY) < 0.01 && Math.Abs(sqOff[at9[7]].Y - frontY) < 0.01
                && new[] { 0, 5, 8 }.All(i => Math.Abs(sqOff[at9[i]].Y - backY) < 0.01),
                "in a square the captain takes the front middle, warriors the rest of the front row, recruits the middle row and archers the back row");
        }

        // ---- 0.0.105 (owner: archers "end up jumping off walls in order to enter melee combat") ----
        {
            EntityBehaviorEdgeGuard.Cell Wall(int x, int y, int z) => y <= 0 ? EntityBehaviorEdgeGuard.Cell.Solid : (x == 0 && y <= 3 ? EntityBehaviorEdgeGuard.Cell.Solid : EntityBehaviorEdgeGuard.Cell.Air);
            // Standing on a wall 3 high at x = 0 (feet at y 4); stepping out to x = 1 drops 3 blocks.
            Check(!EntityBehaviorEdgeGuard.Unsafe(Wall, 1, 4, 0, EntityBehaviorEdgeGuard.MaxSafeDrop) && EntityBehaviorEdgeGuard.Unsafe(Wall, 1, 4, 0, EntityBehaviorEdgeGuard.ArcherMaxDrop)
                && !EntityBehaviorEdgeGuard.Unsafe(Wall, 1, 1, 0, EntityBehaviorEdgeGuard.ArcherMaxDrop),
                "a fighting archer won't step off a 3-block wall (others still may), but takes a one-block step down");
            Check(AiTaskFactionSeek.HoldsHeight(3) && !AiTaskFactionSeek.HoldsHeight(1.5) && !AiTaskFactionSeek.HoldsHeight(-4),
                "an archer more than 2 blocks above its target holds its height instead of going down to it");
        }

        // ---- 0.0.104: walking-through fix (owner chose "Push always + 5-block look-ahead"), holding lines (chose "Yes, and
        // turn lines on by default"), captain limit (chose "1 captain per 8 roster places") ----
        {
            double ax = 0, az = 0, br = 0;
            bool inWay = BattleSpacing.AddAvoid(ref ax, ref az, ref br, 1, 0, 2, 0.3, 7);
            Check(inWay && Math.Abs(ax) < 1e-9 && az < 0 && br == 0, "a walking NPC steers right (-z, heading +x) around a friend 2 blocks ahead and a little to its left, without braking");
            double cx = 0, cz = 0, cb = 0;
            BattleSpacing.AddAvoid(ref cx, ref cz, ref cb, 1, 0, 1, 0.1, 7);
            Check(cb > 0.3 && Math.Abs(cz) > Math.Abs(az), "...and brakes, steering harder, for one right in front within 1.6 blocks");
            double nx0 = 0, nz0 = 0, nb = 0;
            Check(!BattleSpacing.AddAvoid(ref nx0, ref nz0, ref nb, 1, 0, -1, 0, 7) && !BattleSpacing.AddAvoid(ref nx0, ref nz0, ref nb, 1, 0, 6, 0, 7) && !BattleSpacing.AddAvoid(ref nx0, ref nz0, ref nb, 1, 0, 2, 1.5, 7)
                && nx0 == 0 && nz0 == 0 && nb == 0 && BattleSpacing.LookAhead == 5,
                "nothing for a friend behind, more than 5 blocks ahead, or well off its way");
            double t1x = 0, t1z = 0, t1b = 0, t2x = 0, t2z = 0, t2b = 0;
            BattleSpacing.AddAvoid(ref t1x, ref t1z, ref t1b, 0, 1, 0, 2, 2); BattleSpacing.AddAvoid(ref t2x, ref t2z, ref t2b, 0, 1, 0, 2, 3);
            Check(t1x * t2x < 0, "two NPCs with one dead ahead pick sides by their id, so they don't both dodge the same way");
            Check(BattleSpacing.Closing(0.04, 0) && BattleSpacing.Closing(0.04, 0.02) && !BattleSpacing.Closing(0.04, 0.035) && !BattleSpacing.Closing(0.04, 0.05) && !BattleSpacing.Closing(0, 0),
                "only a friend it is catching up with (standing, or much slower) is steered round; one marching along with it in a column or rank isn't");

            Check(FactionConfig.CaptainsFor(16, 8) == 2 && FactionConfig.CaptainsFor(22, 8) == 3 && FactionConfig.CaptainsFor(26, 8) == 3 && FactionConfig.CaptainsFor(13, 8) == 1
                && FactionConfig.CaptainsFor(15, 8) == 2 && FactionConfig.CaptainsFor(19, 8) == 2 && FactionConfig.CaptainsFor(3, 8) == 1 && FactionConfig.CaptainsFor(16, 0) == int.MaxValue,
                "captains per roster: Men 2, orcs 3, goblins 3, elves and dwarves 1, Uruk-hai and Dunlendings 2; at least 1; 0 turns the limit off");
            var fsC = new FactionSystem(); SetLegacy(fsC, new FactionConfig());
            var hC = new HiringSystem { Factions = fsC }; fsC.Hiring = hC;
            var capOwner = new HiringOwner { Name = "Cap" }; hC.SetOwner("capuid", capOwner);
            void AddRecruit(long id, string faction, bool captain) { capOwner.Recruits.Add(id); capOwner.RecruitFactions[id] = faction; if (captain) capOwner.Captains.Add(id); }
            AddRecruit(1, "gondor", true); AddRecruit(2, "gondor", false); AddRecruit(3, "gondor", false);
            bool oneMore = hC.CaptainFits("capuid", "gondor");
            AddRecruit(4, "gondor", true);
            Check(oneMore && !hC.CaptainFits("capuid", "gondor") && !hC.CaptainFits("capuid", "elves"), "a Gondor player with one captain may take a second, not a third (warriors and recruits don't count), and then no elf captain either");
            capOwner.Recruits.Remove(4);
            Check(hC.CaptainFits("capuid", "gondor"), "a captain who died or was dismissed frees the place");
            var orcOwner = new HiringOwner { Name = "Orc" }; hC.SetOwner("orcuid", orcOwner);
            foreach (long id in new long[] { 11, 12 }) { orcOwner.Recruits.Add(id); orcOwner.RecruitFactions[id] = "mordor"; orcOwner.Captains.Add(id); }
            bool third = hC.CaptainFits("orcuid", "mordor");
            orcOwner.Recruits.Add(13); orcOwner.RecruitFactions[13] = "mordor"; orcOwner.Captains.Add(13);
            Check(third && !hC.CaptainFits("orcuid", "mordor") && FactionConfig.InstructionText.Any(l => l.StartsWith("RosterPerCaptain")), "a Mordor player may lead 3 captains, not 4; #Instructions explain RosterPerCaptain");
            var offC = new FactionConfig { RosterPerCaptain = 0 }; SetLegacy(fsC, offC);
            Check(hC.CaptainFits("orcuid", "mordor") && hC.CaptainFits("capuid", "elves"), "RosterPerCaptain 0 turns the limit off");

            // Lines: one front place per enemy; at contact the front holds its side, the extras go round.
            var lsC = new BattleLineSystem(fsC); BattleLineSystem.WildLines = true;
            TestAgent LineNpc(string code, double x, double z) { var e = Npc(code); e.EntityId = 60000 + nextId++; e.Pos.SetPos(x, 0, z); return e; }
            var f1 = LineNpc("test-mordororc-warrior", 0, 0); var f2 = LineNpc("test-mordororc-recruit", 0, 2); var en = LineNpc("test-gondor-warrior", 12, 1);
            var lineC = lsC.Join(f1, en, 0); lsC.Join(f2, en, 0);
            int extraIdx = 1; var extra = lineC.Members[extraIdx] == f1.EntityId ? f1 : f2; var frontNpc = extra == f1 ? f2 : f1;
            bool extraWaits = lsC.HoldSlot(extra, en, out _, out _);
            bool noHoldBefore = !lsC.HoldsFront(frontNpc, out _, out _);
            lineC.Contact = true;
            Check(lineC.Front == 1 && extraWaits && noHoldBefore && !lsC.HoldSlot(extra, en, out _, out _) && lsC.HoldsFront(frontNpc, out double hfx, out double hfz) && Math.Abs(hfx * hfx + hfz * hfz - 1) < 1e-6 && !lsC.HoldsFront(extra, out _, out _),
                "two orcs against one man: one in front, the extra waits on the flank; once the lines meet the front orc keeps its side of the man and the extra goes round to surround");
            var near = lsC.Join(LineNpc("test-mordororc-warrior", 0, 20), LineNpc("test-gondor-warrior", 12, 21), 0);
            var far = lsC.Join(LineNpc("test-mordororc-warrior", 0, 60), LineNpc("test-gondor-warrior", 12, 61), 0);
            Check(near != lineC && far != lineC && lsC.SideMetNear(near) && !lsC.SideMetNear(far) && !lsC.SideMetNear(lineC),
                "0.0.118: a second orc line 20 blocks from one that has met counts as met too; one 60 blocks off doesn't, nor does a met line by itself");
            // 0.0.119: idle allies of the same side and formation are rallied into a line; others aren't.
            var idleOrc = LineNpc("test-mordororc-warrior", 5, 5); var idleMan = LineNpc("test-gondor-warrior", 5, 6);
            TestAgent Rec(string formation) { var r = LineNpc("test-gondor-warrior", 5, 7); r.WatchedAttributes.SetString(Recruit.OwnerUidAttr, "uidR"); Recruit.SetOrder(r, Recruit.Follow); r.WatchedAttributes.SetString(Recruit.FormationAttr, formation); return r; }
            var recLine = Rec(Formation.Line); var recLine2 = Rec(Formation.Line); var recWedge = Rec(Formation.Wedge);
            string recSide = BattleLineSystem.SideOf(recLine), recForm = Recruit.FormationOf(recLine);
            Check(lsC.CanRally(idleOrc, lineC.Side, Recruit.FormationOf(f1), 0) && !lsC.CanRally(idleMan, lineC.Side, Recruit.FormationOf(f1), 0)
                && !lsC.CanRally(f2, lineC.Side, Recruit.FormationOf(f1), 0)
                && lsC.CanRally(recLine2, recSide, recForm, 0) && !lsC.CanRally(recWedge, recSide, recForm, 0),
                "0.0.119: an idle orc is rallied into the orc line, not a man or an orc already in a line; a recruit in Line formation joins its leader's Line, not one set to Wedge");
            // 0.0.120: a recruit is rallied only onto a target it would attack.
            var recPassive = Rec(Formation.Line); Recruit.SetStance(recPassive, Recruit.Passive);
            var recAggro = Rec(Formation.Line); Recruit.SetStance(recAggro, Recruit.Aggressive);
            var orcFoe = LineNpc("test-mordororc-warrior", 9, 7);
            Check(lsC.WouldRally(idleOrc, idleMan) && !lsC.WouldRally(recPassive, orcFoe) && !lsC.WouldRally(recAggro, recLine) && !lsC.WouldRally(idleOrc, null),
                "0.0.120: an idle orc is rallied onto a man; a passive recruit isn't rallied, nor a recruit onto its own side's recruit");
            // 0.0.123: a wild NPC starting a fight calls idle wild NPCs of its own faction, not men, recruits or itself.
            var callOrc = LineNpc("test-mordororc-warrior", 30, 30); var callOrc2 = LineNpc("test-mordororc-recruit", 34, 30); var callMan = LineNpc("test-gondor-warrior", 36, 30);
            Check(AiTaskFactionSeek.AnswersCall(callOrc, callOrc2, 0) && !AiTaskFactionSeek.AnswersCall(callOrc, callMan, 0) && !AiTaskFactionSeek.AnswersCall(callOrc, callOrc, 0)
                && !AiTaskFactionSeek.AnswersCall(recLine, recLine2, 0) && FactionConfig.InstructionText.Any(l => l.Contains("JoinFightRange (32 by default")) && new FactionConfig().JoinFightRange == 32,
                "0.0.123: a wild orc that starts a fight calls an idle orc, not a man, itself or a hired recruit; #Instructions explain it");
            // 0.0.122: wild NPCs form no lines unless WildBattleLines is on; recruits in formation still do.
            BattleLineSystem.WildLines = false;
            bool wildOff = BattleLineSystem.SideOf(idleOrc) == null && BattleLineSystem.SideOf(recLine) != null;
            BattleLineSystem.WildLines = true;
            Check(wildOff && BattleLineSystem.SideOf(idleOrc) != null && !new FactionConfig().WildBattleLines && FactionConfig.InstructionText.Any(l => l.StartsWith("WildBattleLines")),
                "0.0.122: WildBattleLines is off by default: wild NPCs make no battle lines, recruits following in Line still do; #Instructions explain it");

            var v25 = Newtonsoft.Json.Linq.JObject.FromObject(FactionConfig.FromFile(null, out _)); v25["Version"] = 25; v25["BattleLines"] = false; v25.Remove("RosterPerCaptain");
            var m25 = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(v25.ToString()), out bool store25);
            Check(store25 && m25.Version == FactionConfig.CurrentVersion && m25.BattleLines && m25.RosterPerCaptain == 8, "a version 25 file turns battle lines on once and gains RosterPerCaptain 8");
            Check(lang["talk-captains-full"] != null, "the captain-limit reply is in the lang file");
            // 0.0.107: a version 26 file with the 0.0.103 tables and orcs following 7 takes the refit; the admin's values stay.
            string T4(Dictionary<string, float> t) => string.Join("/", FactionTiers.Ranks.Select(r => t[r].ToString(System.Globalization.CultureInfo.InvariantCulture)));
            var v26 = Newtonsoft.Json.Linq.JObject.FromObject(FactionConfig.FromFile(null, out _)); v26["Version"] = 26;
            foreach (var f in ((Newtonsoft.Json.Linq.JObject)v26["Factions"]).Properties())
            {
                var old = FactionTiers.Tables103[FactionTiers.Of(f.Name).Name];
                f.Value["Health"] = Newtonsoft.Json.Linq.JObject.FromObject(old.Health); f.Value["Damage"] = Newtonsoft.Json.Linq.JObject.FromObject(old.Damage);
                if (FactionTiers.Of(f.Name) == FactionTiers.Orc) f.Value["MaxFollowers"] = 7;
            }
            v26["Factions"]["gundabad"]["MaxFollowers"] = 9; v26["Factions"]["elves"]["Health"]["captain"] = 70;
            var m26 = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(v26.ToString()), out bool store26);
            Check(store26 && m26.Factions["mordor"].MaxFollowers == 6 && m26.Factions["angmar"].MaxFollowers == 6 && m26.Factions["gundabad"].MaxFollowers == 9
                && T4(m26.Factions["gondor"].Health) == "22/34/48/35" && T4(m26.Factions["mistymountains"].Health) == "17/22/32/22" && T4(m26.Factions["dwarves"].Damage) == "3.2/4.15/7.8/3.3"
                && T4(m26.Factions["elves"].Health) == "27/37/70/37" && T4(m26.Factions["elves"].Damage) == "3.7/5.3/9.7/4.3",
                "0.0.107: a version 26 file's 0.0.103 tables take the refit (the 0.0.108 one since then) and orcs following 7 follow 6; a follow limit or table the admin changed stays");
            // 0.0.108 (owner: "balance combat for equal coin spendage"): a version 27 file with the 0.0.107 tables and prices moves; the admin's values stay.
            string H4(Dictionary<string, int> t) => string.Join("/", FactionTiers.Ranks.Select(r => t[r]));
            string P3(Dictionary<string, int> t) => string.Join("/", new[] { "warrior", "archer", "captain" }.Select(r => t[r]));
            var v27 = Newtonsoft.Json.Linq.JObject.FromObject(FactionConfig.FromFile(null, out _)); v27["Version"] = 27;
            foreach (var f in ((Newtonsoft.Json.Linq.JObject)v27["Factions"]).Properties())
            {
                string tn = FactionTiers.Of(f.Name).Name;
                var old = FactionTiers.Tables107[tn];
                f.Value["Health"] = Newtonsoft.Json.Linq.JObject.FromObject(old.Health); f.Value["Damage"] = Newtonsoft.Json.Linq.JObject.FromObject(old.Damage);
                f.Value["HirePrices"] = Newtonsoft.Json.Linq.JObject.FromObject(FactionTiers.Hire107[tn]);
                f.Value["PromotePrices"] = Newtonsoft.Json.Linq.JObject.FromObject(FactionTiers.PromoteFor(FactionTiers.Hire107[tn]));
            }
            v27["Factions"]["angmar"]["HirePrices"]["captain"] = 30; v27["Factions"]["isengard"]["PromotePrices"]["warrior"] = 20; v27["Factions"]["elves"]["Damage"]["captain"] = 12;
            var m27 = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(v27.ToString()), out bool store27);
            Check(store27 && m27.Version == FactionConfig.CurrentVersion && FactionConfig.CurrentVersion == 30
                && H4(m27.Factions["mordor"].HirePrices) == "5/12/29/12" && P3(m27.Factions["mordor"].PromotePrices) == "9/9/25" && T4(m27.Factions["mordor"].Health) == "19/30/40/30"
                && H4(m27.Factions["angmar"].HirePrices) == "4/11/30/11" && P3(m27.Factions["angmar"].PromotePrices) == "8/8/25" && T4(m27.Factions["angmar"].Damage) == "2.7/3.35/6.2/2.65"
                && H4(m27.Factions["isengard"].HirePrices) == "4/15/41/16" && P3(m27.Factions["isengard"].PromotePrices) == "20/14/40"
                && H4(m27.Factions["elves"].HirePrices) == "6/19/50/20" && P3(m27.Factions["elves"].PromotePrices) == "16/17/46" && T4(m27.Factions["elves"].Damage) == "3.65/5.35/12/4.25" && T4(m27.Factions["elves"].Health) == "27/37/51/36"
                && H4(m27.Factions["gondor"].HirePrices) == "5/15/40/15" && T4(m27.Factions["gondor"].Damage) == "3.15/3.95/7/3.05",
                "0.0.108: a version 27 file's 0.0.107 tables and hire prices take the new ones, promotion prices that followed the old hire prices follow the new; prices or tables the admin changed stay");
        }

        // ---- 0.0.103 (owner chose "Narrow orcs to ~0.78 of a Man"): unchanged 0.0.91 orc tables move, the admin's stay ----
        {
            string T3(Dictionary<string, float> t) => string.Join("/", FactionTiers.Ranks.Select(r => t[r].ToString(System.Globalization.CultureInfo.InvariantCulture)));
            var v24 = Newtonsoft.Json.Linq.JObject.FromObject(FactionConfig.FromFile(null, out _)); v24["Version"] = 24;
            foreach (string f in new[] { "mordor", "angmar", "gundabad" })
            {
                v24["Factions"][f]["Health"] = Newtonsoft.Json.Linq.JObject.FromObject(FactionTiers.OrcHealth091);
                v24["Factions"][f]["Damage"] = Newtonsoft.Json.Linq.JObject.FromObject(FactionTiers.OrcDamage091);
            }
            v24["Factions"]["angmar"]["Health"]["captain"] = 60;   // the admin's own Angmar health stays, its damage still moves
            var m24 = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(v24.ToString()), out bool store24);
            double Strength(string tier, string rank) { var t = tier == "orc" ? FactionTiers.Orc : FactionTiers.Men; return t.Health[rank] * t.Damage[rank]; }
            Check(store24 && T3(m24.Factions["mordor"].Health) == "19/30/40/30" && T3(m24.Factions["gundabad"].Damage) == "2.7/3.35/6.2/2.65"
                && T3(m24.Factions["angmar"].Health) == "15/27/60/22" && T3(m24.Factions["angmar"].Damage) == "2.7/3.35/6.2/2.65" && T3(m24.Factions["gondor"].Health) == "22/34/48/35"
                && FactionTiers.Ranks.All(r => Strength("orc", r) / Strength("men", r) > 0.7 && Strength("orc", r) / Strength("men", r) < 0.82),
                "0.0.103: orcs narrowed to about 0.78 of a Man per rank (health x damage; about 0.74 after the 0.0.108 refit); a version 24 file's unchanged orc tables move, a value the admin changed keeps that table");
        }

        // ---- 0.0.102 (owner: "Implement it again, do 4 per though"): a version 23 file's unchanged 0 becomes 4 ----
        {
            var v23 = Newtonsoft.Json.Linq.JObject.FromObject(FactionConfig.FromFile(null, out _));
            v23["Version"] = 23; v23["MaxAttackersPerTarget"] = 0;
            var m23 = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(v23.ToString()), out bool store23);
            v23["MaxAttackersPerTarget"] = 6;
            var m23b = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(v23.ToString()), out _);
            var cap = new BattleSpacing(); long tc = 100000;
            for (long a = 1; a <= 4; a++) cap.Claim(a, 50, 0, tc);
            Check(store23 && m23.Version == FactionConfig.CurrentVersion && m23.MaxAttackersPerTarget == 4 && m23b.MaxAttackersPerTarget == 6 && BattleSpacing.DefaultMaxAttackers == 4
                && cap.Crowded(5, 50, false, tc) && !cap.Crowded(4, 50, false, tc),
                "0.0.102: a version 23 file's no-limit 0 becomes 4, a limit the admin chose stays; a new battle lets 4 NPCs at one enemy and turns the fifth away");
        }

        // ---- 0.0.87: inner and outer rings (owner's screenshot: a big group on two enemies is one pile) ----
        {
            var rings = new BattleSpacing();
            long t0 = 500000;
            Check(rings.Room == 5 && Math.Abs(rings.OuterRadius - 2.8) < 1e-9 && rings.OuterRadius - 0.3 - 0.3 > 2.0,
                "five attackers fit round an enemy 1.3 blocks out, 1.5 apart; the outer ring is 2.8 out, beyond melee reach");
            // Nine attackers coming from all round: the first five spread in the inner ring, the other four in the outer.
            double[] ax = new double[9], az = new double[9];
            for (int k = 0; k < 9; k++) { double b = k * 0.7; ax[k] = Math.Sin(b) * 6; az[k] = Math.Cos(b) * 6; }
            bool[] inner = new bool[9];
            for (int step = 0; step < 60; step++)
                for (int k = 0; k < 9; k++)
                {
                    long id = 700 + k;
                    double bearing = Math.Atan2(ax[k], az[k]);
                    rings.Claim(id, 77, bearing, t0);
                    rings.SurroundAngle(id, 77, bearing, t0, out double angle, out inner[k]);
                    BattleSpacing.SpotAt(0, 0, angle, inner[k] ? rings.SurroundRadius : rings.OuterRadius, out double sx, out double sz);
                    ax[k] += (sx - ax[k]) * 0.3; az[k] += (sz - az[k]) * 0.3;
                }
            double minGap = double.MaxValue;
            for (int k = 0; k < 9; k++)
                for (int m = k + 1; m < 9; m++) minGap = Math.Min(minGap, Math.Sqrt((ax[k] - ax[m]) * (ax[k] - ax[m]) + (az[k] - az[m]) * (az[k] - az[m])));
            bool radiiOk = Enumerable.Range(0, 9).All(k => Math.Abs(Math.Sqrt(ax[k] * ax[k] + az[k] * az[k]) - (inner[k] ? 1.3 : 2.8)) < 0.05);
            Check(inner.Take(5).All(x => x) && inner.Skip(5).All(x => !x) && radiiOk && minGap > 1.4,
                "nine attackers on one enemy: the first five fight in the inner ring, the other four wait in the outer ring, nobody closer than " + minGap.ToString("0.00") + " blocks to another");
            Check(!rings.HasRoom(800, 77, t0) && rings.HasRoom(702, 77, t0) && !rings.HasRoom(707, 77, t0) && rings.HasRoom(800, 78, t0),
                "an enemy with five attackers has no room for a newcomer (or for those waiting outside), one nobody fights has");
            rings.Forget(701);
            rings.SurroundAngle(705, 77, Math.Atan2(ax[5], az[5]), t0, out _, out bool movedUp);
            Check(movedUp, "when an inner fighter falls, the first one waiting outside steps into the inner ring");
        }

        // ---- 0.0.88: the edge guard (owner: NPCs "will fling themselves off the edge" of deep pits) ----
        {
            // A column of cells: ground at y 9 everywhere but a pit at x 5 (deep), a 3-block step down at x 6, water at
            // x 7, lava at x 8, a wall at x 9.
            EntityBehaviorEdgeGuard.Cell Cell(int x, int y, int z)
            {
                if (x == 5) return y < 0 ? EntityBehaviorEdgeGuard.Cell.Solid : EntityBehaviorEdgeGuard.Cell.Air;
                if (x == 6) return y <= 6 ? EntityBehaviorEdgeGuard.Cell.Solid : EntityBehaviorEdgeGuard.Cell.Air;
                if (x == 7) return y <= 4 ? EntityBehaviorEdgeGuard.Cell.Solid : y <= 6 ? EntityBehaviorEdgeGuard.Cell.Water : EntityBehaviorEdgeGuard.Cell.Air;
                if (x == 8) return y == 8 ? EntityBehaviorEdgeGuard.Cell.Lava : y < 8 ? EntityBehaviorEdgeGuard.Cell.Solid : EntityBehaviorEdgeGuard.Cell.Air;
                if (x == 9) return y <= 11 ? EntityBehaviorEdgeGuard.Cell.Solid : EntityBehaviorEdgeGuard.Cell.Air;
                if (x == 10) return y <= 3 ? EntityBehaviorEdgeGuard.Cell.Solid : EntityBehaviorEdgeGuard.Cell.Air;
                return y <= 9 ? EntityBehaviorEdgeGuard.Cell.Solid : EntityBehaviorEdgeGuard.Cell.Air;
            }
            const int feet = 10, max = EntityBehaviorEdgeGuard.MaxSafeDrop;
            Check(!EntityBehaviorEdgeGuard.Unsafe(Cell, 0, feet, 0, max) && EntityBehaviorEdgeGuard.Unsafe(Cell, 5, feet, 0, max) && !EntityBehaviorEdgeGuard.Unsafe(Cell, 6, feet, 0, max)
                && !EntityBehaviorEdgeGuard.Unsafe(Cell, 7, feet, 0, max) && EntityBehaviorEdgeGuard.Unsafe(Cell, 8, feet, 0, max) && !EntityBehaviorEdgeGuard.Unsafe(Cell, 9, feet, 0, max)
                && EntityBehaviorEdgeGuard.Unsafe(Cell, 10, feet, 0, max) && max == 4,
                "the edge guard stops a step into a deep pit, onto lava, or down more than 4 blocks, but allows flat ground, a 3-block step down, water below and a wall ahead");
            Check(entityFiles.Values.All(j => { var b = j["server"]["behaviors"].Select(x => (string)x["code"]).ToList(); return b.IndexOf("lotrnpcs:edgeguard") == b.IndexOf("taskai") + 1; }),
                "every NPC type has the edge guard right after its AI, so it sees each step the AI sets before the game moves the NPC");
            // 0.0.112: harvestable bodies.
            Check(entityFiles.Values.All(j => { var sb = j["server"]["behaviors"].Select(x => (string)x["code"]).ToList(); var cb = j["client"]["behaviors"].Select(x => (string)x["code"]).ToList();
                    return sb.Contains("harvestable") && sb.Contains("lotrnpcs:bodyloot") && sb.Contains("deaddecay") && cb.Contains("harvestable")
                        && j["server"]["behaviors"].First(x => (string)x["code"] == "harvestable")["drops"] is JArray d && d.Count == 0; }),
                "0.0.112: every NPC type leaves a knife-harvestable body that rots; its vanilla drops list is empty, the config fills it");
        }

        // ---- 0.0.90: factions "unequally equal" (follow shares, tiers, config v19; prompts/0.0.90-faction-balance.md) ----
        {
            var c90 = FactionConfig.FromFile(null, out bool store90);
            string j90 = Newtonsoft.Json.JsonConvert.SerializeObject(c90, Newtonsoft.Json.Formatting.Indented);
            FactionDef D(string f) => c90.Factions[f];
            Check(store90 && c90.Version == FactionConfig.CurrentVersion && j90.Contains("\"MaxFollowers\": 6") && j90.Contains("\"Health\": {") && !j90.Contains("Multiplier") && j90.Contains("\"DamageSpread\": 0.2") && c90.Factions.Count == 18
                && c90.Factions.Values.All(d => d.MaxFollowers != null && d.MaxRecruits != null && d.Health?.Count == 6 && d.Damage?.Count == 6),
                "a new file is version 20: every faction has MaxFollowers, MaxRecruits and Health and Damage for each rank (no multipliers), and DamageSpread 0.2");
            string RT(Dictionary<string, float> t) => string.Join("/", new[] { "recruit", "warrior", "captain", "archer" }.Select(r => t[r].ToString(System.Globalization.CultureInfo.InvariantCulture)));
            bool Tier(string f, int fol, int ros, string h, string d) => D(f).MaxFollowers == fol && D(f).MaxRecruits == ros && RT(D(f).Health) == h && RT(D(f).Damage) == d;
            Check(new[] { "gondor", "rohan", "dale", "arnor", "haradrim", "easterlings", "blacknumenoreans", "shire" }.All(f => Tier(f, 5, 16, "22/34/48/35", "3.15/3.95/7/3.05"))
                && Tier("isengard", 5, 15, "23/32/48/36", "3/4.3/7.1/3") && Tier("dunlendings", 6, 19, "18/28/41/28", "2.85/3.6/6.1/2.8")
                && new[] { "mordor", "angmar", "gundabad" }.All(f => Tier(f, 6, 22, "19/30/40/30", "2.7/3.35/6.2/2.65")) && Tier("mistymountains", 8, 26, "17/22/32/22", "1.85/2.8/4.7/2.2")
                && new[] { "elves", "tormentedelves" }.All(f => Tier(f, 4, 13, "27/37/51/36", "3.7/5.3/9.7/4.3")) && new[] { "dwarves", "wickeddwarves" }.All(f => Tier(f, 4, 13, "31/47/63/47", "3.2/4.15/7.8/3.3")),
                "the tiers (0.0.108 refit): Men 5/16 22/34/48/35 health, orcs 6/22 19/30/40/30 health and 2.7/3.35/6.2/2.65 damage, elves 4/13, dwarves 4/13, goblins 8/26, Dunlendings 6/19, Uruks 5/15");
            string P(Dictionary<string, int> p, params string[] ranks) => string.Join("/", ranks.Select(r => p[r]));
            string[] H = { "recruit", "warrior", "captain", "archer" }, Pr = { "warrior", "archer", "captain" };
            Check(P(D("gondor").HirePrices, H) == "5/15/40/15" && P(D("isengard").HirePrices, H) == "4/15/41/16" && P(D("dunlendings").HirePrices, H) == "4/12/35/11"
                && P(D("mordor").HirePrices, H) == "5/12/29/12" && P(D("mistymountains").HirePrices, H) == "3/9/25/10" && P(D("elves").HirePrices, H) == "6/19/50/20" && P(D("dwarves").HirePrices, H) == "6/19/50/19"
                && P(D("gondor").PromotePrices, Pr) == "12/12/36" && P(D("isengard").PromotePrices, Pr) == "12/13/37" && P(D("mordor").PromotePrices, Pr) == "9/9/25" && P(D("mistymountains").PromotePrices, Pr) == "6/7/21" && P(D("elves").PromotePrices, Pr) == "16/17/46",
                "tier prices fitted for equal coin (0.0.108): per rank (0.0.108): Men 5/15/40/15, Uruks 4/15/41/16, Dunlendings 4/12/35/11, orcs 5/12/29/12, goblins 3/9/25/10, elves 6/19/50/20, dwarves 6/19/50/19");
            var mine = Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(@"{ ""Version"": 20, ""DamageSpread"": 7, ""Factions"": { ""rivendell"": { ""Stance"": ""aggressive"" }, ""mordor"": { ""MaxFollowers"": 99, ""Health"": { ""Recruit"": 0, ""warrior"": 26.4 }, ""Damage"": { ""captain"": 6.5 } } } }");
            var mMine = FactionConfig.FromFile(mine, out _);
            Check(mMine.Factions["rivendell"].MaxFollowers == 5 && mMine.Factions["rivendell"].MaxRecruits == 16 && RT(mMine.Factions["rivendell"].Health) == "22/34/48/35" && P(mMine.Factions["rivendell"].HirePrices, H) == "5/15/40/15"
                && mMine.Factions["mordor"].MaxFollowers == 32 && RT(mMine.Factions["mordor"].Health) == "1/26/40/30" && RT(mMine.Factions["mordor"].Damage) == "2.7/3.35/6.5/2.65" && mMine.Factions["mordor"].MaxRecruits == 22 && mMine.Spread == 0.9,
                "a faction the mod doesn't ship gets the baseline; out of range values are clamped (follow 1 to 32, health at least 1 and whole, spread at most 0.9), rank names in any case, missing ranks filled from the tier");
            Check(mMine.FollowersOf("nobody") == 5 && mMine.RosterOf(null) == 16 && mMine.HealthFor("nobody", "recruit") == null && mMine.DamageFor("mordor", null) == null && mMine.HealthFor("mordor", "warrior") == 26 && mMine.DamageFor("mordor", "captain") == 6.5f,
                "a faction the config doesn't know, or a rankless NPC, keeps its type's health and damage; a faction's own rank values are used");
            // Version 19 (0.0.90's multipliers) to 20.
            var o19 = Newtonsoft.Json.Linq.JObject.FromObject(c90); o19["Version"] = 19; ((Newtonsoft.Json.Linq.JObject)o19).Remove("DamageSpread");
            foreach (var f in ((Newtonsoft.Json.Linq.JObject)o19["Factions"]).Properties())
            {
                var fo = (Newtonsoft.Json.Linq.JObject)f.Value; fo.Remove("Health"); fo.Remove("Damage");
                var tier = FactionTiers.Of(f.Name); fo["HealthMultiplier"] = tier.Health090; fo["DamageMultiplier"] = tier.Damage090;
            }
            o19["Factions"]["mordor"]["HealthMultiplier"] = 1.0;   // the admin made orcs as tough as men
            o19["Factions"]["elves"]["DamageMultiplier"] = 2.0;
            var m19 = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(o19.ToString()), out bool store19);
            string j19 = Newtonsoft.Json.JsonConvert.SerializeObject(m19);
            Check(store19 && m19.Version == FactionConfig.CurrentVersion && RT(m19.Factions["gondor"].Health) == "22/34/48/35" && RT(m19.Factions["dwarves"].Damage) == "3.2/4.15/7.8/3.3"
                && RT(m19.Factions["mordor"].Health) == "22/34/48/34" && RT(m19.Factions["mordor"].Damage) == "2.7/3.35/6.2/2.65" && RT(m19.Factions["elves"].Damage) == "6/8/14/6" && RT(m19.Factions["elves"].Health) == "27/37/51/36"
                && !j19.Contains("Multiplier") && m19.DamageSpread == 0.2,
                "a version 19 file: multipliers as 0.0.90 shipped them take the fitted per-rank tables; ones the admin changed become the units' own values times theirs (orcs 22/34/48/34, elves hit 6/8/14/6); the multipliers aren't written back; DamageSpread 0.2 comes in");
            // The spread.
            Check(FactionTiers.Spread(10, 0.2, 0) == 8 && FactionTiers.Spread(10, 0.2, 1) == 12 && FactionTiers.Spread(10, 0.2, 0.5) == 10 && FactionTiers.Spread(10, 0, 0) == 10 && new FactionConfig().Spread == 0.2
                && new FactionConfig { DamageSpread = -1 }.Spread == 0 && Enumerable.Range(0, 1000).Select(i => FactionTiers.Spread(5, 0.2, i / 999.0)).All(v => v >= 4 - 1e-4 && v <= 6 + 1e-4),
                "the hit spread: 20% either way (10 damage lands as 8 to 12), 0 turns it off, never below 0");

            // Health: rounded half up, at least 1.
            Check(FactionTiers.ScaledHealth(22, 0.66f) == 15 && FactionTiers.ScaledHealth(34, 0.66f) == 22 && FactionTiers.ScaledHealth(48, 0.66f) == 32 && FactionTiers.ScaledHealth(22, 0.57f) == 13
                && FactionTiers.ScaledHealth(48, 1.33f) == 64 && FactionTiers.ScaledHealth(22, 1.05f) == 23 && FactionTiers.ScaledHealth(34, 1.05f) == 36 && FactionTiers.ScaledHealth(22, 1f) == 22
                && FactionTiers.ScaledHealth(5, 0.1f) == 1 && FactionTiers.ScaledHealth(0, 2f) == 0 && FactionTiers.Round(12.5) == 13 && FactionTiers.Round(28.57) == 29,
                "health under a multiplier: orc recruit 15, warrior 22, captain 32; goblin recruit 13; dwarf captain 64; Uruk recruit 23; never below 1");
            var fs90 = new FactionSystem(); fs90.SetConfig(FactionConfig.FromFile(null, out _));
            TestAgent Hp90(string code, float baseMax, float cur) { var e = Npc(code); var h = new TreeAttribute(); h.SetFloat("basemaxhealth", baseMax); h.SetFloat("maxhealth", baseMax); h.SetFloat("currenthealth", cur); e.WatchedAttributes["health"] = h; return e; }
            float Hv(TestAgent e, string k) => e.WatchedAttributes.GetTreeAttribute("health").GetFloat(k);
            var orcNew = Hp90("test-mordororc-recruit", 22, 22); var orcHurt = Hp90("test-mordororc-captain", 48, 40); var manNew = Hp90("test-gondor-recruit", 22, 22); var dwNew = Hp90("test-dwarf-captain", 48, 48);
            // The harness's types carry no server behaviors (no type health), so the type's health is passed in: what
            // ScaledMaxHealth does with TypeMaxHealth in game.
            float S(TestAgent e, float typeMax) => fs90.Config.HealthFor(FactionSystem.FactionOf(e), EntityBehaviorFactionMember.RankOf(e)) ?? typeMax;
            bool c1 = EntityBehaviorFactionMember.SyncMaxHealth(orcNew, S(orcNew, 22));
            EntityBehaviorFactionMember.SyncMaxHealth(orcHurt, S(orcHurt, 48));
            bool c3 = EntityBehaviorFactionMember.SyncMaxHealth(manNew, S(manNew, 22));
            EntityBehaviorFactionMember.SyncMaxHealth(dwNew, S(dwNew, 48));
            Console.WriteLine($"   > health: orc recruit {Hv(orcNew, "maxhealth")}/{Hv(orcNew, "currenthealth")} (changed {c1}), orc captain {Hv(orcHurt, "maxhealth")}/{Hv(orcHurt, "currenthealth")}, man {Hv(manNew, "maxhealth")} (changed {c3}), dwarf captain {Hv(dwNew, "maxhealth")}/{Hv(dwNew, "currenthealth")}");
            Check(c1 && Hv(orcNew, "maxhealth") == 19 && Hv(orcNew, "currenthealth") == 19 && Hv(orcHurt, "maxhealth") == 40 && Hv(orcHurt, "currenthealth") == 32
                && !c3 && Hv(manNew, "maxhealth") == 22 && Hv(dwNew, "maxhealth") == 63 && Hv(dwNew, "currenthealth") == 63
                && !EntityBehaviorFactionMember.SyncMaxHealth(orcNew, S(orcNew, 22)) && EntityBehaviorFactionMember.ScaledMaxHealth(orcNew, null) == 0,
                "on load an orc recruit takes 19 health (full stays full), a wounded orc captain keeps its 8 wounds (32 of 40), a man is unchanged, a dwarf captain gets 63; a second load changes nothing");
            Check(fs90.Config.DamageFor("mordor", "recruit") == 2.7f && fs90.Config.DamageFor("gondor", "recruit") == 3.15f && fs90.Config.DamageFor("elves", "captain") == 9.7f && fs90.Config.DamageFor("mordor", "archer") == 2.65f
                && EntityBehaviorFactionMember.RankOf(Npc("test-mordororc-archer")) == "archer" && EntityBehaviorFactionMember.RankOf(Npc("test-goblin")) == null,
                "damage per rank: an orc recruit hits for 2.7, a Men recruit 3.15, an elf captain 9.7, an orc archer's arrow 2.65 (before the spread); the rankless goblin keeps its own");

            // Shares.
            var h90 = new HiringSystem { Factions = fs90 };
            double men = h90.FollowShare("gondor"), orcS = h90.FollowShare("mordor"), gob = h90.FollowShare("mistymountains"), elf = h90.FollowShare("elves"), dw = h90.FollowShare("dwarves");
            Check(HiringSystem.Fits(4 * men, men) && !HiringSystem.Fits(5 * men, men) && HiringSystem.Fits(5 * orcS, orcS) && !HiringSystem.Fits(6 * orcS, orcS) && HiringSystem.Fits(7 * gob, gob) && !HiringSystem.Fits(8 * gob, gob)
                && HiringSystem.Fits(3 * men + orcS, orcS) && !HiringSystem.Fits(3 * men + 2 * orcS, orcS) && !HiringSystem.Fits(3 * men + 2 * orcS, gob)
                && HiringSystem.Fits(2 * elf + dw, dw) && HiringSystem.Fits(elf + 5 * gob, gob) && !HiringSystem.Fits(4 * men, elf),
                "follow shares: 5 men, 6 orcs or 8 goblins fill a band; 3 men and 2 orcs fit but not a third orc or a goblin; 2 elves and 2 dwarves fill it exactly; 4 men leave no room for an elf (never over 1)");
            Check(h90.FollowShare("nobody") == 0.2 && Math.Abs(h90.RosterShare("mordor") - 1.0 / 22) < 1e-12 && Math.Abs(h90.RosterShare(null) - 1.0 / 16) < 1e-12,
                "an unknown faction takes the baseline share (1/5, 1/16); an orc takes 1/22 of the roster");
            var room = h90.Room("uidNobody", "mordor");
            JObject langJ = lang;
            string LangF(string k, object[] a) { string t = (string)langJ[k] ?? ("lotrnpcs:" + k); return a.Length == 0 ? t : string.Format(t, a); }
            Check(room.Count == 1 && room[0].Faction == "mordor" && room[0].Count == 6 && HiringSystem.RoomText(room, LangF) == "Room in your band: Mordor orcs 6."
                && HiringSystem.RoomText(new List<RoomEntry>(), LangF) == "" && HiringSystem.Folk("rivendell", LangF) == "rivendell",
                "the room line: \"Room in your band: Mordor orcs 6.\" for a player with nobody following; a faction without a folk name shows its id");

            // Over the limits (owner chose "B", then "BB").
            string ou = "uidOver";
            var hl = new HiringSystem { Factions = fs90 };
            var loadedL = new Dictionary<long, EntityAgent>();
            var owner90 = new HiringOwner { Name = "Thorin" };
            for (int i = 1; i <= 16; i++)
            {
                var dw90 = Npc("test-dwarf-recruit"); dw90.EntityId = 9000 + i; Recruit.Bind(dw90, ou, "Thorin");
                owner90.Recruits.Add(dw90.EntityId); owner90.RecruitFactions[dw90.EntityId] = "dwarves";
                if (i != 14) loadedL[dw90.EntityId] = dw90;
            }
            owner90.Recruits.Add(9100); owner90.Recruits.Add(9101);   // hired before 0.0.90, not loaded since: faction unknown
            hl.SetOwner(ou, owner90);
            Check(Math.Abs(hl.RosterUsed(ou) - (16.0 / 13 + 2.0 / 16)) < 1e-9 && HiringSystem.Over(hl.RosterUsed(ou)), "16 dwarves (roster 13) and 2 recruits of unknown faction use 1.35 of the roster");
            var (gone90, later90, ret90) = hl.EnforceLimits(ou, id => loadedL.TryGetValue(id, out var e) ? e : null, e => "D" + e.EntityId);
            Check(gone90.SequenceEqual(new[] { "D9016", "D9015", "D9013", "D9012" }) && later90 == 1 && ret90 == 0 && owner90.PendingDismiss.SequenceEqual(new long[] { 9014 })
                && owner90.Recruits.Count == 13 && owner90.Recruits.Contains(9100) && owner90.Recruits.Contains(9101) && !owner90.RecruitFactions.ContainsKey(9016) && !owner90.RecruitFactions.ContainsKey(9014)
                && Recruit.OwnerUidOf(loadedL[9016]) == null && Recruit.OwnerUidOf(loadedL[9011]) == ou && !HiringSystem.Over(hl.RosterUsed(ou)),
                "over the roster, the most recently hired known recruits go first (the far-away one when it next loads); recruits of unknown faction are never dismissed");
            var again90 = hl.EnforceLimits(ou, id => loadedL.TryGetValue(id, out var e) ? e : null, e => "D" + e.EntityId);
            Check(again90.Gone.Count == 0 && again90.Later == 0 && owner90.Recruits.Count == 13, "a player within the limits loses nobody");
            // An old recruit loads: its faction is learned. Two more dwarves fill the roster exactly (13 of 13), so nobody goes.
            var old1 = Npc("test-dwarf-warrior"); old1.EntityId = 9100; Recruit.Bind(old1, ou, "Thorin"); hl.OnRecruitLoaded(old1);
            var old2 = Npc("test-dwarf-recruit"); old2.EntityId = 9101; Recruit.Bind(old2, ou, "Thorin"); hl.OnRecruitLoaded(old2);
            Check(owner90.RecruitFactions[9100] == "dwarves" && owner90.RecruitFactions[9101] == "dwarves" && owner90.Recruits.Count == 13 && Math.Abs(hl.RosterUsed(ou) - 1) < 1e-9,
                "a recruit hired before 0.0.90 is learned when it loads; 13 dwarves fill the roster exactly and stay");
            // Promotion keeps the faction's share.
            var promoted = Npc("test-dwarf-captain"); promoted.EntityId = 9200; Recruit.Bind(promoted, ou, "Thorin");
            hl.Replace(old1, promoted);
            Check(owner90.Recruits.IndexOf(9200) >= 0 && !owner90.Recruits.Contains(9100) && owner90.RecruitFactions[9200] == "dwarves" && !owner90.RecruitFactions.ContainsKey(9100),
                "a promoted recruit takes its place with the same faction (same shares)");
            // 0.0.91 roster line (owner: players "should be notified how many recruits they have left in their capacity").
            var rr = hl.RosterRoom(ou, "mordor");
            Check(rr.Count == 2 && rr[0].Faction == "mordor" && rr[0].Count == 0 && rr[1].Faction == "dwarves" && rr[1].Count == 0
                && HiringSystem.RosterText(rr, LangF) == "Room on your roster: Mordor orcs 0, Dwarves 0." && HiringSystem.RosterText(new List<RoomEntry>(), LangF) == "",
                "the roster line for a full roster of 13 dwarves: \"Room on your roster: Mordor orcs 0, Dwarves 0.\"");
            var hEmpty = new HiringSystem { Factions = fs90 };
            hEmpty.SetOwner("uidFresh", new HiringOwner { Name = "Fresh" });
            Check(hEmpty.RosterRoom("uidFresh", "mordor").Single().Count == 22 && hEmpty.RosterRoom("uidFresh", "elves").Single().Count == 13 && hEmpty.RosterRoom("uidNobody", "gondor").Single().Count == 16,
                "a player with no recruits has room for 22 orcs, 13 elves or 16 men");
            var round90 = Newtonsoft.Json.JsonConvert.DeserializeObject<HiringOwner>(Newtonsoft.Json.JsonConvert.SerializeObject(owner90));
            Check(round90.RecruitFactions.Count == owner90.RecruitFactions.Count && round90.RecruitFactions[9200] == "dwarves", "recruits' factions are saved with the world");

            // Texts.
            Check(new[] { "room-line", "roster-line", "hired-room", "limits-dismissed", "limits-away", "limits-dismissed-later", "limits-returned" }.All(k => langJ[k] != null)
                && fs90.Config.Factions.Keys.All(f => langJ["folk-" + f] != null) && ((string)langJ["talk-full"]).Contains("{7}") && ((string)langJ["talk-followfull"]).Contains("{6}")
                && ((string)langJ["cmd-follow-full"]).Contains("{2}") && !((string)langJ["talk-followfull"]).Contains("{5}")
                && langJ.Properties().Where(pp => pp.Name.StartsWith("room-") || pp.Name.StartsWith("limits-") || pp.Name.StartsWith("folk-")).All(pp => !((string)pp.Value).Contains("—")),
                "lang: the room line, the over-limit lines and a folk name for every faction; the talk lines show the room line instead of the old caps; no em dashes");
            Check(FactionConfig.InstructionText.Any(l => l.StartsWith("MaxFollowers and MaxRecruits") && l.Contains("1/MaxFollowers")) && FactionConfig.InstructionText.Any(l => l.StartsWith("Health and Damage") && l.Contains("by rank"))
                && FactionConfig.InstructionText.Any(l => l.StartsWith("DamageSpread")) && !FactionConfig.InstructionText.Any(l => l.Contains("Multiplier"))
                && FactionConfig.InstructionText.Any(l => l.StartsWith("ShareFriends") && l.Contains("hire")) && FactionConfig.InstructionText.Any(l => l.StartsWith("Hiring:") && l.Contains("ShareFriends")),
                "#Instructions explain follow shares, the multipliers, and that ShareFriends allies can hire each other's NPCs (owner: \"every faction they are allied to\")");
            Check(fs90.DescribeFactions().Contains("mordor: aggressive") && fs90.DescribeFactions().Contains("follow 6, roster 22, health 19/30/40/30/38/24, damage 2.7/3.35/6.2/2.65/2.68/4.19") && fs90.DescribeFactions().Contains("Hits vary by 20% either way."),
                "/recruits faction list shows each faction's follow limit, roster and per-rank health and damage, and the hit spread");
        }

        // ---- 0.0.92: factions are picked (owner: "instead of applying the race allegiance, have them select right after
        // making a character"; groups stop counting, "A"; every faction with Joinable, "A") ----
        {
            var c92 = FactionConfig.FromFile(null, out _);
            var fs92 = new FactionSystem(); fs92.SetConfig(c92);
            Check(!c92.RaceAndGroupMembership && c92.Factions.Values.All(d => d.Joinable) && Newtonsoft.Json.JsonConvert.SerializeObject(c92).Contains("\"RaceAndGroupMembership\":false"),
                "a new file: race and groups don't count (RaceAndGroupMembership false) and every faction is Joinable");
            Check(!fs92.IsFriend("shire", "lrc:harfoot", new List<string>()) && !fs92.IsFriend("elves", "lrc:noldor", new List<string>()) && !fs92.IsFriend("gondor", null, new List<string> { "gondor" })
                && fs92.IsFriend("rohan", "lrc:noldor", new List<string>(), "rohan") && !fs92.IsFriend("elves", "lrc:noldor", new List<string>(), "rohan"),
                "a harfoot isn't Shire and a Noldor isn't Elves by race, a group doesn't make anyone a member; the picked faction is the one that counts");
            var gr = c92.Relations.First(r => FactionConfig.SamePair(r, "gondor", "rohan")); gr.Relation = "ally"; gr.ShareFriends = true; fs92.SetConfig(c92);
            Check(fs92.IsFriend("gondor", null, null, "rohan") && !fs92.IsFriend("dale", null, null, "rohan"),
                "a ShareFriends ally still counts the player as a friend (and lets them hire); other factions don't");
            // The menu's list and the pick.
            c92.Factions["wickeddwarves"].Joinable = false; fs92.SetConfig(c92);
            var opts = JoinSystem.Options(c92);
            Check(opts.Count == 17 && opts[0].Faction == "shire" && opts[1].Faction == "gondor" && opts.All(o => o.Faction != "wickeddwarves")
                && opts.First(o => o.Faction == "mordor").Followers == 6 && opts.First(o => o.Faction == "mordor").Roster == 22 && opts.First(o => o.Faction == "shire").Stance == "defensive",
                "the menu lists every Joinable faction in the creative tab's order, with its stance, follow limit and roster; Joinable false leaves one out");
            var r1 = JoinSystem.Pick(fs92, "uidNew", "Frodo", "Mordor");
            var r2 = JoinSystem.Pick(fs92, "uidNew", "Frodo", "gondor");
            var r3 = JoinSystem.Pick(fs92, "uidOther", "Sam", "wickeddwarves");
            var r4 = JoinSystem.Pick(fs92, "uidOther", "Sam", "nowhere");
            Check(r1.Ok && r1.Faction == "mordor" && fs92.AssignedFaction("uidNew") == "mordor" && !r2.Ok && r2.Error == "lotrnpcs:join-already" && fs92.AssignedFaction("uidNew") == "mordor"
                && !r3.Ok && r3.Error == "lotrnpcs:join-closed" && !r4.Ok && r4.Error == "lotrnpcs:join-unknown" && fs92.AssignedFaction("uidOther") == null,
                "a pick sets the faction once; a second pick is refused (only admins change it); a faction left out of the menu or unknown is refused");
            fs92.SetAssigned("uidNew", "Frodo", null);
            Check(JoinSystem.Pick(fs92, "uidNew", "Frodo", "shire").Ok && fs92.AssignedFaction("uidNew") == "shire", "after an admin's clear the player can pick again");
            // Texts.
            Check(new[] { "join-title", "join-explain", "join-followers", "join-follower", "join-total", "join-legend", "join-bloc-free", "join-bloc-shadow", "join-bloc-other", "join-stance-aggressive", "join-stance-defensive", "join-confirm", "join-yes", "join-back", "join-later", "join-none", "join-done", "join-reminder", "join-already", "join-closed", "join-unknown" }.All(k => lang[k] != null)
                && ((string)lang["join-reminder"]).Contains("/recruits join") && ((string)lang["join-confirm"]).Contains("permanent")
                && FactionConfig.InstructionText.Any(l => l.StartsWith("Members (since 0.0.92)") && l.Contains("/recruits join")) && FactionConfig.InstructionText.Any(l => l.StartsWith("RaceAndGroupMembership"))
                && ModCommands.RootHelp.Contains("join"),
                "lang for the join menu (the reminder names /recruits join, the confirmation says it's permanent); #Instructions and /help recruits explain it");
            Check(!fs92.DescribeFactions().Contains("friendly races") && fs92.DescribeFactions().Contains("wickeddwarves: aggressive, not in the join menu"),
                "/recruits faction list leaves out races and groups while they don't count, and marks factions not in the menu");
        }

        // ---- 0.0.93: the join menu's cards (owner chose the grouped cards, "A"; "followers" and "total") and promotion
        // prices (owner: "the cost of the actual unit minus 3-4 coins", chose "Minus 4 captain, 3 others") ----
        {
            var c93 = FactionConfig.FromFile(null, out _);
            string P93(Dictionary<string, int> p) => string.Join("/", new[] { "warrior", "archer", "captain" }.Select(r => p[r]));
            Check(c93.Version == FactionConfig.CurrentVersion && P93(c93.Factions["gondor"].PromotePrices) == "12/12/36" && P93(c93.Factions["shire"].PromotePrices) == "12/12/36"
                && P93(c93.Factions["isengard"].PromotePrices) == "12/13/37" && P93(c93.Factions["dunlendings"].PromotePrices) == "9/8/31"
                && P93(c93.Factions["mordor"].PromotePrices) == "9/9/25" && P93(c93.Factions["gundabad"].PromotePrices) == "9/9/25" && P93(c93.Factions["mistymountains"].PromotePrices) == "6/7/21"
                && P93(c93.Factions["elves"].PromotePrices) == "16/17/46" && P93(c93.Factions["dwarves"].PromotePrices) == "16/16/46",
                "promotion costs the hire price of the new rank less 3 coins, less 4 for a captain (warrior/archer/captain, 0.0.108 prices: Men 12/12/36, Uruks 12/13/37, Dunlendings 9/8/31, orcs 9/9/25, goblins 6/7/21, elves 16/17/46, dwarves 16/16/46)");
            Check(c93.Factions.All(kv => new[] { "warrior", "archer", "captain" }.All(r => kv.Value.PromotePrices[r] == kv.Value.HirePrices[r] - (r == "captain" ? 4 : 3)))
                && FactionTiers.PromotePrice("warrior", 2) == 0 && FactionTiers.PromotePrice("captain", 3) == 0 && FactionTiers.PromotePrice("archer", 0) == 0,
                "every faction's promotion price is its own hire price less the discount, never below 0");
            // Version 21 to 22: prices as 0.0.90 to 0.0.92 shipped move; the admin's stay; changed hire prices count.
            var o21 = Newtonsoft.Json.Linq.JObject.FromObject(c93); o21["Version"] = 21;
            foreach (var f in ((Newtonsoft.Json.Linq.JObject)o21["Factions"]).Properties())
                f.Value["PromotePrices"] = Newtonsoft.Json.Linq.JObject.FromObject(FactionTiers.Of(f.Name).Promote090);
            o21["Factions"]["elves"]["PromotePrices"]["captain"] = 99;
            o21["Factions"]["rohan"]["HirePrices"]["warrior"] = 20;
            o21["Factions"]["dale"]["PromotePrices"] = Newtonsoft.Json.Linq.JObject.Parse(@"{ ""warrior"": 1, ""archer"": 10, ""captain"": 30 }");
            var m21 = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(o21.ToString()), out bool store21);
            Check(store21 && m21.Version == FactionConfig.CurrentVersion && P93(m21.Factions["gondor"].PromotePrices) == "12/12/36" && P93(m21.Factions["mordor"].PromotePrices) == "9/9/25"
                && P93(m21.Factions["mistymountains"].PromotePrices) == "6/7/21" && P93(m21.Factions["elves"].PromotePrices) == "13/13/99"
                && P93(m21.Factions["rohan"].PromotePrices) == "17/12/36" && P93(m21.Factions["dale"].PromotePrices) == "1/10/30",
                "a version 21 file: promotion prices as shipped become hire price less the discount (an admin's hire price counts); promotion prices the admin changed stay");
            var o21x = Newtonsoft.Json.Linq.JObject.FromObject(c93); o21x["Version"] = 21;
            var m21x = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(o21x.ToString()), out _);
            Check(P93(m21x.Factions["gondor"].PromotePrices) == "12/12/36" && FactionConfig.InstructionText.Any(l => l.StartsWith("PromotePrices") && l.Contains("less 3 coins") && l.Contains("less 4 for a captain")),
                "#Instructions explain the promotion default");
            // The menu's sections and portraits.
            var opts93 = JoinSystem.Options(c93);
            string[] free93 = { "shire", "gondor", "rohan", "arnor", "dale", "dwarves", "elves" };
            // 0.0.95 (owner: "move wicked dwarves to the shadow faction").
            string[] shadow93 = { "mordor", "angmar", "mistymountains", "gundabad", "isengard", "haradrim", "easterlings", "blacknumenoreans", "wickeddwarves" };
            Check(opts93.Count == 18 && opts93.Where(o => o.Bloc == "free").Select(o => o.Faction).SequenceEqual(free93)
                && opts93.Where(o => o.Bloc == "shadow").Select(o => o.Faction).SequenceEqual(shadow93)
                && opts93.Where(o => o.Bloc == "other").Select(o => o.Faction).SequenceEqual(new[] { "dunlendings", "tormentedelves" })
                && JoinSystem.BlocOf("Mordor") == "shadow" && JoinSystem.BlocOf("WickedDwarves") == "shadow" && JoinSystem.BlocOf("myfaction") == "other" && JoinSystem.BlocOf(null) == "other"
                && JoinSystem.Blocs.SequenceEqual(new[] { "free", "shadow", "other" }),
                "the menu's sections: Free Peoples (7), Shadow (9, with Gundabad and, since 0.0.95, the wicked dwarves), Others (Dunlendings, tormented elves; and any faction an admin adds)");
            var types93 = Directory.GetFiles(argv[0], "*.json").Select(f => Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(f)))
                .Where(j => j["attributes"]?["lotrnpcsFaction"] != null)
                .Select(j => ("lotrnpcs:" + (string)j["code"], (string)j["attributes"]["lotrnpcsFaction"], (string)j["attributes"]["lotrnpcsRank"])).ToList();
            var portraits = opts93.ToDictionary(o => o.Faction, o => JoinSystem.PortraitType(o.Faction, types93));
            Check(portraits.Values.All(p => p != null) && portraits["gondor"] == "lotrnpcs:test-gondor-warrior" && portraits["mistymountains"] == "lotrnpcs:test-goblin-warrior"
                && portraits["shire"] == "lotrnpcs:test-hobbit" && portraits["isengard"] == "lotrnpcs:test-uruk-warrior" && JoinSystem.PortraitType("nowhere", types93) == null,
                "every faction in the menu has a portrait: its warrior (the hobbit for the Shire); a faction without NPCs has none");
            var item = GuiDialogJoinFaction.PortraitItem("lotrnpcs:test-gondor-warrior");
            var creature = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(argv[0], "..", "itemtypes", "creature.json")));
            var states = creature["variantgroups"][0]["states"].Select(t => (string)t).ToList();
            Check(item.Domain == "lotrnpcs" && item.Path == "creature-test-gondor-warrior" && portraits.Values.All(p => states.Contains(p.Substring("lotrnpcs:".Length))),
                "each portrait is a creative-tab spawn item that exists (lotrnpcs:creature-<entity>)");
            Check(((string)lang["join-followers"]).Contains("{0}") && ((string)lang["join-total"]).Contains("{0}") && !((string)lang["join-explain"]).Contains("\u2014")
                && ((string)lang["join-legend"]).StartsWith("Followers:") && ((string)lang["join-legend"]).Contains("Total:") && lang["join-row"] == null,
                "the cards say followers and total (owner), the legend explains both, the old roster line is gone");
        }

        // ---- 0.0.94: sight, hiding, weapons and shields ----
        {
            var t = Sight.Toward(new Vintagestory.API.MathTools.Vec3d(0, 5, 0), 3, 4, 1.5);
            var t2 = Sight.Toward(new Vintagestory.API.MathTools.Vec3d(0, 5, 0), 0.3, 0.4, 1.5);
            Check(Math.Abs(t.X - 0.9) < 1e-9 && Math.Abs(t.Z - 1.2) < 1e-9 && t.Y == 5 && Math.Abs(t2.X - 0.3) < 1e-9 && Sight.ArrowAhead == 0.4 && AiTaskFactionSeek.LoseSightMs == 5000,
                "sight: a point some way toward a target, level, never past it; arrows judged 0.4 ahead; 5 s memory of an unseen target");
            Check(AiTaskFactionHide.Unreachable(10, 6, false, 0) && AiTaskFactionHide.Unreachable(10, -4, false, 0) && !AiTaskFactionHide.Unreachable(10, 1, false, 0)
                && AiTaskFactionHide.Unreachable(10, 0, true, 10.5) && !AiTaskFactionHide.Unreachable(8, 0, true, 10) && !AiTaskFactionHide.Unreachable(3, 6, true, 3),
                "hiding: hit from beyond reach by an enemy 3+ blocks up or down, or hit again without closing the gap; not in melee reach");
            var ents = Directory.GetFiles(argv[0], "*.json").Select(f => (Path.GetFileNameWithoutExtension(f), Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(f))))
                .Where(x => x.Item1.StartsWith("test-")).ToList();
            bool hideOk = ents.All(x =>
            {
                var tasks = x.Item2["server"]?["behaviors"]?.FirstOrDefault(b => (string)b["code"] == "taskai")?["aitasks"] as Newtonsoft.Json.Linq.JArray;
                var hide = tasks?.FirstOrDefault(tk => (string)tk["code"] == "lotrnpcs:hide");
                var seek = tasks?.FirstOrDefault(tk => (string)tk["code"] == "lotrnpcs:seekentity");
                var sh = tasks?.FirstOrDefault(tk => (string)tk["code"] == "lotrnpcs:shootattack");
                return hide != null && (float)hide["priority"] > (float)(seek?["priority"] ?? 0) && (sh == null || (float)hide["priority"] > (float)sh["priority"]);
            });
            Check(ents.Count > 70 && hideOk, "every NPC type has the hide task, above seeking and shooting");
            var itemsDir = Path.Combine(argv[0], "..", "itemtypes");
            Newtonsoft.Json.Linq.JObject Item(string n) => Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(itemsDir, "orcweapon-" + n + ".json")));
            double Scale(string n) => (double)Item(n)["tpHandTransform"]["scale"];
            Check(Math.Abs(Scale("dwaxe") - 0.72) < 1e-6 && Math.Abs(Scale("dwbattleaxe") - 0.72) < 1e-6 && Math.Abs(Scale("dwspear") - 0.66) < 1e-6
                && Math.Abs(Scale("dwdagger") - 0.369) < 1e-3 && Math.Abs(Scale("elfspear") - 0.6) < 1e-6 && Math.Abs(Scale("hdspear") - 0.6) < 1e-6,
                "weapon sizes: dwarven axe and battleaxe +20%, boar spear +10%, dwarven dagger at the orc knife's length, elven spear (60 units) and Haradrim spear at the normal scale");
            string Held(string type) => File.ReadAllText(Path.Combine(argv[0], type + ".json"));
            Check(new[] { "recruit", "warrior", "captain" }.All(r => Held("test-haradrim-" + r).Contains("orcweapon-hdspear") && !Held("test-haradrim-" + r).Contains("pike-iron")
                    && Held("test-gondor-" + r).Contains("orcweapon-gondorshield") && Held("test-gondor-" + r).Contains("orcweapon-boromirshield") && Held("test-elf-" + r).Contains("orcweapon-elfshield"))
                && !Held("test-tormentedelf-warrior").Contains("elfshield") && !Held("test-arnor-warrior").Contains("gondorshield") && !Held("test-dale-warrior").Contains("boromirshield"),
                "Haradrim carry the owner's spear instead of the pike; Gondor one of its two shields; elves the elven shield; tormented elves, Arnor and Dale none of them");
            var shapes = Path.Combine(argv[0], "..", "shapes", "item", "orcweapon");
            var tex = Path.Combine(argv[0], "..", "textures", "item", "orcweapon", "atlas");
            Check(new[] { "gondorshield", "boromirshield", "elfshield", "hdspear", "elfspear" }.All(n => File.Exists(Path.Combine(tex, n + ".png")) && Item(n)["shape"] != null),
                "the new models are converted from the owner's Blockbench files at build time, each with its texture");
        }

        // ---- 0.0.95: the captain's sword, the dwarf hammers ----
        {
            var itemsDir = Path.Combine(argv[0], "..", "itemtypes");
            Newtonsoft.Json.Linq.JObject Item(string n) => Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(itemsDir, "orcweapon-" + n + ".json")));
            var tex = Path.Combine(argv[0], "..", "textures", "item", "orcweapon", "atlas");
            var gc95 = HeldWeapon(entityFiles["test-gondor-captain"], "server")["weapons"];
            var sword95 = gc95.Single(w => (string)w["item"] == "lotrnpcs:orcweapon-gdcaptsword");
            var anims95 = entityFiles["test-gondor-captain"]["client"]["animations"].ToDictionary(a => (string)a["code"], a => a);
            Check(Math.Abs((double)Item("gdcaptsword")["tpHandTransform"]["scale"] - 0.459) < 1e-9 && (string)lang["item-orcweapon-gdcaptsword"] == "Captain's sword"
                && (bool)sword95["twoHanded"] && (string)sword95["holdAnimation"] == "lotrnpcs-gdcaptsword-hold" && (string)anims95["lotrnpcs-gdcaptsword-attack"]["animation"] == "poleaxechop"
                && (string)anims95["lotrnpcs-gdcaptsword-hold"]["animation"] == "greatswordhold",
                "the Gondor captain's two-handed \"Captain's sword\" at 0.459 scale (2.0 blocks since 0.0.96, owner: \"very small\"), swung like the battleaxe and held in the two-handed guard (greatswordhold since 0.0.96)");
            var dw95 = HeldWeapon(entityFiles["test-wickeddwarf-recruit"], "server")["weapons"];
            Check((string)lang["item-orcweapon-dwwarhammer"] == "Dwarven warhammer" && (string)lang["item-orcweapon-dwshorthammer"] == "Dwarven shorthammer"
                && Math.Abs((double)Item("dwwarhammer")["tpHandTransform"]["scale"] - 0.6) < 1e-9 && Math.Abs((double)Item("dwshorthammer")["tpHandTransform"]["scale"] - 0.6) < 1e-9
                && (bool)dw95.Single(w => (string)w["item"] == "lotrnpcs:orcweapon-dwwarhammer")["twoHanded"] && (string)dw95.Single(w => (string)w["item"] == "lotrnpcs:orcweapon-dwwarhammer")["holdAnimation"] == "lotrnpcs-dwwarhammer-hold"
                && new[] { "gdcaptsword", "dwwarhammer", "dwshorthammer" }.All(n => File.Exists(Path.Combine(tex, n + ".png")) && Item(n)["shape"] != null),
                "the dwarven warhammer (two-handed) and shorthammer at the normal scale, carried by dwarves and wicked dwarves; all three converted from the owner's files with their textures");
        }

        // ---- 0.0.94: rusty gears (owner: "change the coins to rusty gears for now") ----
        {
            var c94 = FactionConfig.FromFile(null, out _);
            var o22 = Newtonsoft.Json.Linq.JObject.FromObject(c94); o22["Version"] = 22;
            foreach (var f in ((Newtonsoft.Json.Linq.JObject)o22["Factions"]).Properties()) f.Value["HireCoin"] = "coinage:planchet-silver-lg";
            o22["Factions"]["rohan"]["HireCoin"] = "game:ingot-copper";
            var m22 = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(o22.ToString()), out bool store22);
            Check(c94.Version == FactionConfig.CurrentVersion && c94.Factions.Values.All(d => d.HireCoin == "game:gear-rusty") && FactionConfig.DefaultHireCoin == "game:gear-rusty"
                && store22 && m22.Version == FactionConfig.CurrentVersion && m22.Factions.Where(kv => kv.Key != "rohan").All(kv => kv.Value.HireCoin == "game:gear-rusty") && m22.Factions["rohan"].HireCoin == "game:ingot-copper",
                "hiring and promotion are paid in rusty gears; a version 22 file's planchets become rusty gears, a coin the admin chose stays");
        }

        // ---- 0.0.109: the battle log and the hurt-window switch (owner: test tools before the in-game pass) ----
        {
            var c109 = FactionConfig.FromFile(null, out _);
            string j109 = Newtonsoft.Json.JsonConvert.SerializeObject(c109, Newtonsoft.Json.Formatting.Indented);
            Check(c109.NpcHitsIgnoreHurtWindow && j109.Contains("\"NpcHitsIgnoreHurtWindow\": true") && FactionConfig.InstructionText.Any(l => l.StartsWith("NpcHitsIgnoreHurtWindow") && l.Contains("Players always keep")),
                "0.0.109: NpcHitsIgnoreHurtWindow is in the file, on by default since 0.0.112, and explained in #Instructions");
            var v28 = Newtonsoft.Json.Linq.JObject.FromObject(c109); v28["Version"] = 28; v28["NpcHitsIgnoreHurtWindow"] = false;
            var m28 = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(v28.ToString()), out bool store28);
            v28["Version"] = 29;
            var m29 = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(v28.ToString()), out _);
            Check(store28 && m28.NpcHitsIgnoreHurtWindow && m28.Version == FactionConfig.CurrentVersion && !m29.NpcHitsIgnoreHurtWindow,
                "0.0.112 (owner: \"keep the i-frames off for now\"): a version 28 file's false turns on once; an admin's false in a version 29 file stays");
            var v29 = Newtonsoft.Json.Linq.JObject.FromObject(c109); v29["Version"] = 29;
            v29["Factions"]["shire"]["FriendlyRaces"] = new Newtonsoft.Json.Linq.JArray("lrc:harfoot", "faraces:hobbit", " FARACES:Hobbit2 ");
            var m30 = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(v29.ToString()), out bool store30);
            Check(store30 && m30.Version == 30 && m30.Factions["shire"].FriendlyRaces.SequenceEqual(new[] { "lrc:harfoot" })
                && c109.Factions.Values.All(d => d.FriendlyRaces.All(r => !r.StartsWith("faraces:"))),
                "0.0.114 (owner: \"remove connection with fa races\"): no faction lists a Fourth Age Races race, and a version 29 file drops them once");
            Check(BattleLog.Classify(true, true, true) == BattleLog.Result.Landed && BattleLog.Classify(false, true, true) == BattleLog.Result.LostWindow
                && BattleLog.Classify(false, false, true) == BattleLog.Result.LostOther && BattleLog.Classify(false, true, false) == BattleLog.Result.LostOther,
                "0.0.109: a hit is landed, lost to the hurt window (target alive and in its window), or lost otherwise");
            var log = new BattleLog();
            var lorc = Npc("test-mordororc-recruit"); lorc.EntityId = 9101;
            var man = Npc("test-gondor-recruit"); man.EntityId = 9102;
            var arch = Npc("test-gondor-archer"); arch.EntityId = 9103;
            log.Attack(lorc, man, false, 3, BattleLog.Result.Landed, false, 100);
            bool offIgnored = log.ByFaction.Count == 0;
            string dir = Path.Combine(Path.GetTempPath(), "lotrnpcs-battlelog-test-" + Environment.ProcessId);
            log.Start(dir, 1000);
            log.Attack(lorc, man, false, 3, BattleLog.Result.Landed, false, 1500);
            log.Attack(lorc, man, false, 3, BattleLog.Result.LostWindow, false, 4500);
            log.Attack(arch, lorc, true, 2, BattleLog.Result.LostWindow, false, 4600);
            log.Attack(arch, lorc, true, 2, BattleLog.Result.Landed, true, 7600);
            log.Death(lorc, arch, 7600);
            log.TurnWait(man);
            for (long t = 2000; t <= 9000; t += 1000) log.Tick(man, true, 1, t);
            log.Tick(arch, false, 1, 9000);
            var mordor = log.ByFaction["mordor"]; var gondor = log.ByFaction["gondor"];
            string ltext = log.Describe(10000);
            log.Stop();
            string[] lines = File.ReadAllLines(log.FilePath);
            Check(offIgnored && mordor.Swings == 2 && mordor.SwingsLanded == 1 && mordor.SwingsLostWindow == 1 && mordor.DamageDealt == 3 && mordor.Deaths == 1
                && gondor.Arrows == 2 && gondor.ArrowsLanded == 1 && gondor.ArrowsLostWindow == 1 && gondor.Kills == 1 && gondor.TurnWaits == 1 && gondor.Npcs.Count == 2
                && gondor.IdleSeconds == 3 && ltext.Contains("gondor: 2 NPCs; arrows") && ltext.Contains("mordor: 1 NPCs; melee 1/2 landed (1 lost to hurt window)") && ltext.Contains("arrows 1/2 landed")
                && lines.Length == 9 && lines[0].StartsWith("ms,event,attacker") && lines[1].StartsWith("500,melee,9101,mordor,recruit,9102,gondor,recruit,3,landed") && lines[4].Contains(",kill,") && lines[5].StartsWith("6600,death,9103,gondor,archer,9101,mordor")
                && lines[6].StartsWith("6000,idle,9102,gondor,recruit,,,,-1,none") && lines[8].StartsWith("8000,idle,9102") && gondor.IdleBy["none"] == 3 && ltext.Contains("NPC-seconds (none 100%)"),
                "0.0.109 battle log: counts nothing while off; per faction melee and arrows landed or lost to the hurt window, damage, kills, deaths, turn waits, and seconds a fighting NPC went 5 s without attacking (" + ltext.Replace("\n", " | ") + ")");
            try { Directory.Delete(dir, true); } catch (Exception) { }
        }

        // ---- 0.0.112: body loot ----
        {
            var bc = new FactionConfig(); bc.FillDefaults(); var br = new Random(5);
            int Gears(string rank, bool hired) => bc.RollBodyLoot(rank, hired, br).Where(x => x.code == "game:gear-rusty").Sum(x => x.count);
            var cap = Enumerable.Range(0, 300).Select(_ => Gears("captain", false)).ToList(); var rec = Enumerable.Range(0, 300).Select(_ => Gears("recruit", true)).ToList();
            var war = Enumerable.Range(0, 300).Select(_ => Gears("warrior", false)).ToList(); var rankless = Enumerable.Range(0, 300).Select(_ => Gears(null, false)).ToList();
            Check(cap.Min() == 3 && cap.Max() == 5 && war.Min() == 1 && war.Max() == 2 && rec.Min() == 0 && rec.Max() == 1 && rankless.Max() == 1,
                "0.0.112: bodies hold rusty gears by rank (captain 3-5, warrior 1-2, recruit 0-1, rankless as a recruit), wild and hired alike by default");
            Check(Enumerable.Range(0, 100).All(_ => bc.RollBodyLoot("captain", false, br).All(x => x.count > 0 && (x.code == "game:gear-rusty" || x.code == "game:leather-normal-plain"))),
                "0.0.112: no zero stacks; a captain's body holds only gears and leather (no armor or weapons)");
            bc.HiredBodyLoot["captain"] = new List<BodyLoot>();
            Check(bc.RollBodyLoot("captain", true, br).Count == 0 && bc.RollBodyLoot("captain", false, br).Count > 0, "0.0.112: hired and wild loot are set apart; an empty list leaves an empty body");
            var loaded = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>("{\"Version\": 28, \"WildBodyLoot\": {\"recruit\": [{\"Code\": \"game:bone\", \"Min\": 2, \"Max\": 2}]}}"), out _);
            Check(loaded.RollBodyLoot("captain", false, br).SequenceEqual(new[] { ("game:bone", 2) }) && loaded.HiredBodyLoot.Count == 4 && FactionConfig.InstructionText.Any(t => t.StartsWith("WildBodyLoot")),
                "0.0.112: a file's own loot table is kept (a missing rank uses the recruit list), a missing one gets the defaults, and #Instructions explain it");
        }

        // ---- 0.0.112: spawn-safe radius ----
        Check(new FactionConfig().SpawnSafeRadius == 64 && FactionConfig.InstructionText.Any(t => t.StartsWith("SpawnSafeRadius")), "0.0.112: SpawnSafeRadius defaults to 64 and is explained in #Instructions");
        Check(FactionSystem.WithinRadius(30, 50, 64) && FactionSystem.WithinRadius(64, 0, 64) && !FactionSystem.WithinRadius(50, 50, 64) && !FactionSystem.WithinRadius(0, 0, 0),
            "0.0.112: within the radius across (a circle, edge included); 0 turns it off");

        // ---- 0.0.113: guard and shock, promotion-only warrior branches ----
        {
            var b113 = new FactionConfig(); b113.FillDefaults();
            FactionDef g = b113.Factions["gondor"], mo = b113.Factions["mordor"];
            Check(g.Health["guard"] == 43 && g.Health["shock"] == 27 && Math.Abs(g.Damage["guard"] - 3.16f) < 1e-4 && Math.Abs(g.Damage["shock"] - 4.94f) < 1e-4
                && mo.Health["guard"] == 38 && mo.Health["shock"] == 24 && b113.Factions["dwarves"].Health["guard"] == 59,
                "0.0.113: guards have a warrior's health x1.25 and damage x0.8, shock troops the other way round (Men 43/3.16 and 27/4.94)");
            var own = FactionConfig.FromFile(Newtonsoft.Json.JsonConvert.DeserializeObject<FactionConfig>(@"{ ""Version"": 29, ""Factions"": { ""rohan"": { ""Health"": { ""Warrior"": 40 }, ""PromotePrices"": { ""warrior"": 20, ""shock"": 5 } } } }"), out _);
            Check(own.Factions["rohan"].Health["guard"] == 50 && own.Factions["rohan"].Health["shock"] == 32 && own.Factions["rohan"].PromotePrices["guard"] == 20 && own.Factions["rohan"].PromotePrices["shock"] == 5
                && g.PromotePrices["guard"] == g.PromotePrices["warrior"] && !g.HirePrices.ContainsKey("guard") && HiringSystem.PriceFor(g, "guard") == g.HirePrices["warrior"],
                "0.0.113: a file without the branches gets them from its own warrior (health and promotion price); a listed price stays; they can't be hired, and a dismissed one rehires at a warrior's price");
            Check(Formation.OrderOfRank("guard") == Formation.OrderOfRank("warrior") && Formation.OrderOfRank("shock") == Formation.OrderOfRank("warrior")
                && Enumerable.Range(0, 100).All(_ => b113.RollBodyLoot("shock", false, new Random(_)).Where(x => x.code == "game:gear-rusty").Sum(x => x.count) >= 1),
                "0.0.113: in formations and bodies they count as warriors");
            JToken GuardW(string f) => HeldWeapon(entityFiles[$"test-{f}-guard"], "server");
            JToken ShockW(string f) => HeldWeapon(entityFiles[$"test-{f}-shock"], "server");
            Check((bool?)GuardW("gondor")["alwaysShield"] == true && GuardW("gondor")["weapons"].All(w => w["twoHanded"] == null)
                && ShockW("gondor")["shields"] == null && ShockW("mordororc")["weapons"].All(w => (bool?)w["twoHanded"] == true) && ShockW("dwarf")["weapons"].Count() == 2
                && (bool?)GuardW("uruk")["alwaysShield"] == true && (string)GuardW("uruk")["shields"][0] == "game:shield-woodmetal"
                && ShockW("gondor")["weapons"].Count() == 1 && (string)ShockW("gondor")["weapons"][0]["item"] == "game:axe-bardiche-ruined" && (bool?)ShockW("gondor")["weapons"][0]["twoHanded"] == true
                && HeldWeapon(entityFiles["test-uruk-warrior"], "server")["shields"] == null && (string)entityFiles["test-gondor-guard"]["attributes"]["lotrnpcsRank"] == "guard"
                && JToken.DeepEquals(entityFiles["test-gondor-guard"]["attributes"]["lotrnpcsHelmets"], entityFiles["test-gondor-warrior"]["attributes"]["lotrnpcsHelmets"])
                && (string)lang["title-guard"] == "Guard" && (string)lang["title-shock"] == "Shock trooper",
                "0.0.113: guards carry a one-handed weapon and always a shield (a game stand-in where the faction has none, its warriors still without), shock troops a two-handed weapon (the ruined bardiche where the faction has none) and never a shield; both wear and offer the warrior's armor and helmets");

            // The roster screen.
            Check(GuiDialogRoster.Compass(0, -5) == "n" && GuiDialogRoster.Compass(5, 0) == "e" && GuiDialogRoster.Compass(0, 5) == "s" && GuiDialogRoster.Compass(-5, 0) == "w"
                && GuiDialogRoster.Compass(3, -3) == "ne" && GuiDialogRoster.Compass(-3, 3) == "sw", "0.0.113: the roster names the way to a recruit by compass point (north is -Z)");
            var rr = new[] { new RosterRow { Order = "follow", Loaded = true }, new RosterRow { Order = "follow", Borrower = "Ann", Loaded = true }, new RosterRow { Order = "guard" },
                new RosterRow { Rank = "recruit", Xp = 15, Need = 15, Loaded = true }, new RosterRow { Need = 0 } };
            Check(rr.Count(r => GuiDialogRoster.Passes(r, "following")) == 1 && rr.Count(r => GuiDialogRoster.Passes(r, "guarding")) == 1 && rr.Count(r => GuiDialogRoster.Passes(r, "ready")) == 1
                && rr.Count(r => GuiDialogRoster.Passes(r, "away")) == 2 && rr.All(r => GuiDialogRoster.Passes(r, "all")),
                "0.0.113: roster filters: following (not lent out), guarding, ready to promote (XP full), not loaded, all");
            var sumOwner = new HiringOwner { Recruits = new List<long> { 7 }, Summaries = new Dictionary<long, RecruitSummary> { [7] = new RecruitSummary { Name = "Hirgon", Rank = "guard", X = 10.5, Seen = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc) } } };
            var sumBack = Newtonsoft.Json.JsonConvert.DeserializeObject<HiringOwner>(Newtonsoft.Json.JsonConvert.SerializeObject(sumOwner));
            Check(sumBack.Summaries[7].Name == "Hirgon" && sumBack.Summaries[7].Rank == "guard" && sumBack.Summaries[7].X == 10.5 && sumBack.Summaries[7].Seen == sumOwner.Summaries[7].Seen
                && new[] { "roster-title", "roster-away", "roster-where", "promote-title", "talk-opt-promote-menu", "orders-roster", "compass-nw" }.All(k => lang[k] != null),
                "0.0.113: recruit summaries are saved with the world, and the roster and promotion screens' texts are in the lang file");
        }

        {
            var body = Mob("game:drifter-normal");
            body.WatchedAttributes.SetString(Recruit.OwnerUidAttr, "uidB");
            bool aliveHelp = EntityBehaviorHireable.ShowsOwnerHelp(body, "uidB") && !EntityBehaviorHireable.ShowsOwnerHelp(body, "uidX");
            body.Alive = false;
            Check(aliveHelp && !EntityBehaviorHireable.ShowsOwnerHelp(body, "uidB"), "0.0.117: talk and orders hints show on your living recruit only, not on its body");
        }

        Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
        return fails;
    }
    static T Also<T>(this T t, Action<T> a) { a(t); return t; }
}
