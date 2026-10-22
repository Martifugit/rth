using System.Collections.Generic;
using Rth.Client.Characters;
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
        /// <summary>How long after its last attack or hit an entity keeps its combat stance.</summary>
        private const float CombatStanceSeconds = 8f;

        private readonly Queue<Vector3> _pending = new Queue<Vector3>();
        private float _tickSeconds = 0.6f;
        private int _stepsPerTick = 1;
        private bool _moving;
        private Vector3 _from;
        private Vector3 _to;
        private float _t;
        private EntityBody _body;
        private Quaternion _facing = Quaternion.identity;
        private float _deathTime = -1f;

        public int Id { get; private set; }
        public EntityKind Kind { get; private set; }
        /// <summary>NPC type id, for looking up its menu; empty for players.</summary>
        public string TypeId { get; private set; } = "";
        public string DisplayName { get; private set; }
        public TileCoord Tile { get; private set; }
        /// <summary>Top of the model, for labels and health bars.</summary>
        public float Height => _body != null ? _body.Height : 1.8f;
        public int HealthPercent { get; set; } = 100;
        /// <summary>Last time it took or dealt a hit; health bars show for a while after.</summary>
        public float LastHitTime { get; set; } = -100f;
        /// <summary>The overhead hymn it sings ("" for none), drawn above its head.</summary>
        public string Overhead { get; set; } = "";
        public bool IsDying => _deathTime >= 0f;

        public static EntityView Create(EntityAdded added, Color color, float tickSeconds)
        {
            var go = new GameObject((added.Kind == EntityKind.Npc ? "NPC " : "Player ") + added.Name + " #" + added.Id);
            var view = go.AddComponent<EntityView>();
            view.Id = added.Id;
            view.Kind = added.Kind;
            view.TypeId = added.TypeId ?? "";
            view.DisplayName = added.Name;
            view.HealthPercent = added.HealthPercent;
            view._tickSeconds = tickSeconds;
            view.BuildBody(added, color);
            view.Teleport(added.Position);
            return view;
        }

        /// <summary>Shows the item in its hand (null for none).</summary>
        public void SetHeld(Rth.Shared.Items.ItemDef item)
        {
            if (_body != null) _body.SetHeld(item);
        }

        private void BuildBody(EntityAdded added, Color color)
        {
            _body = EntityBody.Create(added, color, transform);

            // One collider on the root so clicks find the entity.
            var hitbox = gameObject.AddComponent<CapsuleCollider>();
            hitbox.center = new Vector3(0f, Height / 2f, 0f);
            hitbox.height = Mathf.Max(Height, 0.8f);
            hitbox.radius = Mathf.Max(0.35f, _body.Radius);
        }

        public void Teleport(TileCoord tile)
        {
            _pending.Clear();
            _moving = false;
            Tile = tile;
            transform.position = TileMath.Center(tile);
            transform.rotation = _facing;
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

        /// <summary>It attacked or used an ability; <paramref name="animation"/> is the clip or role to play.</summary>
        public void PlayAction(string animation)
        {
            if (_body != null) _body.PlayAction(animation);
        }

        /// <summary>It took a hit.</summary>
        public void PlayHit()
        {
            if (!IsDying && _body != null) _body.PlayHit();
        }

        /// <summary>Plays its death; the server removes or respawns it shortly after.</summary>
        public void PlayDeath()
        {
            _deathTime = Time.time;
            if (_body != null) _body.PlayDeath();
            HealthPercent = 0;
            var hitbox = GetComponent<Collider>();
            if (hitbox != null) hitbox.enabled = false;
        }

        private void Update()
        {
            // A script reload during Play drops the body; nothing to draw until it is re-sent.
            if (_body == null) return;
            if (IsDying)
            {
                _body.Animate(0f, false, Time.deltaTime);
                return;
            }

            Vector3 before = transform.position;
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

            // Stand on the ground, which may have streamed in (or changed) since the step was queued.
            transform.position = GroundHeight.OnGround(transform.position);

            // Turn smoothly rather than snapping to each new heading.
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _facing, 720f * Time.deltaTime);
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            float speed = Time.deltaTime > 0f ? moved.magnitude / Time.deltaTime : 0f;
            _body.InCombat = Time.time - LastHitTime < CombatStanceSeconds;
            _body.Animate(speed, _stepsPerTick == 2, Time.deltaTime);
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) _facing = Quaternion.LookRotation(direction);
        }
    }
}
