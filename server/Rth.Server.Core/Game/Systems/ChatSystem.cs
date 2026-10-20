using System.Linq;
using System.Text;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>
    /// Players talking. Public chat reaches everyone who can see the speaker (and appears over
    /// their head on the client). Private messages, friends and group chat become further
    /// <see cref="ChatChannel"/>s handled here.
    /// </summary>
    public sealed class ChatSystem : GameSystem
    {
        protected override void OnAttach() => World.OnMessage<ChatMessage>(Handle);

        private void Handle(Player player, ChatMessage message)
        {
            string text = Clean(message.Text);
            if (text.Length == 0) return;
            if (message.Channel != ChatChannel.Public) return;

            var line = new ChatLineMessage { Channel = ChatChannel.Public, EntityId = player.Id, From = player.Name, Text = text };
            foreach (Player listener in World.Players.ToList())
                if (World.CanSee(listener, player)) listener.Connection.Send(line);
        }

        /// <summary>Printable characters only, single spaces, trimmed, and not too long.</summary>
        public static string Clean(string text)
        {
            var sb = new StringBuilder();
            bool space = false;
            foreach (char c in text ?? "")
            {
                if (char.IsWhiteSpace(c))
                {
                    space = sb.Length > 0;
                    continue;
                }
                if (char.IsControl(c)) continue;
                if (space) sb.Append(' ');
                space = false;
                sb.Append(c);
                if (sb.Length >= ChatMessage.MaxLength) break;
            }
            return sb.ToString();
        }
    }
}
