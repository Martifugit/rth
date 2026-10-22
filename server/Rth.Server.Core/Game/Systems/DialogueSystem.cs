using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Rth.Server.Content;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>Where a player is in a conversation.</summary>
    public sealed class DialogueState
    {
        public DialogueDef? Dialogue;
        public string Node = "";
        public int NpcId;
        /// <summary>Who speaks the "npc" lines of a conversation with no NPC in the world (0 <see cref="NpcId"/>).</summary>
        public string Speaker = "";
        public List<DialogueOption> Visible = new List<DialogueOption>();

        public bool Active => Dialogue != null;
    }

    /// <summary>
    /// Runs conversations from dialogues.json. Three registries make it extensible: conditions
    /// (can this option show / should this branch be taken), actions (what choosing something
    /// does) and text tokens ({task}, {points:yard_tokens}). Each system registers its own, so the
    /// bank adds "openBank" and tasks add "assignTask" without this class knowing either exists.
    /// </summary>
    public sealed class DialogueSystem : GameSystem
    {
        public const string Talk = "talk";
        private const int MaxJumps = 25;
        private static readonly Regex Token = new Regex(@"\{([a-zA-Z]+)(?::([a-zA-Z0-9_]+))?\}");

        private readonly Dictionary<string, Func<Player, DialogueCondition, Npc?, bool>> _conditions = new Dictionary<string, Func<Player, DialogueCondition, Npc?, bool>>();
        private readonly Dictionary<string, Action<Player, DialogueAction, Npc?>> _actions = new Dictionary<string, Action<Player, DialogueAction, Npc?>>();
        private readonly Dictionary<string, Func<Player, string?, Npc?, string>> _tokens = new Dictionary<string, Func<Player, string?, Npc?, string>>();

        public void RegisterCondition(string type, Func<Player, DialogueCondition, Npc?, bool> test) => _conditions[type] = test;
        public void RegisterAction(string type, Action<Player, DialogueAction, Npc?> action) => _actions[type] = action;
        public void RegisterToken(string name, Func<Player, string?, Npc?, string> value) => _tokens[name] = value;

        public IEnumerable<string> ConditionTypes => _conditions.Keys;
        public IEnumerable<string> ActionTypes => _actions.Keys;
        public IEnumerable<string> TokenNames => _tokens.Keys;

        public DialogueState State(Player player) => player.Component<DialogueState>();

        protected override void OnAttach()
        {
            World.OnMessage<DialogueChoiceMessage>((p, m) => Choose(p, m.Option));
            World.Get<NpcSystem>().RegisterAction(Talk, (player, npc) =>
            {
                if (npc.Def.Dialogue != null) Start(player, npc, npc.Def.Dialogue);
            });

            ItemSystem Items() => World.Get<ItemSystem>();
            FlagSystem Flags() => World.Get<FlagSystem>();

            RegisterCondition("hasItem", (p, c, _) => Items().CountOf(p, c.Item ?? "") >= c.Count);
            RegisterCondition("level", (p, c, _) => World.Get<SkillSystem>().Level(p, c.Skill ?? "") >= c.Level);
            RegisterCondition("flag", (p, c, _) => c.Value == null ? Flags().IsSet(p, c.Key ?? "") : Flags().Get(p, c.Key ?? "") == c.Value);

            RegisterAction("giveItem", (p, a, _) => Items().Give(p, a.Item ?? "", a.Count));
            RegisterAction("takeItem", (p, a, _) => Items().Take(p, a.Item ?? "", a.Count));
            RegisterAction("setFlag", (p, a, _) => Flags().Set(p, a.Key ?? "", a.Value ?? "1"));
            RegisterAction("message", (p, a, _) => World.Tell(p, TextKind.Info, Format(p, a.Value ?? "", null)));
            RegisterAction("end", (p, _, _) => End(p));

            RegisterToken("player", (p, _, _) => p.Name);
            RegisterToken("npc", (_, _, npc) => npc?.Name ?? "");
        }

        public void Start(Player player, Npc npc, string dialogueId)
        {
            if (!World.Content.Dialogues.TryGetValue(dialogueId, out DialogueDef? def)) return;
            World.Find<InterfaceSystem>()?.Close(player);
            DialogueState state = State(player);
            state.Dialogue = def;
            state.NpcId = npc.Id;
            state.Speaker = npc.Name;
            Enter(player, state, def.Start, 0);
        }

        /// <summary>
        /// Starts a conversation with someone who isn't an NPC in the world (a voice, a narrator,
        /// the ferryman before his landing exists). It doesn't end when the player walks away, only
        /// when it finishes, the player dies or closes it.
        /// </summary>
        public void Start(Player player, string dialogueId, string speaker)
        {
            if (!World.Content.Dialogues.TryGetValue(dialogueId, out DialogueDef? def)) return;
            World.Find<InterfaceSystem>()?.Close(player);
            DialogueState state = State(player);
            state.Dialogue = def;
            state.NpcId = 0;
            state.Speaker = speaker;
            Enter(player, state, def.Start, 0);
        }

        public void End(Player player)
        {
            DialogueState state = State(player);
            if (!state.Active) return;
            state.Dialogue = null;
            state.Visible.Clear();
            player.Connection.Send(new DialogueMessage { Open = false });
        }

        private Npc? NpcOf(DialogueState state) => World.FindEntity(state.NpcId) as Npc;

        private void Enter(Player player, DialogueState state, string nodeId, int jumps)
        {
            DialogueDef? def = state.Dialogue;
            if (def == null) return;
            if (jumps > MaxJumps || !def.Nodes.TryGetValue(nodeId, out DialogueNode? node))
            {
                World.Log("Dialogue '" + def.Id + "' went to '" + nodeId + "' and stopped.");
                End(player);
                return;
            }
            Npc? npc = NpcOf(state);
            if (npc != null) npc.BusyUntilTick = World.CurrentTick + 20;
            state.Node = nodeId;

            foreach (DialogueAction action in node.Actions)
            {
                Run(player, action, npc);
                if (!state.Active || state.Dialogue != def) return; // an action ended it or opened a window
            }

            foreach (DialogueBranch branch in node.Branches)
            {
                if (!branch.Conditions.All(c => Test(player, c, npc))) continue;
                Enter(player, state, branch.Next, jumps + 1);
                return;
            }

            if (string.IsNullOrEmpty(node.Text))
            {
                if (node.Next != null) Enter(player, state, node.Next, jumps + 1);
                else End(player);
                return;
            }

            state.Visible = node.Options.Where(o => o.Conditions.All(c => Test(player, c, npc))).ToList();
            player.Connection.Send(new DialogueMessage
            {
                Speaker = node.Speaker == "player" ? player.Name : npc?.Name ?? state.Speaker,
                Text = Format(player, node.Text, npc),
                Options = state.Visible.Select(o => Format(player, o.Text, npc)).ToList(),
            });
        }

        private void Choose(Player player, int option)
        {
            DialogueState state = State(player);
            DialogueDef? def = state.Dialogue;
            if (def == null || !def.Nodes.TryGetValue(state.Node, out DialogueNode? node)) return;
            Npc? npc = NpcOf(state);

            if (state.Visible.Count == 0)
            {
                if (node.Next != null) Enter(player, state, node.Next, 0);
                else End(player);
                return;
            }
            if (option < 0 || option >= state.Visible.Count) return;

            DialogueOption chosen = state.Visible[option];
            foreach (DialogueAction action in chosen.Actions)
            {
                Run(player, action, npc);
                if (!state.Active || state.Dialogue != def) return;
            }
            if (chosen.Next != null) Enter(player, state, chosen.Next, 0);
            else End(player);
        }

        /// <summary>Whether a condition holds, through its registered handler (also used by item uses).</summary>
        public bool Test(Player player, DialogueCondition condition, Npc? npc)
        {
            if (!_conditions.TryGetValue(condition.Type, out var test))
            {
                World.Log("No handler for dialogue condition '" + condition.Type + "'.");
                return false;
            }
            return test(player, condition, npc) != condition.Not;
        }

        /// <summary>Does an action through its registered handler (also used by item uses).</summary>
        public void Run(Player player, DialogueAction action, Npc? npc)
        {
            if (_actions.TryGetValue(action.Type, out var run)) run(player, action, npc);
            else World.Log("No handler for dialogue action '" + action.Type + "'.");
        }

        /// <summary>Fills {token} and {token:arg} from the registered providers; unknown tokens stay as they are.</summary>
        public string Format(Player player, string text, Npc? npc) =>
            Token.Replace(text, m => _tokens.TryGetValue(m.Groups[1].Value, out var value)
                ? value(player, m.Groups[2].Success ? m.Groups[2].Value : null, npc)
                : m.Value);

        public override void AfterPlayer(Player player)
        {
            DialogueState state = State(player);
            if (!state.Active) return;
            if (state.NpcId == 0)
            {
                if (player.IsDead) End(player);
                return;
            }
            Npc? npc = NpcOf(state);
            // Wandering off (or the NPC vanishing) ends the conversation.
            if (player.IsDead || npc == null || npc.IsDead || npc.Plane != player.Plane || npc.Position.ChebyshevDistance(player.Position) > 2)
                End(player);
        }
    }
}
