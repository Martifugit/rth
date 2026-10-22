using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Rth.Server.Persistence
{
    /// <summary>Everything about a player that survives logout. Grows with each milestone.</summary>
    public sealed class PlayerSave
    {
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        public string Name = "";
        /// <summary>The region the position is in, for reference. Positions are absolute world tiles.</summary>
        public string? Map;
        public int Plane;
        public int X;
        public int Y;

        // Added in version 2. Missing (older saves) means full.
        public int? Blood;
        public int Bile;
        /// <summary>The ferryman keeps count.</summary>
        public int Deaths;

        /// <summary>
        /// Per-system data, keyed by system ("items", later "skills", "quests"...). Each system reads
        /// and writes its own section, so adding one never changes this class. Sections a running
        /// server doesn't recognise are kept as they are.
        /// </summary>
        public Dictionary<string, JToken> Sections = new Dictionary<string, JToken>();

        public T? Section<T>(string key) where T : class =>
            Sections.TryGetValue(key, out JToken? token) ? token.ToObject<T>() : null;

        public void SetSection(string key, object value) => Sections[key] = JToken.FromObject(value);
    }

    public interface IPlayerStore
    {
        /// <summary>The saved player for this account key, or null if there is none.</summary>
        PlayerSave? Load(string key);

        void Save(string key, PlayerSave save);
    }
}
