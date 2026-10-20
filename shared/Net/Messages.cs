using System.Collections.Generic;
using System.IO;
using Rth.Shared.World;

namespace Rth.Shared.Net
{
    public static class Protocol
    {
        /// <summary>Bump whenever a message layout changes, so mismatched builds are refused at login.</summary>
        public const int Version = 1;
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

        // Server to client: state.
        LoginOk = 100,
        LoginRejected = 101,
        WorldUpdate = 102,
    }

    public abstract class Message
    {
        public abstract MessageType Type { get; }
        internal abstract void WriteBody(BinaryWriter w);
    }

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

    /// <summary>An entity that came into view (or logged in) this tick, with its current position.</summary>
    public sealed class EntityAdded
    {
        public int Id;
        public string Name = "";
        public int Plane;
        public TileCoord Position;
    }

    /// <summary>An entity in view that moved this tick, with each tile it stepped through in order.</summary>
    public sealed class EntityMoved
    {
        public int Id;
        public bool Running;
        public List<TileCoord> Steps = new List<TileCoord>();
    }

    /// <summary>Everything that changed near one player during one tick. Sent every tick, even when empty.</summary>
    public sealed class WorldUpdateMessage : Message
    {
        public uint Tick;
        public List<EntityAdded> Added = new List<EntityAdded>();
        public List<EntityMoved> Moved = new List<EntityMoved>();
        public List<int> Removed = new List<int>();

        public override MessageType Type => MessageType.WorldUpdate;

        public bool IsEmpty => Added.Count == 0 && Moved.Count == 0 && Removed.Count == 0;

        internal override void WriteBody(BinaryWriter w)
        {
            w.Write(Tick);
            w.Write((ushort)Added.Count);
            foreach (EntityAdded a in Added)
            {
                w.Write(a.Id);
                w.Write(a.Name);
                w.Write((byte)a.Plane);
                WriteTile(w, a.Position);
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
        }

        internal static WorldUpdateMessage Read(BinaryReader r)
        {
            var msg = new WorldUpdateMessage { Tick = r.ReadUInt32() };
            int added = r.ReadUInt16();
            for (int i = 0; i < added; i++)
            {
                msg.Added.Add(new EntityAdded
                {
                    Id = r.ReadInt32(),
                    Name = r.ReadString(),
                    Plane = r.ReadByte(),
                    Position = ReadTile(r),
                });
            }
            int moved = r.ReadUInt16();
            for (int i = 0; i < moved; i++)
            {
                var m = new EntityMoved { Id = r.ReadInt32(), Running = r.ReadBoolean() };
                int steps = r.ReadByte();
                for (int s = 0; s < steps; s++) m.Steps.Add(ReadTile(r));
                msg.Moved.Add(m);
            }
            int removed = r.ReadUInt16();
            for (int i = 0; i < removed; i++) msg.Removed.Add(r.ReadInt32());
            return msg;
        }

        private static void WriteTile(BinaryWriter w, TileCoord t)
        {
            w.Write(t.X);
            w.Write(t.Y);
        }

        private static TileCoord ReadTile(BinaryReader r) => new TileCoord(r.ReadInt32(), r.ReadInt32());
    }
}
