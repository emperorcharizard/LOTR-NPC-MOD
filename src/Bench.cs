using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace LotrNpcs
{
    /// <summary>Server to client: start timing frames (the admin's /recruits bench).</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class BenchStart
    {
        public int Count, WaitSeconds, Seconds;
    }

    /// <summary>Frame-time numbers for a run: average, 1% low (the slowest 1% of frames) and the worst frame.</summary>
    public static class BenchStats
    {
        public static string Summarize(IList<double> ms, string rate = "FPS", string item = "frame")
        {
            if (ms == null || ms.Count == 0) return "no " + item + "s timed";
            double[] sorted = ms.OrderBy(t => t).ToArray();
            double avg = sorted.Average();
            double p99 = sorted.Skip(sorted.Length - Math.Max(1, sorted.Length / 100)).Average();
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "avg {0:0.0} {6} ({1:0.0} ms), 1% low {2:0.0} {6} ({3:0.0} ms), worst {7} {4:0} ms, {5} {7}s",
                1000 / avg, avg, 1000 / p99, p99, sorted[sorted.Length - 1], sorted.Length, rate, item);
        }
    }

    /// <summary>Calls back once a frame with that frame's time in seconds.</summary>
    public class BenchFrameTimer : IRenderer
    {
        public Action<float> OnFrame;
        public double RenderOrder => 1;
        public int RenderRange => 0;
        public void OnRenderFrame(float deltaTime, EnumRenderStage stage) => OnFrame?.Invoke(deltaTime);
        public void Dispose() { }
    }

    /// <summary>
    /// /recruits bench (owner, 2026-11-12: a fixed fight any player can run so other PCs can be measured): an admin
    /// spawns two armies, Gondor against Mordor, in front of them; after a short wait their client times every frame
    /// and the server times its ticks for the same span, and both print the numbers to chat and their log.
    /// /recruits bench clear removes the bench NPCs, bodies included.
    /// </summary>
    public class BenchSystem : ModSystem
    {
        public const string Channel = "lotrnpcs-bench", BenchAttr = "lotrnpcsBench";
        public const int MinCount = 2, MaxCount = 300, WaitSeconds = 5, Seconds = 30;
        static readonly (string Faction, string Code)[] Sides = { ("gondor", "test-gondor"), ("mordor", "test-mordororc") };

        ICoreServerAPI sapi;
        IServerNetworkChannel serverChannel;
        ICoreClientAPI capi;
        long serverListener;
        BenchFrameTimer timer;

        public override void Start(ICoreAPI api) => api.Network.RegisterChannel(Channel).RegisterMessageType(typeof(BenchStart));

        // ---- server ----

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            serverChannel = api.Network.GetChannel(Channel);
            CommandArgumentParsers parsers = api.ChatCommands.Parsers;
            ModCommands.RootOf(api)
                .BeginSubCommand("bench")
                .WithDescription("Admins: frame-rate test. Spawns <count> NPCs (" + MinCount + " to " + MaxCount + "), Gondor against Mordor, 16 blocks in front of you; after " + WaitSeconds + " s your client times frames and the server times ticks for " + Seconds + " s, then both print the numbers to chat and their log. bench clear removes them.")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .WithArgs(parsers.Word("count|clear"))
                .HandleWith(ModCommands.DebugOnly(api, OnBench))
                .EndSubCommand();
        }

        TextCommandResult OnBench(TextCommandCallingArgs args)
        {
            if (!(args.Caller.Player is IServerPlayer player) || player.Entity == null) return TextCommandResult.Error("Only a player in the world can use this.");
            string arg = (args[0] as string)?.Trim().ToLowerInvariant();
            if (arg == "clear") return TextCommandResult.Success("Removed " + Clear() + " bench NPCs.");
            if (!int.TryParse(arg, out int count) || count < MinCount || count > MaxCount)
                return TextCommandResult.Error("Give a count from " + MinCount + " to " + MaxCount + ", or clear.");
            if (serverListener != 0) return TextCommandResult.Error("A bench run is still timing; wait for its numbers.");

            EntityProperties[][] types = Sides.Select(s => new[] { Type(s.Code + "-warrior"), Type(s.Code + "-archer") }).ToArray();
            if (types.Any(t => t[0] == null || t[1] == null)) return TextCommandResult.Error("The Gondor or Mordor warrior or archer type is missing.");

            // Two blocks of NPCs 12 blocks apart across your view, 20 to a row, every 4th an archer.
            EntityPos me = player.Entity.Pos;
            Vec3f view = me.GetViewVector();
            double len = Math.Sqrt(view.X * view.X + view.Z * view.Z);
            double fx = len > 1e-6 ? view.X / len : 0, fz = len > 1e-6 ? view.Z / len : 1;
            double cx = me.X + fx * 16, cz = me.Z + fz * 16;
            int spawned = 0;
            for (int side = 0; side < 2; side++)
            {
                int n = count / 2 + (side == 0 ? count % 2 : 0);
                double toward = side == 0 ? -1 : 1;
                for (int i = 0; i < n; i++)
                {
                    double across = (i % 20 - 9.5) * 1.5, back = 6 + i / 20 * 1.5;
                    double x = cx + fx * toward * back + fz * across, z = cz + fz * toward * back - fx * across;
                    Entity e = sapi.World.ClassRegistry.CreateEntity(types[side][i % 4 == 3 ? 1 : 0]);
                    int y = sapi.World.BlockAccessor.GetRainMapHeightAt((int)Math.Floor(x), (int)Math.Floor(z)) + 1;
                    e.Pos.SetPosWithDimension(new Vec3d(x, y + 0.01, z));
                    e.PositionBeforeFalling.Set(e.Pos.X, e.Pos.Y, e.Pos.Z);
                    e.Attributes.SetBool(BenchAttr, true);
                    sapi.World.SpawnEntity(e);
                    spawned++;
                }
            }

            serverChannel.SendPacket(new BenchStart { Count = spawned, WaitSeconds = WaitSeconds, Seconds = Seconds }, player);
            TimeServerTicks(player, spawned);
            return TextCommandResult.Success("Spawned " + spawned + " NPCs. Timing starts in " + WaitSeconds + " s and runs " + Seconds + " s; stay looking at the fight.");
        }

        EntityProperties Type(string code) => sapi.World.GetEntityType(new AssetLocation("lotrnpcs", code));

        // The server's ticks during the client's timing: their spacing shows whether the server keeps up.
        void TimeServerTicks(IServerPlayer player, int count)
        {
            List<double> ticks = new List<double>();
            long start = sapi.World.ElapsedMilliseconds + WaitSeconds * 1000L, end = start + Seconds * 1000L;
            serverListener = sapi.Event.RegisterGameTickListener(dt =>
            {
                long now = sapi.World.ElapsedMilliseconds;
                if (now < start) return;
                ticks.Add(dt * 1000);
                if (now < end) return;
                sapi.Event.UnregisterGameTickListener(serverListener);
                serverListener = 0;
                string text = "[lotrnpcs] bench server, " + count + " NPCs: " + BenchStats.Summarize(ticks, "ticks/s", "tick");
                sapi.Logger.Notification(text);
                if (player.ConnectionState == EnumClientState.Playing) player.SendMessage(GlobalConstants.GeneralChatGroup, text, EnumChatType.Notification);
            }, 0);
        }

        int Clear()
        {
            List<Entity> bench = new List<Entity>();
            // The loaded entities directly: their Values property copies the whole table first.
            foreach (KeyValuePair<long, Entity> pair in sapi.World.LoadedEntities)
                if (pair.Value.Attributes.GetBool(BenchAttr)) bench.Add(pair.Value);
            foreach (Entity e in bench) sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
            return bench.Count;
        }

        // ---- client ----

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Network.GetChannel(Channel).SetMessageHandler<BenchStart>(start => api.Event.EnqueueMainThreadTask(() => TimeFrames(start), "lotrnpcs-bench"));
        }

        void TimeFrames(BenchStart run)
        {
            StopTimer();
            List<double> frames = new List<double>();
            long start = capi.World.ElapsedMilliseconds + run.WaitSeconds * 1000L, end = start + run.Seconds * 1000L;
            BenchFrameTimer mine = timer = new BenchFrameTimer();
            mine.OnFrame = dt =>
            {
                long now = capi.World.ElapsedMilliseconds;
                if (now < start) return;
                frames.Add(dt * 1000);
                if (now < end) return;
                // Not while the game walks its renderers: unregister after this frame.
                mine.OnFrame = null;
                capi.Event.EnqueueMainThreadTask(() => { if (timer == mine) StopTimer(); }, "lotrnpcs-bench-stop");
                int drawn = capi.World.LoadedEntities.Values.Count(e => e.Code?.Domain == "lotrnpcs" && e.Alive);
                string text = "[lotrnpcs] bench client, " + run.Count + " spawned, " + drawn + " alive: " + BenchStats.Summarize(frames)
                    + "; vsync " + capi.Settings.Int["vsyncMode"] + ", max FPS " + capi.Settings.Int["maxFps"] + ", view distance " + capi.Settings.Int["viewDistance"];
                capi.Logger.Notification(text);
                capi.ShowChatMessage(text);
            };
            capi.Event.RegisterRenderer(mine, EnumRenderStage.Done, "lotrnpcs-bench");
        }

        void StopTimer()
        {
            if (timer != null) capi.Event.UnregisterRenderer(timer, EnumRenderStage.Done);
            timer = null;
        }

        public override void Dispose()
        {
            if (capi != null) StopTimer();
        }
    }
}
