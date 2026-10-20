using System.Collections.Generic;
using Rth.Shared.Skills;

namespace Rth.Server.Content
{
    // ------------------------------------------------------------------ dialogue (dialogues.json)

    /// <summary>
    /// A conversation: a graph of nodes, starting at <see cref="Start"/>. Conditions and actions
    /// are typed by string and handled by whichever system registered that type with the
    /// dialogue system ("hasItem", "openBank", "assignTask"...), so new systems extend dialogue
    /// without the dialogue code knowing about them.
    /// </summary>
    public sealed class DialogueDef
    {
        public string Id = "";
        public string Start = "start";
        public Dictionary<string, DialogueNode> Nodes = new Dictionary<string, DialogueNode>();
    }

    public sealed class DialogueNode
    {
        /// <summary>"npc" (default) or "player".</summary>
        public string Speaker = "npc";
        /// <summary>The line, with {tokens} filled in by registered providers. Empty nodes just route.</summary>
        public string Text = "";
        /// <summary>Run when the node is entered.</summary>
        public List<DialogueAction> Actions = new List<DialogueAction>();
        /// <summary>Checked on entry, in order: the first whose conditions all hold jumps elsewhere.</summary>
        public List<DialogueBranch> Branches = new List<DialogueBranch>();
        /// <summary>Choices; only those whose conditions hold are shown.</summary>
        public List<DialogueOption> Options = new List<DialogueOption>();
        /// <summary>Where "continue" goes when there are no options; null ends the conversation.</summary>
        public string? Next;
    }

    public sealed class DialogueOption
    {
        public string Text = "";
        public List<DialogueCondition> Conditions = new List<DialogueCondition>();
        public List<DialogueAction> Actions = new List<DialogueAction>();
        /// <summary>Null ends the conversation.</summary>
        public string? Next;
    }

    public sealed class DialogueBranch
    {
        public List<DialogueCondition> Conditions = new List<DialogueCondition>();
        public string Next = "";
    }

    /// <summary>A test on the player. Which fields matter depends on <see cref="Type"/>.</summary>
    public sealed class DialogueCondition
    {
        public string Type = "";
        /// <summary>Inverts the result.</summary>
        public bool Not;
        public string? Id;
        public string? Item;
        public int Count = 1;
        public string? Skill;
        public int Level;
        public string? Key;
        public string? Value;
    }

    /// <summary>Something that happens. Which fields matter depends on <see cref="Type"/>.</summary>
    public sealed class DialogueAction
    {
        public string Type = "";
        public string? Id;
        public string? Item;
        public int Count = 1;
        public string? Key;
        public string? Value;
    }

    // ------------------------------------------------------------------ shops (shops.json)

    public sealed class ShopDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>An item id ("marks") or a points pool ("points:yard_tokens").</summary>
        public string Currency = "marks";
        public List<ShopStockDef> Stock = new List<ShopStockDef>();
        /// <summary>Percentage of an item's value paid when players sell to the shop; 0 = doesn't buy.</summary>
        public int SellPercent;
    }

    public sealed class ShopStockDef
    {
        public string Item = "";
        public int Price;
        /// <summary>How many it holds when full; -1 = never runs out.</summary>
        public int Quantity = -1;
        /// <summary>Ticks to restock one unit.</summary>
        public int RestockTicks = 50;
    }

    // ------------------------------------------------------------------ tasks (tasks.json)

    public sealed class PointPoolDef
    {
        public string Id = "";
        public string Name = "";
    }

    /// <summary>Someone who hands out tasks (contracts) and pays in their own points.</summary>
    public sealed class TaskGiverDef
    {
        public string Id = "";
        public string Name = "";
        public string Pool = "";
        public List<SkillRequirement> Requirements = new List<SkillRequirement>();
        public List<TaskDef> Tasks = new List<TaskDef>();
        /// <summary>Every Nth task in a row for this giver pays <see cref="StreakMultiplier"/> times the points.</summary>
        public int StreakEvery = 10;
        public int StreakMultiplier = 3;
        /// <summary>Points it costs to drop a task.</summary>
        public int CancelCost = 5;
    }

    public sealed class TaskDef
    {
        public string Id = "";
        /// <summary>"recipe" (complete a recipe) or "kill" (kill an NPC type). Systems can add more.</summary>
        public string Kind = "recipe";
        /// <summary>The recipe or NPC id.</summary>
        public string Target = "";
        public int Min = 5;
        public int Max = 10;
        public int Weight = 1;
        public int Points = 5;
        /// <summary>"Butcher {count} fen rat carcasses."</summary>
        public string Description = "";
        public List<SkillRequirement> Requirements = new List<SkillRequirement>();
    }

    // ------------------------------------------------------------------ quests (quests.json)

    /// <summary>
    /// A story with stages. Dialogue moves it forward ("startQuest", "setQuestStage",
    /// "completeQuest"); a stage can also have an objective that advances it automatically.
    /// </summary>
    public sealed class QuestDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>Shown in the journal before the quest is started.</summary>
        public string Summary = "";
        public List<SkillRequirement> Requirements = new List<SkillRequirement>();
        public List<QuestStage> Stages = new List<QuestStage>();
        public QuestRewards Rewards = new QuestRewards();
        /// <summary>Journal text once it's done.</summary>
        public string Completed = "";
    }

    public sealed class QuestStage
    {
        /// <summary>Stage number (from 1, ascending in the order they happen).</summary>
        public int Stage;
        /// <summary>What the journal says while at this stage.</summary>
        public string Journal = "";
        public QuestObjective? Objective;
        /// <summary>Stage to move to when the objective is met; null means dialogue moves it on.</summary>
        public int? Next;
    }

    /// <summary>"kill" an NPC type, complete a "recipe", or have an "item" in the pack.</summary>
    public sealed class QuestObjective
    {
        public string Kind = "";
        public string Target = "";
        public int Count = 1;
    }

    public sealed class QuestRewards
    {
        public List<SkillXp> Xp = new List<SkillXp>();
        public List<Rth.Shared.Items.ItemCost> Items = new List<Rth.Shared.Items.ItemCost>();
    }

    public sealed class SkillXp
    {
        public string Skill = "";
        public double Amount;
    }

    public sealed class TasksFile
    {
        public List<PointPoolDef> Pools = new List<PointPoolDef>();
        public List<TaskGiverDef> Givers = new List<TaskGiverDef>();
    }
}
