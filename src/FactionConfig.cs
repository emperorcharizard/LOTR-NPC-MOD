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
    public enum FactionRelation { Neutral, Ally, Enemy }

    /// <summary>One item in a body's loot (FactionConfig.WildBodyLoot, HiredBodyLoot).</summary>
    public class BodyLoot
    {
        public string Code;
        public int Min, Max;
    }

    /// <summary>One NPC faction in ModConfig/lotrnpcs-factions.json.</summary>
    public class FactionDef
    {
        // The factions this one fights and the ones it fights beside, each on one line of the file. A faction in
        // neither list is neutral to it. Null until FillDefaults: the shipped sides (FactionConfig.DefaultLines).
        [JsonConverter(typeof(OneLineList))]
        public List<string> Enemies, Allies;
        // "aggressive": attacks players who aren't its friends on sight. "defensive": only fights players who attack
        // it or its allies. Both attack enemy factions' NPCs and monsters on sight.
        public string Stance = "aggressive";
        // Player Model Lib race codes ("domain:model", as in the player's skinModel) whose players are friends.
        public List<string> FriendlyRaces = new List<string>();
        // Vanilla /group names whose members are friends.
        public List<string> FriendlyGroups = new List<string>();
        // What hiring one of its NPCs costs: HirePrices coins of HireCoin per rank. Null until FillDefaults, so a
        // file's prices replace the defaults instead of adding to them; ranks a file leaves out get the default
        // price.
        public string HireCoin;
        public Dictionary<string, int> HirePrices;
        // What promoting a recruit costs, in HireCoin, by the rank it is promoted to. Filled like HirePrices.
        public Dictionary<string, int> PromotePrices;
        // How many of this faction's recruits can follow a player at once and how many a player can have. Each
        // recruit uses 1/MaxFollowers of a player's following and 1/MaxRecruits of their roster, so factions can be
        // mixed. Null until FillDefaults: the faction's tier (FactionTiers), scaled by the file's baseline
        // MaxFollowers and MaxRecruits.
        public int? MaxFollowers, MaxRecruits;
        // false leaves this faction out of the join menu. Admins can still set anyone to it.
        public bool Joinable = true;
        // Health and damage per hit by rank (recruit, warrior, captain, archer) for this faction's
        // NPCs. A rank without an entry (or a rankless NPC) keeps its type's own. Null until FillDefaults: the tier's
        // table.
        public Dictionary<string, float> Health, Damage;
        // Old per-faction multipliers, read once from a version 19 file so an admin's change carries over; never
        // written back.
        [JsonProperty("HealthMultiplier")]
        public float? OldHealthMultiplier;
        [JsonProperty("DamageMultiplier")]
        public float? OldDamageMultiplier;
        public bool ShouldSerializeOldHealthMultiplier() => false;
        public bool ShouldSerializeOldDamageMultiplier() => false;
    }

    /// <summary>Writes a list of strings on one line of the file ("Enemies": ["mordor", "angmar"]), so each faction's
    /// sides read at a glance. Reads any JSON list.</summary>
    public class OneLineList : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(List<string>);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) =>
            reader.TokenType == JsonToken.Null ? null : serializer.Deserialize<List<string>>(reader);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (!(value is List<string> list)) { writer.WriteNull(); return; }
            writer.WriteRawValue("[" + string.Join(", ", list.Select(s => JsonConvert.ToString(s))) + "]");
        }
    }

    /// <summary>A player whose faction an admin set with /recruits faction set, saved with the world.</summary>
    public class AssignedPlayer
    {
        public string Name, Faction;
    }

    /// <summary>
    /// An entry for one pair of factions: "ally", "enemy" or "neutral". It wins over the factions' Allies and Enemies
    /// lines. ShareFriends (allies only): each faction's members are also safe from the other's NPCs and can hire them.
    /// </summary>
    public class FactionRelationDef
    {
        public string A, B, Relation;
        public bool ShareFriends;
    }

    /// <summary>
    /// ModConfig/lotrnpcs-factions.json. Collections start null so the file fully replaces the defaults
    /// (Newtonsoft would otherwise add the file's entries to the default ones).
    /// </summary>
    public class FactionConfig
    {
        // Field order is the order of the keys in the file, so related settings sit together. The how-to text for
        // each one is InstructionText below; the history of each setting is in docs/notes and CHANGELOG.md.

        // Rewritten on every load from InstructionText.
        [JsonProperty("#Instructions")]
        public List<string> Instructions;
        // File format. Migrate() updates older files once and then stores them.
        public const int CurrentVersion = 36;
        public int Version;

        // ---- Factions and relations ----
        public Dictionary<string, FactionDef> Factions;
        // Factions from other mods' faction files (FactionRegistry) that this file has taken in, so one an admin
        // removes stays removed.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> AddedFactions;
        public List<FactionRelationDef> Relations;
        // Players join by the menu; true also counts race (FriendlyRaces) and /group (FriendlyGroups).
        public bool RaceAndGroupMembership = false;
        public List<string> MonsterCodes;
        public int GrudgeSeconds = 30;
        public int CallForHelpRange = 16;
        // How far a wild NPC that starts a fight calls idle wild NPCs of its faction.
        public int JoinFightRange = 32;

        // ---- Recruits ----
        // Baseline roster and following, used by factions without their own.
        public const int DefaultMaxRecruits = 16, DefaultMaxFollowers = FactionTiers.EqualFollowers;
        public int MaxRecruits = DefaultMaxRecruits;
        public int MaxFollowers = DefaultMaxFollowers;
        public const int DefaultMaxCaptains = 1;
        public int MaxCaptains = DefaultMaxCaptains;
        // Extra following places for plain recruits only (0 to 10).
        public const int DefaultRecruitExtraFollowers = 1;
        public int RecruitExtraFollowers = DefaultRecruitExtraFollowers;
        [JsonIgnore]
        public int RecruitExtra => GameMath.Clamp(RecruitExtraFollowers, 0, 10);
        // Old captain limit (one per this many roster places). Only read from files older than version 32, which
        // turns it into MaxCaptains; never written.
        public const int DefaultRosterPerCaptain = 8;
        public int RosterPerCaptain = DefaultRosterPerCaptain;
        public bool ShouldSerializeRosterPerCaptain() => false;
        // Seconds followers told to Retreat start no fights (1 to 60).
        public const double DefaultRetreatSeconds = 10;
        public double RetreatSeconds = DefaultRetreatSeconds;
        public static double ClampRetreatSeconds(double s) => double.IsNaN(s) ? DefaultRetreatSeconds : GameMath.Clamp(s, 1, 60);
        // How far around their post guards patrol and watch for intruders.
        public const int DefaultGuardRadius = 16, MinGuardRadius = 4, MaxGuardRadius = 64;
        public int GuardRadius = DefaultGuardRadius;
        public static int ClampGuardRadius(int radius) => GameMath.Clamp(radius, MinGuardRadius, MaxGuardRadius);
        // Within this many blocks of world spawn no recruit attacks a player on sight (0 = off).
        public int SpawnSafeRadius = 64;

        // ---- Healing and loot ----
        // Out-of-combat healing as a multiple of vanilla creature regeneration (0.01 health a second): 30 heals a
        // recruit in about 1.2 minutes. In a fight, and for RegenDelaySeconds after the last hit, vanilla speed applies.
        public const float DefaultHealthRegenSpeed = 30;
        public float HealthRegenSpeed = DefaultHealthRegenSpeed;
        public int RegenDelaySeconds = 10;
        // By rank, what a dead wild NPC's and a dead recruit's body hold. Rankless NPCs use the recruit list.
        public Dictionary<string, List<BodyLoot>> WildBodyLoot;
        public Dictionary<string, List<BodyLoot>> HiredBodyLoot;

        // ---- Battles ----
        // Every NPC hit on anything but a player is this times its damage, so NPC fights last longer (0 to 10).
        public const float DefaultNpcVsNpcDamageMultiplier = 0.5f;
        public float NpcVsNpcDamageMultiplier = DefaultNpcVsNpcDamageMultiplier;
        /// <summary>The multiplier for a hit on target: NpcVsNpcDamageMultiplier (0 to 10) on non-players, 1 on players.</summary>
        public float HitScaleFor(Entity target) => target is EntityPlayer ? 1f : (float.IsNaN(NpcVsNpcDamageMultiplier) ? DefaultNpcVsNpcDamageMultiplier : GameMath.Clamp(NpcVsNpcDamageMultiplier, 0, 10));
        // Hits from an NPC's rear cone (BackArcDegrees wide) do BackHitMultiplier times the damage.
        public const double DefaultBackHitMultiplier = 1.5, DefaultBackArcDegrees = 120;
        public double BackHitMultiplier = DefaultBackHitMultiplier, BackArcDegrees = DefaultBackArcDegrees;
        [JsonIgnore]
        public double BackScale => double.IsNaN(BackHitMultiplier) ? DefaultBackHitMultiplier : GameMath.Clamp(BackHitMultiplier, 0, 10);
        [JsonIgnore]
        public double BackArc => double.IsNaN(BackArcDegrees) ? DefaultBackArcDegrees : GameMath.Clamp(BackArcDegrees, 0, 360);
        // Each NPC hit deals its damage times a random 1 - DamageSpread to 1 + DamageSpread (0 = off).
        public const double DefaultDamageSpread = 0.2;
        public double DamageSpread = DefaultDamageSpread;
        // NPC hits on anything but a player land even inside the target's 0.5 s hurt window. Players keep the window.
        public bool NpcHitsIgnoreHurtWindow = true;
        // Most NPCs on one NPC at once (0 = no limit; players are never limited) and how far apart fighters keep.
        public int MaxAttackersPerTarget = BattleSpacing.DefaultMaxAttackers;
        public double CombatSpacing = BattleSpacing.DefaultSpacing;
        public double DuelDistanceMin = BattleSpacing.DefaultDuelMin;
        public double DuelDistanceMax = BattleSpacing.DefaultDuelMax;
        // Battle lines for recruits in formation, and for wild NPCs too when WildBattleLines is on.
        public bool BattleLines = true;
        public bool WildBattleLines = false;
        // Defensive recruits hold their line until an enemy is this close.
        public double LineHoldRange = 6;

        // ---- Looks ----
        // Chance a new NPC that may go bare-headed wears its helmet; whether captains always do.
        public double HelmetChance = 0.6;
        public bool CaptainsAlwaysHelmeted = false;
        // Chance a new NPC that may carry a shield gets one (never with a two-handed weapon).
        public double ShieldChance = 0.5;

        // ---- Admin ----
        // Turns on the testing commands (ModCommands.DebugOnly).
        public bool DebugCommands = false;

        public void FillDefaults()
        {
            Factions = Factions ?? DefaultFactions();
            Relations = Relations ?? new List<FactionRelationDef>();
            WildBodyLoot = WildBodyLoot ?? DefaultBodyLoot();
            HiredBodyLoot = HiredBodyLoot ?? DefaultBodyLoot();
            MonsterCodes = MonsterCodes ?? new List<string> { "game:drifter-*", "game:bowtorn-*", "game:shiver-*", "game:bell-*", "game:locust-*" };
            foreach (KeyValuePair<string, FactionDef> kv in Factions)
            {
                FactionDef def = kv.Value;
                if (def == null) continue;
                FactionTier tier = FactionTiers.Of(kv.Key);
                if (def.Enemies == null && def.Allies == null)
                {
                    (List<string> enemies, List<string> allies) = DefaultLines(kv.Key?.Trim().ToLowerInvariant(), Factions.Keys);
                    def.Enemies = enemies; def.Allies = allies;
                }
                def.Enemies = def.Enemies ?? new List<string>();
                def.Allies = def.Allies ?? new List<string>();
                def.HireCoin = string.IsNullOrWhiteSpace(def.HireCoin) ? DefaultHireCoin : def.HireCoin.Trim();
                // Ranks the file leaves out cost the faction's tier price. Rank names compare in lower case; if a
                // file names one twice ("Captain" and "captain"), the first counts.
                Dictionary<string, int> prices = new Dictionary<string, int>();
                foreach (KeyValuePair<string, int> price in def.HirePrices ?? new Dictionary<string, int>())
                {
                    string rank = price.Key?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(rank) && !Promotion.IsRetired(rank) && !prices.ContainsKey(rank)) prices[rank] = price.Value;
                }
                foreach (KeyValuePair<string, int> price in tier.Hire)
                {
                    if (!prices.ContainsKey(price.Key)) prices[price.Key] = price.Value;
                }
                def.HirePrices = prices;
                Dictionary<string, int> promote = new Dictionary<string, int>();
                foreach (KeyValuePair<string, int> price in def.PromotePrices ?? new Dictionary<string, int>())
                {
                    string rank = price.Key?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(rank) && !Promotion.IsRetired(rank) && !promote.ContainsKey(rank)) promote[rank] = price.Value;
                }
                // Ranks the file leaves out cost the faction's own hire price for that rank less the discount.
                foreach (KeyValuePair<string, int> price in FactionTiers.PromoteFor(prices))
                {
                    if (!promote.ContainsKey(price.Key)) promote[price.Key] = price.Value;
                }
                def.PromotePrices = promote;
                FillLimits(def, tier, false);
            }
        }

        // Limits clamped to what makes sense.
        public const int MaxFollowersLimit = 32, MaxRecruitsLimit = 256;
        public const float MaxHealthValue = 10000, MaxDamageValue = 1000;

        /// <summary>A faction's follow limit, roster and per-rank health and damage. Missing values (or all of them,
        /// with replace) come from its tier, the limits scaled by this file's baseline MaxFollowers and MaxRecruits,
        /// so an admin who changed those keeps their proportions. Ranks a table leaves out come from the tier too;
        /// rank names compare in lower case.</summary>
        void FillLimits(FactionDef def, FactionTier tier, bool replace)
        {
            int baseFollowers = Math.Max(1, MaxFollowers), baseRecruits = Math.Max(1, MaxRecruits);
            if (replace || def.MaxFollowers == null) def.MaxFollowers = FactionTiers.Round(tier.Followers * (double)baseFollowers / DefaultMaxFollowers);
            if (replace || def.MaxRecruits == null) def.MaxRecruits = FactionTiers.Round(tier.Roster * (double)baseRecruits / DefaultMaxRecruits);
            def.MaxFollowers = GameMath.Clamp(def.MaxFollowers.Value, 1, MaxFollowersLimit);
            def.MaxRecruits = GameMath.Clamp(def.MaxRecruits.Value, 1, MaxRecruitsLimit);
            def.Health = RankTable(replace ? null : def.Health, tier.Health, v => GameMath.Clamp(FactionTiers.Round(v), 1, MaxHealthValue), true);
            def.Damage = RankTable(replace ? null : def.Damage, tier.Damage, v => GameMath.Clamp((float)Math.Round(v, 3), 0, MaxDamageValue), false);
        }

        static Dictionary<string, float> RankTable(Dictionary<string, float> given, Dictionary<string, float> tier, System.Func<float, float> clean, bool health)
        {
            Dictionary<string, float> table = new Dictionary<string, float>();
            foreach (KeyValuePair<string, float> kv in given ?? new Dictionary<string, float>())
            {
                string rank = kv.Key?.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(rank) && !Promotion.IsRetired(rank) && !table.ContainsKey(rank) && !float.IsNaN(kv.Value)) table[rank] = clean(kv.Value);
            }
            foreach (KeyValuePair<string, float> kv in tier)
            {
                if (!table.ContainsKey(kv.Key)) table[kv.Key] = clean(kv.Value);
            }
            return table;
        }

        // ---- per-faction values; a faction the config doesn't know uses the baseline ----

        public FactionDef Def(string faction) =>
            faction != null && Factions != null && Factions.TryGetValue(faction, out FactionDef def) ? def : null;
        /// <summary>How many of this faction's recruits may follow a player at once.</summary>
        public int FollowersOf(string faction) => Def(faction)?.MaxFollowers ?? Math.Max(1, MaxFollowers);
        /// <summary>How many of this faction's recruits a player may have.</summary>
        public int RosterOf(string faction) => Def(faction)?.MaxRecruits ?? Math.Max(1, MaxRecruits);
        /// <summary>How many captains a player may have (MaxCaptains; int.MaxValue with the limit off).</summary>
        public int CaptainsOf(string faction) => MaxCaptains <= 0 ? int.MaxValue : MaxCaptains;
        public static int CaptainsFor(int roster, int perCaptain) =>
            perCaptain <= 0 ? int.MaxValue : Math.Max(1, (int)Math.Floor(roster / (double)perCaptain + 0.25 + 1e-9));
        /// <summary>The health of this faction's NPCs of this rank, or null: the type's own (a faction the config
        /// doesn't know, a rank without an entry, a rankless NPC).</summary>
        public float? HealthFor(string faction, string rank) => StatFor(Def(faction), Def(faction)?.Health, rank, 0);
        /// <summary>The damage of one hit (before the spread) of this faction's NPCs of this rank, or null: the type's.</summary>
        public float? DamageFor(string faction, string rank) => StatFor(Def(faction), Def(faction)?.Damage, rank, 2);

        /// <summary>A rider's health and damage, unless the config lists a rider entry, are its warrior's times this.
        /// Riders cost a captain's price; at 1.25 a rider beats 2 warriors and loses to a captain (in-game tests;
        /// history in CHANGELOG.md and docs/notes).</summary>
        public const double DefaultRiderScale = 1.25;

        public static float RiderScale(FactionDef def) => (float)DefaultRiderScale;

        static float? StatFor(FactionDef def, Dictionary<string, float> table, string rank, int digits)
        {
            if (rank == null || table == null) return null;
            if (table.TryGetValue(rank, out float v)) return v;
            if (rank == "rider" && table.TryGetValue("warrior", out float w)) return (float)Math.Round(w * RiderScale(def), digits);
            return table.TryGetValue(Promotion.StatRank(rank), out v) ? v : (float?)null;
        }
        /// <summary>The spread on every NPC hit, 0 to 0.9.</summary>
        [JsonIgnore]
        public double Spread => double.IsNaN(DamageSpread) ? DefaultDamageSpread : GameMath.Clamp(DamageSpread, 0, 0.9);
        /// <summary>How much bigger this faction's wild groups are than the baseline's: its follow limit over the
        /// baseline MaxFollowers (1 while every faction has 3).</summary>
        public double GroupScaleOf(string faction) => FollowersOf(faction) / (double)Math.Max(1, MaxFollowers);

        // The coin hiring and promotion are paid in: the game's rusty gear. PlanchetHireCoin (a Coinage large silver
        // planchet) was the default before; older files paying in it move to the gear.
        public const string DefaultHireCoin = "game:gear-rusty";
        public const string PlanchetHireCoin = "coinage:planchet-silver-lg";
        /// <summary>The default body loot: a few rusty gears by rank, plus a piece of cloth (recruits) or leather
        /// (the rest).</summary>
        public static Dictionary<string, List<BodyLoot>> DefaultBodyLoot()
        {
            BodyLoot Gears(int min, int max) => new BodyLoot { Code = "game:gear-rusty", Min = min, Max = max };
            BodyLoot Leather() => new BodyLoot { Code = "game:leather-normal-plain", Min = 0, Max = 1 };
            return new Dictionary<string, List<BodyLoot>>
            {
                ["recruit"] = new List<BodyLoot> { Gears(0, 1), new BodyLoot { Code = "game:cloth-plain", Min = 0, Max = 1 } },
                ["warrior"] = new List<BodyLoot> { Gears(1, 2), Leather() },
                ["archer"] = new List<BodyLoot> { Gears(1, 2), Leather() },
                ["captain"] = new List<BodyLoot> { Gears(3, 5), Leather() },
            };
        }

        /// <summary>What one body holds: each entry's item, Min to Max of it (both included), zeros left out.</summary>
        public List<(string code, int count)> RollBodyLoot(string rank, bool hired, Random rand)
        {
            var table = hired ? HiredBodyLoot : WildBodyLoot;
            var rolled = new List<(string, int)>();
            if (table == null || (!table.TryGetValue(rank ?? "recruit", out var list) && !table.TryGetValue(Promotion.StatRank(rank) ?? "recruit", out list) && !table.TryGetValue("recruit", out list)) || list == null) return rolled;
            foreach (BodyLoot loot in list)
            {
                if (string.IsNullOrEmpty(loot?.Code) || loot.Max <= 0) continue;
                int n = rand.Next(Math.Max(0, Math.Min(loot.Min, loot.Max)), loot.Max + 1);
                if (n > 0) rolled.Add((loot.Code, n));
            }
            return rolled;
        }

        public static Dictionary<string, int> DefaultPromotePrices() => new Dictionary<string, int> { ["warrior"] = 10, ["archer"] = 10, ["captain"] = 30 };
        public static Dictionary<string, int> DefaultHirePrices() => new Dictionary<string, int> { ["recruit"] = 5, ["warrior"] = 15, ["captain"] = 40, ["archer"] = 15 };

        public static readonly List<string> InstructionText = new List<string>
        {
            "LOTR NPCs faction settings. After editing, run /recruits faction reload (no restart needed). This list is rewritten on every start, so don't edit it. The full guide is docs/config-guide.md in the mod's GitHub repo.",
            "--- Each faction (under Factions, by id) ---",
            "Stance: \"aggressive\" attacks every non-member player on sight; \"defensive\" only fights players who hurt it or its allies.",
            "Members: a player belongs to the faction they pick in the join menu (opens on first join; /recruits join reopens it until they pick). The pick is permanent; admins change it with /recruits faction set or clear. Joinable: false hides the faction from the menu.",
            "HireCoin and HirePrices: the coin item (default game:gear-rusty, the rusty gear) and how many it costs to hire each rank (recruit, warrior, captain, archer). 0 = free.",
            "PromotePrices: coins to promote a recruit to each rank. Default: the hire price less 3 coins, or less 4 for a captain (12 to warrior or archer; 36 to captain or rider).",
            "MaxFollowers and MaxRecruits: how many of this faction's recruits may follow a player at once (default 3) and how many they may own (default 16). Each recruit uses 1/MaxFollowers of the following and 1/MaxRecruits of the roster, so a player can mix factions as long as the shares add up to 1. The values at the top of the file are used by factions without their own.",
            "Health and Damage (by rank): health and damage per hit for recruit, warrior, captain and archer. Every faction ships the same numbers: health 22/34/48/35, damage 3.15/3.95/7/3.05 (recruit/warrior/captain/archer). Riders use warrior numbers x1.25. Captains hit other NPCs x1.5 and take x0.5 from them.",
            "FriendlyRaces and FriendlyGroups: only used when RaceAndGroupMembership is true.",
            "--- Between factions ---",
            "Enemies and Allies (in each faction): the factions it fights and the ones it fights beside. A faction in neither line is neutral: they leave each other alone. To change a pair, move the name from one line to the other; one faction's line is enough, and the mod copies the change to the other faction on /recruits faction reload. Allies win: if either lists the other as an ally they are allies. To make allies enemies again, move the name back in both factions.",
            "Default sides: the Free Peoples (gondor, rohan, dale, arnor, dwarves, shire, woodelves) are allies; the Shadow (mordor, angmar, mistymountains, gundabad, isengard, haradrim, easterlings, blacknumenoreans, wickeddwarves) are allies; the two are enemies. The dunlendings are enemies of everyone.",
            "Allies never fight each other and come to help each other. A player is safe from NPCs of their own faction and its allies, but hires only from their own faction.",
            "Relations (optional): entries for one pair, {\"A\": \"gondor\", \"B\": \"dale\", \"Relation\": \"ally\", \"ShareFriends\": true}, which win over the lines.",
            "ShareFriends (allies only, in a Relations entry): true lets each faction's members hire the other's NPCs and keeps them safe from those NPCs.",
            "Hiring: right-click an NPC to talk. Members of its faction (and of allies with ShareFriends) can hire it, paying from hotbar and backpack.",
            "RaceAndGroupMembership (default false): true also makes a player a member by race (FriendlyRaces, e.g. lrc:harfoot) or /group (FriendlyGroups), unless the menu or an admin already set their faction.",
            "MonsterCodes: creatures every faction attacks on sight (* = wildcard).",
            "GrudgeSeconds: how long NPCs keep fighting someone who hurt them or an ally.",
            "CallForHelpRange: how many blocks away allies come to help.",
            "JoinFightRange (32 by default): how far a wild NPC that starts a fight calls idle wild NPCs of its faction to join. 0 = off.",
            "--- Recruits ---",
            "MaxCaptains (default 1): captains a player may have, of any faction. 0 = no limit. Riders don't count.",
            "RecruitExtraFollowers (default 1, 0 to 10): extra following places for plain recruits only, on top of MaxFollowers.",
            "RetreatSeconds (default 10, 1 to 60): after the Retreat order, followers run back and start no fights for this long. The Charge order sends them at the enemy you look at for up to 30 seconds.",
            "GuardRadius (default 16, 4 to 64): how far recruits ordered to Guard here patrol. Aggressive and raiding guards attack any player there who isn't the owner, their group, an ally or on the owner's whitelist (/recruits whitelist add <player>).",
            "SpawnSafeRadius (default 64, 0 = off): within this many blocks of world spawn, recruits never attack a player on sight (they still fight back).",
            "--- Healing and loot ---",
            "HealthRegenSpeed: out-of-combat healing as a multiple of vanilla's (1 = 0.01 health a second; default 30). RegenDelaySeconds: wait after the last hit before healing.",
            "WildBodyLoot and HiredBodyLoot (by rank): what a dead NPC's body holds for a player with a knife, as { Code, Min, Max } lists. Default: a few rusty gears plus cloth or leather. Armor and weapons never drop.",
            "--- Battles ---",
            "NpcVsNpcDamageMultiplier (default 0.5, 0 to 10): scales every NPC hit on anything that isn't a player, so NPC fights last longer.",
            "BackHitMultiplier (default 1.5, 0 to 10) and BackArcDegrees (default 120): hits from behind, within this cone, do this much more damage.",
            "DamageSpread (default 0.2, at most 0.9): each NPC hit deals a random 80% to 120% of its damage at 0.2. 0 = off.",
            "NpcHitsIgnoreHurtWindow (default true): NPC hits on NPCs and creatures land even right after another hit. Players always keep the game's half-second hurt window.",
            "MaxAttackersPerTarget (default 4, 0 = no limit): how many NPCs may fight one NPC at once; the rest find another enemy or wait. Players are never limited.",
            "CombatSpacing (default 1.5, 0.8 to 4): blocks between fighting NPCs of the same side, and between neighbours in a battle line.",
            "DuelDistanceMin and DuelDistanceMax (default 1.0 and 1.6): the distance an NPC keeps from the enemy it fights.",
            "BattleLines (default true): groups (wild NPCs, and followers in any formation but Loose) line up when they meet an enemy group. Defensive recruits hold the line until an enemy is within LineHoldRange blocks.",
            "WildBattleLines (default false): wild NPCs form battle lines too; off, they walk straight in.",
            "--- Looks ---",
            "HelmetChance (default 0.6): chance that a new NPC that may go bare-headed (orcs, Uruk-hai and some others) wears its helmet. CaptainsAlwaysHelmeted: true makes captains always wear theirs. Helmets are only for looks.",
            "ShieldChance (default 0.5): chance that a new NPC that can carry a shield gets one (never with a two-handed weapon). Shields are only for looks.",
            "--- Admin ---",
            "DebugCommands (default false): true turns on the testing commands /recruits battlelog, xp, bench and zone spawnnow.",
            "Version: the file format. Don't change it; the mod updates older files once.",
        };

        /// <summary>Factions that were taken out (the tormented elves). Their NPCs still in a world are removed when
        /// they load, and a file's entry for them is dropped.</summary>
        public static readonly string[] RetiredFactions = { "tormentedelves" };
        public static bool IsRetiredFaction(string faction) => faction != null && Array.IndexOf(RetiredFactions, faction) >= 0;
        /// <summary>Faction ids that were renamed: the elves became the Wood Elves, since other elf factions may
        /// come with their own armor.</summary>
        public static readonly Dictionary<string, string> RenamedFactions = new Dictionary<string, string> { ["elves"] = "woodelves" };
        /// <summary>The current id for a faction id read from a file or save (lower case, renames applied).</summary>
        public static string CurrentId(string faction)
        {
            string id = faction?.Trim().ToLowerInvariant();
            return id != null && RenamedFactions.TryGetValue(id, out string now) ? now : id;
        }

        /// <summary>A faction's default Enemies and Allies lines among these factions: the others on its side
        /// (FactionRegistry: free or shadow) are allies, every other one an enemy. A faction on no side (the
        /// Dunlendings) is an enemy of all.</summary>
        public static (List<string> Enemies, List<string> Allies) DefaultLines(string faction, IEnumerable<string> all)
        {
            string side = FactionRegistry.SideOf(faction);
            bool Ally(string f) => side != "none" && FactionRegistry.SideOf(f) == side;
            List<string> others = all.Select(f => f?.Trim().ToLowerInvariant()).Where(f => !string.IsNullOrEmpty(f) && f != faction).Distinct().ToList();
            return (others.Where(f => !Ally(f)).ToList(), others.Where(Ally).ToList());
        }

        /// <summary>Takes in factions another installed mod defines that this file hasn't seen yet: each gets its
        /// default values and lines, and the factions already here list it as an ally or an enemy by its side. True if
        /// anything was added.</summary>
        public bool AddNewFactions()
        {
            bool changed = false;
            foreach (FactionInfo info in FactionRegistry.All.Where(f => !f.Shipped))
            {
                AddedFactions = AddedFactions ?? new List<string>();
                if (AddedFactions.Contains(info.Id)) continue;
                AddedFactions.Add(info.Id);
                changed = true;
                if (Factions.ContainsKey(info.Id)) continue;
                (List<string> enemies, List<string> allies) = DefaultLines(info.Id, Factions.Keys);
                foreach (KeyValuePair<string, FactionDef> kv in Factions.Where(kv => kv.Value != null))
                {
                    kv.Value.Enemies = kv.Value.Enemies ?? new List<string>();
                    kv.Value.Allies = kv.Value.Allies ?? new List<string>();
                    if (kv.Value.Enemies.Contains(info.Id) || kv.Value.Allies.Contains(info.Id)) continue;
                    (allies.Contains(kv.Key) ? kv.Value.Allies : kv.Value.Enemies).Add(info.Id);
                }
                Factions[info.Id] = new FactionDef { Stance = info.Stance, FriendlyRaces = info.FriendlyRaces.ToList(), FriendlyGroups = new List<string>(), Enemies = enemies, Allies = allies };
            }
            if (changed) FillDefaults();
            return changed;
        }

        /// <summary>The config to use for what LoadModConfig returned (null: no file yet), with defaults filled in
        /// and older formats updated. store: write it back to the file.</summary>
        public static FactionConfig FromFile(FactionConfig loaded, out bool store)
        {
            store = loaded == null;
            FactionConfig config = loaded ?? new FactionConfig { Version = CurrentVersion };
            config.FillDefaults();
            if (config.Migrate()) store = true;
            if (config.AddNewFactions()) store = true;
            if (config.Instructions == null || !config.Instructions.SequenceEqual(InstructionText)) store = true;
            config.Instructions = new List<string>(InstructionText);
            return config;
        }

        /// <summary>Renamed factions take their new key (unless the file already has it) and retired ones go, in the
        /// faction list, the Enemies and Allies lines and the Relations entries.</summary>
        void RenameAndRetire()
        {
            if (Factions != null)
            {
                Dictionary<string, FactionDef> renamed = new Dictionary<string, FactionDef>();
                foreach (KeyValuePair<string, FactionDef> kv in Factions)
                {
                    string id = CurrentId(kv.Key);
                    if (id == null || IsRetiredFaction(id) || renamed.ContainsKey(id)) continue;
                    renamed[id] = kv.Value;
                }
                foreach (FactionDef def in renamed.Values.Where(d => d != null))
                {
                    if (def.Enemies != null) def.Enemies = def.Enemies.Select(CurrentId).Where(f => f != null && !IsRetiredFaction(f)).Distinct().ToList();
                    if (def.Allies != null) def.Allies = def.Allies.Select(CurrentId).Where(f => f != null && !IsRetiredFaction(f)).Distinct().ToList();
                }
                Factions = renamed;
            }
            if (Relations != null)
            {
                foreach (FactionRelationDef r in Relations.Where(r => r != null)) { r.A = CurrentId(r.A); r.B = CurrentId(r.B); }
                Relations = Relations.Where(r => r != null && !IsRetiredFaction(r.A) && !IsRetiredFaction(r.B)).ToList();
            }
        }

        /// <summary>Updates a file from an older version. True if anything was done.</summary>
        public bool Migrate()
        {
            if (Version >= CurrentVersion) return false;
            // Files older than version 19 are no longer converted step by step; they keep their own values and take
            // the steps below.
            // Version 20: a version 19 file's HealthMultiplier and DamageMultiplier become per-rank tables.
            // Multipliers still as shipped take the fitted table; ones the admin changed become the unit's own values
            // times theirs. DamageSpread comes in with its default.
            if (Version == 19)
            {
                foreach (KeyValuePair<string, FactionDef> kv in Factions)
                {
                    if (kv.Value == null) continue;
                    FactionTier tier = FactionTiers.Of(kv.Key);
                    float? h = kv.Value.OldHealthMultiplier, d = kv.Value.OldDamageMultiplier;
                    kv.Value.Health = h == null || Math.Abs(h.Value - tier.Health090) < 1e-4 ? new Dictionary<string, float>(tier.Health)
                        : FactionTiers.BaseHealth.ToDictionary(r => r.Key, r => (float)Math.Max(1, FactionTiers.Round(r.Value * (double)h.Value)));
                    kv.Value.Damage = d == null || Math.Abs(d.Value - tier.Damage090) < 1e-4 ? new Dictionary<string, float>(tier.Damage)
                        : FactionTiers.BaseDamage.ToDictionary(r => r.Key, r => (float)Math.Round(r.Value * (double)d.Value, 3));
                    FillLimits(kv.Value, tier, false);
                }
            }
            // Version 22: promotion prices still as shipped become the faction's hire prices less the discount (so
            // hire prices an admin changed count); promotion prices the admin chose stay.
            // Version 23: factions still paying in the planchet move to the rusty gear; a coin the admin chose stays.
            if (Version < 23 && Version > 0)
            {
                foreach (FactionDef def in Factions.Values)
                {
                    if (def != null && string.Equals(def.HireCoin, PlanchetHireCoin, StringComparison.OrdinalIgnoreCase)) def.HireCoin = DefaultHireCoin;
                }
            }
            if (Version < 22 && Version > 0)
            {
                foreach (KeyValuePair<string, FactionDef> kv in Factions)
                {
                    if (kv.Value == null) continue;
                    FactionTier tier = FactionTiers.Of(kv.Key);
                    if (SamePrices(kv.Value.PromotePrices, tier.Promote090) || SamePrices(kv.Value.PromotePrices, tier.Promote))
                        kv.Value.PromotePrices = (FactionTiers.PromoteFor(kv.Value.HirePrices));
                }
            }
            // Version 28: tables, hire prices and derived promotion prices still exactly as version 27 shipped them
            // take the refitted ones; values the admin chose stay. (Runs before the version 27 step, which lands
            // older unchanged tables on the new ones directly.)
            if (Version < 28 && Version > 0)
            {
                foreach (KeyValuePair<string, FactionDef> kv in Factions)
                {
                    if (kv.Value == null) continue;
                    FactionTier tier = FactionTiers.Of(kv.Key);
                    if (FactionTiers.Tables107.TryGetValue(tier.Name, out var old))
                    {
                        if (SameTable(kv.Value.Health, old.Health)) kv.Value.Health = new Dictionary<string, float>(tier.Health);
                        if (SameTable(kv.Value.Damage, old.Damage)) kv.Value.Damage = new Dictionary<string, float>(tier.Damage);
                    }
                    if (FactionTiers.Hire107.TryGetValue(tier.Name, out var oldHire) && SamePrices(kv.Value.HirePrices, oldHire))
                    {
                        bool derived = SamePrices(kv.Value.PromotePrices, FactionTiers.PromoteFor(oldHire));
                        kv.Value.HirePrices = new Dictionary<string, int>(tier.Hire);
                        if (derived) kv.Value.PromotePrices = (FactionTiers.PromoteFor(kv.Value.HirePrices));
                    }
                }
            }
            // Version 27: health and damage tables still exactly as versions 25-26 shipped them take the refitted
            // ones, and an orc-tier follow limit still at the old 7 (scaled by the file's MaxFollowers) becomes 6;
            // values the admin chose stay. (The version 25 orc move below lands on the new tables too.)
            if (Version < 27 && Version > 0)
            {
                foreach (KeyValuePair<string, FactionDef> kv in Factions)
                {
                    if (kv.Value == null) continue;
                    FactionTier tier = FactionTiers.Of(kv.Key);
                    if (FactionTiers.Tables103.TryGetValue(tier.Name, out var old))
                    {
                        if (SameTable(kv.Value.Health, old.Health)) kv.Value.Health = new Dictionary<string, float>(tier.Health);
                        if (SameTable(kv.Value.Damage, old.Damage)) kv.Value.Damage = new Dictionary<string, float>(tier.Damage);
                    }
                    int baseFollowers = Math.Max(1, MaxFollowers);
                    if (tier == FactionTiers.Orc && kv.Value.MaxFollowers == FactionTiers.Round(FactionTiers.OrcFollowers103 * (double)baseFollowers / DefaultMaxFollowers))
                        kv.Value.MaxFollowers = FactionTiers.Round(tier.Followers * (double)baseFollowers / DefaultMaxFollowers);
                }
            }
            // Version 26: battle lines go on once. RosterPerCaptain comes in with its default.
            if (Version < 26 && Version > 0) BattleLines = true;
            // Version 29: NPC hits on NPCs skip the hurt window, once.
            if (Version < 29 && Version > 0) NpcHitsIgnoreHurtWindow = true;
            // Version 30: Fourth Age Races' races leave every faction's friendly races, once.
            if (Version < 30 && Version > 0)
            {
                foreach (FactionDef def in Factions.Values)
                    def?.FriendlyRaces?.RemoveAll(r => r != null && r.Trim().StartsWith("faraces:", StringComparison.OrdinalIgnoreCase));
            }
            // Version 25: orc-tier factions whose health or damage table is still exactly version 20's take the new
            // one; a table the admin changed stays.
            if (Version < 25 && Version > 0)
            {
                foreach (KeyValuePair<string, FactionDef> kv in Factions)
                {
                    if (kv.Value == null || FactionTiers.Of(kv.Key) != FactionTiers.Orc) continue;
                    if (SameTable(kv.Value.Health, FactionTiers.OrcHealth091)) kv.Value.Health = new Dictionary<string, float>(FactionTiers.Orc.Health);
                    if (SameTable(kv.Value.Damage, FactionTiers.OrcDamage091)) kv.Value.Damage = new Dictionary<string, float>(FactionTiers.Orc.Damage);
                }
            }
            // Version 36: the elves are the Wood Elves (key "woodelves"), and the tormented elves and the guard and
            // shock classes are gone (their table entries are dropped as the file is read).
            if (Version < 36 && Version > 0) RenameAndRetire();
            // Version 35: sides move into each faction's Enemies and Allies lines (filled with the shipped sides by
            // FillDefaults); old pair entries go, except alliances that share friends, which the lines can't say.
            if (Version < 35 && Version > 0 && Relations != null)
                Relations = Relations.Where(r => r != null && r.ShareFriends && r.Relation?.Trim().ToLowerInvariant() == "ally").ToList();
            // Version 34: a captain still at 96 health or 10.5 damage (version 33's) goes back to 48 or 7, so players
            // in equal armor can kill captains.
            if (Version < 34 && Version > 0)
                foreach (FactionDef def in Factions.Values)
                {
                    if (def == null) continue;
                    if (def.Health != null && def.Health.TryGetValue("captain", out float h) && Math.Abs(h - FactionTiers.CaptainHealth153) < 1e-4) def.Health["captain"] = FactionTiers.CaptainHealth;
                    if (def.Damage != null && def.Damage.TryGetValue("captain", out float d) && Math.Abs(d - FactionTiers.CaptainDamage153) < 1e-4) def.Damage["captain"] = FactionTiers.CaptainDamage;
                }
            // Version 33: a captain still at the shipped 48 health or 7 damage takes 96 or 10.5. Values the admin
            // chose stay.
            if (Version < 33 && Version > 0)
                foreach (FactionDef def in Factions.Values)
                {
                    if (def == null) continue;
                    if (def.Health != null && def.Health.TryGetValue("captain", out float h) && Math.Abs(h - FactionTiers.CaptainHealth152) < 1e-4) def.Health["captain"] = FactionTiers.CaptainHealth;
                    if (def.Damage != null && def.Damage.TryGetValue("captain", out float d) && Math.Abs(d - FactionTiers.CaptainDamage152) < 1e-4) def.Damage["captain"] = FactionTiers.CaptainDamage;
                }
            // Version 32: the roster-based captain count becomes MaxCaptains: the default 8 gives 1, 0 (no limit)
            // stays none, another value keeps what it gave a roster of 16.
            if (Version < 32 && Version > 0)
                MaxCaptains = RosterPerCaptain == DefaultRosterPerCaptain ? DefaultMaxCaptains : RosterPerCaptain <= 0 ? 0 : CaptainsFor(MaxRecruits, RosterPerCaptain);
            // Version 31 (equal factions): a baseline follow limit still at the old 5 becomes 3; each faction's
            // follow limit, roster, health and damage tables and hire prices still exactly as shipped before take the
            // equal ones, and promotion prices derived from the old hire prices follow. Values the admin chose stay.
            if (Version < 31 && Version > 0)
            {
                int oldBase = Math.Max(1, MaxFollowers);
                if (MaxFollowers == FactionTiers.OldDefaultMaxFollowers134) MaxFollowers = DefaultMaxFollowers;
                int baseRecruits = Math.Max(1, MaxRecruits);
                foreach (KeyValuePair<string, FactionDef> kv in Factions)
                {
                    if (kv.Value == null) continue;
                    FactionTier tier = FactionTiers.Of(kv.Key);
                    if (!FactionTiers.Tables134.TryGetValue(tier.Name, out var old)) continue;
                    FactionDef def = kv.Value;
                    if (def.MaxFollowers == FactionTiers.Round(old.Followers * (double)oldBase / FactionTiers.OldDefaultMaxFollowers134))
                        def.MaxFollowers = FactionTiers.Round(tier.Followers * (double)Math.Max(1, MaxFollowers) / DefaultMaxFollowers);
                    if (def.MaxRecruits == FactionTiers.Round(old.Roster * (double)baseRecruits / DefaultMaxRecruits))
                        def.MaxRecruits = FactionTiers.Round(tier.Roster * (double)baseRecruits / DefaultMaxRecruits);
                    if (SameTable(def.Health, old.Health)) def.Health = new Dictionary<string, float>(tier.Health);
                    if (SameTable(def.Damage, old.Damage)) def.Damage = new Dictionary<string, float>(tier.Damage);
                    var oldHire = new Dictionary<string, int> { ["recruit"] = old.Hire[0], ["warrior"] = old.Hire[1], ["captain"] = old.Hire[2], ["archer"] = old.Hire[3] };
                    if (SamePrices(def.HirePrices, oldHire))
                    {
                        bool derived = SamePrices(def.PromotePrices, FactionTiers.PromoteFor(oldHire));
                        def.HirePrices = new Dictionary<string, int>(tier.Hire);
                        if (derived) def.PromotePrices = (FactionTiers.PromoteFor(def.HirePrices));
                    }
                }
            }
            // Version 24: the unchanged no-limit 0 for MaxAttackersPerTarget becomes 4.
            if (Version < 24 && Version > 0 && MaxAttackersPerTarget == BattleSpacing.Default085MaxAttackers) MaxAttackersPerTarget = BattleSpacing.DefaultMaxAttackers;
            foreach (FactionDef def in Factions.Values)
            {
                if (def == null) continue;
                def.OldHealthMultiplier = null;
                def.OldDamageMultiplier = null;
            }
            Version = CurrentVersion;
            return true;
        }

        // Both leave out the retired guard and shock ranks, which files from 0.0.113 to 1.0.3 list, so those compare
        // equal to the tables and prices older versions shipped.
        static bool SameTable(Dictionary<string, float> a, Dictionary<string, float> b) =>
            a != null && b != null && NoBranches(a).Count() == NoBranches(b).Count() && NoBranches(b).All(kv => a.TryGetValue(kv.Key, out float v) && Math.Abs(v - kv.Value) < 1e-4);

        static bool SamePrices(Dictionary<string, int> a, Dictionary<string, int> b) =>
            a != null && b != null && NoBranches(a).Count() == NoBranches(b).Count() && NoBranches(a).All(kv => b.TryGetValue(kv.Key, out int v) && v == kv.Value);

        static IEnumerable<KeyValuePair<string, T>> NoBranches<T>(Dictionary<string, T> d) => d.Where(kv => !Promotion.IsRetired(kv.Key));

        public static bool SamePair(FactionRelationDef r, string a, string b)
        {
            string ra = r.A?.Trim().ToLowerInvariant(), rb = r.B?.Trim().ToLowerInvariant();
            return (ra == a && rb == b) || (ra == b && rb == a);
        }

        /// <summary>The factions a new config file starts with: every faction the installed mods define
        /// (FactionRegistry), with its stance and member races.</summary>
        public static Dictionary<string, FactionDef> DefaultFactions() =>
            FactionRegistry.All.ToDictionary(f => f.Id, f => new FactionDef { Stance = f.Stance, FriendlyRaces = f.FriendlyRaces.ToList(), FriendlyGroups = new List<string>() });
    }
}
