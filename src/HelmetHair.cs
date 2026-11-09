using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// 0.0.80 (owner, 2026-11-04, Haradrim test: "when the helmet is on, hide the hair options underneath"): while an NPC
    /// wears head armor, the shapes of its hair skin parts (hairbase, hairextra; not beards or mustaches) are left out of
    /// its model, so hair doesn't poke through or hang out of helmets. Every faction's helmets, not only the Haradrim's.
    ///
    /// The game hides parts under gear by element name, and the game's hair shapes can't be listed ahead of time, so the
    /// names are read from the hair shapes themselves: the race model's customplayermodels file gives each part's shape
    /// template, the NPC's skinConfig gives the chosen variant. Names that also exist in the NPC's body shape are never
    /// removed. Client only; runs after seraphinventory has added the gear, so a helmet taken off brings the hair back
    /// on the next redraw.
    ///
    /// 0.0.81 (owner, 2026-11-04, Haradrim test: "beards cannot be worn with these helmets either. but when the helmet is
    /// toggled off, the hair and beard should come back as was"): helmets matching the behavior's "hideBeardUnder" codes
    /// (wildcards; gen gives the Haradrim armor mod's helmets, whose veils and masks cover the face) also hide the beard and
    /// mustache parts. Nothing is changed in the NPC's skin config: the parts are only left out of the drawn shape while
    /// the helmet is worn, so taking it off (looks menu) redraws the same hair, beard and mustache.
    ///
    /// 0.0.96 (owner, 2026-11-07: "with the new gundabad helmet, the ears stick out from the sides"): helmets matching
    /// "hideEarsUnder" also hide the ears part. Ear variants name their shape directly (variant shape.base) rather than
    /// through the part's shapeTemplate, so HairShapePath reads that too.
    /// </summary>
    public class EntityBehaviorHelmetHidesHair : EntityBehavior
    {
        public static readonly string[] HairParts = { "hairbase", "hairextra" };
        public static readonly string[] BeardParts = { "beard", "mustache" };
        public static readonly string[] EarParts = { "ears" };
        const string CacheKey = "lotrnpcs-helmethair";

        AssetLocation[] beardHelmets = Array.Empty<AssetLocation>();
        AssetLocation[] earHelmets = Array.Empty<AssetLocation>();

        public EntityBehaviorHelmetHidesHair(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            string[] codes = attributes?["hideBeardUnder"].AsArray<string>();
            if (codes != null) beardHelmets = codes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => new AssetLocation(c)).ToArray();
            string[] ears = attributes?["hideEarsUnder"].AsArray<string>();
            if (ears != null) earHelmets = ears.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => new AssetLocation(c)).ToArray();
        }

        /// <summary>Does this helmet hide the beard (its code matches one of the wildcard codes)?</summary>
        public static bool HidesBeard(AssetLocation helmet, IEnumerable<AssetLocation> patterns) =>
            helmet != null && patterns != null && patterns.Any(p => WildcardUtil.Match(p, helmet));

        /// <summary>The skin parts to leave out under this helmet (null: no helmet).</summary>
        public static string[] PartsUnder(AssetLocation helmet, IEnumerable<AssetLocation> beardPatterns) =>
            PartsUnder(helmet, beardPatterns, null);

        /// <summary>The skin parts to leave out under this helmet, ears too when it matches earPatterns (0.0.96).</summary>
        public static string[] PartsUnder(AssetLocation helmet, IEnumerable<AssetLocation> beardPatterns, IEnumerable<AssetLocation> earPatterns)
        {
            if (helmet == null) return null;
            IEnumerable<string> parts = HairParts;
            if (HidesBeard(helmet, beardPatterns)) parts = parts.Concat(BeardParts);
            if (HidesBeard(helmet, earPatterns)) parts = parts.Concat(EarParts);
            return parts.ToArray();
        }

        public override string PropertyName() => "lotrnpcs:helmethidehair";

        /// <summary>Is this worn item head armor (a helmet, not a hat or hood)?</summary>
        public static bool IsHelmetCategory(string clothesCategory) => clothesCategory == "armorhead";

        /// <summary>A part variant's shape path from a Player Model Lib model definition, or null (no shape template, no
        /// variant, or a variant without hair).</summary>
        public static string HairShapePath(JObject model, string part, string variant)
        {
            if (model == null || string.IsNullOrEmpty(variant) || variant == "none") return null;
            JObject def = (model["SkinnableParts"] as JArray)?.OfType<JObject>().FirstOrDefault(p => (string)p["code"] == part);
            string template = (string)def?["shapeTemplate"]?["base"];
            if (template != null) return template.Replace("{code}", variant);
            // 0.0.96: a variant with its own shape (the ears).
            JObject v = (def?["variants"] as JArray)?.OfType<JObject>().FirstOrDefault(x => (string)x["code"] == variant);
            return (string)v?["shape"]?["base"];
        }

        /// <summary>The element names of the hair shapes that may be removed: everything not also in the body.</summary>
        public static string[] RemovableNames(IEnumerable<string> hairNames, ISet<string> bodyNames) =>
            hairNames.Where(n => !string.IsNullOrEmpty(n) && !bodyNames.Contains(n)).Distinct().ToArray();

        public static IEnumerable<string> ElementNames(IEnumerable<ShapeElement> elements)
        {
            if (elements == null) yield break;
            foreach (ShapeElement e in elements)
            {
                yield return e.Name;
                foreach (string n in ElementNames(e.Children)) yield return n;
            }
        }

        /// <summary>The code of the worn head armor, or null.</summary>
        AssetLocation WornHelmet()
        {
            IInventory inv = entity.GetBehavior<EntityBehaviorContainer>()?.Inventory;
            if (inv == null) return null;
            foreach (ItemSlot slot in inv)
            {
                if (slot.Empty) continue;
                if (IsHelmetCategory(slot.Itemstack.Collectible.Attributes?["clothescategory"].AsString())) return slot.Itemstack.Collectible.Code;
            }
            return null;
        }

        string[] HairNames(ICoreClientAPI capi, string[] parts)
        {
            string modelCode = entity.WatchedAttributes.GetString("skinModel");
            ITreeAttribute applied = entity.WatchedAttributes.GetTreeAttribute("skinConfig")?.GetTreeAttribute("appliedParts");
            if (modelCode == null || applied == null) return Array.Empty<string>();
            var cache = ObjectCacheUtil.GetOrCreate(capi, CacheKey, () => new Dictionary<string, object>());

            AssetLocation modelLoc = new AssetLocation(modelCode);
            string modelKey = "model:" + modelLoc;
            if (!cache.TryGetValue(modelKey, out object modelObj))
            {
                IAsset asset = capi.Assets.TryGet(new AssetLocation(modelLoc.Domain, "config/customplayermodels/" + modelLoc.Path + ".json"));
                modelObj = asset == null ? null : JObject.Parse(asset.ToText())[modelLoc.Path] as JObject;
                cache[modelKey] = modelObj;
            }
            JObject model = modelObj as JObject;
            if (model == null) return Array.Empty<string>();

            var body = new HashSet<string>(ElementNames(entity.Properties.Client.LoadedShapeForEntity?.Elements));
            var names = new List<string>();
            foreach (string part in parts)
            {
                string path = HairShapePath(model, part, applied.GetString(part));
                if (path == null) continue;
                string shapeKey = "shape:" + path;
                if (!cache.TryGetValue(shapeKey, out object shapeNames))
                {
                    Shape shape = Shape.TryGet(capi, new AssetLocation(path).WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                    shapeNames = shape == null ? Array.Empty<string>() : ElementNames(shape.Elements).ToArray();
                    cache[shapeKey] = shapeNames;
                }
                names.AddRange((string[])shapeNames);
            }
            return RemovableNames(names, body);
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging, ref bool shapeIsCloned, ref string[] willDeleteElements)
        {
            if (!(entity.Api is ICoreClientAPI capi) || entityShape == null) return;
            try
            {
                string[] parts = PartsUnder(WornHelmet(), beardHelmets, earHelmets);
                if (parts == null) return;
                string[] names = HairNames(capi, parts);
                if (names.Length == 0) return;
                if (!shapeIsCloned)
                {
                    entityShape = entityShape.Clone();
                    shapeIsCloned = true;
                }
                entityShape.RemoveElements(names);
                willDeleteElements = willDeleteElements == null ? names : willDeleteElements.Append(names);
            }
            catch (Exception e)
            {
                capi.Logger.Warning("[lotrnpcs] {0}: could not hide the hair under its helmet: {1}", entity.Code, e.Message);
            }
        }
    }
}
