using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client.Ui
{
    /// <summary>
    /// Where each default window sits before the player moves anything, docked like a classic
    /// layout: the minimap top right, the tabbed side window bottom right, chat bottom left, the
    /// main action bar bottom centre. Panels name their window with <see cref="UiPanel.DefaultWindow"/>;
    /// fixed-size panels use their own size. One not listed here opens in the middle.
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
        public const string ActionBar2 = "actionbar2";
        public const string ActionBar3 = "actionbar3";
        public const string Target = "target";
        public const string Buffs = "buffs";
        public const string Debuffs = "debuffs";
        public const string Dialogue = "dialogue";
        public const string Town = "town";
        public const string Boss = "boss";

        public static readonly Dictionary<string, Placement> Windows = new Dictionary<string, Placement>
        {
            [Minimap] = new Placement(1f, 0f, 0f, 0f, 236f, 282f),
            [Side] = new Placement(1f, 1f, 0f, 0f, 312f, 400f),
            [Chat] = new Placement(0f, 1f, 0f, 0f, 480f, 230f),
            [ActionBar] = new Placement(0.5f, 1f, 0f, 0f, 636f, 110f),
            [Buffs] = new Placement(0.5f, 1f, -160f, -114f, 316f, 40f),
            [Debuffs] = new Placement(0.5f, 1f, 160f, -114f, 316f, 40f),
            [ActionBar2] = new Placement(0.5f, 1f, 0f, -158f, 636f, 62f),
            [ActionBar3] = new Placement(0.5f, 1f, 0f, -222f, 636f, 62f),
            [Target] = new Placement(0.5f, 0f, 0f, 8f, 260f, 64f),
            [Dialogue] = new Placement(0.5f, 1f, 0f, -130f, 600f, 230f),
            [Town] = new Placement(0.5f, 0.5f, -60f, -30f, 780f, 480f),
            [Boss] = new Placement(0.5f, 0f, 0f, 76f, 460f, 58f),
        };

        /// <summary>A window for a panel with no default home: centred, at the panel's own size.</summary>
        public static Placement Centred(UiPanel panel) =>
            new Placement(0.5f, 0.5f, 0f, 0f, panel.DefaultSize.x, panel.DefaultSize.y);
    }
}
