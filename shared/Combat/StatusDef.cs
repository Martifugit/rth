namespace Rth.Shared.Combat
{
    /// <summary>
    /// A status effect a player can be under, shown as an icon in the buff or debuff bar. Loaded
    /// from server/data/content/statuses.json and sent to clients; the server decides who has
    /// which (see <see cref="StatusIds"/>) and how strong it is.
    /// </summary>
    public sealed class StatusDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>What it does. "{value}" is replaced by its current magnitude.</summary>
        public string Description = "";
        public string Icon = "";
        /// <summary>Tint for the icon, as "#rrggbb"; empty for white.</summary>
        public string Color = "";
        /// <summary>Shown in the debuff bar rather than the buff bar.</summary>
        public bool Debuff;
    }

    /// <summary>The status ids the server gives out by itself. statuses.json must define them all.</summary>
    public static class StatusIds
    {
        /// <summary>Yellow bile: more damage dealt.</summary>
        public const string Infused = "infused";
        /// <summary>Yellow bile: more damage taken.</summary>
        public const string Choleric = "choleric";
        /// <summary>Black bile: less damage taken.</summary>
        public const string Warding = "warding";
        /// <summary>Black bile: less damage dealt.</summary>
        public const string Melancholic = "melancholic";

        /// <summary>The status shown for a timed ability effect (<see cref="EffectType"/>).</summary>
        public static string ForEffect(EffectType type) => type.ToString().ToLowerInvariant();

        public static readonly string[] BuiltIn = { Infused, Choleric, Warding, Melancholic };
    }
}
