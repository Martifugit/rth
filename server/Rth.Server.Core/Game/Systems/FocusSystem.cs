using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Tells each client what its player's interaction is aimed at (the "focus" state: the door,
    /// tree or station they are walking to or working, the NPC they are talking to or fighting), so
    /// the client can mark it until it is done. An interaction says what it is aimed at
    /// (<see cref="Interaction.Focus"/>); nothing, and the mark goes.
    /// </summary>
    public sealed class FocusSystem : GameSystem
    {
        private sealed class SentFocus
        {
            public string? Last;
        }

        public override void SendState(Player player)
        {
            InteractionFocus focus = player.Interaction?.Focus(World) ?? new InteractionFocus();
            JsonStateMessage message = JsonStateMessage.Of(InteractionFocus.StateKey, focus);
            SentFocus sent = player.Component<SentFocus>();
            if (message.Json == sent.Last) return;
            sent.Last = message.Json;
            player.Connection.Send(message);
        }
    }
}
