using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    public partial class HiringSystem
    {
        // ---- orders (hold R) ----

        /// <summary>Is this recruit charging on its owner's order?</summary>
        public bool Charging(Entity recruit) => recruit != null && charging.TryGetValue(recruit.EntityId, out var c) && c.Until > Now;

        /// <summary>Whether a charge is over: past its end, or ChargeQuietMs with no enemy near (quietSince 0: one is).</summary>
        public static bool ChargeOver(long now, long until, long quietSince) => now >= until || (quietSince > 0 && now - quietSince >= ChargeQuietMs);

        void StartCharge(Entity recruit)
        {
            charging[recruit.EntityId] = (Now + ChargeMs, 0);
            lock (chargingIds) chargingIds.Add(recruit.EntityId);
            disengageUntil.Remove(recruit.EntityId);
            regrouping.Remove(recruit.EntityId);
        }

        void EndCharge(long id)
        {
            charging.Remove(id);
            lock (chargingIds) chargingIds.Remove(id);
        }

        /// <summary>The charge's target: the entity the owner looks at when it is a candidate, else the candidate nearest
        /// the point looked at within ChargeNearPoint, else the one nearest the owner within ChargeNearOwner; -1 for none.
        /// Positions are (x, z). Public for tests.</summary>
        public static int ChooseChargeTarget(IList<(double X, double Z)> candidates, int lookedAt, (double X, double Z)? point, (double X, double Z) owner)
        {
            if (lookedAt >= 0 && lookedAt < candidates.Count) return lookedAt;
            int Nearest((double X, double Z) at, double within)
            {
                int best = -1; double bestD = within * within;
                for (int i = 0; i < candidates.Count; i++)
                {
                    double dx = candidates[i].X - at.X, dz = candidates[i].Z - at.Z, d = dx * dx + dz * dz;
                    if (d <= bestD) { bestD = d; best = i; }
                }
                return best;
            }
            int near = point is (double, double) p ? Nearest(p, ChargeNearPoint) : -1;
            return near >= 0 ? near : Nearest(owner, ChargeNearOwner);
        }

        /// <summary>Is this recruit still breaking off after a movement order?</summary>
        public bool Disengaging(Entity recruit) => regrouping.Contains(recruit.EntityId) || (disengageUntil.TryGetValue(recruit.EntityId, out long until) && until > Now);

        /// <summary>Is this follower breaking off a fight to get back to its leader?</summary>
        public bool Regrouping(Entity recruit) => regrouping.Contains(recruit.EntityId);

        /// <summary>Is the target more than FollowFightRange from a formation follower's leader? Unused: followers
        /// may pick targets anywhere and break off past the range instead.</summary>
        public bool BeyondFollowRange(Entity recruit, Entity target) =>
            target != null && formationLeader.TryGetValue(recruit.EntityId, out Entity leader) && leader != null
            && target.Pos.SquareDistanceTo(leader.Pos) > FollowFightRange * FollowFightRange;

        /// <summary>
        /// Once a second for every loaded recruit, with its leader's entity when it is following an online leader
        /// (else null). Remembers formation followers' leaders, and starts or ends the break-off. True when this
        /// call started a break-off (the caller then stops the recruit's fight). Public for tests.
        /// </summary>
        public bool UpdateFollowFight(Entity recruit, Entity leader)
        {
            long id = recruit.EntityId;
            bool inFormation = leader != null && Recruit.OrderOf(recruit) == Recruit.Follow && Recruit.FormationOf(recruit) != Formation.Loose;
            // A charging recruit fights wherever the charge takes it.
            if (inFormation && Charging(recruit)) { formationLeader[id] = leader; regrouping.Remove(id); return false; }
            if (!inFormation)
            {
                formationLeader.Remove(id);
                regrouping.Remove(id);
                return false;
            }
            formationLeader[id] = leader;
            double d2 = recruit.Pos.SquareDistanceTo(leader.Pos);
            if (regrouping.Contains(id))
            {
                if (d2 <= RegroupWithin * RegroupWithin) regrouping.Remove(id);
                return false;
            }
            if (d2 <= FollowFightRange * FollowFightRange) return false;
            regrouping.Add(id);
            return true;
        }

        /// <summary>Drops what UpdateFollowFight remembers about a recruit that left the list.</summary>
        void ForgetFollowFight(long id) { formationLeader.Remove(id); regrouping.Remove(id); }

        /// <summary>Starts the break-off after a movement order. Public for tests.</summary>
        public void SetDisengaging(Entity recruit, long ms = DisengageMs)
        {
            long now = Now;
            FactionSystem.RemoveExpired(disengageUntil, now);
            disengageUntil[recruit.EntityId] = now + ms;
            EndCharge(recruit.EntityId);
        }

        /// <summary>Which way followers in formation line up: the way the owner has been walking, else the way they last
        /// faced when giving an order.</summary>
        public Vec2d Heading(string uid) => Owner(uid) is HiringOwner owner ? new Vec2d(owner.HeadX, owner.HeadZ) : new Vec2d(0, 1);

        static void UpdateHeading(HiringOwner owner, EntityPlayer entity)
        {
            if (!owner.HasLast)
            {
                owner.LastX = entity.Pos.X;
                owner.LastZ = entity.Pos.Z;
                owner.HasLast = true;
                return;
            }
            double dx = entity.Pos.X - owner.LastX, dz = entity.Pos.Z - owner.LastZ;
            if (dx * dx + dz * dz < 2 * 2) return;
            double len = Math.Sqrt(dx * dx + dz * dz);
            owner.HeadX = dx / len;
            owner.HeadZ = dz / len;
            owner.LastX = entity.Pos.X;
            owner.LastZ = entity.Pos.Z;
        }

        /// <summary>The owner's recruits that hear an order: loaded, alive and within CommandRange.</summary>
        List<EntityAgent> Crew(EntityPlayer by) =>
            Led(by.PlayerUID).Where(e => e.Pos.Dimension == by.Pos.Dimension && e.Pos.SquareDistanceTo(by.Pos) <= CommandRange * CommandRange).ToList();

        // A borrower lost one of their followers to its owner: close up their formation.
        void RefreshBorrowerSlots(string uid)
        {
            if (sapi?.World.PlayerByUid(uid)?.Entity is EntityPlayer e && Owner(uid) is HiringOwner rec) RefreshFollowSlots(rec, e);
        }

        static void StopFights(EntityAgent npc)
        {
            AiTaskManager tasks = npc.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
            if (tasks == null) return;
            tasks.StopTask<AiTaskMeleeAttack>();
            tasks.StopTask<AiTaskSeekEntity>();
            tasks.StopTask<AiTaskFactionShoot>();
            // 1.0.2: a rider drops its focus too, or its mount kept charging the enemy it was finishing.
            Riders.SetFocus(npc, null);
        }

        void Disengage(EntityAgent npc)
        {
            StopFights(npc);
            SetDisengaging(npc);
        }

        /// <summary>Gives the owner's followers their places in the owner's formation (none when loose).</summary>
        public void RefreshFollowSlots(HiringOwner owner, EntityPlayer by)
        {
            if (sapi == null) return;
            List<EntityAgent> followers = Crew(by).Where(e => Recruit.OrderOf(e) == Recruit.Follow).ToList();
            string formation = Formation.Valid(owner.Formation);
            foreach (EntityAgent npc in followers) npc.WatchedAttributes.SetString(Recruit.FormationAttr, formation);
            if (formation == Formation.Loose)
            {
                foreach (EntityAgent npc in followers) Recruit.SetFollowSlot(npc, null);
                return;
            }
            List<Vec2d> slots = Formation.FollowOffsets(formation, followers.Count);
            Vec3d at = by.Pos.XYZ;
            int[] spotOf = Formation.AssignByRank(followers.Select(e => e.Pos.XYZ).ToList(),
                slots.Select(o => Formation.World(at, owner.HeadX, owner.HeadZ, o)).ToList(), slots, followers.Select(e => Formation.OrderOfRank(RankOf(e))).ToList());
            for (int i = 0; i < followers.Count; i++) Recruit.SetFollowSlot(followers[i], slots[spotOf[i]]);
        }

        /// <summary>Sends these recruits to hold the spots of a formation around anchor, facing (fx, fz).</summary>
        void Place(List<EntityAgent> crew, string formation, Vec3d anchor, double fx, double fz, int dimension, bool guards = false)
        {
            if (crew.Count == 0) return;
            List<Vec2d> offsets = Formation.Offsets(formation, crew.Count);
            List<Vec3d> spots = offsets.Select(o =>
            {
                Vec3d p = Formation.World(anchor, fx, fz, o);
                if (Ground.StandHeight(sapi.World.BlockAccessor, p.X, anchor.Y + 0.5, p.Z, dimension, 10) is double y) p.Y = y;
                return p;
            }).ToList();
            int[] spotOf = Formation.AssignByRank(crew.Select(e => e.Pos.XYZ).ToList(), spots, offsets, crew.Select(e => Formation.OrderOfRank(RankOf(e))).ToList());
            for (int i = 0; i < crew.Count; i++)
            {
                if (guards) Recruit.SetGuardSpot(crew[i], spots[spotOf[i]]); else Recruit.SetHoldAt(crew[i], spots[spotOf[i]]);
                if (formation != Formation.Loose) Recruit.SetFacing(crew[i], fx, fz);
                Recruit.SetFollowSlot(crew[i], null);
                crew[i].WatchedAttributes.SetString(Recruit.FormationAttr, formation);
            }
        }

        /// <summary>Carries out an order from the orders menu. Returns what to tell the player.</summary>
        public string Command(IServerPlayer player, RecruitCommand cmd)
        {
            string L(string key, params object[] args) => Lang.GetL(player.LanguageCode, "lotrnpcs:" + key, args);
            EntityPlayer by = player.Entity;
            if (sapi == null || by == null || cmd == null) return L("cmd-none");
            // The numbers come from the client: a NaN or infinity would end up in positions and facings.
            if (!new[] { cmd.X, cmd.Y, cmd.Z, cmd.Fx, cmd.Fz }.All(double.IsFinite)) return L("cmd-none");
            List<EntityAgent> crew = Crew(by);
            if (crew.Count == 0) return L("cmd-none");
            HiringOwner owner = EnsureOwner(player);
            // Movement orders from the owner make their recruits private again (Guard here for my group shares them).
            void OwnOrder(IEnumerable<EntityAgent> npcs) { foreach (EntityAgent npc in npcs) if (Recruit.OwnerUidOf(npc) == player.PlayerUID) Recruit.SetShared(npc, false); }
            if (cmd.Action == Recruit.Hold || cmd.Action == "move" || cmd.Action == Recruit.Guard) OwnOrder(crew);

            // The way the player looks, for hold orders, and the default way to face.
            double fx = cmd.Fx, fz = cmd.Fz, flen = Math.Sqrt(fx * fx + fz * fz);
            if (flen < 1e-3) { fx = owner.HeadX; fz = owner.HeadZ; flen = Math.Sqrt(fx * fx + fz * fz); }
            fx /= flen; fz /= flen;
            string formation = Formation.Valid(owner.Formation);

            switch (cmd.Action)
            {
                case Recruit.Follow:
                    {
                        // As many follow as fit the follow shares. Those already following stay with the owner; the
                        // others are tried nearest first, each joining if its share fits (so a goblin may still join
                        // after an elf didn't), and the rest keep their orders.
                        List<EntityAgent> following = Followers(player.PlayerUID);
                        // Ranked recruits take the ranked places first, then recruits fill the recruit-only ones.
                        List<EntityAgent> joining = crew.Where(e => !following.Contains(e)).OrderBy(e => Promotion.BaseRank(RankOf(e)) == "recruit").ThenBy(e => e.Pos.SquareDistanceTo(by.Pos)).ToList();
                        List<EntityAgent> coming = following.Where(crew.Contains).ToList();
                        List<EntityAgent> band = new List<EntityAgent>(following);
                        int joined = 0, left = 0;
                        string firstLeft = null;
                        foreach (EntityAgent npc in joining)
                        {
                            if (BandFits(band.Append(npc).Select(Member), RecruitExtra)) { band.Add(npc); coming.Add(npc); joined++; }
                            else { left++; firstLeft ??= FactionSystem.FactionOf(npc); }
                        }
                        OwnOrder(coming);
                        foreach (EntityAgent npc in coming) { Recruit.SetOrder(npc, Recruit.Follow); Disengage(npc); }
                        owner.HeadX = fx; owner.HeadZ = fz;
                        owner.LastX = by.Pos.X; owner.LastZ = by.Pos.Z; owner.HasLast = true;
                        RefreshFollowSlots(owner, by);
                        int now = following.Count + joined;
                        return left > 0 ? L("cmd-follow-full", now, left, RoomText(Room(player.PlayerUID, firstLeft), (k, a) => L(k, a))) : L("cmd-follow", now);
                    }

                case Recruit.Hold:
                    owner.FaceX = fx; owner.FaceZ = fz;
                    if (formation == Formation.Loose)
                    {
                        foreach (EntityAgent npc in crew)
                        {
                            Recruit.SetOrder(npc, Recruit.Hold);
                            Recruit.SetFollowSlot(npc, null);
                            npc.WatchedAttributes.SetString(Recruit.FormationAttr, formation);
                        }
                    }
                    else Place(crew, formation, Centre(crew.Select(e => e.Pos.XYZ)), fx, fz, by.Pos.Dimension);
                    return L("cmd-hold", crew.Count);

                case Recruit.Guard:
                case Recruit.GuardShared:
                    {
                        // Guard here: the post is where the player stands. Guards spread out over the area on their own.
                        // Guard here for my group shares the owner's own; borrowed guards posted either way go back to
                        // being shared and free to borrow.
                        Vec3d post = PostAt(by);
                        bool returned = false;
                        foreach (EntityAgent npc in crew)
                        {
                            if (Recruit.OwnerUidOf(npc) == player.PlayerUID) Recruit.SetShared(npc, cmd.Action == Recruit.GuardShared);
                            else { Recruit.ClearBorrower(npc); returned = true; }
                            Recruit.SetGuardAt(npc, post);
                            Recruit.SetFollowSlot(npc, null);
                            npc.WatchedAttributes.SetString(Recruit.FormationAttr, formation);
                            Disengage(npc);
                        }
                        // In a formation, guards stand in it around the post, facing the way the player looks.
                        if (formation != Formation.Loose)
                        {
                            owner.FaceX = fx; owner.FaceZ = fz;
                            Place(crew, formation, post, fx, fz, by.Pos.Dimension, true);
                        }
                        return cmd.Action == Recruit.GuardShared || returned ? L("cmd-guard-shared", crew.Count, (int)GuardRadius) : L("cmd-guard", crew.Count, (int)GuardRadius);
                    }

                case "retreat":
                    {
                        // Followers drop their fights, come back at a run (the follow task runs past 6 blocks, a
                        // rider's mount gallops) and start no fights for RetreatSeconds; they still take hits. Then
                        // they simply follow on, stance and formation unchanged.
                        List<EntityAgent> back = crew.Where(e => Recruit.OrderOf(e) == Recruit.Follow).ToList();
                        if (back.Count == 0) return L("cmd-nofollowers");
                        long ms = (long)(FactionConfig.ClampRetreatSeconds(Factions?.Config.RetreatSeconds ?? FactionConfig.DefaultRetreatSeconds) * 1000);
                        foreach (EntityAgent npc in back) { StopFights(npc); SetDisengaging(npc, ms); }
                        return L("cmd-retreat", back.Count, (int)(ms / 1000));
                    }

                case "charge":
                    {
                        // Followers run at the enemy the owner looks at (or the nearest to where they look, or to
                        // them), out of formation and battle lines, and fight from there until the charge ends
                        // (Charging).
                        List<EntityAgent> chargers = crew.Where(e => Recruit.OrderOf(e) == Recruit.Follow && Recruit.StanceOf(e) != Recruit.Passive).ToList();
                        if (chargers.Count == 0) return L("cmd-nofollowers");
                        bool Wanted(Entity e) => Factions != null && chargers.Any(n => Factions.RecruitWantsToAttack(n, Recruit.OwnerUidOf(n), e, true));
                        List<Entity> candidates = sapi.World.GetEntitiesAround(by.Pos.XYZ, (float)ChargeLookRange, (float)ChargeLookRange / 2, e => e.Alive && e != by && Wanted(e)).ToList();
                        int looked = cmd.TargetId == 0 ? -1 : candidates.FindIndex(e => e.EntityId == cmd.TargetId);
                        int pick = ChooseChargeTarget(candidates.Select(e => (e.Pos.X, e.Pos.Z)).ToList(), looked,
                            cmd.HasTarget ? (cmd.X, cmd.Z) : ((double, double)?)null, (by.Pos.X, by.Pos.Z));
                        if (pick < 0) return L("cmd-charge-none");
                        Entity foe = candidates[pick];
                        int sent = 0;
                        foreach (EntityAgent npc in chargers)
                        {
                            if (!Factions.RecruitWantsToAttack(npc, Recruit.OwnerUidOf(npc), foe, true)) continue;
                            StartCharge(npc);
                            npc.Notify("seekEntity", foe);
                            sent++;
                        }
                        return sent == 0 ? L("cmd-charge-none") : L("cmd-charge", sent);
                    }

                case "move":
                    if (!cmd.HasTarget) return L("cmd-notarget");
                    Vec3d target = new Vec3d(cmd.X, cmd.Y, cmd.Z);
                    if (target.SquareDistanceTo(by.Pos.XYZ) > CommandRange * CommandRange) return L("cmd-toofar");
                    double mx = target.X - by.Pos.X, mz = target.Z - by.Pos.Z, mlen = Math.Sqrt(mx * mx + mz * mz);
                    if (mlen > 0.5) { fx = mx / mlen; fz = mz / mlen; }
                    owner.FaceX = fx; owner.FaceZ = fz;
                    Place(crew, formation, target, fx, fz, by.Pos.Dimension);
                    foreach (EntityAgent npc in crew) Disengage(npc);
                    return L("cmd-move", crew.Count);

                case "formation":
                    if (!Formation.All.Contains(cmd.Arg)) return L("cmd-none");
                    owner.Formation = cmd.Arg;
                    RefreshFollowSlots(owner, by);
                    List<EntityAgent> holders = crew.Where(e => Recruit.OrderOf(e) == Recruit.Hold).ToList();
                    // Holders re-form around the middle of the spots they hold; loose leaves them where they are.
                    if (cmd.Arg != Formation.Loose && holders.Count > 0)
                        Place(holders, cmd.Arg, Centre(holders.Select(e => Recruit.HoldPos(e) ?? e.Pos.XYZ)), owner.FaceX, owner.FaceZ, by.Pos.Dimension);
                    foreach (EntityAgent npc in holders)
                    {
                        npc.WatchedAttributes.SetString(Recruit.FormationAttr, cmd.Arg);
                        if (cmd.Arg == Formation.Loose) Recruit.SetGuardSpot(npc, null);   // drops the facing: loose holders stand easy
                    }
                    // Guards form up around their post (each post its own formation); loose sends them back to patrolling.
                    foreach (IGrouping<string, EntityAgent> post in crew.Where(e => Recruit.OrderOf(e) == Recruit.Guard && Recruit.HoldPos(e) != null)
                        .GroupBy(e => { Vec3d p = Recruit.HoldPos(e); return Math.Round(p.X, 1) + "/" + Math.Round(p.Y, 1) + "/" + Math.Round(p.Z, 1); }))
                    {
                        List<EntityAgent> guards = post.ToList();
                        foreach (EntityAgent npc in guards) { npc.WatchedAttributes.SetString(Recruit.FormationAttr, cmd.Arg); Recruit.SetGuardSpot(npc, null); }
                        if (cmd.Arg != Formation.Loose) Place(guards, cmd.Arg, Recruit.HoldPos(guards[0]), owner.FaceX, owner.FaceZ, by.Pos.Dimension, true);
                    }
                    return L("cmd-formation", L("formation-" + cmd.Arg));

                case "stance":
                    if (!Recruit.Stances.Contains(cmd.Arg)) return L("cmd-none");
                    foreach (EntityAgent npc in crew)
                    {
                        Recruit.SetStance(npc, cmd.Arg);
                        // Calmer stances drop fights the new stance wouldn't pick; the others carry on.
                        if (cmd.Arg == Recruit.Passive || cmd.Arg == Recruit.Defensive) StopFights(npc);
                    }
                    return L("cmd-stance", L("stance-" + cmd.Arg), crew.Count);
            }
            return L("cmd-none");
        }

        /// <summary>Where a guard post goes when this player orders Guard here: on the ground where they stand.</summary>
        Vec3d PostAt(Entity by)
        {
            Vec3d post = by.Pos.XYZ;
            if (sapi != null && Ground.StandHeight(sapi.World.BlockAccessor, post.X, post.Y + 0.5, post.Z, by.Pos.Dimension, 10) is double y) post.Y = y;
            return post;
        }

        static Vec3d Centre(IEnumerable<Vec3d> points)
        {
            List<Vec3d> list = points.ToList();
            if (list.Count == 0) return new Vec3d();
            return new Vec3d(list.Average(p => p.X), list.Average(p => p.Y), list.Average(p => p.Z));
        }
        // ---- the whitelist (/recruits whitelist) ----

        TextCommandResult Whitelist(TextCommandCallingArgs args, bool add)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only a player can have recruits.");
            string text = ChangeWhitelist(player, args[0] as string, add, out bool ok);
            return ok ? TextCommandResult.Success(text) : TextCommandResult.Error(text);
        }

        /// <summary>
        /// Puts a player on the owner's whitelist or takes them off (the command and the whitelist menu). Returns the
        /// reply; ok is false when nothing changed because of a mistake (unknown player, yourself, not listed).
        /// </summary>
        public string ChangeWhitelist(IServerPlayer player, string name, bool add, out bool ok)
        {
            string L(string key, params object[] a) => Lang.GetL(player?.LanguageCode, "lotrnpcs:" + key, a);
            ok = false;
            if (!(FactionSystem.FindPlayer(sapi, name) is (string uid, string known))) return L("whitelist-unknown", name);
            if (uid == player.PlayerUID) return L("whitelist-self");
            HiringOwner owner = Owner(player.PlayerUID);
            if (add)
            {
                if (owner == null) { owners[player.PlayerUID] = owner = new HiringOwner(); Refresh(owner, player); }
                ok = true;
                if (owner.Whitelist.ContainsKey(uid)) return L("whitelist-already", known);
                owner.Whitelist[uid] = known;
                sapi.Logger.Notification("[lotrnpcs] " + player.PlayerName + " put " + known + " on their recruit whitelist.");
                return L("whitelist-added", known);
            }
            if (owner == null || !owner.Whitelist.Remove(uid)) return L("whitelist-notlisted", known);
            ok = true;
            sapi.Logger.Notification("[lotrnpcs] " + player.PlayerName + " took " + known + " off their recruit whitelist.");
            return L("whitelist-removed", known);
        }

        /// <summary>The whitelist menu's contents for this player: who is listed, and who is online to add.</summary>
        public WhitelistState WhitelistStateFor(IServerPlayer player, string notice = null)
        {
            Dictionary<string, string> list = Owner(player.PlayerUID)?.Whitelist ?? new Dictionary<string, string>();
            return new WhitelistState
            {
                Listed = list.OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Value).ToList(),
                Online = sapi.World.AllOnlinePlayers.Where(p => p.PlayerUID != player.PlayerUID && !list.ContainsKey(p.PlayerUID))
                    .Select(p => p.PlayerName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
                Notice = notice,
            };
        }

        TextCommandResult OpenWhitelistMenu(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only a player can have recruits.");
            sapi.ModLoader.GetModSystem<WhitelistMenuSystem>()?.Open(player);
            return TextCommandResult.Success();
        }

        TextCommandResult ListWhitelist(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Only a player can have recruits.");
            Dictionary<string, string> list = Owner(player.PlayerUID)?.Whitelist;
            if (list == null || list.Count == 0) return TextCommandResult.Success(Lang.GetL(player.LanguageCode, "lotrnpcs:whitelist-empty"));
            return TextCommandResult.Success(Lang.GetL(player.LanguageCode, "lotrnpcs:whitelist-list", string.Join(", ", list.Values.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))));
        }

        void Load()
        {
            byte[] bytes = sapi.WorldManager.SaveGame.GetData(SaveKey);
            if (bytes == null) return;
            try
            {
                owners = JsonConvert.DeserializeObject<Dictionary<string, HiringOwner>>(Encoding.UTF8.GetString(bytes)) ?? new Dictionary<string, HiringOwner>();
                foreach (string uid in owners.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) owners.Remove(uid);
                foreach (HiringOwner owner in owners.Values)
                {
                    owner.Recruits = owner.Recruits?.Distinct().ToList() ?? new List<long>();
                    owner.Groups ??= new List<string>();
                    owner.Whitelist ??= new Dictionary<string, string>();
                    owner.PendingDismiss ??= new List<long>();
                    owner.RecruitFactions ??= new Dictionary<long, string>();
                    owner.Captains ??= new HashSet<long>();
                    owner.Summaries ??= new Dictionary<long, RecruitSummary>();
                }
                sapi.Logger.Notification("[lotrnpcs] Loaded " + owners.Values.Sum(o => o.Recruits.Count) + " recruits of " + owners.Count + " players.");
            }
            catch (Exception e)
            {
                SavedData.KeepUnreadable(sapi, SaveKey, bytes, "recruits list", e);
            }
        }

        void Save()
        {
            // Factions of recruits no longer on the list (dead, dismissed) aren't needed any more.
            foreach (HiringOwner owner in owners.Values)
            {
                if (owner.RecruitFactions == null) continue;
                foreach (long id in owner.RecruitFactions.Keys.Where(id => !owner.Recruits.Contains(id)).ToList()) owner.RecruitFactions.Remove(id);
                owner.Captains?.RemoveWhere(id => !owner.Recruits.Contains(id));
                if (owner.Summaries != null) foreach (long id in owner.Summaries.Keys.Where(id => !owner.Recruits.Contains(id)).ToList()) owner.Summaries.Remove(id);
            }
            sapi.WorldManager.SaveGame.StoreData(SaveKey, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(owners)));
        }
    }
}
