using System.Collections.Generic;
using Rth.Shared.Angling;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The reeling panel (trophy angling, docs/fishing.md 3.3): while a fish is on, it plays the
    /// same <see cref="AnglingFight"/> the server runs, from the "anglingFight" state, live at 50 ms
    /// steps, and sends what the player does as the "angling" intent in small batches; the server
    /// steps its copy through them and decides the outcome. Hold Space (or the water) to reel; A
    /// and D pull left and right, W pulls up. It shows the fish (facing the way it swims, wrong if
    /// revised), its tell before each move, the tension bar, distance and energy, then how it
    /// ended: the weight, and a personal best.
    /// </summary>
    public sealed class AnglingPanel : UiPanel
    {
        public override string Id => "angling";
        public override string Title => _species != null ? (_revised ? "Revised " + _species.Name.ToLowerInvariant() : _species.Name) : "Reeling";
        public override string Icon => "fishing";
        public override string DefaultWindow => UiDefaultLayout.Town;
        public override bool OpenByDefault => false;
        public override Vector2? FixedSize => new Vector2(380f, 330f);
        public override bool? ServerOpen => _open;
        public override void OnClosedByPlayer()
        {
            if (_fight != null && !_fight.Over) Session.Intent(AnglingIntent, "quit");
            _resultUntil = 0f;
        }

        private const string AnglingIntent = "angling";
        private const int BatchSteps = 6;
        private const float ResultSeconds = 5f;

        private AnglingFight _fight;
        private (int Id, uint Seed) _key = (-1, 0);
        private readonly List<byte> _inputs = new List<byte>();
        private int _sent;
        private float _ms;
        private float _resultUntil;
        private bool _open;
        private bool _mouseReel;
        private bool _revised;
        private AnglingSpeciesDef _species;
        private AnglingFightState _state;

        private VisualElement _water;
        private VisualElement _fish;
        private Label _tell;
        private VisualElement _tensionMark;
        private VisualElement _distanceFill;
        private VisualElement _energyFill;
        private Label _result;
        private Label _hint;

        private static readonly Color RevisedTint = new Color(0.62f, 0.95f, 0.85f);
        private static readonly Color RedZone = new Color(0.62f, 0.16f, 0.12f);
        private static readonly Color Comfort = new Color(0.24f, 0.42f, 0.22f);
        private static readonly Color Slack = new Color(0.3f, 0.3f, 0.32f);

        protected override void Build(VisualElement root)
        {
            _water = UiKit.El(root, "well");
            _water.style.height = 150f;
            _water.style.position = Position.Relative;
            _water.style.backgroundColor = new Color(0.08f, 0.14f, 0.16f, 0.9f);
            _water.RegisterCallback<PointerDownEvent>(_ => _mouseReel = true);
            _water.RegisterCallback<PointerUpEvent>(_ => _mouseReel = false);
            _water.RegisterCallback<PointerLeaveEvent>(_ => _mouseReel = false);

            _fish = UiKit.Icon(_water, IconLibrary.Get("fish"), 56f);
            _fish.style.position = Position.Absolute;
            _fish.pickingMode = PickingMode.Ignore;
            _tell = UiKit.Text(_water, "", "small");
            _tell.style.position = Position.Absolute;
            _tell.style.left = 0f;
            _tell.style.right = 0f;
            _tell.style.top = 4f;
            _tell.style.unityTextAlign = TextAnchor.UpperCenter;
            _tell.pickingMode = PickingMode.Ignore;

            UiKit.Text(root, "Tension", "small").style.marginTop = 6f;
            VisualElement tension = Track(root, out _);
            Zone(tension, 0, AnglingFight.SlackLine, Slack);
            Zone(tension, AnglingFight.ComfortLow, AnglingFight.ComfortHigh, Comfort);
            Zone(tension, AnglingFight.RedLine, AnglingFight.TensionMax, RedZone);
            _tensionMark = UiKit.El(tension);
            _tensionMark.style.position = Position.Absolute;
            _tensionMark.style.width = 4f;
            _tensionMark.style.top = 0f;
            _tensionMark.style.bottom = 0f;
            _tensionMark.style.backgroundColor = UiKit.Value;

            VisualElement row = UiKit.El(root, "row");
            row.style.marginTop = 4f;
            VisualElement left = UiKit.El(row);
            left.style.flexGrow = 1;
            left.style.marginRight = 6f;
            UiKit.Text(left, "Distance", "small");
            Track(left, out _distanceFill).style.height = 10f;
            _distanceFill.style.backgroundColor = new Color(0.35f, 0.55f, 0.7f);
            VisualElement right = UiKit.El(row);
            right.style.flexGrow = 1;
            UiKit.Text(right, "Energy", "small");
            Track(right, out _energyFill).style.height = 10f;
            _energyFill.style.backgroundColor = UiKit.YellowBile;

            _result = UiKit.Text(root, "", "prose");
            _result.style.marginTop = 6f;
            _hint = UiKit.Text(root, UiKit.Colour("Hold Space or the water to reel.  A / D pull left / right.  W pulls up.", UiKit.Muted), "small");
            _hint.style.marginTop = 4f;
        }

        private static VisualElement Track(VisualElement parent, out VisualElement fill)
        {
            VisualElement track = UiKit.El(parent, "well");
            track.style.height = 16f;
            track.style.position = Position.Relative;
            track.style.paddingLeft = track.style.paddingRight = track.style.paddingTop = track.style.paddingBottom = 0f;
            fill = UiKit.El(track);
            fill.style.position = Position.Absolute;
            fill.style.left = 0f;
            fill.style.top = 0f;
            fill.style.bottom = 0f;
            fill.style.width = Length.Percent(0f);
            return track;
        }

        private static void Zone(VisualElement track, int from, int to, Color colour)
        {
            VisualElement zone = UiKit.El(track);
            zone.style.position = Position.Absolute;
            zone.style.top = 0f;
            zone.style.bottom = 0f;
            zone.style.left = Length.Percent(100f * from / AnglingFight.TensionMax);
            zone.style.width = Length.Percent(100f * (to - from) / AnglingFight.TensionMax);
            zone.style.backgroundColor = colour;
            zone.pickingMode = PickingMode.Ignore;
        }

        // ------------------------------------------------------------------ the fight, every frame

        public override void Observe()
        {
            if (Session == null || !Session.IsInGame) return;
            AnglingFightState state = Session.ServerState<AnglingFightState>(AnglingFightState.StateKey);
            _state = state;
            if (string.IsNullOrEmpty(state.Species))
            {
                _open = false;
                return;
            }
            if ((state.Id, state.Seed) != _key)
            {
                _key = (state.Id, state.Seed);
                _inputs.Clear();
                _sent = 0;
                _ms = 0f;
                _resultUntil = 0f;
                _revised = state.Revised;
                _species = Species(state.Species);
                // A fight already over when we first hear of it (logging in) isn't shown.
                _fight = state.Outcome == AnglingOutcome.None ? new AnglingFight(state.Fish(), state.Rig) : null;
                if (_fight != null) Window?.Activate(this);
            }
            if (_fight == null)
            {
                _open = false;
                return;
            }
            if (state.Outcome == AnglingOutcome.None)
            {
                // Real time, at most half a second of catching up a frame.
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
            else if (_resultUntil == 0f) _resultUntil = Time.unscaledTime + ResultSeconds;
            _open = state.Outcome == AnglingOutcome.None || Time.unscaledTime < _resultUntil;
        }

        private void Send()
        {
            while (_sent < _inputs.Count)
            {
                string batch = AnglingInputs.Encode(_sent, _inputs, _sent, IntentMessage.MaxLength, out int taken);
                if (taken == 0) break;
                Session.Intent(AnglingIntent, batch);
                _sent += taken;
            }
        }

        private byte ReadInput()
        {
            Keyboard k = Keyboard.current;
            bool typing = UiRoot.IsTyping;
            bool reel = _mouseReel || (!typing && k != null && k.spaceKey.isPressed);
            byte pull = AnglingInput.PullNone;
            if (!typing && k != null)
            {
                if (k.wKey.isPressed) pull = AnglingInput.PullUp;
                else if (k.aKey.isPressed) pull = AnglingInput.PullLeft;
                else if (k.dKey.isPressed) pull = AnglingInput.PullRight;
            }
            return AnglingInput.Make(reel, pull);
        }

        private AnglingSpeciesDef Species(string id)
        {
            if (Session.Content?.Angling == null) return null;
            foreach (AnglingSpeciesDef s in Session.Content.Angling.Species)
                if (s.Id == id) return s;
            return null;
        }

        // ------------------------------------------------------------------ drawing

        public override void Refresh()
        {
            if (_fight == null || _state == null) return;
            float width = Mathf.Max(100f, _water.resolvedStyle.width);
            float height = Mathf.Max(60f, _water.resolvedStyle.height);

            // The fish: further away is higher and smaller; it faces the way it swims.
            float near = 1f - Mathf.Clamp01((float)_fight.Distance / Mathf.Max(1, _fight.Distance0 * 2));
            float size = Mathf.Lerp(0.55f, 1.15f, near);
            float x = width * 0.5f - 28f, y = Mathf.Lerp(10f, height - 70f, near);
            float angle = 0f, sx = size, sy = size;
            AnglingMove move = _fight.Current;
            float t = Time.unscaledTime;
            switch (move)
            {
                case AnglingMove.Left: x -= width * 0.25f; sx = -size; break;
                case AnglingMove.Right: x += width * 0.25f; break;
                case AnglingMove.Dive: angle = 90f; y += 12f; sy = size * 0.8f; break;
                case AnglingMove.Run: angle = -90f; sx = sy = size * 0.75f; y -= 12f; break;
                case AnglingMove.Jump: y -= 40f * Mathf.Sin(Mathf.PI * Mathf.Repeat(t * 1.25f, 1f)); angle = -25f; break;
                case AnglingMove.Latch: x += Mathf.Sin(t * 40f) * 2f; angle = 80f; break;
            }
            if (_fight.InTell && _fight.TellVisible) angle += Mathf.Sin(t * 30f) * 8f;      // the flick before a move
            _fish.style.left = x;
            _fish.style.top = y;
            _fish.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            _fish.style.scale = new Scale(new Vector3(sx, sy, 1f));
            Color tint = _revised ? Color.Lerp(RevisedTint, new Color(0.9f, 0.6f, 1f), 0.5f + 0.5f * Mathf.Sin(t * 2.3f))
                : _species != null && ColorUtility.TryParseHtmlString(_species.Color, out Color c) ? Color.Lerp(c, Color.white, 0.35f) : UiKit.IconTint;
            _fish.style.unityBackgroundImageTintColor = tint;

            _tell.text = _fight.InTell && _fight.TellVisible ? UiKit.Colour(TellWord(_fight.Tell), UiKit.Value)
                : !_fight.TellVisible ? UiKit.Colour(_species?.Fight.Dark == true ? "Too dark to see its tells." : "It gives nothing away.", UiKit.Muted)
                : "";

            _tensionMark.style.left = Length.Percent(Mathf.Clamp(100f * _fight.Tension / AnglingFight.TensionMax, 0f, 99f));
            _tensionMark.style.backgroundColor = _fight.Tension >= AnglingFight.RedLine ? UiKit.Bad : _fight.Tension <= AnglingFight.SlackLine ? UiKit.Muted : UiKit.Value;
            _distanceFill.style.width = Length.Percent(100f * Mathf.Clamp01((float)_fight.Distance / Mathf.Max(1, _fight.Distance0)));
            _energyFill.style.width = Length.Percent(100f * Mathf.Clamp01((float)_fight.Energy / Mathf.Max(1, _fight.Energy0)));

            _result.text = _state.Outcome switch
            {
                AnglingOutcome.Landed => UiKit.Colour("Landed! ", UiKit.Good) + Weight(_state.Grams)
                    + (string.IsNullOrEmpty(_state.SizeClass) ? "" : UiKit.Colour("  (" + _state.SizeClass + ")", UiKit.Muted))
                    + (_state.PersonalBest ? UiKit.Colour("  New personal best!", UiKit.Value) : ""),
                AnglingOutcome.Snapped => UiKit.Colour("The line snapped.", UiKit.Bad),
                AnglingOutcome.HookFree => UiKit.Colour("The hook pulled free.", UiKit.Bad),
                AnglingOutcome.Escaped => UiKit.Colour("It got away.", UiKit.Bad),
                _ => _revised ? UiKit.Colour("Something is wrong with this fish.", RevisedTint) : "",
            };
        }

        private static string TellWord(AnglingMove move) => move switch
        {
            AnglingMove.Left => "<  it's turning left",
            AnglingMove.Right => "it's turning right  >",
            AnglingMove.Dive => "it's going to dive",
            AnglingMove.Run => "it's about to run!",
            AnglingMove.Jump => "it's going to jump!",
            AnglingMove.Latch => "it's making for the stones",
            _ => "",
        };

        private static string Weight(int grams) =>
            grams >= 1000 ? (grams / 1000f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " kg" : grams + " g";
    }
}
