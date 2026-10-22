using System;
using System.Collections.Generic;
using Rth.Client.Ui.Modals;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The minimap: a round top-down view that turns with the camera (dots: gold creatures, white
    /// players; click to walk), set in a bronze bezel (the skin's minimap-bezel, drawn by
    /// tools/ui-skin/make_skin.py). The compass sits in a medallion at the top left (click to face
    /// north), the clock in one at the top right, the region's name low on the map. Round buttons
    /// sit in a cradle around the bottom of the ring: run (running is free: there is no run
    /// energy), the world map, journal, guide, settings and a menu of every window.
    /// </summary>
    public sealed class MinimapPanel : UiPanel
    {
        // Geometry in panel pixels. The bezel texture is drawn to these numbers: change make_skin.py
        // (MM_SIZE, MM_C, MM_MAP_R, MM_RING_OUT, MM_ORB_RING, MM_ORB_ANGLES, MM_CRADLE,
        // MM_MEDAL_RING, the two medallion angles) together with them.
        private const float Size = 240f;
        private const float Centre = 120f;
        private const float MapRadius = 88f;
        private const float RingOuter = 100.5f;
        private const float OrbRing = 105f;
        private const float OrbSize = 30f;
        private static readonly float[] OrbAngles = { 240f, 216f, 192f, 168f, 144f, 120f };
        private const float CradleInner = 92f, CradleOuter = 118f, CradleFrom = 108f, CradleTo = 252f;
        private const float MedalRing = 97f;
        private const float MedalSize = 40f;
        private const float CompassAngle = 315f;
        private const float ClockAngle = 45f;

        private const int TextureSize = 320;
        private const float ViewTiles = 20f;
        private const float GameDayRealSeconds = 3600f;

        public override string Id => "minimap";
        public override string Title => "Minimap";
        public override string Icon => "map";
        public override string DefaultWindow => UiDefaultLayout.Minimap;
        public override bool Bare => true;
        public override Vector2? FixedSize => new Vector2(Size, Size);

        private Camera _camera;
        private RenderTexture _texture;
        private VisualElement _map;
        private VisualElement _dots;
        private VisualElement _compass;
        private VisualElement _run;
        private Label _clock;
        private Label _region;
        private readonly List<VisualElement> _dotPool = new List<VisualElement>();
        private bool _fogWas;

        private float Yaw => Ui.cameraRig != null ? Ui.cameraRig.yaw : 0f;
        private static float PixelsPerTile => MapRadius / ViewTiles;

        /// <summary>
        /// An element that takes the pointer only where a test says (a circle, the ring), so the
        /// bezel's empty corners let clicks through to the world.
        /// </summary>
        private sealed class Shaped : VisualElement
        {
            private readonly Func<Vector2, bool> _contains;
            public Shaped(Func<Vector2, bool> contains) => _contains = contains;
            public override bool ContainsPoint(Vector2 localPoint) => _contains(localPoint);
        }

        /// <summary>A round element of a size that only takes the pointer inside its circle.</summary>
        private static VisualElement Round(VisualElement parent, float size, params string[] classes)
        {
            float r = size / 2f;
            var e = new Shaped(p => (p - new Vector2(r, r)).sqrMagnitude <= r * r);
            foreach (string c in classes) e.AddToClassList(c);
            e.style.width = e.style.height = size;
            parent.Add(e);
            return e;
        }

        /// <summary>A point on the bezel: an angle clockwise from the top and a radius, in panel pixels.</summary>
        private static Vector2 Polar(float angle, float radius)
        {
            float a = angle * Mathf.Deg2Rad;
            return new Vector2(Centre + radius * Mathf.Sin(a), Centre - radius * Mathf.Cos(a));
        }

        private static void Place(VisualElement e, Vector2 centre, float size)
        {
            e.style.position = Position.Absolute;
            e.style.left = centre.x - size / 2f;
            e.style.top = centre.y - size / 2f;
        }

        /// <summary>The bezel takes the pointer on its ring and the buttons' cradle, not over the map or in the corners.</summary>
        private static bool OnBezel(Vector2 p)
        {
            Vector2 d = p - new Vector2(Centre, Centre);
            float r = d.magnitude;
            if (r >= MapRadius && r <= RingOuter + 1f) return true;
            float angle = Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            return r >= CradleInner && r <= CradleOuter && angle >= CradleFrom && angle <= CradleTo;
        }

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            CreateCamera();
            VisualElement frame = UiKit.El(root, "minimap-frame");
            frame.pickingMode = PickingMode.Ignore;

            // The map, clipped to a circle: a round border radius with overflow hidden masks its children.
            _map = Round(frame, MapRadius * 2f, "minimap");
            Place(_map, new Vector2(Centre, Centre), MapRadius * 2f);
            _map.RegisterCallback<PointerDownEvent>(OnMapClick);
            VisualElement view = UiKit.El(_map, "minimap__view");
            view.pickingMode = PickingMode.Ignore;
            view.style.backgroundImage = Background.FromRenderTexture(_texture);
            _dots = UiKit.El(_map, "minimap__layer");
            _dots.pickingMode = PickingMode.Ignore;
            VisualElement you = UiKit.El(_map, "minimap__dot", "minimap__you");
            you.pickingMode = PickingMode.Ignore;
            you.style.left = you.style.top = MapRadius - 4f;
            _region = UiKit.Text(_map, "", "minimap__region");
            _region.pickingMode = PickingMode.Ignore;

            // The bezel over it: ring, inner shadow, the medallions' mounts and the cradle.
            var bezel = new Shaped(OnBezel);
            bezel.AddToClassList("minimap__bezel");
            frame.Add(bezel);

            VisualElement compassButton = Round(frame, MedalSize, "minimap__medal");
            Place(compassButton, Polar(CompassAngle, MedalRing), MedalSize);
            _compass = UiKit.Icon(compassButton, IconLibrary.Get("compass"), 30f);
            _compass.style.unityBackgroundImageTintColor = UiKit.IconTint;
            _compass.pickingMode = PickingMode.Ignore;
            compassButton.RegisterCallback<PointerDownEvent>(e =>
            {
                if (Ui.cameraRig != null) Ui.cameraRig.yaw = 0f;
                e.StopPropagation();
            });
            UiKit.Tooltip(compassButton, () => "Face north");

            VisualElement clockMedal = Round(frame, MedalSize, "minimap__medal", "minimap__medal--clock");
            Place(clockMedal, Polar(ClockAngle, MedalRing), MedalSize);
            _clock = UiKit.Text(clockMedal, "", "minimap__clock");
            _clock.pickingMode = PickingMode.Ignore;
            UiKit.Tooltip(clockMedal, () => UiKit.Title("Time in R'th") + "\nA day passes every real hour.");

            _run = Orb(frame, OrbAngles[0], "run", () => UiKit.Title("Run") + "\nTwo tiles a tick instead of one.\n" + UiKit.Colour("Click or press R.", UiKit.Muted), () => Session.ToggleRun());
            Orb(frame, OrbAngles[1], "map", () => "World map (M)", () => Ui.ToggleModal(Ui.Modal<WorldMapModal>()));
            Orb(frame, OrbAngles[2], "journal", () => "Journal: quests, achievements, bestiary (J)", () => Ui.ToggleModal(Ui.Modal<JournalModal>()));
            Orb(frame, OrbAngles[3], "guide", () => "Guide: skills and abilities (G)", () => Ui.ToggleModal(Ui.Modal<GuideModal>()));
            Orb(frame, OrbAngles[4], "settings", () => "Settings", () => Ui.ToggleModal(Ui.Modal<SettingsModal>()));
            VisualElement menu = Orb(frame, OrbAngles[5], "menu", () => WindowsMenu.IsOpen ? null : "Interface: show or hide windows, lock, reset", null);
            menu.RegisterCallback<PointerDownEvent>(e =>
            {
                if (WindowsMenu.IsOpen) WindowsMenu.Close();
                else WindowsMenu.Open(Ui, e.position);
            });

            RenderPipelineManager.beginCameraRendering += BeforeRender;
            RenderPipelineManager.endCameraRendering += AfterRender;
        }

        /// <summary>A round button in the cradle under the map.</summary>
        private static VisualElement Orb(VisualElement parent, float angle, string icon, Func<string> tooltip, Action onClick)
        {
            VisualElement orb = Round(parent, OrbSize, "orb");
            Place(orb, Polar(angle, OrbRing), OrbSize);
            VisualElement i = UiKit.Icon(orb, IconLibrary.Get(icon), 17f);
            i.AddToClassList("orb__icon");
            i.pickingMode = PickingMode.Ignore;
            if (onClick != null) orb.RegisterCallback<ClickEvent>(_ => onClick());
            UiKit.Tooltip(orb, tooltip);
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
            _compass.style.rotate = new Rotate(-Yaw);
            _run.EnableInClassList("orb--on", Session.Running);
            string region = Ui.regions != null && Ui.regions.Current != null ? Ui.regions.Current.Title : "";
            _region.text = region;
            _region.style.display = string.IsNullOrEmpty(region) ? DisplayStyle.None : DisplayStyle.Flex;

            double seconds = (DateTime.UtcNow - DateTime.UtcNow.Date).TotalSeconds % GameDayRealSeconds;
            int minutes = (int)(seconds / GameDayRealSeconds * 24 * 60);
            _clock.text = (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
        }

        private void UpdateDots(EntityView local)
        {
            int used = 0;
            float inside = MapRadius - 6f;
            foreach (EntityView e in World.Entities)
            {
                if (e == null || e == local || e.IsDying) continue;
                Vector2 offset = ToMap(e.transform.position - local.transform.position);
                if (offset.sqrMagnitude > inside * inside) continue;
                if (used == _dotPool.Count)
                {
                    VisualElement d = UiKit.El(_dots, "minimap__dot");
                    d.pickingMode = PickingMode.Ignore;
                    _dotPool.Add(d);
                }
                VisualElement dot = _dotPool[used++];
                dot.style.display = DisplayStyle.Flex;
                dot.style.left = MapRadius + offset.x - 3f;
                dot.style.top = MapRadius - offset.y - 3f;
                dot.style.backgroundColor = e.Kind == EntityKind.Player ? Color.white : new Color(1f, 0.85f, 0.2f);
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
            Vector2 offset = new Vector2(e.localPosition.x - MapRadius, MapRadius - e.localPosition.y);
            float yaw = Yaw * Mathf.Deg2Rad;
            var up = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
            Vector2 world = (right * offset.x + up * offset.y) / PixelsPerTile;
            Vector3 target = local.transform.position + new Vector3(world.x, 0f, world.y);
            Session.WalkTo(TileMath.FromWorld(target));
            e.StopPropagation();
        }
    }
}
