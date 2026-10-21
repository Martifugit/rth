using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Shared.Net
{
    public static class Protocol
    {
        /// <summary>Bump whenever a message layout changes, so mismatched builds are refused at login.</summary>
        public const int Version = 12;
        public const int DefaultPort = 43600;
        /// <summary>Sanity limits on a single frame's length, in bytes.</summary>
        public const int MaxClientFrame = 4 * 1024;
        public const int MaxServerFrame = 1024 * 1024;
    }

    public enum MessageType : byte
    {
        // Client to server: intents.
        Login = 1,
        WalkTo = 2,
        Logout = 3,
        Attack = 4,
        UseAbility = 5,
        SetHymn = 6,
        ItemAction = 7,
        MoveItem = 8,
        Pickup = 9,
        ObjectAction = 10,
        NpcAction = 11,
        DialogueChoice = 12,
        CloseInterface = 13,
        BankDeposit = 14,
        BankWithdraw = 15,
        ShopBuy = 16,
        ShopSell = 17,
        Chat = 18,
        SetClientState = 19,

        // Server to client: state.
        LoginOk = 100,
        LoginRejected = 101,
        WorldUpdate = 102,
        PlayerState = 103,
        ContentInfo = 104,
        GameText = 105,
        Container = 106,
        Stats = 107,
        Skills = 108,
        Dialogue = 109,
        Interface = 110,
        Shop = 111,
        Task = 112,
        Quests = 113,
        Regions = 114,
        ChatLine = 115,
        JsonState = 116,
    }

    public abstract class Message
    {
        public abstract MessageType Type { get; }
        internal abstract void WriteBody(BinaryWriter w);
    }

    // ---------------------------------------------------------------- client to server

    public sealed class LoginMessage : Message
    {
        public int ProtocolVersion = Protocol.Version;
        public string Name = "";

        public override MessageType Type => MessageType.Login;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(ProtocolVersion);
            w.Write(Name);
        }

        internal static LoginMessage Read(BinaryReader r) =>
            new LoginMessage { ProtocolVersion = r.ReadInt32(), Name = r.ReadString() };
    }

    public sealed class WalkToMessage : Message
    {
        public int X;
        public int Y;
        public bool Run;

        public override MessageType Type => MessageType.WalkTo;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(X);
            w.Write(Y);
            w.Write(Run);
        }

        internal static WalkToMessage Read(BinaryReader r) =>
            new WalkToMessage { X = r.ReadInt32(), Y = r.ReadInt32(), Run = r.ReadBoolean() };
    }

    public sealed class LogoutMessage : Message
    {
        public override MessageType Type => MessageType.Logout;
        internal override void WriteBody(BinaryWriter w) { }
    }

    /// <summary>Target an entity: walk into range and fight it.</summary>
    public sealed class AttackMessage : Message
    {
        public int TargetId;

        public override MessageType Type => MessageType.Attack;
        internal override void WriteBody(BinaryWriter w) => w.Write(TargetId);
        internal static AttackMessage Read(BinaryReader r) => new AttackMessage { TargetId = r.ReadInt32() };
    }

    /// <summary>Queue an ability on the current target. It fires when the global cooldown allows.</summary>
    public sealed class UseAbilityMessage : Message
    {
        public string AbilityId = "";

        public override MessageType Type => MessageType.UseAbility;
        internal override void WriteBody(BinaryWriter w) => w.Write(AbilityId);
        internal static UseAbilityMessage Read(BinaryReader r) => new UseAbilityMessage { AbilityId = r.ReadString() };
    }

    /// <summary>Start or stop singing a hymn (Religion's prayers, which drain devotion).</summary>
    public sealed class SetHymnMessage : Message
    {
        public string HymnId = "";
        public bool On;

        public override MessageType Type => MessageType.SetHymn;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(HymnId);
            w.Write(On);
        }

        internal static SetHymnMessage Read(BinaryReader r) => new SetHymnMessage { HymnId = r.ReadString(), On = r.ReadBoolean() };
    }

    // ---------------------------------------------------------------- server to client

    public sealed class LoginOkMessage : Message
    {
        public int PlayerId;
        public int TickMillis;

        public override MessageType Type => MessageType.LoginOk;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(PlayerId);
            w.Write(TickMillis);
        }

        internal static LoginOkMessage Read(BinaryReader r) =>
            new LoginOkMessage { PlayerId = r.ReadInt32(), TickMillis = r.ReadInt32() };
    }

    public sealed class LoginRejectedMessage : Message
    {
        public string Reason = "";

        public override MessageType Type => MessageType.LoginRejected;
        internal override void WriteBody(BinaryWriter w) => w.Write(Reason);
        internal static LoginRejectedMessage Read(BinaryReader r) => new LoginRejectedMessage { Reason = r.ReadString() };
    }

    public enum EntityKind : byte
    {
        Player = 0,
        Npc = 1,
    }

    public enum EntityShape : byte
    {
        Capsule = 0,
        Box = 1,
        Cylinder = 2,
        Sphere = 3,
    }

    /// <summary>
    /// An entity that came into view this tick (or respawned), with its current position and look.
    /// The client replaces any entity it already had with this id.
    /// </summary>
    public sealed class EntityAdded
    {
        public int Id;
        public EntityKind Kind;
        public string Name = "";
        /// <summary>The NPC type id (empty for players), so the client can look up its menu.</summary>
        public string TypeId = "";
        public int Plane;
        public TileCoord Position;
        public EntityShape Shape;
        /// <summary>0xRRGGBB. Zero means the client picks.</summary>
        public int Color;
        /// <summary>Size in tenths (10 = normal).</summary>
        public byte Scale = 10;
        public byte HealthPercent = 100;
    }

    /// <summary>An entity in view that moved this tick, with each tile it stepped through in order.</summary>
    public sealed class EntityMoved
    {
        public int Id;
        public bool Running;
        public List<TileCoord> Steps = new List<TileCoord>();
    }

    public enum HitKind : byte
    {
        Normal = 0,
        Miss = 1,
        /// <summary>Reduced by deflection.</summary>
        Deflected = 2,
        Heal = 3,
    }

    public struct HitEvent
    {
        public int TargetId;
        public int SourceId;
        public int Amount;
        public HitKind Kind;
    }

    /// <summary>Someone used an attack or ability: the client animates it (projectile, swing).</summary>
    public struct ActionEvent
    {
        public int SourceId;
        public int TargetId;
        public string AbilityId;
        /// <summary>-1, 0 or +1: which bile pole the action draws on, for its colour.</summary>
        public sbyte Pole;
    }

    public struct HealthEvent
    {
        public int Id;
        public byte Percent;
    }

    /// <summary>Everything that changed near one player during one tick. Sent every tick, even when empty.</summary>
    public sealed class WorldUpdateMessage : Message
    {
        public uint Tick;
        public List<EntityAdded> Added = new List<EntityAdded>();
        public List<EntityMoved> Moved = new List<EntityMoved>();
        public List<int> Removed = new List<int>();
        public List<HealthEvent> Health = new List<HealthEvent>();
        public List<HitEvent> Hits = new List<HitEvent>();
        public List<ActionEvent> Actions = new List<ActionEvent>();
        public List<int> Deaths = new List<int>();
        public List<GroundItemAdded> GroundAdded = new List<GroundItemAdded>();
        public List<int> GroundRemoved = new List<int>();

        public override MessageType Type => MessageType.WorldUpdate;

        public bool IsEmpty =>
            Added.Count == 0 && Moved.Count == 0 && Removed.Count == 0 && Health.Count == 0 &&
            Hits.Count == 0 && Actions.Count == 0 && Deaths.Count == 0 && GroundAdded.Count == 0 && GroundRemoved.Count == 0;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(Tick);
            w.Write((ushort)Added.Count);
            foreach (EntityAdded a in Added)
            {
                w.Write(a.Id);
                w.Write((byte)a.Kind);
                w.Write(a.Name);
                w.Write(a.TypeId ?? "");
                w.Write((byte)a.Plane);
                WriteTile(w, a.Position);
                w.Write((byte)a.Shape);
                w.Write(a.Color);
                w.Write(a.Scale);
                w.Write(a.HealthPercent);
            }
            w.Write((ushort)Moved.Count);
            foreach (EntityMoved m in Moved)
            {
                w.Write(m.Id);
                w.Write(m.Running);
                w.Write((byte)m.Steps.Count);
                foreach (TileCoord step in m.Steps) WriteTile(w, step);
            }
            w.Write((ushort)Removed.Count);
            foreach (int id in Removed) w.Write(id);
            w.Write((ushort)Health.Count);
            foreach (HealthEvent h in Health)
            {
                w.Write(h.Id);
                w.Write(h.Percent);
            }
            w.Write((ushort)Hits.Count);
            foreach (HitEvent h in Hits)
            {
                w.Write(h.TargetId);
                w.Write(h.SourceId);
                w.Write(h.Amount);
                w.Write((byte)h.Kind);
            }
            w.Write((ushort)Actions.Count);
            foreach (ActionEvent a in Actions)
            {
                w.Write(a.SourceId);
                w.Write(a.TargetId);
                w.Write(a.AbilityId ?? "");
                w.Write(a.Pole);
            }
            w.Write((ushort)Deaths.Count);
            foreach (int id in Deaths) w.Write(id);
            w.Write((ushort)GroundAdded.Count);
            foreach (GroundItemAdded g in GroundAdded)
            {
                w.Write(g.Id);
                w.Write(g.ItemId);
                w.Write(g.Count);
                w.Write((byte)g.Plane);
                WriteTile(w, g.Position);
            }
            w.Write((ushort)GroundRemoved.Count);
            foreach (int id in GroundRemoved) w.Write(id);
        }

        internal static WorldUpdateMessage Read(BinaryReader r)
        {
            var msg = new WorldUpdateMessage { Tick = r.ReadUInt32() };
            int count = r.ReadUInt16();
            for (int i = 0; i < count; i++)
            {
                msg.Added.Add(new EntityAdded
                {
                    Id = r.ReadInt32(),
                    Kind = (EntityKind)r.ReadByte(),
                    Name = r.ReadString(),
                    TypeId = r.ReadString(),
                    Plane = r.ReadByte(),
                    Position = ReadTile(r),
                    Shape = (EntityShape)r.ReadByte(),
                    Color = r.ReadInt32(),
                    Scale = r.ReadByte(),
                    HealthPercent = r.ReadByte(),
                });
            }
            count = r.ReadUInt16();
            for (int i = 0; i < count; i++)
            {
                var m = new EntityMoved { Id = r.ReadInt32(), Running = r.ReadBoolean() };
                int steps = r.ReadByte();
                for (int s = 0; s < steps; s++) m.Steps.Add(ReadTile(r));
                msg.Moved.Add(m);
            }
            count = r.ReadUInt16();
            for (int i = 0; i < count; i++) msg.Removed.Add(r.ReadInt32());
            count = r.ReadUInt16();
            for (int i = 0; i < count; i++) msg.Health.Add(new HealthEvent { Id = r.ReadInt32(), Percent = r.ReadByte() });
            count = r.ReadUInt16();
            for (int i = 0; i < count; i++)
                msg.Hits.Add(new HitEvent { TargetId = r.ReadInt32(), SourceId = r.ReadInt32(), Amount = r.ReadInt32(), Kind = (HitKind)r.ReadByte() });
            count = r.ReadUInt16();
            for (int i = 0; i < count; i++)
                msg.Actions.Add(new ActionEvent { SourceId = r.ReadInt32(), TargetId = r.ReadInt32(), AbilityId = r.ReadString(), Pole = r.ReadSByte() });
            count = r.ReadUInt16();
            for (int i = 0; i < count; i++) msg.Deaths.Add(r.ReadInt32());
            count = r.ReadUInt16();
            for (int i = 0; i < count; i++)
                msg.GroundAdded.Add(new GroundItemAdded { Id = r.ReadInt32(), ItemId = r.ReadString(), Count = r.ReadInt32(), Plane = r.ReadByte(), Position = ReadTile(r) });
            count = r.ReadUInt16();
            for (int i = 0; i < count; i++) msg.GroundRemoved.Add(r.ReadInt32());
            return msg;
        }

        internal static void WriteTile(BinaryWriter w, TileCoord t)
        {
            w.Write(t.X);
            w.Write(t.Y);
        }

        internal static TileCoord ReadTile(BinaryReader r) => new TileCoord(r.ReadInt32(), r.ReadInt32());
    }

    public struct CooldownState
    {
        public string AbilityId;
        public uint ReadyTick;
    }

    /// <summary>A status the player is under (a <see cref="StatusDef"/> id), for the buff and debuff bars.</summary>
    public struct StatusState
    {
        public string Id;
        /// <summary>Its strength now (e.g. the % for infused); 0 when it has none.</summary>
        public int Magnitude;
        /// <summary>The tick it wears off; 0 while it lasts as long as its cause (bile, for one).</summary>
        public uint EndTick;
    }

    /// <summary>The player's own resources and combat state. Sent every tick.</summary>
    public sealed class PlayerStateMessage : Message
    {
        public uint Tick;
        public int Blood;
        public int MaxBlood;
        public int Phlegm;
        public int Bile;
        public int Devotion;
        public int MaxDevotion;
        /// <summary>The hymns being sung.</summary>
        public List<string> Hymns = new List<string>();
        public bool InCombat;
        /// <summary>The entity being fought, or 0.</summary>
        public int TargetId;
        public uint GlobalCooldownReadyTick;
        public string QueuedAbility = "";
        /// <summary>An ability used out of range, waiting to fire on reaching the target (its cooldown already runs).</summary>
        public string StalledAbility = "";
        public List<CooldownState> Cooldowns = new List<CooldownState>();
        public List<StatusState> Statuses = new List<StatusState>();

        public override MessageType Type => MessageType.PlayerState;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(Tick);
            w.Write(Blood);
            w.Write(MaxBlood);
            w.Write(Phlegm);
            w.Write(Bile);
            w.Write(Devotion);
            w.Write(MaxDevotion);
            w.Write((byte)Hymns.Count);
            foreach (string hymn in Hymns) w.Write(hymn);
            w.Write(InCombat);
            w.Write(TargetId);
            w.Write(GlobalCooldownReadyTick);
            w.Write(QueuedAbility);
            w.Write(StalledAbility);
            w.Write((byte)Cooldowns.Count);
            foreach (CooldownState c in Cooldowns)
            {
                w.Write(c.AbilityId);
                w.Write(c.ReadyTick);
            }
            w.Write((byte)Statuses.Count);
            foreach (StatusState s in Statuses)
            {
                w.Write(s.Id);
                w.Write(s.Magnitude);
                w.Write(s.EndTick);
            }
        }

        internal static PlayerStateMessage Read(BinaryReader r)
        {
            var m = new PlayerStateMessage
            {
                Tick = r.ReadUInt32(),
                Blood = r.ReadInt32(),
                MaxBlood = r.ReadInt32(),
                Phlegm = r.ReadInt32(),
                Bile = r.ReadInt32(),
                Devotion = r.ReadInt32(),
                MaxDevotion = r.ReadInt32(),
            };
            int count = r.ReadByte();
            for (int i = 0; i < count; i++) m.Hymns.Add(r.ReadString());
            m.InCombat = r.ReadBoolean();
            m.TargetId = r.ReadInt32();
            m.GlobalCooldownReadyTick = r.ReadUInt32();
            m.QueuedAbility = r.ReadString();
            m.StalledAbility = r.ReadString();
            count = r.ReadByte();
            for (int i = 0; i < count; i++) m.Cooldowns.Add(new CooldownState { AbilityId = r.ReadString(), ReadyTick = r.ReadUInt32() });
            count = r.ReadByte();
            for (int i = 0; i < count; i++) m.Statuses.Add(new StatusState { Id = r.ReadString(), Magnitude = r.ReadInt32(), EndTick = r.ReadUInt32() });
            return m;
        }
    }

    /// <summary>
    /// Everything the client needs to know about content to draw its UI: abilities, items, the
    /// action bar and the bile thresholds. Sent at login and after every hot-reload.
    /// </summary>
    public sealed class ClientContent
    {
        public List<AbilityDef> Abilities = new List<AbilityDef>();
        /// <summary>Ids of the abilities on the player's action bar, in slot order.</summary>
        public List<string> ActionBar = new List<string>();
        public List<ItemDef> Items = new List<ItemDef>();
        public List<SkillDef> Skills = new List<SkillDef>();
        public XpCurve XpCurve = new XpCurve();
        public List<ObjectDef> Objects = new List<ObjectDef>();
        public List<NpcInfo> Npcs = new List<NpcInfo>();
        public int BileLimit;
        public int MaxPhlegm;
        public int GlobalCooldownTicks = 3;
        public List<RecipeInfo> Recipes = new List<RecipeInfo>();
        public List<Rth.Shared.Progress.AchievementDef> Achievements = new List<Rth.Shared.Progress.AchievementDef>();
        public List<StatusDef> Statuses = new List<StatusDef>();
        public List<Rth.Shared.Religion.HymnDef> Hymns = new List<Rth.Shared.Religion.HymnDef>();
        public CombatLevelDef CombatLevel = new CombatLevelDef();
    }

    /// <summary>
    /// Carries <see cref="ClientContent"/> as JSON: it's sent rarely, and this way new content
    /// fields and item components reach the client without any codec changes.
    /// </summary>
    /// <summary>What the client needs to know about a recipe: for skill guides and tooltips.</summary>
    public sealed class RecipeInfo
    {
        public string Id = "";
        public string Name = "";
        public string Verb = "";
        public string Skill = "";
        public int Level;
        public string Station = "";
        public List<ItemCost> Inputs = new List<ItemCost>();
        public double Xp;
    }

    public sealed class ContentInfoMessage : Message
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

        public string Json = "{}";

        public override MessageType Type => MessageType.ContentInfo;

        public static ContentInfoMessage From(ClientContent content) =>
            new ContentInfoMessage { Json = JsonConvert.SerializeObject(content, Settings) };

        public ClientContent Parse() => JsonConvert.DeserializeObject<ClientContent>(Json, Settings) ?? new ClientContent();

        internal override void WriteBody(BinaryWriter w) => w.Write(Json);

        internal static ContentInfoMessage Read(BinaryReader r) => new ContentInfoMessage { Json = r.ReadString() };
    }

    /// <summary>
    /// The world's regions, sent at login, so the client knows which scene to stream in where.
    /// JSON, like content, so regions can grow fields without codec changes.
    /// </summary>
    public sealed class RegionsMessage : Message
    {
        public List<WorldRegion> Regions = new List<WorldRegion>();

        public override MessageType Type => MessageType.Regions;

        internal override void WriteBody(BinaryWriter w) => w.Write(JsonConvert.SerializeObject(Regions));

        internal static RegionsMessage Read(BinaryReader r) =>
            new RegionsMessage { Regions = JsonConvert.DeserializeObject<List<WorldRegion>>(r.ReadString()) ?? new List<WorldRegion>() };
    }

    public struct GroundItemAdded
    {
        public int Id;
        public string ItemId;
        public int Count;
        public int Plane;
        public TileCoord Position;
    }

    public struct ItemSlot
    {
        /// <summary>Empty string for an empty slot.</summary>
        public string ItemId;
        public int Count;

        public bool IsEmpty => string.IsNullOrEmpty(ItemId) || Count <= 0;
    }

    /// <summary>The full contents of one of the player's item containers. Sent whenever it changes.</summary>
    public sealed class ContainerMessage : Message
    {
        public ContainerKind Kind;
        public List<ItemSlot> Slots = new List<ItemSlot>();

        public override MessageType Type => MessageType.Container;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((byte)Kind);
            w.Write((ushort)Slots.Count);
            foreach (ItemSlot s in Slots)
            {
                w.Write(s.ItemId ?? "");
                w.Write(s.Count);
            }
        }

        internal static ContainerMessage Read(BinaryReader r)
        {
            var m = new ContainerMessage { Kind = (ContainerKind)r.ReadByte() };
            int count = r.ReadUInt16();
            for (int i = 0; i < count; i++) m.Slots.Add(new ItemSlot { ItemId = r.ReadString(), Count = r.ReadInt32() });
            return m;
        }
    }

    /// <summary>The player's stat totals (see <see cref="StatIds"/>). Sent whenever they change.</summary>
    public sealed class StatsMessage : Message
    {
        public Dictionary<string, int> Stats = new Dictionary<string, int>();

        public override MessageType Type => MessageType.Stats;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((byte)Stats.Count);
            foreach (KeyValuePair<string, int> s in Stats)
            {
                w.Write(s.Key);
                w.Write(s.Value);
            }
        }

        internal static StatsMessage Read(BinaryReader r)
        {
            var m = new StatsMessage();
            int count = r.ReadByte();
            for (int i = 0; i < count; i++) m.Stats[r.ReadString()] = r.ReadInt32();
            return m;
        }
    }

    /// <summary>Use one of an item's actions (see <see cref="ItemActions"/>).</summary>
    public sealed class ItemActionMessage : Message
    {
        public ContainerKind Container;
        public int Slot;
        public string ActionId = "";

        public override MessageType Type => MessageType.ItemAction;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((byte)Container);
            w.Write((byte)Slot);
            w.Write(ActionId);
        }

        internal static ItemActionMessage Read(BinaryReader r) =>
            new ItemActionMessage { Container = (ContainerKind)r.ReadByte(), Slot = r.ReadByte(), ActionId = r.ReadString() };
    }

    /// <summary>Rearrange the inventory: swap two slots.</summary>
    public sealed class MoveItemMessage : Message
    {
        public int From;
        public int To;

        public override MessageType Type => MessageType.MoveItem;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((byte)From);
            w.Write((byte)To);
        }

        internal static MoveItemMessage Read(BinaryReader r) => new MoveItemMessage { From = r.ReadByte(), To = r.ReadByte() };
    }

    /// <summary>Use an action on a world object: walk beside it, then do it (e.g. butcher at a rack).</summary>
    public sealed class ObjectActionMessage : Message
    {
        public string ObjectType = "";
        public int Plane;
        public int X;
        public int Y;
        public string ActionId = "";

        public override MessageType Type => MessageType.ObjectAction;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(ObjectType);
            w.Write((byte)Plane);
            w.Write(X);
            w.Write(Y);
            w.Write(ActionId);
        }

        internal static ObjectActionMessage Read(BinaryReader r) =>
            new ObjectActionMessage { ObjectType = r.ReadString(), Plane = r.ReadByte(), X = r.ReadInt32(), Y = r.ReadInt32(), ActionId = r.ReadString() };
    }

    /// <summary>The player's XP in every skill. Sent at login and whenever it changes.</summary>
    public sealed class SkillsMessage : Message
    {
        public Dictionary<string, long> Xp = new Dictionary<string, long>();

        public override MessageType Type => MessageType.Skills;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((byte)Xp.Count);
            foreach (KeyValuePair<string, long> s in Xp)
            {
                w.Write(s.Key);
                w.Write(s.Value);
            }
        }

        internal static SkillsMessage Read(BinaryReader r)
        {
            var m = new SkillsMessage();
            int count = r.ReadByte();
            for (int i = 0; i < count; i++) m.Xp[r.ReadString()] = r.ReadInt64();
            return m;
        }
    }

    /// <summary>Walk to a ground item and pick it up.</summary>
    public sealed class PickupMessage : Message
    {
        public int GroundItemId;

        public override MessageType Type => MessageType.Pickup;
        internal override void WriteBody(BinaryWriter w) => w.Write(GroundItemId);
        internal static PickupMessage Read(BinaryReader r) => new PickupMessage { GroundItemId = r.ReadInt32() };
    }

    public enum TextKind : byte
    {
        Info = 0,
        /// <summary>Something you tried didn't work.</summary>
        Warning = 1,
        /// <summary>The Assayer speaks (on death).</summary>
        Assayer = 2,
    }

    /// <summary>
    /// A named piece of player state as JSON ("achievements", "bestiary", "client"...), so a system
    /// can show the client what it tracks without a message type and codec of its own.
    /// </summary>
    public sealed class JsonStateMessage : Message
    {
        public string Key = "";
        public string Json = "{}";

        public override MessageType Type => MessageType.JsonState;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(Key);
            w.Write(Json);
        }

        internal static JsonStateMessage Read(BinaryReader r) => new JsonStateMessage { Key = r.ReadString(), Json = r.ReadString() };

        public static JsonStateMessage Of(string key, object value) => new JsonStateMessage { Key = key, Json = JsonConvert.SerializeObject(value) };

        public T Parse<T>() where T : class, new() => JsonConvert.DeserializeObject<T>(Json) ?? new T();
    }

    /// <summary>
    /// The client keeps small pieces of its own state on the server (action bar layouts, targets),
    /// so they follow the player between machines. The server stores them as given.
    /// </summary>
    public sealed class SetClientStateMessage : Message
    {
        public const int MaxLength = 16 * 1024;

        public string Key = "";
        public string Json = "";

        public override MessageType Type => MessageType.SetClientState;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(Key);
            w.Write(Json);
        }

        internal static SetClientStateMessage Read(BinaryReader r) => new SetClientStateMessage { Key = r.ReadString(), Json = r.ReadString() };
    }

    /// <summary>Who a chat line is for. Public is everyone nearby; more channels come with friends and groups.</summary>
    public enum ChatChannel : byte
    {
        Public = 0,
    }

    /// <summary>The player says something.</summary>
    public sealed class ChatMessage : Message
    {
        public const int MaxLength = 120;

        public ChatChannel Channel;
        public string Text = "";

        public override MessageType Type => MessageType.Chat;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((byte)Channel);
            w.Write(Text);
        }

        internal static ChatMessage Read(BinaryReader r) => new ChatMessage { Channel = (ChatChannel)r.ReadByte(), Text = r.ReadString() };
    }

    /// <summary>Someone said something. Public lines also appear over the speaker's head.</summary>
    public sealed class ChatLineMessage : Message
    {
        public ChatChannel Channel;
        public int EntityId;
        public string From = "";
        public string Text = "";

        public override MessageType Type => MessageType.ChatLine;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((byte)Channel);
            w.Write(EntityId);
            w.Write(From);
            w.Write(Text);
        }

        internal static ChatLineMessage Read(BinaryReader r) =>
            new ChatLineMessage { Channel = (ChatChannel)r.ReadByte(), EntityId = r.ReadInt32(), From = r.ReadString(), Text = r.ReadString() };
    }

    /// <summary>A line for the player's message log.</summary>
    public sealed class GameTextMessage : Message
    {
        public TextKind Kind;
        public string Text = "";

        public override MessageType Type => MessageType.GameText;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write((byte)Kind);
            w.Write(Text);
        }

        internal static GameTextMessage Read(BinaryReader r) => new GameTextMessage { Kind = (TextKind)r.ReadByte(), Text = r.ReadString() };
    }
}
