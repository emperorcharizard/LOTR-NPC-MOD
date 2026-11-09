using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace LotrNpcs
{
    /// <summary>One faction a player may join, as the join menu shows it (0.0.92).</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class JoinOption
    {
        public string Faction, Stance;
        public int Followers, Roster;
        // 0.0.93: the menu's section ("free", "shadow" or "other", JoinSystem.BlocOf) and the entity code whose
        // creative-tab portrait the card shows (null: none).
        public string Bloc, Portrait;
    }

    /// <summary>Server to client: open the join menu with these factions.</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class JoinMenu
    {
        public List<JoinOption> Options = new List<JoinOption>();
    }

    /// <summary>Client to server: the player picked this faction.</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class JoinPick
    {
        public string Faction;
    }

    /// <summary>Server to client: the answer to a pick. Ok, or a lang key (lotrnpcs:...) saying why not.</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class JoinResult
    {
        public bool Ok;
        public string Faction, Error;
    }

    /// <summary>
    /// The faction join menu (0.0.92). Owner, 2026-11-05: race didn't put players in factions ("when selecting a race with
    /// the lotr characters mod, it doesnt apply the player to the associated faction"), so "when a fresh player spawns in
    /// they have a menu pop up asking what faction they intend to join. as well as a selectable menu command for all
    /// players. This can only be changed once (when they select the first time). (after that its changed by admins)";
    /// "instead of applying the race allegiance, have them select right after making a character"; groups stop counting
    /// ("A"); every faction with Joinable ("A"); the menu may be closed and opened again with /recruits join ("A");
    /// "if they press escape without selecting a faction, give them a reminder of the command to open the menu in their
    /// local chat".
    /// The pick is stored where admins' /recruits faction set stores a faction (FactionSystem.SetAssigned), so admins
    /// change it the same way, and /recruits faction clear lets the player pick again.
    /// </summary>
    public class JoinSystem : ModSystem
    {
        public const string Channel = "lotrnpcs-join";
        // How long after joining the menu waits for a player whose character creation the game hasn't marked done
        // (servers without it, or another character mod); the client still waits for any creation dialog to close.
        public const double WaitForCharacterSeconds = 15;
        // The order factions are listed in (as the creative tab: free peoples, then the shadow, then the rest).
        public static readonly string[] Order = { "shire", "gondor", "rohan", "arnor", "dale", "dwarves", "elves", "mordor", "angmar",
            "mistymountains", "gundabad", "isengard", "haradrim", "easterlings", "dunlendings", "blacknumenoreans", "wickeddwarves", "tormentedelves" };

        // The menu's sections (0.0.93, owner chose the grouped cards, "A"): the lore blocs of the design doc. The design
        // doc leaves Gundabad, the wicked dwarves and the tormented elves unset and calls the Dunlendings independent;
        // Gundabad goes with the Shadow and the other three under Others (my call, shown to the owner in the mockup).
        // Factions an admin adds are Others. Only the layout uses this: it allies nobody. 0.0.95 (owner: "move wicked
        // dwarves to the shadow faction"): the wicked dwarves under Shadow.
        public static readonly string[] Blocs = { "free", "shadow", "other" };
        static readonly Dictionary<string, string> BlocByFaction = new Dictionary<string, string>
        {
            ["shire"] = "free", ["gondor"] = "free", ["rohan"] = "free", ["arnor"] = "free", ["dale"] = "free", ["dwarves"] = "free", ["elves"] = "free",
            ["mordor"] = "shadow", ["angmar"] = "shadow", ["mistymountains"] = "shadow", ["gundabad"] = "shadow", ["isengard"] = "shadow",
            ["haradrim"] = "shadow", ["easterlings"] = "shadow", ["blacknumenoreans"] = "shadow", ["wickeddwarves"] = "shadow",
        };

        /// <summary>The menu section of a faction id: free, shadow or other.</summary>
        public static string BlocOf(string faction) =>
            faction != null && BlocByFaction.TryGetValue(faction.Trim().ToLowerInvariant(), out string bloc) ? bloc : "other";

        // Which unit's portrait stands for a faction: a warrior if it has one, else a recruit, a captain, an archer,
        // then any of its types (rankless ones such as the hobbit).
        static readonly string[] PortraitRanks = { "warrior", "recruit", "captain", "archer" };

        /// <summary>The entity code whose portrait the join menu shows for a faction, from (code, faction, rank) of the
        /// loaded entity types; null if the faction has none. Codes compare in order, so the pick is stable.</summary>
        public static string PortraitType(string faction, IEnumerable<(string Code, string Faction, string Rank)> types)
        {
            if (faction == null || types == null) return null;
            List<(string Code, string Faction, string Rank)> own = types
                .Where(t => t.Code != null && string.Equals(t.Faction, faction, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Code, StringComparer.Ordinal).ToList();
            foreach (string rank in PortraitRanks)
            {
                foreach ((string Code, string Faction, string Rank) t in own)
                {
                    if (string.Equals(t.Rank, rank, StringComparison.OrdinalIgnoreCase)) return t.Code;
                }
            }
            return own.Count > 0 ? own[0].Code : null;
        }

        ICoreServerAPI sapi;
        IServerNetworkChannel serverChannel;
        List<(string Code, string Faction, string Rank)> npcTypes;
        // Players without a faction waiting for the menu: uid -> when they joined (ms).
        readonly Dictionary<string, long> waiting = new Dictionary<string, long>();

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel(Channel)
                .RegisterMessageType(typeof(JoinMenu)).RegisterMessageType(typeof(JoinPick)).RegisterMessageType(typeof(JoinResult));
        }

        // ---- server ----

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            serverChannel = api.Network.GetChannel(Channel);
            serverChannel.SetMessageHandler<JoinPick>((player, pick) => serverChannel.SendPacket(Pick(player, pick?.Faction), player));
            api.Event.PlayerNowPlaying += player =>
            {
                if (Factions?.AssignedFaction(player.PlayerUID) == null) waiting[player.PlayerUID] = api.World.ElapsedMilliseconds;
            };
            api.Event.PlayerDisconnect += player => waiting.Remove(player.PlayerUID);
            api.Event.RegisterGameTickListener(_ => SendWaiting(), 1000);
            ModCommands.RootOf(api)
                .BeginSubCommand("join")
                    .RequiresPlayer()
                    .WithDescription("Opens the menu to choose your faction. You can choose once; after that only an admin can change it")
                    .HandleWith(args =>
                    {
                        if (!(args.Caller.Player is IServerPlayer player)) return TextCommandResult.Error("Only players can join a faction.");
                        string faction = Factions?.AssignedFaction(player.PlayerUID);
                        if (faction != null) return TextCommandResult.Success(Lang.GetL(player.LanguageCode, "lotrnpcs:join-already", Folk(player.LanguageCode, faction)));
                        Open(player);
                        return TextCommandResult.Success();
                    })
                .EndSubCommand();
        }

        FactionSystem Factions => sapi?.ModLoader.GetModSystem<FactionSystem>();

        /// <summary>The factions the menu offers: those with Joinable, in Order, then any others by id.</summary>
        public static List<JoinOption> Options(FactionConfig config)
        {
            if (config?.Factions == null) return new List<JoinOption>();
            return config.Factions.Where(kv => kv.Value != null && kv.Value.Joinable)
                .OrderBy(kv => Array.IndexOf(Order, kv.Key) is int i && i >= 0 ? i : Order.Length).ThenBy(kv => kv.Key)
                .Select(kv => new JoinOption { Faction = kv.Key, Stance = kv.Value.Stance, Followers = config.FollowersOf(kv.Key), Roster = config.RosterOf(kv.Key), Bloc = BlocOf(kv.Key) })
                .ToList();
        }

        /// <summary>
        /// Puts the player in the faction if they have none yet and it may be joined. Public for tests (the harness
        /// passes a FactionSystem and no player).
        /// </summary>
        public static JoinResult Pick(FactionSystem factions, string uid, string name, string faction)
        {
            faction = faction?.Trim().ToLowerInvariant();
            if (factions == null || uid == null) return new JoinResult { Error = "lotrnpcs:join-unknown" };
            if (factions.AssignedFaction(uid) is string had) return new JoinResult { Faction = had, Error = "lotrnpcs:join-already" };
            if (faction == null || factions.Config.Def(faction) == null) return new JoinResult { Error = "lotrnpcs:join-unknown" };
            if (!factions.Config.Def(faction).Joinable) return new JoinResult { Faction = faction, Error = "lotrnpcs:join-closed" };
            factions.SetAssigned(uid, name, faction);
            return new JoinResult { Ok = true, Faction = faction };
        }

        JoinResult Pick(IServerPlayer player, string faction)
        {
            JoinResult result = Pick(Factions, player.PlayerUID, player.PlayerName, faction);
            if (result.Ok)
            {
                Factions.SaveAssigned();
                waiting.Remove(player.PlayerUID);
                sapi.Logger.Notification("[lotrnpcs] " + player.PlayerName + " joined " + result.Faction + " from the join menu.");
            }
            return result;
        }

        /// <summary>Opens the join menu for this player.</summary>
        public void Open(IServerPlayer player)
        {
            if (serverChannel == null) return;
            npcTypes = npcTypes ?? sapi.World.EntityTypes
                .Where(t => t?.Code != null && t.Attributes?["lotrnpcsFaction"].AsString(null) != null)
                .Select(t => (t.Code.ToShortString(), t.Attributes["lotrnpcsFaction"].AsString(null), t.Attributes["lotrnpcsRank"].AsString(null)))
                .ToList();
            List<JoinOption> options = Options(Factions?.Config);
            foreach (JoinOption o in options) o.Portrait = PortraitType(o.Faction, npcTypes);
            serverChannel.SendPacket(new JoinMenu { Options = options }, player);
        }

        // Sends the menu to players without a faction once the game has marked their character made (vanilla keeps
        // "createCharacter" in the player's mod data), or WaitForCharacterSeconds after they joined.
        void SendWaiting()
        {
            if (waiting.Count == 0) return;
            long now = sapi.World.ElapsedMilliseconds;
            foreach (string uid in waiting.Keys.ToList())
            {
                if (!(sapi.World.PlayerByUid(uid) is IServerPlayer player) || player.ConnectionState != EnumClientState.Playing || player.Entity == null) continue;
                if (Factions?.AssignedFaction(uid) != null) { waiting.Remove(uid); continue; }
                bool made = false;
                try { made = SerializerUtil.Deserialize(player.GetModdata("createCharacter"), false); } catch (Exception) { }
                if (!made && now - waiting[uid] < WaitForCharacterSeconds * 1000) continue;
                waiting.Remove(uid);
                Open(player);
            }
        }

        static string Folk(string lang, string faction)
        {
            string key = "lotrnpcs:folk-" + faction, text = Lang.GetL(lang, key);
            return text == key || text.EndsWith("folk-" + faction, StringComparison.Ordinal) ? faction : text;
        }

        // ---- client ----

        ICoreClientAPI capi;
        IClientNetworkChannel clientChannel;
        GuiDialogJoinFaction dialog;
        JoinMenu pendingMenu;
        long openTicker;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientChannel = api.Network.GetChannel(Channel);
            clientChannel.SetMessageHandler<JoinMenu>(menu =>
            {
                pendingMenu = menu;
                if (openTicker == 0) openTicker = api.Event.RegisterGameTickListener(_ => TryOpenPending(), 500);
            });
            clientChannel.SetMessageHandler<JoinResult>(result =>
            {
                if (result.Ok)
                {
                    capi.ShowChatMessage(Lang.Get("lotrnpcs:join-done", ClientFolk(result.Faction)));
                    dialog?.Joined();
                }
                else dialog?.Refused(Lang.Get(result.Error ?? "lotrnpcs:join-unknown", ClientFolk(result.Faction)));
            });
        }

        // Opens the menu once no character creation dialog is open (it comes right after making a character).
        void TryOpenPending()
        {
            if (pendingMenu == null || capi.World?.Player?.Entity == null || CreatingCharacter()) return;
            JoinMenu menu = pendingMenu;
            pendingMenu = null;
            capi.Event.UnregisterGameTickListener(openTicker);
            openTicker = 0;
            if (dialog?.IsOpened() == true) return;   // already open: keep it (closing it would send the reminder)
            dialog = new GuiDialogJoinFaction(capi, menu, faction => clientChannel.SendPacket(new JoinPick { Faction = faction }), ClientFolk);
            dialog.TryOpen();
        }

        bool CreatingCharacter() => capi.Gui.OpenedGuis.Any(g =>
        {
            string n = g?.GetType().Name ?? "";
            return n.Contains("CreateCharacter") || n.Contains("CharacterCreat") || n.Contains("CreateCustomCharacter") || n.Contains("CharacterSelect");
        });

        static string ClientFolk(string faction)
        {
            if (faction == null) return "";
            string key = "lotrnpcs:folk-" + faction, text = Lang.Get(key);
            return text == key || text.EndsWith("folk-" + faction, StringComparison.Ordinal) ? faction : text;
        }
    }

    /// <summary>
    /// Client: the join menu. 0.0.93 (owner, 2026-11-05: "beautify it", chose the grouped cards, "A"; "change it to
    /// followers then instead of roster total"): the factions sit under Free Peoples, Shadow and Others, three cards a
    /// row, each with the faction's portrait, its name, and "N followers · M total"; only a stance other than aggressive
    /// showed as a tag until 0.0.94 (owner: "just say hobbits"); one line under the cards explains followers and total. A click on a card asks to confirm, with the
    /// card shown again; closing without a pick reminds the player of /recruits join in their own chat.
    /// </summary>
    public class GuiDialogJoinFaction : GuiDialog
    {
        const int Columns = 3, CardWidth = 232, CardHeight = 60, Gap = 6, HeaderHeight = 28;
        const int Width = Columns * CardWidth + (Columns - 1) * Gap;
        readonly JoinMenu menu;
        readonly Action<string> send;
        readonly System.Func<string, string> folk;
        string confirming, notice;
        bool joined;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogJoinFaction(ICoreClientAPI capi, JoinMenu menu, Action<string> send, System.Func<string, string> folk) : base(capi)
        {
            this.menu = menu ?? new JoinMenu();
            this.send = send;
            this.folk = folk;
            Compose();
        }

        public void Joined()
        {
            joined = true;
            TryClose();
        }

        public void Refused(string text)
        {
            confirming = null;
            notice = text;
            Compose();
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            if (!joined) capi.ShowChatMessage(Lang.Get("lotrnpcs:join-reminder"));
        }

        /// <summary>The stats line of a card: "5 followers · 16 total".</summary>
        public static string Stats(JoinOption o) =>
            Lang.Get(o.Followers == 1 ? "lotrnpcs:join-follower" : "lotrnpcs:join-followers", o.Followers) + "  ·  " + Lang.Get("lotrnpcs:join-total", o.Roster);

        // Adds a card (and its portrait) at x, y. onPick null: shown only (the confirm screen).
        void AddCard(GuiComposer c, JoinOption o, int x, int y, Action onPick, string key)
        {
            c.AddInteractiveElement(new GuiElementFactionCard(capi, ElementBounds.Fixed(x, y, CardWidth, CardHeight), folk(o.Faction), Stats(o), onPick), key);
            Item item = o.Portrait == null ? null : capi.World.GetItem(PortraitItem(o.Portrait));
            if (item == null) return;
            DummyInventory inv = new DummyInventory(capi, 1);
            inv[0].Itemstack = new ItemStack(item);
            c.AddPassiveItemSlot(ElementBounds.Fixed(x + GuiElementFactionCard.PortraitInset, y + (CardHeight - GuiElementFactionCard.PortraitSize) / 2.0,
                GuiElementFactionCard.PortraitSize, GuiElementFactionCard.PortraitSize), inv, inv[0], false, key + "-portrait");
        }

        /// <summary>How tall (unscaled) a block of text is when wrapped to width (unscaled), plus a little room.</summary>
        static int TextHeight(CairoFont font, string text, int width)
        {
            double scale = Math.Max(0.01, RuntimeEnv.GUIScale);
            return (int)Math.Ceiling(new TextDrawUtil().GetMultilineTextHeight(font, text, GuiElement.scaled(width)) / scale) + 2;
        }

        /// <summary>The creative-tab item of an entity code (lotrnpcs:test-gondor-warrior -> lotrnpcs:creature-test-gondor-warrior).</summary>
        public static AssetLocation PortraitItem(string entityCode)
        {
            AssetLocation loc = new AssetLocation(entityCode);
            return new AssetLocation(loc.Domain, "creature-" + loc.Path);
        }

        void Compose()
        {
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            CairoFont text = CairoFont.WhiteSmallText();
            GuiComposer c = capi.Gui.CreateCompo("lotrnpcs-join", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("lotrnpcs:join-title"), () => TryClose())
                .BeginChildElements(bgBounds);
            int y = 35;
            JoinOption chosen = confirming == null ? null : menu.Options.FirstOrDefault(o => o.Faction == confirming);
            if (confirming != null)
            {
                if (chosen != null)
                {
                    AddCard(c, chosen, 0, y, null, "chosen");
                    string ask = Lang.Get("lotrnpcs:join-confirm", folk(confirming));
                    int askHeight = TextHeight(CairoFont.WhiteSmallishText(), ask, Width - CardWidth - 16);
                    c.AddStaticText(ask, CairoFont.WhiteSmallishText(), ElementBounds.Fixed(CardWidth + 16, y + 4, Width - CardWidth - 16, askHeight));
                    y += Math.Max(CardHeight, askHeight + 4) + 14;
                }
                else
                {
                    string ask = Lang.Get("lotrnpcs:join-confirm", folk(confirming));
                    int askHeight = TextHeight(CairoFont.WhiteSmallishText(), ask, Width);
                    c.AddStaticText(ask, CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, y, Width, askHeight));
                    y += askHeight + 6;
                }
                c.AddSmallButton(Lang.Get("lotrnpcs:join-yes"), () => { send(confirming); return true; }, ElementBounds.Fixed(0, y, 160, 28))
                 .AddSmallButton(Lang.Get("lotrnpcs:join-back"), () => { confirming = null; Compose(); return true; }, ElementBounds.Fixed(176, y, 160, 28));
                y += 40;
            }
            else
            {
                string explain = Lang.Get("lotrnpcs:join-explain");
                int explainHeight = TextHeight(text, explain, Width);
                c.AddStaticText(explain, text, ElementBounds.Fixed(0, y, Width, explainHeight));
                y += explainHeight + 6;
                if (menu.Options.Count == 0)
                {
                    c.AddStaticText(Lang.Get("lotrnpcs:join-none"), text, ElementBounds.Fixed(0, y, Width, 30));
                    y += 34;
                }
                int n = 0;
                foreach (string bloc in JoinSystem.Blocs)
                {
                    List<JoinOption> group = menu.Options.Where(o => (o.Bloc ?? "other") == bloc).ToList();
                    if (group.Count == 0) continue;
                    c.AddStaticElement(new GuiElementSectionHeader(capi, ElementBounds.Fixed(0, y + 4, Width, HeaderHeight - 4),
                        Lang.Get("lotrnpcs:join-bloc-" + bloc), GuiElementFactionCard.BlocColor(bloc)));
                    y += HeaderHeight;
                    for (int i = 0; i < group.Count; i++)
                    {
                        JoinOption o = group[i];
                        AddCard(c, o, (i % Columns) * (CardWidth + Gap), y + (i / Columns) * (CardHeight + Gap),
                            () => { confirming = o.Faction; notice = null; Compose(); }, "card" + n++);
                    }
                    y += ((group.Count + Columns - 1) / Columns) * (CardHeight + Gap) + 2;
                }
            }
            if (notice != null)
            {
                int noticeHeight = TextHeight(text, notice, Width);
                c.AddStaticText(notice, text.Clone().WithColor(GuiStyle.ColorTime1), ElementBounds.Fixed(0, y, Width, noticeHeight));
                y += noticeHeight + 4;
            }
            int bottom = y + 32;
            if (confirming == null && menu.Options.Count > 0)
            {
                CairoFont legendFont = CairoFont.WhiteDetailText().WithColor(GuiElementFactionCard.MutedText);
                string legend = Lang.Get("lotrnpcs:join-legend");
                int legendHeight = TextHeight(legendFont, legend, Width - 150);
                c.AddStaticText(legend, legendFont, ElementBounds.Fixed(0, y + 4, Width - 150, legendHeight));
                bottom = Math.Max(bottom, y + 4 + legendHeight);
            }
            c.AddSmallButton(Lang.Get("lotrnpcs:join-later"), () => TryClose(), ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, y, 0, 0).WithFixedPadding(10, 5))
             .AddStaticText("", text, ElementBounds.Fixed(0, bottom, 1, 1))
             .EndChildElements();
            SingleComposer = c.Compose();
        }
    }

    /// <summary>A faction card in the join menu (0.0.93): a dark panel with room for the portrait on the left, the name
    /// (up to two lines) and the stats line (no stance tag since 0.0.94, owner: "Make it just say hobbits"). Highlighted under the mouse; a click (press and
    /// release on the card) calls onPick.</summary>
    public class GuiElementFactionCard : GuiElement
    {
        public const int PortraitSize = 48, PortraitInset = 4, TextLeft = PortraitInset + PortraitSize + 8;
        public static readonly double[] CardFill = { 0, 0, 0, 0.28 }, CardBorder = { 0.42, 0.34, 0.25, 0.9 },
            HoverFill = { 1, 0.9, 0.7, 0.10 }, HoverBorder = { 0.85, 0.7, 0.42, 1 }, PortraitFill = { 0, 0, 0, 0.3 },
            NameText = { 0.93, 0.88, 0.8, 1 }, MutedText = { 0.74, 0.68, 0.58, 1 };

        /// <summary>The section heading colour: gold for the Free Peoples, ember for the Shadow, grey for the rest.</summary>
        public static double[] BlocColor(string bloc) =>
            bloc == "free" ? new double[] { 0.85, 0.71, 0.42, 1 } : bloc == "shadow" ? new double[] { 0.85, 0.54, 0.42, 1 } : MutedText;

        readonly string name, stats;
        readonly Action onPick;
        readonly TextDrawUtil textUtil = new TextDrawUtil();
        LoadedTexture hoverTexture;
        bool hover, pressed;

        public GuiElementFactionCard(ICoreClientAPI capi, ElementBounds bounds, string name, string stats, Action onPick) : base(capi, bounds)
        {
            this.name = name ?? "";
            this.stats = stats ?? "";
            this.onPick = onPick;
            hoverTexture = new LoadedTexture(capi);
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            Bounds.CalcWorldBounds();
            double x = Bounds.drawX, y = Bounds.drawY, w = Bounds.OuterWidth, h = Bounds.OuterHeight;
            RoundRectangle(ctx, x, y, w, h, scaled(3));
            ctx.SetSourceRGBA(CardFill[0], CardFill[1], CardFill[2], CardFill[3]);
            ctx.FillPreserve();
            ctx.SetSourceRGBA(CardBorder[0], CardBorder[1], CardBorder[2], CardBorder[3]);
            ctx.LineWidth = scaled(1);
            ctx.Stroke();
            double ps = scaled(PortraitSize);
            RoundRectangle(ctx, x + scaled(PortraitInset), y + (h - ps) / 2, ps, ps, scaled(2));
            ctx.SetSourceRGBA(PortraitFill[0], PortraitFill[1], PortraitFill[2], PortraitFill[3]);
            ctx.Fill();

            double left = x + scaled(TextLeft), width = w - scaled(TextLeft) - scaled(6);
            CairoFont nameFont = CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold).WithColor(NameText);
            CairoFont statsFont = CairoFont.WhiteDetailText().WithColor(MutedText);
            // 0.0.94 (owner's 0.0.93 screenshot: the stats came out large and dark): DrawTextLine draws with whatever font
            // and colour the context has, so set the stats font up first. Shrink it if it would run past the card.
            statsFont.SetupContext(ctx);
            if (statsFont.GetTextExtents(stats).Width > width)
            {
                statsFont = statsFont.Clone().WithFontSize((float)Math.Max(9, statsFont.UnscaledFontsize * width / Math.Max(1, statsFont.GetTextExtents(stats).Width)));
                statsFont.SetupContext(ctx);
            }
            double nameHeight = textUtil.GetMultilineTextHeight(nameFont, name, width);
            double statsHeight = textUtil.GetLineHeight(statsFont);
            double blockHeight = nameHeight + scaled(2) + statsHeight;
            double top = y + Math.Max(scaled(2), (h - blockHeight) / 2);
            textUtil.AutobreakAndDrawMultilineTextAt(ctx, nameFont, name, left, top, width, EnumTextOrientation.Left);
            statsFont.SetupContext(ctx);
            textUtil.DrawTextLine(ctx, statsFont, stats, left, top + nameHeight + scaled(2), false);

            if (onPick == null) return;
            ImageSurface hoverSurface = new ImageSurface(Format.Argb32, (int)Math.Ceiling(w), (int)Math.Ceiling(h));
            Context hc = genContext(hoverSurface);
            RoundRectangle(hc, 0, 0, w, h, scaled(3));
            hc.SetSourceRGBA(HoverFill[0], HoverFill[1], HoverFill[2], HoverFill[3]);
            hc.FillPreserve();
            hc.SetSourceRGBA(HoverBorder[0], HoverBorder[1], HoverBorder[2], HoverBorder[3]);
            hc.LineWidth = scaled(1.5);
            hc.Stroke();
            generateTexture(hoverSurface, ref hoverTexture);
            hc.Dispose();
            hoverSurface.Dispose();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (onPick != null && (hover || pressed) && hoverTexture.TextureId != 0)
                api.Render.Render2DTexturePremultipliedAlpha(hoverTexture.TextureId, Bounds);
        }

        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            hover = onPick != null && IsPositionInside(args.X, args.Y);
        }

        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (onPick == null || args.Button != EnumMouseButton.Left) return;
            pressed = true;
            args.Handled = true;
        }

        public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
        {
            bool was = pressed;
            pressed = false;
            if (!was || onPick == null || !IsPositionInside(args.X, args.Y)) return;
            args.Handled = true;
            api.Gui.PlaySound("menubutton_press");
            onPick();
        }

        public override void Dispose()
        {
            base.Dispose();
            hoverTexture?.Dispose();
        }
    }

    /// <summary>A section heading in the join menu (0.0.93): the title in its colour, then a thin rule to the right edge.</summary>
    public class GuiElementSectionHeader : GuiElement
    {
        readonly string title;
        readonly double[] color;
        readonly TextDrawUtil textUtil = new TextDrawUtil();

        public GuiElementSectionHeader(ICoreClientAPI capi, ElementBounds bounds, string title, double[] color) : base(capi, bounds)
        {
            this.title = title ?? "";
            this.color = color;
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            Bounds.CalcWorldBounds();
            CairoFont font = CairoFont.WhiteSmallishText().WithWeight(FontWeight.Bold).WithColor(color);
            font.SetupContext(ctx);
            double textWidth = font.GetTextExtents(title).Width, lineHeight = textUtil.GetLineHeight(font);
            double top = Bounds.drawY + (Bounds.OuterHeight - lineHeight) / 2;
            textUtil.DrawTextLine(ctx, font, title, Bounds.drawX, top, false);
            double ruleX = Bounds.drawX + textWidth + scaled(10), ruleY = Math.Round(top + lineHeight / 2) + 0.5;
            if (ruleX >= Bounds.drawX + Bounds.OuterWidth) return;
            ctx.SetSourceRGBA(GuiElementFactionCard.CardBorder[0], GuiElementFactionCard.CardBorder[1], GuiElementFactionCard.CardBorder[2], GuiElementFactionCard.CardBorder[3]);
            ctx.LineWidth = scaled(1);
            ctx.MoveTo(ruleX, ruleY);
            ctx.LineTo(Bounds.drawX + Bounds.OuterWidth, ruleY);
            ctx.Stroke();
        }
    }
}
