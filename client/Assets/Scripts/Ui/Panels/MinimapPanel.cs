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
    /// The minimap: a square top-down view that turns with the camera (dots: gold creatures,
    /// white players; click to walk), shaded at its edges, with the compass in one corner (click
    /// to face north), the clock in the other and the region's name along the bottom. Beneath it,
    /// a row of round buttons: the run toggle (running is free: there is no run energy), then the
    /// world map, journal, guide, settings and a menu of every window.
    /// </summary>
    public sealed class MinimapPanel : UiPanel
    {
        private const int TextureSize = 320;
        private const float ViewTiles = 24f;
        private const float MapPixels = 214f;
        private const float GameDayRealSeconds = 3600f;

        public override string Id => "minimap";
        public override string Title => "Minimap";
        public override string Icon => "map";
        public override string DefaultWindow => UiDefaultLayout.Minimap;
        public override bool Bare => true;
        public override Vector2? FixedSize => new Vector2(236f, 282f);

        private Camera _camera;
        private RenderTexture _texture;
        private VisualElement _map;
        private VisualElement _dots;
        private VisualElement _compass;
        private VisualElement _run;
        private Label _clock;
        private Label _region;
        private static Texture2D _edgeShade;
        private readonly List<VisualElement> _dotPool = new List<VisualElement>();
        private bool _fogWas;

        private float Yaw => Ui.cameraRig != null ? Ui.cameraRig.yaw : 0f;
        private float PixelsPerTile => MapPixels / 2f / ViewTiles;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            CreateCamera();
            VisualElement frame = UiKit.El(root, "hud");
            frame.style.flexGrow = 1;

            _map = UiKit.El(frame, "minimap");
            _map.style.width = _map.style.height = MapPixels;
            _map.style.backgroundImage = Background.FromRenderTexture(_texture);
            _map.RegisterCallback<PointerDownEvent>(OnMapClick);
            _dots = UiKit.El(_map);
            _dots.pickingMode = PickingMode.Ignore;
            _dots.style.position = Position.Absolute;
            _dots.style.left = _dots.style.top = _dots.style.right = _dots.style.bottom = 0f;
            VisualElement you = UiKit.El(_map, "minimap__dot");
            you.pickingMode = PickingMode.Ignore;
            you.style.left = you.style.top = MapPixels / 2f - 4f;
            you.style.width = you.style.height = 8f;
            you.style.borderTopLeftRadius = you.style.borderTopRightRadius = you.style.borderBottomLeftRadius = you.style.borderBottomRightRadius = 4f;
            you.style.backgroundColor = Color.white;

            VisualElement shade = UiKit.El(_map, "minimap__shade");
            shade.pickingMode = PickingMode.Ignore;
            shade.style.backgroundImage = EdgeShade();

            VisualElement compassButton = UiKit.El(_map, "minimap__compass");
            _compass = UiKit.Icon(compassButton, IconLibrary.Get("compass"), 28f);
            _compass.style.unityBackgroundImageTintColor = UiKit.IconTint;
            _compass.pickingMode = PickingMode.Ignore;
            compassButton.RegisterCallback<PointerDownEvent>(e =>
            {
                if (Ui.cameraRig != null) Ui.cameraRig.yaw = 0f;
                e.StopPropagation();
            });
            UiKit.Tooltip(compassButton, () => "Face north");

            _clock = UiKit.Text(_map, "", "minimap__clock");
            UiKit.Tooltip(_clock, () => UiKit.Title("Time in R'th") + "\nA day passes every real hour.");
            _region = UiKit.Text(_map, "", "minimap__region");
            _region.pickingMode = PickingMode.Ignore;

            VisualElement buttons = UiKit.El(frame, "minimap__orbs");
            _run = Orb(buttons, "run", () => UiKit.Title("Run") + "\nTwo tiles a tick instead of one.\n" + UiKit.Colour("Click or press R.", UiKit.Muted), () => Session.ToggleRun());
            Orb(buttons, "map", () => "World map (M)", () => Ui.ToggleModal(Ui.Modal<WorldMapModal>()));
            Orb(buttons, "journal", () => "Journal: quests, achievements, bestiary (J)", () => Ui.ToggleModal(Ui.Modal<JournalModal>()));
            Orb(buttons, "guide", () => "Guide: skills and abilities (G)", () => Ui.ToggleModal(Ui.Modal<GuideModal>()));
            Orb(buttons, "settings", () => "Settings", () => Ui.ToggleModal(Ui.Modal<SettingsModal>()));
            VisualElement menu = Orb(buttons, "menu", () => WindowsMenu.IsOpen ? null : "Interface: show or hide windows, lock, reset", null);
            menu.RegisterCallback<PointerDownEvent>(e =>
            {
                if (WindowsMenu.IsOpen) WindowsMenu.Close();
                else WindowsMenu.Open(Ui, e.position);
            });

            RenderPipelineManager.beginCameraRendering += BeforeRender;
            RenderPipelineManager.endCameraRendering += AfterRender;
        }

        /// <summary>A round button under the map.</summary>
        private static VisualElement Orb(VisualElement parent, string icon, Func<string> tooltip, Action onClick)
        {
            VisualElement orb = UiKit.El(parent, "orb");
            VisualElement i = UiKit.Icon(orb, IconLibrary.Get(icon), 18f);
            i.AddToClassList("orb__icon");
            i.pickingMode = PickingMode.Ignore;
            if (onClick != null) orb.RegisterCallback<ClickEvent>(_ => onClick());
            UiKit.Tooltip(orb, tooltip);
            return orb;
        }

        /// <summary>A soft dark fade at the map's edges (9-sliced, so the middle stays clear).</summary>
        private static Texture2D EdgeShade()
        {
            if (_edgeShade != null) return _edgeShade;
            const int size = 32;
            const float band = 14f;
            _edgeShade = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Minimap edge shade" };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)) + 0.5f;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - edge / band), 2f) * 0.7f;
                    _edgeShade.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }
            _edgeShade.Apply();
            return _edgeShade;
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
            foreach (EntityView e in World.Entities)
            {
                if (e == null || e == local || e.IsDying) continue;
                Vector2 offset = ToMap(e.transform.position - local.transform.position);
                if (Mathf.Abs(offset.x) > MapPixels / 2f - 4f || Mathf.Abs(offset.y) > MapPixels / 2f - 4f) continue;
                if (used == _dotPool.Count)
                {
                    VisualElement d = UiKit.El(_dots, "minimap__dot");
                    d.pickingMode = PickingMode.Ignore;
                    _dotPool.Add(d);
                }
                VisualElement dot = _dotPool[used++];
                dot.style.display = DisplayStyle.Flex;
                dot.style.left = MapPixels / 2f + offset.x - 3f;
                dot.style.top = MapPixels / 2f - offset.y - 3f;
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
            Vector2 offset = new Vector2(e.localPosition.x - MapPixels / 2f, MapPixels / 2f - e.localPosition.y);
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
