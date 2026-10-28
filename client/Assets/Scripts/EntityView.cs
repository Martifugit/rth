using System.Collections.Generic;
using Rth.Client.Characters;
using Rth.Shared.Net;
using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Draws one entity and walks it through the tiles the server reports, the way the 2004 client
    /// does (2004scape's client.updateMovement / updateFacingDirection), which is what makes
    /// tick-based movement feel continuous:
    /// <list type="bullet">
    /// <item>a step walks a little slower than a tick (32 client cycles of 20 ms against 30), so the
    /// next step always arrives before this one ends and a walker never stops between tiles;</item>
    /// <item>it speeds up by how far behind it is: 1.5 times with more than two steps to go, twice
    /// with more than three; running doubles it;</item>
    /// <item>it turns at a fixed rate, so doubling back eases round instead of snapping (click
    /// behind you and back, in rhythm, and it never stops). The old client also walks at half speed
    /// while it turns, but it has turning animations to hide that; without them it reads as a limp
    /// at every bend, so this one keeps its pace;</item>
    /// <item>it moves along each axis on its own, so a diagonal step takes as long as a straight one;</item>
    /// <item>more than two tiles from its next step, it jumps there.</item>
    /// </list>
    /// </summary>
    public sealed class EntityView : MonoBehaviour
    {
        /// <summary>How long after its last attack or hit an entity keeps its combat stance.</summary>
        private const float CombatStanceSeconds = 8f;

        // The 2004 client's numbers: a cycle is 20 ms, a tile 128 units; walking 4 units a cycle,
        // turning 32 of 2048 a cycle.
        private const float CycleSeconds = 0.02f;
        private const float TurnDegreesPerSecond = 32f / 2048f * 360f / CycleSeconds;
        /// <summary>Steps of a tile; the server's tick may be faster in tests, so the cycle scales with it.</summary>
        private const float CyclesPerTileWalking = 32f;
        private const int MaxPending = 9;

        private readonly Queue<Vector3> _pending = new Queue<Vector3>();
        private float _tickSeconds = 0.6f;
        private int _stepsPerTick = 1;
        private bool _moving;
        private Vector3 _from;
        private Vector3 _to;
        private float _pace = 1f;
        private EntityBody _body;
        private Quaternion _facing = Quaternion.identity;
        private float _deathTime = -1f;
        private float _lastY;
        private bool _grounded = true;
        private float _nextGroundCheck;

        public int Id { get; private set; }
        public EntityKind Kind { get; private set; }
        /// <summary>NPC type id, for looking up its menu; empty for players.</summary>
        public string TypeId { get; private set; } = "";
        public string DisplayName { get; private set; }
        public TileCoord Tile { get; private set; }
        /// <summary>The floor it stands on: 0 is the ground, 1 the floor above (see MapLevel).</summary>
        public int Plane { get; private set; }
        /// <summary>Top of the model, for labels and health bars.</summary>
        public float Height => _body != null ? _body.Height : 1.8f;
        public int HealthPercent { get; set; } = 100;
        /// <summary>Last time it took or dealt a hit; health bars show for a while after.</summary>
        public float LastHitTime { get; set; } = -100f;
        /// <summary>The overhead hymn it sings ("" for none), drawn above its head.</summary>
        public string Overhead { get; set; } = "";
        public bool IsDying => _deathTime >= 0f;

        public static EntityView Create(EntityAdded added, Color color, float tickSeconds, string model = "")
        {
            var go = new GameObject((added.Kind == EntityKind.Npc ? "NPC " : "Player ") + added.Name + " #" + added.Id);
            var view = go.AddComponent<EntityView>();
            view.Id = added.Id;
            view.Kind = added.Kind;
            view.TypeId = added.TypeId ?? "";
            view.DisplayName = added.Name;
            view.HealthPercent = added.HealthPercent;
            view._tickSeconds = tickSeconds;
            view.BuildBody(added, color, model);
            view.Plane = added.Plane;
            view.Teleport(added.Position);
            return view;
        }

        /// <summary>Shows the item in its hand (null for none).</summary>
        public void SetHeld(Rth.Shared.Items.ItemDef item)
        {
            if (_body != null) _body.SetHeld(item);
        }

        private void BuildBody(EntityAdded added, Color color, string model)
        {
            _body = EntityBody.Create(added, color, transform, model);

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
            _grounded = GroundHeight.TryCentre(tile, Plane, out Vector3 centre);
            transform.position = centre;
            _lastY = centre.y;
            transform.rotation = _facing;
        }

        public void AddSteps(IList<TileCoord> steps, bool running)
        {
            _stepsPerTick = running ? 2 : 1;
            foreach (TileCoord step in steps)
            {
                // Far behind (a long hitch): drop the oldest steps rather than replaying them all.
                if (_pending.Count >= MaxPending) _pending.Dequeue();
                // Each step stands on the ground just under the last one, so hills and stairs are
                // followed and a roof or the floor above is never mistaken for the ground.
                Vector3 centre = GroundHeight.Centre(step, Plane, _lastY);
                _lastY = centre.y;
                _pending.Enqueue(centre);
                Tile = step;
            }
        }

        public void FaceTowards(Vector3 point)
        {
            _facePoint = point;
            Face(point - transform.position);
        }

        /// <summary>
        /// It attacked or used an ability; <paramref name="animation"/> is the clip or role to play.
        /// A clip that stops when its player moves (a swing at a tree) arriving while it is still
        /// walking its last steps is held until it stands: the client walks a little behind the
        /// server, so the swing comes in before the walk ends, and would be cut off at once.
        /// </summary>
        /// <summary>Whether it is still walking steps it has been sent.</summary>
        public bool IsWalking => _moving || _pending.Count > 0;

        public void PlayAction(string animation)
        {
            if (_body == null) return;
            if ((_moving || _pending.Count > 0) && ClipAnimator.Tuning(animation).StopOnMove)
            {
                _heldAction = animation;
                _heldFace = _facePoint;
                _heldUntil = Time.time + 1.5f;
                return;
            }
            _body.PlayAction(animation);
        }

        private string _heldAction;
        private Vector3? _facePoint, _heldFace;
        private float _heldUntil;

        /// <summary>Plays a held action once it stands still (turned to what it acts on), if it isn't stale.</summary>
        private void PlayHeldAction()
        {
            if (_heldAction == null || _moving || _pending.Count > 0) return;
            if (Time.time <= _heldUntil)
            {
                if (_heldFace.HasValue) Face(_heldFace.Value - transform.position);
                _body.PlayAction(_heldAction);
            }
            _heldAction = null;
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

            // Placed before its region's ground had streamed in: settle onto it once it has.
            if (!_grounded && !_moving && _pending.Count == 0 && Time.time >= _nextGroundCheck)
            {
                _nextGroundCheck = Time.time + 0.5f;
                if (GroundHeight.TryCentre(Tile, Plane, out Vector3 found))
                {
                    _grounded = true;
                    transform.position = found;
                    _lastY = found.y;
                }
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
                    _moving = true;
                    Face(_to - _from);
                    if (Mathf.Abs(_to.x - _from.x) > 2.01f || Mathf.Abs(_to.z - _from.z) > 2.01f)
                    {
                        transform.position = _to; // too far behind to walk it
                        _moving = false;
                        continue;
                    }
                }

                // Tiles a second: a walking step is 32 cycles, scaled to the server's tick (30 cycles a tick).
                float cycle = CycleSeconds * _tickSeconds / 0.6f;
                float tilesPerSecond = 1f / (CyclesPerTileWalking * cycle);
                int pathLength = _pending.Count + 1;
                float pace = 1f;
                if (pathLength > 2) pace = 1.5f;
                if (pathLength > 3) pace = 2f;
                if (_stepsPerTick == 2) pace *= 2f;
                _pace = pace;
                float stepLength = tilesPerSecond * pace * dt;

                // Each axis moves on its own (a diagonal step takes as long as a straight one).
                Vector3 p = transform.position;
                float nx = Mathf.MoveTowards(p.x, _to.x, stepLength), nz = Mathf.MoveTowards(p.z, _to.z, stepLength);
                float span = Mathf.Max(Mathf.Abs(_to.x - _from.x), Mathf.Abs(_to.z - _from.z));
                float left = Mathf.Max(Mathf.Abs(_to.x - nx), Mathf.Abs(_to.z - nz));
                float ny = span > 0.0001f ? Mathf.Lerp(_to.y, _from.y, left / span) : _to.y;
                float used = Mathf.Max(Mathf.Abs(nx - p.x), Mathf.Abs(nz - p.z));
                transform.position = new Vector3(nx, ny, nz);
                if (nx == _to.x && nz == _to.z)
                {
                    transform.position = _to;
                    _moving = false;
                    // The rest of the frame carries on into the next step.
                    dt = stepLength > 0f ? dt * Mathf.Clamp01(1f - used / stepLength) : 0f;
                }
                else dt = 0f;
            }

            // Turn at the old client's fixed rate, snapping the last few degrees.
            if (Quaternion.Angle(transform.rotation, _facing) < 5.7f) transform.rotation = _facing;
            else transform.rotation = Quaternion.RotateTowards(transform.rotation, _facing, TurnDegreesPerSecond * Time.deltaTime);
            PlayHeldAction();
            // Speed in steps, as the game moves: a diagonal step is one step, as long as a straight
            // one, so it mustn't play the walk faster (its distance is 1.4 tiles).
            Vector3 moved = transform.position - before;
            float speed = Time.deltaTime > 0f ? Mathf.Max(Mathf.Abs(moved.x), Mathf.Abs(moved.z)) / Time.deltaTime : 0f;
            _body.InCombat = Time.time - LastHitTime < CombatStanceSeconds;
            // Catching up at double pace or more looks like running, as the old client shows it.
            _body.Animate(speed, _stepsPerTick == 2 || (_moving && _pace >= 2f), Time.deltaTime);
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) _facing = Quaternion.LookRotation(direction);
        }
    }
}
