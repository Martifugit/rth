using System.Collections.Generic;
using Rth.Shared.Combat;
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

        /// <summary>Whether a screen position (bottom-left origin, as the Input System reports) is over the HUD.</summary>
        public static bool IsPointerOverHud(Vector2 screenPosition)
        {
            var gui = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            foreach (Rect r in HudRects)
                if (r.Contains(gui)) return true;
            return false;
        }

        private Rect Claim(Rect rect)
        {
            HudRects.Add(rect);
            return rect;
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Layout) HudRects.Clear();
            if (session == null || !session.IsInGame) return;
            EnsureStyles();

            DrawStatusPanel();
            PlayerStateMessage state = session.PlayerState;
            ContentInfoMessage content = session.Content;
            if (state == null || content == null) return;

            DrawTarget(state);
            float barTop = DrawActionBar(state, content);
            DrawResources(state, content, barTop);
            DrawLog();
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
        private float DrawActionBar(PlayerStateMessage state, ContentInfoMessage content)
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

                bool usable = ability.CheckRequirements(state.Phlegm, state.Bile) == null;
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
            lines.Add(string.Join(" | ", costs));
            return string.Join("\n", lines);
        }

        private void DrawResources(PlayerStateMessage state, ContentInfoMessage content, float barTop)
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
        private void DrawBileAxis(Rect r, int bile, ContentInfoMessage content)
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

        private static string BileSummary(PlayerStateMessage state, ContentInfoMessage content, float tickSeconds)
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
            _assayer = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, fontStyle = FontStyle.Italic, alignment = TextAnchor.UpperCenter };
            _assayer.normal.textColor = new Color(0.88f, 0.84f, 0.95f);
        }
    }
}
