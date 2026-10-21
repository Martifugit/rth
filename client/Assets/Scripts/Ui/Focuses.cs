using Rth.Shared.Combat;
using Rth.Shared.Net;

namespace Rth.Client.Ui
{
    /// <summary>
    /// The focuses a player can hold with breath, as the interface knows them. Today these are the
    /// three deflections; the breath skill will add more (and should move them into content).
    /// A focus is used like an ability: from the focuses tab, or from an action bar slot ("f:id").
    /// </summary>
    public static class Focuses
    {
        public sealed class Focus
        {
            public string Id;
            public string Name;
            public string Icon;
            public CombatStyle Deflects;

            public string Description =>
                "Halves the damage you take from " + Deflects.ToString().ToLowerInvariant() + " attacks while held.";

            public bool IsHeld(PlayerStateMessage state) => state != null && state.Deflect == Deflects;

            public void Toggle(GameSession session) => session.ToggleDeflect(Deflects);
        }

        public static readonly Focus[] All =
        {
            new Focus { Id = "deflect_melee", Name = "Deflect melee", Icon = "deflect_melee", Deflects = CombatStyle.Melee },
            new Focus { Id = "deflect_ranged", Name = "Deflect ranged", Icon = "deflect_ranged", Deflects = CombatStyle.Ranged },
            new Focus { Id = "deflect_occult", Name = "Deflect occult", Icon = "deflect_occult", Deflects = CombatStyle.Occult },
        };

        public static readonly UnityEngine.Color Tint = new UnityEngine.Color(0.65f, 0.8f, 1f);

        public static Focus Get(string id)
        {
            foreach (Focus f in All)
                if (f.Id == id) return f;
            return null;
        }

        public static string Describe(Focus f) =>
            UiKit.Title(f.Name) + "\n" + f.Description + "\n" + UiKit.Colour("Drains 1 breath a tick. Use it again to let go.", UiKit.Muted);
    }
}
