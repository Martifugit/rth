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
        }

        private readonly Dictionary<int, EntityView> _entities = new Dictionary<int, EntityView>();
        private readonly Dictionary<int, GroundItemView> _groundItems = new Dictionary<int, GroundItemView>();

        /// <summary>Looks up item definitions (set by the session from the server's content).</summary>
        public System.Func<string, ItemDef> ItemLookup { get; set; } = _ => null;
        public System.Func<string, AbilityDef> AbilityLookup { get; set; } = _ => null;

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
        private Texture2D _white;

        public int LocalId { get; private set; } = -1;
        /// <summary>The entity the local player is fighting, highlighted in the HUD.</summary>
        public int TargetId { get; set; }

        public EntityView Local => Find(LocalId);

        public EntityView Find(int id) => _entities.TryGetValue(id, out EntityView view) ? view : null;

        public IEnumerable<EntityView> Entities => _entities.Values;

        private const float OverheadSeconds = 4f;
        private readonly Dictionary<int, (string Text, float Time)> _overhead = new Dictionary<int, (string, float)>();

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
                _entities[added.Id] = view;
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
                source.PlayAction(string.IsNullOrEmpty(animation) ? "cast" : animation);
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
                    color = new Color(0.45f, 0.65f, 1f);
                    break;
                case HitKind.Heal:
                    text = "+" + hit.Amount;
                    color = new Color(0.4f, 0.95f, 0.4f);
                    break;
                case HitKind.Deflected:
                    text = hit.Amount.ToString();
                    color = new Color(0.75f, 0.85f, 1f);
                    break;
                case HitKind.Bleed:
                    text = hit.Amount.ToString();
                    color = new Color(0.62f, 0.08f, 0.12f);
                    break;
                default:
                    text = hit.Amount.ToString();
                    color = new Color(1f, 0.25f, 0.2f);
                    break;
            }

            // Stagger splats that land on the same entity at once.
            int stacked = 0;
            foreach (Hitsplat h in _hitsplats)
                if (h.EntityId == hit.TargetId && Time.time - h.Time < 0.3f) stacked++;
            _hitsplats.Add(new Hitsplat { EntityId = hit.TargetId, Text = text, Color = color, Time = Time.time, Offset = stacked * 18f });
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

                Color nameColor = view.Id == TargetId ? new Color(1f, 0.55f, 0.3f) : view.Kind == EntityKind.Npc ? new Color(1f, 1f, 0.4f) : Color.white;
                _labelStyle.normal.textColor = nameColor;
                GUI.Label(new Rect(x - 70f, y - 18f, 140f, 18f), view.DisplayName, _labelStyle);

                if (_overhead.TryGetValue(view.Id, out (string Text, float Time) said) && Time.time - said.Time < OverheadSeconds)
                {
                    _labelStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
                    GUI.Label(new Rect(x - 159f, y - 37f, 320f, 18f), said.Text, _labelStyle);
                    _labelStyle.normal.textColor = new Color(1f, 1f, 0f);
                    GUI.Label(new Rect(x - 160f, y - 38f, 320f, 18f), said.Text, _labelStyle);
                }
            }

            foreach (Hitsplat h in _hitsplats)
            {
                EntityView view = Find(h.EntityId);
                if (view == null) continue;
                float age = (Time.time - h.Time) / HitsplatSeconds;
                Vector3 screen = cam.WorldToScreenPoint(view.transform.position + Vector3.up * (view.Height * 0.6f));
                if (screen.z <= 0f || Ui.UiRoot.IsPointerOverUi(screen)) continue;
                var rect = new Rect(screen.x - 30f + h.Offset, Screen.height - screen.y - 12f - age * 30f, 60f, 24f);
                Color c = h.Color;
                c.a = 1f - age * age;
                _splatStyle.normal.textColor = new Color(0f, 0f, 0f, c.a);
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), h.Text, _splatStyle);
                _splatStyle.normal.textColor = c;
                GUI.Label(rect, h.Text, _splatStyle);
            }
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null) return;
            _labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _splatStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 18 };
            _white = Texture2D.whiteTexture;
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = old;
        }
    }
}
