using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// What is left of the IMGUI interface while it moves to the window system (<c>Ui/</c>): the
    /// hover text (what a left-click would do), the Assayer's words on death, and the screen
    /// claims that keep clicks on IMGUI windows (bank, shop, dialogue, menus) out of the world.
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        public GameSession session;
        public WorldView world;

        private static readonly List<Rect> HudRects = new List<Rect>();

        private GUIStyle _center;
        private GUIStyle _hover;
        private GUIStyle _assayer;

        /// <summary>Whether a screen position (bottom-left origin, as the Input System reports) is over IMGUI.</summary>
        public static bool IsPointerOverHud(Vector2 screenPosition) =>
            ClaimsContain(new Vector2(screenPosition.x, Screen.height - screenPosition.y));

        /// <summary>Whether a GUI position (top-left origin) is over an IMGUI window or menu.</summary>
        public static bool ClaimsContain(Vector2 gui)
        {
            if (PopupMenu.OpenRect.Contains(gui)) return true;
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

        private void OnGUI()
        {
            GUI.depth = 0;
            if (session == null || !session.IsInGame) return;
            EnsureStyles();
            DrawHoverText();
            DrawAssayer();
        }

        /// <summary>What a left-click would do, top-left like the classic games.</summary>
        private void DrawHoverText()
        {
            if (string.IsNullOrEmpty(session.HoverText) || PopupMenu.IsOpen) return;
            GUI.Label(new Rect(10f, 8f, 500f, 20f), session.HoverText, _hover);
        }

        private void DrawAssayer()
        {
            if (string.IsNullOrEmpty(session.AssayerText)) return;
            Rect r = Claim(new Rect(Screen.width / 2f - 230f, Screen.height / 2f - 110f, 460f, 170f));
            GUI.color = new Color(0.06f, 0.05f, 0.09f, 0.94f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y + 10f, r.width, 22f), "THE ASSAYER", _center);
            GUI.Label(new Rect(r.x + 18f, r.y + 36f, r.width - 36f, r.height - 76f), session.AssayerText, _assayer);
            if (GUI.Button(new Rect(r.x + r.width / 2f - 60f, r.yMax - 34f, 120f, 24f), "Return")) session.AssayerText = null;
        }

        private void EnsureStyles()
        {
            if (_center != null) return;
            _center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };
            _center.normal.textColor = Color.white;
            _hover = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _hover.normal.textColor = new Color(1f, 0.95f, 0.75f);
            _assayer = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, fontStyle = FontStyle.Italic, alignment = TextAnchor.UpperCenter };
            _assayer.normal.textColor = new Color(0.88f, 0.84f, 0.95f);
        }
    }
}
