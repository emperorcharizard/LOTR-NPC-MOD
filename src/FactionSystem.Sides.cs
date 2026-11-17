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
    public partial class FactionSystem
    {
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
        /// Wild NPCs treat a recruit the way they treat its owner: on sight if they would attack the owner on sight,
        /// and also when the recruit or its owner hurt them or their allies. A recruit keeps its faction's looks, not
        /// its friends: its own faction fights it too if its owner is that faction's enemy.
        /// </summary>
        public bool WouldAttackRecruit(string faction, Entity recruit, string ownerUid)
        {
            if (HasGrudge(faction, recruit.EntityId)) return true;
            HiringOwner owner = Hiring?.Owner(ownerUid);
            if (owner != null && owner.EntityId != 0 && HasGrudge(faction, owner.EntityId)) return true;
            return IsAggressive(faction) && !SideOf(ownerUid).FriendOf.Contains(faction);
        }

        /// <summary>Would this NPC fight that entity (counting a battle line's enemies)?</summary>
        public bool IsEnemyOf(Entity npc, Entity e)
        {
            string owner = Recruit.OwnerUidOf(npc);
            if (owner != null) return RecruitWantsToAttack(npc, owner, e);
            return FactionOf(npc) is string faction && WantsToAttack(faction, e);
        }

        /// <summary>Same side for battle spacing: recruits of one leader, or wild NPCs of one faction or allied
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
        /// or breaking off after a movement order or Retreat (a Charge still fights). Otherwise it fights whoever
        /// hurt the owner or one of them, and whatever is going for them; defensive recruits fight only those.
        /// Aggressive ones (the default) also attack enemies on sight: faction NPCs that would attack the owner on
        /// sight, monsters, and players of an enemy side and their recruits (EnemyForces); other players and their
        /// recruits only when they started it. Raiding ones attack every player and creature that isn't allied
        /// (RaidTarget). Guards pick nothing on sight outside their area, and inside it they also attack intruders
        /// (IsIntruder).
        /// </summary>
        public bool RecruitWantsToAttack(Entity recruit, string ownerUid, Entity target, bool charge = false)
        {
            if (target == null || !target.Alive || target.EntityId == recruit.EntityId || Recruit.IsTeam(target, ownerUid)) return false;
            string stance = Recruit.StanceOf(recruit);
            // An ordered charge fights like Aggressive (passive recruits still never fight).
            charge = charge || (Hiring != null && Hiring.Charging(recruit));
            if (stance == Recruit.Passive || (!charge && Hiring != null && Hiring.Disengaging(recruit))) return false;
            if (charge && stance == Recruit.Defensive) stance = Recruit.Aggressive;
            // Followers fight wherever the target is; they break off past 12 blocks from their leader
            // (UpdateFollowFight).
            if (Hiring != null && Hiring.HasTeamGrudge(ownerUid, target.EntityId)) return true;
            if (HiringSystem.IsTargetingTeam(target, ownerUid)) return true;
            if (stance == Recruit.Defensive) return false;
            // No fights started with players near the world spawn (fighting back is above).
            if (target is EntityPlayer && NearWorldSpawn(target)) return false;
            bool guarding = Recruit.OrderOf(recruit) == Recruit.Guard;
            if (guarding && !InGuardArea(recruit, target)) return false;
            if (stance == Recruit.Raid) return RaidTarget(recruit, ownerUid, target);
            if (guarding && IsIntruder(ownerUid, target)) return true;
            // Players of an enemy side (survival mode) and their recruits are fought on sight, so two hired armies of
            // warring factions fight each other.
            if (target is EntityPlayer targetPlayer) return CanBeAttacked(targetPlayer) && EnemyForces(recruit, ownerUid, Attrs.Uid(targetPlayer), null);
            if (Recruit.OwnerUidOf(target) is string otherOwner) return EnemyForces(recruit, ownerUid, otherOwner, target);
            if (FactionOf(target) is string other) return WouldAttackRecruit(other, recruit, ownerUid);
            return IsMonster(target);
        }

        /// <summary>
        /// Are two players' forces enemies? Each side is its player's factions plus, for a recruit, the recruit's own
        /// faction, so two players' hired armies of enemy factions fight even when the players themselves belong to
        /// no faction. Enemies when some pair of their factions is at war and no pair is allied (the same faction
        /// counts as allied). Players on the same side (group, allied factions, whitelist) never are, and a side with
        /// no faction is nobody's enemy.
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
        /// An intruder for Guard here: a player who isn't on the owner's side (Friendly) and can be attacked, or such
        /// a player's recruit. Applies even while the owner is offline.
        /// </summary>
        public bool IsIntruder(string ownerUid, Entity target)
        {
            if (target is EntityPlayer player) return CanBeAttacked(player) && !Friendly(ownerUid, Attrs.Uid(player));
            return Recruit.OwnerUidOf(target) is string otherOwner && !Friendly(ownerUid, otherOwner);
        }

        /// <summary>How far around their post guards patrol, from the config.</summary>
        public double GuardRadius => FactionConfig.ClampGuardRadius(Config.GuardRadius);

        /// <summary>Is this entity within SpawnSafeRadius blocks (across) of the world spawn?</summary>
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
        /// Raid stance: attack every non-allied player and creature. Allied: players on the owner's side (Friendly),
        /// those players' recruits, NPCs of a faction allied to the owner's or the recruit's own, and creatures owned
        /// by any of those players. Players in creative or spectator mode are left alone. Everything else alive with
        /// a mind of its own is fair game: other players, neutral and enemy NPCs, traders, monsters, animals.
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
                // Membership is by the join menu; races and groups only show while they count.
                + (kv.Value.Joinable ? "" : ", not in the join menu")
                + (Config.RaceAndGroupMembership
                    ? ", friendly races " + (kv.Value.FriendlyRaces.Count == 0 ? "none" : string.Join(" ", kv.Value.FriendlyRaces))
                      + ", friendly groups " + (kv.Value.FriendlyGroups.Count == 0 ? "none" : string.Join(" ", kv.Value.FriendlyGroups))
                    : "")
                // The balance values, short.
                + ", follow " + Config.FollowersOf(kv.Key) + ", roster " + Config.RosterOf(kv.Key)
                // Per rank, recruit/warrior/captain/archer.
                + ", health " + RankList(Config.Def(kv.Key)?.Health) + ", damage " + RankList(Config.Def(kv.Key)?.Damage)))
                // Hire prices and the coin are in the config file, not here.
                + "\nAllies: " + (allies.Count == 0 ? "none" : string.Join(", ", allies))
                + "\nNeutral: " + (neutral.Count == 0 ? "none" : string.Join(", ", neutral))
                + "\nEvery other pair are enemies. Hits vary by " + Math.Round(Config.Spread * 100) + "% either way.";
        }
    }
}
