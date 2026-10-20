using System.Collections.Generic;
using Rth.Shared.Net;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>The client's picture of the world: applies the server's per-tick updates to entity views.</summary>
    public sealed class WorldView : MonoBehaviour
    {
        private readonly Dictionary<int, EntityView> _entities = new Dictionary<int, EntityView>();
        private float _tickSeconds = 0.6f;
        private GUIStyle _labelStyle;

        public int LocalId { get; private set; } = -1;

        public EntityView Local => _entities.TryGetValue(LocalId, out EntityView view) ? view : null;

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
                Color color = added.Id == LocalId ? new Color(0.25f, 0.45f, 0.85f) : ColorFor(added.Id);
                EntityView view = EntityView.Create(added.Id, added.Name, added.Position, color, _tickSeconds);
                view.transform.SetParent(transform, true);
                _entities[added.Id] = view;
            }

            foreach (EntityMoved moved in update.Moved)
                if (_entities.TryGetValue(moved.Id, out EntityView view)) view.AddSteps(moved.Steps, moved.Running);
        }

        public void Clear()
        {
            foreach (EntityView view in _entities.Values)
                if (view != null) Destroy(view.gameObject);
            _entities.Clear();
            LocalId = -1;
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

        private void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _labelStyle.normal.textColor = new Color(1f, 1f, 0.4f);
            }

            foreach (EntityView view in _entities.Values)
            {
                if (view == null) continue;
                Vector3 screen = cam.WorldToScreenPoint(view.transform.position + Vector3.up * 2.1f);
                if (screen.z <= 0f) continue;
                var rect = new Rect(screen.x - 60f, Screen.height - screen.y - 10f, 120f, 20f);
                GUI.Label(rect, view.DisplayName, _labelStyle);
            }
        }
    }
}
