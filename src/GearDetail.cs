using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// 0.0.127 (owner, 2026-11-14: "Can the gear be simplified at all to not be so heavy on performance", chose "Strip
    /// tiny details only"; docs/notes/gear-pieces.md): a geared NPC is 100 to 420 shape elements, and the client poses
    /// every element of every visible NPC each frame. On this client, when worn gear is added to one of this mod's NPCs'
    /// shapes (the game's EntityBehaviorContainer.addGearToShape, which loads a fresh copy of the gear shape and then
    /// calls Shape.StepParentShape with it), the gear's tiny leaf elements (rivets, studs: every side under TinyGearSize
    /// voxels) are left out. Players, other mods' entities, the server and the armor mods' files are untouched.
    /// </summary>
    public static class GearDetail
    {
        public const double DefaultTinySize = 0.5, MinTinySize = 0.1, MaxTinySize = 4;
        const string HarmonyId = "lotrnpcs.geardetail";

        static Harmony harmony;
        static ICoreClientAPI capi;
        static bool enabled = true;
        static double size = DefaultTinySize;
        // The gear being added to one of our NPCs on this thread right now (addGearToShape's item code), else null.
        [ThreadStatic] static string adding;
        static readonly HashSet<string> logged = new HashSet<string>();

        /// <summary>Whether an element may go: no children, no attachment points, not attached by step parent itself,
        /// not a step parent target in its shape, and every side under size voxels.</summary>
        public static bool IsTiny(ShapeElement e, double size, ISet<string> targets)
        {
            if (e == null || e.From == null || e.To == null || e.From.Length < 3 || e.To.Length < 3) return false;
            if (e.Children != null && e.Children.Length > 0) return false;
            if (e.AttachmentPoints != null && e.AttachmentPoints.Length > 0) return false;
            if (!string.IsNullOrEmpty(e.StepParentName) || (e.Name != null && targets.Contains(e.Name))) return false;
            for (int k = 0; k < 3; k++) if (Math.Abs(e.To[k] - e.From[k]) >= size) return false;
            return true;
        }

        /// <summary>Leaves out the tiny elements of a gear shape's element tree, in place; returns how many went.</summary>
        public static int Strip(Shape shape, double size)
        {
            if (shape?.Elements == null) return 0;
            var targets = new HashSet<string>();
            void Collect(ShapeElement[] els) { foreach (ShapeElement e in els ?? Array.Empty<ShapeElement>()) { if (!string.IsNullOrEmpty(e?.StepParentName)) targets.Add(e.StepParentName); Collect(e?.Children); } }
            Collect(shape.Elements);
            int removed = 0;
            ShapeElement[] Keep(ShapeElement[] els)
            {
                if (els == null) return null;
                var kept = new List<ShapeElement>(els.Length);
                foreach (ShapeElement e in els)
                {
                    if (IsTiny(e, size, targets)) { removed++; continue; }
                    if (e != null && e.Children != null) e.Children = Keep(e.Children);
                    kept.Add(e);
                }
                return kept.Count == els.Length ? els : kept.ToArray();
            }
            // Top-level gear elements carry the step parent names that attach them, so they always stay.
            foreach (ShapeElement root in shape.Elements) if (root?.Children != null) root.Children = Keep(root.Children);
            return removed;
        }

        public static void Apply(ICoreClientAPI api, ClientConfig config)
        {
            if (harmony != null) return;
            capi = api;
            enabled = config.StripTinyGearDetail;
            size = Math.Clamp(config.TinyGearSize, MinTinySize, MaxTinySize);
            api.ChatCommands.Create("npcgeardetail")
                .WithDescription("LOTR NPCs: leave out the tiny pieces (rivets, studs) of NPCs' worn gear (on), keep every piece (off), or set the size in voxels below which a piece counts as tiny.")
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("on|off|size"))
                .HandleWith(args => OnCommand(args, config));
            MethodInfo addGear = AccessTools.TypeByName("Vintagestory.GameContent.EntityBehaviorContainer")?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "addGearToShape" && m.GetParameters().Length == 11);
            MethodInfo stepParent = typeof(Shape).GetMethod("StepParentShape", new[] { typeof(Shape), typeof(string), typeof(string), typeof(ILogger), typeof(Action<string, AssetLocation>) });
            if (addGear == null || stepParent == null) { api.Logger.Warning("[lotrnpcs] Gear hooks not found; NPC gear keeps every piece."); return; }
            harmony = new Harmony(HarmonyId);
            harmony.Patch(addGear, prefix: new HarmonyMethod(typeof(GearDetail).GetMethod(nameof(AddGearPrefix), BindingFlags.Static | BindingFlags.NonPublic)),
                finalizer: new HarmonyMethod(typeof(GearDetail).GetMethod(nameof(AddGearFinalizer), BindingFlags.Static | BindingFlags.NonPublic)));
            harmony.Patch(stepParent, prefix: new HarmonyMethod(typeof(GearDetail).GetMethod(nameof(StepParentPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
        }

        public static void Remove()
        {
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;
            capi = null;
        }

        static TextCommandResult OnCommand(TextCommandCallingArgs args, ClientConfig config)
        {
            string arg = (args[0] as string)?.Trim().ToLowerInvariant();
            if (arg == "on") enabled = true;
            else if (arg == "off") enabled = false;
            else if (arg != null && double.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double voxels))
            {
                size = Math.Clamp(voxels, MinTinySize, MaxTinySize);
                enabled = true;
            }
            else if (arg != null) return TextCommandResult.Error("Use on, off or a size in voxels.");
            if (arg != null)
            {
                config.StripTinyGearDetail = enabled;
                config.TinyGearSize = size;
                try { capi.StoreModConfig(config, ClientConfig.File); } catch (Exception) { }
                logged.Clear();
                // Rebuild our NPCs' shapes so the change shows at once.
                foreach (Entity e in capi.World.LoadedEntities.Values) if (e.Code?.Domain == "lotrnpcs") e.MarkShapeModified();
            }
            return TextCommandResult.Success(enabled ? "NPC gear leaves out pieces under " + size + " voxels on every side." : "NPC gear keeps every piece.");
        }

        static void AddGearPrefix(ICoreAPI api, Entity optionalTargetEntity, ItemStack stack)
        {
            adding = enabled && api?.Side == EnumAppSide.Client && optionalTargetEntity?.Code?.Domain == "lotrnpcs" ? stack?.Collectible?.Code?.ToString() ?? "?" : null;
        }

        static Exception AddGearFinalizer(Exception __exception)
        {
            adding = null;
            return __exception;
        }

        static void StepParentPrefix(Shape childShape)
        {
            string code = adding;
            if (code == null) return;
            int removed = Strip(childShape, size);
            if (logged.Add(code)) capi?.Logger.VerboseDebug("[lotrnpcs] gear {0}: left out {1} tiny pieces", code, removed);
        }
    }
}
