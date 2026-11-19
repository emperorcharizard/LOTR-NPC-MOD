using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace LotrNpcs
{
    /// <summary>One faction as a mod defines it (config/lotrnpcs-factions/*.json): its id, default side, stance,
    /// balance tier and member races. The server config (lotrnpcs-factions.json) then holds the admin's values.</summary>
    public sealed class FactionInfo
    {
        [JsonProperty("id")] public string Id;
        // "free", "shadow" or "none": allied by default to the rest of its side, and its join menu section.
        [JsonProperty("side")] public string Side = "none";
        [JsonProperty("stance")] public string Stance = "aggressive";
        [JsonProperty("tier")] public string Tier = "men";
        [JsonProperty("friendlyRaces")] public List<string> FriendlyRaces = new List<string>();
        // The mod (asset domain) whose file defines it: lotrnpcs for ours.
        [JsonIgnore] public string Source;
        public bool Shipped => Source == "lotrnpcs";

        // ---- Faction packs (FactionPackSystem): NPC types made at load from a shipped faction's ----
        // The shipped faction whose NPC types this one copies (AI, stats, sounds, weapons). Null: the faction brings
        // its own NPC types, as the shipped ones do.
        [JsonProperty("template")] public string Template;
        // Player Model Lib model code (such as lrc:sindar), the skin texture for the spawn item's fallback icon, and
        // the skin parts its NPCs may have (part -> variants). Missing: the template's.
        [JsonProperty("model")] public string Model;
        [JsonProperty("skin")] public string Skin;
        [JsonProperty("parts")] public Dictionary<string, List<string>> Parts;
        // Names its NPCs get when hired. Missing: the template's.
        [JsonProperty("names")] public List<string> Names;
        // Outfits by rank (recruit, warrior, captain, archer), each a list of item codes, as tools/data/armor.json.
        // Riders wear the warriors', archers the recruits' unless listed. Missing ranks: the template's.
        [JsonProperty("armor")] public Dictionary<string, List<List<string>>> Armor;
        // Mount entity codes for its riders. Missing: the template's.
        [JsonProperty("mounts")] public List<string> Mounts;
    }

    /// <summary>
    /// The factions every installed mod defines (1.0.5): ours come from config/lotrnpcs-factions/lotrnpcs.json, built
    /// from tools/data/factions.json; another mod adds its own with assets/&lt;its id&gt;/config/lotrnpcs-factions/*.json.
    /// Ours come first, in file order, then the others by mod and file name. An id already defined is skipped. The
    /// lists that were fixed in code (default factions and sides, tiers, spawn zone factions, join menu order) read
    /// this.
    /// </summary>
    public static class FactionRegistry
    {
        public const string AssetFolder = "config/lotrnpcs-factions/";
        public static readonly string[] Sides = { "free", "shadow", "none" };

        // Replaced whole, so readers on other threads always see a complete list.
        static volatile FactionInfo[] all = Array.Empty<FactionInfo>();
        static volatile Dictionary<string, FactionInfo> byId = new Dictionary<string, FactionInfo>();

        public static IReadOnlyList<FactionInfo> All => all;
        public static IEnumerable<string> Ids => all.Select(f => f.Id);
        public static bool Loaded => all.Length > 0;

        public static FactionInfo Get(string faction) =>
            faction != null && byId.TryGetValue(faction.Trim().ToLowerInvariant(), out FactionInfo info) ? info : null;

        /// <summary>The faction's default side: free, shadow or none (also for ids no mod defines).</summary>
        public static string SideOf(string faction) => Get(faction)?.Side ?? "none";

        /// <summary>Reads every mod's faction files (the systems that need them call this as the server starts).</summary>
        public static void Load(ICoreAPI api)
        {
            List<(string Source, string Json)> files = api.Assets.GetMany(AssetFolder, null, true)
                .OrderBy(a => a.Location.Domain == "lotrnpcs" ? 0 : 1).ThenBy(a => a.Location.Domain, StringComparer.Ordinal)
                .ThenBy(a => a.Location.Path, StringComparer.Ordinal)
                .Select(a => (a.Location.ToString(), a.ToText())).ToList();
            // The source is the asset's domain (lotrnpcs:config/... -> lotrnpcs).
            Set(Parse(files, msg => api.Logger.Error("[lotrnpcs] " + msg)));
            api.Logger.Notification("[lotrnpcs] {0} factions: {1}", all.Length, string.Join(", ", Ids));
        }

        /// <summary>Reads faction files from disk, for the offline tests.</summary>
        public static void LoadFiles(IEnumerable<string> paths) =>
            Set(Parse(paths.Select(p => ("lotrnpcs:" + System.IO.Path.GetFileName(p), System.IO.File.ReadAllText(p))), msg => Console.WriteLine("faction file: " + msg)));

        /// <summary>The factions in these files, in order, skipping bad entries and ids seen before (each reported to
        /// error). A file holds a list of factions or one faction.</summary>
        public static List<FactionInfo> Parse(IEnumerable<(string Source, string Json)> files, Action<string> error)
        {
            List<FactionInfo> list = new List<FactionInfo>();
            HashSet<string> seen = new HashSet<string>();
            foreach ((string source, string json) in files)
            {
                List<FactionInfo> found;
                try
                {
                    string text = json?.TrimStart() ?? "";
                    found = text.StartsWith("[") ? JsonConvert.DeserializeObject<List<FactionInfo>>(text)
                        : new List<FactionInfo> { JsonConvert.DeserializeObject<FactionInfo>(text) };
                }
                catch (Exception e)
                {
                    error?.Invoke(source + ": not readable, skipped: " + e.Message);
                    continue;
                }
                foreach (FactionInfo info in found ?? new List<FactionInfo>())
                {
                    string id = info?.Id?.Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(id) || id.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
                    {
                        error?.Invoke(source + ": faction id '" + info?.Id + "' must be letters, digits or _, skipped");
                        continue;
                    }
                    if (!seen.Add(id))
                    {
                        error?.Invoke(source + ": faction '" + id + "' is already defined, skipped");
                        continue;
                    }
                    info.Id = id;
                    info.Source = source.Contains(":") ? source.Substring(0, source.IndexOf(':')) : "lotrnpcs";
                    info.Template = string.IsNullOrWhiteSpace(info.Template) ? null : info.Template.Trim().ToLowerInvariant();
                    info.Side = info.Side?.Trim().ToLowerInvariant();
                    if (Array.IndexOf(Sides, info.Side) < 0)
                    {
                        if (info.Side != null) error?.Invoke(source + ": " + id + " side '" + info.Side + "' is not free, shadow or none; using none");
                        info.Side = "none";
                    }
                    info.Stance = string.Equals(info.Stance?.Trim(), "defensive", StringComparison.OrdinalIgnoreCase) ? "defensive" : "aggressive";
                    info.Tier = info.Tier?.Trim().ToLowerInvariant() ?? "men";
                    if (!FactionTiers.IsTier(info.Tier))
                    {
                        error?.Invoke(source + ": " + id + " tier '" + info.Tier + "' is unknown; using men");
                        info.Tier = "men";
                    }
                    info.FriendlyRaces = (info.FriendlyRaces ?? new List<string>()).Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList();
                    list.Add(info);
                }
            }
            return list;
        }

        public static void Set(IEnumerable<FactionInfo> factions)
        {
            FactionInfo[] list = factions?.Where(f => f?.Id != null).ToArray() ?? Array.Empty<FactionInfo>();
            byId = list.ToDictionary(f => f.Id);
            all = list;
        }
    }
}
