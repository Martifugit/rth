namespace Rth.Server.Persistence
{
    /// <summary>Everything about a player that survives logout. Grows with each milestone.</summary>
    public sealed class PlayerSave
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public string Name = "";
        public int Plane;
        public int X;
        public int Y;
    }

    public interface IPlayerStore
    {
        /// <summary>The saved player for this account key, or null if there is none.</summary>
        PlayerSave? Load(string key);

        void Save(string key, PlayerSave save);
    }
}
