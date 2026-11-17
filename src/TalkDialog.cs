using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>
    /// The talk menu, built like vanilla's trader dialogue: the NPC's lines and the player's numbered choices in
    /// a scrolling text box (number keys pick a choice). The server decides what is on offer (TalkState).
    /// </summary>
    public class GuiDialogTalk : GuiDialog
    {
        readonly EntityAgent npc;
        readonly string faction;
        TalkState state;
        GuiElementRichtext textElem;
        ElementBounds clipBounds;
        bool confirmDismiss, pickClass, closing;
        int lastQuestion;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogTalk(ICoreClientAPI capi, EntityAgent npc, TalkState state) : base(capi)
        {
            this.npc = npc;
            this.state = state;
            faction = FactionSystem.FactionOf(npc);
        }

        public void Begin()
        {
            Compose();
            TryOpen();
            string kind = state.Mode == "member" ? "greet" : state.Mode == "own" || state.Mode == "borrowed" ? "own" : state.Mode == "others" ? "others"
                : state.Mode == "shared" ? "shared" : "stranger";
            Npc(Line(kind));
            if (state.Mode == "member") Status();
            Options();
        }

        public void OnReply(TalkState next)
        {
            state = next;
            confirmDismiss = false;
            pickClass = false;
            if (state.Reply != null)
            {
                string key = Has("lotrnpcs:" + state.Reply + "-" + faction) ? "lotrnpcs:" + state.Reply + "-" + faction : "lotrnpcs:" + state.Reply;
                Npc(Format(key));
                if (state.Reply == "talk-hired" || state.Reply == "talk-hired-guard") Status();
            }
            Options();
        }

        void Compose()
        {
            ClearComposers();
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightMiddle).WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);
            const int w = 480, h = 340;
            ElementBounds textBounds = ElementBounds.Fixed(0, 30, w, h);
            clipBounds = textBounds.ForkBoundingParent();
            ElementBounds insetBounds = textBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-2, -2);
            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(3 + textBounds.fixedWidth + 7).WithFixedWidth(20);
            ElementBounds byeBounds = ElementBounds.Fixed(EnumDialogArea.LeftFixed, 0, 0, 0, 0).WithFixedPadding(8, 5);
            string title = Recruit.OwnerUidOf(npc) != null ? Recruit.Title(npc) : npc.GetBehavior<EntityBehaviorNameTag>()?.DisplayName ?? npc.GetName();

            SingleComposer = capi.Gui.CreateCompo("lotrnpcs-talk-" + npc.EntityId, dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(title, () => TryClose())
                .BeginChildElements(bgBounds)
                    .BeginClip(clipBounds)
                        .AddInset(insetBounds, 3)
                        .AddRichtext("", CairoFont.WhiteSmallText(), textBounds.WithFixedPadding(5).WithFixedSize(w - 10, h - 10), "talkText")
                    .EndClip()
                    .AddVerticalScrollbar(OnScroll, scrollbarBounds, "scrollbar")
                    .AddSmallButton(Lang.Get("lotrnpcs:talk-opt-bye"), () => TryClose(), byeBounds.FixedUnder(clipBounds, 20))
                .EndChildElements()
                .Compose();
            textElem = SingleComposer.GetRichtext("talkText");
        }

        void OnScroll(float value)
        {
            textElem.Bounds.fixedY = -value;
            textElem.Bounds.CalcWorldBounds();
        }

        static CairoFont Font() => CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2);

        void Emit(RichTextComponentBase[] comps)
        {
            // Only the newest choices can be clicked.
            foreach (RichTextComponentBase comp in textElem.Components)
            {
                if (comp is LinkTextComponent link) link.Clickable = false;
            }
            textElem.AppendText(comps);
            GuiElementScrollbar bar = SingleComposer.GetScrollbar("scrollbar");
            bar.Bounds.CalcWorldBounds();
            bar.SetHeights((float)clipBounds.fixedHeight, (float)textElem.Bounds.fixedHeight);
            bar.ScrollToBottom();
        }

        void Npc(string text) => Emit(VtmlUtil.Richtextify(capi, text + "\r\n", Font()));

        void Player(string text) =>
            Emit(new RichTextComponentBase[] { new RichTextComponent(capi, text + "\r\n\r\n", Font().WithColor(GuiStyle.ColorParchment).WithOrientation(EnumTextOrientation.Right)) });

        void Options()
        {
            List<RichTextComponentBase> comps = new List<RichTextComponentBase> { new RichTextComponent(capi, "\r\n", Font()) };
            int number = 1;
            void Add(string text, Action onClick)
            {
                comps.Add(new LinkTextComponent(capi, number++ + ". " + text, Font().WithColor(GuiStyle.ColorTime1).WithOrientation(EnumTextOrientation.Right), _ => onClick()));
                comps.Add(new RichTextComponent(capi, "\r\n", Font()));
            }
            void Choice(string key, Action onClick) { string text = Format(key); Add(text, () => { Player(text); onClick(); }); }

            if (pickClass && state.Mode == "own")
            {
                // 0.0.156: the class choices behind "Let me change your class" (owner, 2026-11-16).
                foreach (string cls in state.Classes ?? new List<string>())
                {
                    string text = Lang.Get("lotrnpcs:talk-opt-class", ClassTitle(cls));
                    string pick = cls;
                    Add(text, () => { Player(text); pickClass = false; Send("class:" + pick); });
                }
                Choice("lotrnpcs:talk-opt-class-no", () => { pickClass = false; Npc(Line("own")); Options(); });
            }
            else if (confirmDismiss)
            {
                Choice("lotrnpcs:talk-opt-dismiss-yes", () => Send("dismiss"));
                Choice("lotrnpcs:talk-opt-dismiss-no", () => { confirmDismiss = false; Npc(Line("stay")); Options(); });
            }
            else if (state.Mode == "member")
            {
                Choice(state.Price > 0 ? "lotrnpcs:talk-opt-hire" : "lotrnpcs:talk-opt-hire-free", () => Send("hire"));
                Add(Lang.Get("lotrnpcs:talk-opt-ask"), Ask);
            }
            else if (state.Mode == "own")
            {
                Choice("lotrnpcs:talk-opt-follow", () => Send(Recruit.Follow));
                Choice("lotrnpcs:talk-opt-hold", () => Send(Recruit.Hold));
                Choice("lotrnpcs:talk-opt-guard", () => Send(Recruit.Guard));
                Choice("lotrnpcs:talk-opt-guard-shared", () => Send(Recruit.GuardShared));
                Choice("lotrnpcs:talk-opt-looks", OpenLooks);
                // 0.0.154: a recruit changes class for free. 0.0.156: one choice that opens the classes.
                if (state.Classes != null && state.Classes.Count > 0)
                    Choice("lotrnpcs:talk-opt-class-menu", () => { pickClass = true; Npc(Format("lotrnpcs:talk-class-ask")); Options(); });
                // Promotion (0.0.76), once the recruit's XP is full. 0.0.113: one choice that opens the promotion screen.
                if ((state.Promote != null && state.Promote.Count > 0) || state.CaptainsFull)
                    Choice("lotrnpcs:talk-opt-promote-menu", () => { capi.ModLoader.GetModSystem<RosterSystem>()?.Send(new RosterRequest { Action = "promote-menu", Id = npc.EntityId }); Options(); });
                Add(Lang.Get("lotrnpcs:talk-opt-ask"), Ask);
                Choice("lotrnpcs:talk-opt-dismiss", () => { confirmDismiss = true; Npc(Format("lotrnpcs:talk-dismiss-ask")); Options(); });
            }
            else if (state.Mode == "borrowed")
            {
                Choice("lotrnpcs:talk-opt-follow", () => Send(Recruit.Follow));
                Choice("lotrnpcs:talk-opt-hold", () => Send(Recruit.Hold));
                Choice("lotrnpcs:talk-opt-return", () => Send(Recruit.Guard));
                Add(Lang.Get("lotrnpcs:talk-opt-ask"), Ask);
            }
            else if (state.Mode == "shared")
            {
                Choice("lotrnpcs:talk-opt-borrow", () => Send("borrow"));
                Add(Lang.Get("lotrnpcs:talk-opt-ask"), Ask);
            }
            Add(Lang.Get("lotrnpcs:talk-opt-bye"), () => TryClose());
            Emit(comps.ToArray());
        }

        GuiDialogLooks looks;

        // The appearance dialog opens beside the talk; the NPC remarks on it when the owner is done.
        void OpenLooks()
        {
            if (looks?.IsOpened() == true) { Options(); return; }
            looks = new GuiDialogLooks(capi, npc, done =>
            {
                looks = null;
                if (!IsOpened()) return;
                Npc(Line(done ? "looks-done" : "looks-cancel"));
                Options();
            });
            looks.TryOpen();
        }

        public void OnLooksRefused(string key) => looks?.Refused(key);

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            looks?.TryClose();
        }

        // The NPC is gone (promoted into a new one, or dead): the conversation ends.
        public override void OnRenderGUI(float deltaTime)
        {
            if (!closing && (!npc.Alive || capi.World.GetEntityById(npc.EntityId) == null)) { closing = true; TryClose(); return; }
            base.OnRenderGUI(deltaTime);
        }

        // The server answers with a new TalkState (OnReply).
        void Send(string action) => capi.Network.SendEntityPacket(npc.EntityId, EntityBehaviorHireable.ActionPacket, Encoding.UTF8.GetBytes(action));

        // A random question and the NPC's answer, never the same twice in a row.
        void Ask()
        {
            string prefix = Count("lotrnpcs:talk-ask-" + faction + "-") > 0 ? faction + "-" : "";
            int count = Count("lotrnpcs:talk-ask-" + prefix);
            if (count == 0) { Options(); return; }
            int pick = capi.World.Rand.Next(count) + 1;
            if (pick == lastQuestion && count > 1) pick = pick % count + 1;
            lastQuestion = pick;
            Player(Lang.Get("lotrnpcs:talk-ask-" + prefix + pick));
            Npc(Lang.Get("lotrnpcs:talk-answer-" + prefix + pick));
            Options();
        }

        // A random line of this kind for the NPC's faction, or a general one.
        string Line(string kind)
        {
            foreach (string prefix in new[] { "lotrnpcs:talk-" + kind + "-" + faction + "-", "lotrnpcs:talk-" + kind + "-" })
            {
                int count = Count(prefix);
                if (count > 0) return Format(prefix + (capi.World.Rand.Next(count) + 1));
            }
            return "...";
        }

        // {0} price, {1} coin name, {2} the baseline roster, {3} owner, {4} recruits the player leads, {5} the baseline
        // follow limit, {6} the room line (0.0.90 follow shares: "Room in your band: Mordor orcs 1, Men of Gondor 0."),
        // {7} the roster line (0.0.91: "Room on your roster: Mordor orcs 12.").
        string ClassTitle(string cls) =>
            Recruit.TitleKey(faction, cls, k => Lang.HasTranslation(k)) is string key ? Lang.Get(key).ToLowerInvariant() : cls;

        string Format(string key) => Lang.Get(key, state.Price, CoinName(), state.Cap, state.OwnerName ?? "", state.Hired, state.FollowCap,
            HiringSystem.RoomText(state.Room, ClientLang), HiringSystem.RosterText(state.RosterRoom, ClientLang));

        static string ClientLang(string key, object[] args) => Lang.Get("lotrnpcs:" + key, args);

        // 0.0.91: what still fits, in grey under the NPC's words, for a player who could hire it and after a hire.
        void Status()
        {
            string text = (HiringSystem.RoomText(state.Room, ClientLang) + " " + HiringSystem.RosterText(state.RosterRoom, ClientLang)).Trim();
            if (text.Length > 0) Emit(new RichTextComponentBase[] { new RichTextComponent(capi, text + "\r\n", Font().WithColor(GuiStyle.ColorParchment)) });
        }

        string CoinName()
        {
            if (string.IsNullOrEmpty(state.Coin)) return "";
            AssetLocation code = new AssetLocation(state.Coin);
            CollectibleObject coin = (CollectibleObject)capi.World.GetItem(code) ?? capi.World.GetBlock(code);
            return coin == null ? state.Coin : new ItemStack(coin).GetName();
        }

        static int Count(string prefix)
        {
            int n = 0;
            while (Has(prefix + (n + 1))) n++;
            return n;
        }

        // Our lines are English only so far: fall back to English in other languages, as Lang.Get does.
        static bool Has(string key) => Lang.HasTranslation(key, false, false)
            || (Lang.AvailableLanguages.TryGetValue(Lang.DefaultLocale, out ITranslationService english) && english.HasTranslation(key, false, false));

        public override void OnKeyDown(KeyEvent args)
        {
            if (args.KeyCode >= (int)GlKeys.Number1 && args.KeyCode <= (int)GlKeys.Number9)
            {
                int index = args.KeyCode - (int)GlKeys.Number1, i = 0;
                foreach (RichTextComponentBase comp in textElem.Components)
                {
                    if (!(comp is LinkTextComponent link) || !link.Clickable) continue;
                    if (i++ == index)
                    {
                        link.Trigger();
                        args.Handled = true;
                        break;
                    }
                }
            }
            base.OnKeyDown(args);
        }

        // Closes when the NPC dies or goes, or the player walks away.
        public override void OnFinalizeFrame(float dt)
        {
            base.OnFinalizeFrame(dt);
            EntityPlayer player = capi.World.Player?.Entity;
            double range = EntityBehaviorHireable.TalkRange + 1;
            if (closing || !IsOpened() || player == null) return;
            if (!npc.Alive || npc.ShouldDespawn || player.Pos.SquareDistanceTo(npc.Pos) > range * range)
            {
                closing = true;
                capi.Event.EnqueueMainThreadTask(() => TryClose(), "lotrnpcs-closetalk");
            }
        }
    }
}
