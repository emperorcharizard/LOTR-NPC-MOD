using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// Mounted-units spike (docs/notes/feverstone-mounts.md, owner 2026-11-12): a testing command that seats the NPC you
    /// look at on a tamed mount from another mod (a Feverstone horse by default) and steers it toward you, to find out in
    /// game whether an NPC can ride at all. Two ways to steer: "controls" presses the rider's movement keys and lets the
    /// game's riding code (EntityBehaviorRideable, which reads the seated rider's Controls) move the mount; "direct" sets
    /// the mount's gait and facing ourselves. Once a second each rider's numbers go to the server log. Not a feature:
    /// shortcut: riders are kept in memory only (re-run the command after a reload), upgrade when the rider rank is built.
    /// </summary>
    public class MountSpikeSystem : ModSystem
    {
        public const string DefaultMount = "feverstonewilds:horse-tamed-male-quarter", DefaultRider = "lotrnpcs:test-rohan-warrior";
        const int TickMs = 100;

        sealed class Rider
        {
            public EntityAgent Npc;
            public string OwnerUid, Mode;
            public long LastLogMs;
        }

        ICoreServerAPI sapi;
        readonly Dictionary<long, Rider> riders = new Dictionary<long, Rider>();

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            CommandArgumentParsers parsers = api.ChatCommands.Parsers;
            ModCommands.RootOf(api)
                .BeginSubCommand("mountspike")
                .WithDescription("Testing: spawn = a new rider and its saddled mount as one unit where you look (spawn [npc code] [mount code], default " + DefaultRider + " on " + DefaultMount + "); ai / controls / direct = seat the NPC you look at, or switch how it steers: ai = the mount goes where the rider's own AI walks (its orders and fights, as on foot), controls / direct = toward you by pressing the rider's keys or by setting the mount's gait and facing; off = get off. Logs to server-main.log.")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .WithArgs(parsers.OptionalWord("spawn|ai|controls|direct|off"), parsers.OptionalWord("code"), parsers.OptionalWord("mount code"))
                .HandleWith(ModCommands.DebugOnly(api, OnCommand))
                .EndSubCommand();
            api.Event.RegisterGameTickListener(_ => Tick(), TickMs);
        }

        TextCommandResult OnCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            EntityPlayer me = player.Entity;
            string mode = (args[0] as string)?.ToLowerInvariant() ?? "controls";
            if (mode != "spawn" && mode != "ai" && mode != "controls" && mode != "direct" && mode != "off") return TextCommandResult.Error("Mode is spawn, ai, controls, direct or off.");
            BlockSelection bsel = null;
            EntitySelection esel = null;
            sapi.World.RayTraceForSelection(me.SidedPos.XYZ.Add(me.LocalEyePos), me.SidedPos.Pitch, me.SidedPos.Yaw, (float)HiringSystem.CommandRange, ref bsel, ref esel, null, e => e != me);

            // The goal (owner, 2026-11-12: "the goal is to have [the mount] spawn as a part of the unit"): rider and
            // mount come into the world together.
            if (mode == "spawn")
            {
                EntityProperties riderType = sapi.World.GetEntityType(new AssetLocation(args[1] as string ?? DefaultRider));
                if (riderType == null) return TextCommandResult.Error("No entity type " + (args[1] as string ?? DefaultRider) + ".");
                if (!(sapi.World.ClassRegistry.CreateEntity(riderType) is EntityAgent rider)) return TextCommandResult.Error("Not an agent.");
                Vec3d at = bsel != null ? bsel.Position.ToVec3d().Add(0.5, 1, 0.5) : me.Pos.XYZ.Add(me.Pos.GetViewVector().ToVec3d().Mul(4));
                rider.Pos.SetPos(at);
                rider.Pos.Yaw = me.Pos.Yaw + GameMath.PI;
                rider.PositionBeforeFalling.Set(at.X, at.Y, at.Z);
                sapi.World.SpawnEntity(rider);
                string spawned = MountOn(rider, args[2] as string ?? DefaultMount, player, "ai", out bool ok);
                return ok ? TextCommandResult.Success("Spawned " + rider.Code + ". " + spawned) : TextCommandResult.Error("Spawned " + rider.Code + ". " + spawned);
            }
            if (!(esel?.Entity is EntityAgent npc) || npc.Code?.Domain != "lotrnpcs" || !npc.Alive) return TextCommandResult.Error("Look at a living LOTR NPC.");

            if (mode == "off")
            {
                riders.Remove(npc.EntityId);
                Release(npc);
                return TextCommandResult.Success("Off: " + (npc.MountedOn == null ? "dismounted." : "still mounted (TryUnmount refused)."));
            }
            if (npc.MountedOn != null)
            {
                riders[npc.EntityId] = new Rider { Npc = npc, OwnerUid = player.PlayerUID, Mode = mode };
                return TextCommandResult.Success("Already mounted; steering now " + mode + ".");
            }

            string result = MountOn(npc, args[1] as string ?? DefaultMount, player, mode, out bool done);
            return done ? TextCommandResult.Success(result) : TextCommandResult.Error(result);
        }

        /// <summary>Spawns a saddled mount of this type under the NPC and seats it; the text says how it went.</summary>
        string MountOn(EntityAgent npc, string code, IServerPlayer player, string mode, out bool mounted)
        {
            mounted = false;
            EntityProperties type = sapi.World.GetEntityType(new AssetLocation(code));
            if (type == null) return "No entity type " + code + " (is its mod installed and the creature switched on?).";
            Entity mount = sapi.World.ClassRegistry.CreateEntity(type);
            mount.Pos.SetFrom(npc.Pos);
            mount.Pos.Yaw = npc.Pos.Yaw;
            mount.PositionBeforeFalling.Set(mount.Pos.X, mount.Pos.Y, mount.Pos.Z);
            sapi.World.SpawnEntity(mount);

            // Saddle and bridle of the mount's breed, by Feverstone's item naming (fsmountwearables-<breed>-...), straight
            // into the slot at their attachment point: the game's TryGiveItemStack throws on the server (it maps the
            // client's selection boxes). EntityBehaviorAttachable is in VSSurvivalMod, which we don't reference.
            string breed = code.Contains("direwolf") ? "direwolf" : code.Split('-').Last();
            EntityBehavior tackBehavior = mount.SidedProperties.Behaviors.FirstOrDefault(b => b.PropertyName() == "rideableaccessories");
            List<string> tack = new List<string>();
            if (tackBehavior == null) tack.Add("no rideableaccessories behavior (has: " + string.Join(" ", mount.SidedProperties.Behaviors.Select(x => x.PropertyName())) + ")");
            // The slot for each piece: the one whose categories hold it, in the order of the mount's "wearableSlots"
            // (behaviorConfigs); looking slots up by attachment point needs the client's selection boxes, so it finds
            // nothing on the server. Fallback: Feverstone's order, bridle first and saddle fourth.
            InventoryBase inv = (tackBehavior as EntityBehaviorContainer)?.Inventory;
            JsonObject[] slotConfigs = mount.Properties.Server?.BehaviorsAsJsonObj?.FirstOrDefault(b => b["code"].AsString() == "rideableaccessories")?["wearableSlots"].AsArray()
                ?? Array.Empty<JsonObject>();
            foreach ((string item, string category, int fallback) in new[] { ("middleback-saddle", "saddle-", 3), ("head-bridle", "bridle-", 0) })
            {
                Item it = sapi.World.GetItem(new AssetLocation("feverstonewilds:fsmountwearables-" + breed + "-" + item + "-both"));
                string name = item.Split('-')[1];
                int index = Array.FindIndex(slotConfigs, c => (c["forCategoryCodes"].AsArray<string>() ?? Array.Empty<string>()).Any(cc => cc.StartsWith(category)));
                bool fromConfig = index >= 0;
                if (!fromConfig) index = fallback;
                if (it == null || inv == null || index >= inv.Count) { tack.Add(name + (it == null ? " (no item)" : " (no slot)")); continue; }
                inv[index].Itemstack = new ItemStack(it);
                inv[index].MarkDirty();
                tack.Add(name + " in slot " + index + (fromConfig ? "" : " (fallback)"));
            }
            (tackBehavior as EntityBehaviorContainer)?.storeInv();

            IMountable mountable = mount.SidedProperties.Behaviors.OfType<IMountable>().FirstOrDefault();
            IMountableSeat seat = mountable?.Seats?.FirstOrDefault(s => s.Config?.Controllable == true) ?? mountable?.Seats?.FirstOrDefault();
            if (seat == null) return "Spawned " + code + " (" + mount.EntityId + ") but it has no seat; " + string.Join(", ", tack) + ".";
            bool canMount = seat.CanMount(npc);
            mounted = npc.TryMount(seat);
            if (mounted) riders[npc.EntityId] = new Rider { Npc = npc, OwnerUid = player.PlayerUID, Mode = mode };
            string text = "Mount " + code + " (" + mount.EntityId + "): " + string.Join(", ", tack) + "; seat " + seat.SeatId + (seat.CanControl ? " (controllable)" : "")
                + "; CanMount " + canMount + "; TryMount " + (mounted ? "ok, steering " + mode : "refused") + ".";
            sapi.Logger.Notification("[lotrnpcs] mountspike: " + npc.Code + " (" + npc.EntityId + ") " + text);
            return text;
        }

        static void Release(EntityAgent npc)
        {
            npc.Controls.Forward = npc.Controls.Sprint = npc.Controls.Left = npc.Controls.Right = false;
            npc.TryUnmount();
        }

        void Tick()
        {
            if (riders.Count == 0) return;
            long now = sapi.World.ElapsedMilliseconds;
            foreach (Rider r in riders.Values.ToList())
            {
                EntityAgent npc = r.Npc;
                Entity mount = npc.MountedOn?.Entity;
                IPlayer owner = sapi.World.PlayerByUid(r.OwnerUid);
                if (!npc.Alive || mount == null || owner?.Entity == null)
                {
                    riders.Remove(npc.EntityId);
                    sapi.Logger.Notification("[lotrnpcs] mountspike: " + npc.EntityId + " stopped (" + (!npc.Alive ? "dead" : mount == null ? "not mounted" : "owner gone") + ").");
                    continue;
                }
                double dx = owner.Entity.Pos.X - mount.Pos.X, dz = owner.Entity.Pos.Z - mount.Pos.Z, dist = Math.Sqrt(dx * dx + dz * dz);
                // The game's yaw for a heading (AiTaskSeekEntity uses the same).
                float want = (float)Math.Atan2(dx, dz);
                bool go = dist > 4, fast = dist > 12;
                // ai (owner: "the behavior should remain the same for commanding the unit"): where the rider's own tasks
                // (follow, hold, move, guard, seek, battle line) want to walk; the mount carries it there.
                Vec3d walk = npc.Controls.WalkVector;
                double walkLen = Math.Sqrt(walk.X * walk.X + walk.Z * walk.Z);
                if (r.Mode == "ai")
                {
                    go = walkLen > 1e-4;
                    fast = go && (npc.Controls.Sprint || walkLen > 0.04);
                    if (go) want = (float)Math.Atan2(walk.X, walk.Z);
                }
                float diff = GameMath.AngleRadDistance(mount.Pos.Yaw, want);
                EntityBehaviorGait gait = mount.GetBehavior<EntityBehaviorGait>();

                if (r.Mode == "controls")
                {
                    npc.Controls.Forward = go;
                    npc.Controls.Sprint = fast;
                    npc.Controls.Left = go && diff > 0.15f;
                    npc.Controls.Right = go && diff < -0.15f;
                }
                else if (gait != null)
                {
                    if (r.Mode == "direct") npc.Controls.Forward = npc.Controls.Sprint = npc.Controls.Left = npc.Controls.Right = false;
                    if (go) mount.Pos.Yaw = want;
                    string g = !go ? null : fast ? "sprint" : "walk";
                    if (g == null) gait.SetIdle();
                    else if (gait.Gaits.TryGetValue(g, out GaitMeta meta)) gait.CurrentGait = meta;
                }

                if (now - r.LastLogMs >= 1000)
                {
                    r.LastLogMs = now;
                    sapi.Logger.Notification(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "[lotrnpcs] mountspike {0}: mode {1}, dist {2:0.0}, mountYaw {3:0.00}, want {4:0.00}, diff {5:0.00}, gait {6}, mount at {7:0.0} {8:0.0}, F{9} S{10} L{11} R{12}, walk {13:0.000}",
                        npc.EntityId, r.Mode, dist, mount.Pos.Yaw, want, diff, gait?.CurrentGait?.Code ?? "-", mount.Pos.X, mount.Pos.Z,
                        npc.Controls.Forward ? 1 : 0, npc.Controls.Sprint ? 1 : 0, npc.Controls.Left ? 1 : 0, npc.Controls.Right ? 1 : 0, walkLen));
                }
            }
        }
    }
}
