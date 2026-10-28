using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Server.Content;
using Rth.Server.Items;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Server.Game
{
    /// <summary>
    /// Making things, for any skill (docs/skills.md 2.4). The object action "process" at a station,
    /// or an item's own process action (a grimy herb's "Clean"), offers the recipes there: one the
    /// pack can make is started at once, several open the make window ("make" server state), where
    /// the player picks one and an amount (the "make" intent). A recipe then repeats every few ticks
    /// (inputs out, outputs in, the output table, XP) until the amount is made or the inputs run
    /// out. A recipe can go wrong (food burns) by level and station; a station can make recipes
    /// faster (its tick scale and its draw, see <see cref="StationSystem"/>).
    /// </summary>
    public sealed class RecipeSystem : GameSystem
    {
        public const string Process = "process";

        /// <summary>The make window a player has open, and where they stood when it opened.</summary>
        private sealed class MakeWindowOf
        {
            public MakeState State = new MakeState();
            public PlacedObject? Station;
            public TileCoord OpenedAt;
            public string? LastSent;
        }

        private int _serial;

        protected override void OnAttach()
        {
            World.Get<ObjectSystem>().RegisterAction(Process, (player, placed, def) =>
                World.StartInteraction(player, new ObjectOnceInteraction(placed, p => Offer(p, placed, null))));
            World.Get<ItemSystem>().RegisterAction(ItemActions.Process, (player, use) => OfferItem(player, use.Def));
            IntentSystem? intents = World.Find<IntentSystem>();
            intents?.Register(MakeState.Intent, Make);
            intents?.Register(MakeState.CloseIntent, (player, _) => CloseMake(player));
        }

        private ItemSystem Items => World.Get<ItemSystem>();
        private SkillSystem Skills => World.Get<SkillSystem>();

        public IEnumerable<RecipeDef> RecipesAt(string station) => World.Content.Recipes.Values.Where(r => r.Station == station);

        /// <summary>How many times the pack holds this recipe's inputs.</summary>
        public int Makeable(Player player, RecipeDef recipe) =>
            recipe.Inputs.Count == 0 ? 0 : recipe.Inputs.Min(i => Items.CountOf(player, i.Item) / Math.Max(1, i.Count));

        private bool HasInputs(Player player, RecipeDef recipe) => Makeable(player, recipe) > 0;

        /// <summary>Whether the player carries or wears a tool: an item id, or "tag:x" for any item with that tag.</summary>
        private bool HasTool(Player player, string tool)
        {
            PlayerItems items = Items.Of(player);
            if (!tool.StartsWith("tag:", StringComparison.Ordinal)) return items.Inventory.CountOf(tool) > 0 || items.Equipment.CountOf(tool) > 0;
            string tag = tool.Substring(4);
            foreach (ItemContainer container in new[] { items.Inventory, items.Equipment })
                for (int i = 0; i < container.Size; i++)
                    if (!container.IsEmpty(i) && World.Content.Item(container[i].ItemId)?.Tags.Contains(tag) == true) return true;
            return false;
        }

        private string ToolName(string tool) =>
            tool.StartsWith("tag:", StringComparison.Ordinal) ? tool.Substring(4) : World.Content.Item(tool)?.Name.ToLowerInvariant() ?? tool;

        /// <summary>Why the player can't make this recipe even with its inputs (level, a missing tool), or null.</summary>
        internal string? Blocker(Player player, RecipeDef recipe)
        {
            if (Skills.Level(player, recipe.Skill) < recipe.Level)
                return "You need " + Skills.Name(recipe.Skill).ToLowerInvariant() + " level " + recipe.Level + " to " + recipe.Verb + " the " + recipe.Name + ".";
            string? missing = recipe.Tools.FirstOrDefault(t => !HasTool(player, t));
            if (missing != null)
                return "You need a " + ToolName(missing) + " to " + recipe.Verb + " the " + recipe.Name + ".";
            return null;
        }

        internal bool CanMake(Player player, RecipeDef recipe, out string? problem, out bool ranOut)
        {
            ranOut = !HasInputs(player, recipe);
            problem = ranOut ? "You have nothing to " + recipe.Verb + "." : Blocker(player, recipe);
            return problem == null;
        }

        /// <summary>The chance one make goes wrong for this player here (the recipe's failure, scaled by the station).</summary>
        public double FailChance(Player player, RecipeDef recipe, PlacedObject? station)
        {
            double chance = recipe.FailChance(Skills.Level(player, recipe.Skill));
            StationDef? def = station == null ? null : StationOf(station);
            return Math.Max(0, Math.Min(1, chance * (def?.FailScale ?? 1)));
        }

        private StationDef? StationOf(PlacedObject placed) =>
            World.Content.Objects.TryGetValue(placed.Type, out ObjectDef? def) ? def.Station : null;

        /// <summary>The ticks one make takes here: the recipe's, scaled by the station and by its draw while it roars.</summary>
        public int TicksFor(RecipeDef recipe, PlacedObject? station)
        {
            double scale = 1;
            if (station != null)
            {
                scale *= StationOf(station)?.TickScale ?? 1;
                scale *= World.Find<StationSystem>()?.DrawScale(station) ?? 1;
            }
            return Math.Max(1, (int)Math.Round(recipe.Ticks * scale));
        }

        // ------------------------------------------------------------------ offering recipes

        /// <summary>
        /// Offers what can be made at a station (only the recipes taking <paramref name="item"/>, if
        /// given: ore used on a furnace): starts the one the pack can make, or opens the make window.
        /// </summary>
        public void Offer(Player player, PlacedObject station, string? item)
        {
            if (!World.Content.Objects.TryGetValue(station.Type, out ObjectDef? def)) return;
            List<RecipeDef> recipes = RecipesAt(station.Type).Where(r => item == null || r.Inputs.Any(i => i.Item == item)).ToList();
            OfferRecipes(player, recipes, station, def.Name, (recipes.FirstOrDefault()?.Verb ?? "work on"));
        }

        /// <summary>Offers the recipes with no station that start with this item (cleaning a herb).</summary>
        public void OfferItem(Player player, ItemDef item)
        {
            List<RecipeDef> recipes = World.Content.Recipes.Values
                .Where(r => r.Station.Length == 0 && r.Inputs.Count > 0 && r.Inputs[0].Item == item.Id).ToList();
            OfferRecipes(player, recipes, null, item.Process?.Verb ?? "Make", recipes.FirstOrDefault()?.Verb ?? "make");
        }

        private void OfferRecipes(Player player, List<RecipeDef> recipes, PlacedObject? station, string title, string verb)
        {
            CloseMake(player);
            if (recipes.Count == 0)
            {
                World.Tell(player, TextKind.Info, "You can't make anything here.");
                return;
            }
            List<RecipeDef> withInputs = recipes.Where(r => HasInputs(player, r)).ToList();
            if (withInputs.Count == 0)
            {
                // Nothing to work on: open the window anyway when there is a choice, so the player can see what it takes.
                if (recipes.Count > 1) OpenMake(player, recipes, station, title, verb);
                else World.Tell(player, TextKind.Info, "You have nothing to " + verb + " here.");
                return;
            }
            // Straight to work when only one recipe here touches what the player carries (a carcass
            // at the rack); a choice (bars at the anvil: a dagger or a sword?) opens the window.
            int touched = recipes.Count(r => r.Inputs.Any(i => Items.CountOf(player, i.Item) > 0));
            if (withInputs.Count == 1 && touched == 1)
            {
                RecipeDef only = withInputs[0];
                string? blocked = Blocker(player, only);
                if (blocked != null) World.Tell(player, TextKind.Warning, blocked);
                else Start(player, only, station, 0);
                return;
            }
            OpenMake(player, recipes, station, title, verb);
        }

        private void OpenMake(Player player, List<RecipeDef> recipes, PlacedObject? station, string title, string verb)
        {
            MakeWindowOf open = player.Component<MakeWindowOf>();
            open.Station = station;
            open.OpenedAt = player.Position;
            open.State = new MakeState
            {
                Serial = ++_serial,
                Title = title,
                Verb = verb,
                Station = station?.Type ?? "",
                Plane = station?.Plane ?? player.Plane,
                X = station?.X ?? 0,
                Y = station?.Y ?? 0,
                Options = recipes.OrderBy(r => r.Level).ThenBy(r => r.Group).Select(r => Option(player, r, station)).ToList(),
            };
        }

        private MakeOption Option(Player player, RecipeDef r, PlacedObject? station)
        {
            ItemAmount? shown = r.Outputs.FirstOrDefault() ?? r.Inputs.FirstOrDefault();
            return new MakeOption
            {
                Recipe = r.Id,
                Name = r.Name,
                Group = r.Group,
                Item = shown?.Item ?? "",
                Count = r.Outputs.Count > 0 ? r.Outputs[0].Count : 1,
                Skill = r.Skill,
                Level = r.Level,
                Xp = r.Xp,
                Inputs = r.Inputs.Select(i => new ItemCost { Item = i.Item, Count = i.Count }).ToList(),
                Makeable = Makeable(player, r),
                Problem = Blocker(player, r),
                FailChance = FailChance(player, r, station),
            };
        }

        /// <summary>The make window this player has open (closed: no options).</summary>
        public MakeState MakeWindow(Player player) => player.Component<MakeWindowOf>().State;

        public void CloseMake(Player player)
        {
            MakeWindowOf open = player.Component<MakeWindowOf>();
            if (open.State.IsOpen) open.State = new MakeState();
            open.Station = null;
        }

        /// <summary>The "make" intent: "recipe|count" (count 0, or none, for as many as the pack allows).</summary>
        private void Make(Player player, string arg)
        {
            MakeWindowOf open = player.Component<MakeWindowOf>();
            string[] parts = arg.Split('|');
            string id = parts[0];
            int count = parts.Length > 1 && int.TryParse(parts[1], out int n) ? Math.Max(0, n) : 0;
            if (!open.State.IsOpen || !open.State.Options.Any(o => o.Recipe == id) || !World.Content.Recipes.TryGetValue(id, out RecipeDef? recipe)) return;
            PlacedObject? station = open.Station;
            CloseMake(player);
            if (!CanMake(player, recipe, out string? problem, out _))
            {
                World.Tell(player, TextKind.Warning, problem ?? "You can't make that.");
                return;
            }
            Start(player, recipe, station, count);
        }

        /// <summary>Starts making a recipe <paramref name="count"/> times (0: until the inputs run out), at a station or where the player stands.</summary>
        public void Start(Player player, RecipeDef recipe, PlacedObject? station, int count)
        {
            if (station != null) World.StartInteraction(player, new ProcessInteraction(station, recipe, count));
            else World.StartInteraction(player, new RepeatRecipeInteraction(recipe, null, count));
        }

        /// <summary>
        /// Makes one recipe over and over where the player stands (facing <paramref name="face"/>),
        /// until its inputs run out or they walk away: a use (a carcass on a rack, a knife on a
        /// carcass) names the recipe rather than leaving the station to choose.
        /// </summary>
        public void Repeat(Player player, RecipeDef recipe, TileCoord? face = null) =>
            World.StartInteraction(player, new RepeatRecipeInteraction(recipe, face, 0));

        // ------------------------------------------------------------------ making one

        /// <summary>
        /// Does one completion of a recipe: inputs out, then either outputs in (or on the ground if
        /// the pack is full) and XP, or, if it goes wrong, the failure's outputs and XP.
        /// </summary>
        internal void Complete(Player player, RecipeDef recipe, PlacedObject? station = null)
        {
            foreach (ItemAmount input in recipe.Inputs) Items.Take(player, input.Item, input.Count);

            bool failed = recipe.Failure != null && World.Random.NextDouble() < FailChance(player, recipe, station);
            var produced = new List<Drop>();
            if (failed)
            {
                foreach (ItemAmount output in recipe.Failure!.Outputs) produced.Add(new Drop(output.Item, output.Count));
            }
            else
            {
                foreach (ItemAmount output in recipe.Outputs) produced.Add(new Drop(output.Item, output.Count));
                if (recipe.OutputTable != null)
                {
                    int rolls = 1;
                    if (recipe.ExtraRollEveryLevels > 0)
                        rolls += Math.Max(0, Skills.Level(player, recipe.Skill) - recipe.Level) / recipe.ExtraRollEveryLevels;
                    for (int i = 0; i < rolls; i++) produced.AddRange(DropRoller.Roll(World.Content, recipe.OutputTable, World.Random));
                }
            }

            GroundItemSystem? ground = World.Find<GroundItemSystem>();
            foreach (Drop drop in produced)
            {
                int left = Items.Give(player, drop.ItemId, drop.Count);
                if (left > 0 && ground != null) ground.Spawn(drop.ItemId, left, player.Plane, player.Position, player);
            }
            if (failed)
            {
                Skills.AddXp(player, recipe.Skill, recipe.Failure!.Xp);
                World.Tell(player, TextKind.Info, recipe.Failure.Message.Replace("{name}", recipe.Name));
                World.Events.Publish(new RecipeFailed(player, recipe));
                return;
            }
            Skills.AddXp(player, recipe.Skill, recipe.Xp);
            if (produced.Count > 0) World.Events.Publish(new ItemsObtained(player, "recipe:" + recipe.Id, produced));
            World.Events.Publish(new RecipeCompleted(player, recipe));
        }

        // ------------------------------------------------------------------ what clients see

        public override void SendState(Player player)
        {
            MakeWindowOf open = player.Component<MakeWindowOf>();
            // Walking off (or starting something else) closes the window.
            if (open.State.IsOpen && (player.Position != open.OpenedAt || player.Interaction != null)) CloseMake(player);
            if (open.State.IsOpen)
                foreach (MakeOption option in open.State.Options)
                    if (World.Content.Recipes.TryGetValue(option.Recipe, out RecipeDef? r))
                    {
                        option.Makeable = Makeable(player, r);
                        option.Problem = Blocker(player, r);
                    }
            JsonStateMessage message = JsonStateMessage.Of(MakeState.StateKey, open.State);
            if (message.Json == open.LastSent) return;
            open.LastSent = message.Json;
            player.Connection.Send(message);
        }
    }

    /// <summary>A recipe that went wrong (food burnt); <see cref="RecipeCompleted"/> is only for the ones that worked.</summary>
    public sealed class RecipeFailed
    {
        public RecipeFailed(Player player, RecipeDef recipe)
        {
            Player = player;
            Recipe = recipe;
        }

        public Player Player { get; }
        public RecipeDef Recipe { get; }
    }

    /// <summary>Standing at a station, making one recipe until the amount is made or the inputs run out.</summary>
    public sealed class ProcessInteraction : ObjectInteraction
    {
        private readonly RecipeDef _recipe;
        private int _left;
        private bool _started;
        private uint _readyAt;

        /// <param name="count">How many to make; 0 for as many as the inputs allow.</param>
        public ProcessInteraction(PlacedObject station, RecipeDef recipe, int count) : base(station)
        {
            _recipe = recipe;
            _left = count;
        }

        protected override bool Work(World world, Player player)
        {
            RecipeSystem recipes = world.Get<RecipeSystem>();
            if (!_started)
            {
                if (!recipes.CanMake(player, _recipe, out string? problem, out _))
                {
                    world.Tell(player, TextKind.Warning, problem ?? "You can't do anything here.");
                    return false;
                }
                _started = true;
                world.Tell(player, TextKind.Info, "You begin to " + _recipe.Verb + " the " + _recipe.Name + ".");
                _readyAt = world.CurrentTick + (uint)recipes.TicksFor(_recipe, Object);
                world.Animate(player, _recipe.Animation, Object.Tile);
                return true;
            }

            if (world.CurrentTick < _readyAt) return true;
            recipes.Complete(player, _recipe, Object);
            if (_left > 0 && --_left == 0) return false;
            if (!recipes.CanMake(player, _recipe, out string? stop, out bool ranOut))
            {
                world.Tell(player, TextKind.Info, ranOut ? "You have nothing left to " + _recipe.Verb + "." : stop ?? "You stop.");
                return false;
            }
            _readyAt = world.CurrentTick + (uint)recipes.TicksFor(_recipe, Object);
            world.Animate(player, _recipe.Animation, Object.Tile);
            return true;
        }
    }

    /// <summary>Making one named recipe again and again where the player stands (see <see cref="RecipeSystem.Repeat"/>).</summary>
    public sealed class RepeatRecipeInteraction : Interaction
    {
        private readonly RecipeDef _recipe;
        private readonly TileCoord? _face;
        private int _left;
        private bool _started;
        private uint _readyAt;

        /// <param name="count">How many to make; 0 for as many as the inputs allow.</param>
        public RepeatRecipeInteraction(RecipeDef recipe, TileCoord? face, int count = 0)
        {
            _recipe = recipe;
            _face = face;
            _left = count;
        }

        public override bool Tick(World world, Player player)
        {
            RecipeSystem recipes = world.Get<RecipeSystem>();
            if (!_started)
            {
                player.Waypoints.Clear();
                if (!recipes.CanMake(player, _recipe, out string? problem, out _))
                {
                    world.Tell(player, TextKind.Warning, problem ?? "You can't do that.");
                    return false;
                }
                _started = true;
                world.Tell(player, TextKind.Info, "You begin to " + _recipe.Verb + " the " + _recipe.Name + ".");
                _readyAt = world.CurrentTick + (uint)_recipe.Ticks;
                world.Animate(player, _recipe.Animation, _face);
                return true;
            }

            if (world.CurrentTick < _readyAt) return true;
            recipes.Complete(player, _recipe);
            if (_left > 0 && --_left == 0) return false;
            if (!recipes.CanMake(player, _recipe, out string? stop, out bool ranOut))
            {
                world.Tell(player, TextKind.Info, ranOut ? "You have nothing left to " + _recipe.Verb + "." : stop ?? "You stop.");
                return false;
            }
            _readyAt = world.CurrentTick + (uint)_recipe.Ticks;
            world.Animate(player, _recipe.Animation, _face);
            return true;
        }
    }
}
