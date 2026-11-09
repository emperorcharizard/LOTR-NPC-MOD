using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace LotrNpcs
{
    /// <summary>Server to client: the whitelist menu's contents (0.0.76).</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WhitelistState
    {
        /// <summary>The names on the owner's whitelist, sorted.</summary>
        public List<string> Listed = new List<string>();
        /// <summary>Players online now who aren't on it (not the owner), sorted.</summary>
        public List<string> Online = new List<string>();
        /// <summary>The answer to the last change, or null.</summary>
        public string Notice;
    }

    /// <summary>Client to server: put a player on the whitelist or take them off.</summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WhitelistChange
    {
        public bool Add;
        public string Name;
    }

    /// <summary>
    /// The whitelist menu (0.0.76, user, 2026-11-04: "the whitelist menu would be good for this round. A diplomacy button
    /// for this menu can be discussed later."): /recruits whitelist with nothing after it opens it. The server keeps the
    /// list (HiringSystem); the menu only asks it to add or remove a player, on its own channel. The typed add, remove and
    /// list commands still work.
    /// </summary>
    public class WhitelistMenuSystem : ModSystem
    {
        public const string Channel = "lotrnpcs-whitelist";
        IServerNetworkChannel serverChannel;
        IClientNetworkChannel clientChannel;
        ICoreClientAPI capi;
        GuiDialogWhitelist dialog;

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel(Channel).RegisterMessageType(typeof(WhitelistState)).RegisterMessageType(typeof(WhitelistChange));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverChannel = api.Network.GetChannel(Channel);
            serverChannel.SetMessageHandler<WhitelistChange>((player, change) =>
            {
                HiringSystem hiring = api.ModLoader.GetModSystem<HiringSystem>();
                if (hiring == null || string.IsNullOrWhiteSpace(change?.Name)) return;
                string notice = hiring.ChangeWhitelist(player, change.Name.Trim(), change.Add, out _);
                serverChannel.SendPacket(hiring.WhitelistStateFor(player, notice), player);
            });
        }

        /// <summary>Server: opens (or refreshes) the menu for this player.</summary>
        public void Open(IServerPlayer player)
        {
            HiringSystem hiring = player.Entity?.Api.ModLoader.GetModSystem<HiringSystem>();
            if (hiring != null) serverChannel?.SendPacket(hiring.WhitelistStateFor(player), player);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientChannel = api.Network.GetChannel(Channel);
            clientChannel.SetMessageHandler<WhitelistState>(state =>
            {
                if (dialog?.IsOpened() == true) { dialog.Show(state); return; }
                dialog = new GuiDialogWhitelist(capi, (add, name) => clientChannel.SendPacket(new WhitelistChange { Add = add, Name = name }));
                dialog.Show(state);
                dialog.TryOpen();
            });
        }
    }

    /// <summary>Client: the whitelist menu. Rebuilt whenever the server sends the list.</summary>
    public class GuiDialogWhitelist : GuiDialog
    {
        const int Width = 460, RowHeight = 30, MaxRows = 12, MaxOnline = 12, OnlinePerRow = 3;
        readonly Action<bool, string> send;
        WhitelistState state = new WhitelistState();

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogWhitelist(ICoreClientAPI capi, Action<bool, string> send) : base(capi)
        {
            this.send = send;
        }

        public void Show(WhitelistState next)
        {
            state = next ?? new WhitelistState();
            Compose();
        }

        void Compose()
        {
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            CairoFont text = CairoFont.WhiteSmallText();
            GuiComposer c = capi.Gui.CreateCompo("lotrnpcs-whitelist", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("lotrnpcs:whitelist-title"), () => TryClose())
                .BeginChildElements(bgBounds);
            int y = 35;
            c.AddStaticText(Lang.Get("lotrnpcs:whitelist-explain"), text, ElementBounds.Fixed(0, y, Width, 60));
            y += 66;

            // Who is on it, each with a Remove button.
            c.AddStaticText(Lang.Get("lotrnpcs:whitelist-listed"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, y, Width, 25));
            y += 28;
            if (state.Listed.Count == 0)
            {
                c.AddStaticText(Lang.Get("lotrnpcs:whitelist-none"), text, ElementBounds.Fixed(10, y + 4, Width - 10, 25));
                y += RowHeight;
            }
            foreach (string name in state.Listed.Take(MaxRows))
            {
                string n = name;
                c.AddStaticText(n, text, ElementBounds.Fixed(10, y + 5, Width - 130, 25))
                 .AddSmallButton(Lang.Get("lotrnpcs:whitelist-remove"), () => { send(false, n); return true; }, ElementBounds.Fixed(Width - 110, y, 110, 26));
                y += RowHeight;
            }
            if (state.Listed.Count > MaxRows)
            {
                c.AddStaticText(Lang.Get("lotrnpcs:whitelist-more", state.Listed.Count - MaxRows), text, ElementBounds.Fixed(10, y + 4, Width - 10, 25));
                y += RowHeight;
            }
            y += 8;

            // Add by name, or one click for a player online now.
            c.AddStaticText(Lang.Get("lotrnpcs:whitelist-add-label"), CairoFont.WhiteSmallishText(), ElementBounds.Fixed(0, y, Width, 25));
            y += 28;
            c.AddTextInput(ElementBounds.Fixed(0, y, Width - 120, 28), null, CairoFont.TextInput(), "name")
             .AddSmallButton(Lang.Get("lotrnpcs:whitelist-add"), OnAddTyped, ElementBounds.Fixed(Width - 110, y + 1, 110, 26));
            y += RowHeight + 8;
            List<string> online = state.Online.Take(MaxOnline).ToList();
            if (online.Count > 0)
            {
                c.AddStaticText(Lang.Get("lotrnpcs:whitelist-online"), text, ElementBounds.Fixed(0, y + 4, Width, 25));
                y += 28;
                int cell = Width / OnlinePerRow;
                for (int i = 0; i < online.Count; i++)
                {
                    string n = online[i];
                    c.AddSmallButton(n, () => { send(true, n); return true; }, ElementBounds.Fixed((i % OnlinePerRow) * cell, y + (i / OnlinePerRow) * RowHeight, cell - 10, 26));
                }
                y += ((online.Count + OnlinePerRow - 1) / OnlinePerRow) * RowHeight;
            }
            y += 6;
            c.AddDynamicText(state.Notice ?? "", text.Clone().WithColor(GuiStyle.ColorTime1), ElementBounds.Fixed(0, y, Width, 40), "notice");
            y += 44;
            // A Diplomacy button is to be discussed later (owner, 2026-11-04); it would sit beside Close.
            c.AddSmallButton(Lang.Get("lotrnpcs:whitelist-close"), () => TryClose(), ElementBounds.Fixed(EnumDialogArea.RightFixed, 0, y, 0, 0).WithFixedPadding(10, 5))
             .EndChildElements();
            SingleComposer = c.Compose();
        }

        bool OnAddTyped()
        {
            string name = SingleComposer.GetTextInput("name").GetText()?.Trim();
            if (!string.IsNullOrEmpty(name)) send(true, name);
            return true;
        }
    }
}
