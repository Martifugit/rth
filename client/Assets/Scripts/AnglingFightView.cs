using System.Collections.Generic;
using Rth.MapTools;
using Rth.Shared.Angling;
using Rth.Shared.Net;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Rth.Client
{
    /// <summary>
    /// Trophy angling's fight, in the world (docs/fishing.md 3.3): no panel. While a fish is on it
    /// plays the same <see cref="AnglingFight"/> the server runs (from the "anglingFight" state) at
    /// 50 ms steps, and sends what the player does as the "angling" intent in small batches; the
    /// server replays them and decides.
    ///
    /// What you see: the fish as a dark shape under the water, nearer as you reel it in, swinging
    /// to the side it swims, sinking as it dives, gone in a splash as it leaps; the line from your
    /// rod to it, sagging when slack, straight when taut, reddening and trembling near snapping;
    /// its tells as water effects (a swirl on the side it will turn to, bubbles before a dive, a
    /// wake before a run, the surface boiling before a leap); and how much fight it has left in
    /// how hard it thrashes. It is drawn from effects alone, like a fishing spot.
    ///
    /// The mouse is the rod while the fight lasts (world clicks are ignored: <see cref="Capturing"/>).
    /// Two ways to fight, a setting (<see cref="SteerWithMouse"/>), to find which feels right:
    /// steer (hold the left button to reel; sweep the cursor across the line to the opposite side
    /// from where the fish swims; pull it back towards you to lift against a dive) or reel only
    /// (hold to reel; your angler leans against the fish by itself).
    /// </summary>
    public sealed class AnglingFightView : MonoBehaviour
    {
        public GameSession session;

        /// <summary>A fight is on: the mouse is the rod, and world clicks are ignored.</summary>
        public static bool Capturing { get; private set; }

        private const string SteerPref = "rth.angling.steer";
        private const string AnglingIntent = "angling";
        private const int BatchSteps = 6;
        private const int AutoReaction = 6;
        /// <summary>Pixels the cursor must be off the line to count as a pull.</summary>
        private const float PullDeadZone = 50f;

        /// <summary>Steer the fish with the mouse (otherwise the angler steers and you only reel).</summary>
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
        private float _endedAt = -1f;
        private bool _hinted;

        private Vector3 _spot;
        private float _water;
        private Vector3 _fishPos;
        private float _lateral;
        private float _depth;
        private AnglingMove _lastMove = AnglingMove.Straight;
        private bool _lastInTell;
        private float _nextThrash;

        private GameObject _root;
        private ParticleSystem _shadow;
        private ParticleSystem _rings;
        private ParticleSystem _spray;
        private LineRenderer _line;
        private readonly ParticleSystem.Particle[] _one = new ParticleSystem.Particle[1];

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
                    _inputs.Add(input);
                    _fight.Step(input);
                }
                if (_inputs.Count - _sent >= BatchSteps || (_fight.Over && _sent < _inputs.Count)) Send();
            }
            else if (_endedAt < 0f)
            {
                _endedAt = Time.unscaledTime;
                Ended(state);
            }
            Draw();
            if (_endedAt >= 0f && Time.unscaledTime - _endedAt > 1.2f) Clear();
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
            Build();
            _fishPos = _spot;
            Splash(_spot, 14, 0.9f);     // the strike
            if (!_hinted)
            {
                _hinted = true;
                session.AddLog(TextKind.Info, SteerWithMouse
                    ? "Fish on! Hold the left mouse button to reel. Sweep the mouse to the side the fish isn't swimming, pull it back to lift against a dive, and let go when it runs or leaps."
                    : "Fish on! Hold the left mouse button to reel; let go when the line strains, when it runs, and when it leaps.");
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

        // ------------------------------------------------------------------ input

        private byte ReadInput()
        {
            Mouse mouse = Mouse.current;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            bool overUi = Ui.UiRoot.IsPointerOverUi(pointer);
            bool reel = mouse != null && mouse.leftButton.isPressed && !overUi;
            byte pull = AnglingInput.PullNone;
            if (SteerWithMouse) pull = Steer(pointer);
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

        /// <summary>Where the cursor is against the line on screen: off to one side pulls that way; back towards you lifts.</summary>
        private byte Steer(Vector2 pointer)
        {
            Camera cam = Camera.main;
            Transform me = session.world.Local?.transform;
            if (cam == null || me == null) return AnglingInput.PullNone;
            Vector3 a = cam.WorldToScreenPoint(me.position + Vector3.up * 1.2f), b = cam.WorldToScreenPoint(_fishPos);
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

        // ------------------------------------------------------------------ drawing

        private void Build()
        {
            _root = new GameObject("Angling fight") { hideFlags = HideFlags.DontSave };
            _shadow = Particles("Fish", _root, AnglingFx.Shadow(), 1, ParticleSystemRenderMode.HorizontalBillboard);
            _rings = Particles("Rings", _root, AnglingFx.Rings(), 40, ParticleSystemRenderMode.HorizontalBillboard);
            _spray = Particles("Spray", _root, AnglingFx.Glow(), 200, ParticleSystemRenderMode.Billboard);
            ParticleSystem.MainModule sm = _spray.main;
            sm.gravityModifier = 0.6f;
            // Rings spread as they fade; drops fade as they fall.
            ParticleSystem.SizeOverLifetimeModule rsize = _rings.sizeOverLifetime;
            rsize.enabled = true;
            rsize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f, 0f, 2f), new Keyframe(1f, 1f, 0.3f, 0f)));
            ParticleSystem.ColorOverLifetimeModule rcol = _rings.colorOverLifetime;
            rcol.enabled = true;
            rcol.color = Fade(0.1f, 0.4f);
            ParticleSystem.ColorOverLifetimeModule scol = _spray.colorOverLifetime;
            scol.enabled = true;
            scol.color = Fade(0.05f, 0.6f);
            var lineGo = new GameObject("Line") { hideFlags = HideFlags.DontSave };
            lineGo.transform.SetParent(_root.transform, false);
            _line = lineGo.AddComponent<LineRenderer>();
            _line.sharedMaterial = AnglingFx.LineMaterial();
            _line.positionCount = 12;
            _line.widthMultiplier = 0.018f;
            _line.numCapVertices = 2;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
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

            // Where the fish is: as far out as the fight says, off to the side it swims, down as it dives.
            AnglingMove move = _fight.Current;
            float lateralTarget = move == AnglingMove.Left ? -1.1f : move == AnglingMove.Right ? 1.1f : 0f;
            _lateral = Mathf.MoveTowards(_lateral, lateralTarget, dt * (move == AnglingMove.Run ? 0.6f : 1.4f));
            float depthTarget = move == AnglingMove.Dive || move == AnglingMove.Latch ? 1f : 0f;
            _depth = Mathf.MoveTowards(_depth, depthTarget, dt * 1.5f);
            float out01 = Mathf.Clamp((float)_fight.Distance / Mathf.Max(1, _fight.Distance0), 0f, 3f);
            float along = Mathf.Max(0.8f, out01 * reach);
            Vector3 target = player + forward * along + right * (_lateral * Mathf.Min(1f, out01 + 0.3f));
            target.y = _water;
            _fishPos = Vector3.Lerp(_fishPos, target, 1f - Mathf.Exp(-dt * 6f));
            Vector3 heading = (forward * (move == AnglingMove.Run ? 1f : -0.3f) + right * Mathf.Sign(lateralTarget == 0f ? 0.001f : lateralTarget) * (lateralTarget == 0f ? 0f : 1f)).normalized;

            // The fish: a dark shape, longer than wide, fainter and smaller as it dives, hidden in the air.
            bool airborne = move == AnglingMove.Jump;
            float size = 0.45f + 0.25f * Mathf.Clamp01(_fight.Energy0 / 12000f);
            float alpha = airborne ? 0f : Mathf.Lerp(0.55f, 0.18f, _depth);
            _one[0] = new ParticleSystem.Particle
            {
                position = _fishPos + Vector3.up * 0.015f,
                startSize3D = new Vector3(size * 0.45f, size * 1.4f, 1f) * Mathf.Lerp(1f, 0.8f, _depth),
                rotation = -Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg,
                startColor = _state.Revised ? new Color(0.15f, 0.05f, 0.25f, alpha) : new Color(0.02f, 0.03f, 0.04f, alpha),
                remainingLifetime = 10f,
                startLifetime = 10f,
            };
            _shadow.SetParticles(_one, 1);

            Tells(move, right, forward, t);
            Thrash(t);

            // The line: from the rod tip to the fish, sagging when slack, straight when taut, red and trembling near snapping.
            Vector3 tip = player + Vector3.up * 1.45f + forward * 0.7f + right * 0.12f;
            float tension = _fight.Tension / (float)AnglingFight.TensionMax;
            float sag = Mathf.Lerp(0.7f, 0.02f, Mathf.Clamp01(tension / 0.55f));
            bool red = _fight.Tension >= AnglingFight.RedLine;
            Vector3 end = airborne ? _fishPos + Vector3.up * (0.6f * Mathf.Sin(Mathf.PI * Mathf.Repeat(t * 1.2f, 1f))) : _fishPos;
            for (int i = 0; i < _line.positionCount; i++)
            {
                float u = i / (float)(_line.positionCount - 1);
                Vector3 p = Vector3.Lerp(tip, end, u);
                p.y -= sag * 4f * u * (1f - u);
                if (red) p += right * (Mathf.Sin(t * 70f + u * 9f) * 0.012f * Mathf.Sin(Mathf.PI * u));
                _line.SetPosition(i, p);
            }
            Color calm = new Color(0.9f, 0.88f, 0.8f, 0.85f), strain = new Color(1f, 0.75f, 0.3f, 0.95f), danger = new Color(1f, 0.25f, 0.15f, 1f);
            Color c = tension < 0.6f ? calm : tension < 0.85f ? Color.Lerp(calm, strain, (tension - 0.6f) / 0.25f) : Color.Lerp(strain, danger, (tension - 0.85f) / 0.15f);
            if (_fight.Tension <= AnglingFight.SlackLine) c = new Color(0.75f, 0.75f, 0.75f, 0.6f);
            _line.startColor = _line.endColor = c;
            _line.widthMultiplier = red ? 0.026f : 0.018f;
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
        private void Tells(AnglingMove move, Vector3 right, Vector3 forward, float t)
        {
            bool inTell = _fight.InTell && _fight.TellVisible;
            if (inTell && !_lastInTell)
            {
                AnglingMove next = _fight.Tell;
                switch (next)
                {
                    case AnglingMove.Left:
                    case AnglingMove.Right:
                        // A swirl on the side it is about to turn to.
                        Ring(_fishPos + right * (next == AnglingMove.Left ? -0.6f : 0.6f), 0.9f, 1.2f, 0.7f);
                        Ring(_fishPos + right * (next == AnglingMove.Left ? -0.75f : 0.75f), 0.5f, 1.0f, 0.5f);
                        break;
                    case AnglingMove.Dive:
                        Bubbles(_fishPos, 18);
                        break;
                    case AnglingMove.Run:
                        // A wake opening away from you.
                        for (int i = 0; i < 4; i++) Ring(_fishPos + forward * (0.3f + i * 0.35f), 0.4f + i * 0.15f, 0.9f + i * 0.15f, 0.6f);
                        break;
                    case AnglingMove.Jump:
                        Bubbles(_fishPos, 30);
                        Ring(_fishPos, 1.4f, 0.7f, 0.9f);
                        break;
                    case AnglingMove.Latch:
                        Ring(_fishPos, 0.5f, 1.6f, 0.4f);
                        break;
                }
            }
            _lastInTell = inTell;
            if (move != _lastMove)
            {
                if (move == AnglingMove.Jump) Splash(_fishPos, 22, 1.4f);
                if (_lastMove == AnglingMove.Jump) Splash(_fishPos, 26, 1.1f);
                if (move == AnglingMove.Run) Splash(_fishPos, 8, 0.6f);
                _lastMove = move;
            }
        }

        /// <summary>How much fight it has left: a fresh fish thrashes often and hard, a tired one barely.</summary>
        private void Thrash(float t)
        {
            if (_fight.Current == AnglingMove.Jump || t < _nextThrash) return;
            float fresh = Mathf.Clamp01(_fight.Energy / (float)Mathf.Max(1, _fight.Energy0));
            _nextThrash = t + Mathf.Lerp(2.2f, 0.35f, fresh) * Random.Range(0.7f, 1.3f);
            if (_depth > 0.6f) return;
            Splash(_fishPos, Mathf.RoundToInt(Mathf.Lerp(2f, 8f, fresh)), Mathf.Lerp(0.35f, 0.8f, fresh));
            Ring(_fishPos, Mathf.Lerp(0.4f, 0.9f, fresh), 1.1f, 0.5f);
        }

        private void Ended(AnglingFightState state)
        {
            if (state.Outcome == AnglingOutcome.Landed)
            {
                Transform me = session.world.Local?.transform;
                Vector3 at = me != null ? me.position + (_fishPos - me.position) * 0.3f : _fishPos;
                Splash(at, 24, 1.2f);
            }
            else Ring(_fishPos, 1.2f, 1.4f, 0.7f);
        }

        // ------------------------------------------------------------------ effects

        private void Ring(Vector3 at, float size, float life, float alpha)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = new Vector3(at.x, _water + 0.03f, at.z),
                startSize3D = new Vector3(size, size, 1f),
                startLifetime = life,
                startColor = new Color(0.85f, 0.95f, 1f, alpha),
                velocity = Vector3.zero,
            };
            _rings.Emit(p, 1);
            // Rings grow as they fade (size and colour over lifetime are set up once).
        }

        private void Bubbles(Vector3 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 r = Random.insideUnitCircle * 0.35f;
                _spray.Emit(new ParticleSystem.EmitParams
                {
                    position = new Vector3(at.x + r.x, _water + 0.02f, at.z + r.y),
                    velocity = new Vector3(0f, Random.Range(0.4f, 0.9f), 0f),
                    startSize3D = Vector3.one * Random.Range(0.05f, 0.11f),
                    startLifetime = Random.Range(0.3f, 0.7f),
                    startColor = new Color(0.8f, 0.95f, 1f, 0.7f),
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
                    startSize3D = Vector3.one * Random.Range(0.06f, 0.14f),
                    startLifetime = Random.Range(0.45f, 0.9f),
                    startColor = new Color(0.9f, 0.97f, 1f, 0.85f),
                }, 1);
            }
            Ring(at, 0.6f + strength * 0.5f, 1.0f, 0.6f);
        }

        private void Clear()
        {
            Capturing = false;
            if (_root != null) Destroy(_root);
            _root = null;
            _fight = null;
        }

        private void OnDisable()
        {
            Capturing = false;
            if (_root != null) Destroy(_root);
            _root = null;
        }
    }

    /// <summary>The materials and textures the fight is drawn with: the particle shader the spots use.</summary>
    internal static class AnglingFx
    {
        private static Material _shadow, _rings, _glow, _line;

        public static Material Shadow() => _shadow != null ? _shadow : _shadow = Make(Ellipse(), BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);
        public static Material Rings() => _rings != null ? _rings : _rings = Make(Ring(), BlendMode.One, BlendMode.One);
        public static Material Glow() => _glow != null ? _glow : _glow = Make(Soft(), BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);
        public static Material LineMaterial() => _line != null ? _line : _line = Make(Strip(), BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);

        private static Material Make(Texture2D texture, BlendMode src, BlendMode dst)
        {
            Shader shader = Resources.Load<Shader>("Shaders/Particle");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(shader) { hideFlags = HideFlags.DontSave };
            m.SetTexture("_BaseMap", texture);
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
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

        /// <summary>A fish's shadow from above: an ellipse, soft edged, a little narrower at the tail.</summary>
        private static Texture2D Ellipse() => Tex("FishShadow", 64, (u, v) =>
        {
            float w = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01((v + 1f) / 1.4f));
            float r = Mathf.Sqrt(u * u / (w * w) + v * v);
            return Mathf.Clamp01((1f - r) * 4f);
        });

        private static Texture2D Ring() => Tex("Ripple", 64, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return Mathf.Exp(-Mathf.Pow((r - 0.85f) / 0.07f, 2f));
        });

        private static Texture2D Soft() => Tex("Drop", 32, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            return Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r) * 1.6f;
        });

        private static Texture2D Strip() => Tex("Line", 8, (u, v) => 1f - Mathf.Abs(u) * 0.5f);
    }
}
