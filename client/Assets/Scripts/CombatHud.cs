using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// The in-game interface (IMGUI for now): resources and the bile axis, the action bar, deflection,
    /// the target, the message log and the Assayer. It only reads <see cref="GameSession"/> state and
    /// calls its intent methods.
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        public GameSession session;
        public WorldView world;

        private const float SlotSize = 58f;
        private static readonly List<Rect> HudRects = new List<Rect>();

        private Texture2D _white;
        private GUIStyle _small;
        private GUIStyle _center;
        private GUIStyle _slotName;
        private GUIStyle _log;
        private GUIStyle _assayer;
        private GUIStyle _count;
        private GUIStyle _itemName;
        private GUIStyle _questName;
        private GUIStyle _tabLabel;
        private GUIStyle _hover;

        /// <summary>Whether a screen position (bottom-left origin, as the Input System reports) is over the HUD.</summary>
        public static bool IsPointerOverHud(Vector2 screenPosition)
        {
            var gui = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
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

            DrawStatusPanel();
            PlayerStateMessage state = session.PlayerState;
            ClientContent content = session.Content;
            if (state == null || content == null) return;

            DrawTarget(state);
            float barTop = DrawActionBar(state, content);
            DrawResources(state, content, barTop);
            DrawLog();
            DrawItemPanel(content);
            DrawHoverText();
            DrawAssayer(state);
        }

        // ------------------------------------------------------------------ panels

        private void DrawStatusPanel()
        {
            EntityView local = world.Local;
            GUILayout.BeginArea(Claim(new Rect(10f, 10f, 240f, 92f)), GUI.skin.box);
            GUILayout.Label(local != null ? session.PlayerName + " at " + local.Tile : "Loading...");
            if (GUILayout.Button(session.Running ? "Run: ON  (R)" : "Run: off  (R)")) session.ToggleRun();
            if (GUILayout.Button("Log out")) session.Logout();
            GUILayout.EndArea();
        }

        private void DrawTarget(PlayerStateMessage state)
        {
            EntityView target = state.TargetId != 0 ? world.Find(state.TargetId) : null;
            if (target == null) return;
            Rect r = Claim(new Rect(Screen.width / 2f - 130f, 10f, 260f, 44f));
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x, r.y + 2f, r.width, 18f), target.DisplayName, _center);
            Bar(new Rect(r.x + 10f, r.y + 22f, r.width - 20f, 14f), target.HealthPercent / 100f, new Color(0.15f, 0.75f, 0.15f), target.HealthPercent + "%");
        }

        /// <summary>Draws the ability slots along the bottom; returns the y of their top edge.</summary>
        private float DrawActionBar(PlayerStateMessage state, ClientContent content)
        {
            int slots = content.ActionBar.Count;
            float width = slots * (SlotSize + 4f) + 4f;
            Rect bar = Claim(new Rect((Screen.width - width) / 2f, Screen.height - SlotSize - 14f, width, SlotSize + 8f));
            GUI.Box(bar, GUIContent.none);

            float now = session.EstimatedTick;
            string tooltip = null;
            for (int i = 0; i < slots; i++)
            {
                AbilityDef ability = session.Ability(content.ActionBar[i]);
                if (ability == null) continue;
                var slot = new Rect(bar.x + 4f + i * (SlotSize + 4f), bar.y + 4f, SlotSize, SlotSize);

                bool usable = session.AbilityProblem(ability) == null;
                Color tint = ability.BileShift > 0 ? new Color(0.62f, 0.5f, 0.12f)
                    : ability.BileShift < 0 ? new Color(0.26f, 0.17f, 0.34f)
                    : new Color(0.3f, 0.36f, 0.3f);
                if (!usable) tint = Color.Lerp(tint, Color.black, 0.6f);
                Fill(slot, tint);

                if (GUI.Button(slot, GUIContent.none, GUIStyle.none)) session.UseAbility(ability.Id);
                GUI.Label(new Rect(slot.x + 3f, slot.y + 1f, 20f, 16f), (i + 1).ToString(), _small);
                string kind = ability.Type == AbilityType.Threshold ? "T" : ability.Type == AbilityType.Ultimate ? "U" : "";
                GUI.Label(new Rect(slot.xMax - 14f, slot.y + 1f, 14f, 16f), kind, _small);
                GUI.Label(new Rect(slot.x + 2f, slot.y + 14f, slot.width - 4f, slot.height - 14f), ability.Name, _slotName);

                // Cooldown: whichever is later, the ability's own or the global one.
                float ready = state.GlobalCooldownReadyTick;
                foreach (CooldownState cd in state.Cooldowns)
                    if (cd.AbilityId == ability.Id) ready = Mathf.Max(ready, cd.ReadyTick);
                float remainingTicks = ready - now;
                if (remainingTicks > 0f)
                {
                    float total = Mathf.Max(ability.CooldownTicks, 3);
                    float frac = Mathf.Clamp01(remainingTicks / total);
                    Fill(new Rect(slot.x, slot.y, slot.width, slot.height * frac), new Color(0f, 0f, 0f, 0.6f));
                    if (remainingTicks * session.TickSeconds > 0.95f)
                        GUI.Label(new Rect(slot.x, slot.y + slot.height - 20f, slot.width, 18f), Mathf.CeilToInt(remainingTicks * session.TickSeconds) + "s", _center);
                }

                if (state.QueuedAbility == ability.Id) Outline(slot, new Color(1f, 0.6f, 0.1f), 3f);
                if (slot.Contains(Event.current.mousePosition)) tooltip = Describe(ability, session.TickSeconds);
            }

            if (tooltip != null)
            {
                float h = _small.CalcHeight(new GUIContent(tooltip), 300f) + 8f;
                var tip = new Rect(Mathf.Clamp(Event.current.mousePosition.x - 150f, 4f, Screen.width - 304f), bar.y - h - 120f, 300f, h);
                Fill(tip, new Color(0.08f, 0.08f, 0.1f, 0.95f));
                GUI.Label(new Rect(tip.x + 4f, tip.y + 4f, tip.width - 8f, tip.height - 8f), tooltip, _small);
            }
            return bar.y;
        }

        private static string Describe(AbilityDef a, float tickSeconds)
        {
            var lines = new List<string> { a.Name + "  (" + a.Type + ")", a.Description };
            var costs = new List<string>();
            if (a.PhlegmGain > 0) costs.Add("+" + a.PhlegmGain + " phlegm");
            if (a.PhlegmRequired > 0) costs.Add("needs " + a.PhlegmRequired + "% phlegm, spends " + a.PhlegmCost);
            if (a.BileShift != 0) costs.Add("bile " + (a.BileShift > 0 ? "+" : "") + a.BileShift);
            if (a.BileTowardZero > 0) costs.Add("bile " + a.BileTowardZero + " towards balance");
            if (a.BileMin > -100) costs.Add("bile at least " + (a.BileMin > 0 ? "+" : "") + a.BileMin);
            if (a.BileMax < 100) costs.Add("bile at most " + a.BileMax);
            costs.Add(a.DamageMin + "-" + a.DamageMax + "% damage");
            costs.Add("range " + a.Range + ", cooldown " + (a.CooldownTicks * tickSeconds).ToString("0.#") + "s");
            foreach (Rth.Shared.Skills.SkillRequirement r in a.Requirements) costs.Add("needs " + r.Skill + " " + r.Level);
            lines.Add(string.Join(" | ", costs));
            return string.Join("\n", lines);
        }

        private void DrawResources(PlayerStateMessage state, ClientContent content, float barTop)
        {
            const float width = 470f;
            const float rowH = 18f;
            float x = (Screen.width - width) / 2f;
            float y = barTop - 4f - 5 * (rowH + 4f);
            Rect panel = Claim(new Rect(x - 6f, y - 6f, width + 12f, 5 * (rowH + 4f) + 8f));
            GUI.Box(panel, GUIContent.none);

            Bar(new Rect(x, y, width, rowH), state.Blood / (float)Mathf.Max(1, state.MaxBlood), new Color(0.75f, 0.1f, 0.1f), "Blood " + state.Blood + " / " + state.MaxBlood);
            y += rowH + 4f;
            Bar(new Rect(x, y, width, rowH), state.Phlegm / (float)Mathf.Max(1, content.MaxPhlegm), new Color(0.55f, 0.72f, 0.55f), "Phlegm " + state.Phlegm + "%");
            y += rowH + 4f;

            // Breath, with the three deflection toggles beside it.
            float breathW = width - 3 * 64f;
            Bar(new Rect(x, y, breathW, rowH), state.Breath / (float)Mathf.Max(1, state.MaxBreath), new Color(0.55f, 0.75f, 0.95f), "Breath " + state.Breath + " / " + state.MaxBreath);
            DeflectButton(new Rect(x + breathW + 4f, y, 60f, rowH), state, CombatStyle.Melee, "Melee Z");
            DeflectButton(new Rect(x + breathW + 68f, y, 60f, rowH), state, CombatStyle.Ranged, "Range X");
            DeflectButton(new Rect(x + breathW + 132f, y, 60f, rowH), state, CombatStyle.Occult, "Occult C");
            y += rowH + 4f;

            DrawBileAxis(new Rect(x, y, width, rowH), state.Bile, content);
            y += rowH + 4f;

            GUI.Label(new Rect(x, y, width, rowH), BileSummary(state, content, session.TickSeconds), _center);
        }

        private void DeflectButton(Rect r, PlayerStateMessage state, CombatStyle style, string label)
        {
            bool on = state.Deflect == style;
            Fill(r, on ? new Color(0.4f, 0.6f, 0.95f) : new Color(0.2f, 0.22f, 0.26f));
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) session.ToggleDeflect(style);
            GUI.Label(r, label, _center);
        }

        /// <summary>A centred axis: black bile to the left, yellow bile to the right, marks at the passive and extreme thresholds.</summary>
        private void DrawBileAxis(Rect r, int bile, ClientContent content)
        {
            float half = r.width / 2f;
            float mid = r.x + half;
            Fill(r, new Color(0.12f, 0.12f, 0.12f));
            float limit = Mathf.Max(1, content.BileLimit);
            float fill = Mathf.Abs(bile) / limit * half;
            if (bile > 0) Fill(new Rect(mid, r.y, fill, r.height), new Color(0.9f, 0.75f, 0.15f));
            else if (bile < 0) Fill(new Rect(mid - fill, r.y, fill, r.height), new Color(0.35f, 0.2f, 0.5f));

            foreach (int mark in new[] { content.BilePassiveThreshold, content.BileExtreme })
            {
                float off = mark / limit * half;
                Color c = mark == content.BileExtreme ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 1f, 1f, 0.7f);
                Fill(new Rect(mid + off - 1f, r.y, 2f, r.height), c);
                Fill(new Rect(mid - off - 1f, r.y, 2f, r.height), c);
            }
            Fill(new Rect(mid - 1f, r.y - 2f, 2f, r.height + 4f), Color.white);
            GUI.Label(r, "Bile " + (bile > 0 ? "+" : "") + bile, _center);
        }

        private static string BileSummary(PlayerStateMessage state, ClientContent content, float tickSeconds)
        {
            var rules = new CombatRules
            {
                BileLimit = content.BileLimit,
                BilePassiveThreshold = content.BilePassiveThreshold,
                BileExtreme = content.BileExtreme,
            };
            var parts = new List<string>();
            if (rules.IsCholeric(state.Bile)) parts.Add("Choleric: +" + rules.CholericBonusPercent(state.Bile) + "% damage");
            else if (rules.IsMelancholic(state.Bile)) parts.Add("Melancholic: " + rules.MelancholicWardPercent(state.Bile) + "% less damage taken");
            else parts.Add("Balanced");
            if (rules.IsFevered(state.Bile)) parts.Add("FEVER: your blood is burning");
            if (rules.IsTorpid(state.Bile)) parts.Add("TORPOR: phlegm builds slowly");
            foreach (EffectState e in state.Effects)
            {
                float secs = Mathf.Max(0f, (e.EndTick - (float)state.Tick) * tickSeconds);
                parts.Add(e.Effect + " " + e.Magnitude + "% (" + secs.ToString("0") + "s)");
            }
            if (state.InCombat) parts.Add("in combat");
            return string.Join("   ", parts);
        }

        private void DrawLog()
        {
            const int lines = 7;
            Rect r = Claim(new Rect(10f, Screen.height - 18f * lines - 24f, 380f, 18f * lines + 14f));
            GUI.Box(r, GUIContent.none);
            List<GameSession.LogLine> log = session.Log;
            int first = Mathf.Max(0, log.Count - lines);
            float y = r.y + 6f;
            for (int i = first; i < log.Count; i++)
            {
                _log.normal.textColor = log[i].Kind == TextKind.Warning ? new Color(1f, 0.65f, 0.3f)
                    : log[i].Kind == TextKind.Assayer ? new Color(0.8f, 0.7f, 1f)
                    : Color.white;
                string text = log[i].Kind == TextKind.Assayer ? "The Assayer: " + log[i].Text : log[i].Text;
                GUI.Label(new Rect(r.x + 6f, y, r.width - 12f, 18f), text, _log);
                y += 18f;
            }
        }

        private void DrawAssayer(PlayerStateMessage state)
        {
            if (string.IsNullOrEmpty(session.AssayerText)) return;
            Rect r = Claim(new Rect(Screen.width / 2f - 230f, Screen.height / 2f - 110f, 460f, 170f));
            Fill(r, new Color(0.06f, 0.05f, 0.09f, 0.94f));
            Outline(r, new Color(0.6f, 0.5f, 0.75f), 2f);
            GUI.Label(new Rect(r.x, r.y + 10f, r.width, 22f), "THE ASSAYER", _center);
            GUI.Label(new Rect(r.x + 18f, r.y + 36f, r.width - 36f, r.height - 76f), session.AssayerText, _assayer);
            if (GUI.Button(new Rect(r.x + r.width / 2f - 60f, r.yMax - 34f, 120f, 24f), "Return")) session.AssayerText = null;
        }

        // ------------------------------------------------------------------ items

        private enum ItemTab
        {
            Inventory,
            Worn,
            Stats,
            Skills,
            Quests,
        }

        private ItemTab _tab;
        private int _dragFrom = -1;

        private void DrawItemPanel(ClientContent content)
        {
            const float slotW = 48f;
            const float slotH = 40f;
            const int cols = 4;
            int rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(session.Inventory.Count, 28) / (float)cols));
            float width = cols * (slotW + 2f) + 10f;
            float height = 26f + rows * (slotH + 2f) + 10f;
            Rect panel = Claim(new Rect(Screen.width - width - 10f, Screen.height - height - 10f, width, height));
            GUI.Box(panel, GUIContent.none);

            string[] tabs = { "Pack", "Worn", "Stats", "Skills", "Quests" };
            float tabW = (width - 10f) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                var tabRect = new Rect(panel.x + 5f + i * tabW, panel.y + 4f, tabW - 2f, 18f);
                Fill(tabRect, (int)_tab == i ? new Color(0.45f, 0.35f, 0.2f) : new Color(0.18f, 0.16f, 0.14f));
                if (GUI.Button(tabRect, GUIContent.none, GUIStyle.none)) _tab = (ItemTab)i;
                GUI.Label(tabRect, tabs[i], _tabLabel);
            }

            var body = new Rect(panel.x + 5f, panel.y + 26f, width - 10f, height - 30f);
            string tooltip = null;
            switch (_tab)
            {
                case ItemTab.Inventory:
                    tooltip = DrawInventory(body, cols, slotW, slotH);
                    break;
                case ItemTab.Worn:
                    tooltip = DrawWorn(body);
                    break;
                case ItemTab.Quests:
                    tooltip = DrawQuests(body);
                    break;
                case ItemTab.Skills:
                    tooltip = DrawSkills(body, content);
                    break;
                case ItemTab.Stats:
                    DrawStats(body);
                    break;
            }

            if (tooltip != null)
            {
                float h = _small.CalcHeight(new GUIContent(tooltip), 220f) + 8f;
                var tip = new Rect(panel.x - 230f, Mathf.Clamp(Event.current.mousePosition.y - h / 2f, 4f, Screen.height - h - 4f), 224f, h);
                Fill(tip, new Color(0.08f, 0.08f, 0.1f, 0.95f));
                GUI.Label(new Rect(tip.x + 4f, tip.y + 4f, tip.width - 8f, tip.height - 8f), tooltip, _small);
            }
        }

        /// <summary>The 4-column pack. Click uses an item's first action, right-click lists them, drag moves it.</summary>
        private string DrawInventory(Rect body, int cols, float slotW, float slotH)
        {
            Event e = Event.current;
            string tooltip = null;
            for (int i = 0; i < session.Inventory.Count; i++)
            {
                var r = new Rect(body.x + (i % cols) * (slotW + 2f), body.y + (i / cols) * (slotH + 2f), slotW, slotH);
                ItemSlot slot = session.Inventory[i];
                ItemDef def = slot.IsEmpty ? null : session.Item(slot.ItemId);
                DrawSlot(r, slot, def, _dragFrom == i);
                if (!r.Contains(e.mousePosition)) continue;
                if (def != null) tooltip = Describe(def, slot.Count);

                if (e.type == EventType.MouseDown && e.button == 0 && def != null)
                {
                    _dragFrom = i;
                    e.Use();
                }
                else if (e.type == EventType.MouseUp && e.button == 0 && _dragFrom >= 0)
                {
                    if (_dragFrom == i) DefaultAction(ContainerKind.Inventory, i, def);
                    else session.MoveItem(_dragFrom, i);
                    _dragFrom = -1;
                    e.Use();
                }
                else if (e.type == EventType.MouseDown && e.button == 1 && def != null)
                {
                    OpenItemMenu(ContainerKind.Inventory, i, def, e.mousePosition);
                    e.Use();
                }
            }
            if (e.type == EventType.MouseUp && e.button == 0) _dragFrom = -1;
            return tooltip;
        }

        private string DrawWorn(Rect body)
        {
            Event e = Event.current;
            string tooltip = null;
            string[] slotNames = System.Enum.GetNames(typeof(EquipSlot));
            float rowH = Mathf.Min(26f, body.height / slotNames.Length);
            for (int i = 0; i < slotNames.Length; i++)
            {
                var r = new Rect(body.x, body.y + i * rowH, body.width, rowH - 2f);
                ItemSlot slot = i < session.Equipment.Count ? session.Equipment[i] : default;
                ItemDef def = slot.IsEmpty ? null : session.Item(slot.ItemId);
                Fill(r, new Color(0.14f, 0.13f, 0.12f));
                if (def != null) Fill(new Rect(r.x + 2f, r.y + 2f, rowH - 6f, r.height - 4f), WorldView.ParseColor(def.Color));
                GUI.Label(new Rect(r.x + rowH, r.y, r.width - rowH, r.height), slotNames[i] + ": " + (def?.Name ?? (slot.IsEmpty ? "-" : slot.ItemId)), _small);
                if (def == null || !r.Contains(e.mousePosition)) continue;
                tooltip = Describe(def, 1);
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    DefaultAction(ContainerKind.Equipment, i, def);
                    e.Use();
                }
                else if (e.type == EventType.MouseDown && e.button == 1)
                {
                    OpenItemMenu(ContainerKind.Equipment, i, def, e.mousePosition);
                    e.Use();
                }
            }
            return tooltip;
        }

        private void DrawStats(Rect body)
        {
            float y = body.y;
            foreach (string stat in StatIds.All)
            {
                session.Stats.TryGetValue(stat, out int value);
                GUI.Label(new Rect(body.x + 4f, y, body.width - 8f, 18f), StatLabel(stat) + ": " + value + StatUnit(stat), _small);
                y += 20f;
            }

            // The current task and point balances.
            y += 8f;
            TaskMessage task = session.CurrentTask;
            string taskText = task == null || string.IsNullOrEmpty(task.Description) ? "Task: none" : "Task (" + task.GiverName + "): " + task.Description;
            float h = _small.CalcHeight(new GUIContent(taskText), body.width - 8f);
            GUI.Label(new Rect(body.x + 4f, y, body.width - 8f, h), taskText, _small);
            y += h + 4f;
            if (task != null)
            {
                foreach (KeyValuePair<string, int> p in task.Points)
                {
                    GUI.Label(new Rect(body.x + 4f, y, body.width - 8f, 18f), p.Key + ": " + p.Value, _small);
                    y += 18f;
                }
            }
        }

        /// <summary>The quest journal: names coloured by state (red not started, yellow in progress, green done); hover for the entry.</summary>
        private string DrawQuests(Rect body)
        {
            string tooltip = null;
            float y = body.y;
            foreach (QuestEntry quest in session.Quests)
            {
                var row = new Rect(body.x, y, body.width, 22f);
                Fill(row, new Color(0.14f, 0.13f, 0.12f));
                _questName.normal.textColor = quest.State == QuestState.Complete ? new Color(0.4f, 0.9f, 0.4f)
                    : quest.State == QuestState.InProgress ? new Color(1f, 0.9f, 0.3f)
                    : new Color(0.95f, 0.4f, 0.35f);
                GUI.Label(new Rect(row.x + 4f, row.y + 2f, row.width - 8f, 18f), quest.Name + (string.IsNullOrEmpty(quest.Progress) ? "" : "  (" + quest.Progress + ")"), _questName);
                if (row.Contains(Event.current.mousePosition)) tooltip = quest.Name + "\n\n" + quest.Journal;
                y += 24f;
            }
            if (session.Quests.Count == 0) GUI.Label(new Rect(body.x + 4f, y, body.width - 8f, 18f), "No quests yet.", _small);
            return tooltip;
        }

        /// <summary>Each skill's level, with a bar of progress through the current level.</summary>
        private string DrawSkills(Rect body, ClientContent content)
        {
            string tooltip = null;
            float y = body.y;
            foreach (Rth.Shared.Skills.SkillDef skill in content.Skills)
            {
                session.SkillXp.TryGetValue(skill.Id, out long xp);
                int level = content.XpCurve.LevelForXp(xp);
                long from = content.XpCurve.XpForLevel(level);
                long to = content.XpCurve.XpForLevel(level + 1);
                float progress = to > from ? (xp - from) / (float)(to - from) : 1f;

                var row = new Rect(body.x, y, body.width, 34f);
                Fill(row, new Color(0.14f, 0.13f, 0.12f));
                GUI.Label(new Rect(row.x + 4f, row.y + 1f, row.width - 40f, 16f), skill.Name, _small);
                GUI.Label(new Rect(row.xMax - 36f, row.y + 1f, 34f, 16f), level.ToString(), _center);
                Bar(new Rect(row.x + 4f, row.y + 19f, row.width - 8f, 11f), progress, new Color(0.35f, 0.6f, 0.3f), "");
                if (row.Contains(Event.current.mousePosition))
                    tooltip = skill.Name + " " + level + "\n" + xp + " xp" + (level < content.XpCurve.MaxLevel ? ", " + (to - xp) + " to level " + (level + 1) : "") +
                              (string.IsNullOrEmpty(skill.Description) ? "" : "\n" + skill.Description);
                y += 38f;
            }
            return tooltip;
        }

        private static string StatLabel(string stat) => stat switch
        {
            StatIds.AbilityDamage => "Ability damage",
            StatIds.Accuracy => "Accuracy",
            StatIds.DamageBonus => "Damage bonus",
            StatIds.Ward => "Ward",
            StatIds.MaxBlood => "Max blood",
            StatIds.MaxBreath => "Max breath",
            _ => stat,
        };

        private static string StatUnit(string stat) =>
            stat == StatIds.Accuracy || stat == StatIds.DamageBonus || stat == StatIds.Ward ? "%" : "";

        private void DrawSlot(Rect r, ItemSlot slot, ItemDef def, bool dragging)
        {
            Fill(r, dragging ? new Color(0.3f, 0.25f, 0.15f) : new Color(0.14f, 0.13f, 0.12f));
            if (slot.IsEmpty) return;
            Color color = def != null ? WorldView.ParseColor(def.Color) : Color.magenta;
            Fill(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f), color * 0.85f);
            string name = def?.Name ?? slot.ItemId;
            GUI.Label(new Rect(r.x + 1f, r.y + 8f, r.width - 2f, r.height - 8f), name, _itemName);
            if (slot.Count > 1 || (def != null && def.Stackable))
                GUI.Label(new Rect(r.x + 2f, r.y, r.width, 14f), FormatCount(slot.Count), _count);
        }

        private static string FormatCount(int n) => n >= 10_000_000 ? n / 1_000_000 + "M" : n >= 100_000 ? n / 1000 + "K" : n.ToString();

        /// <summary>What clicking a pack item does: deposit with the bank open, sell in a shop, otherwise its first action.</summary>
        private void DefaultAction(ContainerKind where, int slot, ItemDef def)
        {
            if (def == null) return;
            if (where == ContainerKind.Inventory && session.OpenInterface == InterfaceKind.Bank)
            {
                session.BankDeposit(slot, 1);
                return;
            }
            if (where == ContainerKind.Inventory && session.OpenInterface == InterfaceKind.Shop)
            {
                session.ShopSell(slot, 1);
                return;
            }
            List<ItemAction> actions = ItemActions.For(def, where);
            if (actions.Count > 0) session.UseItem(where, slot, actions[0].Id);
        }

        private void OpenItemMenu(ContainerKind where, int slot, ItemDef def, Vector2 guiPosition)
        {
            var options = new List<(string Label, System.Action Action)>();
            if (where == ContainerKind.Inventory && session.OpenInterface == InterfaceKind.Bank)
            {
                options.Add(("Deposit 1", () => session.BankDeposit(slot, 1)));
                options.Add(("Deposit 5", () => session.BankDeposit(slot, 5)));
                options.Add(("Deposit all", () => session.BankDeposit(slot, 0)));
            }
            else if (where == ContainerKind.Inventory && session.OpenInterface == InterfaceKind.Shop && session.Shop != null && session.Shop.SellPercent > 0)
            {
                int each = def.Value * session.Shop.SellPercent / 100;
                options.Add(("Sell 1 (" + each + " each)", () => session.ShopSell(slot, 1)));
                options.Add(("Sell 5", () => session.ShopSell(slot, 5)));
                options.Add(("Sell all", () => session.ShopSell(slot, 1000)));
            }
            foreach (ItemAction a in ItemActions.For(def, where))
            {
                string id = a.Id;
                options.Add((a.Label + " " + def.Name, () => session.UseItem(where, slot, id)));
            }
            PopupMenu.Open(guiPosition, def.Name, options);
        }

        private static string Describe(ItemDef def, int count)
        {
            var lines = new List<string> { def.Name + (count > 1 ? " x" + count : "") };
            if (!string.IsNullOrEmpty(def.Examine)) lines.Add(def.Examine);
            if (def.Equipment != null)
            {
                var stats = new List<string>();
                foreach (KeyValuePair<string, int> s in def.Equipment.Stats)
                    stats.Add((s.Value > 0 ? "+" : "") + s.Value + StatUnit(s.Key) + " " + StatLabel(s.Key).ToLowerInvariant());
                lines.Add(def.Equipment.Slot + ": " + string.Join(", ", stats));
            }
            if (def.Consumable != null)
            {
                var effects = new List<string>();
                foreach (EffectOp op in def.Consumable.Effects) effects.Add(op.Op + " " + op.Amount);
                lines.Add(def.Consumable.Verb + ": " + string.Join(", ", effects));
            }
            return string.Join("\n", lines);
        }

        /// <summary>What a left-click would do, top-left like the classic games.</summary>
        private void DrawHoverText()
        {
            if (string.IsNullOrEmpty(session.HoverText) || PopupMenu.IsOpen) return;
            GUI.Label(new Rect(260f, 12f, 400f, 20f), session.HoverText, _hover);
        }

        // ------------------------------------------------------------------ drawing helpers

        private void Bar(Rect r, float fraction, Color color, string label)
        {
            Fill(r, new Color(0.1f, 0.1f, 0.1f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), color);
            GUI.Label(r, label, _center);
        }

        private void Fill(Rect r, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        private void Outline(Rect r, Color color, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), color);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), color);
            Fill(new Rect(r.x, r.y, t, r.height), color);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), color);
        }

        private void EnsureStyles()
        {
            if (_white != null) return;
            _white = Texture2D.whiteTexture;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _small.normal.textColor = Color.white;
            _center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };
            _center.normal.textColor = Color.white;
            _slotName = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10, wordWrap = true };
            _slotName.normal.textColor = Color.white;
            _log = new GUIStyle(GUI.skin.label) { fontSize = 12, clipping = TextClipping.Clip };
            _itemName = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 8, wordWrap = true, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0) };
            _itemName.normal.textColor = Color.white;
            _questName = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
            _tabLabel = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0) };
            _tabLabel.normal.textColor = Color.white;
            _count = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold };
            _count.normal.textColor = new Color(1f, 1f, 0.3f);
            _hover = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _hover.normal.textColor = new Color(1f, 0.95f, 0.75f);
            _assayer = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, fontStyle = FontStyle.Italic, alignment = TextAnchor.UpperCenter };
            _assayer.normal.textColor = new Color(0.88f, 0.84f, 0.95f);
        }
    }
}
