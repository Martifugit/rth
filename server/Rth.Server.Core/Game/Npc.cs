using System;
using Rth.Server.Content;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    public sealed class Npc : Entity
    {
        public Npc(int id, NpcDef def, NpcSpawn spawn) : base(id, def.Name)
        {
            Def = def;
            Spawn = spawn;
            Home = new TileCoord(spawn.X, spawn.Y);
            Plane = spawn.Plane;
            Position = Home;
            MaxBlood = def.Blood;
            Blood = def.Blood;
        }

        public override EntityKind Kind => EntityKind.Npc;

        /// <summary>Swapped on content hot-reload.</summary>
        public NpcDef Def { get; set; }
        public NpcSpawn Spawn { get; }
        public TileCoord Home { get; }
        public int WanderRadius => Spawn.Wander >= 0 ? Spawn.Wander : Def.WanderRadius;

        /// <summary>Dead and gone from the world until <see cref="RespawnAtTick"/>.</summary>
        public bool Despawned { get; set; }
        public uint RespawnAtTick { get; set; }
        public uint NextWanderTick { get; set; }
        /// <summary>Leashed: walking home and ignoring everyone until it gets there.</summary>
        public bool Returning { get; set; }

        /// <summary>Holds still (no wandering) until this tick, e.g. while someone talks to it.</summary>
        public uint BusyUntilTick { get; set; }

        public override EntityAdded Describe() => new EntityAdded
        {
            Id = Id,
            Kind = EntityKind.Npc,
            Name = Name,
            TypeId = Def.Id,
            Plane = Plane,
            Position = Position,
            Shape = Def.Shape,
            Color = Def.ColorRgb,
            Scale = (byte)Math.Max(1, Math.Min(255, (int)Math.Round(Def.Scale * 10))),
            HealthPercent = HealthPercent,
            Held = Def.Held ?? "",
        };
    }
}
