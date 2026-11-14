using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace LotrNpcs
{
    /// <summary>
    /// Battle spacing (0.0.53, user, 2026-11-03: "in combat I have noticed all the npcs clump all together in one spot.
    /// maybe put a minimum distance npcs have to one another when fighting ... I want it to feel more like an actual
    /// battle"). Vanilla seek picks the nearest target and walks onto its position, so a whole group piles onto one or
    /// two enemies. This keeps, for every NPC being fought, which NPCs are chasing or hitting it right now:
    /// - an enemy NPC that already has MaxAttackers other attackers is skipped when looking for a target (players never
    ///   are, the NPC's own target always stays valid, and striking back ignores the limit);
    /// - each attacker gets its own side of its target, evenly spread around it, in the order they stand around it;
    /// - fighting NPCs closer than Spacing to another NPC are pushed apart (EntityBehaviorFactionMember).
    /// Times are server elapsed milliseconds. Server main thread only.
    ///
    /// 0.0.82 (owner, 2026-11-04, after the combat questions: NPC battles "jitter" (turning in place, stepping in and
    /// out) and end in "one whole blob"; "the spacing between npcs might be too far ... there should be a minimum, and a
    /// maximum range"): attackers stand 1.0 to 1.6 blocks from their target's center (DuelMin, DuelMax; they stop
    /// anywhere in that band instead of walking to an exact point), two attackers on one NPC flank it 90 degrees apart
    /// instead of from opposite sides, at most 2 go for one NPC by default (owner: "wrap the flanks", "never more than
    /// 2"), and only NPCs of the same side keep Spacing (1.5 by default) apart; enemies only keep DuelMin, so a fighter
    /// is no longer shoved out of reach by the enemies fighting its neighbours. Lines: BattleLines.cs.
    /// </summary>
    public sealed class BattleSpacing
    {
        /// <summary>How long a claim lasts unless the seek or melee task renews it. Renewed every half second while
        /// chasing and on every swing, so a claim outlives a short pause but not an attacker that left or died.</summary>
        public const long ClaimMs = 6000;
        // 0.0.82: 3 became 2 (config version 17 moves an unchanged 3). 0.0.85 (owner, 2026-11-04: "remove the limit for
        // how many npcs can target 1 npc as that seems to be causing the issue"): no limit by default (config version 18
        // moves an unchanged 2; 17 moves an unchanged 3 straight to no limit). 0.0.102 (owner, 2026-11-09: "Implement it
        // again, do 4 per though"): 4 by default (config version 24 moves an unchanged 0).
        public const int Default085MaxAttackers = 0, DefaultMaxAttackers = 4;
        // 0.0.55 (user, 2026-11-03: "make npcs inclined to spread out when fighting as a big group just a bit more"):
        // 1.5 became 2.0. 0.0.82 (owner: 1.5 blocks between neighbours in a line): back to 1.5, now only between NPCs of
        // the same side (config version 17 moves an unchanged 2.0).
        public const double DefaultSpacing = 1.5, MinSpacing = 0.8, MaxSpacing = 4;
        // 0.0.82 (owner: "1.0 to 1.6 blocks"): how far from its target's center an attacker stands. Melee reaches 2 blocks
        // from the attacker's front to the target's body (0.3 each for our NPCs), so 1.6 is well in reach.
        public const double DefaultDuelMin = 1.0, DefaultDuelMax = 1.6, MinDuel = 0.6, MaxDuel = 2.2;
        /// <summary>Farthest an attacker stands from its target's center. Melee reaches 2 blocks from the attacker's
        /// front to the target's body (0.3 each for our NPCs), so 2.2 keeps every attacker in reach whatever the
        /// spacing.</summary>
        public const double MaxSurroundRadius = 2.2;
        /// <summary>Top speed of the push, in blocks a second, for two NPCs standing in the same spot.</summary>
        public const double PushSpeed = 1.2;

        int maxAttackers = DefaultMaxAttackers;
        double spacing = DefaultSpacing;
        double duelMin = DefaultDuelMin, duelMax = DefaultDuelMax;

        /// <summary>Most NPCs that may go for one NPC at once (others pick another enemy). 0 turns the limit off.</summary>
        public int MaxAttackers { get => maxAttackers; set => maxAttackers = Math.Max(0, value); }
        /// <summary>Minimum distance between fighting NPCs, kept between MinSpacing and MaxSpacing.</summary>
        public double Spacing { get => spacing; set => spacing = ClampSpacing(value); }
        /// <summary>Where an attacker aims to stand from its target's center: the middle of the duel band.</summary>
        public double SurroundRadius => (duelMin + duelMax) / 2;
        /// <summary>The duel band (0.0.82): closer than DuelMin an attacker steps back, farther than DuelMax it closes in,
        /// in between it stands and fights. Set together with SetDuel.</summary>
        public double DuelMin => duelMin;
        public double DuelMax => duelMax;

        public static double ClampSpacing(double value) => double.IsNaN(value) ? DefaultSpacing : GameMath.Clamp(value, MinSpacing, MaxSpacing);

        /// <summary>Sets the duel band, kept within MinDuel to MaxDuel with the maximum at least 0.2 past the minimum.</summary>
        public void SetDuel(double min, double max)
        {
            duelMin = double.IsNaN(min) ? DefaultDuelMin : GameMath.Clamp(min, MinDuel, MaxDuel - 0.2);
            duelMax = double.IsNaN(max) ? DefaultDuelMax : GameMath.Clamp(max, duelMin + 0.2, MaxDuel);
        }

        /// <summary>Is a center distance inside the duel band?</summary>
        public bool InDuelBand(double distance) => distance >= duelMin && distance <= duelMax;

        /// <summary>Live claims on a target other than this attacker's (0.0.82, pairing: a free enemy is taken before
        /// one already being fought).</summary>
        public int OthersOn(long attacker, long target, long now)
        {
            if (!byTarget.TryGetValue(target, out Claims c)) return 0;
            int n = Prune(target, c, now);
            return IndexOf(c, attacker) >= 0 ? n - 1 : n;
        }

        sealed class Claims
        {
            // Attackers in the order they claimed, until when each claim lasts, and each one's last bearing from
            // the target (radians, yaw style: atan2(dx, dz)).
            public long[] Ids = new long[4];
            public long[] Until = new long[4];
            public double[] Bearing = new double[4];
            public int Count;
        }

        // 0.0.98 (owner, 2026-11-08: in a 3 on 1 "they all swing at the same time, but only ones damage is applied"; owner
        // chose staggered swings over skipping the game's hurt window): after any hit the game drops further damage to
        // that entity for 500 ms ("invulnerable" activity, EntityBehaviorHealth). Target -> earliest time the next of our
        // melee hits on it may land, so attackers take turns at least HitGapMs apart instead of hitting in unison.
        public const int HitGapMs = 550, HitJitterMs = 200;
        readonly Dictionary<long, long> nextHitAt = new Dictionary<long, long>();
        readonly List<long> hitSweepKeys = new List<long>();
        long nextHitSweep;

        // Every 10 s drops turns that have passed, so targets that never die or unload through our behavior (players,
        // animals) don't pile up.
        void SweepHits(long now)
        {
            nextHitSweep = now + 10000;
            hitSweepKeys.Clear();
            foreach (KeyValuePair<long, long> kv in nextHitAt) if (kv.Value < now) hitSweepKeys.Add(kv.Key);
            foreach (long key in hitSweepKeys) nextHitAt.Remove(key);
        }

        /// <summary>For tests: how many targets have a booked melee turn.</summary>
        public int TrackedHitTurns => nextHitAt.Count;

        /// <summary>How long (ms) a melee hit that would land at hitAt (world ms) must wait to be clear of the last booked
        /// hit on the target; 0 when it is clear now.</summary>
        public long HitWaitMs(long target, long hitAt) => nextHitAt.TryGetValue(target, out long free) && hitAt < free ? free - hitAt : 0;

        /// <summary>Books a melee hit on the target landing at hitAt: the next one waits HitGapMs plus jitterMs after it.
        /// A later booking for the same hit (the swing landed late, after turning) only ever moves the turn later.</summary>
        public void BookHit(long target, long hitAt, int jitterMs)
        {
            if (hitAt >= nextHitSweep) SweepHits(hitAt);
            long next = hitAt + HitGapMs + Math.Max(0, jitterMs);
            if (!nextHitAt.TryGetValue(target, out long free) || next > free) nextHitAt[target] = next;
        }

        readonly Dictionary<long, Claims> byTarget = new Dictionary<long, Claims>();
        // Attacker -> the target it claimed (one at a time).
        readonly Dictionary<long, long> byAttacker = new Dictionary<long, long>();
        readonly List<long> sweepKeys = new List<long>();
        readonly Stack<Claims> spare = new Stack<Claims>();
        long nextSweep;

        /// <summary>Live claims on a target.</summary>
        public int AttackersOn(long target, long now) => byTarget.TryGetValue(target, out Claims c) ? Prune(target, c, now) : 0;

        /// <summary>The target an attacker has a live claim on, or 0.</summary>
        public long TargetOf(long attacker, long now)
        {
            if (!byAttacker.TryGetValue(attacker, out long target)) return 0;
            if (byTarget.TryGetValue(target, out Claims c))
            {
                Prune(target, c, now);
                if (IndexOf(c, attacker) >= 0) return target;
            }
            byAttacker.Remove(attacker);
            return 0;
        }

        /// <summary>True when the target is an NPC that already has MaxAttackers attackers other than this one. A
        /// target the attacker already claimed is never crowded, and players never are.</summary>
        public bool Crowded(long attacker, long target, bool targetIsPlayer, long now)
        {
            if (maxAttackers <= 0 || targetIsPlayer || !byTarget.TryGetValue(target, out Claims c)) return false;
            int count = Prune(target, c, now);
            if (count < maxAttackers) return false;
            return IndexOf(c, attacker) < 0;
        }

        /// <summary>Claims a target (or renews the claim), dropping the attacker's claim on any other target.</summary>
        public void Claim(long attacker, long target, double bearing, long now)
        {
            if (now >= nextSweep) Sweep(now);
            if (byAttacker.TryGetValue(attacker, out long old) && old != target) Remove(attacker, old);
            byAttacker[attacker] = target;
            if (!byTarget.TryGetValue(target, out Claims c))
            {
                c = spare.Count > 0 ? spare.Pop() : new Claims();
                c.Count = 0;
                byTarget[target] = c;
            }
            int i = IndexOf(c, attacker);
            if (i < 0)
            {
                if (c.Count == c.Ids.Length)
                {
                    Array.Resize(ref c.Ids, c.Count * 2);
                    Array.Resize(ref c.Until, c.Count * 2);
                    Array.Resize(ref c.Bearing, c.Count * 2);
                }
                i = c.Count++;
                c.Ids[i] = attacker;
            }
            c.Until[i] = now + ClaimMs;
            c.Bearing[i] = bearing;
        }

        /// <summary>A swing renews the attacker's claim when it hits what it claimed, and claims the target when the
        /// attacker had none. A swing at someone else (a passer-by) leaves the claim alone.</summary>
        public void Swing(long attacker, long target, double bearing, long now)
        {
            long current = TargetOf(attacker, now);
            if (current == 0 || current == target) Claim(attacker, target, bearing, now);
        }

        /// <summary>Drops the attacker's claim, if it has one.</summary>
        public void Release(long attacker)
        {
            if (byAttacker.TryGetValue(attacker, out long target)) Remove(attacker, target);
        }

        /// <summary>An entity died or left: its claim goes, and so do the claims on it.</summary>
        public void Forget(long id)
        {
            Release(id);
            nextHitAt.Remove(id);
            if (!byTarget.TryGetValue(id, out Claims c)) return;
            for (int i = 0; i < c.Count; i++)
            {
                if (byAttacker.TryGetValue(c.Ids[i], out long t) && t == id) byAttacker.Remove(c.Ids[i]);
            }
            byTarget.Remove(id);
            spare.Push(c);
        }

        /// <summary>
        /// Where around its target this attacker should stand, as an angle (yaw style, from the target), after
        /// recording the attacker's bearing from the target. The live attackers are taken in the order they stand
        /// around the target and spread evenly, turned so that, on average, each moves as little as it can: one
        /// attacker comes straight in, two flank the target from opposite sides, three stand a third of a turn apart.
        /// False if the attacker has no claim on the target.
        /// </summary>
        public bool SurroundAngle(long attacker, long target, double bearing, long now, out double angle) =>
            SurroundAngle(attacker, target, bearing, now, out angle, out _);

        /// <summary>
        /// 0.0.87 (owner's screenshot of a big group on two enemies: one pile; owner: "do your suggested fix"): only as
        /// many attackers as there is Room for stand in the inner ring (the duel band, SurroundRadius); the others, in the
        /// order they came, stand in an outer ring OuterRadius out, facing in, and step in when an inner place opens
        /// (claims are kept in arrival order, so the first outer one moves up). inner: which ring this attacker is in.
        /// Each ring spreads its own attackers as before.
        /// </summary>
        public bool SurroundAngle(long attacker, long target, double bearing, long now, out double angle, out bool inner)
        {
            angle = bearing;
            inner = true;
            if (!byTarget.TryGetValue(target, out Claims c)) return false;
            int n = Prune(target, c, now);
            int me = IndexOf(c, attacker);
            if (me < 0) return false;
            c.Bearing[me] = bearing;
            int room = Room;
            inner = me < room;
            int lo = inner ? 0 : room, hi = inner ? Math.Min(n, room) : n, count = hi - lo;
            if (count <= 1) return true;

            // Rank of each attacker of this ring going round the target from the ring's first claimant's bearing (so the
            // order doesn't jump where angles wrap round; ties go by claim order), and the turn that best fits everyone's
            // bearing to evenly spaced places in that order. 0.0.82: two attackers flank 90 degrees apart (owner: "wrap
            // the flanks"), not from opposite sides; three or more spread all round.
            double step = AngleStep(count);
            double first = c.Bearing[lo], sin = 0, cos = 0;
            int rank = 0;
            for (int i = lo; i < hi; i++)
            {
                double from = Wrap(c.Bearing[i] - first);
                int r = 0;
                for (int j = lo; j < hi; j++)
                {
                    if (j == i) continue;
                    double other = Wrap(c.Bearing[j] - first);
                    if (other < from || (other == from && j < i)) r++;
                }
                if (i == me) rank = r;
                double offset = c.Bearing[i] - step * r;
                sin += Math.Sin(offset);
                cos += Math.Cos(offset);
            }
            double start = Math.Abs(sin) < 1e-9 && Math.Abs(cos) < 1e-9 ? first : Math.Atan2(sin, cos);
            angle = start + step * rank;
            return true;
        }

        /// <summary>How many attackers fit in the inner ring: as many as stand Spacing apart round SurroundRadius
        /// (5 with the defaults), at least one (0.0.87).</summary>
        public int Room => Math.Max(1, (int)Math.Floor(GameMath.TWOPI * SurroundRadius / spacing + 1e-9));
        /// <summary>The outer ring's distance from the target's center: one Spacing beyond the inner ring (2.8 with the
        /// defaults), out of melee reach (0.0.87).</summary>
        public double OuterRadius => SurroundRadius + spacing;

        /// <summary>Is there room for this attacker in the inner ring around the target (it is already in it, or fewer
        /// than Room others are)? (0.0.87: an NPC goes for an enemy with room before one that is full.)</summary>
        public bool HasRoom(long attacker, long target, long now)
        {
            if (!byTarget.TryGetValue(target, out Claims c)) return true;
            int n = Prune(target, c, now);
            int me = IndexOf(c, attacker);
            return me >= 0 ? me < Room : n < Room;
        }

        /// <summary>The angle between neighbouring attackers' places: 90 degrees for two, an even share of the turn
        /// for more.</summary>
        public static double AngleStep(int attackers) => attackers == 2 ? Math.PI / 2 : GameMath.TWOPI / Math.Max(1, attackers);

        static double Wrap(double a)
        {
            a %= GameMath.TWOPI;
            return a < 0 ? a + GameMath.TWOPI : a;
        }

        /// <summary>The point radius blocks from (tx, tz) at the given angle.</summary>
        public static void SpotAt(double tx, double tz, double angle, double radius, out double x, out double z)
        {
            x = tx + Math.Sin(angle) * radius;
            z = tz + Math.Cos(angle) * radius;
        }

        /// <summary>
        /// Adds the push away from one neighbor, (dx, dz) being from the neighbor to this NPC: nothing at spacing or
        /// farther, growing to 1 as they overlap. Two NPCs in exactly the same spot split in a direction picked from
        /// the entity id, so the two go opposite ways. True if the neighbor was too close.
        /// </summary>
        /// <summary>0.0.137 (owner, 2026-11-14: "npcs to receive 1.5x damage in the back"): is a hit from (dx, dz) (from the
        /// NPC to where the hit came from) inside the rear cone of arcDegrees, i.e. more than 180 - arc/2 degrees off the
        /// facing (yaw, the game's convention: forward is (sin yaw, cos yaw))? A hit from the NPC's own spot isn't.</summary>
        public static bool FromBehind(float yaw, double dx, double dz, double arcDegrees)
        {
            double len = Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6 || arcDegrees <= 0) return false;
            double cos = GameMath.Clamp((Math.Sin(yaw) * dx + Math.Cos(yaw) * dz) / len, -1, 1);
            return Math.Acos(cos) * 180 / Math.PI > 180 - Math.Min(arcDegrees, 360) / 2;
        }

        /// <summary>0.0.137: walking backwards: moving (mx, mz) more than BackStepDegrees off the facing.</summary>
        public const double BackStepDegrees = 100, BackWalkSpeed = 0.02, FaceScan = 6;
        public static bool Backing(float yaw, double mx, double mz) => FromBehind(yaw, mx, mz, 2 * (180 - BackStepDegrees));

        /// <summary>0.0.130 (owner, 2026-11-14: "the mobs dont intersect each other, like they have a solid surface"):
        /// how close two NPCs' centers may come (blocks, horizontally). The game has no creature-to-creature collision,
        /// only soft pushes the AI walks through.</summary>
        public const double BodyContact = 0.7, ContactScan = 1.3;

        /// <summary>The step that takes this NPC out of an overlap with one neighbour, (dx, dz) from the neighbour to
        /// it: half the overlap (the neighbour takes the other half on its own tick), nothing at BodyContact or
        /// farther. Two in exactly one spot split by entity id. False when they don't overlap.</summary>
        public static bool ContactShift(double dx, double dz, long tieBreak, out double sx, out double sz)
        {
            sx = sz = 0;
            double d2 = dx * dx + dz * dz;
            if (d2 >= BodyContact * BodyContact) return false;
            double d = Math.Sqrt(d2), nx, nz;
            if (d < 1e-4) { double a = (tieBreak % 360) * Math.PI / 180; nx = Math.Sin(a); nz = Math.Cos(a); }
            else { nx = dx / d; nz = dz / d; }
            double step = (BodyContact - d) / 2;
            sx = nx * step; sz = nz * step;
            return true;
        }

        public static bool AddPush(ref double px, ref double pz, double dx, double dz, double spacing, long tieBreak)
        {
            double d2 = dx * dx + dz * dz;
            if (d2 >= spacing * spacing) return false;
            double d = Math.Sqrt(d2);
            double weight = (spacing - d) / spacing;
            if (d < 0.01)
            {
                double a = (tieBreak * 0.6180339887 % 1) * GameMath.TWOPI;
                px += Math.Sin(a) * weight;
                pz += Math.Cos(a) * weight;
                return true;
            }
            px += dx / d * weight;
            pz += dz / d * weight;
            return true;
        }

        // 0.0.104 (owner: "check their neighbors in a small block radius around themselves, between 4-6"; chose "Push
        // always + 5-block look-ahead"): out of a fight NPCs of one side keep BodySpacing apart; a walking NPC (Motion
        // above WalkingMotion blocks a tick) looks LookAhead blocks ahead within AvoidWidth of its way.
        public const double BodySpacing = 1.0, LookAhead = 5, AvoidWidth = 1.2, BrakeDistance = 1.6, BrakeWidth = 0.6, BrakeShare = 0.15;
        public const double WalkingMotion = 0.004;

        /// <summary>Is this NPC catching up with a neighbour ahead (0.0.104)? Speeds along its heading, in blocks a tick:
        /// the neighbour is slower by more than ClosingShare of its own speed.</summary>
        public const double ClosingShare = 0.3;
        public static bool Closing(double mine, double theirs) => mine > 0 && mine - theirs > mine * ClosingShare;

        /// <summary>
        /// Adds the steer around one neighbour ahead (0.0.104). (hx, hz) is the unit heading, (dx, dz) from this NPC to
        /// the neighbour. Nothing for one behind, farther than LookAhead or more than AvoidWidth off the way; otherwise a
        /// sideways push away from the way it is on, stronger the nearer and the more in the way it is (a neighbour
        /// right on the line splits by the entity id), and a brake for one within BrakeDistance and BrakeWidth. True if
        /// the neighbour was in the way.
        /// </summary>
        public static bool AddAvoid(ref double px, ref double pz, ref double brake, double hx, double hz, double dx, double dz, long tieBreak)
        {
            double ahead = dx * hx + dz * hz;
            if (ahead <= 0 || ahead >= LookAhead) return false;
            double lateral = dx * hz - dz * hx;   // > 0: the neighbour is to the right
            double off = Math.Abs(lateral);
            if (off >= AvoidWidth) return false;
            double weight = (1 - ahead / LookAhead) * (1 - off / AvoidWidth);
            double side = lateral > 0.05 ? -1 : lateral < -0.05 ? 1 : ((tieBreak & 1) == 0 ? 1 : -1);
            // Right of the heading is (hz, -hx); steer to the side away from the neighbour.
            px += side * hz * weight;
            pz += side * -hx * weight;
            if (ahead < BrakeDistance && off < BrakeWidth) brake += 1 - ahead / BrakeDistance;
            return true;
        }

        /// <summary>
        /// Motion to add this tick for a push of strength 1. Vanilla ground physics adds motion and keeps 70% of it
        /// every 1/60 s, so a nudge moves the entity 0.7 / 0.3 = 2.33 times its size in all; spread over the ticks of
        /// a second that makes PushSpeed blocks a second.
        /// </summary>
        public static double PushImpulse(float dt) => PushSpeed * Math.Min(dt, 0.2f) * 0.3 / 0.7;

        // Drops expired claims, keeping the rest in order. Returns how many are left; removes an empty target.
        int Prune(long target, Claims c, long now)
        {
            int kept = 0;
            for (int i = 0; i < c.Count; i++)
            {
                if (c.Until[i] <= now)
                {
                    if (byAttacker.TryGetValue(c.Ids[i], out long t) && t == target) byAttacker.Remove(c.Ids[i]);
                    continue;
                }
                c.Ids[kept] = c.Ids[i];
                c.Until[kept] = c.Until[i];
                c.Bearing[kept] = c.Bearing[i];
                kept++;
            }
            c.Count = kept;
            if (kept == 0 && byTarget.Remove(target)) spare.Push(c);
            return kept;
        }

        void Remove(long attacker, long target)
        {
            byAttacker.Remove(attacker);
            if (!byTarget.TryGetValue(target, out Claims c)) return;
            int i = IndexOf(c, attacker);
            if (i < 0) return;
            c.Until[i] = long.MinValue;
            Prune(target, c, long.MinValue + 1);
        }

        static int IndexOf(Claims c, long attacker)
        {
            for (int i = 0; i < c.Count; i++) if (c.Ids[i] == attacker) return i;
            return -1;
        }

        /// <summary>Every 10 s, drops expired claims on targets nobody looked at since (creatures without our
        /// behavior, targets that walked away).</summary>
        void Sweep(long now)
        {
            nextSweep = now + 10000;
            sweepKeys.Clear();
            foreach (KeyValuePair<long, Claims> kv in byTarget) sweepKeys.Add(kv.Key);
            foreach (long key in sweepKeys)
            {
                if (byTarget.TryGetValue(key, out Claims c)) Prune(key, c, now);
            }
        }

        /// <summary>For tests: how many targets and attackers are tracked.</summary>
        public int TrackedTargets => byTarget.Count;
        public int TrackedAttackers => byAttacker.Count;
    }
}
