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

        public static void Launch(Vector3 from, EntityView target, Color color, float duration)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.3f;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            go.GetComponent<Renderer>().SetPropertyBlock(block);

            var p = go.AddComponent<Projectile>();
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
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
