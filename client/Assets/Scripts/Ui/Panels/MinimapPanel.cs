using System;
using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The minimap, laid out like the classic one: a round top-down view that turns with the
    /// camera (dots: yellow creatures, white players; click to walk), the compass on its rim
    /// (click to face north), orbs down its left side for blood, breath (opens Focuses) and run
    /// (click to toggle; run energy comes with the breath skill), buttons for the world map,
    /// settings and every panel, and the clock.
    /// </summary>
    public sealed class MinimapPanel : UiPanel
    {
        private const int TextureSize = 256;
        private const float ViewTiles = 22f;
        private const float MapLeft = 52f;
        private const float MapTop = 6f;
        private const float MapPixels = 164f; // inside the frame's 4px rim
        private const float GameDayRealSeconds = 3600f;

        public override string Id => "minimap";
        public override string Title => "Minimap";
        public override string Icon => "map";
        public override string DefaultWindow => UiDefaultLayout.Minimap;
        public override bool Bare => true;
        public override Vector2 MinSize => new Vector2(230f, 212f);

        private Camera _camera;
        private RenderTexture _texture;
        private VisualElement _map;
        private VisualElement _dots;
        private VisualElement _compass;
        private Orb _blood;
        private Orb _breath;
        private Orb _run;
        private Label _clock;
        private readonly List<VisualElement> _dotPool = new List<VisualElement>();
        private bool _fogWas;

        private float Yaw => Ui.cameraRig != null ? Ui.cameraRig.yaw : 0f;
        private float PixelsPerTile => MapPixels / 2f / ViewTiles;

        /// <summary>A classic orb: the number on a dark tab, and a round gauge that fills from the bottom.</summary>
        private sealed class Orb
        {
            public readonly VisualElement Root;
            public readonly VisualElement Circle;
            private readonly Label _number;
            private readonly VisualElement _fill;

            public Orb(VisualElement parent, float left, float top, string icon, Color colour)
            {
                Root = UiKit.El(parent, "orb");
                Root.style.left = left;
                Root.style.top = top;
                _number = UiKit.Text(Root, "", "orb__number");
                _number.pickingMode = PickingMode.Ignore;
                Circle = UiKit.El(Root, "orb__circle");
                _fill = UiKit.El(Circle, "orb__fill");
                _fill.style.backgroundColor = colour;
                _fill.pickingMode = PickingMode.Ignore;
                UiKit.Icon(Circle, IconLibrary.Get(icon), 20f, "orb__icon").pickingMode = PickingMode.Ignore;
            }

            public void Set(float fraction, string number, Color numberColour)
            {
                _fill.style.height = Length.Percent(Mathf.Clamp01(fraction) * 100f);
                _number.text = number;
                _number.style.color = numberColour;
            }
        }

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            CreateCamera();

            VisualElement frame = UiKit.El(root, "minimap-frame");
            frame.style.left = MapLeft;
            frame.style.top = MapTop;
            _map = UiKit.El(frame, "minimap");
            _map.style.backgroundImage = Background.FromRenderTexture(_texture);
            _map.RegisterCallback<PointerDownEvent>(OnMapClick);
            UiKit.Tooltip(_map, () => UiKit.Colour("Click to walk there.", UiKit.Muted));
            _dots = UiKit.El(_map);
            _dots.pickingMode = PickingMode.Ignore;
            _dots.style.position = Position.Absolute;
            _dots.style.left = _dots.style.top = _dots.style.right = _dots.style.bottom = 0f;
            VisualElement you = UiKit.El(_map, "minimap__dot");
            you.pickingMode = PickingMode.Ignore;
            you.style.left = MapPixels / 2f - 3f;
            you.style.top = MapPixels / 2f - 3f;
            you.style.width = you.style.height = 6f;
            you.style.backgroundColor = Color.white;

            // The compass sits on the rim; its face turns so north stays north.
            VisualElement compassButton = RoundButton(root, MapLeft - 8f, MapTop - 4f, null, "Face north", () => { if (Ui.cameraRig != null) Ui.cameraRig.yaw = 0f; });
            _compass = UiKit.Icon(compassButton, IconLibrary.Get("compass"), 26f);
            _compass.pickingMode = PickingMode.Ignore;

            _blood = new Orb(root, 0f, 44f, "blood", new Color(0.62f, 0.08f, 0.06f));
            UiKit.Tooltip(_blood.Circle, () => UiKit.Title("Blood") + "\nYour life.");
            _breath = new Orb(root, 0f, 84f, "focus", new Color(0.25f, 0.45f, 0.72f));
            _breath.Circle.RegisterCallback<ClickEvent>(_ => Ui.Open(Ui.Panel<FocusesPanel>()));
            UiKit.Tooltip(_breath.Circle, () => UiKit.Title("Breath") + "\nHeld focuses spend it.\n" + UiKit.Colour("Click to open Focuses.", UiKit.Muted));
            _run = new Orb(root, 0f, 124f, "run", new Color(0.62f, 0.5f, 0.12f));
            _run.Circle.RegisterCallback<ClickEvent>(_ => Session.ToggleRun());
            UiKit.Tooltip(_run.Circle, () => UiKit.Title("Run") + "\nRunning moves two tiles a tick.\n" + UiKit.Colour("Click or press R to toggle.", UiKit.Muted));

            RoundButton(root, MapLeft + MapPixels - 18f, MapTop + MapPixels - 14f, "map", "World map (M)", () => Ui.Toggle(Ui.Panel<WorldMapPanel>()));
            // Under the orbs, in the corner the map's curve leaves free.
            RoundButton(root, 6f, 166f, "settings", "Settings", () => Ui.Toggle(Ui.Panel<SettingsPanel>()));
            VisualElement menu = RoundButton(root, 40f, 180f, "menu", "Open or close any window", null);
            menu.RegisterCallback<PointerDownEvent>(e => Ui.Menu(e.position, "Interface", PanelOptions()));

            _clock = UiKit.Text(root, "", "clock");
            _clock.style.left = MapLeft + MapPixels / 2f - 18f;
            _clock.style.top = MapTop + MapPixels + 10f;
            UiKit.Tooltip(_clock, () => UiKit.Title("Time in R'th") + "\nA day passes every real hour.");

            RenderPipelineManager.beginCameraRendering += BeforeRender;
            RenderPipelineManager.endCameraRendering += AfterRender;
        }

        private VisualElement RoundButton(VisualElement parent, float left, float top, string icon, string tooltip, Action onClick)
        {
            VisualElement button = UiKit.El(parent, "round-button");
            button.style.left = left;
            button.style.top = top;
            if (icon != null) UiKit.Icon(button, IconLibrary.Get(icon), 20f).pickingMode = PickingMode.Ignore;
            if (onClick != null) button.RegisterCallback<ClickEvent>(_ => onClick());
            UiKit.Tooltip(button, () => tooltip);
            return button;
        }

        private void CreateCamera()
        {
            _texture = new RenderTexture(TextureSize, TextureSize, 16) { name = "Minimap" };
            var go = new GameObject("Minimap Camera");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _camera = go.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = ViewTiles;
            _camera.nearClipPlane = 1f;
            _camera.farClipPlane = 200f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.05f, 0.05f, 0.05f);
            _camera.targetTexture = _texture;
            _camera.enabled = false;
        }

        // The minimap sees from far overhead, where the distance fog would grey it all out.
        private void BeforeRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _camera) return;
            _fogWas = RenderSettings.fog;
            RenderSettings.fog = false;
        }

        private void AfterRender(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _camera) RenderSettings.fog = _fogWas;
        }

        public override void Refresh()
        {
            EntityView local = World.Local;
            _camera.enabled = local != null;
            if (local != null)
            {
                Vector3 p = local.transform.position;
                _camera.transform.SetPositionAndRotation(new Vector3(p.x, p.y + 80f, p.z), Quaternion.Euler(90f, Yaw, 0f));
                UpdateDots(local);
            }
            _compass.style.rotate = new Rotate(-Yaw);

            PlayerStateMessage state = Session.PlayerState;
            if (state != null)
            {
                float blood = state.Blood / (float)Mathf.Max(1, state.MaxBlood);
                _blood.Set(blood, Mathf.RoundToInt(blood * 100f).ToString(), Color.Lerp(UiKit.Bad, UiKit.Good, blood));
                float breath = state.Breath / (float)Mathf.Max(1, state.MaxBreath);
                _breath.Set(breath, Mathf.RoundToInt(breath * 100f).ToString(), state.Deflect != CombatStyle.None ? new Color(0.6f, 0.85f, 1f) : Color.Lerp(UiKit.Bad, UiKit.Good, breath));
            }
            _run.Set(Session.Running ? 1f : 0f, Session.Running ? "On" : "Off", Session.Running ? UiKit.Value : UiKit.Muted);

            double seconds = (DateTime.UtcNow - DateTime.UtcNow.Date).TotalSeconds % GameDayRealSeconds;
            int minutes = (int)(seconds / GameDayRealSeconds * 24 * 60);
            _clock.text = (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
        }

        private void UpdateDots(EntityView local)
        {
            int used = 0;
            float radius = MapPixels / 2f - 3f;
            foreach (EntityView e in World.Entities)
            {
                if (e == null || e == local || e.IsDying) continue;
                Vector2 offset = ToMap(e.transform.position - local.transform.position);
                if (offset.magnitude > radius) continue;
                if (used == _dotPool.Count)
                {
                    VisualElement d = UiKit.El(_dots, "minimap__dot");
                    d.pickingMode = PickingMode.Ignore;
                    _dotPool.Add(d);
                }
                VisualElement dot = _dotPool[used++];
                dot.style.display = DisplayStyle.Flex;
                dot.style.left = MapPixels / 2f + offset.x - 2.5f;
                dot.style.top = MapPixels / 2f - offset.y - 2.5f;
                dot.style.backgroundColor = e.Kind == EntityKind.Player ? Color.white : new Color(1f, 0.9f, 0.1f);
            }
            for (int i = used; i < _dotPool.Count; i++) _dotPool[i].style.display = DisplayStyle.None;
        }

        /// <summary>A world offset to minimap pixels from the centre (x right, y up), turned with the camera.</summary>
        private Vector2 ToMap(Vector3 delta)
        {
            float yaw = Yaw * Mathf.Deg2Rad;
            var up = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
            var d = new Vector2(delta.x, delta.z);
            return new Vector2(Vector2.Dot(d, right), Vector2.Dot(d, up)) * PixelsPerTile;
        }

        private void OnMapClick(PointerDownEvent e)
        {
            EntityView local = World.Local;
            if (local == null || e.button != 0) return;
            Vector2 offset = new Vector2(e.localPosition.x - MapPixels / 2f, MapPixels / 2f - e.localPosition.y);
            if (offset.magnitude > MapPixels / 2f) return;
            float yaw = Yaw * Mathf.Deg2Rad;
            var up = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
            Vector2 world = (right * offset.x + up * offset.y) / PixelsPerTile;
            Vector3 target = local.transform.position + new Vector3(world.x, 0f, world.y);
            Session.WalkTo(TileMath.FromWorld(target));
            e.StopPropagation();
        }

        private List<(string Label, Action Action)> PanelOptions()
        {
            var options = new List<(string Label, Action Action)>();
            foreach (UiPanel panel in Ui.Panels)
            {
                UiPanel p = panel;
                options.Add(((p.Window == null ? "Open " : "Close ") + p.Title, () => Ui.Toggle(p)));
            }
            options.Add(("Reset layout", () => Ui.ResetLayout()));
            return options;
        }
    }
}
