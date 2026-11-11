using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace LotrNpcs
{
    /// <summary>An order from the orders menu, client to server.</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RecruitCommand
    {
        // "follow", "hold", "move", "guard", "guard-shared", "formation" or "stance".
        public string Action;
        // The formation or stance.
        public string Arg;
        // Move: where to (the spot the player looked at).
        public bool HasTarget;
        public double X, Y, Z;
        // The way the player looked (across the ground).
        public double Fx, Fz;
    }

    /// <summary>Server to client: open the orders menu (0.0.89, /recruits orders, which was the client command
    /// /recruits orders).</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class OpenOrdersMenu
    {
        public bool Open = true;
    }

    /// <summary>What the server answers to an order, shown in the menu (or in chat if it was closed already).</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class RecruitCommandResult
    {
        public string Text;
    }

    /// <summary>
    /// Formations for hold, move and follow orders (user, 2026-11-03: "with them arranging themselves into the
    /// formations"). Offsets are (right, forward) in blocks from the anchor, for a group facing forward.
    /// </summary>
    public static class Formation
    {
        public const string Loose = "loose", Line = "line", Column = "column", Wedge = "wedge", Square = "square", Circle = "circle";
        public static readonly string[] All = { Loose, Line, Column, Wedge, Square, Circle };
        // Room between neighbours: NPCs are 0.6 wide and push each other apart.
        public const double Spacing = 1.8;
        // How far behind the owner the front of a following formation walks.
        public const double FollowGap = 2.5;

        public static string Valid(string formation) => All.Contains(formation) ? formation : Loose;

        public static List<Vec2d> Offsets(string formation, int n)
        {
            List<Vec2d> list = new List<Vec2d>();
            double s = Spacing;
            switch (Valid(formation))
            {
                case Line:
                    for (int i = 0; i < n; i++) list.Add(new Vec2d((i - (n - 1) / 2.0) * s, 0));
                    break;
                case Column:
                    {
                        // Two abreast.
                        int wide = n > 1 ? 2 : 1, rows = (n + wide - 1) / wide;
                        for (int i = 0; i < n; i++) list.Add(new Vec2d((i % wide - (wide - 1) / 2.0) * s, ((rows - 1) / 2.0 - i / wide) * s));
                        break;
                    }
                case Wedge:
                    {
                        // Point first, then alternating left and right, each pair a row further back.
                        int rows = n / 2;
                        for (int i = 0; i < n; i++)
                        {
                            int row = (i + 1) / 2, side = i == 0 ? 0 : i % 2 == 1 ? -1 : 1;
                            list.Add(new Vec2d(side * row * s, (rows / 2.0 - row) * s));
                        }
                        break;
                    }
                case Square:
                    {
                        int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(n))), rows = (n + cols - 1) / cols;
                        for (int i = 0; i < n; i++) list.Add(new Vec2d((i % cols - (cols - 1) / 2.0) * s, ((rows - 1) / 2.0 - i / cols) * s));
                        break;
                    }
                case Circle:
                    {
                        // Neighbours a spacing apart along the chord.
                        double r = n < 2 ? 2 : Math.Max(2, s / (2 * Math.Sin(Math.PI / n)));
                        for (int i = 0; i < n; i++)
                        {
                            double a = 2 * Math.PI * i / Math.Max(1, n);
                            list.Add(new Vec2d(r * Math.Sin(a), r * Math.Cos(a)));
                        }
                        break;
                    }
                default:
                    // Loose: a rough cluster (a sunflower spiral), so a group sent somewhere doesn't pile onto one block.
                    for (int i = 0; i < n; i++)
                    {
                        double r = s * 0.8 * Math.Sqrt(i), a = i * 2.39996;
                        list.Add(new Vec2d(r * Math.Cos(a), r * Math.Sin(a)));
                    }
                    break;
            }
            return list;
        }

        /// <summary>Places behind a walking owner: the same shapes with their front FollowGap behind, except that a
        /// circle goes around the owner like a bodyguard.</summary>
        public static List<Vec2d> FollowOffsets(string formation, int n)
        {
            List<Vec2d> offsets = Offsets(formation, n);
            if (offsets.Count == 0) return offsets;
            if (Valid(formation) == Circle)
            {
                double r = offsets.Max(o => Math.Sqrt(o.X * o.X + o.Y * o.Y));
                double k = r < FollowGap ? FollowGap / Math.Max(r, 0.01) : 1;
                return offsets.Select(o => new Vec2d(o.X * k, o.Y * k)).ToList();
            }
            double front = offsets.Max(o => o.Y);
            return offsets.Select(o => new Vec2d(o.X, o.Y - front - FollowGap)).ToList();
        }

        /// <summary>The world position of an offset from anchor, for a group facing (fx, fz).</summary>
        public static Vec3d World(Vec3d anchor, double fx, double fz, Vec2d offset)
        {
            double len = Math.Sqrt(fx * fx + fz * fz);
            if (len < 1e-6) { fx = 0; fz = 1; len = 1; }
            fx /= len; fz /= len;
            // Right of facing (fx, fz) seen from above.
            double rx = -fz, rz = fx;
            return new Vec3d(anchor.X + rx * offset.X + fx * offset.Y, anchor.Y, anchor.Z + rz * offset.X + fz * offset.Y);
        }

        /// <summary>
        /// Like Assign, but members marked "back" (archers, 0.0.55) take the rearmost spots, so they shoot over the
        /// others. Depth comes from each spot's offset (larger Y is further forward). Returns the spot index for each
        /// member.
        /// </summary>
        public static int[] Assign(IList<Vec3d> members, IList<Vec3d> spots, IList<Vec2d> offsets, IList<bool> back)
        {
            int n = Math.Min(members.Count, spots.Count);
            int backCount = Enumerable.Range(0, members.Count).Count(i => back[i]);
            if (backCount == 0 || backCount == members.Count || offsets == null) return Assign(members, spots);
            // Spots front first; ties keep their order.
            int[] byDepth = Enumerable.Range(0, spots.Count).OrderByDescending(j => Math.Round(offsets[j].Y, 3)).ToArray();
            int frontCount = members.Count - backCount;
            int[] spotOf = new int[members.Count];
            for (int pass = 0; pass < 2; pass++)
            {
                bool wantBack = pass == 1;
                List<int> who = Enumerable.Range(0, members.Count).Where(i => back[i] == wantBack).ToList();
                List<int> where = (wantBack ? byDepth.Skip(frontCount) : byDepth.Take(frontCount)).ToList();
                int[] sub = Assign(who.Select(i => members[i]).ToList(), where.Select(j => spots[j]).ToList());
                for (int k = 0; k < who.Count; k++) spotOf[who[k]] = where[sub[k]];
            }
            return spotOf;
        }

        /// <summary>A member's place in the formation order (0.0.106): captains, warriors, recruits (and rankless), archers.</summary>
        public static int OrderOfRank(string rank) => Promotion.BaseRank(rank) switch { "captain" => 0, "warrior" => 1, "archer" => 3, _ => 2 };

        /// <summary>
        /// 0.0.106 (owner, 2026-11-09: "captains to try and be in the front middle, then warriors, then recruits", "with
        /// archers trying to be at the back or ends of formations"): spots ranked front first, then middle first (larger
        /// Y forward, smaller |X| inside); captains take the best of them, then warriors, then recruits, and archers what
        /// is left, which is the back rows or, in a single line, the ends. Within one group each spot goes to the
        /// nearest member still free, so members of a rank don't cross each other. Returns the spot index per member.
        /// </summary>
        public static int[] AssignByRank(IList<Vec3d> members, IList<Vec3d> spots, IList<Vec2d> offsets, IList<int> order)
        {
            int n = Math.Min(members.Count, spots.Count);
            if (offsets == null || order == null) return Assign(members, spots);
            int[] ranked = Enumerable.Range(0, spots.Count)
                .OrderByDescending(j => Math.Round(offsets[j].Y, 3)).ThenBy(j => Math.Round(Math.Abs(offsets[j].X), 3)).ThenBy(j => j).ToArray();
            int[] spotOf = new int[members.Count];
            int next = 0;
            foreach (int group in Enumerable.Range(0, members.Count).Select(i => order[i]).Distinct().OrderBy(g => g))
            {
                List<int> who = Enumerable.Range(0, members.Count).Where(i => order[i] == group).ToList();
                List<int> where = ranked.Skip(next).Take(who.Count).ToList();
                next += who.Count;
                int[] sub = Assign(who.Select(i => members[i]).ToList(), where.Select(j => spots[j]).ToList());
                for (int k = 0; k < who.Count; k++) spotOf[who[k]] = where[sub[k]];
            }
            return spotOf;
        }

        /// <summary>Which spot each member takes: spots in order (front first) go to the nearest member still free.
        /// Returns the spot index for each member.</summary>
        public static int[] Assign(IList<Vec3d> members, IList<Vec3d> spots)
        {
            int[] spotOf = new int[members.Count];
            bool[] taken = new bool[members.Count];
            for (int j = 0; j < spots.Count && j < members.Count; j++)
            {
                int best = -1;
                double bestDist = double.MaxValue;
                for (int i = 0; i < members.Count; i++)
                {
                    if (taken[i]) continue;
                    double d = members[i].SquareDistanceTo(spots[j]);
                    if (d < bestDist) { bestDist = d; best = i; }
                }
                taken[best] = true;
                spotOf[best] = j;
            }
            return spotOf;
        }
    }

    /// <summary>
    /// The orders menu (user, 2026-11-02/03): hold R (rebindable in Settings, Controls) for a small menu below the
    /// crosshair, modelled on the Minecraft Recruits mod but only its movement and combat pages. Click a choice or
    /// press its number (0 for the tenth); the mouse wheel or Tab switches page; letting go of the key closes it.
    /// Orders reach the player's recruits within HiringSystem.CommandRange.
    /// </summary>
    public class RecruitOrdersSystem : ModSystem
    {
        public const string Channel = "lotrnpcs-orders", HotkeyCode = "lotrnpcs-hirelingorders";

        ICoreClientAPI capi;
        IClientNetworkChannel clientChannel;
        IServerNetworkChannel serverChannel;
        GuiDialogRecruitOrders dialog;

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel(Channel).RegisterMessageType(typeof(RecruitCommand)).RegisterMessageType(typeof(RecruitCommandResult))
                .RegisterMessageType(typeof(OpenOrdersMenu));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverChannel = api.Network.GetChannel(Channel);
            serverChannel.SetMessageHandler<RecruitCommand>((player, cmd) =>
            {
                string text = api.ModLoader.GetModSystem<HiringSystem>()?.Command(player, cmd);
                if (text != null) serverChannel.SendPacket(new RecruitCommandResult { Text = text }, player);
            });
            ModCommands.RootOf(api)
                .BeginSubCommand("orders")
                    .RequiresPlayer()
                    .WithDescription("Opens the recruit orders menu until you press Escape (the same menu as holding the orders key)")
                    .HandleWith(args =>
                    {
                        if (args.Caller.Player is IServerPlayer player) serverChannel.SendPacket(new OpenOrdersMenu(), player);
                        return TextCommandResult.Success();
                    })
                .EndSubCommand();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientChannel = api.Network.GetChannel(Channel);
            clientChannel.SetMessageHandler<RecruitCommandResult>(result =>
            {
                if (dialog?.IsOpened() == true) dialog.SetStatus(result.Text);
                else api.ShowChatMessage(result.Text);
            });
            // After the chat closes, so the menu keeps the focus.
            clientChannel.SetMessageHandler<OpenOrdersMenu>(_ => api.Event.EnqueueMainThreadTask(() => OpenMenu("command"), "lotrnpcs-openorders"));
            SetUpHotkey(api);
            // Low-level key events as a second way in and out (0.0.39). They didn't open the menu in the user's game
            // either, so each refusal is logged (OnRawKeyDown).
            api.Event.KeyDown += OnRawKeyDown;
            api.Event.KeyUp += OnRawKeyUp;
            api.Event.LevelFinalize += ReportSharedKey;
        }

        void SetUpHotkey(ICoreClientAPI api)
        {
            try
            {
                // The game keeps its hotkey list after leaving a world, so on the next world the key is still there.
                // Registering it again threw and stopped this whole system from starting (0.0.57, user: the menu
                // "no longer works (opens) when holding R", nor on Caps Lock), so an existing key is reused.
                HotKey hotkey = api.Input.GetHotKeyByCode(HotkeyCode);
                if (hotkey == null)
                {
                    // First in the hotkey list (0.0.40): the user's Claims Radar binds "Show claims" to R as well, and the
                    // game gives a key to the first hotkey on it, so in 0.0.38 and 0.0.39 holding R only reached Claims Radar.
                    api.Input.RegisterHotKeyFirst(HotkeyCode, Lang.Get("lotrnpcs:hotkey-orders"), GlKeys.R, HotkeyType.CharacterControls);
                    hotkey = api.Input.GetHotKeyByCode(HotkeyCode);
                }
                if (hotkey != null)
                {
                    hotkey.Name = Lang.Get("lotrnpcs:hotkey-orders");
                    hotkey.TriggerOnUpAlso = true;
                }
                // Replaces the handler left from an earlier world.
                api.Input.SetHotKeyHandler(HotkeyCode, OnHotkey);
            }
            catch (Exception ex)
            {
                // Never again stops the channel, the key events and /recruits orders from starting.
                api.Logger.Error("[lotrnpcs] Could not set up the orders key: {0}", ex);
            }
        }

        HotKey OrdersHotKey => capi.Input.GetHotKeyByCode(HotkeyCode);
        int rawLogs;

        // The orders key, ignoring Shift, Ctrl and Alt unless the binding uses them (so it works while sprinting).
        bool IsOrdersKey(KeyEvent e)
        {
            HotKey hotkey = OrdersHotKey;
            if (hotkey?.CurrentMapping == null || capi.World?.Player == null) return false;
            return hotkey.DidPress(e, capi.World, capi.World.Player, true) || hotkey.FallbackDidPress(e, capi.World, capi.World.Player, true);
        }

        void OnRawKeyDown(KeyEvent e)
        {
            if (!IsOrdersKey(e)) return;
            if (dialog?.IsOpened() == true)
            {
                // Key repeat while the key is held.
                e.Handled = true;
                return;
            }
            // Only while playing: not while typing in chat or with another menu or the inventory open.
            List<GuiDialog> focused = capi.Gui.OpenedGuis.Where(g => g != dialog && g.Focused).ToList();
            bool typing = focused.Any(g => g.DialogType == EnumDialogType.Dialog || g.GetType().Name.IndexOf("Chat", StringComparison.OrdinalIgnoreCase) >= 0);
            bool menuOpen = capi.Gui.OpenedGuis.Any(g => g != dialog && g.DialogType == EnumDialogType.Dialog && g.PrefersUngrabbedMouse);
            if (rawLogs < 5)
            {
                rawLogs++;
                capi.Logger.Debug("[lotrnpcs] Orders key down: mouse grabbed {0}, focused [{1}], typing {2}, menu open {3}.", capi.Input.MouseGrabbed,
                    string.Join(", ", focused.Select(g => g.GetType().Name + "/" + g.DialogType)), typing, menuOpen);
            }
            if (typing || menuOpen) return;
            if (OpenMenu("key")) e.Handled = true;
        }

        void OnRawKeyUp(KeyEvent e)
        {
            HotKey hotkey = OrdersHotKey;
            if (hotkey?.CurrentMapping == null || e.KeyCode != hotkey.CurrentMapping.KeyCode || dialog?.IsOpened() != true) return;
            dialog.TryClose();
            capi.Logger.Debug("[lotrnpcs] Orders menu closed: key released.");
            e.Handled = true;
        }

        bool OnHotkey(KeyCombination comb)
        {
            if (comb.OnKeyUp)
            {
                if (dialog?.IsOpened() == true)
                {
                    dialog.TryClose();
                    capi.Logger.Debug("[lotrnpcs] Orders menu closed: hotkey released.");
                }
                return true;
            }
            if (dialog?.IsOpened() != true) OpenMenu("hotkey");
            return true;
        }

        bool OpenMenu(string how)
        {
            if (capi.World?.Player?.Entity == null) return false;
            if (dialog?.IsOpened() == true) return true;
            try
            {
                dialog ??= new GuiDialogRecruitOrders(capi, this);
                dialog.Open();
                capi.Logger.Debug("[lotrnpcs] Orders menu opened ({0}): {1}.", how, dialog.IsOpened() ? "shown" : "not shown");
                return dialog.IsOpened();
            }
            catch (Exception ex)
            {
                capi.Logger.Error("[lotrnpcs] Could not open the orders menu: {0}", ex);
                return false;
            }
        }

        // Tells the player when another binding shares the orders key, since one of them can't work as expected.
        void ReportSharedKey()
        {
            KeyCombination mine = OrdersHotKey?.CurrentMapping;
            if (mine == null) return;
            List<string> others = capi.Input.HotKeys.Values
                .Where(h => h.Code != HotkeyCode && h.CurrentMapping != null && h.CurrentMapping.KeyCode == mine.KeyCode
                    && h.CurrentMapping.Ctrl == mine.Ctrl && h.CurrentMapping.Alt == mine.Alt && h.CurrentMapping.Shift == mine.Shift
                    && h.CurrentMapping.SecondKeyCode == mine.SecondKeyCode)
                .Select(h => h.Name ?? h.Code).ToList();
            if (others.Count == 0) return;
            capi.Logger.Notification("[lotrnpcs] The recruit orders key {0} is also bound to: {1}.", mine, string.Join(", ", others));
            capi.ShowChatMessage(Lang.Get("lotrnpcs:orders-sharedkey", mine.ToString(), string.Join(", ", others)));
        }

        public void Send(RecruitCommand cmd) => clientChannel?.SendPacket(cmd);

        /// <summary>The hotkey's current key, to notice it being let go.</summary>
        public int HotkeyKeyCode => capi.Input.GetHotKeyByCode(HotkeyCode)?.CurrentMapping?.KeyCode ?? (int)GlKeys.R;
    }

    public class GuiDialogRecruitOrders : GuiDialog
    {
        const int Width = 432, Gap = 4, ButtonHeight = 26;
        readonly RecruitOrdersSystem orders;
        // 0: movement, 1: combat. Kept between openings.
        int page;
        string status = "";
        // What number keys pick on the current page, in order.
        readonly List<Action> picks = new List<Action>();

        public override string ToggleKeyCombinationCode => null;
        public override bool PrefersUngrabbedMouse => true;

        public GuiDialogRecruitOrders(ICoreClientAPI capi, RecruitOrdersSystem orders) : base(capi)
        {
            this.orders = orders;
        }

        public void Open()
        {
            status = "";
            Compose();
            TryOpen();
        }

        public void SetStatus(string text)
        {
            status = text ?? "";
            SingleComposer?.GetDynamicText("status")?.SetNewText(status);
        }

        List<Entity> Crew()
        {
            EntityPlayer me = capi.World.Player?.Entity;
            if (me == null) return new List<Entity>();
            double range = HiringSystem.CommandRange;
            return capi.World.LoadedEntities.Values.Where(e => e.Alive && Recruit.OwnerUidOf(e) != null && Recruit.LeaderUidOf(e) == me.PlayerUID && e.Pos.Dimension == me.Pos.Dimension
                && e.Pos.SquareDistanceTo(me.Pos) <= range * range).ToList();
        }

        static string Most(IEnumerable<string> values, string fallback) =>
            values.GroupBy(v => v).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? fallback;

        void Compose()
        {
            ClearComposers();
            picks.Clear();
            List<Entity> crew = Crew();
            string formation = Formation.Valid(Most(crew.Select(Recruit.FormationOf), Formation.Loose));
            string stance = Most(crew.Select(Recruit.StanceOf), Recruit.Aggressive);
            string order = Most(crew.Select(Recruit.OrderOf), Recruit.Follow);

            CairoFont titleFont = CairoFont.WhiteSmallText();
            CairoFont buttonFont = CairoFont.SmallButtonText();
            CairoFont detailFont = CairoFont.WhiteDetailText();
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding / 2);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            // Below the crosshair, out of the way of what the player is looking at.
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle).WithFixedAlignmentOffset(0, 190);

            double y = 0;
            GuiComposer c = capi.Gui.CreateCompo("lotrnpcs-orders", dialogBounds).AddShadedDialogBG(bgBounds, false).BeginChildElements(bgBounds);
            string title = crew.Count == 0 ? Lang.Get("lotrnpcs:orders-none")
                : Lang.Get("lotrnpcs:orders-title", crew.Count, Lang.Get("lotrnpcs:order-" + order), Lang.Get("lotrnpcs:stance-" + stance).ToLowerInvariant());
            c.AddStaticText(title, titleFont, ElementBounds.Fixed(0, y, Width, 20));
            y += 24;

            c.AddTextToggleButtons(new[] { Lang.Get("lotrnpcs:orders-page-movement"), Lang.Get("lotrnpcs:orders-page-combat") }, buttonFont,
                i => { page = i; capi.Event.EnqueueMainThreadTask(Compose, "lotrnpcs-orders-page"); }, Row(y, 2), "page");
            y += ButtonHeight + 8;

            if (page == 0)
            {
                // Two to a row, so "2. Hold position" fits (0.0.44 added Guard here).
                ElementBounds[] moves = Grid(y, MoveOrders, 2);
                Button(c, "lotrnpcs:orders-follow", () => Order(Recruit.Follow), moves[0]);
                Button(c, "lotrnpcs:orders-hold", () => Order(Recruit.Hold), moves[1]);
                Button(c, "lotrnpcs:orders-move", () => Order("move"), moves[2]);
                Button(c, "lotrnpcs:orders-guard", () => Order(Recruit.Guard), moves[3]);
                // The longest label gets a row of its own.
                Button(c, "lotrnpcs:orders-guard-shared", () => Order(Recruit.GuardShared), ElementBounds.Fixed(0, moves[4].fixedY, Width, ButtonHeight));
                y += Rows(MoveOrders, 2) * (ButtonHeight + Gap) + 4;
                c.AddStaticText(Lang.Get("lotrnpcs:orders-formation"), detailFont, ElementBounds.Fixed(0, y, Width, 16));
                y += 18;
                c.AddTextToggleButtons(Formation.All.Select((f, i) => KeyPrefix(MoveOrders + i) + Lang.Get("lotrnpcs:formation-" + f)).ToArray(), buttonFont,
                    i => Send("formation", Formation.All[i]), Grid(y, Formation.All.Length, 3), "formation");
                foreach (string f in Formation.All) picks.Add(() => Pick("formation", Array.IndexOf(Formation.All, f), () => Send("formation", f)));
                y += Rows(Formation.All.Length, 3) * (ButtonHeight + Gap) + 2;
            }
            else
            {
                c.AddTextToggleButtons(Recruit.Stances.Select((st, i) => KeyLabel(i) + ". " + Lang.Get("lotrnpcs:stance-" + st)).ToArray(), buttonFont,
                    i => Send("stance", Recruit.Stances[i]), Grid(y, Recruit.Stances.Length, 2), "stance");
                foreach (string st in Recruit.Stances) picks.Add(() => Pick("stance", Array.IndexOf(Recruit.Stances, st), () => Send("stance", st)));
                y += Rows(Recruit.Stances.Length, 2) * (ButtonHeight + Gap) + 2;
                c.AddStaticText(string.Join("\n", Recruit.Stances.Select(st => Lang.Get("lotrnpcs:stance-" + st) + ": " + Lang.Get("lotrnpcs:stance-desc-" + st))),
                    detailFont, ElementBounds.Fixed(0, y, Width, 64));
                y += 68;
            }

            c.AddDynamicText(status, detailFont, ElementBounds.Fixed(0, y, Width, 18), "status");
            y += 20;
            c.AddStaticText(Lang.Get("lotrnpcs:orders-hint", KeyName()), detailFont.Clone().WithColor(GuiStyle.DialogDefaultTextColor), ElementBounds.Fixed(0, y, Width, 16));
            y += 20;
            // 0.0.113: the roster screen, which stays open after the orders key is let go.
            c.AddSmallButton(Lang.Get("lotrnpcs:orders-roster"), () =>
            {
                TryClose();
                capi.ModLoader.GetModSystem<RosterSystem>()?.Send(new RosterRequest { Action = "open" });
                return true;
            }, ElementBounds.Fixed(0, y, Width, ButtonHeight), EnumButtonStyle.Normal);
            SingleComposer = c.EndChildElements().Compose();

            SingleComposer.ToggleButtonsSetValue("page", page);
            if (page == 0) SingleComposer.ToggleButtonsSetValue("formation", Array.IndexOf(Formation.All, formation));
            else SingleComposer.ToggleButtonsSetValue("stance", Array.IndexOf(Recruit.Stances, stance));
        }

        // Follow me, Hold position, Move there, Guard here, and Guard here for my group (0.0.49).
        public const int MoveOrders = 5;

        // Labels like "6. Column" and "3. Aggressive" didn't fit six or four to a row (user's screenshots, 2026-11-03).
        public static int Rows(int count, int perRow) => (count + perRow - 1) / perRow;

        /// <summary>The number key for the pick at this index: 1 to 9, then 0.</summary>
        public static string KeyLabel(int index) => ((index + 1) % 10).ToString();

        /// <summary>"6. " before a pick's name, or nothing past the tenth pick, which has no number key.</summary>
        public static string KeyPrefix(int index) => index < 10 ? KeyLabel(index) + ". " : "";

        static ElementBounds[] Grid(double y, int count, int perRow)
        {
            double w = (Width - Gap * (perRow - 1)) / (double)perRow;
            return Enumerable.Range(0, count).Select(i => ElementBounds.Fixed(i % perRow * (w + Gap), y + i / perRow * (ButtonHeight + Gap), w, ButtonHeight)).ToArray();
        }

        static ElementBounds[] Row(double y, int count) => Grid(y, count, count);

        void Button(GuiComposer c, string key, Action onClick, ElementBounds bounds)
        {
            c.AddSmallButton(Lang.Get(key), () => { onClick(); return true; }, bounds, EnumButtonStyle.Normal);
            picks.Add(onClick);
        }

        // A number key picks a toggle as a click would.
        void Pick(string group, int index, Action send)
        {
            SingleComposer.ToggleButtonsSetValue(group, index);
            send();
        }

        void Order(string action)
        {
            EntityPlayer me = capi.World.Player.Entity;
            Vec3f view = me.Pos.GetViewVector();
            RecruitCommand cmd = new RecruitCommand { Action = action, Fx = view.X, Fz = view.Z };
            if (action == "move")
            {
                // The block the crosshair is on, up to the order range; the recruits stand on top of it, or next to
                // the side that was looked at.
                BlockSelection bsel = null;
                EntitySelection esel = null;
                Vec3d eye = me.Pos.XYZ.Add(me.LocalEyePos);
                capi.World.RayTraceForSelection(eye, me.Pos.Pitch, me.Pos.Yaw, (float)HiringSystem.CommandRange, ref bsel, ref esel, null, e => false);
                if (bsel?.Position != null)
                {
                    BlockPos stand = bsel.Face == BlockFacing.UP || bsel.Face == null ? bsel.Position.UpCopy() : bsel.Position.AddCopy(bsel.Face);
                    cmd.HasTarget = true;
                    cmd.X = stand.X + 0.5;
                    cmd.Y = stand.Y;
                    cmd.Z = stand.Z + 0.5;
                }
            }
            orders.Send(cmd);
        }

        void Send(string action, string arg)
        {
            Vec3f view = capi.World.Player.Entity.Pos.GetViewVector();
            orders.Send(new RecruitCommand { Action = action, Arg = arg, Fx = view.X, Fz = view.Z });
        }

        string KeyName()
        {
            HotKey hotkey = capi.Input.GetHotKeyByCode(RecruitOrdersSystem.HotkeyCode);
            return hotkey?.CurrentMapping?.ToString() ?? "R";
        }

        public override void OnMouseWheel(MouseWheelEventArgs args)
        {
            page = 1 - page;
            Compose();
            args.SetHandled(true);
        }

        public override void OnKeyDown(KeyEvent args)
        {
            if (args.KeyCode == (int)GlKeys.Tab)
            {
                page = 1 - page;
                Compose();
                args.Handled = true;
                return;
            }
            int index = args.KeyCode >= (int)GlKeys.Number1 && args.KeyCode <= (int)GlKeys.Number9 ? args.KeyCode - (int)GlKeys.Number1
                : args.KeyCode == (int)GlKeys.Number0 ? 9 : -1;
            if (index >= 0)
            {
                if (index < picks.Count) picks[index]();
                args.Handled = true;
                return;
            }
            base.OnKeyDown(args);
        }

        public override void OnKeyUp(KeyEvent args)
        {
            if (args.KeyCode == orders.HotkeyKeyCode)
            {
                TryClose();
                args.Handled = true;
                return;
            }
            base.OnKeyUp(args);
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            status = "";
        }
    }
}
