using System.Collections.Generic;
using System.IO;
using Rth.Shared.Combat;
using Rth.Shared.World;

namespace Rth.Shared.Net
{
    public static class Protocol
    {
        /// <summary>Bump whenever a message layout changes, so mismatched builds are refused at login.</summary>
        public const int Version = 2;
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
        SetDeflect = 6,

        // Server to client: state.
        LoginOk = 100,
        LoginRejected = 101,
        WorldUpdate = 102,
        PlayerState = 103,
        ContentInfo = 104,
        GameText = 105,
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

    /// <summary>Turn on deflection against one style (spending breath), or off with <see cref="CombatStyle.None"/>.</summary>
    public sealed class SetDeflectMessage : Message
    {
        public CombatStyle Style;

        public override MessageType Type => MessageType.SetDeflect;
        internal override void WriteBody(BinaryWriter w) => w.Write((byte)Style);
        internal static SetDeflectMessage Read(BinaryReader r) => new SetDeflectMessage { Style = (CombatStyle)r.ReadByte() };
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
        /// <summary>Damage from the user's own humours (choleric fever).</summary>
        Fever = 4,
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

        public override MessageType Type => MessageType.WorldUpdate;

        public bool IsEmpty =>
            Added.Count == 0 && Moved.Count == 0 && Removed.Count == 0 && Health.Count == 0 &&
            Hits.Count == 0 && Actions.Count == 0 && Deaths.Count == 0;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(Tick);
            w.Write((ushort)Added.Count);
            foreach (EntityAdded a in Added)
            {
                w.Write(a.Id);
                w.Write((byte)a.Kind);
                w.Write(a.Name);
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

    public struct EffectState
    {
        public EffectType Effect;
        public int Magnitude;
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
        public int Breath;
        public int MaxBreath;
        public CombatStyle Deflect;
        public bool InCombat;
        /// <summary>The entity being fought, or 0.</summary>
        public int TargetId;
        public uint GlobalCooldownReadyTick;
        public string QueuedAbility = "";
        public List<CooldownState> Cooldowns = new List<CooldownState>();
        public List<EffectState> Effects = new List<EffectState>();

        public override MessageType Type => MessageType.PlayerState;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(Tick);
            w.Write(Blood);
            w.Write(MaxBlood);
            w.Write(Phlegm);
            w.Write(Bile);
            w.Write(Breath);
            w.Write(MaxBreath);
            w.Write((byte)Deflect);
            w.Write(InCombat);
            w.Write(TargetId);
            w.Write(GlobalCooldownReadyTick);
            w.Write(QueuedAbility);
            w.Write((byte)Cooldowns.Count);
            foreach (CooldownState c in Cooldowns)
            {
                w.Write(c.AbilityId);
                w.Write(c.ReadyTick);
            }
            w.Write((byte)Effects.Count);
            foreach (EffectState e in Effects)
            {
                w.Write((byte)e.Effect);
                w.Write(e.Magnitude);
                w.Write(e.EndTick);
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
                Breath = r.ReadInt32(),
                MaxBreath = r.ReadInt32(),
                Deflect = (CombatStyle)r.ReadByte(),
                InCombat = r.ReadBoolean(),
                TargetId = r.ReadInt32(),
                GlobalCooldownReadyTick = r.ReadUInt32(),
                QueuedAbility = r.ReadString(),
            };
            int count = r.ReadByte();
            for (int i = 0; i < count; i++) m.Cooldowns.Add(new CooldownState { AbilityId = r.ReadString(), ReadyTick = r.ReadUInt32() });
            count = r.ReadByte();
            for (int i = 0; i < count; i++) m.Effects.Add(new EffectState { Effect = (EffectType)r.ReadByte(), Magnitude = r.ReadInt32(), EndTick = r.ReadUInt32() });
            return m;
        }
    }

    /// <summary>The content the client needs to draw its UI: abilities and the bile thresholds. Sent at login and on hot-reload.</summary>
    public sealed class ContentInfoMessage : Message
    {
        public List<AbilityDef> Abilities = new List<AbilityDef>();
        /// <summary>Ids of the abilities on the player's action bar, in slot order.</summary>
        public List<string> ActionBar = new List<string>();
        public int BileLimit;
        public int BilePassiveThreshold;
        public int BileExtreme;
        public int MaxPhlegm;

        public override MessageType Type => MessageType.ContentInfo;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(BileLimit);
            w.Write(BilePassiveThreshold);
            w.Write(BileExtreme);
            w.Write(MaxPhlegm);
            w.Write((byte)ActionBar.Count);
            foreach (string id in ActionBar) w.Write(id);
            w.Write((ushort)Abilities.Count);
            foreach (AbilityDef a in Abilities)
            {
                w.Write(a.Id);
                w.Write(a.Name);
                w.Write(a.Description);
                w.Write((byte)a.Type);
                w.Write((byte)a.Style);
                w.Write(a.CooldownTicks);
                w.Write(a.Range);
                w.Write(a.DamageMin);
                w.Write(a.DamageMax);
                w.Write(a.PhlegmGain);
                w.Write(a.PhlegmRequired);
                w.Write(a.PhlegmCost);
                w.Write(a.BileShift);
                w.Write(a.BileTowardZero);
                w.Write(a.BileMin);
                w.Write(a.BileMax);
                w.Write(a.HealPercentOfDamage);
                w.Write(a.HealPercentOfMaxBlood);
                w.Write((byte)a.Effect);
                w.Write(a.EffectMagnitude);
                w.Write(a.EffectTicks);
            }
        }

        internal static ContentInfoMessage Read(BinaryReader r)
        {
            var m = new ContentInfoMessage
            {
                BileLimit = r.ReadInt32(),
                BilePassiveThreshold = r.ReadInt32(),
                BileExtreme = r.ReadInt32(),
                MaxPhlegm = r.ReadInt32(),
            };
            int count = r.ReadByte();
            for (int i = 0; i < count; i++) m.ActionBar.Add(r.ReadString());
            count = r.ReadUInt16();
            for (int i = 0; i < count; i++)
            {
                m.Abilities.Add(new AbilityDef
                {
                    Id = r.ReadString(),
                    Name = r.ReadString(),
                    Description = r.ReadString(),
                    Type = (AbilityType)r.ReadByte(),
                    Style = (CombatStyle)r.ReadByte(),
                    CooldownTicks = r.ReadInt32(),
                    Range = r.ReadInt32(),
                    DamageMin = r.ReadInt32(),
                    DamageMax = r.ReadInt32(),
                    PhlegmGain = r.ReadInt32(),
                    PhlegmRequired = r.ReadInt32(),
                    PhlegmCost = r.ReadInt32(),
                    BileShift = r.ReadInt32(),
                    BileTowardZero = r.ReadInt32(),
                    BileMin = r.ReadInt32(),
                    BileMax = r.ReadInt32(),
                    HealPercentOfDamage = r.ReadInt32(),
                    HealPercentOfMaxBlood = r.ReadInt32(),
                    Effect = (EffectType)r.ReadByte(),
                    EffectMagnitude = r.ReadInt32(),
                    EffectTicks = r.ReadInt32(),
                });
            }
            return m;
        }
    }

    public enum TextKind : byte
    {
        Info = 0,
        /// <summary>Something you tried didn't work.</summary>
        Warning = 1,
        /// <summary>The Assayer speaks (on death).</summary>
        Assayer = 2,
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
