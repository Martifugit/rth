using System.Collections.Generic;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// What a region's hand-made scene content adds to the map (world/regions/&lt;id&gt;.handmade.json):
    /// its collision as text, and the objects, creatures and spawn placed by hand. Written only by
    /// Sync World, from the scene's Handmade root, so that <see cref="RegionCompiler"/> can build the
    /// region's map files without Unity. Never edited by hand.
    /// </summary>
    public sealed class HandmadeData
    {
        public string Note = "Written by Sync World from the scene's Handmade root. Don't edit: change the scene and sync.";
        public List<LayerText> Planes = new List<LayerText>();
        public List<ObjectEntry> Objects = new List<ObjectEntry>();
        public List<NpcEntry> Npcs = new List<NpcEntry>();
        /// <summary>Where new players appear, [plane, x, y], if the hand-made content says.</summary>
        public int[]? Spawn;
    }

    /// <summary>An interactive object in a region's map files (&lt;id&gt;.objects.json).</summary>
    public sealed class ObjectEntry
    {
        public string Type = "";
        public int Plane;
        public int X;
        public int Y;
        public int? ToPlane;
        public int? ToX;
        public int? ToY;
    }

    /// <summary>A creature's home in a region's map files (&lt;id&gt;.spawns.json).</summary>
    public sealed class NpcEntry
    {
        public string Npc = "";
        public int Plane;
        public int X;
        public int Y;
        public int Wander = -1;
    }
}
