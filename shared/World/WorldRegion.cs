namespace Rth.Shared.World
{
    /// <summary>
    /// One authored piece of the world: a zone-aligned rectangle built as one Unity scene and
    /// exported as one map. The server sends the list to clients, which stream the scenes in and
    /// out as the player walks.
    /// </summary>
    /// <summary>How a region is lit: the client eases into it on entering, and back out on leaving.</summary>
    public sealed class RegionAtmosphere
    {
        public float SunIntensity = 1.1f;
        /// <summary>#RRGGBB: ambient light from above, around and below.</summary>
        public string AmbientSky = "#9eadc7";
        public string AmbientEquator = "#808580";
        public string AmbientGround = "#4d473d";
        /// <summary>#RRGGBB: fog and the camera's background.</summary>
        public string Fog = "#a9c4d9";
        public float FogStart = 45f;
        public float FogEnd = 85f;
    }

    public sealed class WorldRegion
    {
        /// <summary>Export name, unique: "tolling".</summary>
        public string Name = "";

        /// <summary>The Unity scene that draws it (a scene name in the build).</summary>
        public string Scene = "";

        /// <summary>Shown to the player on entering: "Tolling".</summary>
        public string Title = "";

        public int X;
        public int Y;
        public int Width;
        public int Height;

        /// <summary>Whether the region has a spawn point for new players.</summary>
        public bool HasSpawn;

        /// <summary>
        /// An instanced place (a boss's arena): built far from the rest of the map and entered by
        /// a door, not walked into, so it isn't drawn on the world map.
        /// </summary>
        public bool Instanced;

        /// <summary>Its music: a track in the client's Resources/Music (with optional layers), or empty for silence.</summary>
        public string Music = "";

        /// <summary>Light, sky and fog of its own (a crypt, a cave), or null for the open world's.</summary>
        public RegionAtmosphere? Atmosphere;

        public bool Contains(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;

        public bool Overlaps(WorldRegion other) =>
            X < other.X + other.Width && other.X < X + Width && Y < other.Y + other.Height && other.Y < Y + Height;

        /// <summary>Chebyshev distance from a tile to the nearest tile of the region (0 inside).</summary>
        public int DistanceTo(int x, int y)
        {
            int dx = x < X ? X - x : x >= X + Width ? x - (X + Width - 1) : 0;
            int dy = y < Y ? Y - y : y >= Y + Height ? y - (Y + Height - 1) : 0;
            return dx > dy ? dx : dy;
        }
    }
}
