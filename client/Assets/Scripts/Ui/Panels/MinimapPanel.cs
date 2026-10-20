using System;
using System.Collections.Generic;
using Rth.Shared.Net;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The minimap: a top-down view around the player that turns with the camera, dots for
    /// creatures (yellow) and players (white), click to walk. Around it: the compass (click to
    /// face north), the run orb (click to toggle running; run energy comes with the breath
    /// skill), the world map, settings, a menu of every panel, and the clock.
    /// </summary>
    public sealed class MinimapPanel : UiPanel
    {
        private const int TextureSize = 256;
        private const float ViewTiles = 22f;
        private const float MapPixels = 180f;
        private const float GameDayRealSeconds = 3600f;

        public override string Id => "minimap";
        public override string Title => "Minimap";
        public override string DefaultWindow => UiDefaultLayout.Minimap;
        public override bool Bare => true;
        public override Vector2 MinSize => new Vector2(210f, 210f);

        private Camera _camera;
        private RenderTexture _texture;
        private VisualElement _map;
        private VisualElement _dots;
        private VisualElement _compassNeedle;
        private Label _run;
        private Label _clock;
        private readonly List<VisualElement> _dotPool = new List<VisualElement>();
        private bool _fogWas;

        private float Yaw => Ui.cameraRig != null ? Ui.cameraRig.yaw : 0f;
        private float PixelsPerTile => MapPixels / 2f / ViewTiles;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            CreateCamera();

            _map = UiKit.El(root, "minimap");
            _map.style.position = Position.Absolute;
            _map.style.left = 22f;
            _map.style.top = 22f;
            _map.style.backgroundImage = Background.FromRenderTexture(_texture);
            _map.RegisterCallback<PointerDownEvent>(OnMapClick);
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

            // The compass: the needle points north, so it turns as the camera turns.
            Label compass = Orb(root, "", 0f, 0f, "Face north", () => { if (Ui.cameraRig != null) Ui.cameraRig.yaw = 0f; });
            _compassNeedle = UiKit.Text(compass, "N");
            _compassNeedle.pickingMode = PickingMode.Ignore;
            _compassNeedle.style.position = Position.Absolute;
            _compassNeedle.style.left = _compassNeedle.style.right = _compassNeedle.style.top = _compassNeedle.style.bottom = 0f;
            _compassNeedle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _compassNeedle.style.color = new Color(1f, 0.35f, 0.25f);
            _compassNeedle.style.fontSize = 14;

            _run = Orb(root, "Run", 0f, 170f, "Toggle running (R)", () => Session.ToggleRun());
            Orb(root, "Map", 170f, 170f, "World map (M)", () => Ui.Toggle(Ui.Panel<WorldMapPanel>()));
            Orb(root, "Set", 170f, 0f, "Settings", () => Ui.Toggle(Ui.Panel<SettingsPanel>()));
            Label menu = Orb(root, "...", 170f, 42f, "Every interface: open or close", null);
            menu.RegisterCallback<PointerDownEvent>(e => Ui.Menu(e.position, "Interface", PanelOptions()));

            _clock = UiKit.Text(root, "", "muted");
            _clock.style.position = Position.Absolute;
            _clock.style.left = 0f;
            _clock.style.right = 0f;
            _clock.style.top = 206f;
            _clock.style.unityTextAlign = TextAnchor.MiddleCenter;
            _clock.style.fontSize = 10;
            UiKit.Tooltip(_clock, () => "The time of day in R'th. A day passes every real hour.");

            RenderPipelineManager.beginCameraRendering += BeforeRender;
            RenderPipelineManager.endCameraRendering += AfterRender;
        }

        private Label Orb(VisualElement parent, string text, float left, float top, string tooltip, Action onClick)
        {
            Label orb = UiKit.Text(parent, text, "orb");
            orb.style.left = left;
            orb.style.top = top;
            if (onClick != null) orb.RegisterCallback<ClickEvent>(_ => onClick());
            UiKit.Tooltip(orb, () => tooltip);
            return orb;
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
            _compassNeedle.style.rotate = new Rotate(-Yaw);
            _run.text = Session.Running ? "Run\non" : "Run\noff";
            _run.EnableInClassList("orb--on", Session.Running);

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
