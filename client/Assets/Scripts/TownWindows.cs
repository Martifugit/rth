using System.Collections.Generic;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Windows the server opens: the dialogue box, the bank and shops. While the bank or a shop is
    /// open, clicking items in the pack deposits or sells them instead (see <see cref="CombatHud"/>).
    /// </summary>
    public sealed class TownWindows : MonoBehaviour
    {
        public GameSession session;

        private const float Cell = 44f;
        private GUIStyle _text;
        private GUIStyle _speaker;
        private GUIStyle _option;
        private GUIStyle _title;
        private GUIStyle _small;
        private GUIStyle _count;
        private GUIStyle _footer;
        private Vector2 _bankScroll;

        private void OnGUI()
        {
            if (session == null || !session.IsInGame) return;
            GUI.depth = -1;
            EnsureStyles();
            if (session.Dialogue != null) DrawDialogue(session.Dialogue);
            if (session.OpenInterface == InterfaceKind.Bank) DrawBank();
            else if (session.OpenInterface == InterfaceKind.Shop && session.Shop != null) DrawShop(session.Shop);
        }

        // ------------------------------------------------------------------ dialogue

        private void DrawDialogue(DialogueMessage line)
        {
            const float width = 520f;
            float textHeight = _text.CalcHeight(new GUIContent(line.Text), width - 24f);
            float height = 40f + textHeight + Mathf.Max(1, line.Options.Count) * 22f + 10f;
            Rect r = CombatHud.Claim(new Rect((Screen.width - width) / 2f, Screen.height - height - 186f, width, height));
            Fill(r, new Color(0.09f, 0.08f, 0.07f, 0.95f));
            Outline(r, new Color(0.55f, 0.45f, 0.3f));

            GUI.Label(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, 20f), line.Speaker, _speaker);
            GUI.Label(new Rect(r.x + 12f, r.y + 30f, r.width - 24f, textHeight), line.Text, _text);

            float y = r.y + 34f + textHeight;
            if (line.Options.Count == 0)
            {
                if (GUI.Button(new Rect(r.x + 12f, y, r.width - 24f, 20f), "Click here to continue  (space)", _option)) session.ChooseDialogue(-1);
                return;
            }
            for (int i = 0; i < line.Options.Count; i++)
            {
                var row = new Rect(r.x + 12f, y + i * 22f, r.width - 24f, 20f);
                if (row.Contains(Event.current.mousePosition)) Fill(row, new Color(1f, 1f, 1f, 0.06f));
                if (GUI.Button(row, (i + 1) + ". " + line.Options[i], _option)) session.ChooseDialogue(i);
            }
        }

        // ------------------------------------------------------------------ bank

        private void DrawBank()
        {
            const int cols = 8;
            float width = cols * (Cell + 2f) + 24f;
            const float height = 360f;
            Rect r = CombatHud.Claim(new Rect((Screen.width - width) / 2f - 120f, 70f, width, height));
            Frame(r, session.InterfaceTitle);

            int used = 0;
            foreach (ItemSlot s in session.Bank)
                if (!s.IsEmpty) used++;
            GUI.Label(new Rect(r.x + 12f, r.yMax - 28f, 300f, 20f), used + " / " + session.Bank.Count + " slots. Click your pack to deposit.", _footer);
            if (GUI.Button(new Rect(r.xMax - 132f, r.yMax - 30f, 120f, 22f), "Deposit pack")) session.BankDeposit(-1, 0);

            var view = new Rect(r.x + 10f, r.y + 32f, r.width - 20f, r.height - 68f);
            int rows = Mathf.CeilToInt(Mathf.Max(1, used) / (float)cols);
            var content = new Rect(0f, 0f, view.width - 18f, rows * (Cell + 2f));
            _bankScroll = GUI.BeginScrollView(view, _bankScroll, content);
            Event e = Event.current;
            for (int i = 0; i < session.Bank.Count; i++)
            {
                ItemSlot slot = session.Bank[i];
                if (slot.IsEmpty) continue;
                var cell = new Rect((i % cols) * (Cell + 2f), (i / cols) * (Cell + 2f), Cell, Cell);
                ItemDef def = session.Item(slot.ItemId);
                DrawItem(cell, def, slot);
                if (!cell.Contains(e.mousePosition) || e.type != EventType.MouseDown) continue;
                int index = i;
                string name = def?.Name ?? slot.ItemId;
                if (e.button == 0) session.BankWithdraw(index, 1);
                else if (e.button == 1)
                {
                    Vector2 screen = GUIUtility.GUIToScreenPoint(e.mousePosition);
                    PopupMenu.Open(screen, name, new List<(string, System.Action)>
                    {
                        ("Withdraw 1", () => session.BankWithdraw(index, 1)),
                        ("Withdraw 5", () => session.BankWithdraw(index, 5)),
                        ("Withdraw 10", () => session.BankWithdraw(index, 10)),
                        ("Withdraw all", () => session.BankWithdraw(index, 0)),
                    });
                }
                e.Use();
            }
            GUI.EndScrollView();
        }

        // ------------------------------------------------------------------ shop

        private void DrawShop(ShopMessage shop)
        {
            const int cols = 6;
            float width = cols * (Cell + 30f) + 24f;
            int rows = Mathf.CeilToInt(Mathf.Max(1, shop.Entries.Count) / (float)cols);
            float height = 70f + rows * (Cell + 20f);
            Rect r = CombatHud.Claim(new Rect((Screen.width - width) / 2f - 120f, 70f, width, height));
            Frame(r, shop.Title);
            GUI.Label(new Rect(r.x + 12f, r.yMax - 26f, r.width - 24f, 20f),
                "Prices in " + shop.CurrencyName.ToLowerInvariant() + "." + (shop.SellPercent > 0 ? " Click items in your pack to sell them." : ""), _footer);

            Event e = Event.current;
            for (int i = 0; i < shop.Entries.Count; i++)
            {
                ShopEntry entry = shop.Entries[i];
                var cell = new Rect(r.x + 12f + (i % cols) * (Cell + 30f), r.y + 34f + (i / cols) * (Cell + 20f), Cell + 26f, Cell);
                ItemDef def = session.Item(entry.ItemId);
                DrawItem(new Rect(cell.x + 13f, cell.y, Cell, Cell), def, new ItemSlot { ItemId = entry.ItemId, Count = entry.Stock < 0 ? 0 : entry.Stock });
                GUI.Label(new Rect(cell.x, cell.yMax, cell.width, 16f), entry.Price.ToString(), _count);
                if (entry.Stock == 0) Fill(new Rect(cell.x + 13f, cell.y, Cell, Cell), new Color(0f, 0f, 0f, 0.55f));

                if (!cell.Contains(e.mousePosition) || e.type != EventType.MouseDown) continue;
                int index = i;
                string name = def?.Name ?? entry.ItemId;
                if (e.button == 0) session.ShopBuy(index, 1);
                else if (e.button == 1)
                {
                    PopupMenu.Open(GUIUtility.GUIToScreenPoint(e.mousePosition), name + " (" + entry.Price + " each)", new List<(string, System.Action)>
                    {
                        ("Buy 1", () => session.ShopBuy(index, 1)),
                        ("Buy 5", () => session.ShopBuy(index, 5)),
                        ("Buy 10", () => session.ShopBuy(index, 10)),
                    });
                }
                e.Use();
            }
        }

        // ------------------------------------------------------------------ drawing

        private void Frame(Rect r, string title)
        {
            Fill(r, new Color(0.11f, 0.1f, 0.09f, 0.96f));
            Outline(r, new Color(0.55f, 0.45f, 0.3f));
            GUI.Label(new Rect(r.x, r.y + 6f, r.width, 20f), title, _title);
            if (GUI.Button(new Rect(r.xMax - 26f, r.y + 5f, 20f, 20f), "x")) session.CloseInterface();
        }

        private void DrawItem(Rect r, ItemDef def, ItemSlot slot)
        {
            Fill(r, new Color(0.16f, 0.15f, 0.13f));
            Fill(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f), WorldView.ParseColor(def?.Color) * 0.85f);
            GUI.Label(new Rect(r.x + 1f, r.y + 8f, r.width - 2f, r.height - 8f), def?.Name ?? slot.ItemId, _small);
            if (slot.Count > 1) GUI.Label(new Rect(r.x + 2f, r.y, r.width, 14f), slot.Count.ToString(), _count);
        }

        private static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void Outline(Rect r, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, 2f), c);
            Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), c);
            Fill(new Rect(r.x, r.y, 2f, r.height), c);
            Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), c);
        }

        private void EnsureStyles()
        {
            if (_text != null) return;
            _text = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _text.normal.textColor = new Color(0.93f, 0.9f, 0.82f);
            _speaker = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _speaker.normal.textColor = new Color(1f, 0.8f, 0.35f);
            _option = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _option.normal.textColor = new Color(0.55f, 0.8f, 1f);
            _option.hover.textColor = Color.white;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = new Color(1f, 0.85f, 0.5f);
            _small = new GUIStyle(GUI.skin.label) { fontSize = 8, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            _small.normal.textColor = Color.white;
            _footer = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            _footer.normal.textColor = new Color(0.8f, 0.78f, 0.7f);
            _count = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            _count.normal.textColor = new Color(1f, 1f, 0.3f);
        }
    }
}
