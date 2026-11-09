using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace LotrNpcs
{
    /// <summary>
    /// 0.0.109 battle log (owner, 2026-11-09, chose "Yes, add it first": a testing tool before the in-game pass). Off by
    /// default; an admin turns it on with /recruits battlelog on. While on, every melee hit and arrow from a faction NPC,
    /// every faction NPC death, and the time fighting NPCs spend without attacking are counted per faction, and each
    /// event is written as a line of a CSV file in the game's Logs folder. /recruits battlelog shows the totals.
    /// Nothing is kept between restarts.
    /// </summary>
    public class BattleLog
    {
        /// <summary>A fighting NPC that hasn't attacked for this long counts as idle (engaged but not attacking).</summary>
        public const long IdleAfterMs = 5000;

        public class Totals
        {
            public readonly HashSet<long> Npcs = new HashSet<long>();
            public int Swings, SwingsLanded, SwingsLostWindow, SwingsLostOther;
            public int Arrows, ArrowsLanded, ArrowsLostWindow, ArrowsLostOther;
            public double DamageDealt;
            public int Kills, Deaths, TurnWaits;
            public double IdleSeconds;
        }

        public bool On { get; private set; }
        public string FilePath { get; private set; }
        public long StartedMs { get; private set; }
        readonly Dictionary<string, Totals> byFaction = new Dictionary<string, Totals>();
        // The last time each NPC swung or shot, for the idle count.
        readonly Dictionary<long, long> lastAttackMs = new Dictionary<long, long>();
        StreamWriter writer;

        public IReadOnlyDictionary<string, Totals> ByFaction => byFaction;

        public void Start(string folder, long now)
        {
            Stop();
            byFaction.Clear();
            lastAttackMs.Clear();
            StartedMs = now;
            On = true;
            FilePath = null;
            if (folder == null) return;
            try
            {
                Directory.CreateDirectory(folder);
                FilePath = Path.Combine(folder, "lotrnpcs-battle-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv");
                writer = new StreamWriter(FilePath, false, Encoding.UTF8) { AutoFlush = false };
                writer.WriteLine("ms,event,attacker,attackerFaction,attackerRank,target,targetFaction,targetRank,damage,result,targetHealth");
            }
            catch (Exception)
            {
                writer = null;
                FilePath = null;
            }
        }

        public void Stop()
        {
            On = false;
            try { writer?.Flush(); writer?.Dispose(); } catch (Exception) { }
            writer = null;
        }

        public void Flush()
        {
            try { writer?.Flush(); } catch (Exception) { }
        }

        Totals Of(string faction)
        {
            faction ??= "-";
            if (!byFaction.TryGetValue(faction, out Totals t)) byFaction[faction] = t = new Totals();
            return t;
        }

        /// <summary>The result of a hit: landed, lost to the target's hurt window, or lost otherwise (target already
        /// dead, or the damage refused).</summary>
        public enum Result { Landed, LostWindow, LostOther }

        /// <summary>How a hit on target went, read before the hit (window, alive) and from ReceiveDamage's answer.</summary>
        public static Result Classify(bool received, bool aliveBefore, bool windowBefore) =>
            received ? Result.Landed : aliveBefore && windowBefore ? Result.LostWindow : Result.LostOther;

        public void Attack(Entity attacker, Entity target, bool arrow, float damage, Result result, bool killed, long now)
        {
            if (!On || attacker == null) return;
            string faction = FactionSystem.FactionOf(attacker);
            Totals t = Of(faction);
            t.Npcs.Add(attacker.EntityId);
            lastAttackMs[attacker.EntityId] = now;
            if (arrow)
            {
                t.Arrows++;
                if (result == Result.Landed) t.ArrowsLanded++; else if (result == Result.LostWindow) t.ArrowsLostWindow++; else t.ArrowsLostOther++;
            }
            else
            {
                t.Swings++;
                if (result == Result.Landed) t.SwingsLanded++; else if (result == Result.LostWindow) t.SwingsLostWindow++; else t.SwingsLostOther++;
            }
            if (result == Result.Landed) t.DamageDealt += damage;
            if (killed) t.Kills++;
            Write(now, arrow ? "arrow" : "melee", attacker, target, damage, result == Result.Landed ? (killed ? "kill" : "landed") : result == Result.LostWindow ? "lostwindow" : "lost");
        }

        /// <summary>An NPC started a swing or loosed an arrow (the idle count restarts).</summary>
        public void Attacked(Entity attacker, long now)
        {
            if (!On || attacker == null) return;
            lastAttackMs[attacker.EntityId] = now;
        }

        public void TurnWait(Entity attacker)
        {
            if (!On || attacker == null) return;
            Of(FactionSystem.FactionOf(attacker)).TurnWaits++;
        }

        public void Death(Entity npc, Entity killer, long now)
        {
            if (!On || npc == null) return;
            Totals t = Of(FactionSystem.FactionOf(npc));
            t.Npcs.Add(npc.EntityId);
            t.Deaths++;
            lastAttackMs.Remove(npc.EntityId);
            Write(now, "death", killer, npc, 0, "died");
        }

        /// <summary>Called once a second for each faction NPC: a fighting NPC that hasn't attacked for IdleAfterMs adds
        /// the second to its faction's idle time.</summary>
        public void Tick(Entity npc, bool fighting, double seconds, long now)
        {
            if (!On || npc == null || !fighting) return;
            Totals t = Of(FactionSystem.FactionOf(npc));
            t.Npcs.Add(npc.EntityId);
            if (!lastAttackMs.TryGetValue(npc.EntityId, out long last)) lastAttackMs[npc.EntityId] = last = now;
            if (now - last >= IdleAfterMs) t.IdleSeconds += seconds;
        }

        void Write(long now, string kind, Entity attacker, Entity target, float damage, string result)
        {
            if (writer == null) return;
            try
            {
                string health = "";
                var tree = target?.WatchedAttributes?.GetTreeAttribute("health");
                if (tree != null) health = tree.GetFloat("currenthealth").ToString("0.##", CultureInfo.InvariantCulture);
                writer.WriteLine(string.Join(",",
                    (now - StartedMs).ToString(CultureInfo.InvariantCulture), kind,
                    attacker?.EntityId.ToString(CultureInfo.InvariantCulture) ?? "", Csv(attacker == null ? null : FactionSystem.FactionOf(attacker) ?? attacker.Code?.ToShortString()), Csv(attacker == null ? null : EntityBehaviorFactionMember.RankOf(attacker)),
                    target?.EntityId.ToString(CultureInfo.InvariantCulture) ?? "", Csv(target == null ? null : FactionSystem.FactionOf(target) ?? (target is EntityPlayer ? "player" : target.Code?.ToShortString())), Csv(target == null ? null : EntityBehaviorFactionMember.RankOf(target)),
                    damage.ToString("0.##", CultureInfo.InvariantCulture), result, health));
            }
            catch (Exception) { }
        }

        static string Csv(string s) => s == null ? "" : s.Replace(",", ";");

        /// <summary>The totals so far, one line per faction, for chat.</summary>
        public string Describe(long now)
        {
            var sb = new StringBuilder();
            sb.Append("Battle log ").Append(On ? "on" : "off").Append(", ").Append(((now - StartedMs) / 1000.0).ToString("0", CultureInfo.InvariantCulture)).Append(" s");
            if (FilePath != null) sb.Append(", file ").Append(FilePath);
            sb.Append('.');
            if (byFaction.Count == 0) return sb.Append(" Nothing recorded yet.").ToString();
            foreach (var kv in byFaction.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                Totals t = kv.Value;
                sb.Append('\n').Append(kv.Key).Append(": ").Append(t.Npcs.Count).Append(" NPCs");
                if (t.Swings > 0)
                {
                    sb.Append("; melee ").Append(t.SwingsLanded).Append('/').Append(t.Swings).Append(" landed");
                    if (t.SwingsLostWindow > 0) sb.Append(" (").Append(t.SwingsLostWindow).Append(" lost to hurt window)");
                }
                if (t.Arrows > 0)
                {
                    sb.Append("; arrows ").Append(t.ArrowsLanded).Append('/').Append(t.Arrows).Append(" landed");
                    if (t.ArrowsLostWindow > 0) sb.Append(" (").Append(t.ArrowsLostWindow).Append(" lost to hurt window)");
                }
                sb.Append("; damage ").Append(t.DamageDealt.ToString("0", CultureInfo.InvariantCulture))
                    .Append("; kills ").Append(t.Kills).Append(", deaths ").Append(t.Deaths)
                    .Append("; idle while fighting ").Append(t.IdleSeconds.ToString("0", CultureInfo.InvariantCulture)).Append(" NPC-seconds");
                if (t.TurnWaits > 0) sb.Append("; waited for a turn ").Append(t.TurnWaits).Append(" times");
            }
            return sb.ToString();
        }
    }
}
