using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Screen claims for anything still drawn with IMGUI (the login box), so clicks there don't
    /// reach the world. The interface itself is UI Toolkit (<c>Ui/</c>).
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        public GameSession session;
        public WorldView world;

        private static readonly List<Rect> HudRects = new List<Rect>();


        /// <summary>Whether a screen position (bottom-left origin, as the Input System reports) is over IMGUI.</summary>
        public static bool IsPointerOverHud(Vector2 screenPosition) =>
            ClaimsContain(new Vector2(screenPosition.x, Screen.height - screenPosition.y));

        /// <summary>Whether a GUI position (top-left origin) is over an IMGUI window or menu.</summary>
        public static bool ClaimsContain(Vector2 gui)
        {
            foreach (Rect r in HudRects)
                if (r.Contains(gui)) return true;
            return false;
        }

        /// <summary>Marks part of the screen as interface, so clicks there don't reach the world.</summary>
        public static Rect Claim(Rect rect)
        {
            if (Event.current == null || Event.current.type == EventType.Repaint) HudRects.Add(rect);
            return rect;
        }

        /// <summary>Called once a frame (after input, before drawing) so this frame's GUI can claim afresh.</summary>
        public static void ClearClaims() => HudRects.Clear();
    }
}
