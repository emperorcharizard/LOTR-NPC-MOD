using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace LotrNpcs
{
    /// <summary>Client to server (0.0.113): "open", "refresh", "call", "dismiss", "promote-menu" or "promote".</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RosterRequest
    {
        public string Action, Rank;
        public long Id;
    }

    /// <summary>One recruit on the roster screen: live when loaded, else as last seen (RecruitSummary).</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RosterRow
    {
        public long Id;
        // Known: false for a recruit hired before 0.0.113 that hasn't loaded since (only its faction is known).
        public bool Loaded, Known;
        public string Code, Name, Rank, Faction, Order, Stance, Borrower;
        public int Xp, Need, Dimension, MinutesAgo;
        public double X, Y, Z;
    }

    /// <summary>Server to client: the roster screen's contents. Open: show it (else only update an open one).</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RosterState
    {
        public bool Open;
        public List<string> Header = new List<string>();
        public List<RosterRow> Rows = new List<RosterRow>();
        public string Notice;
    }

    /// <summary>One rank on the promotion screen. Open: on offer now (captain: not while the captain places are full).</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PromoteCard
    {
        public string Rank;
        public int Price;
        public bool Open;
        public float Health, Damage;
    }

    /// <summary>Server to client: open the promotion screen for this recruit.</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PromoteMenu
    {
        public long Id;
        public string Code, Faction, Title, Coin;
        public int Xp, Need, Coins;
        public bool CaptainsFull;
        public List<PromoteCard> Cards = new List<PromoteCard>();
    }

    /// <summary>
    /// 0.0.113 (design-roster-promotion.md; owner: "a roster screen is a good idea, branching promotion is good"): the
    /// roster screen (/recruits roster, or the Roster button in the orders menu) lists every recruit wherever it is, and
    /// the promotion screen (Promote... in the talk menu or on the roster) shows one card per rank a recruit can become.
    /// The server checks everything again (HiringSystem.RosterAction, PromoteRecruit).
    /// </summary>
    public class RosterSystem : ModSystem
    {
        public const string Channel = "lotrnpcs-roster";
        IServerNetworkChannel serverChannel;
        IClientNetworkChannel clientChannel;
        ICoreClientAPI capi;
        GuiDialogRoster roster;
        GuiDialogPromote promote;

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel(Channel).RegisterMessageType(typeof(RosterRequest)).RegisterMessageType(typeof(RosterState)).RegisterMessageType(typeof(PromoteMenu));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverChannel = api.Network.GetChannel(Channel);
            serverChannel.SetMessageHandler<RosterRequest>((player, req) => OnRequest(api, player, req));
            ModCommands.RootOf(api)
                .BeginSubCommand("roster")
                    .RequiresPlayer()
                    .WithDescription("Opens the roster: every recruit you have, wherever they are, with buttons to find, call, promote or dismiss them")
                    .HandleWith(args =>
                    {
                        if (args.Caller.Player is IServerPlayer player) OnRequest(api, player, new RosterRequest { Action = "open" });
                        return TextCommandResult.Success();
                    })
                .EndSubCommand();
        }

        void OnRequest(ICoreServerAPI api, IServerPlayer player, RosterRequest req)
        {
            HiringSystem hiring = api.ModLoader.GetModSystem<HiringSystem>();
            if (hiring == null || req?.Action == null) return;
            string L(string key, params object[] args) => Lang.GetL(player.LanguageCode, "lotrnpcs:" + key, args);
            switch (req.Action)
            {
                case "open":
                case "refresh":
                    {
                        RosterState state = hiring.RosterFor(player);
                        state.Open = req.Action == "open";
                        serverChannel.SendPacket(state, player);
                        break;
                    }
                case "call":
                case "dismiss":
                    serverChannel.SendPacket(hiring.RosterFor(player, hiring.RosterAction(player, req.Action, req.Id)), player);
                    break;
                case "promote-menu":
                    {
                        PromoteMenu menu = hiring.PromoteMenuFor(player, req.Id);
                        if (menu != null) serverChannel.SendPacket(menu, player);
                        else player.SendMessage(GlobalConstants.GeneralChatGroup, L("promote-unavailable"), EnumChatType.Notification);
                        break;
                    }
                case "promote":
                    {
                        EntityAgent npc = api.World.GetEntityById(req.Id) as EntityAgent;
                        string refusal = hiring.PromoteRecruit(npc, player, req.Rank, out int price);
                        if (refusal != null)
                        {
                            string coin = hiring.Factions?.Config.Def(FactionSystem.FactionOf(npc))?.HireCoin ?? FactionConfig.DefaultHireCoin;
                            player.SendMessage(GlobalConstants.GeneralChatGroup, L(refusal, price, CoinName(api.World, coin)), EnumChatType.Notification);
                        }
                        break;
                    }
            }
        }

        /// <summary>A coin's name as the game shows it, or its code when no such item or block exists.</summary>
        public static string CoinName(IWorldAccessor world, string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            AssetLocation loc = new AssetLocation(code);
            CollectibleObject coin = (CollectibleObject)world.GetItem(loc) ?? world.GetBlock(loc);
            return coin == null ? code : new ItemStack(coin).GetName();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientChannel = api.Network.GetChannel(Channel);
            clientChannel.SetMessageHandler<RosterState>(state =>
            {
                if (roster?.IsOpened() == true) { roster.Show(state); return; }
                if (!state.Open) return;
                roster = new GuiDialogRoster(capi, Send);
                roster.Show(state);
                roster.TryOpen();
            });
            clientChannel.SetMessageHandler<PromoteMenu>(menu =>
            {
                promote?.TryClose();
                promote = new GuiDialogPromote(capi, menu, rank => Send(new RosterRequest { Action = "promote", Id = menu.Id, Rank = rank }));
                promote.TryOpen();
            });
        }

        /// <summary>Client: asks the server for something (the screens answer when it does).</summary>
        public void Send(RosterRequest req) => clientChannel?.SendPacket(req);
    }

    /// <summary>Client: the roster screen. Rebuilt whenever the server sends the list (every 2 seconds while open).</summary>
    public class GuiDialogRoster : GuiDialog
    {
        const int Width = 1090, RowHeight = 28, PageRows = 12, ButtonWidth = 66;
        static readonly string[] Filters = { "all", "following", "guarding", "ready", "away" };
        static readonly (string Key, int Width)[] Columns = { ("name", 150), ("rank", 120), ("xp", 60), ("order", 140), ("stance", 80), ("where", 240) };
        readonly Action<RosterRequest> send;
        RosterState state = new RosterState();
        int filter, page, sortColumn;
        bool sortDown;
        long confirmDismiss;
        long tick;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogRoster(ICoreClientAPI capi, Action<RosterRequest> send) : base(capi)
        {
            this.send = send;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            tick = capi.Event.RegisterGameTickListener(_ => send(new RosterRequest { Action = "refresh" }), 2000);
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            capi.Event.UnregisterGameTickListener(tick);
        }

        public void Show(RosterState next)
        {
            state = next ?? new RosterState();
            if (!state.Rows.Any(r => r.Id == confirmDismiss)) confirmDismiss = 0;
            Compose();
        }

        /// <summary>Does a row pass a filter? Public for tests.</summary>
        public static bool Passes(RosterRow r, string filter) => filter switch
        {
            "following" => r.Order == Recruit.Follow && r.Borrower == null,
            "guarding" => r.Order == Recruit.Guard,
            "ready" => r.Need > 0 && r.Xp >= r.Need,
            "away" => !r.Loaded,
            _ => true,
        };

        string RankTitle(RosterRow r) =>
            Recruit.TitleKey(r.Faction, r.Rank, k => Lang.HasTranslation(k)) is string key ? Lang.Get(key) : HiringSystem.Folk(r.Faction, (k, a) => Lang.Get("lotrnpcs:" + k, a));

        string Cell(RosterRow r, string column)
        {
            EntityPlayer me = capi.World.Player?.Entity;
            switch (column)
            {
                case "name": return r.Name ?? RankTitle(r);
                case "rank": return r.Known ? RankTitle(r) : HiringSystem.Folk(r.Faction, (k, a) => Lang.Get("lotrnpcs:" + k, a));
                case "xp": return r.Need <= 0 ? "-" : Math.Min(r.Xp, r.Need) + "/" + r.Need + (r.Xp >= r.Need ? " *" : "");
                case "order":
                    if (!r.Known) return "";
                    if (r.Borrower != null) return Lang.Get("lotrnpcs:roster-borrowed", r.Borrower);
                    return Lang.Get("lotrnpcs:order-" + (r.Order ?? Recruit.Follow));
                case "stance": return r.Stance == null ? "" : Lang.Get("lotrnpcs:stance-" + r.Stance);
                case "where":
                    if (!r.Known) return Lang.Get("lotrnpcs:roster-unseen");
                    if (!r.Loaded || me == null || me.Pos.Dimension != r.Dimension)
                    {
                        // As the game shows coordinates: from the map middle, like /tp takes them.
                        EntityPos middle = capi.World.DefaultSpawnPosition;
                        int mx = (int)(middle?.X ?? 0), mz = (int)(middle?.Z ?? 0);
                        string ago = r.MinutesAgo < 60 ? Lang.Get("lotrnpcs:roster-minutes", r.MinutesAgo) : r.MinutesAgo < 48 * 60 ? Lang.Get("lotrnpcs:roster-hours", r.MinutesAgo / 60) : Lang.Get("lotrnpcs:roster-days", r.MinutesAgo / 1440);
                        return Lang.Get(r.Loaded ? "lotrnpcs:roster-elsewhere" : "lotrnpcs:roster-away", (int)r.X - mx, (int)r.Z - mz, ago);
                    }
                    return Lang.Get("lotrnpcs:roster-where", (int)Math.Round(Flat(me, r)), Lang.Get("lotrnpcs:compass-" + Compass(r.X - me.Pos.X, r.Z - me.Pos.Z)));
                default: return "";
            }
        }

        static double Flat(EntityPlayer me, RosterRow r) => Math.Sqrt((r.X - me.Pos.X) * (r.X - me.Pos.X) + (r.Z - me.Pos.Z) * (r.Z - me.Pos.Z));

        /// <summary>One of the 8 compass points (n, ne, ..., nw) for a move of dx, dz (north is -Z in the game).</summary>
        public static string Compass(double dx, double dz)
        {
            string[] points = { "n", "ne", "e", "se", "s", "sw", "w", "nw" };
            double angle = Math.Atan2(dx, -dz) * GameMath.RAD2DEG;
            return points[(int)Math.Round((angle + 360) % 360 / 45) % 8];
        }

        IComparable SortKey(RosterRow r) => Columns[sortColumn].Key switch
        {
            "xp" => r.Need <= 0 ? -1 : r.Xp / (double)r.Need,
            "where" => r.Loaded ? (capi.World.Player?.Entity is EntityPlayer me ? Flat(me, r) : 0) : double.MaxValue,
            string column => Cell(r, column) ?? "",
        };

        void Compose()
        {
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            CairoFont text = CairoFont.WhiteSmallText(), detail = CairoFont.WhiteDetailText();
            GuiComposer c = capi.Gui.CreateCompo("lotrnpcs-roster", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("lotrnpcs:roster-title"), () => TryClose())
                .BeginChildElements(bgBounds);
            int y = 35;
            foreach (string line in state.Header)
            {
                c.AddStaticText(line, text, ElementBounds.Fixed(0, y, Width, 20));
                y += 22;
            }
            y += 4;
            List<RosterRow> rows = state.Rows.Where(r => Passes(r, Filters[filter])).ToList();
            c.AddTextToggleButtons(Filters.Select(f => Lang.Get("lotrnpcs:roster-filter-" + f, state.Rows.Count(r => Passes(r, f)))).ToArray(), CairoFont.SmallButtonText(),
                i => { filter = i; page = 0; capi.Event.EnqueueMainThreadTask(Compose, "lotrnpcs-roster"); },
                Enumerable.Range(0, Filters.Length).Select(i => ElementBounds.Fixed(i * 150, y, 144, 26)).ToArray(), "filter");
            y += 34;

            // Column headers sort the list; a second click sorts the other way.
            int x = 0;
            for (int i = 0; i < Columns.Length; i++)
            {
                int col = i;
                string label = Lang.Get("lotrnpcs:roster-col-" + Columns[i].Key) + (sortColumn == i ? (sortDown ? " v" : " ^") : "");
                c.AddSmallButton(label, () => { sortDown = sortColumn == col && !sortDown; sortColumn = col; Compose(); return true; },
                    ElementBounds.Fixed(x, y, Columns[i].Width - 6, 24), EnumButtonStyle.Small);
                x += Columns[i].Width;
            }
            y += 30;

            rows = (sortDown ? rows.OrderByDescending(SortKey) : rows.OrderBy(SortKey)).ThenBy(r => r.Id).ToList();
            int pages = Math.Max(1, (rows.Count + PageRows - 1) / PageRows);
            page = GameMath.Clamp(page, 0, pages - 1);
            if (rows.Count == 0)
            {
                c.AddStaticText(Lang.Get("lotrnpcs:roster-none"), text, ElementBounds.Fixed(10, y + 4, Width, 22));
                y += RowHeight;
            }
            foreach (RosterRow r in rows.Skip(page * PageRows).Take(PageRows))
            {
                RosterRow row = r;
                x = 0;
                foreach ((string key, int w) in Columns)
                {
                    c.AddStaticText(Cell(row, key), key == "name" ? text : detail, ElementBounds.Fixed(x, y + 5, w - 6, 22));
                    x += w;
                }
                if (row.Known)
                    c.AddSmallButton(Lang.Get("lotrnpcs:roster-map"), () => { ShowOnMap(row); return true; }, ElementBounds.Fixed(x, y, ButtonWidth, 24), EnumButtonStyle.Small);
                x += ButtonWidth + 4;
                if (row.Loaded)
                    c.AddSmallButton(Lang.Get("lotrnpcs:roster-call"), () => { send(new RosterRequest { Action = "call", Id = row.Id }); return true; }, ElementBounds.Fixed(x, y, ButtonWidth, 24), EnumButtonStyle.Small);
                x += ButtonWidth + 4;
                if (row.Loaded && row.Need > 0 && row.Xp >= row.Need)
                    c.AddSmallButton(Lang.Get("lotrnpcs:roster-promote"), () => { send(new RosterRequest { Action = "promote-menu", Id = row.Id }); return true; }, ElementBounds.Fixed(x, y, ButtonWidth + 10, 24), EnumButtonStyle.Small);
                x += ButtonWidth + 14;
                bool sure = confirmDismiss == row.Id;
                c.AddSmallButton(Lang.Get(sure ? "lotrnpcs:roster-dismiss-sure" : "lotrnpcs:roster-dismiss"), () =>
                {
                    if (sure) { confirmDismiss = 0; send(new RosterRequest { Action = "dismiss", Id = row.Id }); }
                    else { confirmDismiss = row.Id; Compose(); }
                    return true;
                }, ElementBounds.Fixed(x, y, ButtonWidth + 20, 24), EnumButtonStyle.Small);
                y += RowHeight;
            }
            y += 6;
            if (pages > 1)
            {
                c.AddSmallButton("<", () => { page--; Compose(); return true; }, ElementBounds.Fixed(0, y, 40, 24))
                 .AddStaticText(Lang.Get("lotrnpcs:roster-page", page + 1, pages), detail, ElementBounds.Fixed(50, y + 5, 120, 20))
                 .AddSmallButton(">", () => { page++; Compose(); return true; }, ElementBounds.Fixed(170, y, 40, 24));
                y += 30;
            }
            c.AddStaticText(state.Notice ?? "", detail.Clone().WithColor(GuiStyle.ColorTime1), ElementBounds.Fixed(0, y, Width, 20));
            y += 24;
            c.AddSmallButton(Lang.Get("lotrnpcs:whitelist-close"), () => TryClose(), ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, y, 0, 0).WithFixedPadding(10, 5))
             .EndChildElements();
            SingleComposer = c.Compose();
            SingleComposer.ToggleButtonsSetValue("filter", filter);
        }

        // A temporary waypoint on the world map (not saved; gone when the game sends the waypoints again or on rejoining).
        void ShowOnMap(RosterRow r)
        {
            WaypointMapLayer layer = capi.ModLoader.GetModSystem<WorldMapManager>()?.MapLayers.OfType<WaypointMapLayer>().FirstOrDefault();
            if (layer == null) { capi.ShowChatMessage(Lang.Get("lotrnpcs:roster-nomap")); return; }
            string title = (r.Name ?? RankTitle(r)) + (r.Loaded ? "" : " " + Lang.Get("lotrnpcs:roster-lastseen"));
            layer.AddTemporaryWaypoint(new Waypoint
            {
                Position = new Vec3d(r.X, r.Y, r.Z), Title = title, Icon = "circle", Color = ColorUtil.ColorFromRgba(255, 200, 60, 255),
                Temporary = true, OwningPlayerUid = capi.World.Player?.PlayerUID,
            });
            capi.ShowChatMessage(Lang.Get("lotrnpcs:roster-mapped", title));
        }
    }

    /// <summary>Client: the promotion screen, one card per rank the recruit can become.</summary>
    public class GuiDialogPromote : GuiDialog
    {
        // MinWidth: one card alone (a warrior's captain card) left the title, the XP line and the footer no room.
        const int CardWidth = 200, CardHeight = 270, Gap = 10, MinWidth = 460;
        readonly PromoteMenu menu;
        readonly Action<string> promote;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogPromote(ICoreClientAPI capi, PromoteMenu menu, Action<string> promote) : base(capi)
        {
            this.menu = menu;
            this.promote = promote;
            Compose();
        }

        string Title(string rank) =>
            Recruit.TitleKey(menu.Faction, rank, k => Lang.HasTranslation(k)) is string key ? Lang.Get(key) : rank;

        /// <summary>What a rank's type holds: its weapons' names (up to three), and "+ shield" for a guard's.</summary>
        string Weapons(EntityProperties type)
        {
            JsonObject held = type?.Client?.BehaviorsAsJsonObj?.FirstOrDefault(b => b["code"].AsString() == "lotrnpcs:heldweapon");
            if (held == null) return "";
            List<string> names = (held["weapons"].AsArray() ?? Array.Empty<JsonObject>()).Select(w => w["item"].AsString()).Where(code => code != null)
                .Select(code => capi.World.GetItem(new AssetLocation(code))).Where(item => item != null).Select(item => new ItemStack(item).GetName()).Distinct().ToList();
            string list = string.Join(", ", names.Take(3)) + (names.Count > 3 ? "..." : "");
            return held["alwaysShield"].AsBool(false) ? Lang.Get("lotrnpcs:promote-withshield", list) : list;
        }

        void Compose()
        {
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            CairoFont text = CairoFont.WhiteSmallText(), detail = CairoFont.WhiteDetailText();
            string coin = RosterSystem.CoinName(capi.World, menu.Coin);
            int cardsWidth = Math.Max(1, menu.Cards.Count) * (CardWidth + Gap) - Gap, width = Math.Max(cardsWidth, MinWidth), left = (width - cardsWidth) / 2;
            GuiComposer c = capi.Gui.CreateCompo("lotrnpcs-promote", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("lotrnpcs:promote-title", menu.Title), () => TryClose())
                .BeginChildElements(bgBounds);
            int y = 35;
            c.AddStaticText(Lang.Get("lotrnpcs:promote-have", Math.Min(menu.Xp, menu.Need), menu.Need, menu.Coins, coin), text, ElementBounds.Fixed(0, y, width, 20));
            y += 30;
            for (int i = 0; i < menu.Cards.Count; i++)
            {
                PromoteCard card = menu.Cards[i];
                int x = left + i * (CardWidth + Gap), cy = y;
                c.AddInset(ElementBounds.Fixed(x, cy, CardWidth, CardHeight), 2);
                AssetLocation code = Promotion.CodeAtRank(new AssetLocation(menu.Code), card.Rank);
                EntityProperties type = capi.World.GetEntityType(code);
                Item portrait = code == null ? null : capi.World.GetItem(GuiDialogJoinFaction.PortraitItem(code.ToShortString()));
                if (portrait != null)
                {
                    DummyInventory inv = new DummyInventory(capi, 1);
                    inv[0].Itemstack = new ItemStack(portrait);
                    c.AddPassiveItemSlot(ElementBounds.Fixed(x + (CardWidth - 60) / 2.0, cy + 8, 60, 60), inv, inv[0], false, "portrait-" + i);
                }
                cy += 74;
                c.AddStaticText(Title(card.Rank), CairoFont.WhiteSmallishText().WithOrientation(EnumTextOrientation.Center), ElementBounds.Fixed(x, cy, CardWidth, 24));
                cy += 28;
                List<string> lines = new List<string>();
                if (card.Health > 0) lines.Add(Lang.Get("lotrnpcs:promote-health", card.Health.ToString("0.##")));
                if (card.Damage > 0) lines.Add(Lang.Get(card.Rank == "archer" ? "lotrnpcs:promote-arrows" : "lotrnpcs:promote-damage", card.Damage.ToString("0.##")));
                string weapons = Weapons(type);
                if (weapons.Length > 0) lines.Add(weapons);
                lines.Add(Promotion.Targets(card.Rank).Length > 0 ? Lang.Get("lotrnpcs:promote-leads", string.Join(", ", Promotion.Targets(card.Rank).Select(Title))) : Lang.Get("lotrnpcs:promote-last"));
                c.AddStaticText(string.Join("\n", lines), detail, ElementBounds.Fixed(x + 8, cy, CardWidth - 16, 100));
                cy = y + CardHeight - 64;
                bool afford = card.Price <= menu.Coins;
                string price = card.Price > 0 ? Lang.Get("lotrnpcs:promote-price", card.Price, coin) : Lang.Get("lotrnpcs:promote-free");
                // A closed card (captain places full) comes with price 0; showing it would read "Free".
                if (card.Open) c.AddStaticText(price, detail.Clone().WithColor(afford ? GuiStyle.DialogDefaultTextColor : GuiStyle.ErrorTextColor).WithOrientation(EnumTextOrientation.Center), ElementBounds.Fixed(x, cy, CardWidth, 20));
                cy += 24;
                string rank = card.Rank;
                if (!card.Open)
                    c.AddStaticText(Lang.Get("lotrnpcs:promote-captains-full"), detail.Clone().WithColor(GuiStyle.ErrorTextColor).WithOrientation(EnumTextOrientation.Center), ElementBounds.Fixed(x, cy + 4, CardWidth, 20));
                else if (!afford)
                    c.AddStaticText(Lang.Get("lotrnpcs:promote-poor"), detail.Clone().WithColor(GuiStyle.ErrorTextColor).WithOrientation(EnumTextOrientation.Center), ElementBounds.Fixed(x, cy + 4, CardWidth, 20));
                else
                    c.AddSmallButton(Lang.Get("lotrnpcs:promote-button"), () => { promote(rank); TryClose(); return true; }, ElementBounds.Fixed(x + 40, cy, CardWidth - 80, 26));
            }
            y += CardHeight + 10;
            c.AddStaticText(Lang.Get("lotrnpcs:promote-keeps"), detail, ElementBounds.Fixed(0, y, width, 40))
             .EndChildElements();
            SingleComposer = c.Compose();
        }
    }
}
