using System;
using System.Collections.Generic;
using Rth.Shared.Items;
using Rth.Shared.Net;

namespace Rth.Server.Items
{
    /// <summary>
    /// A fixed number of item slots: the inventory, equipment, and later banks, shops and chests.
    /// Stackable items share a slot; everything else takes one slot each. Item ids whose definition
    /// has been removed from content are kept as they are (never stacked, never lost).
    /// </summary>
    public sealed class ItemContainer
    {
        private readonly ItemSlot[] _slots;

        public ItemContainer(int size)
        {
            _slots = new ItemSlot[size];
            for (int i = 0; i < size; i++) _slots[i] = new ItemSlot { ItemId = "", Count = 0 };
        }

        public int Size => _slots.Length;

        /// <summary>Changed since the client was last told.</summary>
        public bool Dirty { get; set; } = true;

        public ItemSlot this[int slot] => _slots[slot];

        public bool InRange(int slot) => slot >= 0 && slot < _slots.Length;

        public bool IsEmpty(int slot) => _slots[slot].IsEmpty;

        public void Set(int slot, string itemId, int count)
        {
            _slots[slot] = count > 0 && !string.IsNullOrEmpty(itemId)
                ? new ItemSlot { ItemId = itemId, Count = count }
                : new ItemSlot { ItemId = "", Count = 0 };
            Dirty = true;
        }

        public void Clear(int slot) => Set(slot, "", 0);

        public int FreeSlots
        {
            get
            {
                int free = 0;
                foreach (ItemSlot s in _slots)
                    if (s.IsEmpty) free++;
                return free;
            }
        }

        public int FirstFreeSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].IsEmpty) return i;
            return -1;
        }

        public int CountOf(string itemId)
        {
            long total = 0;
            foreach (ItemSlot s in _slots)
                if (s.ItemId == itemId) total += s.Count;
            return (int)Math.Min(int.MaxValue, total);
        }

        /// <summary>Adds as many as fit. Returns how many did NOT fit.</summary>
        public int Add(ItemDef def, int count)
        {
            if (count <= 0) return 0;
            if (def.Stackable)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].ItemId != def.Id) continue;
                    long room = int.MaxValue - (long)_slots[i].Count;
                    int added = (int)Math.Min(room, count);
                    Set(i, def.Id, _slots[i].Count + added);
                    return count - added;
                }
                int free = FirstFreeSlot();
                if (free < 0) return count;
                Set(free, def.Id, count);
                return 0;
            }

            while (count > 0)
            {
                int free = FirstFreeSlot();
                if (free < 0) break;
                Set(free, def.Id, 1);
                count--;
            }
            return count;
        }

        /// <summary>Whether all of <paramref name="count"/> would fit.</summary>
        public bool CanAdd(ItemDef def, int count)
        {
            if (def.Stackable)
            {
                foreach (ItemSlot s in _slots)
                    if (s.ItemId == def.Id) return (long)s.Count + count <= int.MaxValue;
                return FreeSlots > 0;
            }
            return FreeSlots >= count;
        }

        /// <summary>Removes up to <paramref name="count"/> of an item, from any slots. Returns how many were removed.</summary>
        public int Remove(string itemId, int count)
        {
            int removed = 0;
            for (int i = 0; i < _slots.Length && removed < count; i++)
            {
                if (_slots[i].ItemId != itemId) continue;
                int take = Math.Min(count - removed, _slots[i].Count);
                Set(i, itemId, _slots[i].Count - take);
                removed += take;
            }
            return removed;
        }

        /// <summary>Removes up to <paramref name="count"/> from one slot. Returns how many were removed.</summary>
        public int RemoveAt(int slot, int count)
        {
            ItemSlot s = _slots[slot];
            int take = Math.Min(count, s.Count);
            Set(slot, s.ItemId, s.Count - take);
            return take;
        }

        public void Swap(int a, int b)
        {
            (_slots[a], _slots[b]) = (_slots[b], _slots[a]);
            Dirty = true;
        }

        public List<ItemSlot> Snapshot() => new List<ItemSlot>(_slots);
    }
}
