using System;
using System.Collections.Generic;
using Box = Rth.Client.Ui.WindowRules.Box;
using Dir = Rth.Client.Ui.WindowRules.Side;
using Line = Rth.Client.Ui.WindowRules.Align;

namespace Rth.Client.Ui
{
    /// <summary>
    /// Where each default window sits before the player moves anything, as <b>presets</b> the player
    /// picks from (Settings, the windows menu): Classic (minimap top right, the tabbed side window
    /// bottom right, chat bottom left, the main action bar bottom centre), Compact, Wide and Mirrored.
    /// Panels name their window with <see cref="UiPanel.DefaultWindow"/>; fixed-size panels use their
    /// own size. A window not listed opens in the middle.
    ///
    /// Every window sits flush against the screen's edge or another window, never with a gap: a
    /// placement is either anchored to the screen (a corner, an edge's middle) or attached to the side
    /// of another window, so a bar that changes size still has its neighbours flush against it. Sizes
    /// are panel pixels, made for a 1080-pixel-high screen; the interface scale fits them to others
    /// (720p to 1440p is 960 to 1152 panel pixels high, and every preset fits that, see the tests).
    ///
    /// Plain C# with no Unity types, so the presets are checked by `dotnet test` (tools/client-tests).
    /// </summary>
    public static class UiDefaultLayout
    {
        /// <summary>Where one window goes: anchored to the screen, or attached to another window's side.</summary>
        public struct Placement
        {
            /// <summary>0 to 1 per axis (left/centre/right, top/middle/bottom), for a placement on the screen.</summary>
            public float AnchorX, AnchorY;
            /// <summary>Offset from the anchored position (panel pixels). Zero keeps it flush with the screen's edge.</summary>
            public float OffsetX, OffsetY;
            /// <summary>The window's size; 0 takes the panel's own (its fixed or default size).</summary>
            public float W, H;
            /// <summary>The window it is attached to (null: anchored to the screen).</summary>
            public string Next;
            public Dir Side;
            public Line Align;

            public Placement(float ax, float ay, float ox, float oy, float w, float h)
            {
                AnchorX = ax;
                AnchorY = ay;
                OffsetX = ox;
                OffsetY = oy;
                W = w;
                H = h;
                Next = null;
                Side = Dir.Above;
                Align = Line.Start;
            }

            /// <summary>Flush with the screen at an anchor: (1, 0) the top right corner, (0.5, 1) the bottom edge's middle.</summary>
            public static Placement Screen(float ax, float ay, float w = 0f, float h = 0f) => new Placement(ax, ay, 0f, 0f, w, h);

            /// <summary>Flush against a side of another window, lined up with it as asked.</summary>
            public static Placement By(string next, Dir side, Line align, float w = 0f, float h = 0f) =>
                new Placement(0f, 0f, 0f, 0f, w, h) { Next = next, Side = side, Align = align };

            /// <summary>The same placement seen in a mirror: left and right swap.</summary>
            public Placement Mirrored()
            {
                Placement m = this;
                m.AnchorX = 1f - AnchorX;
                m.OffsetX = -OffsetX;
                if (Next != null)
                {
                    if (Side == Dir.LeftOf) m.Side = Dir.RightOf;
                    else if (Side == Dir.RightOf) m.Side = Dir.LeftOf;
                    else if (Align == Line.Start) m.Align = Line.End;
                    else if (Align == Line.End) m.Align = Line.Start;
                }
                return m;
            }
        }

        /// <summary>A whole arrangement the player can pick.</summary>
        public sealed class Preset
        {
            public readonly string Id;
            public readonly string Title;
            public readonly string Description;
            public readonly Dictionary<string, Placement> Windows;
            /// <summary>Default windows this preset leaves closed (the player opens them from the windows menu).</summary>
            public readonly HashSet<string> Closed;

            public Preset(string id, string title, string description, Dictionary<string, Placement> windows, IEnumerable<string> closed = null)
            {
                Id = id;
                Title = title;
                Description = description;
                Windows = windows;
                Closed = new HashSet<string>(closed ?? Array.Empty<string>());
            }
        }

        public const string Minimap = "minimap";
        /// <summary>The tabbed side window (pack, worn, books, skills, quests).</summary>
        public const string Side = "side";
        /// <summary>The second side window, above the first: a window holds at most six panels.</summary>
        public const string Side2 = "side2";
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
        public const string XpDrops = "xpdrops";

        /// <summary>The bars, buffs, target, boss timer, conversation and town windows, where every preset puts them.</summary>
        private static void Hud(Dictionary<string, Placement> w)
        {
            w[ActionBar] = Placement.Screen(0.5f, 1f);
            // The status rows sit on the main bar, one at each end; extra bars stack above them.
            w[Buffs] = Placement.By(ActionBar, Dir.Above, Line.Start);
            w[Debuffs] = Placement.By(ActionBar, Dir.Above, Line.End);
            w[ActionBar2] = Placement.By(Buffs, Dir.Above, Line.Start);
            w[ActionBar3] = Placement.By(ActionBar2, Dir.Above, Line.Start);
            w[Target] = Placement.Screen(0.5f, 0f);
            w[Boss] = Placement.By(Target, Dir.Below, Line.Centre);
            // Floating (server) windows: the conversation over the bars, the bank and shops centred.
            w[Dialogue] = Placement.By(ActionBar, Dir.Above, Line.Centre);
            w[Town] = Placement.Screen(0.5f, 0.5f);
        }

        /// <summary>The classic arrangement: minimap top right, side windows under it, chat bottom left.</summary>
        public static readonly Preset Classic = Make("classic", "Classic",
            "Minimap top right, the side windows bottom right, chat bottom left, bars bottom centre.", w =>
            {
                w[Minimap] = Placement.Screen(1f, 0f);
                // XP drops beside the minimap, falling from the top: the right column stays for the side windows.
                w[XpDrops] = Placement.By(Minimap, Dir.LeftOf, Line.Start);
                w[Side] = Placement.Screen(1f, 1f, 312f, 400f);
                w[Side2] = Placement.By(Side, Dir.Above, Line.End, 312f, 220f);
                w[Chat] = Placement.Screen(0f, 1f, 480f, 230f);
            });

        /// <summary>Less of the screen covered: smaller side window and chat, the task and XP tracker closed.</summary>
        public static readonly Preset Compact = Make("compact", "Compact",
            "Smaller side window and chat; the task and XP tracker start closed.", w =>
            {
                w[Minimap] = Placement.Screen(1f, 0f);
                w[XpDrops] = Placement.By(Minimap, Dir.LeftOf, Line.Start);
                w[Side] = Placement.Screen(1f, 1f, 264f, 380f);
                w[Side2] = Placement.By(Side, Dir.Above, Line.End, 264f, 200f);
                w[Chat] = Placement.Screen(0f, 1f, 400f, 190f);
            }, Side2);

        /// <summary>Uses the top left corner too: the second side window there, a taller side window, wider chat.</summary>
        public static readonly Preset Wide = Make("wide", "Wide",
            "The second side window top left, a taller side window and a wider chat.", w =>
            {
                w[Minimap] = Placement.Screen(1f, 0f);
                w[XpDrops] = Placement.By(Minimap, Dir.LeftOf, Line.Start);
                w[Side] = Placement.Screen(1f, 1f, 312f, 480f);
                w[Side2] = Placement.Screen(0f, 0f, 312f, 260f);
                w[Chat] = Placement.Screen(0f, 1f, 520f, 260f);
            });

        /// <summary>Classic with left and right swapped.</summary>
        public static readonly Preset Mirrored = Make("mirrored", "Mirrored",
            "Classic with left and right swapped: minimap and side windows on the left, chat on the right.", w =>
            {
                // The bars and their rows stay as they are; only the corners swap.
                foreach (string id in new[] { Minimap, XpDrops, Side, Side2, Chat }) w[id] = Classic.Windows[id].Mirrored();
            });

        public static readonly IReadOnlyList<Preset> Presets = new[] { Classic, Compact, Wide, Mirrored };

        /// <summary>The preset with this id, or Classic.</summary>
        public static Preset Find(string id)
        {
            foreach (Preset p in Presets)
                if (p.Id == id) return p;
            return Classic;
        }

        /// <summary>The classic placements (older callers look windows up here).</summary>
        public static Dictionary<string, Placement> Windows => Classic.Windows;

        private static Preset Make(string id, string title, string description, Action<Dictionary<string, Placement>> fill, params string[] closed)
        {
            var w = new Dictionary<string, Placement>();
            Hud(w);
            fill(w);
            return new Preset(id, title, description, w, closed);
        }

        /// <summary>The size a window takes when nothing else says.</summary>
        public const float FallbackW = 260f, FallbackH = 320f;

        /// <summary>
        /// Every window of a preset placed on a screen: attached windows follow the box of the window
        /// they hang from (worked out even when that one isn't open, so a bar keeps its place).
        /// <paramref name="sizeOf"/> gives a window's own size if its panels insist on one (fixed
        /// size), else null and the preset's size is used. Boxes are kept on screen.
        /// </summary>
        public static Dictionary<string, Box> Resolve(Preset preset, float screenW, float screenH, Func<string, (float W, float H)?> sizeOf)
        {
            var boxes = new Dictionary<string, Box>();
            var busy = new HashSet<string>();
            Box Place(string id)
            {
                if (boxes.TryGetValue(id, out Box done)) return done;
                Placement p = preset.Windows[id];
                (float w, float h) = sizeOf?.Invoke(id) ?? (p.W > 0f ? p.W : FallbackW, p.H > 0f ? p.H : FallbackH);
                Box b;
                busy.Add(id);
                if (p.Next != null && preset.Windows.ContainsKey(p.Next) && !busy.Contains(p.Next))
                    b = WindowRules.Attach(Place(p.Next), p.Side, p.Align, w, h);
                else
                    b = WindowRules.Anchored(p.AnchorX, p.AnchorY, p.OffsetX, p.OffsetY, w, h, screenW, screenH);
                busy.Remove(id);
                b = WindowRules.Clamp(b, screenW, screenH);
                boxes[id] = b;
                return b;
            }
            foreach (string id in preset.Windows.Keys) Place(id);
            return boxes;
        }
    }
}
