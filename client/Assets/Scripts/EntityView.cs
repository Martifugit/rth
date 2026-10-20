using System.Collections.Generic;
using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Draws one entity and moves it smoothly through the tiles the server reports. Each tick's
    /// steps are spread over one tick's duration, so walking and running look continuous; if
    /// updates pile up (a hitch or lag spike) it speeds up to catch up rather than falling behind.
    /// </summary>
    public sealed class EntityView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly Queue<Vector3> _pending = new Queue<Vector3>();
        private float _tickSeconds = 0.6f;
        private int _stepsPerTick = 1;
        private bool _moving;
        private Vector3 _from;
        private Vector3 _to;
        private float _t;

        public int Id { get; private set; }
        public string DisplayName { get; private set; }
        public TileCoord Tile { get; private set; }

        public static EntityView Create(int id, string displayName, TileCoord tile, Color color, float tickSeconds)
        {
            var go = new GameObject("Player " + displayName + " #" + id);
            var view = go.AddComponent<EntityView>();
            view.Id = id;
            view.DisplayName = displayName;
            view._tickSeconds = tickSeconds;
            view.BuildBody(color);
            view.Teleport(tile);
            return view;
        }

        private void BuildBody(Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>()); // clicks should reach the ground underneath
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.85f, 0.6f);
            body.GetComponent<Renderer>().SetPropertyBlock(block);

            // A small block for a face, so you can see which way they're heading.
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(face.GetComponent<Collider>());
            face.transform.SetParent(transform, false);
            face.transform.localPosition = new Vector3(0f, 1.35f, 0.27f);
            face.transform.localScale = new Vector3(0.3f, 0.15f, 0.12f);
            block.SetColor(BaseColor, new Color(0.95f, 0.85f, 0.7f));
            face.GetComponent<Renderer>().SetPropertyBlock(block);
        }

        public void Teleport(TileCoord tile)
        {
            _pending.Clear();
            _moving = false;
            Tile = tile;
            transform.position = TileMath.Center(tile);
        }

        public void AddSteps(IList<TileCoord> steps, bool running)
        {
            _stepsPerTick = running ? 2 : 1;
            foreach (TileCoord step in steps)
            {
                _pending.Enqueue(TileMath.Center(step));
                Tile = step;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            while (dt > 0f)
            {
                if (!_moving)
                {
                    if (_pending.Count == 0) break;
                    _from = transform.position;
                    _to = _pending.Dequeue();
                    _t = 0f;
                    _moving = true;
                    Face(_to - _from);
                }

                // Normally one step lasts tick / stepsPerTick. Steps queued beyond one tick's
                // worth mean we're behind, so each extra queued step adds 50% speed.
                float catchUp = 1f + Mathf.Max(0, _pending.Count - _stepsPerTick + 1) * 0.5f;
                float stepSeconds = _tickSeconds / _stepsPerTick / catchUp;
                float remaining = (1f - _t) * stepSeconds;
                if (dt >= remaining)
                {
                    dt -= remaining;
                    transform.position = _to;
                    _moving = false;
                }
                else
                {
                    _t += dt / stepSeconds;
                    transform.position = Vector3.Lerp(_from, _to, _t);
                    dt = 0f;
                }
            }
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
