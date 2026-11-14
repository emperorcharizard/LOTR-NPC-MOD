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

    /// <summary>0.0.112: one item in a body's loot (FactionConfig.WildBodyLoot, HiredBodyLoot).</summary>
    public class BodyLoot
    {
        public string Code;
        public int Min, Max;
    }

    /// <summary>One NPC faction in ModConfig/lotrnpcs-factions.json.</summary>
    public class FactionDef
    {
        // "aggressive": attacks players who aren't its friends on sight. "defensive": only fights players who attack
        // it or its allies. Both attack enemy factions' NPCs and monsters on sight.
        public string Stance = "aggressive";
        // Player Model Lib race codes ("domain:model", as in the player's skinModel) whose players are friends.
        public List<string> FriendlyRaces = new List<string>();
        // Vanilla /group names whose members are friends.
        public List<string> FriendlyGroups = new List<string>();
        // What hiring one of its NPCs costs (user, 2026-11-02): HirePrices coins of HireCoin per rank. Null until
        // FillDefaults, so a file's prices replace the defaults instead of adding to them; ranks a file leaves out get
        // the default price.
        public string HireCoin;
        public Dictionary<string, int> HirePrices;
        // What promoting a recruit costs, in HireCoin, by the rank it is promoted to (0.0.76, user, 2026-11-04: "at 15
        // xp, they have the option to rank up to warrior or archer for 10 coins, then another 40 xp for warrior to rank
        // up to captain for 30 coins"). Filled like HirePrices.
        public Dictionary<string, int> PromotePrices;
        // 0.0.90 (owner, 2026-11-04: factions "unequally equal", "like orcs would have a lower hp pool with a little
        // less damage but be cheaper to hire and be able to have more follow at once"; numbers and rules in
        // prompts/0.0.90-faction-balance.md): how many of this faction's recruits can follow a player at once and how
        // many a player can have. Each recruit uses 1/MaxFollowers of a player's following and 1/MaxRecruits of their
        // roster, so factions mix (owner: "Follow shares"). Null until FillDefaults: the faction's tier (FactionTiers),
        // scaled by the file's baseline MaxFollowers and MaxRecruits.
        public int? MaxFollowers, MaxRecruits;
        // 0.0.92 (owner, 2026-11-05: players pick their faction from a menu after making a character, chose "A": all
        // factions, with a switch): false leaves this faction out of the pick menu. Admins can still set anyone to it.
        public bool Joinable = true;
        // 0.0.91 (owner, 2026-11-05, after a 5 against 7 warrior fight the Men won losing one: per-rank numbers, "B",
        // "balance it for all factions on an average, not just men"; prompts/0.0.91-rank-balance-despawn.md): the
        // health and damage of each rank (recruit, warrior, captain, archer) of this faction's NPCs, in place of
        // 0.0.90's two multipliers. A rank without an entry (or a rankless NPC) keeps its type's. Null until
        // FillDefaults: the tier's table.
        public Dictionary<string, float> Health, Damage;
        // 0.0.90's multipliers, read once from a version 19 file so an admin's change carries over; never written back.
        [JsonProperty("HealthMultiplier")]
        public float? OldHealthMultiplier;
        [JsonProperty("DamageMultiplier")]
        public float? OldDamageMultiplier;
        public bool ShouldSerializeOldHealthMultiplier() => false;
        public bool ShouldSerializeOldDamageMultiplier() => false;
    }

    /// <summary>One balance tier: follow limit, roster, per-rank health and damage, prices, and (for upgrading
    /// version 19 files) the 0.0.90 multipliers.</summary>
    public sealed class FactionTier
    {
        public string Name;
        public int Followers, Roster;
        public Dictionary<string, float> Health, Damage;
        public float Health090, Damage090;
        public Dictionary<string, int> Hire, Promote;
        // The promotion prices 0.0.90 to 0.0.92 shipped (hire price less a third, about), for the version 22 migration.
        public Dictionary<string, int> Promote090;
    }

    /// <summary>
    /// The balance tiers. 0.0.90 (owner, 2026-11-04; "think of balancing numbers yourself for all factions"): follow
    /// limits, rosters and prices so that a full warband of any tier wins about as often as any other's (owner chose
    /// "B": even per full warband); Uruks are the exception the owner asked for ("slightly more health and damage then
    /// men"), at a cost 1.1 times the Men's (owner chose "a"). Prices are the Men's times 5/followers, so a full warband
    /// costs the same (until 0.0.108, which fits prices for equal coin instead; see Tables107). 0.0.91: health and damage per rank, fitted with the simulator in tools/balance (fitspread.py,
    /// ranks-0.0.91.json) so that at each rank a full band of each tier wins half its fights averaged over every other
    /// tier's full band of that rank (Uruks about 0.55), with every hit varied by DamageSpread (0.2). The flavour holds:
    /// orcs low health and a little less damage, elves hard hitting, dwarves tough. The Shire and factions an admin
    /// adds use the Men's (the baseline).
    /// </summary>
    public static class FactionTiers
    {
        public static readonly string[] Ranks = { "recruit", "warrior", "captain", "archer" };
        static Dictionary<string, float> R(float recruit, float warrior, float captain, float archer) =>
            new Dictionary<string, float> { ["recruit"] = recruit, ["warrior"] = warrior, ["captain"] = captain, ["archer"] = archer };
        /// <summary>The unit types' own health and damage (gen RANK_STATS; archers: health and arrow).</summary>
        public static readonly Dictionary<string, float> BaseHealth = R(22, 34, 48, 34), BaseDamage = R(3, 4, 7, 3);

        static FactionTier T(string name, int followers, int roster, float health090, float damage090, Dictionary<string, float> health, Dictionary<string, float> damage,
            int recruit, int warrior, int captain, int archer, int promoteWarrior, int promoteArcher, int promoteCaptain)
        {
            Dictionary<string, int> hire = new Dictionary<string, int> { ["recruit"] = recruit, ["warrior"] = warrior, ["captain"] = captain, ["archer"] = archer };
            return new FactionTier
            {
                Name = name, Followers = followers, Roster = roster, Health090 = health090, Damage090 = damage090, Health = WithBranches(health, true), Damage = WithBranches(damage, false),
                Hire = hire, Promote = WithBranchPrices(PromoteFor(hire)),
                Promote090 = new Dictionary<string, int> { ["warrior"] = promoteWarrior, ["archer"] = promoteArcher, ["captain"] = promoteCaptain },
            };
        }

        /// <summary>The ranks a promotion leads to.</summary>
        public static readonly string[] PromoteRanks = { "warrior", "archer", "captain" };
        // 0.0.93 (owner, 2026-11-05: "There should be cost (coins) alongside xp to promote a unit. do the cost of the
        // actual unit minus 3-4 coins"; chose "Minus 4 captain, 3 others"): a promotion costs the hire price of the rank
        // it leads to, less 3 coins (less 4 for a captain), never below 0. The XP needed is unchanged.
        public const int PromoteDiscount = 3, CaptainPromoteDiscount = 4;

        /// <summary>What promoting to a rank costs when that rank hires for hirePrice.</summary>
        public static int PromotePrice(string rank, int hirePrice) =>
            Math.Max(0, hirePrice - (rank == "captain" ? CaptainPromoteDiscount : PromoteDiscount));

        /// <summary>Promotion prices from a faction's hire prices (ranks it has no hire price for are left out).</summary>
        public static Dictionary<string, int> PromoteFor(Dictionary<string, int> hire)
        {
            Dictionary<string, int> promote = new Dictionary<string, int>();
            foreach (string rank in PromoteRanks)
            {
                if (hire != null && hire.TryGetValue(rank, out int price)) promote[rank] = PromotePrice(rank, price);
            }
            return promote;
        }

        /// <summary>0.0.113: promoting to a branch (guard, shock) costs what promoting to warrior does, unless listed.</summary>
        public static Dictionary<string, int> WithBranchPrices(Dictionary<string, int> promote)
        {
            foreach (string branch in Promotion.Branches)
            {
                if (!promote.ContainsKey(branch) && promote.TryGetValue("warrior", out int price)) promote[branch] = price;
            }
            return promote;
        }

        /// <summary>0.0.113: a tier's branch stats from its warrior's (Promotion.Branches): guard health x1.25 and damage
        /// x0.8, shock the other way round, so health x damage stays the warrior's (tools/balance/branches.py: 0.48 to 0.53
        /// against plain warriors, coin for coin, in every faction).</summary>
        static Dictionary<string, float> WithBranches(Dictionary<string, float> table, bool health)
        {
            foreach (string branch in Promotion.Branches) table[branch] = Branch(branch, table["warrior"], health);
            return table;
        }

        /// <summary>A branch's health or damage from its warrior's.</summary>
        public static float Branch(string branch, float warrior, bool health)
        {
            double scale = (branch == "guard") == health ? 1.25 : 0.8;
            return health ? Round(warrior * scale) : (float)Math.Round(warrior * scale, 2);
        }

        // 0.0.135 (owner, 2026-11-14: "Equal stats, everyone 3", for PvP of 10 v 10 players with their followers): every
        // tier has the Men's per-rank numbers, 3 followers and a roster of 16; hire prices are the Men's too (the
        // coordinator's call, so equal coin stays fair; easy to undo with Tables134 below). Tiers stay apart for their
        // gear, names and the 0.0.90 history the old config steps compare against.
        static Dictionary<string, float> MenHealth => R(22, 34, 48, 35);
        static Dictionary<string, float> MenDamage => R(3.15f, 3.95f, 7f, 3.05f);
        public const int EqualFollowers = 3, EqualRoster = 16;
        public static readonly FactionTier Men = T("men", EqualFollowers, EqualRoster, 1f, 1f, MenHealth, MenDamage, 5, 15, 40, 15, 10, 10, 30);
        public static readonly FactionTier Uruk = T("uruk", EqualFollowers, EqualRoster, 1.05f, 1.03f, MenHealth, MenDamage, 5, 15, 40, 15, 11, 11, 33);
        public static readonly FactionTier Levy = T("levy", EqualFollowers, EqualRoster, 0.85f, 0.85f, MenHealth, MenDamage, 5, 15, 40, 15, 8, 8, 25);
        // 0.0.103 (owner, 2026-11-09: "Maybe the stats only need to be slightly different instead of vastly"; chose
        // "Narrow orcs to ~0.78 of a Man"): health x damage per rank went from about 0.55 of the Men's to about 0.78,
        // roughly even per coin (orcs hire for about 0.75 of a Man). Config version 25 moves unchanged 0.0.91 tables.
        public static readonly Dictionary<string, float> OrcHealth091 = R(15, 27, 31, 22), OrcDamage091 = R(2.5f, 2.8f, 5.95f, 2.45f);
        public static readonly FactionTier Orc = T("orc", EqualFollowers, EqualRoster, 0.66f, 0.85f, MenHealth, MenDamage, 5, 15, 40, 15, 7, 7, 21);
        public static readonly FactionTier Goblin = T("goblin", EqualFollowers, EqualRoster, 0.57f, 0.73f, MenHealth, MenDamage, 5, 15, 40, 15, 6, 6, 19);
        public static readonly FactionTier Elf = T("elf", EqualFollowers, EqualRoster, 1.14f, 1.35f, MenHealth, MenDamage, 5, 15, 40, 15, 13, 13, 38);
        public static readonly FactionTier Dwarf = T("dwarf", EqualFollowers, EqualRoster, 1.33f, 1.15f, MenHealth, MenDamage, 5, 15, 40, 15, 13, 13, 38);

        /// <summary>0.0.130 to 0.0.134 as shipped, for config version 31: tables, hire prices, follow limit and roster
        /// (at the old baseline of 5 followers) that are still exactly these move to the 0.0.135 ones.</summary>
        public static readonly Dictionary<string, (Dictionary<string, float> Health, Dictionary<string, float> Damage, int[] Hire, int Followers, int Roster)> Tables134 =
            new Dictionary<string, (Dictionary<string, float>, Dictionary<string, float>, int[], int, int)>
        {
            ["men"] = (R(22, 34, 48, 35), R(3.15f, 3.95f, 7f, 3.05f), new[] { 5, 15, 40, 15 }, 5, 16),
            ["uruk"] = (R(23, 32, 48, 36), R(3f, 4.3f, 7.1f, 3f), new[] { 4, 15, 41, 16 }, 5, 15),
            ["levy"] = (R(18, 28, 41, 28), R(2.85f, 3.6f, 6.1f, 2.8f), new[] { 4, 12, 35, 11 }, 6, 19),
            ["orc"] = (R(19, 30, 40, 30), R(2.7f, 3.35f, 6.2f, 2.65f), new[] { 5, 12, 29, 12 }, 6, 22),
            ["goblin"] = (R(17, 22, 32, 22), R(1.85f, 2.8f, 4.7f, 2.2f), new[] { 3, 9, 25, 10 }, 8, 26),
            ["elf"] = (R(27, 37, 51, 36), R(3.7f, 5.3f, 9.7f, 4.3f), new[] { 6, 19, 50, 20 }, 4, 13),
            ["dwarf"] = (R(31, 47, 63, 47), R(3.2f, 4.15f, 7.8f, 3.3f), new[] { 6, 19, 50, 19 }, 4, 13),
        };
        public const int OldDefaultMaxFollowers134 = 5;

        // 0.0.107 (owner chose "Orcs to 6 followers + refit all"): orcs follow 6 (was 7), and every tier's per-rank
        // health and damage refitted with tools/balance/fit105.py under the 0.0.105 rules (hit turns, the attacker cap of
        // 4, the hit spread) so each full same-rank following averages about 0.5 against the others (Uruk-hai 0.55),
        // pulled toward the 0.0.103 values. The 0.0.103 tables, for config version 27:
        public static readonly Dictionary<string, (Dictionary<string, float> Health, Dictionary<string, float> Damage)> Tables103 = new Dictionary<string, (Dictionary<string, float>, Dictionary<string, float>)>
        {
            ["men"] = (R(22, 34, 48, 34), R(3.05f, 4f, 7.05f, 3f)),
            ["uruk"] = (R(23, 32, 48, 35), R(2.95f, 4.3f, 7.15f, 2.95f)),
            ["levy"] = (R(18, 29, 41, 28), R(2.7f, 3.4f, 6f, 2.6f)),
            ["orc"] = (R(19, 30, 41, 29), R(2.75f, 3.6f, 6.3f, 2.7f)),
            ["goblin"] = (R(16, 21, 31, 19), R(1.85f, 2.85f, 4.75f, 2.2f)),
            ["elf"] = (R(27, 37, 53, 37), R(3.65f, 5.4f, 9.6f, 4.25f)),
            ["dwarf"] = (R(30, 46, 63, 46), R(3.25f, 4.35f, 7.9f, 3.4f)),
        };
        public const int OrcFollowers103 = 7;

        // 0.0.108 (owner, 2026-11-09: "increase attack frequency up a tick or two. and balance combat for equal coin
        // spendage"): melee swings 0.25 s faster, every tier refitted for that pace (tools/balance/fit105.py), and hire
        // each rank's price fitted (tools/balance/fitmix.py) so equal coin spent on any two factions wins about half
        // the time however it is spent (mixed buys with or without captains: 0.42 to 0.56 on budgets of 50 to 250;
        // recruit-only buys still favour the big orc and goblin rosters). The 0.0.107 tables and hire prices, for config
        // version 28:
        public static readonly Dictionary<string, (Dictionary<string, float> Health, Dictionary<string, float> Damage)> Tables107 = new Dictionary<string, (Dictionary<string, float>, Dictionary<string, float>)>
        {
            ["men"] = (R(22, 34, 48, 35), R(3.1f, 4f, 7f, 3.05f)),
            ["uruk"] = (R(23, 32, 48, 36), R(2.95f, 4.35f, 7.1f, 3f)),
            ["levy"] = (R(18, 29, 42, 28), R(2.9f, 3.6f, 6.1f, 2.8f)),
            ["orc"] = (R(19, 30, 41, 30), R(2.75f, 3.5f, 6.3f, 2.6f)),
            ["goblin"] = (R(18, 24, 34, 22), R(1.85f, 2.8f, 4.85f, 2.2f)),
            ["elf"] = (R(26, 36, 50, 36), R(3.65f, 5.35f, 9.6f, 4.25f)),
            ["dwarf"] = (R(30, 46, 61, 47), R(3.15f, 4.15f, 7.8f, 3.3f)),
        };
        static Dictionary<string, int> H(int recruit, int warrior, int captain, int archer) =>
            new Dictionary<string, int> { ["recruit"] = recruit, ["warrior"] = warrior, ["captain"] = captain, ["archer"] = archer };
        public static readonly Dictionary<string, Dictionary<string, int>> Hire107 = new Dictionary<string, Dictionary<string, int>>
        {
            ["men"] = H(5, 15, 40, 15), ["uruk"] = H(6, 17, 44, 17), ["levy"] = H(4, 13, 33, 13), ["orc"] = H(4, 11, 29, 11),
            ["goblin"] = H(3, 9, 25, 9), ["elf"] = H(6, 19, 50, 19), ["dwarf"] = H(6, 19, 50, 19),
        };

        static readonly Dictionary<string, FactionTier> ByFaction = new Dictionary<string, FactionTier>
        {
            ["gondor"] = Men, ["rohan"] = Men, ["dale"] = Men, ["arnor"] = Men, ["haradrim"] = Men, ["easterlings"] = Men,
            ["blacknumenoreans"] = Men, ["shire"] = Men,
            ["isengard"] = Uruk,
            ["dunlendings"] = Levy,
            ["mordor"] = Orc, ["angmar"] = Orc, ["gundabad"] = Orc,
            ["mistymountains"] = Goblin,
            ["elves"] = Elf, ["tormentedelves"] = Elf,
            ["dwarves"] = Dwarf, ["wickeddwarves"] = Dwarf,
        };

        /// <summary>The tier of a faction id (any case); the Men's for ids the mod doesn't ship.</summary>
        public static FactionTier Of(string faction) =>
            faction != null && ByFaction.TryGetValue(faction.Trim().ToLowerInvariant(), out FactionTier tier) ? tier : Men;

        /// <summary>Rounds half up, as the tables do (22 x 0.66 = 14.52 -> 15, 12.5 -> 13).</summary>
        public static int Round(double value) => (int)Math.Floor(value + 0.5 + 1e-9);

        /// <summary>An NPC's health under a multiplier: whole, at least 1 (it shows on the hover).</summary>
        public static float ScaledHealth(float typeHealth, float multiplier) =>
            typeHealth <= 0 ? typeHealth : Math.Max(1, Round(typeHealth * (double)multiplier));

        /// <summary>One hit's damage with the spread (0.0.91, owner: "add a +-20% spread on hits"): times a random
        /// 1 - spread to 1 + spread. roll is 0 to 1.</summary>
        public static float Spread(float damage, double spread, double roll) =>
            spread <= 0 ? damage : (float)(damage * (1 + spread * (2 * roll - 1)));
    }

    /// <summary>A player whose faction an admin set with /recruits faction set, saved with the world.</summary>
    public class AssignedPlayer
    {
        public string Name, Faction;
    }

    /// <summary>
    /// How two factions treat each other: "ally", "enemy" or "neutral". Factions without an entry are enemies
    /// (user, 2026-11-02: factions are only friendly when the config says so). ShareFriends (allies only): each
    /// faction's friends are also safe from the other's NPCs. Without it a player is safe only from the factions
    /// that list their race or group.
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
        // How to edit the file (user, 2026-11-02: "add #instructions to the config"). Rewritten on every load.
        [JsonProperty("#Instructions")]
        public List<string> Instructions;
        // Format of the file. 0 = written by 0.0.31, 1 = by 0.0.32, 2 = by 0.0.33, 3 = by 0.0.34, 4 = by 0.0.35 to
        // 0.0.38, 5 = by 0.0.39 and 0.0.40, 6 = by 0.0.41 to 0.0.43, 7 = by 0.0.44 to 0.0.47, 8 = by 0.0.48 to 0.0.50, 9 = by 0.0.51 and 0.0.52, 10 = by 0.0.53, 11 = by 0.0.54, 12 = by 0.0.55 to 0.0.58, 13 = by 0.0.59 to 0.0.61, 14 = by 0.0.62 to 0.0.73, 15 = by 0.0.74 and 0.0.75, 16 = by 0.0.76 to 0.0.81, 17 = by 0.0.82 to 0.0.84, 18 = by 0.0.85 to 0.0.89, 19 = by 0.0.90, 20 = by 0.0.91, 21 = by 0.0.92, 22 = by 0.0.93, 23 = by 0.0.94 to 0.0.101, 24 = by 0.0.102, 25 = by 0.0.103, 26 = by 0.0.104 to 0.0.106, 27 = by 0.0.107, 28 = by 0.0.108 to 0.0.111, 29 = by 0.0.112;
        // Migrate() updates older files once and then stores them.
        public const int CurrentVersion = 32;
        public int Version;
        public Dictionary<string, FactionDef> Factions;
        public List<FactionRelationDef> Relations;
        public List<string> MonsterCodes;
        public int GrudgeSeconds = 30;
        public int CallForHelpRange = 16;
        // 0.0.124: how far a wild NPC that starts a fight calls idle NPCs of its faction (0.0.123 used CallForHelpRange,
        // 16, and 11 of 38 orcs that had wandered off while the armies spawned still never joined).
        public int JoinFightRange = 32;
        // Most NPCs one player may have hired at once, and how many of them may follow at a time (user, 2026-11-03:
        // hire more at a time, but only 5 may follow at once; hire cap 15, was 5 for both).
        // 0.0.51 (user, 2026-11-03): the hire cap is 16.
        public const int DefaultMaxRecruits = 16, DefaultMaxFollowers = FactionTiers.EqualFollowers; // 0.0.135: 3 (was 5)
        public int MaxRecruits = DefaultMaxRecruits;
        public int MaxFollowers = DefaultMaxFollowers;
        // 0.0.104 (owner, 2026-11-09: "limit the ranks of units per cap. Like 1-2 captains only"; chose "1 captain per 8
        // roster places"): one captain per RosterPerCaptain places of a faction's roster, rounded so that 13 places
        // give 1, 16 give 2 and 22 give 3 (CaptainsOf). 0 turns the limit off.
        public const int DefaultRosterPerCaptain = 8;
        // 0.0.138 (owner, 2026-11-14: "there should still be the 1 captain"): the captain limit is MaxCaptains per player
        // (0 = none). RosterPerCaptain is only read from older files (version 32 turns it into MaxCaptains) and no
        // longer written.
        public int RosterPerCaptain = DefaultRosterPerCaptain;
        public bool ShouldSerializeRosterPerCaptain() => false;
        public const int DefaultMaxCaptains = 1;
        public int MaxCaptains = DefaultMaxCaptains;
        // 0.0.138 (owner: "for recruits, maybe they can have an additional follower or two"): followers past the 3
        // ranked places, for recruits only (0 to 10).
        public const int DefaultRecruitExtraFollowers = 2;
        public int RecruitExtraFollowers = DefaultRecruitExtraFollowers;
        public int RecruitExtra => GameMath.Clamp(RecruitExtraFollowers, 0, 10);
        // Out-of-combat healing (user, 2026-11-03: "increase the regen of the npcs", then "just a bit faster"), as a
        // multiple of vanilla's creature regeneration of 0.01 health a second: 30 heals a recruit (22 health since
        // 0.0.43) in about 1.2 minutes and a captain (48) in about 2.7. In a fight, and for RegenDelaySeconds after the
        // last hit, vanilla speed applies.
        public const float DefaultHealthRegenSpeed = 30;
        public float HealthRegenSpeed = DefaultHealthRegenSpeed;
        public int RegenDelaySeconds = 10;
        // Guard here (design doc, 2026-10-30: "16 blocks by default, set in the config"): how far around their post
        // guarding recruits patrol and attack intruders. Kept between MinGuardRadius and MaxGuardRadius.
        public const int DefaultGuardRadius = 16, MinGuardRadius = 4, MaxGuardRadius = 64;
        public int GuardRadius = DefaultGuardRadius;

        public static int ClampGuardRadius(int radius) => GameMath.Clamp(radius, MinGuardRadius, MaxGuardRadius);
        // 0.0.112 (owner, 2026-11-09 review Q8: "no-guard radius around world spawn: guards never attack players on
        // sight there, still fight back"): within this many blocks (across) of the world spawn no recruit starts a fight
        // with a player. 0 turns it off.
        public int SpawnSafeRadius = 64;
        // 0.0.112 (owner, review Q10: a drifter-style body to harvest with a knife, rusty gears by rank and a little
        // cloth or leather, no armor or weapons; hired NPCs too, configured apart, same defaults): by rank, what a
        // wild NPC's body and a recruit's body hold. Rankless NPCs use the recruit list.
        public Dictionary<string, List<BodyLoot>> WildBodyLoot;
        public Dictionary<string, List<BodyLoot>> HiredBodyLoot;
        // Battle spacing (0.0.53, user, 2026-11-03: "maybe put a minimum distance npcs have to one another when
        // fighting"): most NPCs that go for one NPC at once (0 = no limit, 4 by default since 0.0.102; players are never limited), and how far
        // apart fighting NPCs keep (BattleSpacing.MinSpacing to MaxSpacing blocks).
        public int MaxAttackersPerTarget = BattleSpacing.DefaultMaxAttackers;
        public double CombatSpacing = BattleSpacing.DefaultSpacing;
        // Helmets (0.0.54, user, 2026-11-03: "randomize whether the npcs are wearing helmets or not"): the chance a new
        // NPC wears its outfit's helmet, and whether captains always do (their helmets mark them out).
        public double HelmetChance = 0.6;
        // 0.0.82 (owner's loadout list decides per unit which may spawn bare-headed): false by default; version 17 turns
        // an old true off once.
        public bool CaptainsAlwaysHelmeted = false;
        // 0.0.82 battle lines and duel band (owner's combat answers, 2026-11-04; Battle.cs, BattleLines.cs).
        // 0.0.85 (owner, 2026-11-04: "the goal is to have the npcs fight first, then maintain formation second"): off
        // by default, since a line keeps NPCs in their places until an enemy comes near. Version 18 turns an unchanged
        // true off once. 0.0.104 (owner, 2026-11-09: lines "stay in that line until they outnumber the enemy where they
        // start to surround them"; chose "Yes, and turn lines on by default"): on by default again; version 26 turns an
        // old false on once.
        public bool BattleLines = true;
        // 0.0.122 (owner, 2026-11-13: smooth, low-lag combat, no long marching lines): wild NPCs don't form lines.
        public bool WildBattleLines = false;
        // 0.0.109 (owner chose "Yes, as a config switch"): NPC hits on anything but a player land even inside the
        // target's 0.5 s hurt window, so crowds and arrows count in full; NPCs then don't wait for a hit turn on such a
        // target. Players keep the window. Off by default (the 0.0.98 staggered swings).
        // 0.0.112 (owner: "keep the i-frames off for now"): on by default; version 29 turns an old false on once.
        public bool NpcHitsIgnoreHurtWindow = true;
        // 0.0.135 (owner, 2026-11-14: "the average fight time should be increased, because it feels like all the units
        // die within 30 seconds to a minute"): every hit this mod's NPCs land on anything but a player is this times its
        // damage (melee, arrows, charge hits after their x2), so NPC fights last about twice as long; players take and
        // deal what they did. A file without it gets 0.5.
        public const float DefaultNpcVsNpcDamageMultiplier = 0.5f;
        // 0.0.137 (owner: "npcs to receive 1.5x damage in the back"): hits on this mod's NPCs from their rear cone
        // (BackArcDegrees wide, behind them) do BackHitMultiplier times the damage, on top of NpcVsNpcDamageMultiplier.
        public const double DefaultBackHitMultiplier = 1.5, DefaultBackArcDegrees = 120;
        public double BackHitMultiplier = DefaultBackHitMultiplier, BackArcDegrees = DefaultBackArcDegrees;
        public double BackScale => double.IsNaN(BackHitMultiplier) ? DefaultBackHitMultiplier : GameMath.Clamp(BackHitMultiplier, 0, 10);
        public double BackArc => double.IsNaN(BackArcDegrees) ? DefaultBackArcDegrees : GameMath.Clamp(BackArcDegrees, 0, 360);
        // 0.0.136: how long followers told to Retreat start no fights (1 to 60 s).
        public const double DefaultRetreatSeconds = 10;
        public double RetreatSeconds = DefaultRetreatSeconds;
        public static double ClampRetreatSeconds(double s) => double.IsNaN(s) ? DefaultRetreatSeconds : GameMath.Clamp(s, 1, 60);
        public float NpcVsNpcDamageMultiplier = DefaultNpcVsNpcDamageMultiplier;
        /// <summary>The multiplier for a hit on target: NpcVsNpcDamageMultiplier (0 to 10) on non-players, 1 on players.</summary>
        public float HitScaleFor(Entity target) => target is EntityPlayer ? 1f : (float.IsNaN(NpcVsNpcDamageMultiplier) ? DefaultNpcVsNpcDamageMultiplier : GameMath.Clamp(NpcVsNpcDamageMultiplier, 0, 10));
        // 0.0.115 (owner: debugging features off before release, a config option turns them back on): the testing
        // commands (ModCommands.DebugOnly). A file without it gets false.
        public bool DebugCommands = false;
        public double DuelDistanceMin = BattleSpacing.DefaultDuelMin;
        public double DuelDistanceMax = BattleSpacing.DefaultDuelMax;
        public double LineHoldRange = 6;
        // Shields (0.0.55, user, 2026-11-03: "Take this shield model for the mordor orcs ... its a toggle-able
        // accessory"): the chance a new NPC that may carry a shield gets one. Never with a two-handed weapon.
        public double ShieldChance = 0.5;
        // 0.0.91 (owner, 2026-11-05: "add a +-20% spread on hits"): every NPC hit (melee and arrows, wild NPCs and
        // recruits) deals its damage times a random 1 - DamageSpread to 1 + DamageSpread, so fights between same-rank
        // bands aren't settled by whole numbers of hits. 0 turns it off.
        public const double DefaultDamageSpread = 0.2;
        public double DamageSpread = DefaultDamageSpread;
        // 0.0.92 (owner, 2026-11-05: "instead of applying the race allegiance, have them select right after making a
        // character"; groups: "A", they stop counting): a player's faction is the one they picked from the join menu
        // or an admin set. true brings back the old membership by race (FriendlyRaces) and /group (FriendlyGroups).
        public bool RaceAndGroupMembership = false;

        public void FillDefaults()
        {
            Factions = Factions ?? DefaultFactions();
            // 0.0.76 (user, 2026-11-04: "No factions should be allies by default"): only the example entry, which
            // allies nobody.
            Relations = Relations ?? new List<FactionRelationDef> { ExampleRelation() };
            WildBodyLoot = WildBodyLoot ?? DefaultBodyLoot();
            HiredBodyLoot = HiredBodyLoot ?? DefaultBodyLoot();
            MonsterCodes = MonsterCodes ?? new List<string> { "game:drifter-*", "game:bowtorn-*", "game:shiver-*", "game:bell-*", "game:locust-*" };
            foreach (KeyValuePair<string, FactionDef> kv in Factions)
            {
                FactionDef def = kv.Value;
                if (def == null) continue;
                FactionTier tier = FactionTiers.Of(kv.Key);
                def.HireCoin = string.IsNullOrWhiteSpace(def.HireCoin) ? DefaultHireCoin : def.HireCoin.Trim();
                // Ranks the file leaves out cost the default: the faction's tier price (0.0.90; the Men's are the old
                // 5/15/40/15). Rank names compare in lower case; if a file names one twice ("Captain" and "captain"),
                // the first counts.
                Dictionary<string, int> prices = new Dictionary<string, int>();
                foreach (KeyValuePair<string, int> price in def.HirePrices ?? new Dictionary<string, int>())
                {
                    string rank = price.Key?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(rank) && !prices.ContainsKey(rank)) prices[rank] = price.Value;
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
                    if (!string.IsNullOrEmpty(rank) && !promote.ContainsKey(rank)) promote[rank] = price.Value;
                }
                // Ranks the file leaves out cost the faction's own hire price for that rank less the discount (0.0.93).
                foreach (KeyValuePair<string, int> price in FactionTiers.PromoteFor(prices))
                {
                    if (!promote.ContainsKey(price.Key)) promote[price.Key] = price.Value;
                }
                def.PromotePrices = FactionTiers.WithBranchPrices(promote);
                FillLimits(def, tier, false);
            }
        }

        // Limits clamped to what makes sense.
        public const int MaxFollowersLimit = 32, MaxRecruitsLimit = 256;
        public const float MaxHealthValue = 10000, MaxDamageValue = 1000;

        /// <summary>A faction's follow limit, roster (0.0.90) and per-rank health and damage (0.0.91). Missing values
        /// (or all of them, with replace) come from its tier, the limits scaled by this file's baseline MaxFollowers and
        /// MaxRecruits (the Men's), so an admin who changed those keeps their proportions. Ranks a table leaves out come
        /// from the tier too; rank names compare in lower case.</summary>
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
                if (!string.IsNullOrEmpty(rank) && !table.ContainsKey(rank) && !float.IsNaN(kv.Value)) table[rank] = clean(kv.Value);
            }
            // 0.0.113: a file from before the branches gets them from its own warrior's value, so an admin's change carries.
            foreach (string branch in Promotion.Branches)
            {
                if (!table.ContainsKey(branch) && table.TryGetValue("warrior", out float warrior)) table[branch] = clean(FactionTiers.Branch(branch, warrior, health));
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
        /// <summary>How many captains a player may lead of this faction alone (0.0.104): roster / RosterPerCaptain,
        /// a fraction of .75 or more rounding up, at least 1; int.MaxValue with the limit off.</summary>
        public int CaptainsOf(string faction) => MaxCaptains <= 0 ? int.MaxValue : MaxCaptains;
        public static int CaptainsFor(int roster, int perCaptain) =>
            perCaptain <= 0 ? int.MaxValue : Math.Max(1, (int)Math.Floor(roster / (double)perCaptain + 0.25 + 1e-9));
        /// <summary>The health of this faction's NPCs of this rank (0.0.91), or null: the type's own (a faction the
        /// config doesn't know, a rank without an entry, a rankless NPC).</summary>
        public float? HealthFor(string faction, string rank) => StatFor(Def(faction), Def(faction)?.Health, rank, 0);
        /// <summary>The damage of one hit (before the spread) of this faction's NPCs of this rank, or null: the type's.</summary>
        public float? DamageFor(string faction, string rank) => StatFor(Def(faction), Def(faction)?.Damage, rank, 2);

        /// <summary>0.0.130 (owner, 2026-11-14: riders "dont seem to be able to hold their own in a fight against a large
        /// group of warriors"): a rider costs a captain's price but fought with a warrior's numbers, about 1/2.5 of its
        /// coin's worth. Unless the config lists a rider entry, its health and damage are the warrior's times RiderScale.
        /// 0.0.135: one scale for every faction, the Men's 2.24 from 0.0.130 (which used (captain price / warrior price)
        /// ^ 0.82 and left orc riders at 2.03 and Dunlending riders at 2.41; docs/balance/matchups-2026-11-14.md).</summary>
        public const double DefaultRiderScale = 2.24;

        public static float RiderScale(FactionDef def) => (float)DefaultRiderScale;

        static float? StatFor(FactionDef def, Dictionary<string, float> table, string rank, int digits)
        {
            if (rank == null || table == null) return null;
            if (table.TryGetValue(rank, out float v)) return v;
            if (rank == "rider" && table.TryGetValue("warrior", out float w)) return (float)Math.Round(w * RiderScale(def), digits);
            return table.TryGetValue(Promotion.StatRank(rank), out v) ? v : (float?)null;
        }
        /// <summary>The spread on every NPC hit (0.0.91), 0 to 0.9.</summary>
        public double Spread => double.IsNaN(DamageSpread) ? DefaultDamageSpread : GameMath.Clamp(DamageSpread, 0, 0.9);
        /// <summary>How much bigger this faction's wild groups are than the baseline's: its follow limit over the
        /// baseline MaxFollowers (orcs 6/5 since 0.0.107).</summary>
        public double GroupScaleOf(string faction) => FollowersOf(faction) / (double)Math.Max(1, MaxFollowers);

        // Hire prices (user, 2026-11-02): 5, 15 and 40 coins for every faction for now; the coin can differ per faction.
        // 0.0.59 (user, 2026-11-03: "Set the hiring coin id mordor uses to be standard for all races"): the large silver
        // planchet the user's Mordor entry uses. The old default, coinage:coin-silver-lg, doesn't exist in their world.
        // 0.0.94 (owner, 2026-11-05: "in the config, change the coins to rusty gears for now", then "just do it for the
        // next build"): the vanilla rusty gear. PlanchetHireCoin was the default from 0.0.59 to 0.0.93; switching back is
        // this one line.
        public const string DefaultHireCoin = "game:gear-rusty";
        public const string PlanchetHireCoin = "coinage:planchet-silver-lg";
        // 0.0.55: archers, a unit of their own without ranks (user), cost a warrior's price.
        /// <summary>The promotion prices 0.0.76 to 0.0.89 shipped for every faction (the version 19 migration compares
        /// with them). Today's defaults come from the hire prices (FactionTiers.PromoteFor).</summary>
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
            "LOTR NPCs: who the NPC factions treat as friends and enemies. After editing, run /recruits faction reload (no restart needed). This #Instructions list is rewritten on every start, so don't edit it.",
            "Factions: one entry per NPC faction, by its id (the names /recruits faction and /recruits zone use).",
            "Stance: \"aggressive\" attacks every player who isn't a member on sight. \"defensive\" only fights players who hurt it or its allies.",
            "Members (since 0.0.92): a player is a member of the faction they picked from the join menu, which opens after they make their character (and on every join until they pick; /recruits join opens it again). The pick is permanent: only an admin changes it, with /recruits faction set, or /recruits faction clear to let them pick again. Joinable (in each faction): false leaves it out of the menu.",
            "RaceAndGroupMembership: false (the default since 0.0.92) means race and /group don't make anyone a member. true brings back the old rule: a player is also a member when their race is in FriendlyRaces (Player Model Lib codes such as lrc:harfoot; /recruits faction check shows a player's) or they are in one of its FriendlyGroups, unless an admin or the menu set their faction (which replaces the race; groups still count).",
            "HireCoin and HirePrices: what hiring one of the faction's NPCs costs, as a number of coins per rank (recruit, warrior, captain) and for archers (archer). HireCoin is the item code of the coin, game:gear-rusty (the game's rusty gear) by default since 0.0.94 (it was coinage:planchet-silver-lg, a large silver planchet). Rankless NPCs cost a recruit's price, and a price of 0 makes hiring free. The shipped prices (since 0.0.108) are fitted so the same coins spent on any two factions buy forces that win about as often as each other.",
            "PromotePrices: what promoting a recruit costs in HireCoin, by the rank it becomes (warrior, guard, shock, archer, captain). By default it is the faction's hire price for that rank less 3 coins, or less 4 for a captain (Men: 12 to warrior or archer, 36 to captain); a rank left out gets that default (guard and shock, which can't be hired: the warrior's promotion price). A hired recruit or warrior earns 1 XP for each NPC of this mod that dies within 10 seconds of its hit: at 15 XP a recruit can become a warrior, guard, shock trooper or archer, at 40 XP a warrior, guard or shock trooper can become a captain. The owner promotes it in its talk menu. 0 makes it free.",
            "Hiring: right-click an NPC to talk. Members of its faction, and members of an allied faction whose alliance has ShareFriends, can hire it, paying from their hotbar and backpack.",
            "MaxFollowers and MaxRecruits (in each faction): how many of the faction's recruits can follow a player at once, and how many a player can have. Each recruit uses a share: 1/MaxFollowers of the following and 1/MaxRecruits of the roster, and a player's shares must add up to no more than 1, so factions can be mixed (3 Gondor men and 2 Mordor orcs use 3/5 + 2/7 of the following). Past the following, a new recruit guards where it was hired and the rest hold or guard. A player over the limits when they join (after an admin lowers them) loses the most recently hired recruits until they fit. The MaxFollowers and MaxRecruits at the top of the file are the baseline (3 and 16 since 0.0.135, every faction alike) and count for a faction without its own.",
            "Health and Damage (in each faction, by rank: recruit, warrior, captain, archer, and since 0.0.113 the promotion-only guard and shock, which a file without them gets as the warrior's health x1.25 and damage x0.8 (guard) or health x0.8 and damage x1.25 (shock)): the health and the damage of one hit of the faction's NPCs of that rank, in place of their unit's own (health is a whole number; an archer's damage is its arrow's, and its shove). Since 0.0.135 every faction ships the same numbers (health 22, 34, 48 and 35, damage 3.15, 3.95, 7 and 3.05 for recruit, warrior, captain and archer), with the same 3 followers, roster of 16 and prices, so fights between factions are decided by numbers and tactics. A rider without its own entry has its warrior's health and damage times 2.24. Damage tiers don't change. Wild groups grow with MaxFollowers over the baseline (1 for every faction now; see the spawning file).",
            "BackHitMultiplier (1.5 by default, since 0.0.137; 0 to 10) and BackArcDegrees (120; 0 to 360): a hit on one of this mod's NPCs that comes from behind it, within a cone BackArcDegrees wide centred on its back, does BackHitMultiplier times the damage (on top of NpcVsNpcDamageMultiplier). Fighting NPCs keep turned toward the nearest enemy within 6 blocks and step backwards (at a walk) instead of turning their back.",
            "RetreatSeconds (10 by default, since 0.0.136; 1 to 60): after the Retreat order (combat page of the orders menu) your followers drop their fights, run back to you and start no fights for this many seconds; then they follow on as before. The Charge order sends them at the enemy you look at (within 48 blocks), out of formation, fighting like Aggressive, until no enemy is within 16 blocks of them for 5 seconds or 30 seconds have passed.",
            "NpcVsNpcDamageMultiplier (0.5 by default, since 0.0.135; 0 to 10): every hit this mod's NPCs land on anything that isn't a player is this times its damage (melee, arrows, and charge hits after their x2), so fights between NPCs last longer. Hits on players, and players' hits, don't change.",
            "DamageSpread: every NPC hit, melee and arrows, deals its damage times a random 1 - DamageSpread to 1 + DamageSpread (0.2 = 80% to 120%), so fights aren't decided by whole numbers of hits. 0 turns it off; at most 0.9.",
            "WildBodyLoot and HiredBodyLoot (by rank: recruit, warrior, captain, archer): what a dead wild NPC's body and a dead recruit's body hold for a player with a knife, as a list of { Code, Min, Max } (item code, and a random count from Min to Max). By default rusty gears (recruit 0-1, warrior and archer 1-2, captain 3-5) and up to one piece of cloth (recruits) or leather (the rest); armor and weapons are never dropped. Rankless NPCs use the recruit list; an empty list leaves an empty body. Bodies rot away after 3 in-game hours.",
            "SpawnSafeRadius (64 by default, 0 = off): within this many blocks of the world spawn, recruits (guards, aggressive and raiding ones alike) never attack a player on sight. They still fight back against a player who attacks them, their owner or their group.",
            "GuardRadius: how many blocks around their post recruits told to Guard here patrol (4 to 64). On the aggressive and raid stances they attack any player there who isn't their owner, the owner's group mate, a member of a faction allied to the owner's, or on the owner's whitelist (/recruits whitelist add <player>), even while the owner is offline.",
            "Relations: how two factions treat each other. Relation is \"ally\", \"enemy\" or \"neutral\", and two factions without an entry are enemies. No factions are allies by default. Allies never fight and come to help each other; neutral factions leave each other's NPCs alone.",
            "ShareFriends (allies only): true makes each faction's members safe from the other's NPCs too, and lets them hire the other's NPCs. Otherwise a player is safe only from their own factions and hires only from them.",
            "MonsterCodes: creatures every faction attacks on sight (* is a wildcard). GrudgeSeconds: how long NPCs fight someone who hurt them or an ally. CallForHelpRange: how many blocks away allies come to help. JoinFightRange (32 by default, since 0.0.124): how far a wild NPC that starts a fight calls idle wild NPCs of its faction to join it; they call in turn, so a spread-out army joins. 0 turns it off.",
            "MaxCaptains (1 by default, since 0.0.138; it replaces RosterPerCaptain): how many captains a player may have, whatever their faction. Hiring or promoting past it is refused; captains a player already has stay. 0 means no limit. Riders don't count.",
            "RecruitExtraFollowers (2 by default, since 0.0.138; 0 to 10): places in a player's following for recruits only, past the MaxFollowers places every rank shares. So a band is up to 3 followers of any rank and 2 more recruits. A following recruit that is promoted while the ranked places are full stays on the roster but guards where it stands until a place frees.",
            "MaxAttackersPerTarget: how many NPCs may fight one NPC at a time. 4 by default; 0 means no limit. With a limit, the rest look for another enemy, wait one step behind in a battle line, or, outside a line, join a full enemy anyway when there is no other within reach. Either way an NPC takes an enemy nobody is fighting before one already being fought. Players are never limited, and an NPC always strikes back at whoever hits it.",
            "CombatSpacing: how far apart, in blocks, fighting NPCs of the same side keep from each other, and the gap between neighbours in a battle line (0.8 to 4, 1.5 by default). Enemies only keep DuelDistanceMin apart. As many attackers as fit round an enemy this far apart (5 by default) fight it; the others take another enemy with room first, or else wait in a second ring one CombatSpacing farther out and step in when a place opens.",
            "DuelDistanceMin and DuelDistanceMax: how far from the center of the NPC it fights an attacker stands (1.0 to 1.6 blocks by default): it closes in when farther, steps back when closer, and stands and fights in between. Players are approached the vanilla way.",
            "BattleLines (true by default): groups of NPCs line up when they meet another faction's NPCs, waiting in their places until an enemy comes near: wild NPCs, and recruits following in any formation but Loose. The front rank has one place per enemy; when the lines meet each front NPC fights the enemy facing it from its place in the line. NPCs past that (the side outnumbers the enemy) lengthen the line at both ends and, when the lines meet, wrap round the enemy's flanks, up to MaxAttackersPerTarget on one enemy. Wild NPCs and aggressive or raid recruits march to meet the enemy; defensive recruits hold their line until an enemy is within LineHoldRange blocks. Loose followers, guards, archers, and fights with players or monsters don't make lines. false lets NPCs fight as they find enemies.",
            "WildBattleLines (false by default since 0.0.122): wild NPCs form battle lines too. Off, they walk straight in, each taking the nearest enemy with room (MaxAttackersPerTarget) and waiting just outside when it is full: smoother and lighter on the server. Recruits following in a formation form lines either way (BattleLines).",
            "NpcHitsIgnoreHurtWindow (true by default since 0.0.112): true lets NPC hits on NPCs and creatures land even within half a second of another hit (the game's hurt window, which otherwise throws such hits away), so every attacker and every arrow counts, and NPCs stop waiting for a turn to hit such a target. Players always keep the hurt window.",
            "HelmetChance: the chance (0 to 1) that a newly spawned NPC that may go bare-headed wears its outfit's helmet. Which units may go bare-headed is set by the mod for each unit (all orcs and Uruk-hai, and the units of other factions marked so); the others always spawn with theirs. A helmet it doesn't wear is kept, and its owner can put it on or take any helmet off from the recruit's menu. Helmets are only for looks: NPC protection comes from rank. CaptainsAlwaysHelmeted: true makes every captain always wear theirs (false by default since 0.0.82).",
            "ShieldChance: the chance (0 to 1) that a newly spawned Mordor orc carries a shield. Shields are only for looks, are never carried with the poleaxe, and the owner can take one off or put it on from the recruit's menu.",
            "HealthRegenSpeed: how fast NPCs heal out of combat, as a multiple of vanilla's creature healing (1 = vanilla, 0.01 health a second; 30 = 0.3 a second). RegenDelaySeconds: how long after its last hit an NPC starts healing at that speed.",
            "DebugCommands (false by default): true turns on the admin commands for testing: /recruits battlelog, xp, bench and zone spawnnow. /recruits faction reload applies a change without a restart.",
            "Version: the file format. Don't change it; the mod updates older files once.",
        };

        // Every pair without an entry is enemies anyway; the entry is there so admins see the format and the switches.
        static FactionRelationDef ExampleRelation() => new FactionRelationDef { A = "gondor", B = "rohan", Relation = "enemy", ShareFriends = false };

        /// <summary>The config to use for what LoadModConfig returned (null: no file yet), with defaults filled in
        /// and older formats updated. store: write it back to the file.</summary>
        public static FactionConfig FromFile(FactionConfig loaded, out bool store)
        {
            store = loaded == null;
            FactionConfig config = loaded ?? new FactionConfig { Version = CurrentVersion };
            config.FillDefaults();
            if (config.Migrate()) store = true;
            if (config.Instructions == null || !config.Instructions.SequenceEqual(InstructionText)) store = true;
            config.Instructions = new List<string>(InstructionText);
            return config;
        }

        /// <summary>Updates a file from an older version. True if anything was done.</summary>
        public bool Migrate()
        {
            if (Version >= CurrentVersion) return false;
            // 0.0.112 (owner: both servers run 0.0.108 or later): files older than version 19 (0.0.90) are no longer
            // converted step by step; they keep their own values and take the steps below.
            // 0.0.91 (version 20, owner: per-rank numbers, "B"): a version 19 file's HealthMultiplier and DamageMultiplier
            // become per-rank tables. Multipliers still as 0.0.90 shipped them take the fitted table; ones the admin
            // changed become the unit's own values times theirs, so their change carries over. DamageSpread comes in
            // with its default.
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
            // 0.0.93 (version 22, owner: promotion costs "the actual unit minus 3-4 coins"): promotion prices still as
            // 0.0.90 to 0.0.92 shipped them become the faction's hire prices less the discount (so hire prices an admin
            // changed count); promotion prices the admin chose stay.
            // 0.0.94 (version 23): factions still paying in the planchet move to the rusty gear; a coin the admin chose stays.
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
                        kv.Value.PromotePrices = FactionTiers.WithBranchPrices(FactionTiers.PromoteFor(kv.Value.HirePrices));
                }
            }
            // 0.0.108 (version 28, owner: "balance combat for equal coin spendage"): tables still exactly as 0.0.107 shipped
            // them take the refitted ones, hire prices still exactly the 0.0.107 tier prices take the new ones, and
            // promotion prices still derived from the old hire prices follow; values the admin chose stay. (Runs before
            // the version 27 step, which lands older unchanged tables on the new ones directly.)
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
                        if (derived) kv.Value.PromotePrices = FactionTiers.WithBranchPrices(FactionTiers.PromoteFor(kv.Value.HirePrices));
                    }
                }
            }
            // 0.0.107 (version 27, owner chose "Orcs to 6 followers + refit all"): health and damage tables still exactly as
            // 0.0.103 to 0.0.106 shipped them take the refitted ones, and an orc-tier follow limit still at the old 7
            // (scaled by the file's MaxFollowers) becomes 6; values the admin chose stay. (The version 25 orc
            // move below lands on the new tables too.)
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
            // 0.0.104 (version 26, owner chose "Yes, and turn lines on by default"): lines go on once. RosterPerCaptain
            // comes in with its default.
            if (Version < 26 && Version > 0) BattleLines = true;
            // 0.0.112 (version 29, owner: "keep the i-frames off for now"): NPC hits on NPCs skip the hurt window, once.
            if (Version < 29 && Version > 0) NpcHitsIgnoreHurtWindow = true;
            // 0.0.114 (version 30, owner: "remove connection with fa races"): Fourth Age Races' races leave every
            // faction's friendly races, once.
            if (Version < 30 && Version > 0)
            {
                foreach (FactionDef def in Factions.Values)
                    def?.FriendlyRaces?.RemoveAll(r => r != null && r.Trim().StartsWith("faraces:", StringComparison.OrdinalIgnoreCase));
            }
            // 0.0.103 (version 25, owner chose "Narrow orcs to ~0.78 of a Man"): orc-tier factions (Mordor, Angmar,
            // Gundabad, and any with the orc tables) whose health or damage table is still exactly 0.0.91's take the new
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
            // 0.0.138 (version 32, owner: "there should still be the 1 captain"): the roster-based captain count becomes
            // MaxCaptains: the default 8 gives 1, 0 (no limit) stays none, another value keeps what it gave a roster of 16.
            if (Version < 32 && Version > 0)
                MaxCaptains = RosterPerCaptain == DefaultRosterPerCaptain ? DefaultMaxCaptains : RosterPerCaptain <= 0 ? 0 : CaptainsFor(MaxRecruits, RosterPerCaptain);
            // 0.0.135 (version 31, owner: "Equal stats, everyone 3"): a baseline follow limit still at the old 5 becomes 3;
            // each faction's follow limit, roster, health and damage tables and hire prices still exactly as 0.0.130 to
            // 0.0.134 shipped them take the equal ones, and promotion prices still derived from the old hire prices follow.
            // Values the admin chose stay.
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
                        if (derived) def.PromotePrices = FactionTiers.WithBranchPrices(FactionTiers.PromoteFor(def.HirePrices));
                    }
                }
            }
            // 0.0.102 (version 24, owner: "Implement it again, do 4 per though"): the unchanged no-limit 0 becomes 4.
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

        // Both leave out the 0.0.113 branches, which FillDefaults adds to every file before Migrate compares it with the
        // tables and prices older versions shipped without them.
        static bool SameTable(Dictionary<string, float> a, Dictionary<string, float> b) =>
            a != null && b != null && NoBranches(a).Count() == NoBranches(b).Count() && NoBranches(b).All(kv => a.TryGetValue(kv.Key, out float v) && Math.Abs(v - kv.Value) < 1e-4);

        static bool SamePrices(Dictionary<string, int> a, Dictionary<string, int> b) =>
            a != null && b != null && NoBranches(a).Count() == NoBranches(b).Count() && NoBranches(a).All(kv => b.TryGetValue(kv.Key, out int v) && v == kv.Value);

        static IEnumerable<KeyValuePair<string, T>> NoBranches<T>(Dictionary<string, T> d) => d.Where(kv => Array.IndexOf(Promotion.Branches, kv.Key) < 0);

        public static bool SamePair(FactionRelationDef r, string a, string b)
        {
            string ra = r.A?.Trim().ToLowerInvariant(), rb = r.B?.Trim().ToLowerInvariant();
            return (ra == a && rb == b) || (ra == b && rb == a);
        }

        // The factions (user, 2026-11-02): Men are Gondor, Rohan, Dale, Arnor, Haradrim, Dunland (wildmen), Black
        // Numenoreans and Easterlings; hobbits are one faction whatever the sub-race; Longbeards and Firebeards are
        // one dwarf faction, wicked dwarves another; Noldor, Sindar and Avari are one elf faction, tormented elves
        // another; orc factions depend on region and players, so orc players join through groups. Gondor, Rohan,
        // Dale and Arnor share one race (Men of the West), so they list none either. Every faction but the Shire
        // attacks non-members on sight, and factions are only allies when the config says so.
        public static Dictionary<string, FactionDef> DefaultFactions()
        {
            FactionDef F(string stance, params string[] races) =>
                new FactionDef { Stance = stance, FriendlyRaces = races.ToList(), FriendlyGroups = new List<string>() };
            return new Dictionary<string, FactionDef>
            {
                ["gondor"] = F("aggressive"),
                ["rohan"] = F("aggressive"),
                ["dale"] = F("aggressive"),
                ["arnor"] = F("aggressive"),
                ["haradrim"] = F("aggressive", "lrc:haradrim"),
                ["dunlendings"] = F("aggressive", "lrc:wildmen"),
                ["blacknumenoreans"] = F("aggressive", "lrc:bnumenorean"),
                ["easterlings"] = F("aggressive", "lrc:easterling"),
                ["dwarves"] = F("aggressive", "lrc:longbeard", "lrc:firebeardsandbroadbeams"),
                ["wickeddwarves"] = F("aggressive", "lrc:wickeddwarf"),
                ["shire"] = F("defensive", "lrc:harfoot", "lrc:stoor", "lrc:fallohide"),
                ["elves"] = F("aggressive", "lrc:noldor", "lrc:sindar", "lrc:avari"),
                ["tormentedelves"] = F("aggressive", "lrc:tormentedelf"),
                ["mordor"] = F("aggressive"),
                ["angmar"] = F("aggressive"),
                ["mistymountains"] = F("aggressive"),
                // 0.0.62, work in progress (user, 2026-11-03: "a gundabad faction for a work in progress"). Like every
                // orc faction, players join by group or /recruits faction set.
                ["gundabad"] = F("aggressive"),
                ["isengard"] = F("aggressive"),
            };
        }
    }

    /// <summary>
    /// What the mod reads from an entity type's JSON, worked out once per type (0.0.44). The AI asks about every
    /// creature near every NPC about once a second, and reading the type's JSON each time cost string copies and LINQ.
    /// Thread-safe: natural spawn checks run on the world generation thread.
    /// </summary>
    /// <summary>
    /// Attribute reads for the AI's hot path. TreeAttribute.GetString passes every value through string.Intern, a
    /// lookup in a global table that took longer than the rest of a targeting check (0.0.44 benchmark), so these
    /// read the attribute directly. The text is the same; only the string object may differ, and nothing here
    /// compares strings by reference except caches checking that a value wasn't replaced.
    /// </summary>
    public static class Attrs
    {
        public static string String(ITreeAttribute tree, string key) => (tree?[key] as StringAttribute)?.value;

        /// <summary>EntityPlayer.PlayerUID without the interning.</summary>
        public static string Uid(EntityPlayer player) => String(player?.WatchedAttributes, "playerUID");
    }

    public sealed class NpcTypeInfo
    {
        /// <summary>The lotrnpcsFaction attribute in lower case, or null for creatures without a faction.</summary>
        public string Faction { get; private set; }
        /// <summary>The lotrnpcsRank attribute as written (recruit, warrior, captain), or null.</summary>
        public string Rank { get; private set; }
        /// <summary>Does the type run the vanilla task AI (animals, traders, NPCs)?</summary>
        public bool HasTaskAi { get; private set; }
        /// <summary>The "maxhealth" of the type's health behavior, or 0.</summary>
        public float MaxHealth { get; private set; }
        /// <summary>May a new NPC of this type spawn bare-headed (lotrnpcsHelmetOptional, 0.0.82)? True when the type
        /// doesn't say, as before.</summary>
        public bool HelmetOptional { get; private set; } = true;
        // FactionSystem.IsMonster's answer under the monster list with this stamp (main thread only).
        internal int MonsterStamp = -1;
        internal bool Monster;

        static readonly ConditionalWeakTable<EntityProperties, NpcTypeInfo> Cache = new ConditionalWeakTable<EntityProperties, NpcTypeInfo>();
        static readonly ConditionalWeakTable<EntityProperties, NpcTypeInfo>.CreateValueCallback ReadType = Read;

        public static NpcTypeInfo Of(EntityProperties type) =>
            type == null ? null : Cache.TryGetValue(type, out NpcTypeInfo info) ? info : Cache.GetValue(type, ReadType);

        public static NpcTypeInfo Of(Entity entity) => Of(entity?.Properties);

        static NpcTypeInfo Read(EntityProperties type)
        {
            NpcTypeInfo info = new NpcTypeInfo
            {
                Faction = type.Attributes?["lotrnpcsFaction"].AsString(null)?.Trim().ToLowerInvariant(),
                Rank = type.Attributes?["lotrnpcsRank"].AsString(null),
                HelmetOptional = type.Attributes?["lotrnpcsHelmetOptional"].AsBool(true) ?? true,
            };
            foreach (JsonObject behavior in type.Server?.BehaviorsAsJsonObj ?? Array.Empty<JsonObject>())
            {
                string code = behavior?["code"].AsString();
                if (code == "taskai") info.HasTaskAi = true;
                else if (code == "health") info.MaxHealth = behavior["maxhealth"].AsFloat(0);
            }
            return info;
        }
    }

    /// <summary>
    /// Who NPC factions fight (design doc, decided 2026-10-30): players by race and /group membership, other
    /// factions by the config's Relations (no entry: enemies), and monsters. Allies never fight and help each other
    /// when attacked.
    /// A player is safe from their own factions, and from those factions' allies only where an admin set
    /// ShareFriends on the alliance (user, 2026-11-02).
    /// The AI tasks ask WantsToAttack and RecruitWantsToAttack; NPCs carry their faction in the lotrnpcsFaction entity
    /// type attribute. Everything here runs on the server's main thread.
    /// </summary>
    public class FactionSystem : ModSystem
    {
        public const string ConfigFile = "lotrnpcs-factions.json";
        public const string AssignedKey = "lotrnpcs:playerfactions";
        /// <summary>How far past their area guards chase something before they give up and go back.</summary>
        public const double GuardLeash = 8;
        // How long a player's side (PlayerSide) is trusted before it is worked out again, so a /group change shows soon.
        const long SideCacheMs = 1000;

        ICoreServerAPI sapi;
        public FactionConfig Config { get; private set; } = new FactionConfig();
        /// <summary>Who is fighting whom, to spread battles out (0.0.53, Battle.cs).</summary>
        public readonly BattleSpacing Spacing = new BattleSpacing();
        /// <summary>The 0.0.109 battle log (BattleLog.cs), off until an admin turns it on.</summary>
        public readonly BattleLog Log = new BattleLog();
        BattleLineSystem lines;
        /// <summary>Battle lines (0.0.82, BattleLines.cs).</summary>
        public BattleLineSystem Lines => lines ??= new BattleLineSystem(this);
        AssetLocation[] monsterCodes = Array.Empty<AssetLocation>();
        // Bumped on every SetConfig, so cached monster answers from an older list are worked out again.
        static int stampCounter;
        int monsterStamp = -1;
        HashSet<string> aggressive = new HashSet<string>();
        // faction -> other faction -> how they treat each other, for every pair the config knows or names; worked out
        // once per config (0.0.44: Relation searched the relations list, copying strings, on every call).
        Dictionary<string, Dictionary<string, FactionRelation>> relations = new Dictionary<string, Dictionary<string, FactionRelation>>();
        // Pairs ("a|b", both orders) whose alliance shares friends.
        HashSet<string> sharedFriends = new HashSet<string>();
        // faction -> itself and its allies, worked out once per config (the AI asks many times a second).
        Dictionary<string, string[]> alliesOf = new Dictionary<string, string[]>();
        // faction -> itself and the allies whose friends it spares (ShareFriends).
        Dictionary<string, string[]> friendsFrom = new Dictionary<string, string[]>();
        // faction -> attacker entity id -> until when (server elapsed ms) its NPCs and their allies fight back.
        readonly Dictionary<string, Dictionary<long, long>> grudges = new Dictionary<string, Dictionary<long, long>>();
        // Player UID -> the faction an admin set with /recruits faction set (user, 2026-11-02), saved with the world.
        Dictionary<string, AssignedPlayer> assigned = new Dictionary<string, AssignedPlayer>();
        // Players' sides as last worked out (0.0.44): by entity while they play (no UID lookup on the hot path), and
        // by UID from their recruits' owner record while they don't. Kept apart, so a player judged both ways (an
        // owner who is also a target) doesn't keep replacing one with the other.
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
            // 0.0.89: was /recruits faction; now a branch of the mod's one command (ModCommands).
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

        /// <summary>After /recruits faction reload (0.0.90): loaded NPCs take their faction's new health at once (damage
        /// is read at each attack), and online players over new limits lose the newest recruits. Returns how many NPCs
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
                // Since 0.0.92 race and groups count only with RaceAndGroupMembership.
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

        // ---- players' sides, cached ----

        PlayerSide Build(string race, IEnumerable<string> groups, string assignedFaction)
        {
            PlayerSide side = new PlayerSide { Race = race, Groups = (groups ?? Array.Empty<string>()).Where(g => g != null).ToArray(), Assigned = assignedFaction };
            foreach (string f in Config.Factions.Keys)
            {
                if (IsFriend(f, race, side.Groups, assignedFaction)) side.FriendOf.Add(f);
            }
            foreach (string f in side.FriendOf)
            {
                side.AlliedTo.Add(f);
                if (!relations.TryGetValue(f, out Dictionary<string, FactionRelation> row)) continue;
                foreach (KeyValuePair<string, FactionRelation> kv in row)
                {
                    if (kv.Value == FactionRelation.Ally) side.AlliedTo.Add(kv.Key);
                }
            }
            return side;
        }

        // An online player's side, from their entity: race, groups and admin-set faction.
        PlayerSide SideOf(EntityPlayer player)
        {
            string raw = Attrs.String(player.WatchedAttributes, "skinModel");
            long now = Now;
            if (sides.TryGetValue(player, out PlayerSide side) && (object)side.RawRace == raw && now < side.Until) return side;
            string uid = Attrs.Uid(player);
            side = Build(raw?.ToLowerInvariant(), GroupsOf(player), AssignedFaction(uid));
            side.Uid = uid;
            side.Source = player;
            side.RawRace = raw;
            side.Until = now + SideCacheMs;
            sides[player] = side;
            return side;
        }

        // A player's side by UID: live while they play, else as their recruits' owner record last saw them.
        PlayerSide SideOf(string uid)
        {
            if (uid == null) return Build(null, null, null);
            if (sapi?.World.PlayerByUid(uid) is IServerPlayer sp && sp.ConnectionState == EnumClientState.Playing && sp.Entity is EntityPlayer online) return SideOf(online);
            HiringOwner owner = Hiring?.Owner(uid);
            object source = (object)owner ?? NoRecord;
            if (recordSides.TryGetValue(uid, out PlayerSide side) && side.Source == source && (object)side.RawRace == owner?.Race && side.GroupsSource == owner?.Groups) return side;
            side = Build(owner?.Race?.ToLowerInvariant(), owner?.Groups, AssignedFaction(uid));
            side.Source = source;
            side.RawRace = owner?.Race;
            side.GroupsSource = owner?.Groups;
            recordSides[uid] = side;
            return side;
        }

        /// <summary>Forgets what is known of a player's side, after something about them changed.</summary>
        public void ForgetSide(string uid)
        {
            if (uid == null) return;
            recordSides.Remove(uid);
            List<EntityPlayer> stale = null;
            foreach (KeyValuePair<EntityPlayer, PlayerSide> kv in sides)
            {
                if (kv.Value.Uid == uid) (stale ??= new List<EntityPlayer>()).Add(kv.Key);
            }
            if (stale != null) foreach (EntityPlayer player in stale) sides.Remove(player);
        }

        /// <summary>The faction an admin set for this player, or null.</summary>
        public string AssignedFaction(string playerUid) => playerUid != null && assigned.TryGetValue(playerUid, out AssignedPlayer a) ? a.Faction : null;

        /// <summary>Sets (faction null: clears) the faction for a player. Public for tests.</summary>
        public void SetAssigned(string playerUid, string name, string faction)
        {
            if (faction == null) assigned.Remove(playerUid);
            else assigned[playerUid] = new AssignedPlayer { Name = name, Faction = faction.Trim().ToLowerInvariant() };
            ForgetSide(playerUid);
        }

        public IEnumerable<AssignedPlayer> AssignedPlayers => assigned.Values;

        void LoadAssigned()
        {
            byte[] bytes = sapi.WorldManager.SaveGame.GetData(AssignedKey);
            if (bytes == null) return;
            try
            {
                assigned = JsonConvert.DeserializeObject<Dictionary<string, AssignedPlayer>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, AssignedPlayer>();
                foreach (string uid in assigned.Where(kv => kv.Value?.Faction == null).Select(kv => kv.Key).ToList()) assigned.Remove(uid);
                sides.Clear();
                recordSides.Clear();
                sapi.Logger.Notification("[lotrnpcs] Loaded " + assigned.Count + " admin-set player factions.");
            }
            catch (Exception e)
            {
                SavedData.KeepUnreadable(sapi, AssignedKey, bytes, "admin-set player factions", e);
            }
        }

        public void SaveAssigned()
        {
            sapi.WorldManager.SaveGame.StoreData(AssignedKey, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(assigned)));
        }

        public bool IsAggressive(string faction) => faction != null && aggressive.Contains(faction);

        public bool IsMonster(Entity entity)
        {
            if (entity?.Code == null || !(NpcTypeInfo.Of(entity) is NpcTypeInfo info)) return false;
            if (info.MonsterStamp == monsterStamp) return info.Monster;
            bool monster = false;
            for (int i = 0; i < monsterCodes.Length && !monster; i++) monster = WildcardUtil.Match(monsterCodes[i], entity.Code);
            info.Monster = monster;
            info.MonsterStamp = monsterStamp;
            return monster;
        }

        /// <summary>A player who can be attacked: not in creative or spectator mode.</summary>
        public static bool CanBeAttacked(EntityPlayer player) =>
            !(player.Player?.WorldData?.CurrentGameMode is EnumGameMode mode && (mode == EnumGameMode.Creative || mode == EnumGameMode.Spectator));

        /// <summary>Does an NPC of this faction pick this entity as a target? Range, sight and game mode are
        /// checked by the AI task itself.</summary>
        public bool WantsToAttack(string faction, Entity target)
        {
            if (faction == null || target == null || !target.Alive) return false;
            if (target is EntityPlayer player) return HasGrudge(faction, player.EntityId) || (IsAggressive(faction) && !IsFriend(faction, player));
            if (Recruit.OwnerUidOf(target) is string ownerUid) return WouldAttackRecruit(faction, target, ownerUid);
            string other = FactionOf(target);
            if (HasGrudge(faction, target.EntityId)) return other == null || Relation(faction, other) != FactionRelation.Ally;
            if (other != null) return Relation(faction, other) == FactionRelation.Enemy;
            return IsMonster(target);
        }

        /// <summary>
        /// Wild NPCs treat a recruit the way they treat its owner (hiring plan, 2026-11-02): on sight if they would
        /// attack the owner on sight, and also when the recruit or its owner hurt them or their allies. A recruit
        /// keeps its faction's looks, not its friends: its own faction fights it too if its owner is that faction's enemy.
        /// </summary>
        public bool WouldAttackRecruit(string faction, Entity recruit, string ownerUid)
        {
            if (HasGrudge(faction, recruit.EntityId)) return true;
            HiringOwner owner = Hiring?.Owner(ownerUid);
            if (owner != null && owner.EntityId != 0 && HasGrudge(faction, owner.EntityId)) return true;
            return IsAggressive(faction) && !SideOf(ownerUid).FriendOf.Contains(faction);
        }

        /// <summary>Would this NPC fight that entity (0.0.82, counting a battle line's enemies)?</summary>
        public bool IsEnemyOf(Entity npc, Entity e)
        {
            string owner = Recruit.OwnerUidOf(npc);
            if (owner != null) return RecruitWantsToAttack(npc, owner, e);
            return FactionOf(npc) is string faction && WantsToAttack(faction, e);
        }

        /// <summary>Same side for battle spacing (0.0.82): recruits of one leader, or wild NPCs of one faction or allied
        /// factions. Same-side NPCs keep CombatSpacing apart, others only the duel minimum.</summary>
        public bool SameSide(Entity a, Entity b)
        {
            string la = Recruit.LeaderUidOf(a), lb = Recruit.LeaderUidOf(b);
            if (la != null || lb != null) return la == lb;
            string fa = FactionOf(a), fb = FactionOf(b);
            if (fa == null || fb == null) return false;
            return fa == fb || Relation(fa, fb) == FactionRelation.Ally;
        }

        /// <summary>
        /// Does a recruit pick this target? Never its owner or the owner's other recruits, and nothing while passive
        /// or breaking off after a movement order. Otherwise it fights whoever hurt the owner or one of them, and
        /// whatever is going for them; defensive recruits fight only those. Aggressive ones (the default) also attack
        /// enemies on sight: faction NPCs that would attack the owner on sight, monsters, and (0.0.102) players of an
        /// enemy side and their recruits (EnemyForces); other players and their recruits only when they started it. Raiding ones attack every player and creature that isn't
        /// allied (RaidTarget). Guarding ones (Guard here, 0.0.44) pick nothing on sight outside their area, and inside
        /// it they also attack intruders (IsIntruder).
        /// </summary>
        public bool RecruitWantsToAttack(Entity recruit, string ownerUid, Entity target, bool charge = false)
        {
            if (target == null || !target.Alive || target.EntityId == recruit.EntityId || Recruit.IsTeam(target, ownerUid)) return false;
            string stance = Recruit.StanceOf(recruit);
            // 0.0.136: an ordered charge fights like Aggressive (passive recruits still never fight).
            charge = charge || (Hiring != null && Hiring.Charging(recruit));
            if (stance == Recruit.Passive || (!charge && Hiring != null && Hiring.Disengaging(recruit))) return false;
            if (charge && stance == Recruit.Defensive) stance = Recruit.Aggressive;
            // 0.0.77 had followers in a formation (not loose) fight nothing more than 12 blocks from their leader; 0.0.84
            // (owner, 2026-11-04: "remove the limit") drops that. The break-off past 12 blocks (UpdateFollowFight) stays.
            if (Hiring != null && Hiring.HasTeamGrudge(ownerUid, target.EntityId)) return true;
            if (HiringSystem.IsTargetingTeam(target, ownerUid)) return true;
            if (stance == Recruit.Defensive) return false;
            // 0.0.112: no fights started with players near the world spawn (fighting back is above).
            if (target is EntityPlayer && NearWorldSpawn(target)) return false;
            bool guarding = Recruit.OrderOf(recruit) == Recruit.Guard;
            if (guarding && !InGuardArea(recruit, target)) return false;
            if (stance == Recruit.Raid) return RaidTarget(recruit, ownerUid, target);
            if (guarding && IsIntruder(ownerUid, target)) return true;
            // 0.0.102 (owner, 2026-11-09: two hired armies sent at each other on Aggressive "did not fight each other";
            // chose "Enemy players and recruits"): players of an enemy side (survival mode) and their recruits are fought
            // on sight. Until 0.0.101 Aggressive left every player and player's recruit alone unless provoked.
            if (target is EntityPlayer targetPlayer) return CanBeAttacked(targetPlayer) && EnemyForces(recruit, ownerUid, Attrs.Uid(targetPlayer), null);
            if (Recruit.OwnerUidOf(target) is string otherOwner) return EnemyForces(recruit, ownerUid, otherOwner, target);
            if (FactionOf(target) is string other) return WouldAttackRecruit(other, recruit, ownerUid);
            return IsMonster(target);
        }

        /// <summary>
        /// Are two players' forces enemies (0.0.102)? Each side is its player's factions (race, groups or admin setting)
        /// plus, for a recruit, the recruit's own faction, so two players' hired armies of enemy factions fight even when
        /// the players themselves belong to no faction. Enemies when some pair of their factions is at war and no pair is
        /// allied (the same faction counts as allied). Players on the same side (group, allied factions, whitelist) never
        /// are, and a side with no faction is nobody's enemy.
        /// </summary>
        public bool EnemyForces(Entity recruit, string ownerUid, string otherUid, Entity otherRecruit)
        {
            if (otherUid == null || ownerUid == null || otherUid == ownerUid || Friendly(ownerUid, otherUid)) return false;
            HashSet<string> mine = SideOf(ownerUid).FriendOf, theirs = SideOf(otherUid).FriendOf;
            string mineExtra = FactionOf(recruit), theirsExtra = otherRecruit == null ? null : FactionOf(otherRecruit);
            bool war = false;
            foreach (string a in mine)
            {
                if (!PairAtWar(a, theirs, theirsExtra, ref war)) return false;
            }
            if (mineExtra != null && !mine.Contains(mineExtra) && !PairAtWar(mineExtra, theirs, theirsExtra, ref war)) return false;
            return war;
        }

        // One of a side's factions against all of the other side's: false if any pair is allied; war set if any is at war
        // and the other side's faction attacks on sight (as with wild NPCs, WouldAttackRecruit: a Gondor army leaves the
        // defensive Shire alone, while Shire recruits on Aggressive go for Gondor's).
        bool PairAtWar(string a, HashSet<string> theirs, string theirsExtra, ref bool war)
        {
            foreach (string b in theirs)
            {
                FactionRelation r = Relation(a, b);
                if (r == FactionRelation.Ally) return false;
                if (r == FactionRelation.Enemy && IsAggressive(b)) war = true;
            }
            if (theirsExtra != null && !theirs.Contains(theirsExtra))
            {
                FactionRelation r = Relation(a, theirsExtra);
                if (r == FactionRelation.Ally) return false;
                if (r == FactionRelation.Enemy && IsAggressive(theirsExtra)) war = true;
            }
            return true;
        }

        /// <summary>
        /// Guard here (design doc, 2026-10-30: "any non-friendly player inside the radius, even while the owner is
        /// offline"): a player who isn't on the owner's side (Friendly) and can be attacked, or such a player's recruit.
        /// </summary>
        public bool IsIntruder(string ownerUid, Entity target)
        {
            if (target is EntityPlayer player) return CanBeAttacked(player) && !Friendly(ownerUid, Attrs.Uid(player));
            return Recruit.OwnerUidOf(target) is string otherOwner && !Friendly(ownerUid, otherOwner);
        }

        /// <summary>How far around their post guards patrol, from the config.</summary>
        public double GuardRadius => FactionConfig.ClampGuardRadius(Config.GuardRadius);

        /// <summary>0.0.112: is this entity within SpawnSafeRadius blocks (across) of the world spawn?</summary>
        public bool NearWorldSpawn(Entity e)
        {
            EntityPos spawn = sapi?.World.DefaultSpawnPosition;
            return spawn != null && e != null && WithinRadius(e.Pos.X - spawn.X, e.Pos.Z - spawn.Z, Config.SpawnSafeRadius);
        }

        public static bool WithinRadius(double dx, double dz, int radius) => radius > 0 && dx * dx + dz * dz <= (double)radius * radius;

        /// <summary>Is the target inside the area this guard watches over (extra: blocks of leeway past its edge)? The
        /// area reaches as far up and down as across.</summary>
        public bool InGuardArea(Entity guard, Entity target, double extra = 0)
        {
            if (target == null || !Recruit.TryGetPost(guard, out double px, out double py, out double pz) || target.Pos.Dimension != guard.Pos.Dimension) return false;
            double r = GuardRadius + extra, dx = target.Pos.X - px, dz = target.Pos.Z - pz;
            return dx * dx + dz * dz <= r * r && Math.Abs(target.Pos.Y - py) <= r;
        }

        /// <summary>
        /// Raid (user, 2026-11-03: "attack all non allied players and creatures"). Allied: players on the owner's side
        /// (Friendly), those players' recruits, NPCs of a faction allied to the owner's or the recruit's own, and
        /// creatures owned by any of those players. Players in creative or spectator mode are left alone. Everything
        /// else alive with a mind of its own is fair game: other players, neutral and enemy NPCs, traders, monsters, animals.
        /// </summary>
        public bool RaidTarget(Entity recruit, string ownerUid, Entity target)
        {
            if (target is EntityPlayer player) return CanBeAttacked(player) && !Friendly(ownerUid, Attrs.Uid(player));
            if (Recruit.OwnerUidOf(target) is string otherOwner) return !Friendly(ownerUid, otherOwner);
            if (FactionOf(target) is string faction)
            {
                if (FactionOf(recruit) is string own && Relation(own, faction) == FactionRelation.Ally) return false;
                return !SideOf(ownerUid).AlliedTo.Contains(faction);
            }
            if (IsMonster(target)) return true;
            if (!HasMind(target)) return false;
            string ownedBy = Attrs.String(target.WatchedAttributes.GetTreeAttribute("ownedby"), "uid");
            return ownedBy == null || !Friendly(ownerUid, ownedBy);
        }

        // A creature with its own AI (animals, traders, NPCs), not armor stands, boats or dropped items.
        static bool HasMind(Entity e) => e is EntityAgent && NpcTypeInfo.Of(e)?.HasTaskAi == true;

        /// <summary>Does this owner's side spare that player: allied (AlliedPlayers) or on the owner's whitelist?
        /// The whitelist is one-way: it spares them from this owner's recruits only.</summary>
        public bool Friendly(string ownerUid, string otherUid) => AlliedPlayers(ownerUid, otherUid) || (Hiring?.Whitelisted(ownerUid, otherUid) ?? false);

        /// <summary>Are these two players on the same side: the same player, sharing a /group, or members of allied
        /// factions? Offline players count as they were when last seen with recruits.</summary>
        public bool AlliedPlayers(string uidA, string uidB)
        {
            if (uidA == null || uidB == null) return false;
            if (uidA == uidB) return true;
            PlayerSide a = SideOf(uidA), b = SideOf(uidB);
            foreach (string g in a.Groups)
            {
                if (Array.IndexOf(b.Groups, g) >= 0) return true;
            }
            foreach (string f in b.FriendOf)
            {
                if (a.AlliedTo.Contains(f)) return true;
            }
            return false;
        }

        /// <summary>The factions a player belongs to (by race, groups or admin setting), in config order.</summary>
        public List<string> PlayerFactions(string uid)
        {
            PlayerSide side = SideOf(uid);
            return Config.Factions.Keys.Where(side.FriendOf.Contains).ToList();
        }

        /// <summary>An NPC of this faction was hurt by attacker: it and its allies fight back for GrudgeSeconds.
        /// Recruits count as their owner's side, so a grudge holds against them even from their own faction.</summary>
        public void RecordAttack(string faction, Entity attacker)
        {
            if (faction == null || attacker == null) return;
            if (Recruit.OwnerUidOf(attacker) == null && FactionOf(attacker) is string other && Relation(faction, other) == FactionRelation.Ally) return;
            if (!grudges.TryGetValue(faction, out Dictionary<long, long> byAttacker)) grudges[faction] = byAttacker = new Dictionary<long, long>();
            long now = Now;
            RemoveExpired(byAttacker, now);
            byAttacker[attacker.EntityId] = now + Config.GrudgeSeconds * 1000L;
        }

        /// <summary>Drops the entries of an until-when table that ran out.</summary>
        public static void RemoveExpired(Dictionary<long, long> untilById, long now)
        {
            List<long> expired = null;
            foreach (KeyValuePair<long, long> kv in untilById)
            {
                if (kv.Value < now) (expired ??= new List<long>()).Add(kv.Key);
            }
            if (expired != null) foreach (long id in expired) untilById.Remove(id);
        }

        public bool HasGrudge(string faction, long attackerId)
        {
            if (grudges.Count == 0 || faction == null) return false;
            long now = Now;
            foreach (string f in WithAllies(faction))
            {
                if (grudges.TryGetValue(f, out Dictionary<long, long> byAttacker) && byAttacker.TryGetValue(attackerId, out long until) && until >= now) return true;
            }
            return false;
        }

        /// <summary>A player by name, online or not: (uid, name as the game knows it), or null.</summary>
        public static (string uid, string name)? FindPlayer(ICoreServerAPI sapi, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (OnlinePlayer(sapi, name) is IPlayer online) return (online.PlayerUID, online.PlayerName);
            IServerPlayerData data = sapi.PlayerData.GetPlayerDataByLastKnownName(name);
            return data == null ? null : (data.PlayerUID, data.LastKnownPlayername ?? name);
        }

        static IPlayer OnlinePlayer(ICoreServerAPI sapi, string name) =>
            sapi.World.AllOnlinePlayers.FirstOrDefault(p => p.PlayerName.Equals(name, StringComparison.InvariantCultureIgnoreCase));

        TextCommandResult OnCheck(TextCommandCallingArgs args)
        {
            string name = args[0] as string;
            IPlayer player = string.IsNullOrEmpty(name) ? args.Caller.Player : OnlinePlayer(sapi, name);
            if (player?.Entity == null) return TextCommandResult.Error("That player isn't online.");
            return TextCommandResult.Success(DescribePlayer(player.PlayerName, RaceOf(player.Entity), GroupsOf(player.Entity), AssignedFaction(player.PlayerUID)));
        }

        TextCommandResult OnSet(TextCommandCallingArgs args)
        {
            string faction = (args[1] as string)?.ToLowerInvariant();
            if (faction == null || !Config.Factions.ContainsKey(faction))
                return TextCommandResult.Error("Unknown faction '" + args[1] + "'. Factions: " + string.Join(", ", Config.Factions.Keys));
            if (!(FindPlayer(sapi, args[0] as string) is (string uid, string name))) return TextCommandResult.Error("No player called '" + args[0] + "' has joined this world.");
            SetAssigned(uid, name, faction);
            SaveAssigned();
            string text = name + " is now a member of " + faction + ".";
            if (OnlinePlayer(sapi, name)?.Entity is EntityPlayer entity) text += "\n" + DescribePlayer(name, RaceOf(entity), GroupsOf(entity), faction);
            return TextCommandResult.Success(text);
        }

        TextCommandResult OnClear(TextCommandCallingArgs args)
        {
            if (!(FindPlayer(sapi, args[0] as string) is (string uid, string name))) return TextCommandResult.Error("No player called '" + args[0] + "' has joined this world.");
            string was = AssignedFaction(uid);
            if (was == null) return TextCommandResult.Success(name + " has no faction yet.");
            SetAssigned(uid, name, null);
            SaveAssigned();
            return TextCommandResult.Success(name + " is no longer a member of " + was + ". They can pick a faction again with /recruits join.");
        }

        TextCommandResult OnMembers(TextCommandCallingArgs args)
        {
            string faction = (args[0] as string)?.ToLowerInvariant();
            List<AssignedPlayer> list = assigned.Values.Where(a => faction == null || a.Faction == faction).OrderBy(a => a.Faction).ThenBy(a => a.Name).ToList();
            if (list.Count == 0) return TextCommandResult.Success(faction == null ? "No player has a faction yet." : "No player is a member of " + faction + ".");
            return TextCommandResult.Success(string.Join("\n", list.Select(a => a.Name + ": " + a.Faction)));
        }

        public string DescribePlayer(string name, string race, List<string> groups, string assignedFaction = null)
        {
            List<string> friends = Config.Factions.Keys.Where(f => IsFriend(f, race, groups, assignedFaction)).ToList();
            List<string> onSight = Config.Factions.Keys.Where(f => IsAggressive(f) && !friends.Contains(f)).ToList();
            List<string> fightBack = Config.Factions.Keys.Where(f => !IsAggressive(f) && !friends.Contains(f)).ToList();
            return name + " (faction " + (assignedFaction ?? "none yet") + (Config.RaceAndGroupMembership ? ", race " + (race ?? "none") + ", groups " + (groups.Count == 0 ? "none" : string.Join(", ", groups)) : ", race code " + (race ?? "none")) + ")\n"
                + "Friend of: " + (friends.Count == 0 ? "nobody" : string.Join(", ", friends)) + "\n"
                + "Attacked on sight by: " + (onSight.Count == 0 ? "nobody" : string.Join(", ", onSight)) + "\n"
                + "Fight back if attacked: " + (fightBack.Count == 0 ? "nobody" : string.Join(", ", fightBack));
        }

        static string RankList(Dictionary<string, float> table) =>
            table == null ? "as the units" : string.Join("/", FactionTiers.Ranks.Concat(Promotion.Branches).Select(r => table.TryGetValue(r, out float v) ? v.ToString("0.##", CultureInfo.InvariantCulture) : "-"));

        public string DescribeFactions()
        {
            List<string> keys = Config.Factions.Keys.ToList();
            List<string> pairs(FactionRelation rel) => keys.SelectMany((a, i) => keys.Skip(i + 1).Where(b => Relation(a, b) == rel)
                .Select(b => a + " + " + b + (rel == FactionRelation.Ally && SharesFriends(a, b) ? " (share friends)" : ""))).ToList();
            List<string> allies = pairs(FactionRelation.Ally), neutral = pairs(FactionRelation.Neutral);
            return string.Join("\n", Config.Factions.Select(kv => kv.Key + ": " + kv.Value.Stance
                // 0.0.92: membership is by the join menu; races and groups only show while they count.
                + (kv.Value.Joinable ? "" : ", not in the join menu")
                + (Config.RaceAndGroupMembership
                    ? ", friendly races " + (kv.Value.FriendlyRaces.Count == 0 ? "none" : string.Join(" ", kv.Value.FriendlyRaces))
                      + ", friendly groups " + (kv.Value.FriendlyGroups.Count == 0 ? "none" : string.Join(" ", kv.Value.FriendlyGroups))
                    : "")
                // 0.0.90: the balance values, short.
                + ", follow " + Config.FollowersOf(kv.Key) + ", roster " + Config.RosterOf(kv.Key)
                // 0.0.91: per rank, recruit/warrior/captain/archer.
                + ", health " + RankList(Config.Def(kv.Key)?.Health) + ", damage " + RankList(Config.Def(kv.Key)?.Damage)))
                // 0.0.89 (owner: "remove the descriptions in chat of the faction unit types and their hiring coin"): no
                // hire prices or coin here any more; they are in the config file.
                + "\nAllies: " + (allies.Count == 0 ? "none" : string.Join(", ", allies))
                + "\nNeutral: " + (neutral.Count == 0 ? "none" : string.Join(", ", neutral))
                + "\nEvery other pair are enemies. Hits vary by " + Math.Round(Config.Spread * 100) + "% either way.";
        }
    }

    /// <summary>
    /// Server behavior on every faction NPC: when something hurts it, its faction and allies hold a grudge against the
    /// attacker, and allied NPCs nearby come to help. It also heals NPCs faster out of combat, and brings NPCs from
    /// older versions up to date when they load.
    /// </summary>
    public class EntityBehaviorFactionMember : EntityBehavior
    {
        public const string TestPrefix = "Test: ";
        FactionSystem factions;
        long lastHurtMs = long.MinValue / 2;
        float sinceRegenCheck;
        // Battle spacing (0.0.53): until when this NPC counts as fighting, and the push away from NPCs standing too
        // close, worked out a few times a second from the entity partitions (no allocation: the callback is cached).
        public const long FightingMs = 3000;
        const float ScanSeconds = 0.25f, IdleScanSeconds = 1f;
        long fightingUntil;
        float sinceScan;
        double pushX, pushZ, scanSpacing, enemySpacing, brake, headX, headZ;
        bool lookAhead, scanFighting;
        // 0.0.130: neighbours within BattleSpacing.ContactScan at the last scan, kept out of this NPC's body every tick.
        readonly List<Entity> contacts = new List<Entity>(8);
        // 0.0.82: the turn toward the NPC it fights, at most FaceRadPerSec, while standing within FaceRange of it.
        public const double FaceRange = 4, FaceRadPerSec = 6;
        long scanTarget;
        EntityPartitioning partitions;
        ActionConsumable<Entity> onNeighbor;

        public EntityBehaviorFactionMember(Entity entity) : base(entity) { }

        /// <summary>The seek and melee tasks call this while chasing or hitting something.</summary>
        public void MarkFighting(long now) => fightingUntil = now + FightingMs;
        public bool Fighting(long now) => now < fightingUntil;

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            if (entity.Api.Side != EnumAppSide.Server) return;
            factions = entity.Api.ModLoader.GetModSystem<FactionSystem>();
            StripTestName(entity);
            // 0.0.90/0.0.91: the faction's health for this rank, so new NPCs spawn with it and older ones get it
            // (keeping their wounds) when they load.
            SyncMaxHealth(entity, ScaledMaxHealth(entity, factions?.Config));
        }

        /// <summary>The health this NPC should have: its faction's for its rank (0.0.91), else its type's.</summary>
        public static float ScaledMaxHealth(Entity entity, FactionConfig config)
        {
            float typeMax = TypeMaxHealth(entity.Properties);
            if (typeMax <= 0) return typeMax;
            float? own = config?.HealthFor(FactionSystem.FactionOf(entity), RankOf(entity));
            return own is float h ? Math.Max(1, FactionTiers.Round(h)) : typeMax;
        }

        /// <summary>The NPC's rank (recruit, warrior, captain, archer) in lower case, or null for rankless NPCs.</summary>
        public static string RankOf(Entity entity) => NpcTypeInfo.Of(entity)?.Rank?.Trim().ToLowerInvariant();

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);
            if (entity.Api.Side != EnumAppSide.Server) return;
            AiTaskManager tasks = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (tasks != null) tasks.OnShouldExecuteTask += AllowTask;
        }

        // 0.0.82 (owner: fighting NPCs "turning in place"): no wandering off or looking about in a fight. Between swings
        // or waiting in a battle line, the vanilla look-around task turned NPCs away from their enemy and the melee task
        // turned them back.
        bool AllowTask(IAiTask task) =>
            !((task is AiTaskWander || task is AiTaskLookAround) && Fighting(entity.World.ElapsedMilliseconds));

        /// <summary>The "maxhealth" the entity type's health behavior gives, or 0.</summary>
        public static float TypeMaxHealth(EntityProperties properties) => NpcTypeInfo.Of(properties)?.MaxHealth ?? 0;

        /// <summary>Vanilla health keeps the max health an NPC spawned with, so after a change to the entity type
        /// (0.0.43, user: "reduce the health of all the npcs by 6") NPCs already in a world are brought to the new
        /// value when they load. A wounded NPC keeps its wounds, and one at full health stays full. True if changed.
        /// Our behavior comes after "health" in the entity JSON, so the health tree exists by now.</summary>
        public static bool SyncMaxHealth(Entity entity, float typeMax)
        {
            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("health");
            if (tree == null || typeMax <= 0) return false;
            float stored = tree.GetFloat("basemaxhealth");
            if (stored <= 0 || Math.Abs(stored - typeMax) < 0.01f) return false;
            float oldMax = tree.GetFloat("maxhealth", stored);
            float health = tree.GetFloat("currenthealth", oldMax);
            float newMax = Math.Max(1, oldMax + typeMax - stored);
            tree.SetFloat("basemaxhealth", typeMax);
            tree.SetFloat("maxhealth", newMax);
            if (health > 0)
            {
                // Same wounds as before, but never killed or brought below 1 by the change.
                float lost = Math.Max(0, oldMax - health);
                tree.SetFloat("currenthealth", Math.Min(newMax, Math.Max(newMax - lost, Math.Min(health, 1))));
            }
            entity.WatchedAttributes.MarkPathDirty("health");
            return true;
        }

        /// <summary>NPCs spawned before 0.0.38 keep the "Test: " name their name tag picked at spawn; drop it.</summary>
        public static bool StripTestName(Entity entity)
        {
            ITreeAttribute tag = entity.WatchedAttributes.GetTreeAttribute("nametag");
            string name = tag?.GetString("name");
            if (name == null || !name.StartsWith(TestPrefix, StringComparison.Ordinal)) return false;
            tag.SetString("name", name.Substring(TestPrefix.Length));
            entity.WatchedAttributes.MarkPathDirty("nametag");
            return true;
        }

        // Faster healing out of combat, through vanilla health's own "regenSpeed" multiplier, checked once a second.
        public override void OnGameTick(float deltaTime)
        {
            if (factions == null || !entity.Alive) return;
            Separate(deltaTime);
            Face(deltaTime);
            sinceRegenCheck += deltaTime;
            if (sinceRegenCheck < 1) return;
            sinceRegenCheck = 0;
            if (factions.Log.On) factions.Log.Tick(entity, Fighting(entity.World.ElapsedMilliseconds), 1, entity.World.ElapsedMilliseconds, factions.Lines);
            float speed = RegenSpeed(factions.Config, entity.World.ElapsedMilliseconds - lastHurtMs);
            if (entity.WatchedAttributes.GetFloat("regenSpeed", 1) != speed) entity.WatchedAttributes.SetFloat("regenSpeed", speed);
        }

        // Fighting NPCs closer than CombatSpacing to another NPC drift apart, at up to BattleSpacing.PushSpeed blocks
        // a second, sideways to whatever they are doing. 0.0.104 (owner, 2026-11-09: men "walking through each other",
        // chose "Push always + 5-block look-ahead"): NPCs that aren't fighting keep BodySpacing apart too, and a walking
        // NPC looks LookAhead blocks along its way for its own side, steering past them and slowing behind one that is
        // right in front. Only neighbours from the entity partitions are looked at, never every NPC. Standing NPCs that
        // aren't fighting scan once a second, walking or fighting ones four times a second.
        void Separate(float dt)
        {
            long now = entity.World.ElapsedMilliseconds;
            bool fighting = now < fightingUntil;
            double mx = entity.Pos.Motion.X, mz = entity.Pos.Motion.Z;
            bool walking = mx * mx + mz * mz > BattleSpacing.WalkingMotion * BattleSpacing.WalkingMotion;
            sinceScan += dt;
            if (sinceScan >= (fighting || walking ? ScanSeconds : IdleScanSeconds))
            {
                sinceScan = 0;
                Scan(now, fighting, walking);
            }
            KeepApart();
            if ((pushX == 0 && pushZ == 0 && brake == 0) || !entity.OnGround) return;
            double k = BattleSpacing.PushImpulse(dt);
            entity.Pos.Motion.X += pushX * k;
            entity.Pos.Motion.Z += pushZ * k;
            if (brake > 0)
            {
                // Takes away up to BrakeShare of the speed along the way each tick, never turning it around.
                double along = mx * headX + mz * headZ;
                if (along > 0)
                {
                    double cut = along * Math.Min(1, brake) * BattleSpacing.BrakeShare;
                    entity.Pos.Motion.X -= headX * cut;
                    entity.Pos.Motion.Z -= headZ * cut;
                }
            }
        }

        // 0.0.130: hard bodies. Each tick the NPC steps out of any overlap with a neighbour from its last scan, by half
        // the overlap (the neighbour does the same on its own tick), never into a wall or off a ledge. Riders and
        // mounts are left out (a rider sits inside its mount); players keep the game's own push.
        void KeepApart()
        {
            if (contacts.Count == 0 || (entity as EntityAgent)?.MountedOn != null) return;
            double x = entity.Pos.X, z = entity.Pos.Z;
            foreach (Entity e in contacts)
            {
                if (!e.Alive || (e as EntityAgent)?.MountedOn != null || Math.Abs(e.Pos.InternalY - entity.Pos.InternalY) > 1.5) continue;
                if (BattleSpacing.ContactShift(x - e.Pos.X, z - e.Pos.Z, entity.EntityId, out double sx, out double sz)) { x += sx; z += sz; }
            }
            if ((x != entity.Pos.X || z != entity.Pos.Z) && CanStepTo(entity.World.BlockAccessor, x, entity.Pos.InternalY, z))
            {
                entity.Pos.X = x;
                entity.Pos.Z = z;
            }
        }

        // 0.0.82: a fighting NPC turns toward the NPC it fights. 0.0.137 (owner: NPCs should "keep their front toward
        // the nearest enemy, and allow them to walk backwards"): its claimed target, else the nearest enemy from its scan
        // or whoever last hurt it, within BattleSpacing.FaceScan, also while it walks (this behavior runs after the AI's
        // path follower, which turns it the way it walks); moving away from that enemy it steps back at a walk with the
        // walkback animation. Not mounted (the mount turns it) and not while breaking off (Retreat: they turn and run).
        void Face(float dt)
        {
            long now = entity.World.ElapsedMilliseconds;
            EntityAgent agent = entity as EntityAgent;
            bool backing = false;
            Entity threat = now < fightingUntil && agent?.MountedOn == null && factions.Hiring?.Disengaging(entity) != true ? Threat(now) : null;
            if (threat != null)
            {
                double dx = threat.Pos.X - entity.Pos.X, dz = threat.Pos.Z - entity.Pos.Z;
                if (dx * dx + dz * dz > 1e-4)
                {
                    float desired = (float)Math.Atan2(dx, dz);
                    float diff = GameMath.AngleRadDistance(entity.Pos.Yaw, desired);
                    float max = (float)(FaceRadPerSec * Math.Min(dt, 0.2f));
                    entity.Pos.Yaw = (entity.Pos.Yaw + GameMath.Clamp(diff, -max, max)) % GameMath.TWOPI;
                    agent.BodyYaw = entity.Pos.Yaw;
                    Vec3d walk = agent.Controls.WalkVector;
                    double wl = Math.Sqrt(walk.X * walk.X + walk.Z * walk.Z);
                    if (wl > 1e-4 && BattleSpacing.Backing(entity.Pos.Yaw, walk.X, walk.Z))
                    {
                        backing = true;
                        if (wl > BattleSpacing.BackWalkSpeed) { walk.X *= BattleSpacing.BackWalkSpeed / wl; walk.Z *= BattleSpacing.BackWalkSpeed / wl; }
                        agent.Controls.Sprint = false;
                    }
                }
            }
            if (backing == walkingBack) return;
            walkingBack = backing;
            if (backing)
            {
                entity.AnimManager?.StopAnimation("sprint");
                entity.AnimManager?.StartAnimation("lotrnpcs-walkback");
            }
            else entity.AnimManager?.StopAnimation("lotrnpcs-walkback");
        }

        bool walkingBack;
        Entity nearestEnemy, lastAttacker;
        double nearestEnemyD2;

        // The enemy to face: the claimed target, else the nearer of the nearest scanned enemy and the last attacker
        // (hit within FightingMs), within FaceScan.
        Entity Threat(long now)
        {
            double r2 = BattleSpacing.FaceScan * BattleSpacing.FaceScan;
            bool Near(Entity e) => e != null && e.Alive && e.Pos.Dimension == entity.Pos.Dimension && e.Pos.SquareDistanceTo(entity.Pos) <= r2;
            long id = factions.Spacing.TargetOf(entity.EntityId, now);
            if (id == 0) id = factions.Lines.TargetOf(entity.EntityId);
            Entity claimed = id == 0 ? null : entity.World.GetEntityById(id);
            if (Near(claimed)) return claimed;
            Entity a = Near(nearestEnemy) ? nearestEnemy : null, b = now - lastHurtMs < FightingMs && Near(lastAttacker) ? lastAttacker : null;
            if (a == null || b == null) return a ?? b;
            return a.Pos.SquareDistanceTo(entity.Pos) <= b.Pos.SquareDistanceTo(entity.Pos) ? a : b;
        }

        void Scan(long now, bool fighting, bool walking)
        {
            pushX = pushZ = 0; brake = 0;
            contacts.Clear();
            nearestEnemy = null; nearestEnemyD2 = double.MaxValue; scanFighting = fighting;
            BattleSpacing spacing = factions.Spacing;
            // 0.0.82: enemies, the one it fights included, may come as close as the duel minimum; only its own side
            // keeps CombatSpacing (before, every NPC but its target did, which shoved fighters out of reach). 0.0.104:
            // out of a fight its own side keeps BodySpacing (formations stand 1.8 apart, so they are left as they are).
            scanSpacing = fighting ? spacing.Spacing : Math.Min(spacing.Spacing, BattleSpacing.BodySpacing);
            enemySpacing = Math.Min(spacing.DuelMin, scanSpacing);
            scanTarget = spacing.TargetOf(entity.EntityId, now);
            lookAhead = walking;
            if (walking)
            {
                double mx = entity.Pos.Motion.X, mz = entity.Pos.Motion.Z, len = Math.Sqrt(mx * mx + mz * mz);
                headX = mx / len; headZ = mz / len;
            }
            partitions ??= entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
            if (partitions == null) return;
            onNeighbor ??= OnNeighbor;
            double radius = Math.Max(walking ? Math.Max(scanSpacing + 0.5, BattleSpacing.LookAhead) : scanSpacing + 0.5, BattleSpacing.ContactScan);
            if (fighting) radius = Math.Max(radius, BattleSpacing.FaceScan);
            partitions.WalkEntities(entity.Pos.X, entity.Pos.InternalY, entity.Pos.Z, radius, onNeighbor, null, EnumEntitySearchType.Creatures);
            double length = Math.Sqrt(pushX * pushX + pushZ * pushZ);
            if (length < 1e-6) { pushX = pushZ = 0; return; }
            double nx = pushX / length, nz = pushZ / length;
            if (length > 1) { pushX = nx; pushZ = nz; }
            // Never off a ledge or into a wall: the spot 0.7 blocks along the push needs open air and ground below.
            if (!CanStepTo(entity.World.BlockAccessor, entity.Pos.X + nx * 0.7, entity.Pos.InternalY, entity.Pos.Z + nz * 0.7)) pushX = pushZ = 0;
        }

        bool OnNeighbor(Entity e)
        {
            if (e == entity || !e.Alive || e is EntityPlayer || FactionSystem.FactionOf(e) == null) return true;
            if (Math.Abs(e.Pos.InternalY - entity.Pos.InternalY) > 1.5) return true;
            double dx = entity.Pos.X - e.Pos.X, dz = entity.Pos.Z - e.Pos.Z;
            bool target = e.EntityId == scanTarget;
            // Cheap distance check first: most neighbours of a 5-block look-ahead scan are neither close nor ahead.
            double d2 = dx * dx + dz * dz;
            if (d2 < BattleSpacing.ContactScan * BattleSpacing.ContactScan && contacts.Count < 8) contacts.Add(e);
            // 0.0.137: the nearest enemy, for Face.
            if (scanFighting && d2 < nearestEnemyD2 && (target || !factions.SameSide(entity, e))) { nearestEnemy = e; nearestEnemyD2 = d2; }
            bool near = d2 < scanSpacing * scanSpacing;
            bool ahead = lookAhead && !target && d2 < BattleSpacing.LookAhead * BattleSpacing.LookAhead && dx * headX + dz * headZ < 0;
            if (!near && !ahead) return true;
            bool sameSide = !target && factions.SameSide(entity, e);
            if (near) BattleSpacing.AddPush(ref pushX, ref pushZ, dx, dz, sameSide ? scanSpacing : enemySpacing, entity.EntityId);
            // Only one it is catching up with: a neighbour walking the same way about as fast (a column, a marching
            // line's rear rank) is left alone, or formations would be pushed out of shape.
            if (ahead && sameSide && BattleSpacing.Closing(entity.Pos.Motion.X * headX + entity.Pos.Motion.Z * headZ, e.Pos.Motion.X * headX + e.Pos.Motion.Z * headZ))
                BattleSpacing.AddAvoid(ref pushX, ref pushZ, ref brake, headX, headZ, -dx, -dz, entity.EntityId);
            return true;
        }

        /// <summary>True when an NPC can stand at (x, y, z): nothing solid at its feet or head, and solid ground at
        /// most one block below its feet.</summary>
        public static bool CanStepTo(IBlockAccessor blocks, double x, double y, double z)
        {
            int bx = (int)Math.Floor(x), by = (int)Math.Floor(y + 0.01), bz = (int)Math.Floor(z);
            return !Solid(blocks, bx, by, bz) && !Solid(blocks, bx, by + 1, bz) && (Solid(blocks, bx, by - 1, bz) || Solid(blocks, bx, by - 2, bz));
        }

        static bool Solid(IBlockAccessor blocks, int x, int y, int z)
        {
            Cuboidf[] boxes = blocks.GetBlockRaw(x, y, z).CollisionBoxes;
            return boxes != null && boxes.Length > 0;
        }

        // A dead or unloaded NPC frees its place in the fight, and nobody keeps a place on it.
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            // Recruits that hit it in the last 10 seconds earn XP (0.0.76).
            if (factions != null) entity.Api.ModLoader.GetModSystem<PromotionSystem>()?.OnDeath(entity);
            factions?.Log.Death(entity, damageSourceForDeath?.GetCauseEntity(), entity.World.ElapsedMilliseconds);
            factions?.Spacing.Forget(entity.EntityId);
            factions?.Lines.Forget(entity.EntityId);
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            factions?.Spacing.Forget(entity.EntityId);
            factions?.Lines.Forget(entity.EntityId);
            base.OnEntityDespawn(despawn);
        }

        public static float RegenSpeed(FactionConfig config, long msSinceHurt) =>
            msSinceHurt < (config?.RegenDelaySeconds ?? 10) * 1000L ? 1 : Math.Max(0, config?.HealthRegenSpeed ?? 1);

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            if (factions == null || damage <= 0 || damageSource?.Type == EnumDamageType.Heal) return;
            lastHurtMs = entity.World.ElapsedMilliseconds;
            MarkFighting(lastHurtMs);
            if (entity.WatchedAttributes.GetFloat("regenSpeed", 1) != 1) entity.WatchedAttributes.SetFloat("regenSpeed", 1);

            Entity attacker = damageSource?.GetCauseEntity();
            if (attacker != null && attacker != entity) lastAttacker = attacker;
            entity.Api.ModLoader.GetModSystem<PromotionSystem>()?.OnHit(entity, attacker);
            string faction = FactionSystem.FactionOf(entity);
            if (attacker == null || attacker == entity || !attacker.Alive || faction == null) return;
            if (Recruit.OwnerUidOf(attacker) == null && FactionSystem.FactionOf(attacker) is string other && factions.Relation(faction, other) == FactionRelation.Ally) return;
            factions.RecordAttack(faction, attacker);
            if (attacker is EntityPlayer player && !FactionSystem.CanBeAttacked(player)) return;

            string[] allies = factions.WithAllies(faction);
            float range = factions.Config.CallForHelpRange;
            foreach (Entity helper in entity.World.GetEntitiesAround(entity.Pos.XYZ, range, range / 2, e => e != entity && e.Alive && e is EntityAgent))
            {
                // Recruits answer only their owner's side (HiringSystem.OnTeamHurt).
                if (Recruit.OwnerUidOf(helper) == null && Array.IndexOf(allies, FactionSystem.FactionOf(helper)) >= 0) helper.Notify("seekEntity", attacker);
            }
        }

        public override string PropertyName() => "lotrnpcs:factionmember";
    }

    /// <summary>Vanilla seek, but the target is chosen by faction instead of entity code, guards stay near their post,
    /// and battles spread out (0.0.53): an NPC skips enemy NPCs that already have enough attackers, and goes to its
    /// own side of its target instead of onto the target's position.</summary>
    public class AiTaskFactionSeek : AiTaskSeekEntity
    {
        readonly FactionSystem factions;
        readonly string faction;
        EntityBehaviorFactionMember member;
        bool memberLookedUp;
        // Where this NPC stands around its target (block coordinates, Y as InternalY), worked out every SpotSeconds.
        readonly Vec3d spot = new Vec3d();
        bool surrounding;
        // 0.0.82: the spot is a battle line slot (stand there) rather than a place around the target (stand anywhere in
        // the duel band near it), and the angle of that place.
        bool slotSpot;
        double spotAngle;
        // 0.0.87: the place is in the outer ring (waiting to step in), not the duel band.
        bool spotOuter;
        /// <summary>How far, in radians, an attacker in the duel band may stand from its place's angle and still stay put
        /// (0.0.82: it no longer walks to an exact point, which made it step in and out).</summary>
        public const double BandAngleSlack = 0.6;
        /// <summary>How far in or out from the outer ring's radius a waiting attacker may stand (0.0.87).</summary>
        public const double OuterSlack = 0.5;
        float sinceSpot;
        const float SpotSeconds = 0.5f;
        /// <summary>Close enough to its spot to stop walking and let the melee task fight.</summary>
        public const double AtSpotDistance = 0.65;

        // Archers (0.0.55): stop this far from a target they can shoot at, instead of walking up to it, and never take
        // a place around it. 0 for everyone else.
        readonly double standOff;
        AiTaskFactionShoot shoot;
        bool shootLookedUp;

        public AiTaskFactionSeek(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            factions = entity.Api.ModLoader.GetModSystem<FactionSystem>();
            faction = FactionSystem.FactionOf(entity);
            standOff = taskConfig["standOff"].AsDouble(0);
        }

        AiTaskFactionShoot Shoot
        {
            get
            {
                if (!shootLookedUp)
                {
                    shoot = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.AllTasks?.OfType<AiTaskFactionShoot>().FirstOrDefault();
                    shootLookedUp = true;
                }
                return shoot;
            }
        }

        /// <summary>An archer close enough to shoot, with a clear shot, stays where it is. 0.0.105 (owner, 2026-11-09:
        /// archers "end up jumping off walls in order to enter melee combat"): so does one standing more than
        /// ArcherHoldHeight blocks above its target within standOff, shot or not: it keeps its height and waits for a
        /// shot (or for the enemy to come up, when its melee fights as before) instead of going down to it.</summary>
        bool CanShootFromHere(Entity target)
        {
            if (standOff <= 0 || target == null || Shoot == null) return false;
            double dx = target.Pos.X - entity.Pos.X, dz = target.Pos.Z - entity.Pos.Z;
            if (dx * dx + dz * dz > standOff * standOff) return false;
            return HoldsHeight(entity.Pos.Y - target.Pos.Y) || Shoot.ClearShot(target);
        }

        public const double ArcherHoldHeight = 2;
        public static bool HoldsHeight(double above) => above > ArcherHoldHeight;

        // Recruits choose as RecruitWantsToAttack says, guards only within their leash, so the search picks the
        // nearest target they may chase. Without a faction, vanilla rules apply. The cheap checks (alive, range, game
        // mode) come first, the crowd check last.
        public override bool IsTargetableEntity(Entity e, float range)
        {
            // 0.0.94 (owner: not "through walls", chose A): only an enemy it can see, checked after the cheap rules.
            if (factions != null && Recruit.OwnerUidOf(entity) is string owner) return e.Alive && CanSense(e, range) && factions.RecruitWantsToAttack(entity, owner, e) && !BeyondLeash(e) && CanSee(e) && NotCrowded(e);
            return faction == null || factions == null ? base.IsTargetableEntity(e, range) : e.Alive && CanSense(e, range) && factions.WantsToAttack(faction, e) && CanSee(e) && NotCrowded(e);
        }

        /// <summary>Lines of sight (0.0.94): a target is picked only when seen (Sight.CanSee); once fighting, one out of
        /// sight is chased for LoseSightMs more (round corners, through doors), then given up, unless it hit this NPC
        /// in that time.</summary>
        public const int LoseSightMs = 5000;
        const float SightSeconds = 0.5f;
        long lastSeenMs;
        float sinceSight;
        bool CanSee(Entity e) => Sight.CanSee(entity, e, this);

        // 0.0.77 (owner, 2026-11-04: "Keep the cap of 3 for spreading out, but when there's no other enemy within
        // reach, the extra NPCs join in anyway"): a target that already has its share of attackers is passed over in
        // the search, but the nearest such one is remembered; if the search finds nothing else, ShouldExecute takes it.
        Entity crowdedFallback;
        double crowdedFallbackDistSq;
        // 0.0.82 (owner: "paired duels"; extras "wrap the flanks"): an enemy NPC someone else already fights, and that
        // isn't fighting this NPC back, is passed over too, and taken only when no free enemy is found.
        Entity pairedFallback;
        double pairedFallbackDistSq;
        // 0.0.87: an enemy whose inner ring is full is taken only when no enemy has room left.
        Entity fullFallback;
        double fullFallbackDistSq;
        bool NotCrowded(Entity e)
        {
            if (Crowded(e))
            {
                double d = e.Pos.SquareDistanceTo(entity.Pos);
                if (crowdedFallback == null || d < crowdedFallbackDistSq) { crowdedFallback = e; crowdedFallbackDistSq = d; }
                return false;
            }
            if (e is EntityPlayer || factions == null) return true;
            long now = entity.World.ElapsedMilliseconds;
            if (factions.Spacing.OthersOn(entity.EntityId, e.EntityId, now) == 0 || factions.Spacing.TargetOf(e.EntityId, now) == entity.EntityId) return true;
            double dp = e.Pos.SquareDistanceTo(entity.Pos);
            if (factions.Spacing.HasRoom(entity.EntityId, e.EntityId, now))
            {
                if (pairedFallback == null || dp < pairedFallbackDistSq) { pairedFallback = e; pairedFallbackDistSq = dp; }
            }
            else if (fullFallback == null || dp < fullFallbackDistSq) { fullFallback = e; fullFallbackDistSq = dp; }
            return false;
        }
        bool Ours => factions != null && (faction != null || Recruit.OwnerUidOf(entity) != null);
        public override bool IsTargetableEntityNoTagsNoAll(Entity e, float range) => Ours ? IsTargetableEntity(e, range) : base.IsTargetableEntityNoTagsNoAll(e, range);
        public override bool IsTargetableEntityNoTagsAll(Entity e, float range) => Ours ? IsTargetableEntity(e, range) : base.IsTargetableEntityNoTagsAll(e, range);

        // An enemy NPC that already has its share of attackers counts only for those already fighting it.
        bool Crowded(Entity e) => factions.Spacing.Crowded(entity.EntityId, e.EntityId, e is EntityPlayer, entity.World.ElapsedMilliseconds);

        // A guard chases nothing past its leash, not even to strike back, so nobody can lure it away from its post.
        // (0.0.77's 12-block limit around a formation follower's leader went in 0.0.84, owner: "remove the limit".)
        bool BeyondLeash(Entity target) =>
            factions != null && target != null && Recruit.OwnerUidOf(entity) != null
            && Recruit.OrderOf(entity) == Recruit.Guard && !factions.InGuardArea(entity, target, FactionSystem.GuardLeash);

        EntityBehaviorFactionMember Member
        {
            get
            {
                if (!memberLookedUp) { member = entity.GetBehavior<EntityBehaviorFactionMember>(); memberLookedUp = true; }
                return member;
            }
        }

        /// <summary>NPC targets get surrounded; players are approached the vanilla way (how hard players' fights are
        /// doesn't change).</summary>
        bool Surrounds(Entity target) => factions != null && Ours && target != null && !(target is EntityPlayer) && standOff <= 0;

        double Bearing(Entity target) => Math.Atan2(entity.Pos.X - target.Pos.X, entity.Pos.Z - target.Pos.Z);

        // Claims the target and works out this NPC's place around it. False when there is no usable place (the
        // NPC then walks at the target as vanilla does). 0.0.82: an NPC told by its battle line to stand in its slot
        // claims nothing and goes there instead.
        bool Place(Entity target, long now)
        {
            BattleSpacing spacing = factions.Spacing;
            if (factions.Lines.HoldSlot(entity, target, out double sx, out double sz))
            {
                spacing.Release(entity.EntityId);
                if (!StandY(sx, sz, out double sy)) return false;
                spot.Set(sx, sy, sz);
                slotSpot = true;
                return true;
            }
            slotSpot = false;
            double bearing = Bearing(target);
            spacing.Claim(entity.EntityId, target.EntityId, bearing, now);
            // 0.0.104: a front member of a line that has met the enemy fights from its own side, keeping the line.
            if (factions.Lines.HoldsFront(entity, out double fdx, out double fdz))
            {
                double front = Math.Atan2(-fdx, -fdz);
                BattleSpacing.SpotAt(target.Pos.X, target.Pos.Z, front, spacing.SurroundRadius, out double fx, out double fz);
                if (EntityBehaviorFactionMember.CanStepTo(entity.World.BlockAccessor, fx, target.Pos.InternalY, fz))
                {
                    spotOuter = false;
                    spot.Set(fx, target.Pos.InternalY, fz);
                    spotAngle = front;
                    return true;
                }
            }
            if (!spacing.SurroundAngle(entity.EntityId, target.EntityId, bearing, now, out double angle, out bool inner)) return false;
            spotOuter = !inner;
            BattleSpacing.SpotAt(target.Pos.X, target.Pos.Z, angle, inner ? spacing.SurroundRadius : spacing.OuterRadius, out double x, out double z);
            if (!EntityBehaviorFactionMember.CanStepTo(entity.World.BlockAccessor, x, target.Pos.InternalY, z)) return false;
            spot.Set(x, target.Pos.InternalY, z);
            spotAngle = angle;
            return true;
        }

        // A slot's standing height: the NPC's own level, or one block up or down.
        bool StandY(double x, double z, out double y)
        {
            IBlockAccessor blocks = entity.World.BlockAccessor;
            foreach (double dy in new[] { 0.0, 1.0, -1.0 })
            {
                y = Math.Floor(entity.Pos.InternalY) + dy;
                if (EntityBehaviorFactionMember.CanStepTo(blocks, x, y, z)) return true;
            }
            y = entity.Pos.InternalY;
            return false;
        }

        // In a slot: within AtSpotDistance of it. Fighting (0.0.82): anywhere in the duel band on its own side of the
        // target, so small moves of the target don't set it walking again.
        bool AtSpot()
        {
            if (Math.Abs(entity.Pos.InternalY - spot.Y) >= 1.5) return false;
            if (slotSpot || targetEntity == null)
            {
                double dx = entity.Pos.X - spot.X, dz = entity.Pos.Z - spot.Z;
                return dx * dx + dz * dz < AtSpotDistance * AtSpotDistance;
            }
            double tx = entity.Pos.X - targetEntity.Pos.X, tz = entity.Pos.Z - targetEntity.Pos.Z;
            double dist = Math.Sqrt(tx * tx + tz * tz);
            if (spotOuter ? Math.Abs(dist - factions.Spacing.OuterRadius) > OuterSlack : !factions.Spacing.InDuelBand(dist)) return false;
            return Math.Abs(GameMath.AngleRadDistance((float)spotAngle, (float)Math.Atan2(tx, tz))) <= BandAngleSlack;
        }

        // Striking back skips IsTargetableEntity in vanilla's seek, so the leash is checked here too. An NPC already
        // standing in its place doesn't start walking again; the melee task does the fighting from there.
        public override bool ShouldExecute()
        {
            crowdedFallback = null;
            pairedFallback = null;
            fullFallback = null;
            long searchedBefore = lastSearchTotalMs;
            bool found = base.ShouldExecute();
            // The search ran (lastSearchTotalMs moved) and found no free target: take one already being fought (0.0.82),
            // or else join a full one (0.0.77), except in a battle line, where the rest wait behind (owner: never more
            // than 2 on one enemy).
            if (!found && targetEntity == null && lastSearchTotalMs != searchedBefore)
            {
                Entity fallback = pairedFallback != null && pairedFallback.Alive ? pairedFallback
                    : fullFallback != null && fullFallback.Alive ? fullFallback
                    : crowdedFallback != null && crowdedFallback.Alive && !(factions?.Lines.Wants(entity, crowdedFallback) ?? false) ? crowdedFallback : null;
                if (fallback != null)
                {
                    targetEntity = fallback;
                    targetPos = targetEntity.Pos.XYZ;
                    found = entity.Pos.SquareDistanceTo(targetPos) > MinDistanceToTarget();
                }
            }
            crowdedFallback = null;
            pairedFallback = null;
            fullFallback = null;
            if (!found || BeyondLeash(targetEntity)) return false;
            long now = entity.World.ElapsedMilliseconds;
            Member?.MarkFighting(now);
            if (CanShootFromHere(targetEntity)) return false;
            if (!Surrounds(targetEntity)) { factions?.Lines.Leave(entity.EntityId); return true; }
            factions.Lines.Join(entity, targetEntity, now);
            return !(Place(targetEntity, now) && AtSpot());
        }

        public override void StartExecute()
        {
            long now = entity.World.ElapsedMilliseconds;
            surrounding = false;
            sinceSpot = 0;
            if (Surrounds(targetEntity))
            {
                factions.Lines.Join(entity, targetEntity, now);
                surrounding = Place(targetEntity, now);
                if (surrounding) targetPos = spot.Clone();
            }
            else factions?.Spacing.Release(entity.EntityId);
            CallIdleAllies(now);
            Member?.MarkFighting(now);
            lastSeenMs = now;
            sinceSight = 0;
            base.StartExecute();
            // 0.0.82: walking to a battle line slot, vanilla must not re-aim the path at the enemy (it does in direct
            // attack mode every 0.75 s); ContinueExecute keeps the path on the slot instead.
            if (surrounding && slotSpot) attackPattern = EnumAttackPattern.CircleTarget;
        }

        // 0.0.123 (owner, 2026-11-13: "Sure" to "when an NPC starts fighting, idle allies within about 16 blocks walk
        // over to join, with no lines"): in the 0.0.122 run 14 of 38 wild orcs never noticed a battle 24+ blocks away.
        // A wild NPC that goes for a target calls the idle wild NPCs of its faction within JoinFightRange (0.0.124; CallForHelpRange in 0.0.123); they come
        // without the sight and crowd checks (their own search sorts out the crowding once there), and call in turn, so
        // a whole army joins in hops. At most once per CallAlliesEveryMs per NPC.
        public const long CallAlliesEveryMs = 5000;
        long lastCallMs = -CallAlliesEveryMs;
        readonly List<Entity> called = new List<Entity>();

        /// <summary>Does this ally answer a wild NPC's call to join its fight: alive, wild, same faction, not fighting.</summary>
        public static bool AnswersCall(Entity caller, Entity ally, long now) =>
            ally != null && caller != null && ally != caller && ally.Alive && ally is EntityAgent && !(ally is EntityPlayer)
            && Recruit.OwnerUidOf(ally) == null && FactionSystem.FactionOf(ally) is string f && f == FactionSystem.FactionOf(caller)
            && (ally.World == null || ally.GetBehavior<EntityBehaviorFactionMember>()?.Fighting(now) != true); // no world: test entities

        void CallIdleAllies(long now)
        {
            if (factions == null || targetEntity == null || !targetEntity.Alive || Recruit.OwnerUidOf(entity) != null || now - lastCallMs < CallAlliesEveryMs) return;
            // 0.0.129: no "already fighting" check here. ShouldExecute marks the NPC fighting before StartExecute runs, so
            // that check stopped every NPC that found its own target from calling (only called-in ones called on), which
            // is why 14 of 38 orcs still sat out in the 0.0.124 run. The per-NPC CallAlliesEveryMs limit and AnswersCall
            // (only allies that aren't fighting) keep the calls few.
            lastCallMs = now;
            float range = factions.Config.JoinFightRange;
            if (range <= 0) return;
            called.Clear();
            foreach (Entity e in entity.World.GetEntitiesAround(entity.Pos.XYZ, range, range / 2, e => AnswersCall(entity, e, now))) called.Add(e);
            foreach (Entity ally in called) ally.Notify("joinFight", targetEntity);
        }

        public override bool ContinueExecute(float dt)
        {
            if (!base.ContinueExecute(dt) || BeyondLeash(targetEntity)) return false;
            long now = entity.World.ElapsedMilliseconds;
            Member?.MarkFighting(now);
            if (Ours && targetEntity != null)
            {
                sinceSight += dt;
                if (sinceSight >= SightSeconds)
                {
                    sinceSight = 0;
                    if (CanSee(targetEntity)) lastSeenMs = now;
                    else if (now - lastSeenMs > LoseSightMs && !(attackedByEntity == targetEntity && RecentlyAttacked)) return false;
                }
            }
            if (standOff > 0)
            {
                sinceSpot += dt;
                if (sinceSpot >= SpotSeconds) { sinceSpot = 0; if (CanShootFromHere(targetEntity)) return false; }
            }
            if (!surrounding) return true;
            sinceSpot += dt;
            if (sinceSpot >= SpotSeconds)
            {
                sinceSpot = 0;
                // The place follows the target (and the others around it) twice a second, not every tick.
                bool wasSlot = slotSpot;
                if (!Place(targetEntity, now)) { surrounding = false; attackPattern = EnumAttackPattern.DirectAttack; return true; }
                // From slot to fighting or back: start again, so the path and mode fit (0.0.82).
                if (wasSlot != slotSpot) return false;
                // A marching line moves the slot: follow it.
                if (slotSpot && targetPos.SquareDistanceTo(spot) > 0.25)
                {
                    targetPos.Set(spot);
                    pathTraverser.NavigateTo(targetPos, moveSpeed, 0.3f, NoOp, NoOp);
                }
            }
            if (AtSpot()) return false;
            // Vanilla points the last waypoint at the target every tick; point it at this NPC's place instead.
            if (attackPattern == EnumAttackPattern.DirectAttack && pathTraverser.Ready)
            {
                Vec3d goal = pathTraverser.CurrentTarget;
                goal.X = spot.X;
                goal.Y = spot.Y;
                goal.Z = spot.Z;
            }
            return true;
        }

        static void NoOp() { }

        // A chase that ended away from the target (it fled, died, or the path failed) gives up the place.
        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            if (factions == null || targetEntity == null) return;
            // 0.0.87: from the outer ring, so an attacker waiting there keeps its place in the queue.
            double reach = factions.Spacing.OuterRadius + 1.5;
            double dx = entity.Pos.X - targetEntity.Pos.X, dz = entity.Pos.Z - targetEntity.Pos.Z;
            if (!targetEntity.Alive || dx * dx + dz * dz > reach * reach)
                factions.Spacing.Release(entity.EntityId);
        }

        // A call for help never turns a recruit on its own side, nor takes a guard past its leash, nor sends a fourth
        // NPC onto an enemy NPC that already has its share.
        public override bool Notify(string key, object data)
        {
            // 0.0.128 (owner, 2026-11-13: "the archers seem to rush into the melee even when they start at range"): every
            // ally starting a fight calls the others (joinFight, call for help), and each call restarted an archer's chase
            // for up to SpotSeconds before it noticed it could shoot from where it stood, so repeated calls walked it
            // into the melee. An archer that can already shoot the target from here stays put; its shoot task fights.
            if ((key == "joinLine" || key == "joinFight" || key == "seekEntity") && data is Entity heard && CanShootFromHere(heard)) return false;
            // 0.0.119: rallied into its side's battle line (BattleLineSystem.Rally), which already counted it in, so
            // no crowd or sight check: it takes its slot like any member. A recruit still never turns on its side.
            if ((key == "joinLine" || key == "joinFight") && data is Entity rallyTarget)
            {
                if (!rallyTarget.Alive || BeyondLeash(rallyTarget)) return false;
                if (Recruit.OwnerUidOf(entity) is string rallyOwner
                    && (Recruit.IsTeam(rallyTarget, rallyOwner) || (factions != null && !factions.RecruitWantsToAttack(entity, rallyOwner, rallyTarget)))) return false;
                // Vanilla ContinueExecute gives up on a target beyond NowSeekRange (seekingRange, 24 for warriors), which a
                // rallied NPC's target often is; the next search sets it back.
                NowSeekRange = Math.Max(NowSeekRange, (float)entity.Pos.DistanceTo(rallyTarget.Pos) + 8);
                return base.Notify("seekEntity", rallyTarget);
            }
            if (key == "seekEntity" && data is Entity target)
            {
                if (Recruit.OwnerUidOf(entity) is string owner
                    && (Recruit.IsTeam(target, owner) || (factions != null && !factions.RecruitWantsToAttack(entity, owner, target)) || BeyondLeash(target))) return false;
                // 0.0.136: a charge goes for its target whoever else is on it, seen or not.
                bool ordered = factions?.Hiring != null && factions.Hiring.Charging(entity);
                if (!ordered && factions != null && Ours && Crowded(target)) return false;
                // 0.0.94: a call for help sends only those who can see the attacker.
                if (!ordered && factions != null && Ours && !CanSee(target)) return false;
            }
            return base.Notify(key, data);
        }
    }
}
