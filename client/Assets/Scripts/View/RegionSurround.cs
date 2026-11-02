using UnityEngine;
using UnityEngine.Rendering;

namespace Rth.Client
{
    /// <summary>
    /// What lies beyond an instanced region's edges (its data's <c>surround</c>): endless water, so a
    /// place set in a lake or a sea (the realm of the dead) never shows the end of the world. While the
    /// player is in or near the region, a wide sheet of water (Water.shader, the region's colours) follows
    /// them just under the region's own water level; the region's own water and ground draw over it,
    /// and the fog swallows its far edge. Set on the MapArea by Sync World.
    /// </summary>
    public sealed class RegionSurround : MonoBehaviour
    {
        [Tooltip("The region's rectangle in tiles: x, y, width, height.")]
        public RectInt area;
        public Color water = new Color(0.04f, 0.09f, 0.1f);
        [Tooltip("The water's level (the region's water surface is at 0).")]
        public float level;

        private const float Size = 600f, Near = 24f;

        private GameObject _sheet;
        private GameSession _session;

        private void OnEnable()
        {
            _sheet = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _sheet.name = "Endless water";
            _sheet.hideFlags = HideFlags.DontSave;
            Destroy(_sheet.GetComponent<Collider>());
            _sheet.transform.localScale = new Vector3(Size / 10f, 1f, Size / 10f);
            var renderer = _sheet.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Shader shader = Resources.Load<Shader>("Shaders/Water");
            var material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit")) { name = "Endless water", hideFlags = HideFlags.DontSave };
            material.SetColor("_Color", water);
            material.SetColor("_Shallow", water);
            material.SetColor("_BaseColor", water);
            renderer.sharedMaterial = material;
            _sheet.SetActive(false);
        }

        private void OnDisable()
        {
            if (_sheet != null)
            {
                Destroy(_sheet.GetComponent<MeshRenderer>().sharedMaterial);
                Destroy(_sheet);
            }
        }

        private void LateUpdate()
        {
            if (_sheet == null) return;
            if (_session == null) _session = FindAnyObjectByType<GameSession>();
            Vector3? at = PlayerAt();
            bool near = at is Vector3 p &&
                        p.x > area.x - Near && p.x < area.xMax + Near && p.z > area.y - Near && p.z < area.yMax + Near;
            _sheet.SetActive(near);
            if (!near) return;
            Vector3 c = at.Value;
            // Snapped to whole tiles, so its ripples don't slide as it follows.
            _sheet.transform.position = new Vector3(Mathf.Round(c.x), level - 0.03f, Mathf.Round(c.z));
        }

        private Vector3? PlayerAt()
        {
            if (_session == null || !_session.IsInGame || _session.world == null) return null;
            var me = _session.world.Find(_session.LocalPlayerId);
            if (me == null) return null;
            return new Vector3(me.Tile.X + 0.5f, 0f, me.Tile.Y + 0.5f);
        }
    }
}
