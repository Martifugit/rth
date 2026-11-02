using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.Angling;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Rth.Client
{
    /// <summary>
    /// Trophy angling's fight, in the world (docs/fishing.md 3.3): no panel. While a fish is on it
    /// plays the same <see cref="AnglingFight"/> the server runs (from the "anglingFight" state) at
    /// 50 ms steps, and sends what the player does as the "angling" intent in small batches; the
    /// server replays them and decides.
    ///
    /// Made to be read at a glance, over everything (Shaders/AnglingOverlay, so a tree or a roof
    /// never hides it):
    /// <list type="bullet">
    /// <item>the camera swings round behind you when a fish bites, so the fight is in front of you;</item>
    /// <item>the line, from your rod's tip to the fish, is the tension: it sags pale when slack,
    /// runs green when it's right, turns amber, then red and trembling before it snaps;</item>
    /// <item>the fish is a dark shape under the water, swinging wide to the side it swims, sinking
    /// as it dives, gone in a splash as it leaps;</item>
    /// <item>over it, its energy as a bar (a tired fish is a fish to reel in), with chevrons on it
    /// pointing the way to pull (amber for its tell, green while you answer it), and its edge
    /// flashing red when there is nothing to pull and you must let go. No words;</item>
    /// <item>water effects: a swirl where it will turn, bubbles before a dive, a wake before a run,
    /// boiling before a leap, spray when you pull it the right way.</item>
    /// </list>
    ///
    /// Hold Space to reel. The cursor is the rod and needs no clicking (world clicks are ignored
    /// while it lasts, <see cref="Capturing"/>): to the side of the line the fish isn't swimming to,
    /// or below you to lift against a dive. Or, with steering off (Settings), you only reel and
    /// your angler leans against the fish by itself.
    /// </summary>
    public sealed class AnglingFightView : MonoBehaviour
    {
        public GameSession session;

        /// <summary>A fight is on: the cursor is the rod, and world clicks are ignored.</summary>
        public static bool Capturing { get; private set; }

        private const string SteerPref = "rth.angling.steer";
        private const string AnglingIntent = "angling";
        private const int BatchSteps = 6;
        private const int AutoReaction = 6;
        /// <summary>Pixels the cursor must be off the line to count as a pull.</summary>
        private const float PullDeadZone = 40f;
        /// <summary>How far to each side a fish swings (tiles).</summary>
        private const float Swing = 2.2f;

        /// <summary>Steer the fish with the cursor (otherwise the angler steers and you only reel).</summary>
        public static bool SteerWithMouse
        {
            get => PlayerPrefs.GetInt(SteerPref, 1) == 1;
            set => PlayerPrefs.SetInt(SteerPref, value ? 1 : 0);
        }

        private AnglingFight _fight;
        private AnglingFightState _state;
        private (int Id, uint Seed) _key = (-1, 0);
        private readonly List<byte> _inputs = new List<byte>();
        private readonly Queue<AnglingMove> _seen = new Queue<AnglingMove>();
        private int _sent;
        private float _ms;
        private float _startedAt;
        private float _endedAt = -1f;
        private bool _hinted;
        private byte _lastInput;

        private Vector3 _spot;
        private float _water;
        private Vector3 _fishPos;
        private float _lateral;
        private float _depth;
        private AnglingMove _lastMove = AnglingMove.Straight;
        private bool _lastInTell;
        private float _nextThrash;
        private float _nextSpray;

        private Transform _rodMesh;
        private Vector3 _rodTipLocal;
        private bool _hasRodTip;

        private GameObject _root;
        private ParticleSystem _shadow;
        private ParticleSystem _rings;
        private ParticleSystem _spray;
        private LineRenderer _line;
        private LineRenderer _outline;
        private readonly ParticleSystem.Particle[] _one = new ParticleSystem.Particle[1];

        private VisualElement _bar, _fill, _arrows;
        private readonly VisualElement[] _chevrons = new VisualElement[3];

        private void Update()
        {
            Capturing = false;
            if (session == null || !session.IsInGame)
            {
                Clear();
                return;
            }
            AnglingFightState state = session.ServerState<AnglingFightState>(AnglingFightState.StateKey);
            _state = state;
            if (string.IsNullOrEmpty(state.Species))
            {
                Clear();
                return;
            }
            if ((state.Id, state.Seed) != _key) Begin(state);
            if (_fight == null) return;

            if (state.Outcome == AnglingOutcome.None)
            {
                Capturing = true;
                _ms = Mathf.Min(_ms + Time.unscaledDeltaTime * 1000f, 500f);
                while (_ms >= AnglingFight.StepMs && !_fight.Over)
                {
                    _ms -= AnglingFight.StepMs;
                    byte input = ReadInput();
                    _lastInput = input;
                    _inputs.Add(input);
                    _fight.Step(input);
                }
                if (_inputs.Count - _sent >= BatchSteps || (_fight.Over && _sent < _inputs.Count)) Send();
                TurnCamera();
            }
            else if (_endedAt < 0f)
            {
                _endedAt = Time.unscaledTime;
                Ended(state);
            }
            Draw();
            if (_endedAt >= 0f && Time.unscaledTime - _endedAt > 2.5f) Clear();
        }

        private void Begin(AnglingFightState state)
        {
            Clear();
            _key = (state.Id, state.Seed);
            _inputs.Clear();
            _seen.Clear();
            _sent = 0;
            _ms = 0f;
            _endedAt = -1f;
            // A fight already over when we first hear of it (logging in) isn't drawn.
            if (state.Outcome != AnglingOutcome.None) return;
            _fight = new AnglingFight(state.Fish(), state.Rig);
            _spot = SpotPosition(state);
            _water = _spot.y;
            _lateral = 0f;
            _depth = 0f;
            _lastMove = AnglingMove.Straight;
            _startedAt = Time.unscaledTime;
            Build();
            FindRodTip();
            _fishPos = _spot;
            Splash(_spot, 14, 0.9f);     // the strike
            if (!_hinted)
            {
                _hinted = true;
                session.AddLog(TextKind.Info, SteerWithMouse
                    ? "Fish on! Hold Space to reel. Move the cursor the way the arrows on the fish's bar point, against where it pulls the line; they turn green when you have it. When the bar's edge flashes red, let go of Space, and let go before the line turns red."
                    : "Fish on! Hold Space to reel; let go when the line turns red, when it runs, and when it leaps.");
            }
        }

        private Vector3 SpotPosition(AnglingFightState state)
        {
            foreach (MapObject o in FindObjectsByType<MapObject>(FindObjectsInactive.Include))
            {
                Vector3 p = o.transform.position;
                if (Mathf.FloorToInt(p.x) == state.X && Mathf.FloorToInt(p.z) == state.Y) return new Vector3(state.X + 0.5f, p.y, state.Y + 0.5f);
            }
            return GroundHeight.Centre(new TileCoord(state.X, state.Y), state.Plane);
        }

        /// <summary>The rod in your hand: the far end of its mesh, along its length, from the grip is where the line starts.</summary>
        private void FindRodTip()
        {
            _hasRodTip = false;
            _rodMesh = null;
            Transform me = session.world.Local?.transform;
            if (me == null || session.Content == null) return;
            var rods = new HashSet<string>();
            foreach (ItemDef i in session.Content.Items)
                if (i.Tackle?.Slot == AnglingSlot.Rod) rods.Add(i.Id);
            foreach (Transform t in me.GetComponentsInChildren<Transform>())
            {
                if (!rods.Contains(t.name)) continue;
                float best = -1f;
                foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    Bounds b = mf.sharedMesh.bounds;
                    Vector3 axis = Dominant(b.extents);
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        Vector3 end = b.center + Vector3.Scale(b.extents, axis) * sign;
                        float d = (mf.transform.TransformPoint(end) - t.position).sqrMagnitude;
                        if (d <= best) continue;
                        best = d;
                        _rodMesh = mf.transform;
                        _rodTipLocal = end;
                    }
                }
                _hasRodTip = _rodMesh != null;
                return;
            }
        }

        /// <summary>The unit axis of a box's longest side.</summary>
        private static Vector3 Dominant(Vector3 extents) =>
            extents.x >= extents.y && extents.x >= extents.z ? Vector3.right : extents.y >= extents.z ? Vector3.up : Vector3.forward;

        private Vector3 RodTip(Transform me, Vector3 forward, Vector3 right)
        {
            if (_hasRodTip && _rodMesh != null) return _rodMesh.TransformPoint(_rodTipLocal);
            if (Time.frameCount % 30 == 0) FindRodTip();     // the rod may only now be in hand
            return me.position + Vector3.up * 1.45f + forward * 0.9f + right * 0.15f;
        }

        /// <summary>For a moment after the bite the camera swings round behind you, facing the fish.</summary>
        private void TurnCamera()
        {
            CameraRig rig = CameraRig.Current;
            Transform me = session.world.Local?.transform;
            float since = Time.unscaledTime - _startedAt;
            if (rig == null || me == null || since > 1.4f) return;
            Vector3 dir = _spot - me.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            float want = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            rig.yaw = Mathf.LerpAngle(rig.yaw, want, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f));
        }

        // ------------------------------------------------------------------ input

        private byte ReadInput()
        {
            // Space reels (no clicking: a click is the world's). The cursor only steers.
            Keyboard keyboard = Keyboard.current;
            bool reel = keyboard != null && keyboard.spaceKey.isPressed && !Ui.UiRoot.IsTyping;
            byte pull;
            if (SteerWithMouse)
            {
                Mouse mouse = Mouse.current;
                pull = mouse != null ? Steer(mouse.position.ReadValue()) : AnglingInput.PullNone;
            }
            else
            {
                // The angler leans against what it saw the fish do a moment ago.
                _seen.Enqueue(_fight.Current);
                AnglingMove seen = _seen.Count > AutoReaction ? _seen.Dequeue() : AnglingMove.Straight;
                pull = seen == AnglingMove.Left ? AnglingInput.PullRight : seen == AnglingMove.Right ? AnglingInput.PullLeft
                    : seen == AnglingMove.Dive ? AnglingInput.PullUp : AnglingInput.PullNone;
            }
            return AnglingInput.Make(reel, pull);
        }

        /// <summary>
        /// Where the cursor is on screen against a fixed line, from you to the spot the fish bit at
        /// (not to the fish, which swings: the line to it would lean with it and turn the controls
        /// round). Right of it pulls right, left of it pulls left, below you lifts.
        /// </summary>
        private byte Steer(Vector2 pointer)
        {
            Camera cam = Camera.main;
            Transform me = session.world.Local?.transform;
            if (cam == null || me == null) return AnglingInput.PullNone;
            Vector3 a = cam.WorldToScreenPoint(me.position + Vector3.up * 1.0f), b = cam.WorldToScreenPoint(new Vector3(_spot.x, _water, _spot.z));
            if (a.z < 0f || b.z < 0f) return AnglingInput.PullNone;
            Vector2 axis = new Vector2(b.x - a.x, b.y - a.y);
            if (axis.sqrMagnitude < 1f) return AnglingInput.PullNone;
            axis.Normalize();
            Vector2 right = new Vector2(axis.y, -axis.x);   // the screen's right of the line from you to the fish
            Vector2 rel = pointer - new Vector2(a.x, a.y);
            float along = Vector2.Dot(rel, axis), side = Vector2.Dot(rel, right);
            if (along < -PullDeadZone && Mathf.Abs(side) < -along) return AnglingInput.PullUp;
            if (side > PullDeadZone) return AnglingInput.PullRight;
            if (side < -PullDeadZone) return AnglingInput.PullLeft;
            return AnglingInput.PullNone;
        }

        /// <summary>Whether this input answers what the fish is doing now (the mark goes green).</summary>
        private static bool Answers(AnglingMove move, byte input)
        {
            byte pull = AnglingInput.Pull(input);
            bool reel = AnglingInput.Reeling(input);
            switch (move)
            {
                case AnglingMove.Left: return pull == AnglingInput.PullRight;
                case AnglingMove.Right: return pull == AnglingInput.PullLeft;
                case AnglingMove.Dive: return pull == AnglingInput.PullUp;
                case AnglingMove.Run:
                case AnglingMove.Jump:
                case AnglingMove.Latch: return !reel;
                default: return true;
            }
        }

        private void Send()
        {
            while (_sent < _inputs.Count)
            {
                string batch = AnglingInputs.Encode(_sent, _inputs, _sent, IntentMessage.MaxLength, out int taken);
                if (taken == 0) break;
                session.Intent(AnglingIntent, batch);
                _sent += taken;
            }
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            _root = new GameObject("Angling fight") { hideFlags = HideFlags.DontSave };
            _shadow = Particles("Fish", _root, AnglingFx.Shadow(), 1, ParticleSystemRenderMode.HorizontalBillboard);
            _rings = Particles("Rings", _root, AnglingFx.Rings(), 60, ParticleSystemRenderMode.HorizontalBillboard);
            _spray = Particles("Spray", _root, AnglingFx.Drops(), 240, ParticleSystemRenderMode.Billboard);
            ParticleSystem.MainModule sm = _spray.main;
            sm.gravityModifier = 0.6f;
            ParticleSystem.SizeOverLifetimeModule rsize = _rings.sizeOverLifetime;
            rsize.enabled = true;
            rsize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f, 0f, 2f), new Keyframe(1f, 1f, 0.3f, 0f)));
            ParticleSystem.ColorOverLifetimeModule rcol = _rings.colorOverLifetime;
            rcol.enabled = true;
            rcol.color = Fade(0.1f, 0.4f);
            ParticleSystem.ColorOverLifetimeModule scol = _spray.colorOverLifetime;
            scol.enabled = true;
            scol.color = Fade(0.05f, 0.6f);

            _outline = Line("Line shadow", AnglingFx.LineMaterial(-1), 0.075f);
            _line = Line("Line", AnglingFx.LineMaterial(0), 0.04f);

            Ui.UiRoot ui = Ui.UiRoot.Instance;
            if (ui?.WorldLayer != null)
            {
                _bar = new VisualElement { pickingMode = PickingMode.Ignore };
                _bar.AddToClassList("world-bar");
                _bar.AddToClassList("angling-energy");
                _fill = new VisualElement { pickingMode = PickingMode.Ignore };
                _fill.AddToClassList("world-bar__fill");
                var gloss = new VisualElement { pickingMode = PickingMode.Ignore };
                gloss.AddToClassList("world-bar__gloss");
                _bar.Add(_fill);
                _bar.Add(gloss);
                // Chevrons on the bar: the way to pull (drawn, not written).
                _arrows = new VisualElement { pickingMode = PickingMode.Ignore };
                _arrows.AddToClassList("angling-arrows");
                for (int i = 0; i < _chevrons.Length; i++)
                {
                    var chevron = new VisualElement { pickingMode = PickingMode.Ignore };
                    chevron.AddToClassList("angling-chevron");
                    _arrows.Add(chevron);
                    _chevrons[i] = chevron;
                }
                _bar.Add(_arrows);
                ui.WorldLayer.Add(_bar);
            }
        }

        private LineRenderer Line(string name, Material material, float width)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root.transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 16;
            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            return line;
        }

        private static Gradient Fade(float inBy, float holdTo)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inBy), new GradientAlphaKey(1f, holdTo), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        private static ParticleSystem Particles(string name, GameObject parent, Material material, int max, ParticleSystemRenderMode mode)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent.transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startSize3D = true;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ drawing

        private void Draw()
        {
            if (_fight == null || _root == null) return;
            Transform me = session.world.Local?.transform;
            if (me == null) return;
            float dt = Time.deltaTime, t = Time.time;
            Vector3 player = me.position;
            Vector3 toSpot = _spot - player;
            toSpot.y = 0f;
            float reach = Mathf.Max(1.5f, toSpot.magnitude);
            Vector3 forward = toSpot.sqrMagnitude > 0.01f ? toSpot.normalized : me.forward;
            Vector3 right = ScreenRight(forward);
            bool over = _endedAt >= 0f;

            // Where the fish is: as far out as the fight says, swung wide to the side it swims, down as it dives.
            AnglingMove move = over ? AnglingMove.Straight : _fight.Current;
            float lateralTarget = move == AnglingMove.Left ? -Swing : move == AnglingMove.Right ? Swing : 0f;
            _lateral = Mathf.MoveTowards(_lateral, lateralTarget, dt * (move == AnglingMove.Run ? 1.2f : 2.6f));
            float depthTarget = move == AnglingMove.Dive || move == AnglingMove.Latch ? 1f : 0f;
            _depth = Mathf.MoveTowards(_depth, depthTarget, dt * 1.8f);
            float out01 = Mathf.Clamp((float)_fight.Distance / Mathf.Max(1, _fight.Distance0), 0f, 3f);
            float along = Mathf.Max(0.9f, out01 * reach);
            Vector3 target = player + forward * along + right * (_lateral * Mathf.Clamp01(out01 + 0.25f));
            target.y = _water;
            Vector3 was = _fishPos;
            _fishPos = Vector3.Lerp(_fishPos, target, 1f - Mathf.Exp(-dt * 5f));
            Vector3 heading = _fishPos - was;
            heading.y = 0f;
            if (heading.sqrMagnitude < 1e-6f) heading = -forward;

            // The fish: a dark shape, longer than wide, fainter and smaller as it dives, hidden in the air.
            bool airborne = move == AnglingMove.Jump;
            float size = 0.7f + 0.35f * Mathf.Clamp01(_fight.Energy0 / 15000f);
            float alpha = airborne || over ? 0f : Mathf.Lerp(0.75f, 0.3f, _depth);
            _one[0] = new ParticleSystem.Particle
            {
                position = _fishPos + Vector3.up * 0.02f,
                startSize3D = new Vector3(size * 0.42f, size * 1.4f, 1f) * Mathf.Lerp(1f, 0.8f, _depth),
                rotation = -Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg,
                startColor = _state.Revised ? new Color(0.22f, 0.06f, 0.32f, alpha) : new Color(0.03f, 0.04f, 0.06f, alpha),
                remainingLifetime = 10f,
                startLifetime = 10f,
            };
            _shadow.SetParticles(_one, 1);

            if (!over)
            {
                Tells(move, right, forward);
                Thrash(t);
                PullSpray(move, right, t);
            }

            // The line: from the rod tip to the fish, its colour the tension.
            Vector3 tip = RodTip(me, forward, right);
            float tension = _fight.Tension / (float)AnglingFight.TensionMax;
            bool slack = _fight.Tension <= AnglingFight.SlackLine, red = _fight.Tension >= AnglingFight.RedLine;
            float sag = Mathf.Lerp(0.9f, 0.03f, Mathf.Clamp01(tension / 0.5f));
            Vector3 end = airborne ? _fishPos + Vector3.up * (0.7f * Mathf.Sin(Mathf.PI * Mathf.Repeat(t * 1.2f, 1f))) : _fishPos;
            for (int i = 0; i < _line.positionCount; i++)
            {
                float u = i / (float)(_line.positionCount - 1);
                Vector3 p = Vector3.Lerp(tip, end, u);
                p.y -= sag * 4f * u * (1f - u);
                // The fish drags the line its way: it bows out to the side the fish pulls.
                p += right * (_lateral / Swing) * 0.45f * Mathf.Clamp01(tension / 0.4f) * 4f * u * (1f - u);
                if (red) p += right * (Mathf.Sin(t * 60f + u * 9f) * 0.03f * Mathf.Sin(Mathf.PI * u));
                _line.SetPosition(i, p);
                _outline.SetPosition(i, p);
            }
            Color c = slack ? new Color(0.78f, 0.78f, 0.78f, 0.9f)
                : tension < AnglingFight.ComfortLow / 1000f ? new Color(0.92f, 0.92f, 0.85f, 1f)
                : tension <= AnglingFight.ComfortHigh / 1000f ? new Color(0.45f, 0.95f, 0.45f, 1f)
                : red ? Color.Lerp(new Color(1f, 0.2f, 0.12f, 1f), Color.white, 0.25f + 0.25f * Mathf.Sin(t * 18f))
                : new Color(1f, 0.72f, 0.2f, 1f);
            if (over) c.a = Mathf.Clamp01(1f - (Time.unscaledTime - _endedAt) / 0.6f);
            _line.startColor = _line.endColor = c;
            Color shade = new Color(0f, 0f, 0f, 0.55f * c.a);
            _outline.startColor = _outline.endColor = shade;
            _line.widthMultiplier = red ? 0.055f : 0.04f;

            DrawMarks(move, over);
        }

        /// <summary>
        /// The energy bar over the fish, with chevrons on it pointing the way to pull against it:
        /// amber while the fish shows what it will do, white while it does it, green while you answer
        /// it. When it runs, leaps or latches there is nothing to pull: the bar's edge flashes red
        /// (let go). No words.
        /// </summary>
        private void DrawMarks(AnglingMove move, bool over)
        {
            Camera cam = Camera.main;
            if (_bar == null || cam == null || _bar.panel == null) return;
            Vector3 above = _fishPos + Vector3.up * 0.9f;
            bool visible = cam.WorldToScreenPoint(above).z > 0f && !over;
            _bar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(_bar.panel, above, cam);
            _bar.style.left = p.x;
            _bar.style.top = p.y;
            float fresh = Mathf.Clamp01(_fight.Energy / (float)Mathf.Max(1, _fight.Energy0));
            _fill.style.width = Length.Percent(fresh * 100f);
            _fill.style.backgroundColor = fresh > 0.5f ? new Color(0.85f, 0.65f, 0.25f) : fresh > 0.2f ? new Color(0.9f, 0.5f, 0.2f) : new Color(0.55f, 0.85f, 0.4f);

            bool telling = _fight.InTell && _fight.TellVisible;
            AnglingMove asks = telling ? _fight.Tell : move;
            // A chevron is a box's top and right edges: rotated 45 degrees it points right, 225 left, 135 down (lift: towards you).
            float angle = asks == AnglingMove.Left ? 45f : asks == AnglingMove.Right ? 225f : 135f;
            bool pull = asks == AnglingMove.Left || asks == AnglingMove.Right || asks == AnglingMove.Dive;
            bool letGo = asks == AnglingMove.Run || asks == AnglingMove.Jump || asks == AnglingMove.Latch;
            Color colour = telling ? new Color(1f, 0.8f, 0.35f) : Answers(move, _lastInput) ? new Color(0.5f, 1f, 0.5f) : Color.white;
            _arrows.style.display = pull ? DisplayStyle.Flex : DisplayStyle.None;
            _arrows.style.flexDirection = asks == AnglingMove.Dive ? FlexDirection.Column : FlexDirection.Row;
            for (int i = 0; i < _chevrons.Length; i++)
            {
                VisualElement c = _chevrons[i];
                c.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
                c.style.borderTopColor = colour;
                c.style.borderRightColor = colour;
                // A wave runs along them the way to pull.
                int order = asks == AnglingMove.Right ? _chevrons.Length - 1 - i : i;
                c.style.opacity = 0.45f + 0.55f * Mathf.Clamp01(Mathf.Sin(Time.time * 8f - order * 1.2f) * 0.5f + 0.5f);
            }
            Color edge = letGo ? Color.Lerp(new Color(0.35f, 0.08f, 0.06f), new Color(1f, 0.25f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.time * 14f))
                : telling ? new Color(1f, 0.8f, 0.35f) : new Color(70 / 255f, 60 / 255f, 48 / 255f);
            _bar.style.borderTopColor = edge;
            _bar.style.borderBottomColor = edge;
            _bar.style.borderLeftColor = edge;
            _bar.style.borderRightColor = edge;
        }

        /// <summary>The horizontal direction that goes to the right on screen, across the line to the fish.</summary>
        private static Vector3 ScreenRight(Vector3 forward)
        {
            Vector3 r = Vector3.Cross(Vector3.up, forward).normalized;
            Camera cam = Camera.main;
            if (cam != null && Vector3.Dot(cam.transform.right, r) < 0f) r = -r;
            return r;
        }

        /// <summary>A move's tell, once, as it begins (if the fish shows its tells); and the moment a move starts.</summary>
        private void Tells(AnglingMove move, Vector3 right, Vector3 forward)
        {
            bool inTell = _fight.InTell && _fight.TellVisible;
            if (inTell && !_lastInTell)
            {
                AnglingMove next = _fight.Tell;
                switch (next)
                {
                    case AnglingMove.Left:
                    case AnglingMove.Right:
                        Vector3 side = right * (next == AnglingMove.Left ? -1f : 1f);
                        Ring(_fishPos + side * 0.8f, 1.1f, 1.2f, 0.8f);
                        Ring(_fishPos + side * 1.2f, 0.7f, 1.0f, 0.6f);
                        break;
                    case AnglingMove.Dive:
                        Bubbles(_fishPos, 22);
                        break;
                    case AnglingMove.Run:
                        for (int i = 0; i < 5; i++) Ring(_fishPos + forward * (0.3f + i * 0.4f), 0.5f + i * 0.15f, 0.9f + i * 0.15f, 0.7f);
                        break;
                    case AnglingMove.Jump:
                        Bubbles(_fishPos, 36);
                        Ring(_fishPos, 1.6f, 0.7f, 0.9f);
                        break;
                    case AnglingMove.Latch:
                        Ring(_fishPos, 0.6f, 1.6f, 0.5f);
                        break;
                }
            }
            _lastInTell = inTell;
            if (move != _lastMove)
            {
                if (move == AnglingMove.Jump) Splash(_fishPos, 24, 1.4f);
                if (_lastMove == AnglingMove.Jump) Splash(_fishPos, 28, 1.1f);
                if (move == AnglingMove.Run) Splash(_fishPos, 10, 0.7f);
                _lastMove = move;
            }
        }

        /// <summary>How much fight it has left: a fresh fish thrashes often and hard, a tired one barely.</summary>
        private void Thrash(float t)
        {
            if (_fight.Current == AnglingMove.Jump || t < _nextThrash) return;
            float fresh = Mathf.Clamp01(_fight.Energy / (float)Mathf.Max(1, _fight.Energy0));
            _nextThrash = t + Mathf.Lerp(2.2f, 0.45f, fresh) * Random.Range(0.7f, 1.3f);
            if (_depth > 0.6f) return;
            Splash(_fishPos, Mathf.RoundToInt(Mathf.Lerp(2f, 8f, fresh)), Mathf.Lerp(0.35f, 0.8f, fresh));
        }

        /// <summary>Pulling it the right way shows: spray off the fish, away from where it wants to go.</summary>
        private void PullSpray(AnglingMove move, Vector3 right, float t)
        {
            if (t < _nextSpray) return;
            byte pull = AnglingInput.Pull(_lastInput);
            bool turning = move == AnglingMove.Left || move == AnglingMove.Right || move == AnglingMove.Dive;
            if (!turning || !Answers(move, _lastInput) || pull == AnglingInput.PullNone) return;
            _nextSpray = t + 0.18f;
            Vector3 away = pull == AnglingInput.PullRight ? right : pull == AnglingInput.PullLeft ? -right : Vector3.up * 0.3f;
            for (int i = 0; i < 3; i++)
                _spray.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(_fishPos.x, _water + 0.05f, _fishPos.z),
                    velocity = away * Random.Range(0.8f, 1.4f) + Vector3.up * Random.Range(0.8f, 1.5f),
                    startSize3D = Vector3.one * Random.Range(0.07f, 0.13f),
                    startLifetime = Random.Range(0.35f, 0.6f),
                    startColor = new Color(0.85f, 1f, 0.9f, 0.9f),
                }, 1);
        }

        private void Ended(AnglingFightState state)
        {
            if (state.Outcome == AnglingOutcome.Landed)
            {
                Transform me = session.world.Local?.transform;
                Vector3 at = me != null ? me.position + (_fishPos - me.position) * 0.3f : _fishPos;
                Splash(at, 30, 1.3f);
            }
            else
            {
                Ring(_fishPos, 1.4f, 1.4f, 0.8f);
            }
        }

        // ------------------------------------------------------------------ effects

        private void Ring(Vector3 at, float size, float life, float alpha)
        {
            _rings.Emit(new ParticleSystem.EmitParams
            {
                position = new Vector3(at.x, _water + 0.03f, at.z),
                startSize3D = new Vector3(size, size, 1f),
                startLifetime = life,
                startColor = new Color(0.85f, 0.95f, 1f, alpha),
                velocity = Vector3.zero,
            }, 1);
        }

        private void Bubbles(Vector3 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 r = Random.insideUnitCircle * 0.4f;
                _spray.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(at.x + r.x, _water + 0.02f, at.z + r.y),
                    velocity = new Vector3(0f, Random.Range(0.4f, 0.9f), 0f),
                    startSize3D = Vector3.one * Random.Range(0.06f, 0.12f),
                    startLifetime = Random.Range(0.3f, 0.7f),
                    startColor = new Color(0.8f, 0.95f, 1f, 0.8f),
                }, 1);
            }
        }

        private void Splash(Vector3 at, int count, float strength)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle;
                _spray.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(at.x, _water + 0.05f, at.z),
                    velocity = new Vector3(dir.x * strength, Random.Range(1.2f, 2.6f) * strength, dir.y * strength),
                    startSize3D = Vector3.one * Random.Range(0.07f, 0.15f),
                    startLifetime = Random.Range(0.45f, 0.9f),
                    startColor = new Color(0.9f, 0.97f, 1f, 0.9f),
                }, 1);
            }
            Ring(at, 0.7f + strength * 0.5f, 1.0f, 0.7f);
        }

        private void Clear()
        {
            Capturing = false;
            if (_root != null) Destroy(_root);
            _root = null;
            _fight = null;
            _bar?.RemoveFromHierarchy();
            _bar = null;
            _arrows = null;
        }

        private void OnDisable() => Clear();
    }

    /// <summary>The materials and textures the fight is drawn with (Shaders/AnglingOverlay: over everything).</summary>
    internal static class AnglingFx
    {
        private static Material _shadow, _rings, _drops, _line, _lineShadow;

        public static Material Shadow() => _shadow != null ? _shadow : _shadow = Make(Ellipse(), 1);
        public static Material Rings() => _rings != null ? _rings : _rings = Make(Ring(), 2);
        public static Material Drops() => _drops != null ? _drops : _drops = Make(Soft(), 3);

        /// <summary>The line (order 0) and its dark edge drawn just under it (order -1), so it reads on any background.</summary>
        public static Material LineMaterial(int order)
        {
            if (order < 0) return _lineShadow != null ? _lineShadow : _lineShadow = Make(Strip(), -1);
            return _line != null ? _line : _line = Make(Strip(), 4);
        }

        private static Material Make(Texture2D texture, int order)
        {
            Shader shader = Resources.Load<Shader>("Shaders/AnglingOverlay");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(shader) { hideFlags = HideFlags.DontSave };
            m.SetTexture("_BaseMap", texture);
            m.renderQueue = 3050 + order;
            return m;
        }

        private static Texture2D Tex(string name, int n, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, name = name };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f)));
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>A fish's shadow from above: an ellipse, soft edged, narrower at the tail.</summary>
        private static Texture2D Ellipse() => Tex("FishShadow", 64, (u, v) =>
        {
            float w = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01((v + 1f) / 1.4f));
            float r = Mathf.Sqrt(u * u / (w * w) + v * v);
            return Mathf.Clamp01((1f - r) * 5f);
        });

        private static Texture2D Ring() => Tex("Ripple", 64, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return Mathf.Exp(-Mathf.Pow((r - 0.85f) / 0.08f, 2f));
        });

        private static Texture2D Soft() => Tex("Drop", 32, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return Mathf.Clamp01((1f - r) * 2.5f);
        });

        /// <summary>The line: solid across, softened at its very edges.</summary>
        private static Texture2D Strip() => Tex("Line", 8, (u, v) => Mathf.Clamp01((1f - Mathf.Abs(v)) * 3f));
    }
}
