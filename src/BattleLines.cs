using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace LotrNpcs
{
    /// <summary>
    /// The geometry of a battle line (0.0.82), kept apart from the game so it can be tested offline. A line has a center
    /// P and a unit direction D toward the enemy; its right is (D.z, -D.x). Slots are numbered front rank first, left to
    /// right, then the extras on the flanks (0.0.110; before, a rear rank one Gap behind).
    /// </summary>
    public static class LineMath
    {
        /// <summary>How many members stand in the front rank: perEnemy for each enemy (0.0.82: 2, owner: extras "wrap the
        /// flanks", "never more than 2" on one enemy), at least one, at most every member; the rest are the extras.
        /// 0.0.85 (owner: "remove the limit for how many npcs can target 1 npc"): perEnemy is MaxAttackersPerTarget, 0 by
        /// default, and 0 puts every member in the front rank.
        /// 0.0.104 (owner, 2026-11-09: lines "stay in that line until they outnumber the enemy where they start to
        /// surround them"): one front place per enemy (LinesPerEnemy); the members past that are the side's extras.</summary>
        public const int LinesPerEnemy = 1;
        public static int FrontCount(int members, int enemies, int perEnemy = LinesPerEnemy) =>
            members <= 0 ? 0 : perEnemy <= 0 ? members : Math.Clamp(perEnemy * Math.Max(1, enemies), 1, members);

        /// <summary>A slot's place across the line (right of the center, in blocks) and behind it (always 0).
        /// 0.0.110 (owner, after the 0.0.109 test: "extend flanks"): the extras no longer wait in a rear rank behind the
        /// middle, from where they swarmed the enemies their own front was fighting; they lengthen the line, the left
        /// half of them past its left end and the rest past its right, so at contact they wrap the enemy's end men.
        /// 0.0.112 (owner, review Q13: captains "held back as reserve"): the last `reserve` members (the captains) stand one
        /// gap behind the middle of the line, side by side, until the lines meet.</summary>
        public static void SlotOffset(int index, int members, int front, double gap, out double right, out double back, int reserve = 0)
        {
            back = 0;
            if (reserve > 0 && index >= members - reserve)
            {
                right = (index - (members - reserve) - (reserve - 1) / 2.0) * gap;
                back = gap;
                return;
            }
            members -= reserve;
            double half = (front - 1) / 2.0;
            if (index < front) { right = (index - half) * gap; return; }
            int j = index - front, left = (members - front) / 2;
            right = j < left ? -(half + left - j) * gap : (half + 1 + j - left) * gap;
        }

        /// <summary>The point right and back of (px, pz) for direction (dx, dz).</summary>
        public static void SlotAt(double px, double pz, double dx, double dz, double right, double back, out double x, out double z)
        {
            x = px + dz * right - dx * back;
            z = pz - dx * right - dz * back;
        }

        /// <summary>
        /// The order members take the slots in (0.0.110, one rank): everyone left to right by where they stand across
        /// the line, so nobody walks through a neighbour to reach their slot; the `front` in the middle come first, then
        /// the left flank's extras, then the right's (the order SlotOffset numbers them in). Returns indices into xs/zs.
        /// </summary>
        public static int[] Arrange(IReadOnlyList<double> xs, IReadOnlyList<double> zs, double px, double pz, double dx, double dz, int front)
        {
            int n = xs.Count;
            int[] byRight = new int[n];
            for (int i = 0; i < n; i++) byRight[i] = i;
            Array.Sort(byRight, (a, b) =>
            {
                double ra = (xs[a] - px) * dz - (zs[a] - pz) * dx, rb = (xs[b] - px) * dz - (zs[b] - pz) * dx;
                int c = ra.CompareTo(rb);
                return c != 0 ? c : a.CompareTo(b);
            });
            front = Math.Clamp(front, 0, n);
            int left = (n - front) / 2;
            int[] order = new int[n];
            Array.Copy(byRight, left, order, 0, front);
            Array.Copy(byRight, 0, order, front, left);
            Array.Copy(byRight, left + front, order, front + left, n - front - left);
            return order;
        }

        /// <summary>How far an advancing line's center moves this update: up to step, never closer to the enemy than
        /// contact, and not at all while its members lag more than maxLag behind their slots on average (so the line
        /// moves at the pace of its slowest members instead of running off without them).</summary>
        public static double Advance(double toEnemy, double contact, double step, double meanLag, double maxLag) =>
            meanLag > maxLag ? 0 : Math.Max(0, Math.Min(step, toEnemy - contact));

        /// <summary>Have the lines met (0.0.83)? True as soon as any of the first
        /// `front` members is within `engage` of its enemy (distances in slot order, NaN = no enemy). Before, the line
        /// only counted as met when its center was 3 blocks from the enemy's middle, and a front member that had gone to
        /// fight counted as lagging behind its slot, which stopped the march: the rest of the front stood in their slots
        /// until an enemy came within reach or hit them (owner's 0.0.82 test: aggressive recruits "not fighting nearby
        /// hostile factions until they are hit first").</summary>
        public static bool FrontEngaged(IReadOnlyList<double> targetDistances, int front, double engage)
        {
            for (int i = 0; i < Math.Min(front, targetDistances.Count); i++)
                if (targetDistances[i] <= engage) return true;
            return false;
        }

        /// <summary>Does a member stay in its line? While fighting, or for MemberGraceMs after it last picked a target:
        /// vanilla's seek task searches only every second or two, at random, so the 3 second fighting mark alone dropped
        /// members waiting in their slots.</summary>
        public static bool StillMember(bool fighting, long now, long lastJoinMs, long graceMs) => fighting || now - lastJoinMs <= graceMs;

        /// <summary>(x, z) turned toward (tx, tz) by share (0 to 1) of the way, kept a unit vector.</summary>
        public static void Blend(ref double x, ref double z, double tx, double tz, double share)
        {
            double nx = x * (1 - share) + tx * share, nz = z * (1 - share) + tz * share;
            double len = Math.Sqrt(nx * nx + nz * nz);
            if (len < 1e-6) return;
            x = nx / len;
            z = nz / len;
        }
    }

    /// <summary>One side's line in one fight.</summary>
    public sealed class BattleLine
    {
        public long Id;
        /// <summary>Who may join: "o:" + the leader's uid for formation followers, "f:" + the faction for wild NPCs.</summary>
        public string Side;
        /// <summary>Marches to meet the enemy (wild NPCs, aggressive and raid recruits) or holds its ground (defensive).</summary>
        public bool Advances;
        public double Px, Py, Pz, Dx, Dz;
        /// <summary>Entity ids in slot order: front rank first.</summary>
        public readonly List<long> Members = new List<long>();
        public int Front;
        /// <summary>0.0.112: captains held back behind the line until contact (the last Reserve members).</summary>
        public int Reserve;
        /// <summary>The lines have met: the front rank fights wherever it is.</summary>
        public bool Contact;
        public long CreatedMs, LastEnemyMs;
        public bool Dirty = true;
        public double Ex, Ez;
        public int Enemies;
    }

    /// <summary>
    /// Battle lines (0.0.82, owner, 2026-11-04, combat questions 6 to 13: "lines that pair off"; outnumbered extras
    /// "wrap the flanks", never more than 2 on one enemy; "everyone but Loose" forms lines: wild NPCs and recruits
    /// following in any formation but Loose, while Loose followers, guards, archers and NPCs fighting players or monsters
    /// just duel; "by stance": wild NPCs and aggressive or raid recruits advance, defensive recruits hold until the enemy
    /// is within LineHoldRange; 1.5 blocks between neighbours).
    ///
    /// An NPC of a side that forms lines joins its side's nearest line within JoinRadius when its seek task picks an
    /// enemy NPC, or starts one there facing that enemy. Twice a second each line: drops members that died, left or
    /// stopped fighting; finds the enemy from its members' targets; for its first FormingMs takes its members' middle as
    /// its center, then marches that center toward the enemy (LineMath.Advance) until ContactDistance; gives the front
    /// rank one slot per enemy and puts the rest on its flanks (0.0.110; before, in a rear rank). A front member stands in its slot until an enemy is
    /// within EngageRange (holding lines: LineHoldRange) or the lines have met, then fights as usual (it takes a free
    /// enemy first, see AiTaskFactionSeek). An extra stays in its slot unless an enemy comes within RearStrikeRange;
    /// when a front member falls, the line closes up at the next arrangement.
    /// Server main thread only.
    /// </summary>
    public sealed class BattleLineSystem
    {
        public const double JoinRadius = 12, ContactDistance = 3, EngageRange = 3.5, RearStrikeRange = 2, MetNearRadius = 2 * JoinRadius;
        public const double StepPerUpdate = 0.6, MaxLag = 1.5, EnemyScanRadius = 8;
        public const long FormingMs = 1500, LoseEnemyMs = 4000, UpdateMs = 500, MemberGraceMs = 8000;
        public const double TurnShare = 0.2;

        readonly FactionSystem factions;
        readonly Dictionary<long, BattleLine> lineOf = new Dictionary<long, BattleLine>();
        readonly List<BattleLine> lines = new List<BattleLine>();
        readonly Dictionary<long, long> targetOf = new Dictionary<long, long>();
        readonly Dictionary<long, long> lastJoin = new Dictionary<long, long>();
        long nextId = 1;

        public BattleLineSystem(FactionSystem factions) { this.factions = factions; }

        public bool Enabled = true;
        public double Gap = BattleSpacing.DefaultSpacing;
        public double HoldRange = 6;
        /// <summary>Front-rank places per enemy: MaxAttackersPerTarget (0 = everyone in front, 0.0.85).</summary>
        public int PerEnemy = BattleSpacing.DefaultMaxAttackers;

        public int LineCount => lines.Count;
        public BattleLine LineOf(long id) => lineOf.TryGetValue(id, out BattleLine line) ? line : null;
        /// <summary>The enemy this member last chased, or 0.</summary>
        public long TargetOf(long id) => targetOf.TryGetValue(id, out long t) ? t : 0;

        /// <summary>The side an NPC forms lines with, or null when it doesn't (Loose, holding or guarding recruits,
        /// archers, creatures without a faction).</summary>
        /// <summary>0.0.122: do wild NPCs form lines (config WildBattleLines, false by default; the tests turn it on).</summary>
        public static bool WildLines = true;

        public static string SideOf(Entity npc)
        {
            if (npc == null || npc is EntityPlayer || Recruit.IsArcher(npc)) return null;
            if (Recruit.OwnerUidOf(npc) != null)
            {
                if (Recruit.OrderOf(npc) != Recruit.Follow || Recruit.FormationOf(npc) == Formation.Loose || HiringSystem.ChargingId(npc.EntityId)) return null;
                return "o:" + Recruit.LeaderUidOf(npc);
            }
            // 0.0.122 (owner, 2026-11-13: "I just want combat to be as smooth as possible and as optimized lag wise as
            // possible", "not too many conflicting paths"): wild NPCs don't form lines unless WildBattleLines is on; they
            // take the nearest enemy with room and wait on the outer ring when it's full (BattleSpacing).
            if (!WildLines) return null;
            string faction = FactionSystem.FactionOf(npc);
            return faction == null ? null : "f:" + faction;
        }

        /// <summary>Wild NPCs and aggressive or raid recruits march; defensive recruits hold.</summary>
        public static bool AdvancesOf(Entity npc) => Recruit.OwnerUidOf(npc) == null || Recruit.StanceOf(npc) != Recruit.Defensive;

        /// <summary>Does this NPC fighting this target belong in a line? Only enemy NPCs of the mod make lines; players
        /// and monsters are fought the usual way.</summary>
        public bool Wants(Entity npc, Entity target) =>
            Enabled && target != null && !(target is EntityPlayer) && FactionSystem.FactionOf(target) != null && SideOf(npc) != null;

        /// <summary>Called by the seek task when the NPC picks a target: joins (or starts) its side's line, or leaves
        /// any line when it shouldn't be in one.</summary>
        public BattleLine Join(Entity npc, Entity target, long now)
        {
            long id = npc.EntityId;
            if (!Wants(npc, target)) { Leave(id); return null; }
            targetOf[id] = target.EntityId;
            lastJoin[id] = now;
            string side = SideOf(npc);
            if (lineOf.TryGetValue(id, out BattleLine line))
            {
                if (line.Side == side) return line;
                Leave(id);
            }
            line = null;
            double best = JoinRadius * JoinRadius;
            foreach (BattleLine l in lines)
            {
                if (l.Side != side) continue;
                double dx = l.Px - npc.Pos.X, dz = l.Pz - npc.Pos.Z;
                double d = dx * dx + dz * dz;
                if (d < best) { best = d; line = l; }
            }
            // 0.0.114: a straggler further out joins its side's line already fighting near its enemy instead of
            // starting a line of one, which marched in at the line's pace, alone (battle log 2026-11-11, goblin 5205
            // 17 blocks out, about 20 s to arrive).
            best = JoinRadius * JoinRadius;
            if (line == null)
                foreach (BattleLine l in lines)
                {
                    if (l.Side != side) continue;
                    double dx = l.Ex - target.Pos.X, dz = l.Ez - target.Pos.Z;
                    double d = dx * dx + dz * dz;
                    if (d < best) { best = d; line = l; }
                }
            if (line == null)
            {
                double ex = target.Pos.X - npc.Pos.X, ez = target.Pos.Z - npc.Pos.Z, len = Math.Sqrt(ex * ex + ez * ez);
                line = new BattleLine
                {
                    Id = nextId++, Side = side, Advances = AdvancesOf(npc), CreatedMs = now, LastEnemyMs = now,
                    Px = npc.Pos.X, Py = npc.Pos.InternalY, Pz = npc.Pos.Z,
                    Dx = len > 1e-6 ? ex / len : 0, Dz = len > 1e-6 ? ez / len : 1,
                    Ex = target.Pos.X, Ez = target.Pos.Z, Enemies = 1,
                };
                lines.Add(line);
            }
            Add(line, id, target.EntityId, now);
            return line;
        }

        void Add(BattleLine line, long id, long target, long now)
        {
            targetOf[id] = target;
            lastJoin[id] = now;
            line.Members.Add(id);
            line.Front = LineMath.FrontCount(line.Members.Count, line.Enemies);
            line.Dirty = true;
            lineOf[id] = line;
        }

        public void Leave(long id)
        {
            if (!lineOf.TryGetValue(id, out BattleLine line)) return;
            lineOf.Remove(id);
            line.Members.Remove(id);
            line.Dirty = true;
            if (line.Members.Count == 0) lines.Remove(line);
        }

        static bool IsCaptain(Entity e) => e != null && NpcTypeInfo.Of(e)?.Rank == "captain";

        /// <summary>An entity died or left.</summary>
        public void Forget(long id)
        {
            Leave(id);
            targetOf.Remove(id);
            lastJoin.Remove(id);
        }

        /// <summary>
        /// Where this member should stand instead of fighting, or false when it should fight (not in a line, front rank
        /// in reach of an enemy or the lines have met, an extra with an enemy right on it).
        /// </summary>
        public bool HoldSlot(Entity npc, Entity target, out double x, out double z)
        {
            x = z = 0;
            if (!lineOf.TryGetValue(npc.EntityId, out BattleLine line)) return false;
            int i = line.Members.IndexOf(npc.EntityId);
            if (i < 0) return false;
            double tx = target.Pos.X - npc.Pos.X, tz = target.Pos.Z - npc.Pos.Z, td = Math.Sqrt(tx * tx + tz * tz);
            if (i < line.Front)
            {
                if (line.Contact || td <= (line.Advances ? EngageRange : HoldRange)) return false;
            }
            // 0.0.104: once the lines have met, the extras (the side outnumbers the enemy) go round to surround;
            // 0.0.110: from the flanks, so the nearest enemies with room are the end men.
            else if (line.Contact || td <= RearStrikeRange) return false;
            LineMath.SlotOffset(i, line.Members.Count, line.Front, Gap, out double right, out double back, line.Reserve);
            LineMath.SlotAt(line.Px, line.Pz, line.Dx, line.Dz, right, back, out x, out z);
            return true;
        }

        /// <summary>
        /// 0.0.104 (owner: "stay in that line"): a front member of a line that has met the enemy keeps its place in the
        /// line while it fights: it stands on its own side of its enemy, facing the way the line faces, instead of
        /// walking round it. (dx, dz) is the line's facing. False for others (extras surround, the rest as before).
        /// </summary>
        public bool HoldsFront(Entity npc, out double dx, out double dz)
        {
            dx = dz = 0;
            if (!lineOf.TryGetValue(npc.EntityId, out BattleLine line) || !line.Contact) return false;
            int i = line.Members.IndexOf(npc.EntityId);
            if (i < 0 || i >= line.Front) return false;
            dx = line.Dx; dz = line.Dz;
            return true;
        }

        // Scratch lists for Update (main thread only).
        readonly List<BattleLine> scratchLines = new List<BattleLine>();
        readonly List<double> xs = new List<double>(), zs = new List<double>();
        readonly List<long> ids = new List<long>();
        readonly List<double> dists = new List<double>();
        readonly HashSet<long> seenTargets = new HashSet<long>();

        /// <summary>Twice a second (FactionSystem's tick).</summary>
        public void Update(IWorldAccessor world, long now)
        {
            scratchLines.Clear();
            scratchLines.AddRange(lines);
            foreach (BattleLine line in scratchLines) UpdateLine(world, line, now);
        }

        void UpdateLine(IWorldAccessor world, BattleLine line, long now)
        {
            // Members that died, unloaded, changed side or stopped fighting leave.
            for (int i = line.Members.Count - 1; i >= 0; i--)
            {
                long id = line.Members[i];
                Entity e = world.GetEntityById(id);
                bool fighting = e?.GetBehavior<EntityBehaviorFactionMember>()?.Fighting(now) == true;
                if (e == null || !e.Alive || SideOf(e) != line.Side || !LineMath.StillMember(fighting, now, lastJoin.TryGetValue(id, out long joined) ? joined : 0, MemberGraceMs))
                {
                    line.Members.RemoveAt(i);
                    lineOf.Remove(id);
                            line.Dirty = true;
                }
            }
            if (line.Members.Count == 0) { lines.Remove(line); return; }

            // The enemy: the middle of what the members are after, and how many enemy NPCs stand around there.
            Entity founder = world.GetEntityById(line.Members[0]);
            double ex = 0, ez = 0;
            int targets = 0;
            seenTargets.Clear();
            foreach (long id in line.Members)
            {
                long t = TargetOf(id);
                if (t == 0 || !seenTargets.Add(t)) continue;
                Entity te = world.GetEntityById(t);
                if (te == null || !te.Alive) continue;
                ex += te.Pos.X; ez += te.Pos.Z; targets++;
            }
            if (targets == 0)
            {
                if (now - line.LastEnemyMs > LoseEnemyMs) Disband(line);
                return;
            }
            line.LastEnemyMs = now;
            line.Ex = ex / targets;
            line.Ez = ez / targets;
            int enemies = targets;
            if (founder != null && factions != null)
            {
                int around = 0;
                foreach (Entity e in world.GetEntitiesAround(new Vintagestory.API.MathTools.Vec3d(line.Ex, line.Py, line.Ez), (float)EnemyScanRadius, 4,
                             e => e.Alive && !(e is EntityPlayer) && FactionSystem.FactionOf(e) != null && SideOf(e) != line.Side && factions.IsEnemyOf(founder, e)))
                    around++;
                enemies = Math.Max(enemies, around);
            }
            line.Enemies = enemies;
            if (founder != null) Rally(world, line, founder, now);

            // Center and facing.
            double mx = 0, mz = 0, my = 0;
            foreach (long id in line.Members)
            {
                Entity e = world.GetEntityById(id);
                mx += e.Pos.X; mz += e.Pos.Z; my += e.Pos.InternalY;
            }
            int n = line.Members.Count;
            mx /= n; mz /= n; my /= n;
            double toX = line.Ex - line.Px, toZ = line.Ez - line.Pz;
            double toEnemy = Math.Sqrt(toX * toX + toZ * toZ);
            if (now - line.CreatedMs < FormingMs)
            {
                line.Px = mx; line.Pz = mz; line.Py = my;
                toX = line.Ex - mx; toZ = line.Ez - mz;
                toEnemy = Math.Sqrt(toX * toX + toZ * toZ);
                if (toEnemy > 1e-6) { line.Dx = toX / toEnemy; line.Dz = toZ / toEnemy; }
            }
            else if (toEnemy > 1e-6 && !line.Contact)
            {
                LineMath.Blend(ref line.Dx, ref line.Dz, toX / toEnemy, toZ / toEnemy, TurnShare);
            }
            line.Py = my;

            // Ranks.
            // 0.0.112: captains are the reserve, unless the line is nothing but captains.
            int reserve = 0;
            foreach (long id in line.Members) if (IsCaptain(world.GetEntityById(id))) reserve++;
            if (reserve == n) reserve = 0;
            int front = LineMath.FrontCount(n - reserve, line.Enemies);
            if (front != line.Front || reserve != line.Reserve) { line.Front = front; line.Reserve = reserve; line.Dirty = true; }
            if (line.Dirty)
            {
                // The line arranged first, then the reserve behind it, each left to right.
                xs.Clear(); zs.Clear(); ids.Clear();
                foreach (bool captains in new[] { false, true })
                {
                    foreach (long id in line.Members)
                    {
                        Entity e = world.GetEntityById(id);
                        if ((reserve > 0 && IsCaptain(e)) != captains) continue;
                        xs.Add(e.Pos.X); zs.Add(e.Pos.Z); ids.Add(id);
                    }
                }
                int lineCount = n - reserve;
                int[] order = LineMath.Arrange(xs.GetRange(0, lineCount), zs.GetRange(0, lineCount), line.Px, line.Pz, line.Dx, line.Dz, front);
                int[] rear = LineMath.Arrange(xs.GetRange(lineCount, reserve), zs.GetRange(lineCount, reserve), line.Px, line.Pz, line.Dx, line.Dz, reserve);
                line.Members.Clear();
                foreach (int k in order) line.Members.Add(ids[k]);
                foreach (int k in rear) line.Members.Add(ids[lineCount + k]);
                line.Dirty = false;
            }

            // 0.0.119: a member with no enemy in its seekingRange (rallied ones) stops its seek task at its place and
            // never restarts it, so it stood still while its place marched on and held the whole line back (the
            // march waits for laggards). Send it on whenever its place has moved off.
            if (!line.Contact)
                for (int i = 0; i < n; i++)
                {
                    Entity e = world.GetEntityById(line.Members[i]), t = world.GetEntityById(TargetOf(line.Members[i]));
                    if (e == null || t == null || !t.Alive || SeekRunning(e)) continue;
                    LineMath.SlotOffset(i, n, line.Front, Gap, out double right, out double back, line.Reserve);
                    LineMath.SlotAt(line.Px, line.Pz, line.Dx, line.Dz, right, back, out double sx, out double sz);
                    if ((e.Pos.X - sx) * (e.Pos.X - sx) + (e.Pos.Z - sz) * (e.Pos.Z - sz) > 1) e.Notify("joinLine", t);
                }

            // Met: once any front member is in reach of its enemy, the whole front fights, starting now.
            if (!line.Contact)
            {
                dists.Clear();
                foreach (long id in line.Members)
                {
                    Entity e = world.GetEntityById(id), t = world.GetEntityById(TargetOf(id));
                    dists.Add(t == null || !t.Alive ? double.NaN : Math.Sqrt((t.Pos.X - e.Pos.X) * (t.Pos.X - e.Pos.X) + (t.Pos.Z - e.Pos.Z) * (t.Pos.Z - e.Pos.Z)));
                }
                if (LineMath.FrontEngaged(dists, line.Front, line.Advances ? EngageRange : HoldRange) || SideMetNear(line))
                {
                    line.Contact = true;
                    Wake(world, line);
                }
            }

            // March or hold.
            if (line.Contact) return;
            if (line.Advances)
            {
                if (toEnemy <= ContactDistance) { line.Contact = true; return; }
                if (now - line.CreatedMs < FormingMs) return;
                double lag = 0;
                for (int i = 0; i < n; i++)
                {
                    Entity e = world.GetEntityById(line.Members[i]);
                    LineMath.SlotOffset(i, n, line.Front, Gap, out double right, out double back, line.Reserve);
                    LineMath.SlotAt(line.Px, line.Pz, line.Dx, line.Dz, right, back, out double sx, out double sz);
                    lag += Math.Sqrt((e.Pos.X - sx) * (e.Pos.X - sx) + (e.Pos.Z - sz) * (e.Pos.Z - sz));
                }
                double move = LineMath.Advance(toEnemy, ContactDistance, StepPerUpdate, lag / n, MaxLag);
                line.Px += line.Dx * move;
                line.Pz += line.Dz * move;
            }
            else if (toEnemy <= HoldRange) { line.Contact = true; Wake(world, line); }
        }

        /// <summary>0.0.119 (owner, 2026-11-13: allies join a forming line "only if they are set in the same formation"):
        /// can this NPC be rallied into a line of this side and formation? Idle (not fighting, in no line), same side
        /// (same faction for wild NPCs, same leader for recruits) and same formation setting.</summary>
        public bool CanRally(Entity ally, string side, string formation, long now) =>
            ally != null && ally.Alive && !lineOf.ContainsKey(ally.EntityId) && SideOf(ally) == side && Recruit.FormationOf(ally) == formation
            && (ally.World == null || ally.GetBehavior<EntityBehaviorFactionMember>()?.Fighting(now) != true); // no world: test entities, no behaviours

        // Battle log 2026-11-13: an army spread wider than its seekingRange (24) fought with only the NPCs near an
        // enemy; 24 of 64 never noticed the battle. Idle allies near the line now join it, each taking the target of
        // the member nearest to it, and the next update rallies those near them in turn.
        readonly List<Entity> rallied = new List<Entity>();
        void Rally(IWorldAccessor world, BattleLine line, Entity founder, long now)
        {
            string formation = Recruit.FormationOf(founder);
            double reach = JoinRadius + line.Members.Count * Gap / 2;
            rallied.Clear();
            foreach (Entity e in world.GetEntitiesAround(new Vintagestory.API.MathTools.Vec3d(line.Px, line.Py, line.Pz), (float)reach, 4,
                         e => CanRally(e, line.Side, formation, now)))
                rallied.Add(e);
            foreach (Entity ally in rallied)
            {
                Entity target = null;
                double best = JoinRadius * JoinRadius;
                foreach (long id in line.Members)
                {
                    Entity m = world.GetEntityById(id), t = world.GetEntityById(TargetOf(id));
                    if (m == null || t == null || !t.Alive) continue;
                    double dx = m.Pos.X - ally.Pos.X, dz = m.Pos.Z - ally.Pos.Z, d = dx * dx + dz * dz;
                    if (d <= best) { best = d; target = t; }
                }
                if (target == null || !WouldRally(ally, target)) continue;
                Add(line, ally.EntityId, target.EntityId, now);
                ally.Notify("joinLine", target);
            }
        }

        /// <summary>0.0.120: would this ally take the target it's rallied to? A recruit never turns on its own side and
        /// follows its stance (passive, disengaging); 0.0.119 added it to the line before asking, so one that said no
        /// stood as a laggard for up to 8 s (holding back the march) and was rallied again.</summary>
        public bool WouldRally(Entity ally, Entity target) =>
            target != null && target.Alive
            && !(Recruit.OwnerUidOf(ally) is string owner && (Recruit.IsTeam(target, owner) || (factions != null && !factions.RecruitWantsToAttack(ally, owner, target))));

        static bool SeekRunning(Entity e)
        {
            var active = e.GetBehavior<Vintagestory.GameContent.EntityBehaviorTaskAI>()?.TaskManager?.ActiveTasksBySlot;
            if (active != null) foreach (var task in active) if (task is AiTaskFactionSeek) return true;
            return false;
        }

        /// <summary>0.0.118: another line of the same side within MetNearRadius has met the enemy, so the battle has
        /// started here too (battle log 2026-11-13: 3 orcs of a second line stood ~20 s waiting for their line to form
        /// while the main line fought).</summary>
        public bool SideMetNear(BattleLine line)
        {
            foreach (BattleLine l in lines)
            {
                if (l == line || !l.Contact || l.Side != line.Side) continue;
                double dx = l.Px - line.Px, dz = l.Pz - line.Pz;
                if (dx * dx + dz * dz <= MetNearRadius * MetNearRadius) return true;
            }
            return false;
        }

        // The front rank goes for its enemies now, instead of when its seek task next searches (every second or two).
        void Wake(IWorldAccessor world, BattleLine line)
        {
            for (int i = 0; i < Math.Min(line.Front, line.Members.Count); i++)
            {
                Entity e = world.GetEntityById(line.Members[i]), t = world.GetEntityById(TargetOf(line.Members[i]));
                if (e != null && e.Alive && t != null && t.Alive) e.Notify("seekEntity", t);
            }
        }

        void Disband(BattleLine line)
        {
            foreach (long id in line.Members) lineOf.Remove(id);
            line.Members.Clear();
            lines.Remove(line);
        }
    }
}
