using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>One change (or several) to a recruit's looks, sent by its owner's appearance dialog. Null fields
    /// stay as they are.</summary>
    public class LooksRequest
    {
        public string Name;
        /// <summary>A helmet item code from the NPC's choices, or "none"; "on" and "off" still work.</summary>
        public string Helmet;
        /// <summary>A weapon item code from the NPC's list, or "none".</summary>
        public string Weapon;
        /// <summary>"on" or "off": the NPC's shield.</summary>
        public string Shield;
        /// <summary>Skin part code -> variant code.</summary>
        public Dictionary<string, string> Parts;
    }

    /// <summary>
    /// Looks and names: random helmets, a menu for the owner to choose helmet, face, weapon and name, and a random
    /// race-fitting name on hiring. Only cosmetic: protection comes from the rank (lotrnpcs:rankarmor), never from
    /// the gear worn, and weapons are drawn on the client only (Weapons.cs).
    /// </summary>
    public static class Looks
    {
        /// <summary>The helmet an NPC doesn't wear, kept with it (saved, synced, never dropped or drawn).</summary>
        public const string KeptHelmetAttr = "lotrnpcsKeptHelmet";
        /// <summary>Set once an NPC has a proper name (random on hire, or its owner's), so it is never renamed.</summary>
        public const string NamedAttr = "lotrnpcsNamed";
        public const string NoWeapon = "none";
        public const int MaxNameLength = 24;
        /// <summary>The gear slot helmets go in. Face pieces (the Easterling captain's coif, the Haradrim mask) are
        /// worn elsewhere and never count as helmets.</summary>
        public static readonly int HeadSlot = (int)EnumCharacterDressType.ArmorHead;
        /// <summary>The Player Model Lib skin parts the owner may change, in the dialog's order: face and hair only
        /// (no body, underwear or voice).</summary>
        public static readonly string[] FaceParts = { "baseskin", "face", "eyecolor", "facialexpression", "ears", "hairbase", "hairextra", "haircolor", "brows", "mustache", "beard" };

        // Behaviors exist only on an entity in a world (not on test entities).
        static T Behavior<T>(Entity e) where T : EntityBehavior => e.Api == null ? null : e.GetBehavior<T>();

        // ---- Helmets ----

        /// <summary>Whether a newly spawned NPC of this rank wears its helmet. A type that may not spawn bare-headed
        /// (helmetOptional false) always does; its owner can still take it off.</summary>
        public static bool WearsHelmet(string rank, double chance, bool captainsAlways, Random rand, bool helmetOptional = true) =>
            !helmetOptional || (captainsAlways && rank == "captain") || rand.NextDouble() < chance;

        // On an NPC's watched attributes, sends just this key, not all of them (Recruit.DropWatched).
        static void DropKept(ITreeAttribute attrs)
        {
            if (attrs is SyncedTreeAttribute synced) Recruit.DropWatched(synced, KeptHelmetAttr);
            else attrs.RemoveAttribute(KeptHelmetAttr);
        }

        /// <summary>Takes the helmet off into the kept attribute, or puts the kept one back on. True if it moved.</summary>
        public static bool MoveHelmet(ItemSlot head, ITreeAttribute attrs, bool on, IWorldAccessor world)
        {
            if (head == null || attrs == null) return false;
            if (!on)
            {
                if (head.Empty || attrs.HasAttribute(KeptHelmetAttr)) return false;
                attrs.SetItemstack(KeptHelmetAttr, head.Itemstack);
                head.Itemstack = null;
                head.MarkDirty();
                return true;
            }
            ItemStack kept = KeptHelmet(attrs, world);
            if (kept == null || !head.Empty) return false;
            head.Itemstack = kept;
            DropKept(attrs);
            head.MarkDirty();
            return true;
        }

        public static ItemStack KeptHelmet(ITreeAttribute attrs, IWorldAccessor world)
        {
            ItemStack kept = attrs?.GetItemstack(KeptHelmetAttr);
            if (kept != null && world != null && kept.Collectible == null && !kept.ResolveBlockOrItem(world)) return null;
            return kept;
        }

        public static ItemSlot Head(Entity npc)
        {
            InventoryBase inv = Behavior<EntityBehaviorTexturedClothing>(npc)?.Inventory;
            return inv != null && HeadSlot < inv.Count ? inv[HeadSlot] : null;
        }

        /// <summary>The dialog's choice for no helmet at all.</summary>
        public const string NoHelmet = "none";
        /// <summary>Entity type attribute (written by gen_testnpcs.py): every helmet the type can spawn with.</summary>
        public const string HelmetsAttr = "lotrnpcsHelmets";

        /// <summary>An item code with its domain ("game:" when it has none), so codes compare the same everywhere.</summary>
        public static string FullCode(string code) => string.IsNullOrEmpty(code) ? null : new AssetLocation(GearCode.ItemCode(code)).ToString();

        public static string CodeOf(ItemStack stack) => stack?.Collectible?.Code?.ToString();

        /// <summary>
        /// The helmets an owner may choose: the type's own helmets that exist in this world, in order, then the one
        /// it wears and the one it keeps when they aren't among them. "No helmet" isn't in the list.
        /// </summary>
        public static string[] HelmetChoices(string[] typeHelmets, System.Func<string, bool> exists, ItemStack worn, ItemStack kept)
        {
            List<string> list = new List<string>();
            void Add(string code) { if (code != null && !list.Contains(code)) list.Add(code); }
            foreach (string code in typeHelmets ?? Array.Empty<string>())
            {
                if (exists == null || exists(code)) Add(FullCode(code));
            }
            Add(CodeOf(worn));
            Add(CodeOf(kept));
            return list.ToArray();
        }

        public static string[] HelmetChoices(Entity npc)
        {
            ItemSlot head = Head(npc);
            if (head == null) return Array.Empty<string>();
            IWorldAccessor world = npc.World;
            System.Func<string, bool> exists = null;
            if (world != null) exists = code => GearCode.Item(world, code) != null;
            string[] typeHelmets = npc.Properties?.Attributes?[HelmetsAttr].AsArray<string>(null);
            return HelmetChoices(typeHelmets, exists, head.Itemstack, KeptHelmet(npc.WatchedAttributes, world));
        }

        /// <summary>Puts on the helmet with this code: the kept one when it matches, else a new one in place of the one
        /// worn (gear never drops). True if anything changed.</summary>
        public static bool WearHelmet(ItemSlot head, ITreeAttribute attrs, string code, IWorldAccessor world)
        {
            if (head == null || attrs == null || code == null) return false;
            if (CodeOf(head.Itemstack) == code) return false;
            ItemStack stack = KeptHelmet(attrs, world);
            if (CodeOf(stack) != code)
            {
                Item item = world?.GetItem(new AssetLocation(code));
                if (item == null) return false;
                stack = new ItemStack(item);
            }
            head.Itemstack = stack;
            DropKept(attrs);
            head.MarkDirty();
            return true;
        }

        /// <summary>Server, at first spawn: takes the helmet off unless the roll says it wears one.</summary>
        public static void RollHelmet(Entity npc, FactionConfig config)
        {
            if (config == null) return;
            NpcTypeInfo type = NpcTypeInfo.Of(npc);
            if (WearsHelmet(type?.Rank, Math.Clamp(config.HelmetChance, 0, 1), config.CaptainsAlwaysHelmeted, npc.World.Rand, type?.HelmetOptional ?? true)) return;
            if (MoveHelmet(Head(npc), npc.WatchedAttributes, false, npc.World)) StoreGear(npc);
        }

        public static void StoreGear(Entity npc)
        {
            Behavior<EntityBehaviorTexturedClothing>(npc)?.storeInv();
            npc.WatchedAttributes.MarkPathDirty(KeptHelmetAttr);
        }

        // ---- Names ----

        static readonly Regex NameChars = new Regex(@"^\p{L}[\p{L}' \-]*$");

        /// <summary>The name trimmed with single spaces, or null when it is empty, too long, or has anything but
        /// letters, spaces, apostrophes and hyphens (starting with a letter).</summary>
        public static string CleanName(string raw)
        {
            if (raw == null) return null;
            string name = Regex.Replace(raw.Trim(), @"\s+", " ");
            return name.Length >= 1 && name.Length <= MaxNameLength && NameChars.IsMatch(name) ? name : null;
        }

        public static string NameOf(Entity npc) => npc.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name");

        public static void SetName(Entity npc, string name)
        {
            ITreeAttribute tag = npc.WatchedAttributes.GetTreeAttribute("nametag");
            if (tag == null) npc.WatchedAttributes["nametag"] = tag = new TreeAttribute();
            tag.SetString("name", name);
            npc.WatchedAttributes.SetBool(NamedAttr, true);
            npc.WatchedAttributes.MarkPathDirty("nametag");
            npc.WatchedAttributes.MarkPathDirty(NamedAttr);
        }

        /// <summary>A random name from the NPC type's "lotrnpcsNames" list, with a random "lotrnpcsSurnames" entry
        /// after it when the type has those (hobbits), or null when it has no list.</summary>
        public static string RandomName(EntityProperties type, Random rand, string model = null)
        {
            string[] names = ByModel(type?.Attributes, model)?["names"].AsArray<string>(null) ?? type?.Attributes?["lotrnpcsNames"].AsArray<string>(null);
            if (names == null || names.Length == 0) return null;
            string name = names[rand.Next(names.Length)];
            string[] surnames = type.Attributes["lotrnpcsSurnames"].AsArray<string>(null);
            return surnames == null || surnames.Length == 0 ? name : name + " " + surnames[rand.Next(surnames.Length)];
        }

        /// <summary>Server, when hired: a name befitting the race, unless it already has a proper one.</summary>
        public static bool NameOnHire(Entity npc, Random rand)
        {
            if (npc.WatchedAttributes.GetBool(NamedAttr)) return false;
            string name = RandomName(npc.Properties, rand, Attrs.String(npc.WatchedAttributes, "skinModel"));
            if (name == null) return false;
            SetName(npc, name);
            return true;
        }

        // ---- Faces ----

        /// <summary>
        /// The face parts a Player Model Lib race model offers, part code -> its standard variants in order, read
        /// from the model's config/customplayermodels file (the same file Player Model Lib loads). Cached per model.
        /// </summary>
        public static Dictionary<string, string[]> PartsOf(ICoreAPI api, string model)
        {
            if (string.IsNullOrEmpty(model)) return Empty;
            lock (partsCache)
            {
                if (partsCache.TryGetValue(model, out Dictionary<string, string[]> cached)) return cached;
                return partsCache[model] = ParseParts(ModelConfigs.Find(api, model));
            }
        }

        /// <summary>
        /// 1.0.4: what this NPC's owner may pick: its model's parts, limited to its type's own picks
        /// (lotrnpcsSkinParts, such as dark hair for Gondor on LOTR Races and Classes' shared Men of the West model).
        /// A part the type limits to one variant isn't offered.
        /// </summary>
        public static Dictionary<string, string[]> PartsFor(Entity npc)
        {
            Dictionary<string, string[]> all = PartsOf(npc.Api, Attrs.String(npc.WatchedAttributes, "skinModel"));
            return Limit(all, SkinLimits(npc.Properties?.Attributes, Attrs.String(npc.WatchedAttributes, "skinModel")));
        }

        /// <summary>1.0.7: a type with more than one model (lotrnpcs:presetskinmodel skinModels) keeps each model's own
        /// looks and names in lotrnpcsByModel: model -> {parts, names}. Null when it has none for this model.</summary>
        public static JsonObject ByModel(JsonObject typeAttributes, string model)
        {
            if (model == null) return null;
            JsonObject entry = typeAttributes?["lotrnpcsByModel"]?[model];
            return entry != null && entry.Exists ? entry : null;
        }

        /// <summary>A type's part limits (attributes.lotrnpcsSkinParts, or its model's in lotrnpcsByModel): part code
        /// -> the variants it may have.</summary>
        public static Dictionary<string, string[]> SkinLimits(JsonObject typeAttributes, string model = null)
        {
            var limits = new Dictionary<string, string[]>();
            JsonObject parts = ByModel(typeAttributes, model)?["parts"] ?? typeAttributes?["lotrnpcsSkinParts"];
            if (parts == null || !parts.Exists || !(parts.Token is JObject obj)) return limits;
            foreach (JProperty p in obj.Properties())
            {
                string[] codes = (p.Value as JArray)?.Select(v => (string)v).Where(v => v != null).ToArray();
                if (codes != null && codes.Length > 0) limits[p.Name] = codes;
            }
            return limits;
        }

        /// <summary>The parts with each limited one cut down to its allowed variants, in the model's order. Public for tests.</summary>
        public static Dictionary<string, string[]> Limit(Dictionary<string, string[]> parts, Dictionary<string, string[]> limits)
        {
            if (limits.Count == 0) return parts;
            var result = new Dictionary<string, string[]>();
            foreach (KeyValuePair<string, string[]> part in parts)
            {
                string[] variants = limits.TryGetValue(part.Key, out string[] allowed) ? part.Value.Where(v => Array.IndexOf(allowed, v) >= 0).ToArray() : part.Value;
                if (variants.Length > 1) result[part.Key] = variants;
            }
            return result;
        }

        /// <summary>
        /// Server, 1.0.4: keeps an NPC's looks inside its type's limits. Player Model Lib picks every part at random from
        /// the whole race model when the NPC first spawns; a part outside the limits (or left over from an older model)
        /// gets one of the allowed variants. Brows take the hair colour and the face the body's skin when they may, as
        /// a person's would. True when something changed.
        /// </summary>
        public static bool KeepInLimits(Entity npc, Dictionary<string, string[]> limits, Random rand)
        {
            bool changed = false;
            foreach (KeyValuePair<string, string[]> limit in limits)
            {
                string current = CurrentPart(npc, limit.Key);
                string match = limit.Key == "brows" ? CurrentPart(npc, "haircolor") : limit.Key == "face" ? CurrentPart(npc, "baseskin") : null;
                // Brows and face follow their partner only when they were picked here, so an owner's own pick in the
                // looks menu stays.
                if (current != null && Array.IndexOf(limit.Value, current) >= 0) continue;
                string pick = match != null && Array.IndexOf(limit.Value, match) >= 0 ? match : limit.Value[rand.Next(limit.Value.Length)];
                SetPart(npc, limit.Key, pick);
                changed = true;
            }
            return changed;
        }

        static readonly Dictionary<string, string[]> Empty = new Dictionary<string, string[]>();
        static readonly Dictionary<string, Dictionary<string, string[]>> partsCache = new Dictionary<string, Dictionary<string, string[]>>();

        /// <summary>The face parts in one customplayermodels file (keyed by model name).</summary>
        public static Dictionary<string, string[]> ParseParts(string json, string modelKey) => ParseParts(ModelConfigs.ModelIn(json, modelKey));

        /// <summary>The face parts of one model definition.</summary>
        public static Dictionary<string, string[]> ParseParts(JObject model)
        {
            JArray list = (model?["SkinnableParts"] ?? model?["skinnableParts"]) as JArray;
            Dictionary<string, string[]> parts = new Dictionary<string, string[]>();
            if (list == null) return parts;
            foreach (string part in FaceParts)
            {
                JToken entry = list.FirstOrDefault(p => (string)p["code"] == part);
                if (entry == null) continue;
                string[] variants = (entry["variants"] as JArray ?? new JArray())
                    .Where(v => v["code"] != null && ((string)v["category"] ?? "standard") == "standard")
                    .Select(v => (string)v["code"]).Distinct().ToArray();
                if (variants.Length > 1) parts[part] = variants;
            }
            return parts;
        }

        public static string CurrentPart(Entity npc, string part) =>
            npc.WatchedAttributes.GetTreeAttribute("skinConfig")?.GetTreeAttribute("appliedParts")?.GetString(part);

        static void SetPart(Entity npc, string part, string variant)
        {
            ITreeAttribute skin = npc.WatchedAttributes.GetTreeAttribute("skinConfig");
            if (skin == null) npc.WatchedAttributes["skinConfig"] = skin = new TreeAttribute();
            ITreeAttribute applied = skin.GetTreeAttribute("appliedParts");
            if (applied == null) skin["appliedParts"] = applied = new TreeAttribute();
            applied.SetString(part, variant);
        }

        // ---- Weapons ----

        /// <summary>What the NPC may carry: its type's weapons, then none. Empty when its type carries none.</summary>
        public static string[] WeaponChoices(Entity npc)
        {
            EntityBehaviorHeldWeapon held = Behavior<EntityBehaviorHeldWeapon>(npc);
            IReadOnlyList<NpcWeapon> weapons = held?.Weapons;
            // An archer's bow isn't the owner's to change.
            if (weapons == null || weapons.Count == 0 || held.Fixed) return Array.Empty<string>();
            return weapons.Select(w => w.Item).Append(NoWeapon).ToArray();
        }

        /// <summary>What the shield may go with, for Apply: null when the NPC has no shield.</summary>
        public sealed class ShieldRule
        {
            /// <summary>Weapons held in both hands (the poleaxe).</summary>
            public string[] TwoHanded = Array.Empty<string>();
        }

        /// <summary>The NPC's shield rule, or null when its type carries no shield.</summary>
        public static ShieldRule ShieldOf(Entity npc)
        {
            EntityBehaviorHeldWeapon held = Behavior<EntityBehaviorHeldWeapon>(npc);
            if (held?.ShieldItem == null || held.AlwaysShield) return null;
            return new ShieldRule { TwoHanded = held.Weapons.Where(w => w.TwoHanded).Select(w => w.Item).ToArray() };
        }

        // ---- The owner's request ----

        /// <summary>
        /// Server: checks a request from the player whose UID is given, then makes every change in it, or none. Returns
        /// null when done, else the lang key (lotrnpcs:) of the reason: not their recruit, a bad name, a part or
        /// variant the race doesn't have, a weapon the NPC can't carry, or a helmet it doesn't have.
        /// </summary>
        public static string Apply(Entity npc, string playerUid, LooksRequest req, Dictionary<string, string[]> parts, string[] weapons, ItemSlot head,
            ShieldRule shield = null, string[] helmets = null)
        {
            if (npc == null || req == null || playerUid == null || Recruit.OwnerUidOf(npc) != playerUid) return "looks-notyours";
            string name = null;
            if (req.Name != null && (name = CleanName(req.Name)) == null) return "looks-badname";
            if (req.Parts != null)
            {
                foreach (KeyValuePair<string, string> change in req.Parts)
                {
                    if (change.Key == null || !parts.TryGetValue(change.Key, out string[] variants) || Array.IndexOf(variants, change.Value) < 0) return "looks-badpart";
                }
            }
            string weapon = req.Weapon?.Trim().ToLowerInvariant();
            if (weapon != null && Array.IndexOf(weapons, weapon) < 0) return "looks-badweapon";
            bool? helmetOn = req.Helmet == "on" ? true : req.Helmet == "off" ? false : (bool?)null;
            // A chosen helmet: one of the NPC's choices, or none, which takes it off as "off" does.
            string helmetCode = null;
            bool helmetNone = req.Helmet == NoHelmet;
            if (req.Helmet != null && helmetOn == null && !helmetNone)
            {
                helmetCode = FullCode(req.Helmet.Trim());
                if (head == null || helmets == null || Array.IndexOf(helmets, helmetCode) < 0) return "looks-badhelmet";
            }
            if (helmetNone && head == null) return "looks-nohelmet";
            if (helmetOn != null && (head == null || (head.Empty && !npc.WatchedAttributes.HasAttribute(KeptHelmetAttr)))) return "looks-nohelmet";
            bool? shieldOn = req.Shield == "on" ? true : req.Shield == "off" ? false : (bool?)null;
            if (req.Shield != null && (shieldOn == null || shield == null)) return "looks-badshield";
            // Never the shield with the poleaxe: asking for both is refused; choosing the poleaxe alone puts the
            // shield away.
            string weaponAfter = weapon ?? Attrs.String(npc.WatchedAttributes, EntityBehaviorHeldWeapon.WeaponAttr);
            bool twoHanded = shield != null && Array.IndexOf(shield.TwoHanded, weaponAfter) >= 0;
            if (shieldOn == true && twoHanded) return "looks-shieldtwohanded";
            if (twoHanded && shieldOn == null && Attrs.String(npc.WatchedAttributes, EntityBehaviorHeldWeapon.ShieldAttr) == "on") shieldOn = false;

            if (name != null && name != NameOf(npc)) SetName(npc, name);
            if (req.Parts != null && req.Parts.Count > 0)
            {
                foreach (KeyValuePair<string, string> change in req.Parts) SetPart(npc, change.Key, change.Value);
                npc.WatchedAttributes.MarkPathDirty("skinConfig");
            }
            if (weapon != null && weapon != Attrs.String(npc.WatchedAttributes, EntityBehaviorHeldWeapon.WeaponAttr))
            {
                EntityBehaviorHeldWeapon held = Behavior<EntityBehaviorHeldWeapon>(npc);
                held?.Lower();
                npc.WatchedAttributes.SetString(EntityBehaviorHeldWeapon.WeaponAttr, weapon);
                npc.WatchedAttributes.MarkPathDirty(EntityBehaviorHeldWeapon.WeaponAttr);
                held?.Raise();
            }
            if (helmetCode != null) { if (WearHelmet(head, npc.WatchedAttributes, helmetCode, npc.World)) StoreGear(npc); }
            else if (helmetNone) { if (MoveHelmet(head, npc.WatchedAttributes, false, npc.World)) StoreGear(npc); }
            else if (helmetOn != null && MoveHelmet(head, npc.WatchedAttributes, helmetOn.Value, npc.World)) StoreGear(npc);
            if (shieldOn != null)
            {
                npc.WatchedAttributes.SetString(EntityBehaviorHeldWeapon.ShieldAttr, shieldOn.Value ? "on" : "off");
                npc.WatchedAttributes.MarkPathDirty(EntityBehaviorHeldWeapon.ShieldAttr);
            }
            return null;
        }
    }
}
