using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>
    /// Building blocks for panels, all styled by Rth.uss: elements with classes, icons, wells,
    /// resource bars, buttons, checkboxes and tooltips. Panels should compose these rather than
    /// set colours and borders by hand, so the whole interface keeps one look.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Heading = new Color(1f, 0.596f, 0.122f);
        public static readonly Color Value = new Color(1f, 1f, 0f);
        public static readonly Color Muted = new Color(0.66f, 0.6f, 0.48f);
        public static readonly Color Good = new Color(0.235f, 0.847f, 0.235f);
        public static readonly Color Bad = new Color(1f, 0.29f, 0.227f);

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

        /// <summary>An element showing an icon at a fixed size (pixel art scales by whole steps best: 16, 32, 48).</summary>
        public static VisualElement Icon(VisualElement parent, Texture2D texture, float size, params string[] classes)
        {
            VisualElement e = El(parent, classes);
            e.AddToClassList("icon");
            e.style.width = size;
            e.style.height = size;
            SetIcon(e, texture);
            return e;
        }

        public static void SetIcon(VisualElement element, Texture2D texture) =>
            element.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);

        public static Label Button(VisualElement parent, string text, Action onClick, params string[] classes)
        {
            Label b = Text(parent, text, "button");
            foreach (string c in classes) b.AddToClassList(c);
            b.RegisterCallback<ClickEvent>(_ => onClick());
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

        /// <summary>A resource bar: optional icon, the bar, and a centred label.</summary>
        public sealed class Bar
        {
            public readonly VisualElement Root;
            public readonly VisualElement Track;
            private readonly VisualElement _fill;
            private readonly Label _label;

            public Bar(VisualElement parent, Color color, Texture2D icon = null, float height = 16f)
            {
                Root = El(parent, "bar-row");
                if (icon != null) Icon(Root, icon, Mathf.Min(18f, height + 2f), "bar-row__icon");
                Track = El(Root, "bar");
                Track.style.height = height;
                _fill = El(Track, "bar__fill");
                _fill.style.backgroundColor = color;
                El(Track, "bar__gloss");
                _label = Text(Track, "", "bar__label");
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

        /// <summary>Shows a tooltip (rich text allowed) while the pointer is over an element; the text is asked for each time.</summary>
        public static void Tooltip(VisualElement element, Func<string> text)
        {
            element.RegisterCallback<PointerEnterEvent>(e => UiRoot.Instance?.ShowTooltip(text(), e.position));
            element.RegisterCallback<PointerMoveEvent>(e => UiRoot.Instance?.ShowTooltip(text(), e.position));
            element.RegisterCallback<PointerLeaveEvent>(_ => UiRoot.Instance?.HideTooltip());
        }

        /// <summary>Rich-text colour wrapper for tooltips and labels.</summary>
        public static string Colour(string text, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";

        public static string Title(string text) => "<b>" + Colour(text, Heading) + "</b>";

        public static Color Rgb(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        public static string FormatCount(int n) =>
            n >= 10_000_000 ? n / 1_000_000 + "M" : n >= 100_000 ? n / 1000 + "K" : n.ToString();

        /// <summary>The classic stack colours: yellow, white from 100K, green from 10M.</summary>
        public static Color CountColour(int n) =>
            n >= 10_000_000 ? new Color(0f, 1f, 0.5f) : n >= 100_000 ? Color.white : Value;

        public static string FormatXp(long xp) =>
            xp >= 10_000_000 ? (xp / 1_000_000f).ToString("0.#") + "M" : xp >= 10_000 ? (xp / 1000f).ToString("0.#") + "K" : xp.ToString("N0");
    }
}
