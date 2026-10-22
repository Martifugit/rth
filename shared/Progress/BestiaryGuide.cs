using System.Collections.Generic;

namespace Rth.Shared.Progress
{
    /// <summary>
    /// What the bestiary knows about the world, the same for every player: which regions have
    /// creatures, where each creature lives, which drops are worth hunting for, and which
    /// creatures leave a carcass for a skill. The server works it out from content and the map's
    /// spawns and sends it as the "bestiaryGuide" state; the player's own kills and drop log stay
    /// in <see cref="BestiaryState"/>.
    /// </summary>
    public sealed class BestiaryGuide
    {
        /// <summary>Regions with at least one creature, in tab order. A region named "" holds creatures that live nowhere on the map.</summary>
        public List<BestiaryRegion> Regions = new List<BestiaryRegion>();
        public List<BestiaryCreature> Creatures = new List<BestiaryCreature>();
        /// <summary>One per boss (its id is the boss's id), with the notable drops of its rewards.</summary>
        public List<BestiaryCreature> Bosses = new List<BestiaryCreature>();
    }

    public sealed class BestiaryRegion
    {
        public string Name = "";
        public string Title = "";
    }

    public sealed class BestiaryCreature
    {
        /// <summary>The NPC id (or boss id).</summary>
        public string Id = "";
        /// <summary>Names of the regions it spawns in; empty if none (it appears under "").</summary>
        public List<string> Regions = new List<string>();
        /// <summary>The carcass it leaves and how it is worked, or null if it leaves none.</summary>
        public BestiaryCarcass? Carcass;
        /// <summary>Its rare and unique drops (carcass yields included), commonest first.</summary>
        public List<NotableDrop> Notable = new List<NotableDrop>();
    }

    /// <summary>A creature's carcass: an item it drops that a recipe (butchering) turns into its yield.</summary>
    public sealed class BestiaryCarcass
    {
        public string Item = "";
        public string Recipe = "";
        public string Skill = "";
        public int Level;
        /// <summary>The object type it is worked at, or "" for a recipe done by a use.</summary>
        public string Station = "";
    }

    /// <summary>A drop the bestiary hints at before the player has had it.</summary>
    public sealed class NotableDrop
    {
        public string Item = "";
        /// <summary>The chance of getting at least one per kill (or completion), carcass included: 0 to 1.</summary>
        public double Chance;
        /// <summary>Below the rare chance (world.json <c>rareDropChance</c>).</summary>
        public bool Rare;
        /// <summary>No other creature or boss gives it.</summary>
        public bool Unique;
    }

    /// <summary>The words the interface uses for how likely a drop is.</summary>
    public static class DropRarity
    {
        public static string Word(double chance) =>
            chance >= 0.999 ? "Always"
            : chance >= 0.25 ? "Common"
            : chance >= 0.05 ? "Uncommon"
            : chance >= 0.01 ? "Rare"
            : "Very rare";
    }
}
