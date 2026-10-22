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
    /// "Use" an item on something (RuneScape's item-on-object, item-on-NPC and item-on-item). The
    /// client picks a pack item and clicks a target; the player walks beside an object or NPC (an
    /// interaction, so walking away cancels it) and the server looks the pair up in uses.json. What
    /// a use does is data built from existing mechanisms: dialogue conditions and actions, effect
    /// ops, a recipe, a conversation. Anything unlisted says "Nothing interesting happens."
    /// Quests and achievements count a use by its id (kind "use"), through <see cref="ItemUsedOn"/>.
    /// </summary>
    public sealed class UseSystem : GameSystem
    {
        public const string Nothing = "Nothing interesting happens.";

        private ItemSystem Items => World.Get<ItemSystem>();

        protected override void OnAttach()
        {
            World.OnMessage<UseItemMessage>(Handle);
            // "Use" only picks the item up on the client; as an item action it does nothing.
            Items.RegisterAction(ItemActions.Use, (_, _) => { });
        }

        private void Handle(Player player, UseItemMessage m)
        {
            if (player.IsDead) return;
            ItemDef? item = PackItem(player, m.Slot, m.ItemId);
            if (item == null) return;

            switch (m.Target)
            {
                case UseTarget.Object:
                {
                    PlacedObject? placed = World.Get<ObjectSystem>().At(m.Plane, new TileCoord(m.X, m.Y), m.ObjectType);
                    if (placed == null || !World.CanSee(player, placed.Plane, placed.Tile)) return;
                    if (!World.Content.Objects.TryGetValue(placed.Type, out ObjectDef? def)) return;
                    World.StartInteraction(player, new ObjectOnceInteraction(placed, p => OnObject(p, item, placed, def)));
                    break;
                }
                case UseTarget.Npc:
                {
                    if (!(World.FindEntity(m.NpcId) is Npc npc) || npc.IsDead || !World.CanSee(player, npc)) return;
                    World.StartInteraction(player, new NpcInteraction(npc.Id, (p, n) => OnNpc(p, item, n)));
                    break;
                }
                case UseTarget.Item:
                {
                    if (m.TargetSlot == m.Slot) return;
                    ItemDef? other = PackItem(player, m.TargetSlot, m.TargetItemId);
                    if (other != null) OnItem(player, item, other);
                    break;
                }
            }
        }

        /// <summary>The item in a pack slot, if it is still the one the client clicked.</summary>
        private ItemDef? PackItem(Player player, int slot, string itemId)
        {
            ItemContainer pack = Items.Of(player).Inventory;
            if (!pack.InRange(slot) || pack.IsEmpty(slot) || pack[slot].ItemId != itemId) return null;
            return World.Content.Item(itemId);
        }

        // ------------------------------------------------------------------ resolving a use

        /// <summary>Uses an item on a placed object the player is standing beside.</summary>
        public void OnObject(Player player, ItemDef item, PlacedObject placed, ObjectDef def)
        {
            if (!Still(player, item)) return;
            var matches = World.Content.Uses.Where(u => u.OnObject == def.Id && Matches(u.Item, item)).Select(u => (u, item, (ItemDef?)null)).ToList();
            Resolve(player, matches, "object:" + def.Id, null, def.Name, placed.Tile);
        }

        /// <summary>Uses an item on an NPC the player is standing beside.</summary>
        public void OnNpc(Player player, ItemDef item, Npc npc)
        {
            if (!Still(player, item)) return;
            var matches = World.Content.Uses.Where(u => u.OnNpc != null && Matches(u.Item, item) && Matches(u.OnNpc, npc.Def.Id, npc.Def.Tags))
                .Select(u => (u, item, (ItemDef?)null)).ToList();
            if (matches.Count > 0)
            {
                npc.BusyUntilTick = World.CurrentTick + 20;
                npc.Waypoints.Clear();
            }
            Resolve(player, matches, "npc:" + npc.Def.Id, npc, npc.Name, npc.Position);
        }

        /// <summary>Uses one pack item on another; either may be the one the use names first.</summary>
        public void OnItem(Player player, ItemDef used, ItemDef other)
        {
            var matches = new List<(UseDef, ItemDef, ItemDef?)>();
            foreach (UseDef u in World.Content.Uses)
            {
                if (u.OnItem == null) continue;
                if (Matches(u.Item, used) && Matches(u.OnItem, other)) matches.Add((u, used, other));
                else if (Matches(u.Item, other) && Matches(u.OnItem, used)) matches.Add((u, other, used));
            }
            Resolve(player, matches, "item:" + other.Id, null, other.Name, null);
        }

        private bool Still(Player player, ItemDef item)
        {
            if (Items.CountOf(player, item.Id) > 0) return true;
            World.Tell(player, TextKind.Warning, "You no longer have the " + item.Name.ToLowerInvariant() + ".");
            return false;
        }

        private static bool Matches(string spec, ItemDef item) => Matches(spec, item.Id, item.Tags);

        /// <summary>An id, or "tag:x" for anything carrying that tag.</summary>
        private static bool Matches(string spec, string id, List<string> tags) =>
            spec.StartsWith("tag:") ? tags.Contains(spec.Substring(4)) : spec == id;

        /// <summary>
        /// The first use whose requirements and conditions pass happens. If some matched but none
        /// passed, the first one's fail message (or what level it wants) is said instead.
        /// </summary>
        private void Resolve(Player player, List<(UseDef Use, ItemDef Item, ItemDef? Other)> matches, string target, Npc? npc, string targetName, TileCoord? face)
        {
            string? firstProblem = null;
            foreach ((UseDef use, ItemDef item, ItemDef? other) in matches)
            {
                string? problem = Problem(player, use, npc);
                if (problem == null)
                {
                    Do(player, use, item, other, other != null ? "item:" + other.Id : target, npc, targetName, face);
                    return;
                }
                firstProblem ??= problem;
            }
            World.Tell(player, firstProblem != null ? TextKind.Warning : TextKind.Info, firstProblem ?? Nothing);
        }

        private string? Problem(Player player, UseDef use, Npc? npc)
        {
            string? level = World.Find<SkillSystem>()?.Check(player, use.Requirements, "do that");
            if (level != null) return use.FailMessage ?? level;
            DialogueSystem dialogue = World.Get<DialogueSystem>();
            if (!use.Conditions.All(c => dialogue.Test(player, c, npc))) return use.FailMessage ?? Nothing;
            return null;
        }

        private void Do(Player player, UseDef use, ItemDef item, ItemDef? other, string target, Npc? npc, string speaker, TileCoord? face)
        {
            DialogueSystem dialogue = World.Get<DialogueSystem>();
            if (use.Consume) Items.Take(player, item.Id, 1);
            if (use.ConsumeTarget && other != null) Items.Take(player, other.Id, 1);
            if (use.Animation != null) World.Animate(player, use.Animation, face);
            else if (face.HasValue) World.Face(player, face.Value);
            if (use.Message != null) World.Tell(player, TextKind.Info, dialogue.Format(player, use.Message, npc));
            if (use.Effects.Count > 0) Items.ApplyEffects(player, use.Effects);
            foreach (DialogueAction action in use.Actions) dialogue.Run(player, action, npc);
            World.Events.Publish(new ItemUsedOn(player, item, target, use));

            if (use.Recipe != null && World.Content.Recipes.TryGetValue(use.Recipe, out RecipeDef? recipe))
                World.Get<RecipeSystem>().Repeat(player, recipe, face);
            if (use.Dialogue != null)
            {
                if (npc != null) dialogue.Start(player, npc, use.Dialogue);
                else dialogue.Start(player, use.Dialogue, speaker);
            }
        }
    }
}
