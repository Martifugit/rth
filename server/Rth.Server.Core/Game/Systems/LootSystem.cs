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
            foreach (Drop drop in DropRoller.Roll(World.Content, npc.Def.DropTable, World.Random))
                ground.Spawn(drop.ItemId, drop.Count, npc.Plane, npc.Position, owner);
        }
    }
}
