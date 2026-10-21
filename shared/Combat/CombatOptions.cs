namespace Rth.Shared.Combat
{
    /// <summary>How much of the action bar fires on its own (RuneScape's Revolution).</summary>
    public enum RevolutionMode : byte
    {
        /// <summary>Nothing fires by itself: every ability is pressed by hand.</summary>
        Manual = 0,
        /// <summary>Builders on the main bar fire on their own, leftmost ready first.</summary>
        Builders = 1,
        /// <summary>Builders, spenders and ultimates all fire on their own.</summary>
        All = 2,
    }

    /// <summary>
    /// A player's combat preferences. Kept as client state (so they follow the player) under the
    /// keys below, as plain strings, and read by the server, which applies them.
    /// </summary>
    public sealed class CombatOptions
    {
        public const string QueueingKey = "combat.queue";
        public const string RevolutionKey = "combat.revolution";
        /// <summary>The action bar layout (a string[][] of "a:id", "f:id" and "i:id" slots); bar 0 is the main bar.</summary>
        public const string BarsKey = "bars";
        public const string AbilityPrefix = "a:";

        /// <summary>Whether an ability pressed during the global cooldown waits and fires when it ends.</summary>
        public bool Queueing = true;
        public RevolutionMode Revolution = RevolutionMode.Manual;

        public static bool QueueingFrom(string? value) => value != "false";
        public static string QueueingValue(bool on) => on ? "true" : "false";

        public static RevolutionMode RevolutionFrom(string? value) => value switch
        {
            "builders" => RevolutionMode.Builders,
            "all" => RevolutionMode.All,
            _ => RevolutionMode.Manual,
        };

        public static string RevolutionValue(RevolutionMode mode) => mode switch
        {
            RevolutionMode.Builders => "builders",
            RevolutionMode.All => "all",
            _ => "manual",
        };

        /// <summary>Whether Revolution fires abilities of this type by itself.</summary>
        public bool AutoCasts(AbilityType type) => Revolution switch
        {
            RevolutionMode.Builders => type == AbilityType.Builder,
            RevolutionMode.All => type == AbilityType.Builder || type == AbilityType.Spender || type == AbilityType.Ultimate,
            _ => false,
        };
    }
}
