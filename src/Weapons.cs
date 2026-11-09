using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace LotrNpcs
{
    /// <summary>One weapon an NPC may carry, from the lotrnpcs:heldweapon behavior's "weapons" list.</summary>
    public sealed class NpcWeapon
    {
        /// <summary>The weapon item, as a lowercase "domain:code".</summary>
        public string Item;
        /// <summary>The entity animation swung instead of the unarmed punch.</summary>
        public string AttackAnimation;
        /// <summary>The entity animation held between attacks, or null to let the arm hang as usual.</summary>
        public string HoldAnimation;
        /// <summary>Held in both hands (the poleaxe): never carried with a shield (0.0.55).</summary>
        public bool TwoHanded;
        /// <summary>How likely it is picked against the others in the list (0.0.82, the owner's "prefer spears" and the
        /// like): an entry with weight 3 comes up three times as often as one with weight 1. At least 1.</summary>
        public int Weight = 1;
        /// <summary>When the blow lands, in ms after the swing starts (0.0.99, each weapon's own wind-up); 0 keeps the
        /// melee task's damagePlayerAtMs.</summary>
        public int HitMs;
        /// <summary>The weapon in other poses, first match wins (0.0.59 the drawn bow; 0.0.73 the string pulled back by
        /// stages). Empty when the weapon has one look.</summary>
        public List<WeaponPose> Poses = new List<WeaponPose>();
    }

    /// <summary>Another item shown instead of the weapon while one of its animations runs, within a window.</summary>
    public sealed class WeaponPose
    {
        public string Item;
        /// <summary>Entity animation code -> (from, until) in milliseconds after it starts; until 0 = while it runs.</summary>
        public Dictionary<string, (int From, int Until)> While = new Dictionary<string, (int From, int Until)>();
    }

    /// <summary>
    /// Gives an NPC a weapon to carry (0.0.45, user: Mordor orcs hold weapons that are "randomized, and do not affect
    /// damage tier"). The server picks one at random the first time the NPC loads and keeps it in a watched attribute for
    /// life; an NPC whose weapon is no longer listed picks again. The weapon is only for looks: just the client puts it in
    /// the NPC's hand, so nothing on the server, vanilla or Combat Overhaul, ever sees a held item, and damage, tier and
    /// timing stay the rank's own. The melee task asks this behavior which swing to play, and the hold animation (the
    /// poleaxe's two-handed guard) runs between attacks.
    /// </summary>
    public class EntityBehaviorHeldWeapon : EntityBehavior
    {
        public const string WeaponAttr = "lotrnpcsWeapon";
        /// <summary>"on" or "off": whether the NPC carries its shield (0.0.55). Missing until the server decides.</summary>
        public const string ShieldAttr = "lotrnpcsShield";

        /// <summary>Which of its type's shields the NPC carries (0.0.76, dwarves have two), as "domain:code".</summary>
        public const string ShieldItemAttr = "lotrnpcsShieldItem";

        /// <summary>The shields its type may carry in the left hand (0.0.55 Mordor orcs; 0.0.76 a list, "shields").</summary>
        public IReadOnlyList<string> ShieldItems { get; private set; } = Array.Empty<string>();

        /// <summary>The shield item this NPC carries in the left hand when its shield is on, or null: its type's only
        /// shield, or the one it picked from several.</summary>
        public string ShieldItem => PickShield(Attrs.String(entity.WatchedAttributes, ShieldItemAttr), ShieldItems);

        /// <summary>The picked shield while it is still listed, else the first listed one, or null.</summary>
        public static string PickShield(string picked, IReadOnlyList<string> shields) =>
            shields == null || shields.Count == 0 ? null : picked != null && shields.Contains(picked) ? picked : shields[0];
        /// <summary>True when the owner can't change the weapon (archers keep their bow).</summary>
        public bool Fixed { get; private set; }
        // Client: the shield now in the left hand.
        string shownShield;
        ItemSlot shownShieldSlot;

        NpcWeapon[] weapons = Array.Empty<NpcWeapon>();
        // Client: the item now in the hand, so a resync of the same code doesn't rebuild the slot.
        string shownItem;
        ItemSlot shownSlot;
        // Server: the resolved swing for the current weapon, looked up again only when the weapon changes.
        string resolvedFor;
        AnimationMetaData attackAnimation;

        public EntityBehaviorHeldWeapon(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "lotrnpcs:heldweapon";

        public IReadOnlyList<NpcWeapon> Weapons => weapons;

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            Configure(attributes);
            if (entity.World.Side == EnumAppSide.Server)
            {
                Choose(entity.World.Rand);
            }
            else
            {
                entity.WatchedAttributes.RegisterModifiedListener(WeaponAttr, ShowWeapon);
                entity.WatchedAttributes.RegisterModifiedListener(ShieldAttr, ShowShield);
                entity.WatchedAttributes.RegisterModifiedListener(ShieldItemAttr, ShowShield);
                ShowWeapon();
            }
        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            if (entity.World.Side == EnumAppSide.Server)
            {
                FactionConfig config = entity.Api?.ModLoader.GetModSystem<FactionSystem>()?.Config;
                ChooseShield(onFirstSpawn, config?.ShieldChance ?? 0.5, entity.World.Rand);
                Raise();
            }
            else ShowWeapon();
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            // The guard pose would hold the arms up through the death animation.
            Lower();
        }

        /// <summary>Reads the behavior's "weapons" list.</summary>
        public void Configure(JsonObject attributes)
        {
            weapons = ReadWeapons(attributes?["weapons"]);
            // "shields": [..] (0.0.76), or the older single "shield".
            List<string> shields = new List<string>();
            foreach (JsonObject one in attributes?["shields"].AsArray() ?? Array.Empty<JsonObject>()) shields.Add(Clean(one.AsString()));
            if (shields.Count == 0) shields.Add(Clean(attributes?["shield"].AsString()));
            ShieldItems = shields.Where(c => c != null).Select(c => c.Contains(':') ? c : "lotrnpcs:" + c).Distinct().ToArray();
            Fixed = attributes?["fixed"].AsBool(false) ?? false;
            resolvedFor = null;
            attackAnimation = null;
        }

        /// <summary>The "weapons" list, with codes lowercased and entries without an item or attack dropped.</summary>
        public static NpcWeapon[] ReadWeapons(JsonObject list)
        {
            JsonObject[] entries = list?.AsArray() ?? Array.Empty<JsonObject>();
            List<NpcWeapon> read = new List<NpcWeapon>(entries.Length);
            foreach (JsonObject entry in entries)
            {
                string item = Clean(entry?["item"].AsString());
                string attack = Clean(entry?["attackAnimation"].AsString());
                if (item == null || attack == null || read.Any(w => w.Item == item)) continue;
                if (!item.Contains(':')) item = "lotrnpcs:" + item;
                NpcWeapon weapon = new NpcWeapon { Item = item, AttackAnimation = attack, HoldAnimation = Clean(entry["holdAnimation"].AsString()), TwoHanded = entry["twoHanded"].AsBool(false), Weight = Math.Max(1, entry["weight"].AsInt(1)), HitMs = Math.Max(0, entry["hitMs"].AsInt(0)) };
                // "poses": [{ "item": ..., "while": { anim: [from, until] } }] (0.0.73), in order; the 0.0.59 form
                // "drawnItem" + "drawnWhile": { anim: until } still reads, as one pose after them.
                foreach (JsonObject pose in entry["poses"].AsArray() ?? Array.Empty<JsonObject>())
                {
                    if (ReadPose(pose["item"].AsString(), pose["while"].Token, true) is WeaponPose read1) weapon.Poses.Add(read1);
                }
                if (ReadPose(entry["drawnItem"].AsString(), entry["drawnWhile"].Token, false) is WeaponPose old) weapon.Poses.Add(old);
                read.Add(weapon);
            }
            return read.ToArray();
        }

        static WeaponPose ReadPose(string item, Newtonsoft.Json.Linq.JToken windows, bool ranges)
        {
            item = Clean(item);
            if (item == null || !(windows is Newtonsoft.Json.Linq.JObject anims)) return null;
            WeaponPose pose = new WeaponPose { Item = item.Contains(':') ? item : "lotrnpcs:" + item };
            foreach (Newtonsoft.Json.Linq.JProperty anim in anims.Properties())
            {
                string code = Clean(anim.Name);
                if (code == null) continue;
                if (!ranges && anim.Value.Type == Newtonsoft.Json.Linq.JTokenType.Integer) pose.While[code] = (0, Math.Max(0, (int)anim.Value));
                else if (ranges && anim.Value is Newtonsoft.Json.Linq.JArray pair && pair.Count == 2 && pair.All(v => v.Type == Newtonsoft.Json.Linq.JTokenType.Integer))
                    pose.While[code] = (Math.Max(0, (int)pair[0]), Math.Max(0, (int)pair[1]));
            }
            return pose.While.Count > 0 ? pose : null;
        }

        static string Clean(string code)
        {
            code = code?.Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(code) ? null : code;
        }

        /// <summary>
        /// The weapon an NPC should carry: its current one while that is still listed, otherwise a random listed one, or
        /// null when the list is empty.
        /// </summary>
        public static string PickWeapon(string current, NpcWeapon[] weapons, Random rand, bool fixedWeapon = false)
        {
            if (weapons.Length == 0) return null;
            // An owner who took the weapon away (Looks.cs, 0.0.54) keeps it that way, unless it can't be taken away.
            if (current == Looks.NoWeapon && !fixedWeapon) return current;
            if (current != null && Array.Exists(weapons, w => w.Item == current)) return current;
            // By weight (0.0.82).
            int total = 0;
            foreach (NpcWeapon w in weapons) total += Math.Max(1, w.Weight);
            int roll = rand.Next(total);
            foreach (NpcWeapon w in weapons)
            {
                roll -= Math.Max(1, w.Weight);
                if (roll < 0) return w.Item;
            }
            return weapons[weapons.Length - 1].Item;
        }

        /// <summary>Server: keeps or picks this NPC's weapon and stores it for the client.</summary>
        public void Choose(Random rand)
        {
            string current = Attrs.String(entity.WatchedAttributes, WeaponAttr);
            string pick = PickWeapon(current, weapons, rand, Fixed);
            if (pick == current) return;
            if (pick == null) entity.WatchedAttributes.RemoveAttribute(WeaponAttr);
            else entity.WatchedAttributes.SetString(WeaponAttr, pick);
            entity.WatchedAttributes.MarkPathDirty(WeaponAttr);
        }

        /// <summary>True when this weapon is held in both hands, so no shield goes with it.</summary>
        public static bool IsTwoHanded(string item, IReadOnlyList<NpcWeapon> weapons) =>
            item != null && weapons != null && weapons.Any(w => w.Item == item && w.TwoHanded);

        /// <summary>
        /// Whether a new NPC carries its shield: by chance, and never with a two-handed weapon (user, 2026-11-03: "it
        /// cant be used in conjunction with the poleaxe"). NPCs that were already in the world before 0.0.55 get none
        /// until their owner gives them one.
        /// </summary>
        public static bool RollShield(bool firstSpawn, string weapon, IReadOnlyList<NpcWeapon> weapons, double chance, Random rand) =>
            firstSpawn && !IsTwoHanded(weapon, weapons) && rand.NextDouble() < Math.Clamp(chance, 0, 1);

        /// <summary>Server: decides the shield once, and takes it away if the NPC holds a two-handed weapon.</summary>
        public void ChooseShield(bool firstSpawn, double chance, Random rand)
        {
            if (ShieldItems.Count == 0) return;
            // Which shield, picked once from several (0.0.76).
            string picked = Attrs.String(entity.WatchedAttributes, ShieldItemAttr);
            if (ShieldItems.Count > 1 && (picked == null || !ShieldItems.Contains(picked)))
            {
                entity.WatchedAttributes.SetString(ShieldItemAttr, ShieldItems[rand.Next(ShieldItems.Count)]);
                entity.WatchedAttributes.MarkPathDirty(ShieldItemAttr);
            }
            string weapon = Attrs.String(entity.WatchedAttributes, WeaponAttr);
            string now = Attrs.String(entity.WatchedAttributes, ShieldAttr);
            string want = now == null ? (RollShield(firstSpawn, weapon, weapons, chance, rand) ? "on" : "off") : now;
            if (want == "on" && IsTwoHanded(weapon, weapons)) want = "off";
            if (want == now) return;
            entity.WatchedAttributes.SetString(ShieldAttr, want);
            entity.WatchedAttributes.MarkPathDirty(ShieldAttr);
        }

        /// <summary>True when the NPC shows its shield.</summary>
        public bool ShieldOn => ShieldItem != null && Attrs.String(entity.WatchedAttributes, ShieldAttr) == "on"
            && !IsTwoHanded(Attrs.String(entity.WatchedAttributes, WeaponAttr), weapons);

        /// <summary>The weapon this NPC carries, or null.</summary>
        public NpcWeapon Current
        {
            get
            {
                string code = Attrs.String(entity.WatchedAttributes, WeaponAttr);
                return code == null ? null : Array.Find(weapons, w => w.Item == code);
            }
        }

        /// <summary>
        /// Server: the swing for the current weapon, from the entity's animation list (so its speed and blending apply),
        /// or null when the NPC carries nothing and keeps its unarmed punch.
        /// </summary>
        public AnimationMetaData AttackAnimation
        {
            get
            {
                NpcWeapon weapon = Current;
                if (weapon?.Item != resolvedFor)
                {
                    resolvedFor = weapon?.Item;
                    attackAnimation = weapon == null ? null : entity.Properties.Client?.Animations?.FirstOrDefault(a => a.Code == weapon.AttackAnimation);
                    if (weapon != null && attackAnimation == null)
                    {
                        entity.World?.Logger.Warning("[lotrnpcs] {0}: attack animation {1} for {2} is not in the entity's animations, using the unarmed one.",
                            entity.Code, weapon.AttackAnimation, weapon.Item);
                    }
                }
                return attackAnimation;
            }
        }

        /// <summary>Server: when the current weapon's blow lands, in ms after its swing starts, or 0 for the task's own.</summary>
        public int HitMs => Current?.HitMs ?? 0;

        /// <summary>Server: drops the guard pose before a swing.</summary>
        public void Lower()
        {
            if (Current?.HoldAnimation is string hold) entity.AnimManager?.StopAnimation(hold);
        }

        /// <summary>Server: takes up the guard pose again (after a swing, and on load).</summary>
        public void Raise()
        {
            if (!entity.Alive || !(Current?.HoldAnimation is string hold)) return;
            AnimationMetaData meta = entity.Properties.Client?.Animations?.FirstOrDefault(a => a.Code == hold);
            if (meta != null) entity.AnimManager?.StartAnimation(meta);
        }

        // Client: the pose item shown now (null: the weapon itself), and when each pose animation started.
        string shownPose;
        readonly Dictionary<string, long> poseSince = new Dictionary<string, long>();

        /// <summary>
        /// The pose item to show (0.0.73): the first of the weapon's poses with an animation that runs and started between
        /// its from and until milliseconds ago (until 0: any time after from). Null: the weapon itself. started holds when
        /// each running pose animation started; animations that stopped are dropped.
        /// </summary>
        public static string PoseNow(NpcWeapon weapon, System.Func<string, bool> active, long now, Dictionary<string, long> started)
        {
            if (weapon == null || weapon.Poses.Count == 0) { started.Clear(); return null; }
            HashSet<string> running = new HashSet<string>();
            foreach (WeaponPose pose in weapon.Poses)
            {
                foreach (string anim in pose.While.Keys)
                {
                    if (running.Contains(anim) || !active(anim)) continue;
                    running.Add(anim);
                    if (!started.ContainsKey(anim)) started[anim] = now;
                }
            }
            foreach (string anim in started.Keys.Where(a => !running.Contains(a)).ToList()) started.Remove(anim);
            foreach (WeaponPose pose in weapon.Poses)
            {
                foreach (KeyValuePair<string, (int From, int Until)> w in pose.While)
                {
                    if (!started.TryGetValue(w.Key, out long since)) continue;
                    long age = now - since;
                    if (age >= w.Value.From && (w.Value.Until == 0 || age < w.Value.Until)) return pose.Item;
                }
            }
            return null;
        }

        /// <summary>
        /// The name the game tracks a running animation by: the shape animation (AnimationMetaData.Animation, such as
        /// "bowaim"), not the entity's animation code. 0.0.59 asked for the code, which was never active, so the drawn bow
        /// never showed (user, 2026-11-03: "the bow is held wrong"). Falls back to the code when the list lacks it.
        /// </summary>
        public static string TrackedAnimation(string code, AnimationMetaData[] animations) =>
            animations?.FirstOrDefault(a => a?.Code == code)?.Animation ?? code;

        readonly Dictionary<string, string> trackedNames = new Dictionary<string, string>();

        public override void OnGameTick(float deltaTime)
        {
            if (entity.World.Side != EnumAppSide.Client) return;
            NpcWeapon weapon = Current;
            if ((weapon == null || weapon.Poses.Count == 0) && shownPose == null) return;
            IAnimationManager anims = entity.AnimManager;
            string pose = anims == null ? null : PoseNow(weapon, code =>
            {
                if (!trackedNames.TryGetValue(code, out string name)) trackedNames[code] = name = TrackedAnimation(code, entity.Properties.Client?.Animations);
                return anims.IsAnimationActive(name);
            }, entity.World.ElapsedMilliseconds, poseSince);
            if (pose == shownPose) return;
            shownPose = pose;
            ShowWeapon();
        }

        /// <summary>Client: puts the current weapon in the NPC's right hand, or takes it away.</summary>
        void ShowWeapon()
        {
            string code = Attrs.String(entity.WatchedAttributes, WeaponAttr);
            if (code == Looks.NoWeapon) code = null;
            if (code != null && shownPose != null) code = shownPose;
            if (code == shownItem || !(entity is EntityAgent agent)) return;
            shownItem = code;
            Item item = code == null ? null : entity.World.GetItem(new AssetLocation(code));
            if (code != null && item == null) entity.World.Logger.Warning("[lotrnpcs] {0}: weapon item {1} does not exist.", entity.Code, code);
            SetHand(agent, item);
            ShowShield();
        }

        /// <summary>Client: puts the shield on the NPC's left arm, or takes it away (also when it takes up the poleaxe).</summary>
        void ShowShield()
        {
            if (!(entity is EntityAgent agent)) return;
            string code = ShieldOn ? ShieldItem : null;
            if (code == shownShield) return;
            shownShield = code;
            Item item = code == null ? null : entity.World.GetItem(new AssetLocation(code));
            if (code != null && item == null) entity.World.Logger.Warning("[lotrnpcs] {0}: shield item {1} does not exist.", entity.Code, code);
            if (agent.LeftHandItemSlot != null && agent.LeftHandItemSlot != shownShieldSlot) return;
            shownShieldSlot = item == null ? null : new DummySlot(new ItemStack(item));
            agent.LeftHandItemSlot = shownShieldSlot;
        }

        /// <summary>
        /// Client: shows the item in the hand through a slot that belongs to no inventory, so it can't be dropped, taken
        /// or used. Only a slot this behavior put there is ever replaced.
        /// </summary>
        public void SetHand(EntityAgent agent, Item item)
        {
            if (agent.RightHandItemSlot != null && agent.RightHandItemSlot != shownSlot) return;
            shownSlot = item == null ? null : new DummySlot(new ItemStack(item));
            agent.RightHandItemSlot = shownSlot;
        }
    }
}
