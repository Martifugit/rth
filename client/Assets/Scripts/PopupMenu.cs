using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// The right-click menu: a list of labelled options at the pointer. Anything can open one
    /// (the world, inventory slots, later NPC dialogue or bank items). Picking an option or
    /// clicking elsewhere closes it.
    /// </summary>
    public sealed class PopupMenu : MonoBehaviour
    {
        private const float Width = 190f;
        private const float RowHeight = 20f;

        private static PopupMenu _instance;

        private readonly List<(string Label, Action Action)> _options = new List<(string, Action)>();
        private string _title = "";
        private Vector2 _position;
        private bool _open;
        private GUIStyle _row;
        private GUIStyle _header;

        public static bool IsOpen => _instance != null && _instance._open;

        /// <summary>Screen rect of the open menu in GUI coordinates (top-left origin), or empty.</summary>
        public static Rect OpenRect => IsOpen ? _instance.MenuRect : Rect.zero;

        private Rect MenuRect => new Rect(_position.x, _position.y, Width, RowHeight * (_options.Count + 1) + 4f);

        private void Awake() => _instance = this;

        /// <summary>Opens a menu at a GUI position (top-left origin).</summary>
        public static void Open(Vector2 guiPosition, string title, List<(string Label, Action Action)> options)
        {
            if (_instance == null || options.Count == 0) return;
            PopupMenu m = _instance;
            m._options.Clear();
            m._options.AddRange(options);
            m._title = title;
            Rect r = new Rect(guiPosition.x - Width / 2f, guiPosition.y - 4f, Width, RowHeight * (options.Count + 1) + 4f);
            r.x = Mathf.Clamp(r.x, 0f, Screen.width - r.width);
            r.y = Mathf.Clamp(r.y, 0f, Screen.height - r.height);
            m._position = r.position;
            m._open = true;
        }

        public static void Close()
        {
            if (_instance != null) _instance._open = false;
        }

        private void OnGUI()
        {
            if (!_open) return;
            GUI.depth = -10; // above everything else
            if (_row == null)
            {
                _row = new GUIStyle(GUI.skin.label) { fontSize = 12, padding = new RectOffset(6, 4, 2, 2) };
                _row.normal.textColor = Color.white;
                _row.hover.textColor = new Color(1f, 0.85f, 0.3f);
                _header = new GUIStyle(_row) { fontStyle = FontStyle.Bold };
                _header.normal.textColor = new Color(0.8f, 0.75f, 0.6f);
            }

            Rect r = MenuRect;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && !r.Contains(e.mousePosition))
            {
                _open = false;
                return;
            }

            Fill(r, new Color(0.08f, 0.07f, 0.06f, 0.96f));
            GUI.Label(new Rect(r.x, r.y + 2f, r.width, RowHeight), _title, _header);
            for (int i = 0; i < _options.Count; i++)
            {
                var row = new Rect(r.x, r.y + 2f + RowHeight * (i + 1), r.width, RowHeight);
                if (row.Contains(e.mousePosition)) Fill(row, new Color(1f, 1f, 1f, 0.08f));
                if (GUI.Button(row, _options[i].Label, _row))
                {
                    _open = false;
                    _options[i].Action();
                    return;
                }
            }
        }

        private static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
