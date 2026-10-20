using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Content;
using Rth.Server.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Making things at stations, for any skill: the object action "process" picks the first recipe
    /// at that station the player has inputs for, then repeats it every few ticks (consume inputs,
    /// give outputs, roll the output table, award XP) until they run out. Butchering is the first
    /// user; distilling, brewing and cooking only need recipes and stations in data.
    /// </summary>
    public sealed class RecipeSystem : GameSystem
    {
        public const string Process = "process";

        protected override void OnAttach() =>
            World.Get<ObjectSystem>().RegisterAction(Process, (player, placed, def) =>
                World.StartInteraction(player, new ProcessInteraction(placed)));

        private ItemSystem Items => World.Get<ItemSystem>();
        private SkillSystem Skills => World.Get<SkillSystem>();

        public IEnumerable<RecipeDef> RecipesAt(string station) => World.Content.Recipes.Values.Where(r => r.Station == station);

        private bool HasInputs(Player player, RecipeDef recipe) =>
            recipe.Inputs.All(i => Items.CountOf(player, i.Item) >= i.Count);

        private bool HasTool(Player player, string tool)
        {
            PlayerItems items = Items.Of(player);
            return items.Inventory.CountOf(tool) > 0 || items.Equipment.CountOf(tool) > 0;
        }

        /// <summary>
        /// The recipe to do next at this station, or null with <paramref name="problem"/> saying why
        /// not (nothing to work on, level too low, missing tool).
        /// </summary>
        internal RecipeDef? Choose(Player player, string station, out string? problem, out bool nothingToWorkOn)
        {
            problem = null;
            nothingToWorkOn = false;
            List<RecipeDef> candidates = RecipesAt(station).Where(r => HasInputs(player, r)).ToList();
            if (candidates.Count == 0)
            {
                RecipeDef? any = RecipesAt(station).FirstOrDefault();
                problem = "You have nothing to " + (any?.Verb ?? "work on") + " here.";
                nothingToWorkOn = true;
                return null;
            }
            foreach (RecipeDef recipe in candidates)
            {
                if (Skills.Level(player, recipe.Skill) < recipe.Level)
                {
                    problem ??= "You need " + Skills.Name(recipe.Skill).ToLowerInvariant() + " level " + recipe.Level + " to " + recipe.Verb + " the " + recipe.Name + ".";
                    continue;
                }
                string? missing = recipe.Tools.FirstOrDefault(t => !HasTool(player, t));
                if (missing != null)
                {
                    problem ??= "You need a " + (World.Content.Item(missing)?.Name.ToLowerInvariant() ?? missing) + " to " + recipe.Verb + " the " + recipe.Name + ".";
                    continue;
                }
                problem = null;
                return recipe;
            }
            return null;
        }

        /// <summary>Does one completion of a recipe: inputs out, outputs in (or on the ground if full), XP.</summary>
        internal void Complete(Player player, RecipeDef recipe)
        {
            foreach (ItemAmount input in recipe.Inputs) Items.Take(player, input.Item, input.Count);

            var produced = new List<Drop>();
            foreach (ItemAmount output in recipe.Outputs) produced.Add(new Drop(output.Item, output.Count));
            if (recipe.OutputTable != null)
            {
                int rolls = 1;
                if (recipe.ExtraRollEveryLevels > 0)
                    rolls += Math.Max(0, Skills.Level(player, recipe.Skill) - recipe.Level) / recipe.ExtraRollEveryLevels;
                for (int i = 0; i < rolls; i++) produced.AddRange(DropRoller.Roll(World.Content, recipe.OutputTable, World.Random));
            }

            GroundItemSystem? ground = World.Find<GroundItemSystem>();
            foreach (Drop drop in produced)
            {
                int left = Items.Give(player, drop.ItemId, drop.Count);
                if (left > 0 && ground != null) ground.Spawn(drop.ItemId, left, player.Plane, player.Position, player);
            }
            Skills.AddXp(player, recipe.Skill, recipe.Xp);
        }
    }

    /// <summary>Standing at a station, working through recipe after recipe until the inputs run out.</summary>
    public sealed class ProcessInteraction : ObjectInteraction
    {
        private RecipeDef? _current;
        private uint _readyAt;

        public ProcessInteraction(PlacedObject station) : base(station) { }

        protected override bool Work(World world, Player player)
        {
            RecipeSystem recipes = world.Get<RecipeSystem>();
            if (_current == null)
            {
                _current = recipes.Choose(player, Target.Type, out string? problem, out _);
                if (_current == null)
                {
                    world.Tell(player, TextKind.Warning, problem ?? "You can't do anything here.");
                    return false;
                }
                world.Tell(player, TextKind.Info, "You begin to " + _current.Verb + " the " + _current.Name + ".");
                _readyAt = world.CurrentTick + (uint)_current.Ticks;
                return true;
            }

            if (world.CurrentTick < _readyAt) return true;
            recipes.Complete(player, _current);

            // Carry on with whatever is still possible here: the same recipe, or the next one.
            RecipeDef? next = recipes.Choose(player, Target.Type, out string? stop, out bool ranOut);
            if (next == null)
            {
                world.Tell(player, TextKind.Info, ranOut ? "You have nothing left to " + _current.Verb + "." : stop ?? "You stop.");
                return false;
            }
            if (next != _current) world.Tell(player, TextKind.Info, "You begin to " + next.Verb + " the " + next.Name + ".");
            _current = next;
            _readyAt = world.CurrentTick + (uint)next.Ticks;
            return true;
        }
    }
}
