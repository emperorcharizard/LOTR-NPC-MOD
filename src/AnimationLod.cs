using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace LotrNpcs
{
    /// <summary>The client's own settings (ModConfig/lotrnpcs-client.json): what each player picks for their PC.</summary>
    public class ClientConfig
    {
        public const string File = "lotrnpcs-client.json";
        public string[] Instructions =
        {
            "LOTR NPCs, settings for this computer only (not the server).",
            "FarAnimationLod (true by default; the chat command .npcanimlod on / off / <blocks> changes it in game): NPCs of this mod farther than AnimationFullRateWithin blocks from you animate every 2nd frame, past twice that every 3rd and past three times that every 4th, so big battles cost less frame time. Up close nothing changes. false animates every NPC every frame, as the game does.",
            "AnimationFullRateWithin (10 by default since 0.0.116 testing, owner 2026-11-12; 4 to 256): how many blocks around you NPCs always animate every frame.",
        };
        public bool FarAnimationLod = true;
        public double AnimationFullRateWithin = AnimationLod.DefaultFullRateWithin;
    }

    /// <summary>
    /// Animation LOD for this mod's NPCs (2026-11-12, owner: "research ways optimization / fps can be improved then act
    /// on it"; prompts/fps-optimization.md). The client frame with many NPCs is mostly the game's per-NPC pose
    /// calculation (AnimationManager.OnClientFrame, profiled as esr-afteranim, about 30 µs per drawn NPC). The game
    /// already skips it for NPCs off screen; this skips it on some frames for far ones, handing the skipped time to the
    /// next run so animations keep their pace. OptiTime has the same idea for all entities but turns it off when Combat
    /// Overhaul's legacy library patches this method, so this one only ever touches lotrnpcs entities.
    /// </summary>
    public static class AnimationLod
    {
        public const double DefaultFullRateWithin = 10, MinFullRateWithin = 4, MaxFullRateWithin = 256;
        const string HarmonyId = "lotrnpcs.animationlod";

        sealed class State { public int Count; public float Skipped; }

        static readonly ConditionalWeakTable<Entity, State> states = new ConditionalWeakTable<Entity, State>();
        static Harmony harmony;
        static ICoreClientAPI capi;
        static double near = DefaultFullRateWithin;
        static bool enabled;
        static ClientConfig config;

        /// <summary>Every how many frames an NPC this far away (squared blocks) is animated: 1 within the full-rate
        /// distance, 2 within twice it, 3 within three times it, else 4.</summary>
        public static int Interval(double distanceSq, double fullRateWithin)
        {
            double n = Math.Clamp(fullRateWithin, MinFullRateWithin, MaxFullRateWithin);
            if (distanceSq <= n * n) return 1;
            if (distanceSq <= 4 * n * n) return 2;
            if (distanceSq <= 9 * n * n) return 3;
            return 4;
        }

        /// <summary>One frame for one NPC: whether to animate now, and how much time to animate by (this frame's plus
        /// the skipped ones'). A new NPC's count starts at its id, so NPCs at one distance skip different frames.</summary>
        public static bool Step(ref int count, ref float skipped, ref float dt, int interval)
        {
            if (interval <= 1 || ++count >= interval)
            {
                count = 0;
                dt += skipped;
                skipped = 0;
                return true;
            }
            skipped += dt;
            return false;
        }

        public static void Apply(ICoreClientAPI api)
        {
            if (harmony != null) return;
            config = Load(api);
            enabled = config.FarAnimationLod;
            near = Math.Clamp(config.AnimationFullRateWithin, MinFullRateWithin, MaxFullRateWithin);
            capi = api;
            // Players switch it here without a restart, for their own PC (and to compare frame rates).
            api.ChatCommands.Create("npcanimlod")
                .WithDescription("LOTR NPCs: far NPCs animate less often (on), every NPC every frame (off), or set how many blocks around you stay at full rate.")
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("on|off|blocks"))
                .HandleWith(OnCommand);
            MethodInfo target = typeof(AnimationManager).GetMethod("OnClientFrame", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(float) }, null);
            if (target == null) { api.Logger.Warning("[lotrnpcs] AnimationManager.OnClientFrame not found; far NPCs animate every frame."); return; }
            harmony = new Harmony(HarmonyId);
            harmony.Patch(target, new HarmonyMethod(typeof(AnimationLod).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic)) { priority = Priority.First });
        }

        static ClientConfig Load(ICoreClientAPI api)
        {
            ClientConfig config = null;
            try { config = api.LoadModConfig<ClientConfig>(ClientConfig.File); }
            catch (Exception e) { api.Logger.Warning("[lotrnpcs] Could not read ModConfig/{0}, using the defaults: {1}", ClientConfig.File, e.Message); }
            if (config == null)
            {
                config = new ClientConfig();
                try { api.StoreModConfig(config, ClientConfig.File); } catch (Exception) { }
            }
            return config;
        }

        static TextCommandResult OnCommand(TextCommandCallingArgs args)
        {
            string arg = (args[0] as string)?.Trim().ToLowerInvariant();
            if (arg == "on") enabled = true;
            else if (arg == "off") enabled = false;
            else if (arg != null && double.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double blocks))
            {
                near = Math.Clamp(blocks, MinFullRateWithin, MaxFullRateWithin);
                enabled = true;
            }
            else if (arg != null) return TextCommandResult.Error("Use on, off or a number of blocks.");
            if (config != null && arg != null)
            {
                config.FarAnimationLod = enabled;
                config.AnimationFullRateWithin = near;
                try { capi.StoreModConfig(config, ClientConfig.File); } catch (Exception) { }
            }
            return TextCommandResult.Success(enabled ? "Far NPCs animate less often: every frame within " + near + " blocks, then every 2nd, 3rd and 4th frame." : "Every NPC animates every frame.");
        }

        public static void Remove()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            capi = null;
            config = null;
        }

        // Harmony prefix: false skips this frame's animation. Players and other mods' entities always pass.
        static bool Prefix(Entity ___entity, ref float __0)
        {
            Entity e = ___entity;
            if (!enabled || e == null || !e.IsRendered || e.Code?.Domain != "lotrnpcs") return true;
            EntityPos me = capi?.World?.Player?.Entity?.Pos;
            if (me == null) return true;
            int interval = Interval(e.Pos.SquareDistanceTo(me.X, me.Y, me.Z), near);
            State st = states.GetValue(e, k => new State { Count = (int)(k.EntityId % 4) });
            return Step(ref st.Count, ref st.Skipped, ref __0, interval);
        }
    }
}
