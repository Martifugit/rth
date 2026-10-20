namespace Rth.Server.Persistence
{
    /// <summary>Everything about a player that survives logout. Grows with each milestone.</summary>
    public sealed class PlayerSave
    {
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        public string Name = "";
        public int Plane;
        public int X;
        public int Y;

        // Added in version 2. Missing (older saves) means full.
        public int? Blood;
        public int? Breath;
        public int Bile;
        /// <summary>The Assayer keeps count.</summary>
        public int Deaths;
    }

    public interface IPlayerStore
    {
        /// <summary>The saved player for this account key, or null if there is none.</summary>
        PlayerSave? Load(string key);

        void Save(string key, PlayerSave save);
    }
}
