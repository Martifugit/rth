using System.Collections.Generic;
using Rth.Shared.Net;
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
        private Transform _body;
        private Vector3 _bodyScale;
        private Vector3 _bodyPosition;
        private float _deathTime = -1f;

        public int Id { get; private set; }
        public EntityKind Kind { get; private set; }
        public string DisplayName { get; private set; }
        public TileCoord Tile { get; private set; }
        /// <summary>Top of the model, for labels and health bars.</summary>
        public float Height { get; private set; } = 1.8f;
        public int HealthPercent { get; set; } = 100;
        /// <summary>Last time it took or dealt a hit; health bars show for a while after.</summary>
        public float LastHitTime { get; set; } = -100f;
        public bool IsDying => _deathTime >= 0f;

        public static EntityView Create(EntityAdded added, Color color, float tickSeconds)
        {
            var go = new GameObject((added.Kind == EntityKind.Npc ? "NPC " : "Player ") + added.Name + " #" + added.Id);
            var view = go.AddComponent<EntityView>();
            view.Id = added.Id;
            view.Kind = added.Kind;
            view.DisplayName = added.Name;
            view.HealthPercent = added.HealthPercent;
            view._tickSeconds = tickSeconds;
            view.BuildBody(added.Shape, color, added.Scale / 10f);
            view.Teleport(added.Position);
            return view;
        }

        private void BuildBody(EntityShape shape, Color color, float scale)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);

            PrimitiveType type = shape switch
            {
                EntityShape.Box => PrimitiveType.Cube,
                EntityShape.Cylinder => PrimitiveType.Cylinder,
                EntityShape.Sphere => PrimitiveType.Sphere,
                _ => PrimitiveType.Capsule,
            };
            GameObject body = GameObject.CreatePrimitive(type);
            Destroy(body.GetComponent<Collider>());
            body.name = "Body";
            body.transform.SetParent(transform, false);

            // Sizes chosen so a scale-1 entity is about player-sized.
            Vector3 size = shape switch
            {
                EntityShape.Box => new Vector3(0.8f, 0.6f, 1.0f),
                EntityShape.Sphere => new Vector3(0.9f, 0.9f, 0.9f),
                EntityShape.Cylinder => new Vector3(0.5f, 0.9f, 0.5f), // a cylinder primitive is 2 tall
                _ => new Vector3(0.6f, 0.85f, 0.6f),                   // so is a capsule
            } * scale;
            float height = shape == EntityShape.Capsule || shape == EntityShape.Cylinder ? size.y * 2f : size.y;
            body.transform.localScale = size;
            body.transform.localPosition = new Vector3(0f, height / 2f, 0f);
            body.GetComponent<Renderer>().SetPropertyBlock(block);
            _body = body.transform;
            _bodyScale = body.transform.localScale;
            _bodyPosition = body.transform.localPosition;
            Height = height;

            // A face, so you can see which way it's heading.
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(face.GetComponent<Collider>());
            face.transform.SetParent(transform, false);
            face.transform.localPosition = new Vector3(0f, height * 0.75f, size.z * 0.45f);
            face.transform.localScale = new Vector3(0.3f, 0.15f, 0.12f) * Mathf.Max(0.6f, scale);
            block.SetColor(BaseColor, Kind == EntityKind.Player ? new Color(0.95f, 0.85f, 0.7f) : Color.Lerp(color, Color.black, 0.5f));
            face.GetComponent<Renderer>().SetPropertyBlock(block);
            face.transform.SetParent(_body, true);

            // One collider on the root so clicks find the entity.
            var hitbox = gameObject.AddComponent<CapsuleCollider>();
            hitbox.center = new Vector3(0f, height / 2f, 0f);
            hitbox.height = Mathf.Max(height, 0.8f);
            hitbox.radius = Mathf.Max(0.35f, Mathf.Max(size.x, size.z) * 0.5f);
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

        public void FaceTowards(Vector3 point) => Face(point - transform.position);

        /// <summary>Sinks and shrinks; the server removes or respawns it shortly after.</summary>
        public void PlayDeath()
        {
            _deathTime = Time.time;
            HealthPercent = 0;
            var hitbox = GetComponent<Collider>();
            if (hitbox != null) hitbox.enabled = false;
        }

        private void Update()
        {
            if (IsDying)
            {
                float k = Mathf.Clamp01((Time.time - _deathTime) / 1.0f);
                _body.localScale = Vector3.Lerp(_bodyScale, _bodyScale * 0.3f, k);
                _body.localPosition = Vector3.Lerp(_bodyPosition, new Vector3(0f, _bodyScale.y * 0.15f, 0f), k);
                _body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 80f, k));
                return;
            }

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
