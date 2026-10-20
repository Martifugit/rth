using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rth.Client.Ui
{
    /// <summary>Small helpers for building panels: elements with theme classes, bars, buttons, tooltips.</summary>
    public static class UiKit
    {
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

        public static Label Button(VisualElement parent, string text, Action onClick, params string[] classes)
        {
            Label b = Text(parent, text, "button");
            foreach (string c in classes) b.AddToClassList(c);
            b.RegisterCallback<ClickEvent>(_ => onClick());
            return b;
        }

        /// <summary>A labelled progress bar.</summary>
        public sealed class Bar
        {
            public readonly VisualElement Root;
            private readonly VisualElement _fill;
            private readonly Label _label;

            public Bar(VisualElement parent, Color color)
            {
                Root = El(parent, "bar");
                _fill = El(Root, "bar__fill");
                _fill.style.backgroundColor = color;
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

        /// <summary>Shows a tooltip while the pointer is over an element; the text is asked for each time.</summary>
        public static void Tooltip(VisualElement element, Func<string> text)
        {
            element.RegisterCallback<PointerEnterEvent>(e => UiRoot.Instance?.ShowTooltip(text(), e.position));
            element.RegisterCallback<PointerMoveEvent>(e => UiRoot.Instance?.ShowTooltip(text(), e.position));
            element.RegisterCallback<PointerLeaveEvent>(_ => UiRoot.Instance?.HideTooltip());
        }

        public static Color Rgb(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        public static string FormatCount(int n) =>
            n >= 10_000_000 ? n / 1_000_000 + "M" : n >= 100_000 ? n / 1000 + "K" : n.ToString();

        public static string FormatXp(long xp) =>
            xp >= 10_000_000 ? (xp / 1_000_000f).ToString("0.#") + "M" : xp >= 10_000 ? (xp / 1000f).ToString("0.#") + "K" : xp.ToString("N0");
    }
}
