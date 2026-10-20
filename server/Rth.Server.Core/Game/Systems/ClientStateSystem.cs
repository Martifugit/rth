using System.Collections.Generic;
using Rth.Server.Persistence;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    public sealed class PlayerClientState
    {
        public Dictionary<string, string> Values = new Dictionary<string, string>();
    }

    /// <summary>
    /// Keeps small pieces of client state for each player (action bar layouts, skill targets...)
    /// so they follow the player to any machine. The server doesn't interpret them: it stores
    /// what the client sends under a key and hands everything back at login as "client" state.
    /// </summary>
    public sealed class ClientStateSystem : GameSystem
    {
        public const string StateKey = "client";
        private const string SaveSection = "client";
        private const int MaxKeys = 32;
        private const int MaxKeyLength = 64;

        protected override void OnAttach() => World.OnMessage<SetClientStateMessage>(Set);

        public string? Get(Player player, string key) =>
            player.Component<PlayerClientState>().Values.TryGetValue(key, out string? value) ? value : null;

        private void Set(Player player, SetClientStateMessage m)
        {
            Dictionary<string, string> values = player.Component<PlayerClientState>().Values;
            if (string.IsNullOrEmpty(m.Key) || m.Key.Length > MaxKeyLength || m.Json.Length > SetClientStateMessage.MaxLength) return;
            if (string.IsNullOrEmpty(m.Json))
            {
                values.Remove(m.Key);
                return;
            }
            if (!values.ContainsKey(m.Key) && values.Count >= MaxKeys) return;
            values[m.Key] = m.Json;
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            Dictionary<string, string>? saved = save?.Section<Dictionary<string, string>>(SaveSection);
            if (saved != null) player.Component<PlayerClientState>().Values = saved;
            player.Connection.Send(JsonStateMessage.Of(StateKey, player.Component<PlayerClientState>().Values));
        }

        public override void OnSave(Player player, PlayerSave save) =>
            save.SetSection(SaveSection, player.Component<PlayerClientState>().Values);
    }
}
