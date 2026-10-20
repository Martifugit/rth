namespace Rth.Server.Game
{
    /// <summary>
    /// What a player is busy doing with something in the world: attacking an NPC, picking up an
    /// item, and later mining a rock or talking to someone. A player has at most one; walking
    /// somewhere cancels it. Each tick the world calls <see cref="Tick"/> instead of plain movement,
    /// so the interaction decides whether to walk closer or act.
    /// </summary>
    public abstract class Interaction
    {
        /// <summary>Advance one tick. Return false when finished (or impossible) to end it.</summary>
        public abstract bool Tick(World world, Player player);

        /// <summary>Called once when the interaction ends for any reason.</summary>
        public virtual void End(World world, Player player) { }
    }
}
