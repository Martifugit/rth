using UnityEngine;

namespace Rth.Client
{
    /// <summary>A blob of humour flying at a target. Purely visual: the hit was already decided by the server.</summary>
    public sealed class Projectile : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private Vector3 _start;
        private EntityView _target;
        private Vector3 _lastTargetPoint;
        private float _duration;
        private float _age;
        private float _glow;
        private Color _color;

        /// <summary>
        /// Throws a blob at a target. <paramref name="glow"/> above 0 gives it a light of its own and
        /// a flash where it lands (a boss's strike).
        /// </summary>
        public static void Launch(Vector3 from, EntityView target, Color color, float duration, float size = 0.3f, float glow = 0f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = from;
            go.transform.localScale = Vector3.one * size;
            if (glow > 0f)
            {
                var light = new GameObject("Glow").AddComponent<Light>();
                light.transform.SetParent(go.transform, false);
                light.type = LightType.Point;
                light.color = color;
                light.intensity = glow;
                light.range = 4f;
            }
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            go.GetComponent<Renderer>().SetPropertyBlock(block);

            var p = go.AddComponent<Projectile>();
            p._glow = glow;
            p._color = color;
            p._start = from;
            p._target = target;
            p._duration = Mathf.Max(0.1f, duration);
            p._lastTargetPoint = TargetPoint(target);
        }

        private static Vector3 TargetPoint(EntityView target) => target.transform.position + Vector3.up * target.Height * 0.6f;

        private void Update()
        {
            if (_target != null) _lastTargetPoint = TargetPoint(_target);
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _duration);
            Vector3 pos = Vector3.Lerp(_start, _lastTargetPoint, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 0.6f; // a little arc
            transform.position = pos;
            if (t < 1f) return;
            if (_glow > 0f) ImpactFlash.At(_lastTargetPoint, _color, _glow * 3f);
            Destroy(gameObject);
        }
    }

    /// <summary>A burst of light where something landed, gone in a moment.</summary>
    public sealed class ImpactFlash : MonoBehaviour
    {
        private Light _light;
        private float _age, _peak;

        public static void At(Vector3 where, Color color, float intensity)
        {
            var go = new GameObject("Impact flash");
            go.transform.position = where;
            var flash = go.AddComponent<ImpactFlash>();
            flash._light = go.AddComponent<Light>();
            flash._light.type = LightType.Point;
            flash._light.color = color;
            flash._light.range = 6f;
            flash._peak = intensity;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            _light.intensity = _peak * Mathf.Max(0f, 1f - _age / 0.35f);
            if (_age > 0.35f) Destroy(gameObject);
        }
    }
}
