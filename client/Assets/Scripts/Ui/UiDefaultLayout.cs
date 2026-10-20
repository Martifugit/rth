using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client.Ui
{
    /// <summary>
    /// Where each default window sits before the player moves anything: a classic arrangement,
    /// minimap top right, the tabbed side window under it, chat bottom left, the action bar
    /// bottom centre. Panels name their window with <see cref="UiPanel.DefaultWindow"/>; one
    /// not listed here gets a window of its own in the middle of the screen.
    /// </summary>
    public static class UiDefaultLayout
    {
        public struct Placement
        {
            /// <summary>0, 0.5 or 1 per axis (left/centre/right, top/middle/bottom).</summary>
            public Vector2 Anchor;
            public Vector2 Offset;
            public Vector2 Size;

            public Placement(float ax, float ay, float ox, float oy, float w, float h)
            {
                Anchor = new Vector2(ax, ay);
                Offset = new Vector2(ox, oy);
                Size = new Vector2(w, h);
            }
        }

        public const string Minimap = "minimap";
        public const string Side = "side";
        public const string Chat = "chat";
        public const string ActionBar = "actionbar";
        public const string Target = "target";
        public const string Dialogue = "dialogue";
        public const string Town = "town";

        public static readonly Dictionary<string, Placement> Windows = new Dictionary<string, Placement>
        {
            [Minimap] = new Placement(1f, 0f, -4f, 4f, 232f, 214f),
            [Side] = new Placement(1f, 1f, -4f, -4f, 278f, 372f),
            [Chat] = new Placement(0f, 1f, 4f, -4f, 460f, 210f),
            [ActionBar] = new Placement(0.5f, 1f, 0f, 0f, 440f, 140f),
            [Target] = new Placement(0.5f, 0f, 0f, 6f, 230f, 58f),
            [Dialogue] = new Placement(0.5f, 1f, 0f, -146f, 540f, 168f),
            [Town] = new Placement(0.5f, 0.5f, -150f, -40f, 430f, 380f),
        };

        /// <summary>A window for a panel with no default home: centred, at the panel's own size.</summary>
        public static Placement Centred(UiPanel panel) =>
            new Placement(0.5f, 0.5f, 0f, 0f, panel.DefaultSize.x, panel.DefaultSize.y);
    }
}
