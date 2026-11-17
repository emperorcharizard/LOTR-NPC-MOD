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
    /// <summary>
    /// Attribute reads for the AI's hot path. TreeAttribute.GetString passes every value through string.Intern, a
    /// lookup in a global table that took longer than the rest of a targeting check, so these
    /// read the attribute directly. The text is the same; only the string object may differ, and nothing here
    /// compares strings by reference except caches checking that a value wasn't replaced.
    /// </summary>
    public static class Attrs
    {
        public static string String(ITreeAttribute tree, string key) => (tree?[key] as StringAttribute)?.value;

        /// <summary>EntityPlayer.PlayerUID without the interning.</summary>
        public static string Uid(EntityPlayer player) => String(player?.WatchedAttributes, "playerUID");
    }

    /// <summary>
    /// What the mod reads from an entity type's JSON, worked out once per type. The AI asks about every
    /// creature near every NPC about once a second, and reading the type's JSON each time cost string copies and LINQ.
    /// Thread-safe: natural spawn checks run on the world generation thread.
    /// </summary>
    public sealed class NpcTypeInfo
    {
        /// <summary>The lotrnpcsFaction attribute in lower case, or null for creatures without a faction.</summary>
        public string Faction { get; private set; }
        /// <summary>The lotrnpcsRank attribute as written (recruit, warrior, captain), or null.</summary>
        public string Rank { get; private set; }
        /// <summary>Does the type run the vanilla task AI (animals, traders, NPCs)?</summary>
        public bool HasTaskAi { get; private set; }
        /// <summary>The "maxhealth" of the type's health behavior, or 0.</summary>
        public float MaxHealth { get; private set; }
        /// <summary>May a new NPC of this type spawn bare-headed (lotrnpcsHelmetOptional)? True when the type
        /// doesn't say, as before.</summary>
        public bool HelmetOptional { get; private set; } = true;
        // FactionSystem.IsMonster's answer under the monster list with this stamp (main thread only).
        internal int MonsterStamp = -1;
        internal bool Monster;

        static readonly ConditionalWeakTable<EntityProperties, NpcTypeInfo> Cache = new ConditionalWeakTable<EntityProperties, NpcTypeInfo>();
        static readonly ConditionalWeakTable<EntityProperties, NpcTypeInfo>.CreateValueCallback ReadType = Read;

        public static NpcTypeInfo Of(EntityProperties type) =>
            type == null ? null : Cache.TryGetValue(type, out NpcTypeInfo info) ? info : Cache.GetValue(type, ReadType);

        public static NpcTypeInfo Of(Entity entity) => Of(entity?.Properties);

        static NpcTypeInfo Read(EntityProperties type)
        {
            NpcTypeInfo info = new NpcTypeInfo
            {
                Faction = type.Attributes?["lotrnpcsFaction"].AsString(null)?.Trim().ToLowerInvariant(),
                Rank = type.Attributes?["lotrnpcsRank"].AsString(null),
                HelmetOptional = type.Attributes?["lotrnpcsHelmetOptional"].AsBool(true) ?? true,
            };
            foreach (JsonObject behavior in type.Server?.BehaviorsAsJsonObj ?? Array.Empty<JsonObject>())
            {
                string code = behavior?["code"].AsString();
                if (code == "taskai") info.HasTaskAi = true;
                else if (code == "health") info.MaxHealth = behavior["maxhealth"].AsFloat(0);
            }
            return info;
        }
    }
}
