using Rth.Server.Content;

namespace Rth.Server.Game
{
    /// <summary>When an NPC with a drop table dies, roll it and put the loot on the ground for the killer.</summary>
    public sealed class LootSystem : GameSystem
    {
        protected override void OnAttach() => World.Events.Subscribe<EntityDied>(OnDied);

        private void OnDied(EntityDied e)
        {
            if (!(e.Victim is Npc npc) || npc.Def.DropTable == null) return;
            GroundItemSystem ground = World.Get<GroundItemSystem>();
            Player? owner = e.Killer as Player;
            System.Collections.Generic.List<Drop> drops = DropRoller.Roll(World.Content, npc.Def.DropTable, World.Random);
            foreach (Drop drop in drops)
                ground.Spawn(drop.ItemId, drop.Count, npc.Plane, npc.Position, owner);
            if (owner != null && drops.Count > 0) World.Events.Publish(new ItemsObtained(owner, "npc:" + npc.Def.Id, drops));
        }
    }
}
