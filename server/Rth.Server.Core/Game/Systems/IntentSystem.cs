using System;
using System.Collections.Generic;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>
    /// General client intents (<see cref="IntentMessage"/>): a kind and a short argument, handled by
    /// whichever system registered the kind with <see cref="Register"/> ("make" picks a recipe and
    /// an amount in the make window). A new intent is a registration, not a new message.
    /// </summary>
    public sealed class IntentSystem : GameSystem
    {
        private readonly Dictionary<string, Action<Player, string>> _handlers = new Dictionary<string, Action<Player, string>>();

        protected override void OnAttach() => World.OnMessage<IntentMessage>((player, message) => Handle(player, message.Kind, message.Arg));

        /// <summary>Handles intents of a kind. A later registration replaces an earlier one.</summary>
        public void Register(string kind, Action<Player, string> handler) => _handlers[kind] = handler;

        /// <summary>Acts on an intent as if <paramref name="player"/> had sent it (also used by tests).</summary>
        public void Handle(Player player, string kind, string arg)
        {
            if (kind == null || arg == null || kind.Length > IntentMessage.MaxLength || arg.Length > IntentMessage.MaxLength) return;
            if (_handlers.TryGetValue(kind, out Action<Player, string>? handler)) handler(player, arg);
            else World.Log("Unknown intent '" + kind + "' from " + player.Name + ".");
        }
    }
}
