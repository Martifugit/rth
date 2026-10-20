using System.Collections.Generic;
using Rth.Server.Content;
using Rth.Shared.Combat;
using Rth.Shared.Net;

namespace Rth.Server.Game
{
    /// <summary>
    /// What happens when someone dies: the Assayer weighs a dead player, who then respawns with
    /// balanced humours; whoever killed an NPC is told so. Listens for <see cref="EntityDied"/>.
    /// </summary>
    public sealed class DeathSystem : GameSystem
    {
        protected override void OnAttach() => World.Events.Subscribe<EntityDied>(OnDied);

        private void OnDied(EntityDied e)
        {
            if (e.Victim is Player player)
            {
                player.Deaths++;
                player.QueuedAbility = null;
                player.Deflect = CombatStyle.None;
                player.RespawnAtTick = World.CurrentTick + (uint)World.Rules.RespawnDelayTicks;
                World.Tell(player, TextKind.Info, "Your humours come apart.");
                World.Tell(player, TextKind.Assayer, AssayerRemark(player));
            }
            else if (e.Killer is Player winner)
            {
                World.Tell(winner, TextKind.Info, "You have defeated the " + e.Victim.Name.ToLowerInvariant() + ".");
            }
        }

        /// <summary>What the Assayer says over the scales, based on how this player died.</summary>
        private string AssayerRemark(Player player)
        {
            AssayerLines lines = World.Content.Assayer;
            CombatRules rules = World.Rules;
            List<string> pool =
                player.Deaths == 1 ? lines.First :
                player.Deaths >= 10 && lines.Frequent.Count > 0 && World.Random.Next(3) == 0 ? lines.Frequent :
                rules.IsCholeric(player.Bile) ? lines.Choleric :
                rules.IsMelancholic(player.Bile) ? lines.Melancholic :
                lines.Balanced;
            if (pool.Count == 0) pool = lines.Balanced;
            string line = pool.Count > 0 ? pool[World.Random.Next(pool.Count)] : "The Assayer weighs you, and says nothing.";
            return line.Replace("{name}", player.Name).Replace("{deaths}", player.Deaths.ToString());
        }

        public override void BeforePlayer(Player player)
        {
            if (player.IsDead && World.CurrentTick >= player.RespawnAtTick) Respawn(player);
        }

        private void Respawn(Player player)
        {
            player.IsDead = false;
            player.Plane = World.Map.SpawnPlane;
            player.Position = World.Map.Spawn;
            player.Waypoints.Clear();
            player.Teleported = true;
            player.Effects.Clear();
            player.Cooldowns.Clear();
            player.LastCombatTick = 0;
            player.Phlegm = 0;
            player.Bile = 0; // the Assayer puts back a balanced measure
            World.RecalculateStats(player);
            player.Blood = player.MaxBlood;
            player.HealthChanged = true;
        }
    }
}
