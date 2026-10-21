using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// The client's picture of the world: applies the server's per-tick updates to entity views,
    /// and draws what happens to them (hitsplats, projectiles, health bars, names).
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        private const float HitsplatSeconds = 1.2f;
        private const float HealthBarSeconds = 6f;

        private struct Hitsplat
        {
            public int EntityId;
            public string Text;
            public Color Color;
            public float Time;
            public float Offset;
            public int Row;
        }

        private readonly Dictionary<int, EntityView> _entities = new Dictionary<int, EntityView>();
        private readonly Dictionary<int, GroundItemView> _groundItems = new Dictionary<int, GroundItemView>();

        /// <summary>Looks up item definitions (set by the session from the server's content).</summary>
        public System.Func<string, ItemDef> ItemLookup { get; set; } = _ => null;
        public System.Func<string, AbilityDef> AbilityLookup { get; set; } = _ => null;
        /// <summary>An overhead hymn's icon id, by hymn id.</summary>
        public System.Func<string, string> HymnIconLookup { get; set; } = _ => null;

        public GroundItemView FindGroundItem(int id) => _groundItems.TryGetValue(id, out GroundItemView v) ? v : null;

        public IEnumerable<GroundItemView> GroundItemsOn(Rth.Shared.World.TileCoord tile)
        {
            foreach (GroundItemView g in _groundItems.Values)
                if (g != null && g.Tile == tile) yield return g;
        }
        private readonly List<Hitsplat> _hitsplats = new List<Hitsplat>();
        private float _tickSeconds = 0.6f;
        private GUIStyle _labelStyle;
        private GUIStyle _splatStyle;
        private GUIStyle _xpStyle;
        private Texture2D _white;

        /// <summary>The entity under the pointer, or 0 (set by the session each frame): its name shows.</summary>
        public int HoveredId { get; set; }

        public int LocalId { get; private set; } = -1;
        /// <summary>The entity the local player is fighting, highlighted in the HUD.</summary>
        public int TargetId { get; set; }

        public EntityView Local => Find(LocalId);

        public EntityView Find(int id) => _entities.TryGetValue(id, out EntityView view) ? view : null;

        public IEnumerable<EntityView> Entities => _entities.Values;

        private const float OverheadSeconds = 4f;
        private readonly Dictionary<int, (string Text, float Time)> _overhead = new Dictionary<int, (string, float)>();

        private readonly Dictionary<int, (string Label, float Start, float Seconds)> _markers = new Dictionary<int, (string, float, float)>();

        /// <summary>
        /// A marker over someone's head for a while (owed blood): a red badge with a label and a
        /// bar running down as the time runs out.
        /// </summary>
        public void ShowMarker(int entityId, string label, float seconds)
        {
            _markers[entityId] = (label, Time.time, Mathf.Max(0.1f, seconds));
            EntityView view = Find(entityId);
            if (view != null) BloodMark.Attach(view, seconds);
        }

        private const float XpDropSeconds = 1.6f;
        private readonly List<(string Text, float Time)> _xpDrops = new List<(string, float)>();

        /// <summary>A little "+12 Melee" rising beside the local player; drops for the same skill close together add up.</summary>
        public void ShowXpDrop(string skill, long amount)
        {
            for (int i = _xpDrops.Count - 1; i >= 0; i--)
            {
                (string text, float time) = _xpDrops[i];
                if (Time.time - time > 0.3f || !text.EndsWith(" " + skill)) continue;
                long had = long.TryParse(text.Substring(1, text.IndexOf(' ') - 1), out long n) ? n : 0;
                _xpDrops[i] = ("+" + (had + amount) + " " + skill, time);
                return;
            }
            _xpDrops.Add(("+" + amount + " " + skill, Time.time));
        }

        /// <summary>Shows what someone said above their head for a few seconds.</summary>
        public void ShowOverheadText(int entityId, string text) => _overhead[entityId] = (text, Time.time);

        public void Begin(int localId, float tickSeconds)
        {
            Clear();
            LocalId = localId;
            _tickSeconds = tickSeconds;
        }

        public void Apply(WorldUpdateMessage update)
        {
            foreach (int id in update.Removed) Remove(id);

            foreach (EntityAdded added in update.Added)
            {
                Remove(added.Id);
                Color color = added.Kind == EntityKind.Player
                    ? (added.Id == LocalId ? new Color(0.25f, 0.45f, 0.85f) : ColorFor(added.Id))
                    : FromRgb(added.Color);
                EntityView view = EntityView.Create(added, color, _tickSeconds);
                view.transform.SetParent(transform, true);
                if (!string.IsNullOrEmpty(added.Held)) view.SetHeld(ItemLookup(added.Held));
                view.Overhead = added.Overhead ?? "";
                _entities[added.Id] = view;
            }

            foreach (ObjectEffectEvent effect in update.ObjectEffects)
                ObjectEffects.Play(effect.ObjectType, effect.X, effect.Y, effect.Effect, effect.Arg);

            foreach (AppearanceEvent appearance in update.Appearances)
                if (_entities.TryGetValue(appearance.Id, out EntityView view) && view != null)
                {
                    view.SetHeld(string.IsNullOrEmpty(appearance.Held) ? null : ItemLookup(appearance.Held));
                    view.Overhead = appearance.Overhead ?? "";
                }

            foreach (EntityMoved moved in update.Moved)
                if (_entities.TryGetValue(moved.Id, out EntityView view)) view.AddSteps(moved.Steps, moved.Running);

            // A blow lands a moment after it starts: when the swing connects or the projectile
            // arrives. Its hitsplat, the flinch, the health bar and a death all wait for that.
            var impact = new Dictionary<int, float>(); // target id -> seconds until the last blow on it lands
            var landsAt = new Dictionary<(int, int), float>(); // (source, target) -> seconds until it lands
            foreach (ActionEvent action in update.Actions)
            {
                float delay = ShowAction(action);
                landsAt[(action.SourceId, action.TargetId)] = delay;
                impact[action.TargetId] = Mathf.Max(delay, impact.TryGetValue(action.TargetId, out float d) ? d : 0f);
            }
            foreach (HitEvent hit in update.Hits)
            {
                float delay = landsAt.TryGetValue((hit.SourceId, hit.TargetId), out float d) ? d : 0f;
                Later(delay, () => ShowHit(hit));
            }
            foreach (HealthEvent health in update.Health)
            {
                HealthEvent h = health;
                Later(impact.TryGetValue(h.Id, out float d) ? d : 0f, () =>
                {
                    if (_entities.TryGetValue(h.Id, out EntityView view) && view != null) view.HealthPercent = h.Percent;
                });
            }
            foreach (int id in update.Deaths)
            {
                int dead = id;
                Later(impact.TryGetValue(dead, out float d) ? d : 0f, () =>
                {
                    if (_entities.TryGetValue(dead, out EntityView view) && view != null) view.PlayDeath();
                });
            }

            foreach (int id in update.GroundRemoved) RemoveGroundItem(id);
            foreach (GroundItemAdded added in update.GroundAdded)
            {
                RemoveGroundItem(added.Id);
                ItemDef def = ItemLookup(added.ItemId);
                GroundItemView view = GroundItemView.Create(added, ParseColor(def?.Color));
                view.transform.SetParent(transform, true);
                _groundItems[added.Id] = view;
            }
        }

        private void RemoveGroundItem(int id)
        {
            if (!_groundItems.TryGetValue(id, out GroundItemView view)) return;
            if (view != null) Destroy(view.gameObject);
            _groundItems.Remove(id);
        }

        /// <summary>"#RRGGBB" to a colour; grey if missing or malformed.</summary>
        public static Color ParseColor(string hex) =>
            hex != null && ColorUtility.TryParseHtmlString(hex, out Color c) ? c : new Color(0.6f, 0.6f, 0.6f);

        /// <summary>
        /// Animates an attack or ability, and returns how long until it lands: the swing's contact
        /// for melee, the projectile's flight from further off, nothing for something used on oneself.
        /// </summary>
        private float ShowAction(ActionEvent action)
        {
            EntityView source = Find(action.SourceId);
            EntityView target = Find(action.TargetId);
            if (source == null) return 0f;
            // The ability says how it looks; "anim:<name>" plays a named animation (skilling).
            string animation = action.AbilityId != null && action.AbilityId.StartsWith(AnimPrefix)
                ? action.AbilityId.Substring(AnimPrefix.Length)
                : AbilityLookup(action.AbilityId)?.Animation;
            if (target == null || target == source)
            {
                if (action.Face.HasValue) source.FaceTowards(TileMath.Center(action.Face.Value));
                if (action.AbilityId != AnimPrefix) source.PlayAction(string.IsNullOrEmpty(animation) ? "cast" : animation); // bare "anim:" only turns
                return 0f;
            }
            source.FaceTowards(target.transform.position);
            source.LastHitTime = Time.time;
            source.PlayAction(string.IsNullOrEmpty(animation) ? "cast" : animation);

            // Melee needs no projectile; anything thrown from further off gets one.
            float distance = Vector3.Distance(source.transform.position, target.transform.position);
            if (distance < 1.6f) return MeleeContactSeconds;
            Color color = action.Pole > 0 ? new Color(0.98f, 0.82f, 0.2f)
                : action.Pole < 0 ? new Color(0.25f, 0.12f, 0.35f)
                : new Color(0.55f, 0.75f, 0.4f);
            // Quick enough to read as one blow, slow enough to see; further shots fly a little longer.
            float flight = Mathf.Clamp(0.12f + distance * 0.045f, 0.2f, _tickSeconds * 0.75f);
            Projectile.Launch(source.transform.position + Vector3.up * source.Height * 0.7f, target, color, flight);
            return flight;
        }

        private const string AnimPrefix = ActionEvent.AnimationPrefix;
        /// <summary>How long into a melee swing the blow connects.</summary>
        private const float MeleeContactSeconds = 0.22f;

        private readonly List<(float Time, System.Action Run)> _later = new List<(float, System.Action)>();

        private void Later(float seconds, System.Action run)
        {
            if (seconds <= 0f) run();
            else _later.Add((Time.time + seconds, run));
        }

        private void Update()
        {
            for (int i = 0; i < _later.Count; i++)
            {
                if (Time.time < _later[i].Time) continue;
                System.Action run = _later[i].Run;
                _later.RemoveAt(i--);
                run();
            }
        }

        private void ShowHit(HitEvent hit)
        {
            EntityView target = Find(hit.TargetId);
            if (target == null) return;
            target.LastHitTime = Time.time;
            if (hit.Kind != HitKind.Heal && hit.Kind != HitKind.Bleed) target.PlayHit();

            string text;
            Color color;
            switch (hit.Kind)
            {
                case HitKind.Miss:
                    text = "0";
                    color = new Color(0.22f, 0.38f, 0.75f);
                    break;
                case HitKind.Heal:
                    text = "+" + hit.Amount;
                    color = new Color(0.18f, 0.6f, 0.22f);
                    break;
                case HitKind.Deflected:
                    text = hit.Amount.ToString();
                    color = new Color(0.32f, 0.48f, 0.72f);
                    break;
                case HitKind.Bleed:
                    text = hit.Amount.ToString();
                    color = new Color(0.62f, 0.08f, 0.12f);
                    break;
                default:
                    text = hit.Amount.ToString();
                    color = new Color(0.78f, 0.12f, 0.1f);
                    break;
            }

            // Splats that land on the same entity close together fan out: beside, then above.
            int stacked = 0;
            foreach (Hitsplat h in _hitsplats)
                if (h.EntityId == hit.TargetId && Time.time - h.Time < HitsplatSeconds * 0.6f) stacked++;
            float offset = (stacked % 3 == 1 ? -1 : stacked % 3 == 2 ? 1 : 0) * 34f;
            _hitsplats.Add(new Hitsplat { EntityId = hit.TargetId, Text = text, Color = color, Time = Time.time, Offset = offset, Row = stacked / 3 });
        }

        public void Clear()
        {
            foreach (EntityView view in _entities.Values)
                if (view != null) Destroy(view.gameObject);
            _entities.Clear();
            foreach (GroundItemView view in _groundItems.Values)
                if (view != null) Destroy(view.gameObject);
            _groundItems.Clear();
            _hitsplats.Clear();
            _overhead.Clear();
            _later.Clear();
            _xpDrops.Clear();
            _markers.Clear();
            LocalId = -1;
            TargetId = 0;
        }

        private void Remove(int id)
        {
            if (!_entities.TryGetValue(id, out EntityView view)) return;
            if (view != null) Destroy(view.gameObject);
            _entities.Remove(id);
        }

        private static Color ColorFor(int id)
        {
            float hue = (id * 0.618034f) % 1f;
            return Color.HSVToRGB(hue, 0.55f, 0.85f);
        }

        private static Color FromRgb(int rgb) =>
            rgb == 0 ? Color.gray : new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        private void OnGUI()
        {
            GUI.depth = 10; // world labels sit underneath the HUD (lower depth draws on top)
            Camera cam = Camera.main;
            if (cam == null) return;
            EnsureStyles();
            _hitsplats.RemoveAll(h => Time.time - h.Time > HitsplatSeconds);

            foreach (EntityView view in _entities.Values)
            {
                if (view == null) continue;
                Vector3 screen = cam.WorldToScreenPoint(view.transform.position + Vector3.up * (view.Height + 0.35f));
                if (screen.z <= 0f || Ui.UiRoot.IsPointerOverUi(screen)) continue; // IMGUI draws over windows: keep labels out from under them
                float x = screen.x;
                float y = Screen.height - screen.y;

                // Health bars appear on whatever is hurt, fighting, or being targeted.
                bool showBar = view.Id == TargetId || Time.time - view.LastHitTime < HealthBarSeconds || view.HealthPercent < 100;
                if (showBar && !view.IsDying)
                {
                    var back = new Rect(x - 25f, y - 4f, 50f, 6f);
                    DrawRect(back, new Color(0.6f, 0f, 0f, 0.9f));
                    DrawRect(new Rect(back.x, back.y, back.width * view.HealthPercent / 100f, back.height), new Color(0.1f, 0.85f, 0.1f, 0.95f));
                    y -= 8f;
                }

                // The overhead hymn, above everything else over its head (as prayers are in RuneScape).
                if (!string.IsNullOrEmpty(view.Overhead) && !view.IsDying)
                {
                    Texture2D icon = Ui.IconLibrary.Get(HymnIconLookup(view.Overhead));
                    if (icon != null)
                    {
                        Color old = GUI.color;
                        GUI.color = new Color(0f, 0f, 0f, 0.6f);
                        GUI.DrawTexture(new Rect(x - 15f, y - 50f, 32f, 32f), icon);
                        GUI.color = Ui.Hymns.Tint;
                        GUI.DrawTexture(new Rect(x - 16f, y - 51f, 32f, 32f), icon);
                        GUI.color = old;
                    }
                }

                // Names: players always; creatures and townsfolk when pointed at, targeted or fighting,
                // so a busy scene isn't a wall of labels.
                bool showName = view.Kind != EntityKind.Npc || view.Id == HoveredId || view.Id == TargetId || Time.time - view.LastHitTime < HealthBarSeconds;
                if (showName)
                {
                    Color nameColor = view.Id == TargetId ? new Color(1f, 0.55f, 0.3f) : view.Kind == EntityKind.Npc ? new Color(1f, 1f, 0.4f) : Color.white;
                    Ink(_labelStyle, new Color(0f, 0f, 0f, 0.75f));
                    GUI.Label(new Rect(x - 69f, y - 17f, 140f, 18f), view.DisplayName, _labelStyle);
                    Ink(_labelStyle, nameColor);
                    GUI.Label(new Rect(x - 70f, y - 18f, 140f, 18f), view.DisplayName, _labelStyle);
                }

                if (_overhead.TryGetValue(view.Id, out (string Text, float Time) said) && Time.time - said.Time < OverheadSeconds)
                {
                    float sayY = y - 38f - (string.IsNullOrEmpty(view.Overhead) ? 0f : 34f); // above the overhead icon
                    Ink(_labelStyle, new Color(0f, 0f, 0f, 0.8f));
                    GUI.Label(new Rect(x - 159f, sayY + 1f, 320f, 18f), said.Text, _labelStyle);
                    Ink(_labelStyle, new Color(1f, 1f, 0f));
                    GUI.Label(new Rect(x - 160f, sayY, 320f, 18f), said.Text, _labelStyle);
                }
            }

            // Markers over heads, above everything else there.
            foreach (KeyValuePair<int, (string Label, float Start, float Seconds)> m in new List<KeyValuePair<int, (string, float, float)>>(_markers))
            {
                float left = 1f - (Time.time - m.Value.Start) / m.Value.Seconds;
                EntityView view = Find(m.Key);
                if (left <= 0f || view == null)
                {
                    _markers.Remove(m.Key);
                    continue;
                }
                // The count, in the middle of the blood mark's seal.
                if (string.IsNullOrEmpty(m.Value.Label)) continue;
                Vector3 seal = cam.WorldToScreenPoint(view.transform.position + Vector3.up * (view.Height + BloodMark.Lift));
                if (seal.z <= 0f) continue;
                var centre = new Rect(seal.x - 30f, Screen.height - seal.y - 12f, 60f, 24f);
                _splatStyle.fontSize = 17;
                Ink(_splatStyle, new Color(0.12f, 0f, 0f, 0.95f));
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0) GUI.Label(new Rect(centre.x + dx * 2f, centre.y + dy * 2f, centre.width, centre.height), m.Value.Label, _splatStyle);
                Ink(_splatStyle, new Color(1f, 0.93f, 0.88f));
                GUI.Label(centre, m.Value.Label, _splatStyle);
            }

            // XP drops rise from beside the local player's head.
            _xpDrops.RemoveAll(d => Time.time - d.Time > XpDropSeconds);
            EntityView local = Local;
            if (local != null && _xpDrops.Count > 0)
            {
                Vector3 head = cam.WorldToScreenPoint(local.transform.position + Vector3.up * (local.Height + 0.2f));
                if (head.z > 0f)
                    for (int i = 0; i < _xpDrops.Count; i++)
                    {
                        float age = (Time.time - _xpDrops[i].Time) / XpDropSeconds;
                        float alpha = age < 0.7f ? 1f : 1f - (age - 0.7f) / 0.3f;
                        var rect = new Rect(head.x + 34f, Screen.height - head.y - age * 50f - (_xpDrops.Count - 1 - i) * 16f, 160f, 18f);
                        Ink(_xpStyle, new Color(0f, 0f, 0f, 0.8f * alpha));
                        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), _xpDrops[i].Text, _xpStyle);
                        Ink(_xpStyle, new Color(1f, 0.85f, 0.35f, alpha));
                        GUI.Label(rect, _xpDrops[i].Text, _xpStyle);
                    }
            }

            // Hitsplats: a coloured badge that pops in on the body, holds, then lifts and fades.
            foreach (Hitsplat h in _hitsplats)
            {
                EntityView view = Find(h.EntityId);
                if (view == null) continue;
                float seconds = Time.time - h.Time;
                float age = seconds / HitsplatSeconds;
                Vector3 screen = cam.WorldToScreenPoint(view.transform.position + Vector3.up * (view.Height * 0.55f));
                if (screen.z <= 0f || Ui.UiRoot.IsPointerOverUi(screen)) continue;
                float pop = seconds < 0.12f ? Mathf.Lerp(1.4f, 1f, seconds / 0.12f) : 1f;
                float fade = age < 0.6f ? 1f : 1f - (age - 0.6f) / 0.4f;
                float lift = age < 0.6f ? 0f : (age - 0.6f) * 40f;
                float width = (16f + 10f * h.Text.Length) * pop;
                float height = 24f * pop;
                float cx = screen.x + h.Offset;
                float cy = Screen.height - screen.y - lift - h.Row * 26f;
                var badge = new Rect(cx - width / 2f, cy - height / 2f, width, height);
                DrawRect(new Rect(badge.x - 2f, badge.y - 2f, badge.width + 4f, badge.height + 4f), new Color(0f, 0f, 0f, 0.7f * fade));
                Color back = h.Color;
                back.a = 0.92f * fade;
                DrawRect(badge, back);
                _splatStyle.fontSize = Mathf.RoundToInt(16f * pop);
                Ink(_splatStyle, new Color(1f, 1f, 1f, fade));
                GUI.Label(badge, h.Text, _splatStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null) return;
            _labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _splatStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 18 };
            _xpStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = 13 };
            _white = Texture2D.whiteTexture;
        }

        /// <summary>Sets a world label's colour, for hovering too: a label under the pointer must not change.</summary>
        private static void Ink(GUIStyle style, Color color) => style.normal.textColor = style.hover.textColor = color;

        private void DrawRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = old;
        }
    }
}
