using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// The owner's appearance dialog (0.0.54): name, helmet (which one since 0.0.58), weapon and face. Every change is sent to the server at once,
    /// which checks it and applies it to the NPC, so the owner watches it change in the world. Cancel puts back what
    /// it had when the dialog opened; Done (or closing) keeps it.
    /// </summary>
    public class GuiDialogLooks : GuiDialog
    {
        readonly EntityAgent npc;
        readonly Action<bool> onFinished;
        readonly Dictionary<string, string[]> parts;
        readonly string[] partOrder;
        readonly string[] weapons;
        // Shown values: index into each part's variants, the weapon, and the helmet.
        readonly Dictionary<string, int> chosen = new Dictionary<string, int>();
        int weaponIndex;
        // The helmets to choose from, then "none" (0.0.58); empty when the NPC has no helmet to choose.
        readonly string[] helmets;
        int helmetIndex;
        // The Mordor orcs' shield (0.0.55): null when the NPC has none.
        readonly Looks.ShieldRule shield;
        bool shieldOn;
        readonly LooksRequest original;
        bool finished;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogLooks(ICoreClientAPI capi, EntityAgent npc, Action<bool> onFinished) : base(capi)
        {
            this.npc = npc;
            this.onFinished = onFinished;
            parts = Looks.PartsOf(capi, npc.WatchedAttributes.GetString("skinModel"));
            partOrder = Looks.FaceParts.Where(parts.ContainsKey).ToArray();
            weapons = Looks.WeaponChoices(npc);
            original = new LooksRequest { Parts = new Dictionary<string, string>() };
            foreach (string part in partOrder)
            {
                string current = Looks.CurrentPart(npc, part);
                int index = Array.IndexOf(parts[part], current);
                chosen[part] = Math.Max(0, index);
                if (current != null) original.Parts[part] = current;
            }
            string weapon = npc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.WeaponAttr);
            weaponIndex = Math.Max(0, Array.IndexOf(weapons, weapon ?? Looks.NoWeapon));
            if (weapons.Length > 0 && weapon != null) original.Weapon = weapon;
            ItemSlot head = Looks.Head(npc);
            string[] choices = Looks.HelmetChoices(npc);
            helmets = choices.Length > 0 ? choices.Append(Looks.NoHelmet).ToArray() : Array.Empty<string>();
            string worn = head != null && !head.Empty ? Looks.CodeOf(head.Itemstack) : Looks.NoHelmet;
            helmetIndex = Math.Max(0, Array.IndexOf(helmets, worn));
            if (helmets.Length > 0) original.Helmet = worn;
            shield = Looks.ShieldOf(npc);
            shieldOn = shield != null && npc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.ShieldAttr) == "on";
            if (shield != null) original.Shield = shieldOn ? "on" : "off";
            Compose();
        }

        // 600 wide since 0.0.59 (user, 2026-11-03: the captain's "servant of the eye option, but hte text is cropped out"): a
        // value now has 400 px, enough for "Servant of the Eye Helmet (Red Mask) (1 of 2)" on one line.
        const int Width = 600, RowHeight = 34, LabelWidth = 120, ArrowWidth = 30;

        void Compose()
        {
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle).WithFixedAlignmentOffset(GuiStyle.DialogToScreenPadding, 0);
            CairoFont label = CairoFont.WhiteSmallText();
            string title = Lang.Get("lotrnpcs:looks-title", Recruit.Title(npc));

            GuiComposer c = capi.Gui.CreateCompo("lotrnpcs-looks-" + npc.EntityId, dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(title, () => TryClose())
                .BeginChildElements(bgBounds);
            int y = 35;
            c.AddStaticText(Lang.Get("lotrnpcs:looks-name"), label, ElementBounds.Fixed(0, y + 6, LabelWidth, 25))
             .AddTextInput(ElementBounds.Fixed(LabelWidth, y, Width - LabelWidth - 100, 28), null, CairoFont.TextInput(), "name")
             .AddSmallButton(Lang.Get("lotrnpcs:looks-rename"), OnRename, ElementBounds.Fixed(Width - 90, y + 1, 90, 26), EnumButtonStyle.Normal, "rename");
            y += RowHeight + 6;
            if (helmets.Length > 0) { Row(c, "helmet", Lang.Get("lotrnpcs:looks-helmet"), ref y, () => StepHelmet(-1), () => StepHelmet(1)); }
            if (weapons.Length > 0) { Row(c, "weapon", Lang.Get("lotrnpcs:looks-weapon"), ref y, () => StepWeapon(-1), () => StepWeapon(1)); }
            if (shield != null) { Row(c, "shield", Lang.Get("lotrnpcs:looks-shield"), ref y, () => ToggleShield()); }
            foreach (string part in partOrder)
            {
                string p = part;
                Row(c, "part-" + p, PartLabel(p), ref y, () => StepPart(p, -1), () => StepPart(p, 1));
            }
            c.AddDynamicText("", CairoFont.WhiteSmallText().WithColor(GuiStyle.ErrorTextColor), ElementBounds.Fixed(0, y, Width, 40), "error");
            y += 44;
            c.AddSmallButton(Lang.Get("lotrnpcs:looks-cancel"), OnCancel, ElementBounds.Fixed(0, y, 0, 0).WithFixedPadding(10, 5))
             .AddSmallButton(Lang.Get("lotrnpcs:looks-done"), OnDone, ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, y, 0, 0).WithFixedPadding(10, 5))
             .EndChildElements();
            SingleComposer = c.Compose();
            SingleComposer.GetTextInput("name").SetValue(Looks.NameOf(npc) ?? "");
            Refresh();
        }

        // A row: its label, the current value, and buttons to step back and forth (one button for a toggle).
        void Row(GuiComposer c, string key, string text, ref int y, Action back, Action forward = null)
        {
            c.AddStaticText(text, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y + 6, LabelWidth, 25));
            if (forward == null)
            {
                c.AddSmallButton(Lang.Get("lotrnpcs:looks-toggle"), () => { back(); return true; }, ElementBounds.Fixed(LabelWidth, y, ArrowWidth * 2 + 10, 26));
                c.AddDynamicText("", CairoFont.WhiteSmallText(), ElementBounds.Fixed(LabelWidth + ArrowWidth * 2 + 20, y + 6, Width - LabelWidth - ArrowWidth * 2 - 20, 25), key);
            }
            else
            {
                c.AddSmallButton("<", () => { back(); return true; }, ElementBounds.Fixed(LabelWidth, y, ArrowWidth, 26));
                c.AddDynamicText("", CairoFont.WhiteSmallText(), ElementBounds.Fixed(LabelWidth + ArrowWidth + 10, y + 6, Width - LabelWidth - ArrowWidth * 2 - 20, 25), key);
                c.AddSmallButton(">", () => { forward(); return true; }, ElementBounds.Fixed(Width - ArrowWidth, y, ArrowWidth, 26));
            }
            y += RowHeight;
        }

        void Refresh()
        {
            if (SingleComposer == null) return;
            if (helmets.Length > 0) SingleComposer.GetDynamicText("helmet")?.SetNewText(Lang.Get("lotrnpcs:looks-variant", HelmetName(helmets[helmetIndex]), helmetIndex + 1, helmets.Length));
            if (weapons.Length > 0) SingleComposer.GetDynamicText("weapon")?.SetNewText(WeaponName(weapons[weaponIndex]));
            if (shield != null) SingleComposer.GetDynamicText("shield")?.SetNewText(Lang.Get(shieldOn ? "lotrnpcs:looks-on" : "lotrnpcs:looks-off"));
            foreach (string part in partOrder)
            {
                string[] variants = parts[part];
                int i = chosen[part];
                SingleComposer.GetDynamicText("part-" + part)?.SetNewText(Lang.Get("lotrnpcs:looks-variant", VariantName(part, variants[i]), i + 1, variants.Length));
            }
        }

        void StepPart(string part, int step)
        {
            string[] variants = parts[part];
            chosen[part] = (chosen[part] + step + variants.Length) % variants.Length;
            Send(new LooksRequest { Parts = new Dictionary<string, string> { [part] = variants[chosen[part]] } });
        }

        void StepWeapon(int step)
        {
            weaponIndex = (weaponIndex + step + weapons.Length) % weapons.Length;
            // The server puts the shield away when the poleaxe comes out; show that here too.
            if (TwoHanded(weapons[weaponIndex])) shieldOn = false;
            Send(new LooksRequest { Weapon = weapons[weaponIndex] });
        }

        void StepHelmet(int step)
        {
            helmetIndex = (helmetIndex + step + helmets.Length) % helmets.Length;
            Send(new LooksRequest { Helmet = helmets[helmetIndex] });
        }

        bool TwoHanded(string weapon) => shield != null && Array.IndexOf(shield.TwoHanded, weapon) >= 0;

        void ToggleShield()
        {
            string weapon = weapons.Length > 0 ? weapons[weaponIndex] : npc.WatchedAttributes.GetString(EntityBehaviorHeldWeapon.WeaponAttr);
            if (!shieldOn && TwoHanded(weapon)) { Refused("looks-shieldtwohanded"); return; }
            shieldOn = !shieldOn;
            Send(new LooksRequest { Shield = shieldOn ? "on" : "off" });
        }

        bool OnRename()
        {
            string name = Looks.CleanName(SingleComposer.GetTextInput("name").GetText());
            if (name == null) { Refused("looks-badname"); return true; }
            SingleComposer.GetTextInput("name").SetValue(name);
            Send(new LooksRequest { Name = name });
            return true;
        }

        void Send(LooksRequest req)
        {
            SingleComposer.GetDynamicText("error")?.SetNewText("");
            Refresh();
            capi.Network.SendEntityPacket(npc.EntityId, EntityBehaviorHireable.LooksPacket, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(req)));
        }

        /// <summary>The server refused a change: say why.</summary>
        public void Refused(string key) => SingleComposer?.GetDynamicText("error")?.SetNewText(Lang.Get("lotrnpcs:" + key));

        bool OnCancel()
        {
            capi.Network.SendEntityPacket(npc.EntityId, EntityBehaviorHireable.LooksPacket, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(original)));
            Finish(false);
            return true;
        }

        bool OnDone()
        {
            // A name typed but not yet confirmed counts too.
            string typed = Looks.CleanName(SingleComposer.GetTextInput("name").GetText());
            if (typed != null && typed != Looks.NameOf(npc)) Send(new LooksRequest { Name = typed });
            Finish(true);
            return true;
        }

        void Finish(bool done)
        {
            if (finished) return;
            finished = true;
            TryClose();
            onFinished?.Invoke(done);
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            if (!finished) { finished = true; onFinished?.Invoke(true); }
        }

        // Closes when the NPC dies or goes, or the player walks away, as the talk does.
        public override void OnFinalizeFrame(float dt)
        {
            base.OnFinalizeFrame(dt);
            EntityPlayer player = capi.World.Player?.Entity;
            double range = EntityBehaviorHireable.TalkRange + 1;
            if (finished || !IsOpened() || player == null) return;
            if (!npc.Alive || npc.ShouldDespawn || player.Pos.SquareDistanceTo(npc.Pos) > range * range)
                capi.Event.EnqueueMainThreadTask(() => TryClose(), "lotrnpcs-closelooks");
        }

        static string PartLabel(string part) => Lang.HasTranslation("lotrnpcs:looks-part-" + part) ? Lang.Get("lotrnpcs:looks-part-" + part) : Pretty(part);

        // Vanilla's own name for a variant when the game has one, else the code made readable.
        static string VariantName(string part, string code)
        {
            string key = "game:skinpart-" + part + "-" + code;
            return Lang.HasTranslation(key) ? Lang.Get(key) : Pretty(code);
        }

        string HelmetName(string code)
        {
            if (code == Looks.NoHelmet) return Lang.Get("lotrnpcs:looks-helmet-none");
            Item item = capi.World.GetItem(new AssetLocation(code));
            return item == null ? code : new ItemStack(item).GetName();
        }

        string WeaponName(string code)
        {
            if (code == Looks.NoWeapon) return Lang.Get("lotrnpcs:looks-weapon-none");
            Item item = capi.World.GetItem(new AssetLocation(code));
            return item == null ? code : new ItemStack(item).GetName();
        }

        public static string Pretty(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            string text = code.Replace('-', ' ').Replace('_', ' ');
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }
}
