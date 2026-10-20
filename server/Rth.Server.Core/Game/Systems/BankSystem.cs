using System;
using System.Collections.Generic;
using Rth.Server.Items;
using Rth.Server.Persistence;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    public sealed class PlayerBank
    {
        public ItemContainer? Items;
    }

    /// <summary>
    /// Each player's bank: a large container where everything stacks. Opened from a bank booth
    /// (object action "bank"), a banker (NPC action "bank") or a dialogue (action "openBank").
    /// Deposits and withdrawals only work while the bank window is open.
    /// </summary>
    public sealed class BankSystem : GameSystem
    {
        public const string Action = "bank";
        private const string SaveSection = "bank";

        private ItemSystem ItemSys => World.Get<ItemSystem>();
        private InterfaceSystem Ui => World.Get<InterfaceSystem>();

        public ItemContainer Of(Player player)
        {
            PlayerBank bank = player.Component<PlayerBank>();
            return bank.Items ??= new ItemContainer(World.Content.World.BankSize, stackEverything: true);
        }

        protected override void OnAttach()
        {
            World.Get<ObjectSystem>().RegisterAction(Action, (player, placed, _) =>
                World.StartInteraction(player, new OpenBankAtObject(placed)));
            World.Get<NpcSystem>().RegisterAction(Action, (player, npc) => Open(player, npc.Plane, npc.Position));
            World.Get<DialogueSystem>().RegisterAction("openBank", (player, _, npc) => Open(player, player.Plane, npc?.Position ?? player.Position));

            World.OnMessage<BankDepositMessage>(Deposit);
            World.OnMessage<BankWithdrawMessage>(Withdraw);
        }

        public void Open(Player player, int plane, TileCoord anchor)
        {
            Of(player).Dirty = true;
            Ui.Open(player, InterfaceKind.Bank, "Bank of " + player.Name, plane, anchor);
        }

        private bool IsOpen(Player player) => Ui.IsOpen(player, InterfaceKind.Bank);

        private void Deposit(Player player, BankDepositMessage m)
        {
            if (!IsOpen(player)) return;
            ItemContainer inv = ItemSys.Of(player).Inventory;
            if (m.Slot < 0)
            {
                for (int i = 0; i < inv.Size; i++) DepositSlot(player, inv, i, 0);
                return;
            }
            if (inv.InRange(m.Slot)) DepositSlot(player, inv, m.Slot, m.Count);
        }

        /// <summary>Moves an item from the pack into the bank: all of that item if count is 0.</summary>
        private void DepositSlot(Player player, ItemContainer inv, int slot, int count)
        {
            if (inv.IsEmpty(slot)) return;
            string id = inv[slot].ItemId;
            ItemDef def = World.Content.Item(id) ?? new ItemDef { Id = id, Name = id };
            int want = count <= 0 ? inv.CountOf(id) : Math.Min(count, inv.CountOf(id));
            ItemContainer bank = Of(player);
            if (!bank.CanAdd(def, want))
            {
                World.Tell(player, TextKind.Warning, "Your bank is full.");
                return;
            }
            // Take from the clicked slot first, then the rest of the pack.
            int moved = inv.RemoveAt(slot, want);
            if (moved < want) moved += inv.Remove(id, want - moved);
            bank.Add(def, moved);
        }

        private void Withdraw(Player player, BankWithdrawMessage m)
        {
            if (!IsOpen(player)) return;
            ItemContainer bank = Of(player);
            if (!bank.InRange(m.Slot) || bank.IsEmpty(m.Slot)) return;
            ItemSlot stack = bank[m.Slot];
            ItemDef def = World.Content.Item(stack.ItemId) ?? new ItemDef { Id = stack.ItemId, Name = stack.ItemId };
            ItemContainer inv = ItemSys.Of(player).Inventory;

            int want = m.Count <= 0 ? stack.Count : Math.Min(m.Count, stack.Count);
            int fits = def.Stackable ? (inv.CanAdd(def, want) ? want : 0) : Math.Min(want, inv.FreeSlots);
            if (fits <= 0)
            {
                World.Tell(player, TextKind.Warning, "You don't have enough inventory space.");
                return;
            }
            int left = inv.Add(def, fits);
            bank.RemoveAt(m.Slot, fits - left);
            bank.Compact();
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            ItemContainer bank = Of(player);
            List<SavedStack>? saved = save?.Section<List<SavedStack>>(SaveSection);
            if (saved == null) return;
            foreach (SavedStack s in saved)
            {
                ItemDef def = World.Content.Item(s.Item) ?? new ItemDef { Id = s.Item, Name = s.Item };
                if (bank.Add(def, s.Count) > 0) World.Log("Bank of " + player.Name + " overflowed loading " + s.Item + ".");
            }
            bank.Dirty = true;
        }

        public override void OnSave(Player player, PlayerSave save)
        {
            ItemContainer bank = Of(player);
            var list = new List<SavedStack>();
            for (int i = 0; i < bank.Size; i++)
                if (!bank.IsEmpty(i)) list.Add(new SavedStack { Item = bank[i].ItemId, Count = bank[i].Count });
            save.SetSection(SaveSection, list);
        }

        private sealed class SavedStack
        {
            public string Item = "";
            public int Count;
        }

        public override void SendState(Player player)
        {
            ItemContainer bank = Of(player);
            if (!bank.Dirty || !IsOpen(player)) return;
            bank.Dirty = false;
            player.Connection.Send(new ContainerMessage { Kind = ContainerKind.Bank, Slots = bank.Snapshot() });
        }
    }

    /// <summary>Walk to a bank booth, then open the bank.</summary>
    public sealed class OpenBankAtObject : ObjectInteraction
    {
        public OpenBankAtObject(PlacedObject booth) : base(booth) { }

        protected override bool Work(World world, Player player)
        {
            world.Get<BankSystem>().Open(player, Object.Plane, Object.Tile);
            return false;
        }
    }
}
