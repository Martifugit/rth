using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>What a query handler gives back: a value to send as JSON, or why there's none.</summary>
    public readonly struct QueryAnswer
    {
        private QueryAnswer(object? value, string problem)
        {
            Value = value;
            Problem = problem;
        }

        public object? Value { get; }
        public string Problem { get; }

        public static QueryAnswer Of(object value) => new QueryAnswer(value, "");
        public static QueryAnswer No(string problem) => new QueryAnswer(null, string.IsNullOrEmpty(problem) ? "Nothing to show." : problem);
    }

    /// <summary>
    /// Questions the client asks about things it doesn't keep ("inspect" a player, later a price
    /// check or the items kept on death). Each kind is answered by a handler another system
    /// registers with <see cref="Register"/>; the answer goes back to the asker alone as a
    /// <see cref="QueryResultMessage"/>. Only public information belongs in an answer.
    /// </summary>
    public sealed class QuerySystem : GameSystem
    {
        private readonly Dictionary<string, Func<Player, string, QueryAnswer>> _handlers = new Dictionary<string, Func<Player, string, QueryAnswer>>();

        protected override void OnAttach() => World.OnMessage<QueryMessage>(Handle);

        /// <summary>Answers queries of a kind. A later registration replaces an earlier one.</summary>
        public void Register(string kind, Func<Player, string, QueryAnswer> handler) => _handlers[kind] = handler;

        /// <summary>Asks a query as <paramref name="asker"/> would (also used by tests).</summary>
        public QueryResultMessage Answer(Player asker, string kind, string arg)
        {
            var result = new QueryResultMessage { Kind = kind ?? "", Arg = arg ?? "" };
            if (result.Kind.Length > QueryMessage.MaxLength || result.Arg.Length > QueryMessage.MaxLength || !_handlers.TryGetValue(result.Kind, out Func<Player, string, QueryAnswer>? handler))
            {
                result.Problem = "Nothing to show.";
                return result;
            }
            QueryAnswer answer = handler(asker, result.Arg);
            if (answer.Value == null) result.Problem = answer.Problem;
            else result.Json = JsonConvert.SerializeObject(answer.Value);
            return result;
        }

        private void Handle(Player player, QueryMessage message) => player.Connection.Send(Answer(player, message.Kind, message.Arg));
    }
}
