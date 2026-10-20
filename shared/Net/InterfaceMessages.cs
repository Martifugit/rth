using System.Collections.Generic;
using System.IO;
using Rth.Shared.Items;
using Rth.Shared.World;

namespace Rth.Shared.Net
{
    /// <summary>What the client needs to know about an NPC type: its name and its menu.</summary>
    public sealed class NpcInfo
    {
        public string Id = "";
        public string Name = "";
        public string Examine = "";
        /// <summary>For the bestiary: blood, hardest hit, style, whether it attacks on sight.</summary>
        public int Blood;
        public int MaxHit;
        public Rth.Shared.Combat.CombatStyle Style;
        public bool Aggressive;
        /// <summary>Whether "Attack" appears in its menu.</summary>
        public bool Attackable = true;
        /// <summary>Other menu options ("Talk-to", "Bank", "Trade"), dispatched by id on the server.</summary>
        public List<ActionDef> Actions = new List<ActionDef>();
    }

    /// <summary>Which window the server has open for the player.</summary>
    public enum InterfaceKind : byte
    {
        None = 0,
        Bank = 1,
        Shop = 2,
    }

    // ---------------------------------------------------------------- client to server

    /// <summary>Use one of an NPC's menu actions ("talk", "bank", "trade"...).</summary>
    public sealed class NpcActionMessage : Message
    {
        public int NpcId;
        public string ActionId = "";

        public override MessageType Type => MessageType.NpcAction;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(NpcId);
            w.Write(ActionId);
        }

        internal static NpcActionMessage Read(BinaryReader r) => new NpcActionMessage { NpcId = r.ReadInt32(), ActionId = r.ReadString() };
    }

    /// <summary>Pick a dialogue option, or -1 to continue past a line with no options.</summary>
    public sealed class DialogueChoiceMessage : Message
    {
        public int Option = -1;

        public override MessageType Type => MessageType.DialogueChoice;
        internal override void WriteBody(BinaryWriter w) => w.Write((sbyte)Option);
        internal static DialogueChoiceMessage Read(BinaryReader r) => new DialogueChoiceMessage { Option = r.ReadSByte() };
    }

    /// <summary>Close whatever window or conversation is open.</summary>
    public sealed class CloseInterfaceMessage : Message
    {
        public override MessageType Type => MessageType.CloseInterface;
        internal override void WriteBody(BinaryWriter w) { }
    }

    /// <summary>Put items from the pack into the bank. Slot -1 deposits the whole pack; Count 0 means all of that item.</summary>
    public sealed class BankDepositMessage : Message
    {
        /// <summary>The slot to deposit from; -1 deposits everything in that container.</summary>
        public int Slot;
        public int Count;
        /// <summary>Where from: the pack, or worn equipment.</summary>
        public ContainerKind From = ContainerKind.Inventory;

        public override MessageType Type => MessageType.BankDeposit;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((short)Slot);
            w.Write(Count);
            w.Write((byte)From);
        }

        internal static BankDepositMessage Read(BinaryReader r) =>
            new BankDepositMessage { Slot = r.ReadInt16(), Count = r.ReadInt32(), From = (ContainerKind)r.ReadByte() };
    }

    /// <summary>Take items out of the bank. Count 0 means as many as fit.</summary>
    public sealed class BankWithdrawMessage : Message
    {
        public int Slot;
        public int Count;
        /// <summary>Take bank notes instead of the items themselves (they stack; not usable until exchanged).</summary>
        public bool Noted;

        public override MessageType Type => MessageType.BankWithdraw;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((short)Slot);
            w.Write(Count);
            w.Write(Noted);
        }

        internal static BankWithdrawMessage Read(BinaryReader r) =>
            new BankWithdrawMessage { Slot = r.ReadInt16(), Count = r.ReadInt32(), Noted = r.ReadBoolean() };
    }

    public sealed class ShopBuyMessage : Message
    {
        public int Index;
        public int Count = 1;

        public override MessageType Type => MessageType.ShopBuy;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((byte)Index);
            w.Write(Count);
        }

        internal static ShopBuyMessage Read(BinaryReader r) => new ShopBuyMessage { Index = r.ReadByte(), Count = r.ReadInt32() };
    }

    public sealed class ShopSellMessage : Message
    {
        public int Slot;
        public int Count = 1;

        public override MessageType Type => MessageType.ShopSell;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((byte)Slot);
            w.Write(Count);
        }

        internal static ShopSellMessage Read(BinaryReader r) => new ShopSellMessage { Slot = r.ReadByte(), Count = r.ReadInt32() };
    }

    // ---------------------------------------------------------------- server to client

    /// <summary>A line of conversation to show, or <see cref="Open"/> false to close the dialogue box.</summary>
    public sealed class DialogueMessage : Message
    {
        public bool Open = true;
        public string Speaker = "";
        public string Text = "";
        /// <summary>Choices to pick from; empty means "click to continue".</summary>
        public List<string> Options = new List<string>();

        public override MessageType Type => MessageType.Dialogue;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(Open);
            w.Write(Speaker);
            w.Write(Text);
            w.Write((byte)Options.Count);
            foreach (string o in Options) w.Write(o);
        }

        internal static DialogueMessage Read(BinaryReader r)
        {
            var m = new DialogueMessage { Open = r.ReadBoolean(), Speaker = r.ReadString(), Text = r.ReadString() };
            int count = r.ReadByte();
            for (int i = 0; i < count; i++) m.Options.Add(r.ReadString());
            return m;
        }
    }

    /// <summary>Opens (or, with <see cref="InterfaceKind.None"/>, closes) a window such as the bank or a shop.</summary>
    public sealed class InterfaceMessage : Message
    {
        public InterfaceKind Kind;
        public string Title = "";

        public override MessageType Type => MessageType.Interface;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((byte)Kind);
            w.Write(Title);
        }

        internal static InterfaceMessage Read(BinaryReader r) => new InterfaceMessage { Kind = (InterfaceKind)r.ReadByte(), Title = r.ReadString() };
    }

    public struct ShopEntry
    {
        public string ItemId;
        public int Price;
        /// <summary>-1 for unlimited.</summary>
        public int Stock;
    }

    /// <summary>A shop's stock and prices. Sent when it opens and whenever stock changes.</summary>
    public sealed class ShopMessage : Message
    {
        public string Title = "";
        public string CurrencyName = "";
        /// <summary>Percentage of an item's value the shop pays when you sell to it; 0 if it doesn't buy.</summary>
        public int SellPercent;
        public List<ShopEntry> Entries = new List<ShopEntry>();

        public override MessageType Type => MessageType.Shop;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(Title);
            w.Write(CurrencyName);
            w.Write((byte)SellPercent);
            w.Write((byte)Entries.Count);
            foreach (ShopEntry e in Entries)
            {
                w.Write(e.ItemId);
                w.Write(e.Price);
                w.Write(e.Stock);
            }
        }

        internal static ShopMessage Read(BinaryReader r)
        {
            var m = new ShopMessage { Title = r.ReadString(), CurrencyName = r.ReadString(), SellPercent = r.ReadByte() };
            int count = r.ReadByte();
            for (int i = 0; i < count; i++) m.Entries.Add(new ShopEntry { ItemId = r.ReadString(), Price = r.ReadInt32(), Stock = r.ReadInt32() });
            return m;
        }
    }

    public enum QuestState : byte
    {
        NotStarted = 0,
        InProgress = 1,
        Complete = 2,
    }

    public struct QuestEntry
    {
        public string Id;
        public string Name;
        /// <summary>What the quest is about, shown before it starts.</summary>
        public string Summary;
        /// <summary>Journal entries reached so far, oldest first (the full story once complete).</summary>
        public List<string> Log;
        /// <summary>"Occultism 5"... what's needed to start.</summary>
        public List<string> Requirements;
        /// <summary>"250 Distillation XP", "Sleeper's ribbon"...</summary>
        public List<string> Rewards;
        public QuestState State;
        /// <summary>Summary, current stage's journal text, or the completion text.</summary>
        public string Journal;
        /// <summary>"2/3" for a counted objective, else empty.</summary>
        public string Progress;
    }

    /// <summary>The player's quest journal. Sent at login and whenever a quest changes.</summary>
    public sealed class QuestsMessage : Message
    {
        public List<QuestEntry> Quests = new List<QuestEntry>();

        public override MessageType Type => MessageType.Quests;

        // JSON, so the journal can grow fields without codec changes.
        internal override void WriteBody(BinaryWriter w) => w.Write(Newtonsoft.Json.JsonConvert.SerializeObject(Quests));

        internal static QuestsMessage Read(BinaryReader r) =>
            new QuestsMessage { Quests = Newtonsoft.Json.JsonConvert.DeserializeObject<List<QuestEntry>>(r.ReadString()) ?? new List<QuestEntry>() };
    }

    /// <summary>The player's current task (if any) and their point balances. Sent whenever they change.</summary>
    public sealed class TaskMessage : Message
    {
        /// <summary>Empty when there's no task.</summary>
        public string Description = "";
        public string GiverName = "";
        public int Remaining;
        public int Total;
        public Dictionary<string, int> Points = new Dictionary<string, int>();

        public override MessageType Type => MessageType.Task;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(Description);
            w.Write(GiverName);
            w.Write(Remaining);
            w.Write(Total);
            w.Write((byte)Points.Count);
            foreach (KeyValuePair<string, int> p in Points)
            {
                w.Write(p.Key);
                w.Write(p.Value);
            }
        }

        internal static TaskMessage Read(BinaryReader r)
        {
            var m = new TaskMessage { Description = r.ReadString(), GiverName = r.ReadString(), Remaining = r.ReadInt32(), Total = r.ReadInt32() };
            int count = r.ReadByte();
            for (int i = 0; i < count; i++) m.Points[r.ReadString()] = r.ReadInt32();
            return m;
        }
    }
}
