using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace LotrNpcs
{
    /// <summary>
    /// Faction packs (1.0.5): a faction file that names a "template" (a shipped faction) gets NPC types made from that
    /// faction's, with its own id, model, looks, names, outfits and mounts, and spawn items in the creative tab. The
    /// types are added as assets before the game reads entity and item types (its loader runs at 0.2), so the rest of
    /// the mod treats them like ours: test-&lt;faction&gt;-&lt;rank&gt; in the lotrnpcs domain. Names and talk lines
    /// come from the pack's own lang file (lotrnpcs:item-creature-test-&lt;faction&gt;-&lt;rank&gt;,
    /// lotrnpcs:folk-&lt;faction&gt;).
    /// </summary>
    public class FactionPackSystem : ModSystem
    {
        const string EntityFolder = "entities/", CreatureItem = "itemtypes/creature.json";
        static readonly string[] LowerRanks = { "recruit", "warrior", "captain" };

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;
        public override double ExecuteOrder() => 0.15;

        public override void AssetsLoaded(ICoreAPI api)
        {
            FactionRegistry.Load(api);
            List<FactionInfo> packs = FactionRegistry.All.Where(f => !string.IsNullOrWhiteSpace(f.Template)).ToList();
            if (packs.Count == 0) return;
            try
            {
                Build(api, packs);
            }
            catch (Exception e)
            {
                api.Logger.Error("[lotrnpcs] Faction packs: could not make NPC types: " + e);
            }
        }

        void Build(ICoreAPI api, List<FactionInfo> packs)
        {
            // Our entity types by faction: (asset, JSON, rank), ranked and not hidden (retired ones are hidden).
            Dictionary<string, List<(IAsset Asset, JObject Json, string Rank)>> byFaction = new Dictionary<string, List<(IAsset, JObject, string)>>();
            HashSet<string> codes = new HashSet<string>();
            foreach (IAsset asset in api.Assets.GetMany(EntityFolder, "lotrnpcs", true))
            {
                JObject json = JObject.Parse(asset.ToText());
                string code = (string)json["code"];
                if (code != null) codes.Add(code);
                JObject attrs = json["attributes"] as JObject;
                string faction = (string)attrs?["lotrnpcsFaction"], rank = (string)attrs?["lotrnpcsRank"];
                if (faction == null || rank == null || attrs["inCreativeInventory"]?.Type == JTokenType.Boolean && !(bool)attrs["inCreativeInventory"]) continue;
                if (!byFaction.TryGetValue(faction, out var list)) byFaction[faction] = list = new List<(IAsset, JObject, string)>();
                list.Add((asset, json, rank));
            }

            IAsset itemAsset = api.Assets.TryGet(new AssetLocation("lotrnpcs", CreatureItem));
            JObject item = itemAsset == null ? null : JObject.Parse(itemAsset.ToText());
            int made = 0;
            foreach (FactionInfo pack in packs)
            {
                string template = pack.Template.Trim().ToLowerInvariant();
                if (!byFaction.TryGetValue(template, out var types))
                {
                    api.Logger.Error("[lotrnpcs] Faction {0}: template '{1}' has no NPC types; its NPCs are not made", pack.Id, template);
                    continue;
                }
                foreach ((IAsset asset, JObject json, string rank) in types.OrderBy(t => (string)t.Json["code"], StringComparer.Ordinal))
                {
                    string code = "test-" + pack.Id + "-" + rank;
                    if (codes.Contains(code))
                    {
                        api.Logger.Warning("[lotrnpcs] Faction {0}: NPC type {1} already exists, kept", pack.Id, code);
                        continue;
                    }
                    JObject copy = Entity(json, code, pack, rank);
                    api.Assets.Add(new AssetLocation("lotrnpcs", EntityFolder + code + ".json"), NewAsset(asset, Encoding.UTF8.GetBytes(copy.ToString(Formatting.None)), new AssetLocation("lotrnpcs", EntityFolder + code + ".json")));
                    codes.Add(code);
                    if (item != null) AddSpawnItem(item, (string)json["code"], code, copy, pack);
                    made++;
                }
            }
            if (item != null && made > 0) itemAsset.Data = Encoding.UTF8.GetBytes(item.ToString(Formatting.None));
            api.Logger.Notification("[lotrnpcs] Faction packs: made {0} NPC types for {1}", made, string.Join(", ", packs.Select(p => p.Id + " (from " + p.Template + ")")));
        }

        /// <summary>A copy of a template NPC type as the pack's faction.</summary>
        public static JObject Entity(JObject template, string code, FactionInfo pack, string rank)
        {
            JObject copy = (JObject)template.DeepClone();
            copy["code"] = code;
            JObject attrs = (JObject)copy["attributes"];
            attrs["lotrnpcsFaction"] = pack.Id;
            if (pack.Parts != null) attrs["lotrnpcsSkinParts"] = JObject.FromObject(pack.Parts);
            if (pack.Names != null && pack.Names.Count > 0)
            {
                attrs["lotrnpcsNames"] = new JArray(pack.Names);
                attrs.Remove("lotrnpcsSurnames");
            }
            if (rank == "rider" && pack.Mounts != null && pack.Mounts.Count > 0) attrs["lotrnpcsMounts"] = new JArray(pack.Mounts);
            List<List<string>> sets = Outfits(pack, rank);
            if (sets != null)
            {
                // The looks menu's helmets: its own outfits' and the lower ranks'.
                int own = Array.IndexOf(LowerRanks, Base(rank));
                IEnumerable<List<string>> all = sets.Concat(LowerRanks.Take(Math.Max(0, own)).SelectMany(r => Outfits(pack, r) ?? new List<List<string>>()));
                List<string> helmets = all.SelectMany(s => s).Where(c => c.Contains("armor-head-") && !c.Contains("@")).Distinct().ToList();
                if (helmets.Count > 0) attrs["lotrnpcsHelmets"] = new JArray(helmets); else attrs.Remove("lotrnpcsHelmets");
            }
            foreach (string side in new[] { "client", "server" })
            {
                foreach (JObject behavior in (copy[side]?["behaviors"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                {
                    string b = (string)behavior["code"];
                    if (b == "lotrnpcs:presetskinmodel" && pack.Model != null) behavior["skinModel"] = pack.Model;
                    if (b == "lotrnpcs:startinggear" && sets != null) behavior["sets"] = JArray.FromObject(sets);
                }
            }
            return copy;
        }

        static string Base(string rank) => rank == "rider" ? "warrior" : rank;

        /// <summary>The pack's outfits for a rank: riders wear the warriors', archers the recruits' unless listed;
        /// null to keep the template's.</summary>
        static List<List<string>> Outfits(FactionInfo pack, string rank)
        {
            if (pack.Armor == null) return null;
            if (pack.Armor.TryGetValue(Base(rank), out var sets) && sets?.Count > 0) return sets;
            if (rank == "archer" && pack.Armor.TryGetValue("recruit", out sets) && sets?.Count > 0) return sets;
            return null;
        }

        /// <summary>Adds the new type's spawn item to our creature item: a variant, its portrait and icon texture
        /// copied from the template's, and a place in the LOTR NPCs tab after the last faction.</summary>
        public static void AddSpawnItem(JObject item, string templateCode, string code, JObject entity, FactionInfo pack)
        {
            JArray states = (JArray)item["variantgroups"][0]["states"];
            states.Add(code);
            if (item["texturesByType"]?["*-" + templateCode] is JToken tex)
            {
                JToken t = tex.DeepClone();
                if (pack.Skin != null && t["seraph"] is JObject seraph) seraph["base"] = pack.Skin;
                item["texturesByType"]["*-" + code] = t;
            }
            if (item["attributesByType"]?["*-" + templateCode] is JToken attrs)
            {
                JToken a = attrs.DeepClone();
                if (a["lotrnpcsPortrait"] is JObject portrait)
                {
                    if (pack.Model != null) portrait["model"] = pack.Model;
                    if (pack.Skin != null) portrait["skin"] = pack.Skin;
                    if (pack.Parts != null && portrait["parts"] is JObject parts)
                    {
                        foreach (KeyValuePair<string, List<string>> part in pack.Parts)
                        {
                            if (part.Value?.Count > 0 && parts[part.Key] != null) parts[part.Key] = part.Value[0];
                        }
                    }
                    if (entity["server"]?["behaviors"] is JArray server && server.OfType<JObject>().FirstOrDefault(b => (string)b["code"] == "lotrnpcs:startinggear")?["sets"] is JArray sets)
                        portrait["sets"] = sets.DeepClone();
                }
                item["attributesByType"]["*-" + code] = a;
            }
            if (item["creativeinventoryStacksByType"] is JObject byType)
            {
                foreach (JProperty prop in byType.Properties())
                {
                    foreach (JObject tab in (prop.Value as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                    {
                        if (tab["stacks"] is JArray stacks) stacks.Add(new JObject { ["type"] = "item", ["code"] = "lotrnpcs:creature-" + code });
                    }
                }
            }
        }

        /// <summary>A new asset of the game's own asset class (as the template's), so the loaders treat it like a file
        /// from a mod.</summary>
        static IAsset NewAsset(IAsset like, byte[] data, AssetLocation location)
        {
            try
            {
                return (IAsset)Activator.CreateInstance(like.GetType(), data, location, like.Origin);
            }
            catch (Exception)
            {
                return new MemoryAsset(data, location, like.Origin);
            }
        }

        sealed class MemoryAsset : IAsset
        {
            public MemoryAsset(byte[] data, AssetLocation location, IAssetOrigin origin) { Data = data; Location = location; Origin = origin; }
            public string Name => Location.GetName();
            public AssetLocation Location { get; }
            public IAssetOrigin Origin { get; set; }
            public byte[] Data { get; set; }
            public bool IsPatched { get; set; }
            public T ToObject<T>(JsonSerializerSettings settings = null) => JsonConvert.DeserializeObject<T>(ToText(), settings);
            public string ToText() => Encoding.UTF8.GetString(Data);
            public BitmapRef ToBitmap(Vintagestory.API.Client.ICoreClientAPI capi) => null;
            public bool IsLoaded() => Data != null;
        }
    }
}
