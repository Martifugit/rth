using System.Collections.Generic;
using Rth.Server.Persistence;

namespace Rth.Server.Game
{
    public sealed class PlayerFlags
    {
        public Dictionary<string, string> Values = new Dictionary<string, string>();
    }

    /// <summary>
    /// Named per-player values that persist: "met_corvin" = "1", "quest.missing_survey" = "3".
    /// The general-purpose memory for dialogue and (later) quests, so each new storyline doesn't
    /// need its own save format.
    /// </summary>
    public sealed class FlagSystem : GameSystem
    {
        private const string SaveSection = "flags";

        public string? Get(Player player, string key) =>
            player.Component<PlayerFlags>().Values.TryGetValue(key, out string? value) ? value : null;

        public bool IsSet(Player player, string key) => !string.IsNullOrEmpty(Get(player, key));

        public void Set(Player player, string key, string? value)
        {
            Dictionary<string, string> values = player.Component<PlayerFlags>().Values;
            if (string.IsNullOrEmpty(value)) values.Remove(key);
            else values[key] = value!;
        }

        public override void OnLogin(Player player, PlayerSave? save)
        {
            Dictionary<string, string>? saved = save?.Section<Dictionary<string, string>>(SaveSection);
            if (saved != null) player.Component<PlayerFlags>().Values = saved;
        }

        public override void OnSave(Player player, PlayerSave save) => save.SetSection(SaveSection, player.Component<PlayerFlags>().Values);
    }
}
