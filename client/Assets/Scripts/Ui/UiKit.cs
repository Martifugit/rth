using System;
using Rth.Shared.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// Building blocks for panels and modals, all styled by Rth.uss: elements with classes, tinted
    /// icons, item icons (bank notes included), bars and resource gauges, buttons, checkboxes,
    /// scroll areas, list rows and tooltips. Compose these rather than styling by hand, so the
    /// whole interface keeps one look.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color TextColour = new Color(0.867f, 0.827f, 0.749f);
        public static readonly Color HeadingColour = new Color(0.878f, 0.765f, 0.529f);
        public static readonly Color Value = new Color(0.941f, 0.835f, 0.549f);
        public static readonly Color Muted = new Color(0.561f, 0.518f, 0.443f);
        public static readonly Color Good = new Color(0.486f, 0.812f, 0.384f);
        public static readonly Color Bad = new Color(0.878f, 0.376f, 0.306f);
        public static readonly Color IconTint = new Color(0.878f, 0.765f, 0.529f);

        public static readonly Color BloodColour = new Color(0.72f, 0.12f, 0.1f);
        public static readonly Color PhlegmColour = new Color(0.4f, 0.66f, 0.38f);
        public static readonly Color DevotionColour = new Color(0.86f, 0.8f, 0.58f);
        public static readonly Color YellowBile = new Color(0.92f, 0.74f, 0.16f);
        public static readonly Color BlackBile = new Color(0.5f, 0.3f, 0.7f);

        public static VisualElement El(VisualElement parent, params string[] classes)
        {
            var e = new VisualElement();
            foreach (string c in classes) e.AddToClassList(c);
            parent?.Add(e);
            return e;
        }

        public static Label Text(VisualElement parent, string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (string c in classes) l.AddToClassList(c);
            parent?.Add(l);
            return l;
        }

        public static Label Heading(VisualElement parent, string text) => Text(parent, text.ToUpperInvariant(), "heading");

        public static VisualElement Divider(VisualElement parent) => El(parent, "divider");

        // ------------------------------------------------------------------ icons

        /// <summary>An element showing an icon at a size, tinted (icons are white silhouettes).</summary>
        public static VisualElement Icon(VisualElement parent, Texture2D texture, float size, params string[] classes)
        {
            VisualElement e = El(parent, classes);
            e.AddToClassList("icon");
            e.style.width = size;
            e.style.height = size;
            SetIcon(e, texture);
            return e;
        }

        public static void SetIcon(VisualElement element, Texture2D texture, Color? tint = null)
        {
            element.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
            if (tint.HasValue) element.style.unityBackgroundImageTintColor = tint.Value;
        }

        /// <summary>
        /// Shows an item on an icon element: its icon in its colour, or for a bank note, a paper
        /// with the item drawn small on it.
        /// </summary>
        public static void SetItem(VisualElement icon, ItemDef def)
        {
            icon.Clear();
            if (def == null)
            {
                SetIcon(icon, null);
                return;
            }
            if (def.NoteOf != null)
            {
                SetIcon(icon, IconLibrary.Get("note"), new Color(0.86f, 0.8f, 0.64f));
                VisualElement inner = El(icon, "icon");
                inner.pickingMode = PickingMode.Ignore;
                inner.style.position = Position.Absolute;
                inner.style.left = Length.Percent(22f);
                inner.style.top = Length.Percent(22f);
                inner.style.width = Length.Percent(56f);
                inner.style.height = Length.Percent(56f);
                SetIcon(inner, IconLibrary.ForItem(def), IconLibrary.ItemTint(def));
                return;
            }
            SetIcon(icon, IconLibrary.ForItem(def), IconLibrary.ItemTint(def));
        }

        // ------------------------------------------------------------------ buttons and toggles

        public static Label Button(VisualElement parent, string text, Action onClick, params string[] classes)
        {
            Label b = Text(parent, text, "button");
            foreach (string c in classes) b.AddToClassList(c);
            b.RegisterCallback<ClickEvent>(_ => onClick());
            return b;
        }

        /// <summary>A square button showing an icon.</summary>
        public static VisualElement IconButton(VisualElement parent, string icon, string tooltip, Action onClick)
        {
            VisualElement b = El(parent, "button", "button--icon");
            Icon(b, IconLibrary.Get(icon), 20f).style.unityBackgroundImageTintColor = IconTint;
            if (onClick != null) b.RegisterCallback<ClickEvent>(_ => onClick());
            Tooltip(b, () => tooltip);
            return b;
        }

        /// <summary>A checkbox row. Call <see cref="Check.Set"/> each refresh with the current value.</summary>
        public sealed class Check
        {
            public readonly VisualElement Root;
            private readonly Label _label;

            public Check(VisualElement parent, string label, Action onClick)
            {
                Root = El(parent, "check");
                El(Root, "check__box");
                _label = Text(Root, label);
                Root.RegisterCallback<ClickEvent>(_ => onClick());
            }

            public void Set(bool on, string label = null)
            {
                Root.EnableInClassList("check--on", on);
                if (label != null) _label.text = label;
            }
        }

        /// <summary>A vertical scroll area that scrolls at a sensible speed.</summary>
        public static ScrollView Scroll(VisualElement parent)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.style.flexGrow = 1;
            scroll.mouseWheelScrollSize = 40f;
            parent.Add(scroll);
            return scroll;
        }

        /// <summary>A selectable row for a master list (quests, creatures, skills).</summary>
        public static VisualElement MasterRow(VisualElement parent, string text, Action onClick, Color? colour = null)
        {
            VisualElement row = El(parent, "master-row");
            Label label = Text(row, text);
            if (colour.HasValue) label.style.color = colour.Value;
            row.RegisterCallback<ClickEvent>(_ => onClick());
            return row;
        }

        // ------------------------------------------------------------------ bars

        /// <summary>A plain bar with an optional centred label.</summary>
        public sealed class Bar
        {
            public readonly VisualElement Root;
            private readonly VisualElement _fill;
            private readonly Label _label;

            public Bar(VisualElement parent, Color color, float height = 18f)
            {
                Root = El(parent, "bar");
                Root.style.height = height;
                _fill = El(Root, "bar__fill");
                _fill.style.backgroundColor = color;
                El(Root, "bar__gloss");
                _label = Text(Root, "", "bar__label");
            }

            public void Set(float fraction, string text)
            {
                _fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
                _label.text = text;
            }

            public Color Color
            {
                set => _fill.style.backgroundColor = value;
            }
        }

        /// <summary>
        /// A resource gauge in the modern style: icon, the numbers written above, a slim bar
        /// beneath. Several sit side by side with equal widths.
        /// </summary>
        public sealed class Gauge
        {
            public readonly VisualElement Root;
            private readonly Label _value;
            private readonly VisualElement _fill;

            public Gauge(VisualElement parent, string icon, Color colour)
            {
                Root = El(parent, "row");
                Root.style.flexGrow = 1;
                Root.style.flexBasis = 0;
                Root.style.marginRight = 8f;
                VisualElement i = Icon(Root, IconLibrary.Get(icon), 22f);
                i.style.unityBackgroundImageTintColor = Color.Lerp(colour, Color.white, 0.25f);
                i.style.marginRight = 6f;
                VisualElement column = El(Root);
                column.style.flexGrow = 1;
                _value = Text(column, "", "bold", "small");
                _value.style.marginBottom = 1f;
                VisualElement track = El(column, "bar");
                track.style.height = 7f;
                track.style.flexGrow = 0;
                _fill = El(track, "bar__fill");
                _fill.style.backgroundColor = colour;
                El(track, "bar__gloss");
            }

            public void Set(float fraction, string text)
            {
                _fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
                _value.text = text;
            }

            public Color Fill
            {
                set => _fill.style.backgroundColor = value;
            }
        }

        // ------------------------------------------------------------------ tooltips and text

        /// <summary>Shows a tooltip (rich text allowed) while the pointer is over an element; the text is asked for each time.</summary>
        public static void Tooltip(VisualElement element, Func<string> text)
        {
            element.RegisterCallback<PointerEnterEvent>(e => UiRoot.Instance?.ShowTooltip(text(), e.position));
            element.RegisterCallback<PointerMoveEvent>(e => UiRoot.Instance?.ShowTooltip(text(), e.position));
            element.RegisterCallback<PointerLeaveEvent>(_ => UiRoot.Instance?.HideTooltip());
        }

        /// <summary>Rich-text colour wrapper for tooltips and labels.</summary>
        public static string Colour(string text, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";

        public static string Title(string text) => "<b>" + Colour(text, HeadingColour) + "</b>";

        public static string FormatCount(int n) =>
            n >= 10_000_000 ? n / 1_000_000 + "M" : n >= 100_000 ? n / 1000 + "K" : n.ToString();

        /// <summary>The classic stack colours: gold, white from 100K, green from 10M.</summary>
        public static Color CountColour(int n) =>
            n >= 10_000_000 ? new Color(0.4f, 1f, 0.6f) : n >= 100_000 ? Color.white : Value;

        public static string FormatXp(long xp) =>
            xp >= 10_000_000 ? (xp / 1_000_000f).ToString("0.#") + "M" : xp >= 10_000 ? (xp / 1000f).ToString("0.#") + "K" : xp.ToString("N0");
    }
}
