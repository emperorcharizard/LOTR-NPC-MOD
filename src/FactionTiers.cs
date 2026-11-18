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
    /// version 19 files) the old multipliers.</summary>
    public sealed class FactionTier
    {
        public string Name;
        public int Followers, Roster;
        public Dictionary<string, float> Health, Damage;
        public float Health090, Damage090;
        public Dictionary<string, int> Hire, Promote;
        // The promotion prices shipped with config versions 19-21, for the version 22 migration.
        public Dictionary<string, int> Promote090;
    }

    /// <summary>
    /// The balance tiers (Men, Uruk, Levy, Orc, Goblin, Elf, Dwarf). Today every tier has the Men's numbers, 3
    /// followers, a roster of 16 and the Men's prices, so factions are equal and battles are decided by numbers and
    /// tactics. Tiers stay apart for gear and names, and their older tables are kept below because the config
    /// migrations compare files against them. The Shire and factions an admin adds use the Men's.
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
                Name = name, Followers = followers, Roster = roster, Health090 = health090, Damage090 = damage090, Health = health, Damage = damage,
                Hire = hire, Promote = PromoteFor(hire),
                Promote090 = new Dictionary<string, int> { ["warrior"] = promoteWarrior, ["archer"] = promoteArcher, ["captain"] = promoteCaptain },
            };
        }

        /// <summary>The ranks a promotion leads to.</summary>
        public static readonly string[] PromoteRanks = { "warrior", "archer", "captain" };
        // A promotion costs the hire price of the rank it leads to, less 3 coins (less 4 for a captain), never below
        // 0.
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

        // Every tier has the Men's per-rank numbers, 3 followers and a roster of 16, and the Men's hire prices
        // (Tables134 below holds the last unequal set). Captains are 48 health and 7 damage; in fights with other
        // NPCs they hit x1.5 and take x0.5 (Riders.CaptainNpcFight), so players in iron and steel can still kill
        // them. CaptainHealth153/CaptainDamage153 are config version 33's stronger captains, which version 34 moves
        // back.
        public const float CaptainHealth152 = 48, CaptainDamage152 = 7, CaptainHealth153 = 96, CaptainDamage153 = 10.5f, CaptainHealth = 48, CaptainDamage = 7f;
        static Dictionary<string, float> MenHealth => R(22, 34, CaptainHealth, 35);
        static Dictionary<string, float> MenDamage => R(3.15f, 3.95f, CaptainDamage, 3.05f);
        public const int EqualFollowers = 3, EqualRoster = 16;
        public static readonly FactionTier Men = T("men", EqualFollowers, EqualRoster, 1f, 1f, MenHealth, MenDamage, 5, 15, 40, 15, 10, 10, 30);
        public static readonly FactionTier Uruk = T("uruk", EqualFollowers, EqualRoster, 1.05f, 1.03f, MenHealth, MenDamage, 5, 15, 40, 15, 11, 11, 33);
        public static readonly FactionTier Levy = T("levy", EqualFollowers, EqualRoster, 0.85f, 0.85f, MenHealth, MenDamage, 5, 15, 40, 15, 8, 8, 25);
        // The orc tables of config version 25 (about 0.78 of a Man's health x damage), for the migrations.
        public static readonly Dictionary<string, float> OrcHealth091 = R(15, 27, 31, 22), OrcDamage091 = R(2.5f, 2.8f, 5.95f, 2.45f);
        public static readonly FactionTier Orc = T("orc", EqualFollowers, EqualRoster, 0.66f, 0.85f, MenHealth, MenDamage, 5, 15, 40, 15, 7, 7, 21);
        public static readonly FactionTier Goblin = T("goblin", EqualFollowers, EqualRoster, 0.57f, 0.73f, MenHealth, MenDamage, 5, 15, 40, 15, 6, 6, 19);
        public static readonly FactionTier Elf = T("elf", EqualFollowers, EqualRoster, 1.14f, 1.35f, MenHealth, MenDamage, 5, 15, 40, 15, 13, 13, 38);
        public static readonly FactionTier Dwarf = T("dwarf", EqualFollowers, EqualRoster, 1.33f, 1.15f, MenHealth, MenDamage, 5, 15, 40, 15, 13, 13, 38);

        /// <summary>The tables, hire prices, follow limit and roster shipped before the factions were made equal (at
        /// the old baseline of 5 followers), for config version 31: values still exactly these move to the equal
        /// ones.</summary>
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

        // The tables of config versions 25-26, for the version 27 migration:
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

        // The tables and hire prices of config version 27, for the version 28 migration:
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
            ["woodelves"] = Elf,
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

        /// <summary>One hit's damage with the spread: times a random 1 - spread to 1 + spread. roll is 0 to
        /// 1.</summary>
        public static float Spread(float damage, double spread, double roll) =>
            spread <= 0 ? damage : (float)(damage * (1 + spread * (2 * roll - 1)));
    }
}
