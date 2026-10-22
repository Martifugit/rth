using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rth.Server.Content;
using Rth.Server.Items;
using Rth.Server.Persistence;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>One stack the ferryman holds, and the equipment slot it was worn in (or null if carried).</summary>
    public sealed class HeldStack
    {
        public string Item = "";
        public int Count = 1;
        public EquipSlot? Worn;
    }

    /// <summary>What the ferryman holds for a player, attached to them as a component and saved.</summary>
    public sealed class FerryHold
    {
        public List<HeldStack> Items = new List<HeldStack>();
        /// <summary>When the held gear is gone for good (real time, UTC).</summary>
        public DateTime ExpiresUtc;
        /// <summary>The fee is settled (an obol was taken), so the gear comes back for nothing.</summary>
        public bool Paid;
        /// <summary>Dead, or woken in the realm of the dead and not yet taken back.</summary>
        public bool InRealm;

        public bool Holding => Items.Count > 0;
    }

    /// <summary>
    /// The ferryman (death.json). When a player dies, everything they carried and wore goes into
    /// his hold; an obol in the pack is taken as the fee. When they wake he talks to them: pay the
    /// fee (carried coin first, then the bank) and he takes them back with their gear; or go back
    /// without it, and he keeps it for <see cref="DeathRules.HoldHours"/> real hours.
    ///
    /// Extension points: dialogue conditions <c>ferryHolds</c>, <c>ferryFree</c>, <c>ferryCanPay</c>,
    /// <c>ferryObol</c>, <c>inRealm</c>; actions <c>ferryPay</c>, <c>ferryLeave</c>; tokens
    /// <c>{ferryFee}</c>, <c>{ferryValue}</c>, <c>{ferryHours}</c>, <c>{deaths}</c>; and the public
    /// <see cref="Pay"/> / <see cref="Leave"/> for anything else that wants to use them.
    /// </summary>
    public sealed class FerrySystem : GameSystem
    {
        private const string SaveSection = "ferry";

        private ItemSystem Items => World.Get<ItemSystem>();
        private DeathRules Rules => World.Content.Death;
        private DateTime Now => World.Config.UtcNow();

        public FerryHold Of(Player player) => player.Component<FerryHold>();

        /// <summary>Set when a conversation should open at the next chance (after waking or logging in).</summary>
        private sealed class PendingTalk
        {
            public bool Open;
        }

        protected override void OnAttach()
        {
            World.Events.Subscribe<EntityDied>(OnDied);
            World.Events.Subscribe<PlayerRespawned>(e => e.Player.Component<PendingTalk>().Open = true);

            DialogueSystem dialogue = World.Get<DialogueSystem>();
            dialogue.RegisterCondition("ferryHolds", (p, _, _) => Of(p).Holding);
            dialogue.RegisterCondition("ferryFree", (p, _, _) => Fee(p) == 0);
            dialogue.RegisterCondition("ferryCanPay", (p, _, _) => CanPay(p));
            dialogue.RegisterCondition("ferryObol", (p, _, _) => Of(p).Paid);
            dialogue.RegisterCondition("inRealm", (p, _, _) => Of(p).InRealm);
            dialogue.RegisterAction("ferryPay", (p, _, _) => Pay(p));
            dialogue.RegisterAction("ferryLeave", (p, _, _) => Leave(p));
            dialogue.RegisterToken("ferryFee", (p, _, _) => Price(Fee(p)));
            dialogue.RegisterToken("ferryValue", (p, _, _) => Price(Value(p)));
            dialogue.RegisterToken("ferryHours", (p, _, _) => HoursLeft(p).ToString(CultureInfo.InvariantCulture));
            dialogue.RegisterToken("deaths", (p, _, _) => p.Deaths.ToString(CultureInfo.InvariantCulture));
        }

        // ------------------------------------------------------------------ dying

        private void OnDied(EntityDied e)
        {
            if (!(e.Victim is Player player)) return;
            FerryHold hold = Of(player);
            Expire(player, tell: false);
            hold.InRealm = true;

            // An obol carried in the pack is taken as the fee, for this death and anything still
            // held from an earlier one. Without one, everything held is owed for again.
            bool obol = Rules.Obol != null && Items.Take(player, Rules.Obol, 1);
            hold.Paid = obol;

            PlayerItems items = Items.Of(player);
            for (int i = 0; i < items.Equipment.Size; i++)
                if (!items.Equipment.IsEmpty(i)) TakeInto(hold, items.Equipment, i, (EquipSlot)i);
            for (int i = 0; i < items.Inventory.Size; i++)
                if (!items.Inventory.IsEmpty(i)) TakeInto(hold, items.Inventory, i, null);
            if (hold.Holding) hold.ExpiresUtc = Now.AddHours(Rules.HoldHours);
            else hold.Paid = false;
            World.RecalculateStats(player);

            World.Tell(player, TextKind.Ferryman, Remark(player, obol));
        }

        private static void TakeInto(FerryHold hold, ItemContainer from, int slot, EquipSlot? worn)
        {
            ItemSlot stack = from[slot];
            HeldStack? same = worn == null ? hold.Items.FirstOrDefault(h => h.Worn == null && h.Item == stack.ItemId) : null;
            if (same != null) same.Count = (int)Math.Min(int.MaxValue, (long)same.Count + stack.Count);
            else hold.Items.Add(new HeldStack { Item = stack.ItemId, Count = stack.Count, Worn = worn });
            from.Clear(slot);
        }

        /// <summary>What the ferryman says over a fresh arrival.</summary>
        private string Remark(Player player, bool tookObol)
        {
            FerrymanLines lines = Rules.Lines;
            List<string> pool =
                tookObol && lines.Obol.Count > 0 ? lines.Obol :
                player.Deaths <= 1 && lines.First.Count > 0 ? lines.First :
                Fee(player) == 0 && lines.Free.Count > 0 ? lines.Free :
                player.Deaths >= 10 && lines.Frequent.Count > 0 && World.Random.Next(3) == 0 ? lines.Frequent :
                lines.Again;
            string line = pool.Count > 0 ? pool[World.Random.Next(pool.Count)] : "The ferryman looks at you, and says nothing.";
            return line.Replace("{name}", player.Name).Replace("{deaths}", player.Deaths.ToString(CultureInfo.InvariantCulture));
        }

        // ------------------------------------------------------------------ the fee

        /// <summary>The value of everything held (the items' <see cref="ItemDef.Value"/> times their counts).</summary>
        public long Value(Player player)
        {
            long total = 0;
            foreach (HeldStack h in Of(player).Items) total += (long)(World.Content.Item(h.Item)?.Value ?? 0) * h.Count;
            return total;
        }

        /// <summary>What he asks to take the player back with their gear: nothing if paid or cheap, else a share of its value.</summary>
        public long Fee(Player player)
        {
            FerryHold hold = Of(player);
            if (!hold.Holding || hold.Paid) return 0;
            long value = Value(player);
            if (value < Rules.FreeBelow) return 0;
            return (long)Math.Ceiling(value * Rules.FeePercent / 100.0);
        }

        /// <summary>Coin the player can pay with: carried, then banked.</summary>
        public long Funds(Player player) =>
            (long)Items.CountOf(player, Rules.Currency) + (World.Find<BankSystem>()?.Of(player).CountOf(Rules.Currency) ?? 0);

        public bool CanPay(Player player) => Funds(player) >= Fee(player);

        private int HoursLeft(Player player)
        {
            FerryHold hold = Of(player);
            return hold.Holding ? Math.Max(0, (int)Math.Ceiling((hold.ExpiresUtc - Now).TotalHours)) : 0;
        }

        private string Price(long amount)
        {
            string name = (World.Content.Item(Rules.Currency)?.Name ?? Rules.Currency).ToLowerInvariant();
            return amount.ToString("N0", CultureInfo.InvariantCulture) + " " + name;
        }

        // ------------------------------------------------------------------ going back

        /// <summary>
        /// Pays the fee and takes the player back with their gear. Returns false (and says why) if
        /// they can't afford it; then nothing changes.
        /// </summary>
        public bool Pay(Player player)
        {
            if (player.IsDead) return false;
            FerryHold hold = Of(player);
            long fee = Fee(player);
            if (Funds(player) < fee)
            {
                World.Tell(player, TextKind.Warning, "You can't afford the ferryman's fee of " + Price(fee) + ".");
                return false;
            }
            Charge(player, fee);
            hold.Paid = true;
            ReturnGear(player);
            SendBack(player);
            return true;
        }

        /// <summary>Takes the player back without their gear; he keeps it until it expires.</summary>
        public void Leave(Player player)
        {
            if (player.IsDead) return;
            FerryHold hold = Of(player);
            if (hold.Holding)
                World.Tell(player, TextKind.Info, "The ferryman will keep your belongings for about " + HoursLeft(player) + " hours.");
            SendBack(player);
        }

        private void Charge(Player player, long fee)
        {
            if (fee <= 0) return;
            int fromPack = (int)Math.Min(fee, Items.CountOf(player, Rules.Currency));
            Items.Of(player).Inventory.Remove(Rules.Currency, fromPack);
            long rest = fee - fromPack;
            if (rest > 0)
            {
                ItemContainer bank = World.Get<BankSystem>().Of(player);
                bank.Remove(Rules.Currency, (int)rest);
                bank.Compact();
            }
            World.Tell(player, TextKind.Info, "You pay the ferryman " + Price(fee) + ".");
        }

        /// <summary>
        /// Gives back what's held: worn things to their slots if free, the rest to the pack, then
        /// the bank. Anything that still doesn't fit stays with him, already paid for.
        /// </summary>
        private void ReturnGear(Player player)
        {
            FerryHold hold = Of(player);
            PlayerItems items = Items.Of(player);
            ItemContainer? bank = World.Find<BankSystem>()?.Of(player);
            var kept = new List<HeldStack>();
            foreach (HeldStack h in hold.Items)
            {
                ItemDef def = World.Content.Item(h.Item) ?? new ItemDef { Id = h.Item, Name = h.Item };
                if (h.Worn is EquipSlot slot && items.Equipment.InRange((int)slot) && items.Equipment.IsEmpty((int)slot))
                {
                    items.Equipment.Set((int)slot, h.Item, h.Count);
                    continue;
                }
                int left = items.Inventory.Add(def, h.Count);
                if (left > 0 && bank != null)
                {
                    ItemDef stored = def.NoteOf != null ? World.Content.Item(def.NoteOf) ?? def : def;
                    if (bank.CanAdd(stored, left))
                    {
                        bank.Add(stored, left);
                        left = 0;
                    }
                }
                if (left > 0) kept.Add(new HeldStack { Item = h.Item, Count = left });
            }
            hold.Items = kept;
            if (kept.Count > 0) World.Tell(player, TextKind.Warning, "You have no room for some of your belongings. The ferryman keeps them for you, paid for.");
            else hold.Paid = false;
            World.RecalculateStats(player);
        }

        private void SendBack(Player player)
        {
            FerryHold hold = Of(player);
            hold.InRealm = false;
            World.Find<DialogueSystem>()?.End(player);
            DeathSystem death = World.Get<DeathSystem>();
            (int plane, TileCoord tile) = death.PlaceOrSpawn(Rules.ReturnTo);
            death.Move(player, plane, tile);
        }

        // ------------------------------------------------------------------ expiry

        /// <summary>Drops held gear whose time is up. Returns whether it did.</summary>
        private bool Expire(Player player, bool tell)
        {
            FerryHold hold = Of(player);
            if (!hold.Holding || Now < hold.ExpiresUtc) return false;
            hold.Items.Clear();
            hold.Paid = false;
            if (tell) World.Tell(player, TextKind.Info, "The ferryman no longer holds your belongings. They are gone.");
            return true;
        }

        public override void BeforePlayer(Player player)
        {
            if (World.CurrentTick % 100 == 0) Expire(player, tell: true);
        }

        public override void AfterPlayer(Player player)
        {
            PendingTalk pending = player.Component<PendingTalk>();
            if (!pending.Open || player.IsDead) return;
            pending.Open = false;
            if (Rules.Dialogue != null) World.Get<DialogueSystem>().Start(player, Rules.Dialogue, Rules.Ferryman);
        }

        // ------------------------------------------------------------------ saving

        private sealed class FerrySave
        {
            public List<HeldStack> Items = new List<HeldStack>();
            public DateTime ExpiresUtc;
            public bool Paid;
            public bool InRealm;
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            FerrySave? saved = save?.Section<FerrySave>(SaveSection);
            if (saved == null) return;
            FerryHold hold = Of(player);
            hold.Items = saved.Items ?? new List<HeldStack>();
            hold.ExpiresUtc = DateTime.SpecifyKind(saved.ExpiresUtc, DateTimeKind.Utc);
            hold.Paid = saved.Paid;
            hold.InRealm = saved.InRealm;
            Expire(player, tell: true);
            // Logged out in the realm of the dead: he picks up where he left off.
            if (hold.InRealm) player.Component<PendingTalk>().Open = true;
        }

        public override void OnSave(Player player, PlayerSave save)
        {
            FerryHold hold = Of(player);
            save.SetSection(SaveSection, new FerrySave { Items = hold.Items, ExpiresUtc = hold.ExpiresUtc, Paid = hold.Paid, InRealm = hold.InRealm });
        }
    }
}
