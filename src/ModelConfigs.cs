using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace LotrNpcs
{
    /// <summary>
    /// Finds a Player Model Lib model's definition by its code ("domain:key"), as Player Model Lib does: in any
    /// config/customplayermodels file of that domain. Our own files are named after their key; LOTR Races and Classes
    /// names its files differently (lrc:freepeople is in freepeople-char.json), so those are searched. Cached per
    /// game session.
    /// </summary>
    public static class ModelConfigs
    {
        const string Folder = "config/customplayermodels/";
        static readonly Dictionary<string, JObject> cache = new Dictionary<string, JObject>();
        static readonly JsonLoadSettings Lenient = new JsonLoadSettings { CommentHandling = CommentHandling.Ignore };

        public static JObject Find(ICoreAPI api, string modelCode)
        {
            if (api == null || string.IsNullOrEmpty(modelCode)) return null;
            AssetLocation code = new AssetLocation(modelCode);
            string key = api.Side + "|" + code;
            lock (cache)
            {
                if (cache.TryGetValue(key, out JObject found)) return found;
                found = null;
                try
                {
                    IAsset own = api.Assets.TryGet(new AssetLocation(code.Domain, Folder + code.Path + ".json"));
                    found = own == null ? null : ModelIn(own.ToText(), code.Path);
                    if (found == null)
                    {
                        foreach (IAsset asset in api.Assets.GetMany(Folder, code.Domain, false))
                        {
                            if ((found = ModelIn(asset.ToText(), code.Path)) != null) break;
                        }
                    }
                }
                catch (Exception e)
                {
                    api.Logger.Warning("[lotrnpcs] Can't read the Player Model Lib model {0}: {1}", modelCode, e.Message);
                }
                return cache[key] = found;
            }
        }

        /// <summary>The model named key in one customplayermodels file's text, or null. Public for tests.</summary>
        public static JObject ModelIn(string json, string key)
        {
            JObject root = JObject.Parse(json, Lenient);
            return root.Properties().FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))?.Value as JObject;
        }
    }
}
