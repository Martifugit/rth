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

    /// <summary>Where combat XP goes: the style that dealt the damage, Defence, or half to each.</summary>
    public enum TrainDefence : byte
    {
        Offence = 0,
        Balanced = 1,
        Defence = 2,
    }

    /// <summary>
    /// A player's combat preferences. Kept as client state (so they follow the player) under the
    /// keys below, as plain strings, and read by the server, which applies them.
    /// </summary>
    public sealed class CombatOptions
    {
        public const string QueueingKey = "combat.queue";
        public const string RevolutionKey = "combat.revolution";
        public const string TrainDefenceKey = "combat.trainDefence";
        /// <summary>The action bar layout (a string[][] of "a:id", "h:id" and "i:id" slots); bar 0 is the main bar.</summary>
        public const string BarsKey = "bars";
        public const string AbilityPrefix = "a:";

        /// <summary>
        /// Each weapon style has its own main bar, which swaps in with the weapon ("bars.main.Melee",
        /// a string[] of slots). Bar 0 of <see cref="BarsKey"/> is the occult main bar from before.
        /// </summary>
        public static string MainBarKey(CombatStyle style) => "bars.main." + style;

        /// <summary>Whether an ability pressed during the global cooldown waits and fires when it ends.</summary>
        public bool Queueing = true;
        public RevolutionMode Revolution = RevolutionMode.Manual;
        public TrainDefence TrainDefence = TrainDefence.Balanced;

        public static TrainDefence TrainDefenceFrom(string? value) => value switch
        {
            "offence" => TrainDefence.Offence,
            "defence" => TrainDefence.Defence,
            _ => TrainDefence.Balanced,
        };

        public static string TrainDefenceValue(TrainDefence mode) => mode switch
        {
            TrainDefence.Offence => "offence",
            TrainDefence.Defence => "defence",
            _ => "balanced",
        };

        /// <summary>The share of combat XP that goes to Defence (0 to 1).</summary>
        public static double DefenceShare(TrainDefence mode) => mode switch
        {
            TrainDefence.Offence => 0,
            TrainDefence.Defence => 1,
            _ => 0.5,
        };

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
