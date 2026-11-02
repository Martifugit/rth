using System;
using System.Collections.Generic;
using Rth.Client.Ui.Modals;
using Rth.Shared.Net;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using TileCoord = global::Rth.Shared.World.TileCoord;

namespace Rth.Client.Ui.Panels
{
    /// <summary>
    /// The minimap: a round top-down view that turns with the camera (dots: gold creatures, white
    /// players; click to walk), set in a bronze bezel (the skin's minimap-bezel, drawn by
    /// tools/ui-skin/make_skin.py), with a flag where a click sent you and, for the marker set on the
    /// world map, a pin; while either is further off than the map shows, a small, softly blinking
    /// arrow at the edge points its way. The compass sits in a medallion at the top left (click to face
    /// north), the clock in one at the top right, the region's name low on the map. Round buttons
    /// sit in a cradle around the bottom of the ring: run (running is free: there is no run
    /// energy), the world map, journal, guide, settings and a menu of every window.
    /// </summary>
    public sealed class MinimapPanel : UiPanel
    {
        // Geometry in panel pixels. The bezel texture is drawn to the design numbers (a 240-pixel
        // panel): change make_skin.py (MM_SIZE, MM_C, MM_MAP_R, MM_RING_OUT, MM_ORB_RING,
        // MM_ORB_ANGLES, MM_CRADLE, MM_MEDAL_RING, the two medallion angles) together with them.
        // Everything is then multiplied by Scale, so the whole piece (map, ring, medallions, buttons)
        // grows together; the bezel is drawn at 512 pixels, so it stays crisp stretched to the
        // larger size.
        private const float Scale = 270f / 240f;
        private const float Size = 240f * Scale;
        private const float Centre = 120f * Scale;
        private const float MapRadius = 88f * Scale;
        private const float RingOuter = 100.5f * Scale;
        private const float OrbRing = 105f * Scale;
        private const float OrbSize = 30f * Scale;
        private const float OrbIconSize = 17f * Scale;
        private static readonly float[] OrbAngles = { 240f, 216f, 192f, 168f, 144f, 120f };
        private const float CradleInner = 92f * Scale, CradleOuter = 118f * Scale, CradleFrom = 108f, CradleTo = 252f;
        private const float MedalRing = 97f * Scale;
        private const float MedalSize = 40f * Scale;
        private const float CompassSize = 30f * Scale;
        private const float CompassAngle = 315f;
        private const float ClockAngle = 45f;

        private const int TextureSize = 320;
        private const float ViewTiles = 20f;

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
        private Marks _marks;
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

        /// <summary>Rounds every corner by a radius (the map's circle mask, the medallions' hover ring), which scales with the piece.</summary>
        private static void Rounded(VisualElement e, float radius) =>
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;

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
            frame.style.width = frame.style.height = Size;

            // The map, clipped to a circle: a round border radius with overflow hidden masks its children.
            _map = Round(frame, MapRadius * 2f, "minimap");
            Place(_map, new Vector2(Centre, Centre), MapRadius * 2f);
            Rounded(_map, MapRadius);
            _map.RegisterCallback<PointerDownEvent>(OnMapClick);
            VisualElement view = UiKit.El(_map, "minimap__view");
            view.pickingMode = PickingMode.Ignore;
            view.style.backgroundImage = Background.FromRenderTexture(_texture);
            _dots = UiKit.El(_map, "minimap__layer");
            _dots.pickingMode = PickingMode.Ignore;
            _marks = new Marks(this);
            _map.Add(_marks);
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
            Rounded(compassButton, MedalSize / 2f);
            _compass = UiKit.Icon(compassButton, IconLibrary.Get("compass"), CompassSize);
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
            Rounded(clockMedal, MedalSize / 2f);
            _clock = UiKit.Text(clockMedal, "", "minimap__clock");
            _clock.pickingMode = PickingMode.Ignore;
            UiKit.Tooltip(clockMedal, () => UiKit.Title("Time in R'th") + "\nA day in R'th is a real day.");

            _run = Orb(frame, OrbAngles[0], "run", () => UiKit.Title("Run") + "\nTwo tiles a tick instead of one.\n" + UiKit.Colour("Click or press R.", UiKit.Muted), () => Session.ToggleRun());
            Orb(frame, OrbAngles[1], "map", () => "World map (M)", () => Ui.ToggleModal(Ui.Modal<WorldMapModal>()));
            Orb(frame, OrbAngles[2], "journal", () => "Journal (" + Ui.Modal<JournalModal>().Hotkey + ")", () => Ui.ToggleModal(Ui.Modal<JournalModal>()));
            Orb(frame, OrbAngles[3], "guide", () => "Guides (" + Ui.Modal<GuideModal>().Hotkey + ")", () => Ui.ToggleModal(Ui.Modal<GuideModal>()));
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
            VisualElement i = UiKit.Icon(orb, IconLibrary.Get(icon), OrbIconSize);
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
                ArriveAtWaypoint(local);
            }
            _marks.MarkDirtyRepaint();
            _compass.style.rotate = new Rotate(-Yaw);
            _run.EnableInClassList("orb--on", Session.Running);
            string region = Ui.regions != null && Ui.regions.Current != null ? Ui.regions.Current.Title : "";
            _region.text = region;
            _region.style.display = string.IsNullOrEmpty(region) ? DisplayStyle.None : DisplayStyle.Flex;

            // A day in R'th is a real day (owner): the clock keeps the time of day where you are.
            DateTime now = DateTime.Now;
            _clock.text = now.Hour.ToString("00") + ":" + now.Minute.ToString("00");
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

        /// <summary>The world map's marker is done with once you reach it.</summary>
        private void ArriveAtWaypoint(EntityView local)
        {
            TileCoord? waypoint = Session.Waypoint;
            if (waypoint.HasValue && Mathf.Abs(local.Tile.X - waypoint.Value.X) <= 2 && Mathf.Abs(local.Tile.Y - waypoint.Value.Y) <= 2)
                Session.SetWaypoint(null);
        }

        /// <summary>A tile's centre in the map's pixels (top-left origin), from where the local player stands.</summary>
        private Vector2 MapPoint(EntityView local, TileCoord tile)
        {
            Vector3 p = local.transform.position;
            Vector2 offset = ToMap(new Vector3(tile.X + 0.5f - p.x, 0f, tile.Y + 0.5f - p.z));
            return new Vector2(MapRadius + offset.x, MapRadius - offset.y);
        }

        /// <summary>
        /// The walk flag and the world map's marker, drawn over the dots. The flag is bold, so you
        /// can see where you're going at a glance; the arrow at the edge for either one out of sight
        /// stays small and fades in and out, so it guides without nagging.
        /// </summary>
        private sealed class Marks : VisualElement
        {
            private static readonly Color FlagRed = new Color32(226, 48, 36, 255);
            private static readonly Color Pole = new Color32(245, 236, 214, 255);
            private static readonly Color Outline = new Color32(16, 10, 6, 255);
            private static readonly Color Pin = new Color32(240, 196, 80, 255);

            private readonly MinimapPanel _panel;

            public Marks(MinimapPanel panel)
            {
                _panel = panel;
                pickingMode = PickingMode.Ignore;
                AddToClassList("minimap__layer");
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                EntityView local = _panel.World != null ? _panel.World.Local : null;
                if (local == null || _panel.Session == null) return;
                Painter2D p = context.painter2D;
                p.lineJoin = LineJoin.Round;
                Vector2 centre = new Vector2(MapRadius, MapRadius);

                // Either marker out of sight shows as the same small arrow at the edge, slowly
                // fading in and out: gold for the world map's marker, red for the walk flag.
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / EdgeArrowSeconds);
                float alpha = 0.45f + 0.5f * pulse;

                TileCoord? waypoint = _panel.Session.Waypoint;
                if (waypoint.HasValue)
                {
                    Vector2 at = _panel.MapPoint(local, waypoint.Value);
                    Vector2 d = at - centre;
                    if (d.sqrMagnitude <= EdgeArrowRadius * EdgeArrowRadius) PinAt(p, at);
                    else ArrowAt(p, centre + d.normalized * EdgeArrowRadius, d.normalized, Pin, alpha);
                }

                TileCoord? flag = _panel.Session.WalkFlag;
                if (flag.HasValue)
                {
                    Vector2 at = _panel.MapPoint(local, flag.Value);
                    Vector2 d = at - centre;
                    if (d.sqrMagnitude <= (MapRadius - 4f) * (MapRadius - 4f)) FlagAt(p, at);
                    else ArrowAt(p, centre + d.normalized * EdgeArrowRadius, d.normalized, FlagRed, alpha);
                }
            }

            /// <summary>Where the edge arrows' tips sit: just inside the ring.</summary>
            private const float EdgeArrowRadius = MapRadius - 8f;

            /// <summary>One slow fade in and out of an edge arrow.</summary>
            private const float EdgeArrowSeconds = 1.8f;

            /// <summary>A flag on a pole, its foot on the tile.</summary>
            private static void FlagAt(Painter2D p, Vector2 foot)
            {
                p.lineCap = LineCap.Round;
                p.strokeColor = Outline;
                p.lineWidth = 3.5f;
                p.BeginPath();
                p.MoveTo(foot);
                p.LineTo(foot + new Vector2(0f, -15f));
                p.Stroke();
                p.strokeColor = Pole;
                p.lineWidth = 1.5f;
                p.BeginPath();
                p.MoveTo(foot);
                p.LineTo(foot + new Vector2(0f, -15f));
                p.Stroke();

                p.fillColor = FlagRed;
                p.strokeColor = Outline;
                p.lineWidth = 1.5f;
                p.BeginPath();
                p.MoveTo(foot + new Vector2(0.5f, -15f));
                p.LineTo(foot + new Vector2(10f, -11.5f));
                p.LineTo(foot + new Vector2(0.5f, -8f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }

            /// <summary>The marker within sight: a small gold diamond.</summary>
            private static void PinAt(Painter2D p, Vector2 c)
            {
                p.fillColor = Pin;
                p.strokeColor = Outline;
                p.lineWidth = 1.5f;
                p.BeginPath();
                p.MoveTo(c + new Vector2(0f, -6f));
                p.LineTo(c + new Vector2(4.5f, 0f));
                p.LineTo(c + new Vector2(0f, 6f));
                p.LineTo(c + new Vector2(-4.5f, 0f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }

            /// <summary>A marker out of sight: a small arrow at the edge pointing its way (y down).</summary>
            private static void ArrowAt(Painter2D p, Vector2 tip, Vector2 dir, Color colour, float alpha)
            {
                Vector2 side = new Vector2(-dir.y, dir.x);
                // Large enough to find at a glance (owner: the first was too small), still a quiet pulse.
                Vector2 back = tip - dir * 15f;
                p.fillColor = new Color(colour.r, colour.g, colour.b, alpha);
                p.strokeColor = new Color(Outline.r, Outline.g, Outline.b, Mathf.Min(1f, alpha + 0.2f));
                p.lineWidth = 1.6f;
                p.BeginPath();
                p.MoveTo(tip);
                p.LineTo(back + side * 9f);
                p.LineTo(back + dir * 4.5f);
                p.LineTo(back - side * 9f);
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }
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
