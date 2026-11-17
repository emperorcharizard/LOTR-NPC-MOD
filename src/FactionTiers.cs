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
            foreach (string branch in Promotion.AllBranches) table[branch] = Branch(branch, table[Promotion.BaseRank(branch)], health);
            return table;
        }

        /// <summary>A branch's health or damage from its warrior's (0.0.154: a recruit branch's from its recruit's).</summary>
        public static float Branch(string branch, float warrior, bool health)
        {
            double scale = Promotion.GuardLike(branch) == health ? 1.25 : 0.8;
            return health ? Round(warrior * scale) : (float)Math.Round(warrior * scale, 2);
        }

        // 0.0.135 (owner, 2026-11-14: "Equal stats, everyone 3", for PvP of 10 v 10 players with their followers): every
        // tier has the Men's per-rank numbers, 3 followers and a roster of 16; hire prices are the Men's too (the
        // coordinator's call, so equal coin stays fair; easy to undo with Tables134 below). Tiers stay apart for their
        // gear, names and the 0.0.90 history the old config steps compare against.
        // 0.0.153 (owner, 2026-11-16, chose "Stronger captains overall" so a captain beats a rider that beats 2 warriors):
        // captains 96 health and 10.5 damage (were 48 and 7), about 3 times the health x damage. Config version 33.
        // 0.0.155 (owner: players in iron and steel must be able to kill them): back to 48 and 7; the strength stays in
        // NPC fights only (Riders.CaptainNpcFight). Config version 34 moves a captain still at 96 or 10.5 back.
        public const float CaptainHealth152 = 48, CaptainDamage152 = 7, CaptainHealth153 = 96, CaptainDamage153 = 10.5f, CaptainHealth = 48, CaptainDamage = 7f;
        static Dictionary<string, float> MenHealth => R(22, 34, CaptainHealth, 35);
        static Dictionary<string, float> MenDamage => R(3.15f, 3.95f, CaptainDamage, 3.05f);
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
}
